/**
 * Module 19, Lap history: your last laps with the delta to the session best.
 *
 * SimHub keeps ten previous laps as numbered properties, so the rows are a repeated layer that
 * builds its property name from its own repeat index. A row whose lap has no time is hidden, which
 * is what a driver on lap two should see: one row, not six empty ones.
 *
 * The catalogue's third column is the fuel each lap cost, under a header carrying the target for
 * it. No previous-lap property publishes a consumption beside the time, and keeping one per lap
 * would be the plugin remembering between frames, which ADR 0009 refuses; the delta to the session
 * best takes that column instead, and only on the wide page, which is the shape the pit wall draws
 * it at. second-screens.md records the refusal.
 */
import type { Item, LayerItem, Monospace } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { boxSlack, cells, monoWidth, type Chars } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { densityOf } from '../second/density.ts';
import { rowCapacity } from '../second/table.ts';
import { CHARS, PREVIOUS_LAP_SLOTS, currentLap, hasTime, lapTime, previousLap, previousLapDelta } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineModule, shapeIn } from './module.ts';
import { archetypeOf, type Archetype } from './shedding.ts';

const { concat, str, fmt, iff, gt, lt, abs, num, sub, repeatIndex, isnull } = ncalc;

/**
 * A delta this far behind the session best is drawn in caution, and twice that in danger.
 *
 * The caution band used to read `purpose.fuel.low`, which resolves to the danger red, so a lap half
 * a second off the best and a lap a full second off were the same colour and the ladder said
 * nothing at all. The canvas, for its part, paints every slower lap red and has no middle band;
 * zones.md records that the ladder is kept.
 */
export const DELTA_THRESHOLDS = { caution: 0.5, danger: 1 } as const;

/**
 * How many laps each shape lists, read off the catalogue rather than off the box.
 *
 * The drawing lists six at `wide` and at `grid` and seven at the two tall shapes. The build used to
 * list whatever the box held, which put ten rows of 26 px in a zone the catalogue gives seven and
 * made the page a block of digits. A box too short for its count still lists fewer, since a
 * declaration is a design decision and a box is a fact.
 */
const ROWS: Record<Archetype, number> = { wide: 6, grid: 6, tallNarrow: 7, tall: 7 };

/** `L12`, and `L123` on a long race. */
const LAP_CHARS: Chars = { digits: 4, specials: 0 };

/**
 * The column widths the catalogue draws, in the 24 px its zone pages draw a value at: the lap
 * number in 44, the time in 104 and the delta in 86. Each is wider than the cells its value is cut
 * from, so the number is the drawing's own slack rather than a measurement, and it scales with the
 * value it stands beside rather than staying put while the value grows.
 */
const COLUMN = { lap: 44, time: 104, delta: 86 } as const;
const COLUMN_SIZE = 24;

/** The row's own padding, which the catalogue draws as `0 8` inside the module's frame. */
const PAD_X = ds.space[2];

/** A column: what the catalogue draws at this size, never narrower than the cells and their slack. */
const columnWidth = (canvas: number, fs: number, chars: Chars, mono: Monospace): number =>
  Math.max(monoWidth(mono, chars) + boxSlack(fs), Math.round((canvas * fs) / COLUMN_SIZE));

export const lapHistory = defineModule('lapHistory', (ctx) => {
  const d = densityOf(ctx.density);
  const shape = archetypeOf(shapeIn(ctx));
  const rowHeight = d.rowHeight;
  const rows = Math.max(1, Math.min(PREVIOUS_LAP_SLOTS, ROWS[shape], rowCapacity(ctx.frame, ctx.density, true, rowHeight)));
  // Row one is the most recent lap, which SimHub numbers 00, so the slot is the repeat index less one.
  const slot = sub(repeatIndex(), num(1));
  const time = previousLap(slot);
  const delta = isnull(previousLapDelta(slot), num(0));
  const lapNumber = sub(currentLap(), repeatIndex());
  const fs = d.small;
  const mono = cells('SemiBold', fs);
  const lapWidth = columnWidth(COLUMN.lap, fs, LAP_CHARS, mono);
  const timeWidth = columnWidth(COLUMN.time, fs, CHARS.lapTime, mono);
  const deltaWidth = columnWidth(COLUMN.delta, fs, CHARS.delta, mono);
  const drawsDelta = shape === 'wide';
  const left = ctx.frame.left + PAD_X;
  const top = ctx.frame.top + d.headerHeight;
  const headTop = ctx.frame.top + (d.headerHeight - d.labelSm) / 2;
  const valueTop = top + (rowHeight - fs) / 2;
  const timeX = left + lapWidth + d.cellGap;
  // The catalogue spreads the delta to the right edge rather than packing it after the time, which
  // is the flexible spacer its row draws between the two.
  const deltaX = ctx.frame.left + ctx.frame.width - PAD_X - deltaWidth;
  const isBest = lt(abs(delta), num(0.0005));
  const deltaColour = iff(
    isBest,
    str(ds.purpose.lap.sessionBest),
    iff(gt(delta, num(DELTA_THRESHOLDS.danger)), str(ds.purpose.delta.slower), iff(gt(delta, num(DELTA_THRESHOLDS.caution)), str(ds.color.caution.primary), str(ds.color.text.secondary))),
  );
  const children: Item[] = [
    band(`${ctx.prefix}row.rule`, rect(ctx.frame.left, top + rowHeight - 1, ctx.frame.width, 1), ds.color.surface.raised),
    numeral(`${ctx.prefix}row.lap`, 'L12', left, valueTop, fs, LAP_CHARS, {
      bind: concat(str('L'), fmt(lapNumber, '0')),
      color: ds.color.text.label,
      width: lapWidth,
    }),
    numeral(`${ctx.prefix}row.time`, '1:42.905', timeX, valueTop, fs, CHARS.lapTime, {
      bind: lapTime(time),
      colorBind: iff(isBest, str(ds.purpose.lap.sessionBest), str(ds.color.text.primary)),
      width: timeWidth,
    }),
    ...(drawsDelta
      ? [
          numeral(`${ctx.prefix}row.delta`, '+0.594', deltaX, valueTop, fs, CHARS.delta, {
            bind: fmt(delta, '0.000', true),
            colorBind: deltaColour,
            width: deltaWidth,
            hAlign: 'right',
          }),
        ]
      : []),
  ];
  const row: LayerItem = { kind: 'layer', name: `${ctx.prefix}row`, children, ...withBindings({ Visible: hasTime(time) }) };
  return [
    label(`${ctx.prefix}head.lap`, 'LAP', left, headTop, lapWidth, { size: d.labelSm }),
    label(`${ctx.prefix}head.time`, 'TIME', timeX, headTop, timeWidth, { size: d.labelSm }),
    ...(drawsDelta ? [label(`${ctx.prefix}head.delta`, 'Δ BEST', deltaX, headTop, deltaWidth, { size: d.labelSm, hAlign: 'right' })] : []),
    { kind: 'layer', name: `${ctx.prefix}rows`, children: [row], repetitions: rows - 1, repeatTopOffset: rowHeight, repeatLeftOffset: 0 },
  ];
});
