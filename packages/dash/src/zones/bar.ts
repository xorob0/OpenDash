/**
 * The bar: settled values, and the strip that hides what the game does not expose.
 *
 * The bar is the one region of a face that is **not a zone**. It does not cycle, and that is what
 * earns it the space: it carries what does not change during a lap, so a driver reads it between
 * corners rather than at speed.
 *
 * Two fields at each end, from a catalogue of ten, and the car settings strip between them. The
 * strip is the part worth care. It draws what the game exposes and hides what it does not, because
 * a strip drawing an empty box for a setting iRacing has no property for is worse than a narrower
 * strip — and iRacing really does omit `dcTractionControl` and `dcABS` on cars without the
 * controls, which is what the `quali` capture scenario is for.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { BAR_FIELDS, BAR_SLOTS, zone as zoneSetting, type BarSlot, type FaceSize } from '../contract.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { boxSlack, canvasBaseline, canvasYForBaseline, cells, monoWidth, type Chars } from '../design/metrics.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { rank, type RankMember } from '../second/rank.ts';
import type { BarScale } from './layout.ts';
import {
  CHARS,
  absLevel,
  airTemperature,
  antiRollFront,
  antiRollRear,
  brakeBias,
  classOpponentCount,
  clock,
  currentLap,
  fuelMixture,
  incidents,
  localClock,
  opponentCount,
  playerClass,
  sessionTimeLeft,
  simClock,
  tcLevel,
  totalLaps,
  roadTemperature,
} from '../second/values.ts';
import { ds } from '../tokens.ts';

const { fmt, isnull, num, str, iff, eq, game, concat, raw, driver, playerPosition } = ncalc;

/**
 * What every artboard draws the same way, whatever the bar's height: twenty pixels of side
 * padding, a 13 px row for the 15 px label, five pixels under it, and six between a value and the
 * dimmer denominator after it. The label is centred in a row two pixels shorter than its type, so
 * the row rather than the font size is what the block is measured with.
 *
 * The sizes that do change with the face -- the value, the denominator, the strip column and the
 * gap -- are `BarScale`, read off the face's own artboard.
 */
const PAD_X = 20;
const LABEL_ROW = 13;
const LABEL_GAP = 5;
const DENOMINATOR_GAP = 6;

/** One field of the bar's catalogue: a label, a value and how wide the value can get. */
interface BarFieldSpec {
  id: string;
  label: string;
  /** What the label reads where one field per end leaves the artboard room for a shorter word. */
  short?: string;
  sample: string;
  bind: string;
  chars: Chars;
  /**
   * The widest string the value can draw, for a field that is a run of text rather than a number
   * and is therefore set proportionally. `chars` then says what it would take in cells and is not
   * what the field is measured by.
   */
  widest?: string;
  /** A second, dimmer value after the first, as "3 / 22" and "4 / 32" are drawn. */
  denominator?: { sample: string; bind: string; chars: Chars };
}

/**
 * The widest class and position the bar promises to draw: a four-character class name and a
 * two-digit place. The field is measured from it rather than from cells, and at 600 x 686 it is
 * also what leaves the strip room for its fifth cell, so widening it shortens the strip.
 */
const WIDEST_CLASS = 'LMP2 · P24';

/** The ten fields an end of the bar can show. Ordered as the plugin lists them. */
export const BAR_FIELD_SPECS: readonly BarFieldSpec[] = [
  { id: 'raceTime', label: 'Race', sample: '0:28:14', bind: clock(sessionTimeLeft()), chars: CHARS.clock },
  {
    id: 'lap',
    label: 'Lap',
    sample: '4',
    bind: fmt(currentLap(), '0'),
    chars: CHARS.position,
    denominator: { sample: '/ 32', bind: concat(str('/ '), fmt(totalLaps(), '0')), chars: { digits: 4, specials: 1 } },
  },
  { id: 'timeLeft', label: 'Time left', sample: '0:42:15', bind: clock(sessionTimeLeft()), chars: CHARS.clock },
  { id: 'clock', label: 'Clock', sample: '14:32', bind: localClock(), chars: CHARS.clock },
  { id: 'simulatedTime', label: 'Real time', sample: '19:26', bind: simClock(), chars: CHARS.clock },
  {
    id: 'position',
    label: 'Position',
    short: 'Pos',
    sample: '3',
    bind: fmt(isnull(game('Position'), num(0)), '0'),
    chars: CHARS.position,
    denominator: { sample: '/ 22', bind: concat(str('/ '), fmt(opponentCount(), '0')), chars: { digits: 4, specials: 1 } },
  },
  // The position through the leaderboard function every other class reading uses, not a GameData
  // property of that name: SimHub publishes none, so the old read fell through to its own default,
  // which was the number of cars in the class. A driver fourth of twelve read "GT3 · P12".
  {
    id: 'classPosition',
    label: 'Class',
    sample: 'GT3 · P4',
    bind: concat(playerClass(), str(' · P'), fmt(isnull(driver('classposition', playerPosition()), num(0)), '0')),
    chars: CHARS.classPosition,
    widest: WIDEST_CLASS,
  },
  // Four cells rather than the count's three: the x the artboard draws after the number takes one.
  { id: 'incidents', label: 'Incidents', sample: '3x', bind: concat(fmt(isnull(incidents(), num(0)), '0'), str('x')), chars: { digits: 4, specials: 0 } },
  { id: 'airTemp', label: 'Air', sample: '21.5', bind: fmt(airTemperature(), '0.0'), chars: CHARS.pressure, denominator: { sample: '°', bind: str('°'), chars: { digits: 1, specials: 1 } } },
  { id: 'trackTemp', label: 'Track', sample: '27.6', bind: fmt(roadTemperature(), '0.0'), chars: CHARS.pressure, denominator: { sample: '°', bind: str('°'), chars: { digits: 1, specials: 1 } } },
];

/**
 * One cell of the car settings strip: what it reads, what it is called, and what says the car has
 * the setting at all.
 *
 * `present` exists because for two of the seven the two are not the same property. SimHub
 * normalises traction control and ABS into `TCLevel` and `ABSLevel` and reports **0** for a car
 * that has neither control, which is indistinguishable from a car whose driver has turned them
 * off. What says the car has the control is the raw iRacing field behind it, `dcTractionControl`
 * and `dcABS`, which is simply absent on a car without it. The same for the brake bias, whose
 * reading is already wrapped in an `isnull` default and so is never null itself.
 *
 * Getting this wrong is not a cell drawn wrongly: it is a cell drawn at all, and the cells beside
 * it sitting where they would have been.
 */
interface StripCell {
  id: string;
  label: string;
  sample: string;
  expr: string;
  pattern: string;
  /** What says the car has this setting; the value's own property when they are the same. */
  present?: string;
}

/**
 * The strip, in the order the canvas draws it. Cut is `dcTractionControl2`, the second traction
 * dial a GT3 car exposes beside TC level.
 *
 * Two of the seven read a property their label does not name, and both are left alone because
 * settling them is a drawing decision rather than a lookup. Diff reads `dcAntiRollRear`, which
 * `modules/carSettings.ts` already draws under the label ARB R, so the bar and page 09 publish one
 * number under two names. Slip reads `dcThrottleShape`, which is the throttle map. Neither a
 * differential nor a slip target is normalised by SimHub, and the iRacing variable set recorded in
 * `tools/irsdk-emulator/Catalog.cs` holds no such field, so a car that has either publishes it
 * under a name of its own and the binding cannot be looked up. `docs/design/zones.md` section 3
 * records the disagreement.
 *
 * TODO: bind Diff and Slip once the canvas says which in-car adjustment each shows, or rename them.
 */
export const STRIP_CELLS: readonly StripCell[] = [
  { id: 'slip', label: 'Slip', sample: '4', expr: raw('dcThrottleShape'), pattern: '0' },
  { id: 'tc', label: 'TC', sample: '5', expr: tcLevel(), pattern: '0', present: raw('dcTractionControl') },
  { id: 'cut', label: 'Cut', sample: '2', expr: raw('dcTractionControl2'), pattern: '0' },
  { id: 'bias', label: 'Bias', sample: '50.5', expr: brakeBias(), pattern: '0.0', present: game('BrakeBias') },
  { id: 'abs', label: 'ABS', sample: '4', expr: absLevel(), pattern: '0', present: raw('dcABS') },
  { id: 'map', label: 'Map', sample: '1', expr: fuelMixture(), pattern: '0' },
  { id: 'diff', label: 'Diff', sample: '4', expr: antiRollRear(), pattern: '0' },
];

/**
 * Which cells a narrow strip keeps, most important first.
 *
 * Separate from the drawing order above, which is the canvas's. A driver on a GT3 car moves the
 * brake bias every corner and has TC and ABS on wheel dials; the mixture changes once a stint; slip,
 * cut and the differential are settings some cars do not have at all. So the order is what a driver
 * would keep if they had to choose, not the order they are drawn in.
 *
 * There is no "the strip fits or it does not". At 850 by 480 the two ends take almost the whole bar
 * and the five cells that were never optional were drawn over the right-hand fields -- which is what
 * the first photograph of that face showed, BIAS sitting on top of POSITION.
 */
const STRIP_PRIORITY: readonly string[] = ['bias', 'tc', 'abs', 'slip', 'cut', 'map', 'diff'];

/** What a strip cell's sample takes in cells: a reading with one decimal, or a bare number. */
const cellChars = (cell: StripCell): Chars => ({ digits: cell.sample.replace('.', '').length, specials: cell.sample.includes('.') ? 1 : 0 });

/** Width of the text a label is drawn with, with the pixel of room WPF needs not to clip it. */
const labelWidth = (text: string): number => Math.ceil(measureText('BarlowMedium', text.toUpperCase(), ds.size.label)) + 2;

/**
 * Width a strip cell takes: the artboard's column, widened to a reading that does not fit it and
 * rounded up to an even number.
 *
 * The column is fixed rather than measured so that the seven read as a rank of equal cells, which
 * is what the artboards draw. The widening is a guard and no face currently uses it: "Bias 50.5"
 * is the widest of the seven at either value size and it fills its column exactly, 57 of 57 at
 * 34 px and 50 of 54 at 28 px, so every cell comes out at the column rounded up. It stays because
 * a sample or a size that grows past the column would otherwise lose its last digit to WPF in
 * silence, which is the one failure this file cannot see.
 *
 * Even, because the strip is a centred rank that closes over what is missing, and `rank` centres
 * on `(width - total) / 2` twice over: once in the static rect, which `roundRect` rounds, and once
 * in the `Left` binding, which NCalc evaluates at runtime and nothing rounds. An odd total puts the
 * two half a pixel apart. The strip's own width and its gap are both even, so even cells keep every
 * arrangement the closing can produce -- not merely the one with every cell present -- on whole
 * pixels, and the binding agrees with the rect it was laid out from.
 */
function stripCellWidth(cell: StripCell, scale: BarScale): number {
  const value = monoWidth(cells('SemiBold', scale.valueSize), cellChars(cell));
  return 2 * Math.ceil(Math.max(value, labelWidth(cell.label), scale.stripCell) / 2);
}

/** Width a field's value takes: its cells, or its widest rendering where it is drawn proportionally. */
const valueWidth = (spec: BarFieldSpec, fs: number): number =>
  spec.widest === undefined ? monoWidth(cells('SemiBold', fs), spec.chars) : Math.ceil(measureText('BarlowCondensedSemiBold', spec.widest, fs));

/** Width a field's denominator takes, and nothing for a field that has none. */
const denominatorWidth = (spec: BarFieldSpec, fs: number): number =>
  spec.denominator ? monoWidth(cells('SemiBold', fs), spec.denominator.chars) : 0;

/** Width a bar end field takes: its value, its denominator, and the label above them. */
function fieldWidth(spec: BarFieldSpec, scale: BarScale): number {
  const denominator = spec.denominator ? DENOMINATOR_GAP + denominatorWidth(spec, scale.denominatorSize) : 0;
  return Math.ceil(Math.max(valueWidth(spec, scale.valueSize) + denominator, labelWidth(spec.label)));
}

export interface BarOptions {
  /** Two fields per end on a wide face, one in portrait. */
  fieldsPerEnd: 1 | 2;
  /** The face this bar is drawn on, which is what its settings are named after. */
  face: FaceSize;
  /** The sizes the face's artboard draws the bar at. */
  scale: BarScale;
}

/**
 * The bar drawn in `frame`.
 *
 * Every end field is drawn once per catalogue entry with its `Visible` bound to the zone setting,
 * rather than one item whose text switches. Ten fields differ in how many values they hold and how
 * wide each is, and a single item would have to be sized for the widest and laid out for the most
 * complicated; the items cost nothing to draw and each is measured for what it actually shows.
 */
export function bar(frame: Rect, prefix: string, opts: BarOptions): Item[] {
  const { gap, valueSize, denominatorSize } = opts.scale;
  const labelFs = ds.size.label;
  const items: Item[] = [];

  const blockHeight = LABEL_ROW + LABEL_GAP + valueSize;
  const top = frame.top + Math.max(0, (frame.height - blockHeight) / 2);
  // The label is set two pixels larger than the row it is centred in, so its line box starts a
  // pixel above the row and the block is measured from the row rather than from the type.
  const labelTop = top + (LABEL_ROW - labelFs) / 2;
  const valueTop = top + LABEL_ROW + LABEL_GAP;
  // A denominator sits on the value's baseline, which is where the artboard's `align-items:
  // baseline` puts it and is not where a shorter line with the same top would sit.
  const denominatorTop = canvasYForBaseline(canvasBaseline(valueTop, valueSize), denominatorSize);

  const slots: { slot: BarSlot; align: 'left' | 'right' }[] = [
    { slot: 'Left1', align: 'left' },
    { slot: 'Left2', align: 'left' },
    { slot: 'Right1', align: 'right' },
    { slot: 'Right2', align: 'right' },
  ];
  const used = opts.fieldsPerEnd === 2 ? slots : slots.filter((s) => s.slot === 'Left1' || s.slot === 'Right1');

  // Each end's fields are laid out from its own edge inwards, so the widest possible catalogue
  // entry decides the gap the strip gets rather than whichever happens to be selected.
  const widest = Math.max(...BAR_FIELD_SPECS.map((s) => fieldWidth(s, opts.scale)));
  const endWidth = opts.fieldsPerEnd * widest + (opts.fieldsPerEnd - 1) * gap;

  for (const { slot, align } of used) {
    const index = BAR_SLOTS.indexOf(slot);
    const withinEnd = index % 2;
    const x =
      align === 'left'
        ? frame.left + PAD_X + withinEnd * (widest + gap)
        : frame.left + frame.width - PAD_X - endWidth + withinEnd * (widest + gap);
    for (const spec of BAR_FIELD_SPECS) {
      const visible = eq(zoneSetting.barField(opts.face, slot), num(BAR_FIELDS.find((f) => f.id === spec.id)?.number ?? 0));
      const name = `${prefix}${slot}.${spec.id}`;
      // One field per end is the portrait face, where the artboard has the room for "POS" and not
      // for "POSITION".
      const text = opts.fieldsPerEnd === 1 ? (spec.short ?? spec.label) : spec.label;
      items.push({
        ...label(`${name}.label`, text.toUpperCase(), x, labelTop, widest, { size: labelFs, hAlign: align }),
        ...withBindings({ Visible: visible }),
      });
      // A field of the right end is drawn flush to the right of its slot, as the artboard draws it:
      // the denominator against the padding and the value one gap in front of it.
      const value = valueWidth(spec, valueSize) + boxSlack(valueSize);
      const denominator = denominatorWidth(spec, denominatorSize) + boxSlack(denominatorSize);
      const after = spec.denominator ? DENOMINATOR_GAP + denominatorWidth(spec, denominatorSize) : 0;
      items.push({
        ...numeral(`${name}.value`, spec.sample, align === 'left' ? x : x + widest - after - value, valueTop, valueSize, spec.chars, {
          width: value,
          hAlign: align,
          ...(spec.widest === undefined ? {} : { proportional: true, widest: spec.widest }),
        }),
        ...withBindings({ Visible: visible, Text: spec.bind }),
      });
      if (spec.denominator) {
        const dx = align === 'left' ? x + valueWidth(spec, valueSize) + DENOMINATOR_GAP : x + widest - denominator;
        items.push({
          ...numeral(`${name}.denominator`, spec.denominator.sample, dx, denominatorTop, denominatorSize, spec.denominator.chars, {
            color: ds.color.text.secondary,
            width: denominator,
            hAlign: align,
          }),
          ...withBindings({ Visible: visible, Text: spec.denominator.bind }),
        });
      }
    }
  }

  // The strip, centred in what the two ends leave. A setting the sim does not publish takes its
  // cell with it and the strip closes over the hole, which is the rank's `close` mode: an empty box
  // where a car has no differential is worse than a narrower strip.
  const stripLeft = frame.left + PAD_X + endWidth + gap;
  const stripRight = frame.left + frame.width - PAD_X - endWidth - gap;
  const stripWidth = Math.max(0, stripRight - stripLeft);
  const strip = rank(
    STRIP_CELLS.map((cell) => {
      const w = stripCellWidth(cell, opts.scale);
      const present = ncalc.not(ncalc.isNull(cell.present ?? cell.expr));
      const name = `${prefix}strip.${cell.id}`;
      return {
        id: cell.id,
        width: w,
        present,
        draw: (at) => [
          label(`${name}.label`, cell.label.toUpperCase(), at.x, labelTop, w, { size: labelFs, hAlign: 'center', leftBind: at.leftAt(), visibleBind: at.visibleBind }),
          numeral(`${name}.value`, cell.sample, at.x, valueTop, valueSize, cellChars(cell), {
            width: w,
            hAlign: 'center',
            leftBind: at.leftAt(),
            visibleBind: at.visibleBind,
            // Emptied as well as hidden: a hidden item still holds its last text, and the strip is
            // rebuilt from the same items when the next car does publish the setting.
            bind: iff(present, fmt(cell.expr, cell.pattern), str('')),
          }),
        ],
      } satisfies RankMember;
    }),
    // `atLeast: 0`: a bar with no room between its ends draws no strip at all, rather than one cell
    // over a field. The two ends are the settled values and they win the space.
    { left: stripLeft, width: stripWidth, gap, when: 'close', shedOrder: STRIP_PRIORITY, atLeast: 0 },
  );
  items.push(...strip.items);

  return items;
}

