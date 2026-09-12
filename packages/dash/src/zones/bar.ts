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

type Expr = string;
import { withBindings } from '../bind.ts';
import { BAR_FIELDS, BAR_SLOTS, zone as zoneSetting, type BarSlot } from '../contract.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { cells, monoWidth } from '../design/metrics.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { densityOf } from '../second/density.ts';
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

const { fmt, isnull, num, str, iff, eq, game, concat, raw, add, sub, div } = ncalc;

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

/** Whether the sim publishes this setting at all. A cell it does not publish is not drawn. */
const present = (cell: StripCell): Expr => ncalc.not(ncalc.isNull(cell.expr));

/** The left edge that centres `width` inside the strip, where `width` is itself an expression. */
const centred = (stripLeft: number, stripWidth: number, width: Expr): Expr =>
  add(num(stripLeft), div(sub(num(stripWidth), width), num(2)));

/** The most important cells that fit the width, in the order the canvas draws them. */
function stripCellsThatFit(width: number, valueFs: number, labelFs: number, gap: number): StripCell[] {
  for (let count = STRIP_PRIORITY.length; count > 0; count -= 1) {
    const keep = new Set(STRIP_PRIORITY.slice(0, count));
    const drawn = STRIP_CELLS.filter((c) => keep.has(c.id));
    const total = drawn.reduce((sum, c) => sum + stripCellWidth(c, valueFs, labelFs), 0) + gap * (drawn.length - 1);
    if (total <= width) return drawn;
  }
  return [];
}

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

  // The strip, centred in what the two ends leave.
  const stripLeft = frame.left + padX + endWidth + d.gapX;
  const stripRight = frame.left + frame.width - padX - endWidth - d.gapX;
  const stripWidth = Math.max(0, stripRight - stripLeft);
  const cellsToDraw = stripCellsThatFit(stripWidth, smallFs, labelFs, d.gapX);
  const widths = cellsToDraw.map((c) => stripCellWidth(c, smallFs, labelFs));
  const total = widths.reduce((a, b) => a + b, 0) + d.gapX * Math.max(0, cellsToDraw.length - 1);
  // The static left of each cell is the layout with every cell present, which is what the binding
  // below evaluates to in that case. It is not decoration: the geometry tests measure it, and a
  // scene graph whose items all sit at one x would be wrong the moment a binding were not read.
  let x = stripLeft + Math.max(0, (stripWidth - total) / 2);
  // What each cell occupies when it is there, and nothing when it is not. The strip closes over an
  // absent cell rather than leaving a hole where it would have been, so `Left` is an expression
  // over the cells to its left rather than a number fixed at build time. iRacing omits
  // dcTractionControl and dcABS on a car without the controls, which is two holes of about fifty
  // pixels in the middle of the bar, and the `quali` scenario exists to show exactly that.
  const occupied = (i: number): Expr => iff(present(cellsToDraw[i]!), num((widths[i] ?? 0) + d.gapX), num(0));
  // Every drawn cell, with the trailing gap of the last one taken off again.
  const drawnWidth = sub(add(...cellsToDraw.map((_, i) => occupied(i))), num(d.gapX));
  cellsToDraw.forEach((cell, i) => {
    const w = widths[i] ?? 0;
    const name = `${prefix}strip.${cell.id}`;
    const here = present(cell);
    // Re-centred on what is actually drawn, then shifted by whatever precedes it. A strip of one
    // cell sits in the middle of the gap between the ends, exactly as a strip of seven does.
    const left = i === 0
      ? centred(stripLeft, stripWidth, drawnWidth)
      : add(centred(stripLeft, stripWidth, drawnWidth), ...Array.from({ length: i }, (_, j) => occupied(j)));
    items.push(
      { ...label(`${name}.label`, cell.label.toUpperCase(), x, top, w, { size: labelFs }), ...withBindings({ Visible: here, Left: left }) },
      {
        ...numeral(`${name}.value`, cell.sample, x, valueTop + (valueFs - smallFs), smallFs, { digits: cell.sample.replace('.', '').length, specials: cell.sample.includes('.') ? 1 : 0 }, {
          color: ds.color.text.secondary,
          maxWidth: w + 4,
        }),
        ...withBindings({ Visible: here, Left: left, Text: iff(here, fmt(cell.expr, cell.pattern), str('')) }),
      },
    );
    x += w + d.gapX;
  });

  return items;
}

