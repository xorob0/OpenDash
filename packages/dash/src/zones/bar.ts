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
import { BAR_FIELDS, BAR_SLOTS, zone as zoneSetting, type BarSlot } from '../contract.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { cells, monoWidth } from '../design/metrics.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { densityOf } from '../second/density.ts';
import { rank, type RankMember } from '../second/rank.ts';
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

const { fmt, isnull, num, str, iff, eq, game, concat, raw } = ncalc;

/** One field of the bar's catalogue: a label, a value and how wide the value can get. */
interface BarFieldSpec {
  id: string;
  label: string;
  sample: string;
  bind: string;
  chars: { digits: number; specials: number };
  /** A second, dimmer value after the first, as "3 / 22" and "4 / 32" are drawn. */
  denominator?: { sample: string; bind: string; chars: { digits: number; specials: number } };
}

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
    sample: '3',
    bind: fmt(isnull(game('Position'), num(0)), '0'),
    chars: CHARS.position,
    denominator: { sample: '/ 22', bind: concat(str('/ '), fmt(opponentCount(), '0')), chars: { digits: 4, specials: 1 } },
  },
  { id: 'classPosition', label: 'Class', sample: 'GT3 P4', bind: concat(playerClass(), str(' P'), fmt(isnull(game('PlayerClassPosition'), classOpponentCount()), '0')), chars: { digits: 9, specials: 0 } },
  { id: 'incidents', label: 'Incidents', sample: '3', bind: fmt(isnull(incidents(), num(0)), '0'), chars: CHARS.count, denominator: { sample: 'x', bind: str('x'), chars: { digits: 1, specials: 0 } } },
  { id: 'airTemp', label: 'Air', sample: '21.5', bind: fmt(airTemperature(), '0.0'), chars: CHARS.pressure, denominator: { sample: '°', bind: str('°'), chars: { digits: 1, specials: 1 } } },
  { id: 'trackTemp', label: 'Track', sample: '27.6', bind: fmt(roadTemperature(), '0.0'), chars: CHARS.pressure, denominator: { sample: '°', bind: str('°'), chars: { digits: 1, specials: 1 } } },
];

/** One cell of the car settings strip: what it reads, and what it is called. */
interface StripCell {
  id: string;
  label: string;
  sample: string;
  expr: string;
  pattern: string;
}

/**
 * The strip, in the order the canvas draws it. Slip and Cut are iRacing's own names for the two
 * traction settings a GT3 car exposes separately from TC level.
 */
export const STRIP_CELLS: readonly StripCell[] = [
  { id: 'slip', label: 'Slip', sample: '4', expr: raw('dcThrottleShape'), pattern: '0' },
  { id: 'tc', label: 'TC', sample: '5', expr: tcLevel(), pattern: '0' },
  { id: 'cut', label: 'Cut', sample: '2', expr: raw('dcTractionControl2'), pattern: '0' },
  { id: 'bias', label: 'Bias', sample: '50.5', expr: brakeBias(), pattern: '0.0' },
  { id: 'abs', label: 'ABS', sample: '4', expr: absLevel(), pattern: '0' },
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

/** Width a strip cell takes: its label or its value, whichever is wider. */
function stripCellWidth(cell: StripCell, valueFs: number, labelFs: number): number {
  const value = monoWidth(cells('SemiBold', valueFs), { digits: cell.sample.replace('.', '').length, specials: cell.sample.includes('.') ? 1 : 0 });
  const text = Math.ceil(measureText('BarlowMedium', cell.label.toUpperCase(), labelFs)) + 2;
  return Math.ceil(Math.max(value, text));
}

/** Width a bar end field takes: its value, its denominator, and the label above them. */
function fieldWidth(spec: BarFieldSpec, valueFs: number, labelFs: number, smallFs: number): number {
  const value = monoWidth(cells('SemiBold', valueFs), spec.chars);
  const denominator = spec.denominator ? ds.space[2] + monoWidth(cells('SemiBold', smallFs), spec.denominator.chars) : 0;
  const text = Math.ceil(measureText('BarlowMedium', spec.label.toUpperCase(), labelFs)) + 2;
  return Math.ceil(Math.max(value + denominator, text));
}

export interface BarOptions {
  /** Two fields per end on a wide face, one in portrait. */
  fieldsPerEnd: 1 | 2;
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
  const d = densityOf('zone');
  const valueFs = d.mid;
  const smallFs = d.labelSm;
  const labelFs = d.labelSm;
  const padX = 20;
  const items: Item[] = [];

  const blockHeight = labelFs + d.fieldGap + valueFs;
  const top = frame.top + Math.max(0, (frame.height - blockHeight) / 2);
  const valueTop = top + labelFs + d.fieldGap;

  const slots: { slot: BarSlot; align: 'left' | 'right' }[] = [
    { slot: 'Left1', align: 'left' },
    { slot: 'Left2', align: 'left' },
    { slot: 'Right1', align: 'right' },
    { slot: 'Right2', align: 'right' },
  ];
  const used = opts.fieldsPerEnd === 2 ? slots : slots.filter((s) => s.slot === 'Left1' || s.slot === 'Right1');

  // Each end's fields are laid out from its own edge inwards, so the widest possible catalogue
  // entry decides the gap the strip gets rather than whichever happens to be selected.
  const widest = Math.max(...BAR_FIELD_SPECS.map((s) => fieldWidth(s, valueFs, labelFs, smallFs)));
  const endWidth = opts.fieldsPerEnd * widest + (opts.fieldsPerEnd - 1) * d.gapX;

  for (const { slot, align } of used) {
    const index = BAR_SLOTS.indexOf(slot);
    const withinEnd = index % 2;
    const x =
      align === 'left'
        ? frame.left + padX + withinEnd * (widest + d.gapX)
        : frame.left + frame.width - padX - endWidth + withinEnd * (widest + d.gapX);
    for (const spec of BAR_FIELD_SPECS) {
      const visible = eq(zoneSetting.barField(slot), num(BAR_FIELDS.find((f) => f.id === spec.id)?.number ?? 0));
      const name = `${prefix}${slot}.${spec.id}`;
      items.push({
        ...label(`${name}.label`, spec.label.toUpperCase(), x, top, widest, { size: labelFs }),
        ...withBindings({ Visible: visible }),
      });
      const valueWidth = monoWidth(cells('SemiBold', valueFs), spec.chars);
      items.push({
        ...numeral(`${name}.value`, spec.sample, x, valueTop, valueFs, spec.chars, { maxWidth: valueWidth + 4 }),
        ...withBindings({ Visible: visible, Text: spec.bind }),
      });
      if (spec.denominator) {
        const dx = x + valueWidth + ds.space[2];
        items.push({
          ...numeral(`${name}.denominator`, spec.denominator.sample, dx, valueTop + (valueFs - smallFs), smallFs, spec.denominator.chars, {
            color: ds.color.text.secondary,
            maxWidth: monoWidth(cells('SemiBold', smallFs), spec.denominator.chars) + 4,
          }),
          ...withBindings({ Visible: visible, Text: spec.denominator.bind }),
        });
      }
    }
  }

  // The strip, centred in what the two ends leave. A setting the sim does not publish takes its
  // cell with it and the strip closes over the hole, which is the rank's `close` mode: an empty box
  // where a car has no differential is worse than a narrower strip.
  const stripLeft = frame.left + padX + endWidth + d.gapX;
  const stripRight = frame.left + frame.width - padX - endWidth - d.gapX;
  const stripWidth = Math.max(0, stripRight - stripLeft);
  const strip = rank(
    STRIP_CELLS.map((cell) => {
      const w = stripCellWidth(cell, smallFs, labelFs);
      const present = ncalc.not(ncalc.isNull(cell.expr));
      const name = `${prefix}strip.${cell.id}`;
      return {
        id: cell.id,
        width: w,
        present,
        draw: (at) => [
          label(`${name}.label`, cell.label.toUpperCase(), at.x, top, w, { size: labelFs, leftBind: at.leftAt(), visibleBind: at.visibleBind }),
          numeral(`${name}.value`, cell.sample, at.x, valueTop + (valueFs - smallFs), smallFs, { digits: cell.sample.replace('.', '').length, specials: cell.sample.includes('.') ? 1 : 0 }, {
            color: ds.color.text.secondary,
            maxWidth: w + 4,
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
    { left: stripLeft, width: stripWidth, gap: d.gapX, when: 'close', shedOrder: STRIP_PRIORITY, atLeast: 0 },
  );
  items.push(...strip.items);

  return items;
}

