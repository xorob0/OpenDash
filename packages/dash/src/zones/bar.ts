/**
 * The bar: settled values, and the strip that hides what the game does not expose.
 *
 * The bar is the one region of a face that is **not a zone**. It does not cycle, and that is what
 * earns it the space: it carries what does not change during a lap, so a driver reads it between
 * corners rather than at speed.
 *
 * Two fields at each end, from a catalogue of ten, and the car settings strip between them. The
 * strip is the part worth care. It draws what the game exposes and hides what it does not, because
 * a strip drawing an empty box for a setting the sim has no property for is worse than a narrower
 * strip — and iRacing really does omit `dcTractionControl` and `dcABS` on cars without the
 * controls, which is what the `quali` capture scenario is for. What says a car has a setting is
 * `hasSetting` in `second/tracked.ts`, and nothing here second-guesses it.
 */
import type { Item, Monospace, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { BAR_FIELDS, BAR_SLOTS, zone as zoneSetting, type BarSlot, type FaceSize } from '../contract.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { boxSlack, canvasBaseline, canvasYForBaseline, cells, DATA_FACE, monoWidth, type Chars } from '../design/metrics.ts';
import { label } from '../elements/label.ts';
import { mark, type Mark, unmarked } from '../elements/mark.ts';
import { numeral } from '../elements/numeral.ts';
import { charsOfText, drawnFigure, drawnWithin, type DrawnFigure } from '../second/drawn.ts';
import { rank, type RankMember } from '../second/rank.ts';
import { TRACKED_VALUES, hasSetting } from '../second/tracked.ts';
import type { BarScale } from './layout.ts';
import {
  CHARS,
  INCIDENTS_WIDEST,
  LAP_TOTAL_WIDEST,
  LAP_WIDEST,
  airTemperature,
  antiRollFront,
  fieldSize,
  currentLap,
  incidents,
  localClock,
  meridiemWidest,
  player,
  positionDigits,
  positionDrawn,
  playerClass,
  sessionClock,
  simClock,
  hasLapTotal,
  totalLaps,
  twelveHour,
  untimedMark,
  roadTemperature,
  type TimeOfDay,
} from '../second/values.ts';
import { ds } from '../tokens.ts';

const { add, and, fmt, isnull, num, str, iff, eq, concat, driver, playerPosition } = ncalc;

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
  /**
   * The longest reading the binding can produce, for a field that stays a number in cells.
   *
   * Not `widest` above, which sets the field proportionally and measures it from the string. This one
   * changes nothing about the drawing, the box being cut from `chars`; it is what the fit tests
   * measure, so a lap count is held to `999` rather than to the `4` of its sample. It is band D's
   * `numeralWidest`, for the same reason.
   */
  numeralWidest?: string;
  /**
   * A mark drawn in the value's place, in the value's own box, for a state whose reading is a glyph
   * no cell can hold: the `∞` of a session with no clock. It takes no width of its own, so the
   * catalogue is measured as it was; see `elements/mark.ts`.
   */
  mark?: Mark;
  /**
   * A second, dimmer value after the first, as "3 / 22" and "4 / 32" are drawn. `widest` is the
   * longest it can read, which the fit tests measure it by; `chars` has to hold it. `when` is the
   * state it means something in, where there is one: the lap's `/ 32` is the race's length only in a
   * race counted in laps, and drawn on the slot's visibility alone it read the leader's laps as the
   * length of a timed race and `/ 0` in an open practice (#989).
   */
  denominator?: { sample: string; bind: string; chars: Chars; widest?: string; when?: Expr };
  /**
   * A word after the value, drawn only while `when` holds: the `AM` or `PM` of a clock the rig writes
   * to twelve hours (#324). It is set as the denominator is -- after a gap, at the denominator's size
   * and in its dimmer ink -- but proportionally, because `M` is one of the glyphs rule 19 keeps out
   * of a cell.
   *
   * The field is measured with it whatever the setting, the box being cut at build time and the
   * setting read at runtime, so a box never holds the shorter clock and clips the longer. What moves
   * with the setting is the value: a field of the right end draws its digits against the padding
   * while there is no word, and one word in from it while there is. A field of the left end draws its
   * digits from its own edge either way and places the word after them by {@link BarFieldSpec.drawn}.
   */
  suffix?: { sample: string; bind: string; widest: string; when: string };
  /**
   * How wide the value really draws, for placing the denominator after the figure rather than after
   * the cells the figure is cut from. A field of the left end is drawn from its own edge, so lap 4
   * in a two-digit budget carried `/ 32` a whole cell further out than lap 16 did; a field of the
   * right end is already flush to the padding and its value is right aligned, so the pair keeps its
   * gap there without a binding. Required of every field that has a denominator. #387.
   */
  drawn?: DrawnFigure;
}

/** The face the bar's followers are set in, which is the value's own. */
const FOLLOWER_FACE = DATA_FACE.SemiBold;

/**
 * One of the two clocks of the day: its digits in {@link CHARS.timeOfDay} and its meridiem after
 * them, which is what makes the field as wide as `2:32 PM` rather than as `14:32`. #324.
 */
const timeOfDayField = (id: string, label: string, sample: string, clock: TimeOfDay): BarFieldSpec => ({
  id,
  label,
  sample,
  bind: clock.text,
  chars: CHARS.timeOfDay,
  drawn: clock.drawn,
  suffix: { sample: 'PM', bind: clock.meridiem, widest: meridiemWidest(FOLLOWER_FACE), when: twelveHour() },
});

/**
 * The widest class and position the bar promises to draw: a four-character class name and a
 * two-digit place. The field is measured from it rather than from cells, and at 600 x 686 it is
 * also what leaves the strip room for its fifth cell, so widening it shortens the strip.
 */
const WIDEST_CLASS = 'LMP2 · P24';

/** The ten fields an end of the bar can show. Ordered as the plugin lists them. */
export const BAR_FIELD_SPECS: readonly BarFieldSpec[] = [
  { id: 'raceTime', label: 'Race', sample: '0:28:14', bind: sessionClock(), mark: untimedMark(), chars: CHARS.clock },
  {
    id: 'lap',
    label: 'Lap',
    sample: '4',
    bind: fmt(currentLap(), '0'),
    chars: CHARS.lap,
    numeralWidest: LAP_WIDEST,
    // Five full cells: the space and the slash are not among the narrow `.,:`, so `/ 120` is five
    // cells wide and the four and a narrow one this was cut from lost its last digit. #596.
    denominator: { sample: '/ 32', bind: concat(str('/ '), fmt(totalLaps(), '0')), chars: { digits: 5, specials: 0 }, widest: LAP_TOTAL_WIDEST, when: hasLapTotal() },
    drawn: drawnFigure({ value: currentLap(), digits: CHARS.lap.digits }),
  },
  { id: 'timeLeft', label: 'Time left', sample: '0:42:15', bind: sessionClock(), mark: untimedMark(), chars: CHARS.clock },
  timeOfDayField('clock', 'Clock', '14:32', localClock()),
  timeOfDayField('simulatedTime', 'Real time', '19:26', simClock()),
  // The two position cells answer two questions, and cannot contradict each other on one face.
  //
  // `position` is the place the rig counts, which is `PositionMode`: in class by default, over the
  // whole field when the rig asks for that, with the count it is out of following it. It is the
  // same reading the session module, the pit wall and the companion header draw, through the same
  // `positionDigits` and `fieldSize`, so the bar is not the one surface where a rig set to class
  // still reads the whole field. It used to bind `Position` and `OpponentsCount` with no reference
  // to the mode, which drew `16 / 40` beside `GT3 · P2`. #432.
  //
  // `classPosition` is always the class, whatever the mode says, because what it adds is the class
  // name: with the rig counting overall it is the one cell on the face that still says where the
  // driver stands in their own class, and with the rig counting in class the two cells agree. The
  // converse, a cell always counting overall, is not offered here: the setting is what asks for the
  // whole field, and a bar cell doing so behind its back is the disagreement #432 removed.
  {
    id: 'position',
    label: 'Position',
    short: 'Pos',
    sample: '3',
    bind: positionDigits(player()),
    chars: CHARS.position,
    denominator: { sample: '/ 22', bind: concat(str('/ '), fmt(fieldSize(), '0')), chars: { digits: 4, specials: 1 } },
    drawn: positionDrawn(player()),
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
  { id: 'incidents', label: 'Incidents', sample: '3x', bind: concat(fmt(isnull(incidents(), num(0)), '0'), str('x')), chars: CHARS.incidents, numeralWidest: INCIDENTS_WIDEST },
  {
    id: 'airTemp',
    label: 'Air',
    sample: '21.5',
    bind: fmt(airTemperature(), '0.0'),
    chars: CHARS.pressure,
    denominator: { sample: '°', bind: str('°'), chars: { digits: 1, specials: 1 } },
    drawn: drawnFigure({ value: airTemperature(), digits: CHARS.pressure.digits - 1, decimals: 1 }),
  },
  {
    id: 'trackTemp',
    label: 'Track',
    sample: '27.6',
    bind: fmt(roadTemperature(), '0.0'),
    chars: CHARS.pressure,
    denominator: { sample: '°', bind: str('°'), chars: { digits: 1, specials: 1 } },
    drawn: drawnFigure({ value: roadTemperature(), digits: CHARS.pressure.digits - 1, decimals: 1 }),
  },
];

/**
 * One cell of the car settings strip: a tracked value, under the short name the strip calls it by.
 *
 * The cells are the watched settings and not a list of their own, so that the strip and the change
 * notification cannot disagree about what a value is; `second/tracked.ts` holds the readings, the
 * two names and the presence tests, and is where a setting is added or corrected.
 *
 * Getting the presence test wrong is not a cell drawn wrongly: it is a cell drawn at all, and the
 * cells beside it sitting where they would have been.
 */
interface StripCell {
  id: string;
  label: string;
  sample: string;
  /** The longest reading the setting can draw, which its cells are cut from: the tracked value's. */
  widest: string;
  expr: string;
  pattern: string;
  /** True when the car has this setting: `hasSetting` of the tracked value, which is the one rule. */
  present: Expr;
}

/** The strip, in the order the canvas draws it, under the strip's own names. */
export const STRIP_CELLS: readonly StripCell[] = TRACKED_VALUES.map((value) => ({
  id: value.id,
  label: value.strip,
  sample: value.sample,
  widest: value.widest,
  expr: value.read,
  pattern: value.pattern,
  present: hasSetting(value),
}));

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

/**
 * What a strip cell's reading takes in cells, cut from its `widest` rather than its sample: the
 * samples are one digit, and a TC of 10 in the one cell its `5` was cut from lost a digit. #596.
 */
const cellChars = (cell: StripCell, mono: Monospace): Chars => charsOfText(cell.widest, mono);

/** Width of the text a label is drawn with, with the pixel of room WPF needs not to clip it. */
const labelWidth = (text: string): number => Math.ceil(measureText('BarlowMedium', text, ds.size.label)) + 2;

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
  const mono = cells('SemiBold', scale.valueSize);
  const value = monoWidth(mono, cellChars(cell, mono));
  return 2 * Math.ceil(Math.max(value, labelWidth(cell.label), scale.stripCell) / 2);
}

/** Width a field's value takes: its cells, or its widest rendering where it is drawn proportionally. */
const valueWidth = (spec: BarFieldSpec, fs: number): number =>
  spec.widest === undefined ? monoWidth(cells('SemiBold', fs), spec.chars) : Math.ceil(measureText(DATA_FACE.SemiBold, spec.widest, fs));

/** Width a field's denominator takes, and nothing for a field that has none. */
const denominatorWidth = (spec: BarFieldSpec, fs: number): number =>
  spec.denominator ? monoWidth(cells('SemiBold', fs), spec.denominator.chars) : 0;

/** Width a field's suffix takes, measured by the widest word it can draw, and nothing for a field that has none. */
const suffixWidth = (spec: BarFieldSpec, fs: number): number => (spec.suffix ? Math.ceil(measureText(FOLLOWER_FACE, spec.suffix.widest, fs)) : 0);

/** The room after a field's value: its denominator or its suffix and the gap in front of it. */
function followerRoom(spec: BarFieldSpec, fs: number): number {
  if (spec.denominator && spec.suffix) throw new Error(`bar field ${spec.id}: a denominator and a suffix both want the place after the value`);
  if (spec.denominator) return DENOMINATOR_GAP + denominatorWidth(spec, fs);
  if (spec.suffix) return DENOMINATOR_GAP + suffixWidth(spec, fs);
  return 0;
}

/** Width a bar end field takes: its value, what follows it, and the label above them. */
function fieldWidth(spec: BarFieldSpec, scale: BarScale): number {
  return Math.ceil(Math.max(valueWidth(spec, scale.valueSize) + followerRoom(spec, scale.denominatorSize), labelWidth(spec.label)));
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
      // One field per end is the portrait face, where the artboard has the room for "Pos" and not
      // for "Position".
      const text = opts.fieldsPerEnd === 1 ? (spec.short ?? spec.label) : spec.label;
      items.push(withMoreBindings(label(`${name}.label`, text, x, labelTop, widest, { size: labelFs, hAlign: align }), { Visible: visible }));
      // A field of the right end is drawn flush to the right of its slot, as the artboard draws it:
      // the denominator against the padding and the value one gap in front of it.
      const value = valueWidth(spec, valueSize) + boxSlack(valueSize);
      const denominator = denominatorWidth(spec, denominatorSize) + boxSlack(denominatorSize);
      const after = followerRoom(spec, denominatorSize);
      const valueX = align === 'left' ? x : x + widest - after - value;
      // A suffix is there only while its setting says so, and a right-hand value is drawn against
      // the padding while it is not: its design-time place is the one without the word, which is
      // what every rig draws until the setting is changed.
      const withoutSuffix = align === 'right' && spec.suffix ? x + widest - value : undefined;
      items.push(
        withMoreBindings(
          numeral(`${name}.value`, spec.sample, withoutSuffix ?? valueX, valueTop, valueSize, spec.chars, {
            width: value,
            hAlign: align,
            ...(spec.widest === undefined ? { widest: spec.numeralWidest } : { proportional: true, widest: spec.widest }),
          }),
          {
            Visible: unmarked(spec.mark, visible),
            Text: spec.bind,
            Left: withoutSuffix === undefined || !spec.suffix ? undefined : iff(spec.suffix.when, num(valueX), num(withoutSuffix)),
          },
        ),
      );
      if (spec.suffix) {
        // After the figure rather than after the cells it is cut from, as a denominator is: a
        // twelve-hour hour is one digit or two, and `9:05` is a cell shorter than `12:05`. A right-hand
        // one sits against the padding, where the right-aligned value leaves it one gap clear.
        const mono = cells('SemiBold', valueSize);
        if (spec.drawn === undefined) throw new Error(`bar field ${spec.id}: a suffix needs the width its value really draws`);
        const box = suffixWidth(spec, denominatorSize) + boxSlack(denominatorSize);
        const sx = align === 'left' ? x + monoWidth(mono, charsOfText(spec.sample, mono)) + DENOMINATOR_GAP : x + widest - box;
        const leftBind = align === 'left' ? add(num(x), drawnWithin(`bar field ${spec.id}`, spec.drawn, spec.chars, mono), num(DENOMINATOR_GAP)) : undefined;
        items.push(
          withMoreBindings(
            numeral(`${name}.unit`, spec.suffix.sample, sx, denominatorTop, denominatorSize, charsOfText(spec.suffix.sample, mono), {
              proportional: true,
              widest: spec.suffix.widest,
              color: ds.color.text.secondary,
              width: box,
              hAlign: align,
            }),
            { Visible: and(visible, spec.suffix.when), Text: spec.suffix.bind, Left: leftBind },
          ),
        );
      }
      // The mark shares the value's box and alignment, so a right-hand slot draws it against the
      // padding where the clock's last digit was, and the field is the width it always was.
      if (spec.mark) items.push(mark(`${name}.mark`, spec.mark, valueX, valueTop, valueSize, visible, { width: value, hAlign: align }));
      if (spec.denominator) {
        // A left-hand field is drawn from its own edge, so its denominator follows the figure rather
        // than the cells the figure is cut from: `4 / 32` and `16 / 32` keep one gap. A right-hand
        // one is laid from the padding inwards, the denominator right aligned in its whole budget and
        // the value right aligned in front of that, so the pair keeps one gap only while the
        // denominator fills its budget: `/ 32` in the five cells `/ 999` needs leaves one cell more
        // between the figure and the slash. See {@link BarFieldSpec.drawn}.
        const mono = cells('SemiBold', valueSize);
        if (align === 'left' && spec.drawn === undefined) {
          throw new Error(`bar field ${spec.id}: a denominator needs the width its value really draws, or it is placed at the end of the budget`);
        }
        const dx = align === 'left' ? x + monoWidth(mono, charsOfText(spec.sample, mono)) + DENOMINATOR_GAP : x + widest - denominator;
        const leftBind = align === 'left' && spec.drawn ? add(num(x), spec.drawn(mono), num(DENOMINATOR_GAP)) : undefined;
        items.push(
          withMoreBindings(
            numeral(`${name}.denominator`, spec.denominator.sample, dx, denominatorTop, denominatorSize, spec.denominator.chars, {
              widest: spec.denominator.widest,
              color: ds.color.text.secondary,
              width: denominator,
              hAlign: align,
            }),
            { Visible: spec.denominator.when === undefined ? visible : and(visible, spec.denominator.when), Text: spec.denominator.bind, Left: leftBind },
          ),
        );
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
      const present = cell.present;
      const name = `${prefix}strip.${cell.id}`;
      return {
        id: cell.id,
        width: w,
        present,
        draw: (at) => [
          label(`${name}.label`, cell.label, at.x, labelTop, w, { size: labelFs, hAlign: 'center', leftBind: at.leftAt(), visibleBind: at.visibleBind }),
          numeral(`${name}.value`, cell.sample, at.x, valueTop, valueSize, cellChars(cell, cells('SemiBold', valueSize)), {
            widest: cell.widest,
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

