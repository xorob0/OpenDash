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
 *
 * **The fuel target is refused with its column** (#343). The catalogue writes it into the fuel
 * column's heading, `Fuel · target 2.85` at `wide` and the bare `Fuel` at `grid`, so it is a
 * heading's value rather than a heading of its own: with no fuel column there is nothing for it to
 * head, and a target beside `Δ best` would read as a target for the delta. It is also a number a
 * driver sets, which no setting holds; #326 is that setting and the colouring it drives, and it
 * needs the column before it needs the heading.
 */
import type { Item, LayerItem } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { boxSlack, cells, monoWidth, type Chars } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { densityOf, type DensitySpec } from '../second/density.ts';
import { rowCapacity, tableRowHeight } from '../second/table.ts';
import { CHARS, PREVIOUS_LAP_SLOTS, currentLap, hasTime, lapTime, previousLap, previousLapDelta } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineModule, drawnAt, pageKeeps, type ModuleContext } from './module.ts';
import type { Archetype } from './shedding.ts';

const { concat, str, fmt, iff, gt, lt, abs, num, sub, repeatIndex, isnull, signed } = ncalc;

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
const columnWidth = (canvas: number, fs: number, chars: Chars): number =>
  Math.max(monoWidth(cells('SemiBold', fs), chars) + boxSlack(fs), Math.round((canvas * fs) / COLUMN_SIZE));

/**
 * The type a row carries: the values a driver reads, and the lap number that says which lap they are.
 *
 * **Two sizes and not one**, which is #343. Every drawing of this page — the catalogue's four, the
 * companion's and the pit wall's two — draws the lap number, the time and the fuel at one size and
 * tells them apart by colour alone, and the build did the same, so the page read as a block of
 * digits. The time is what a driver compares down the page and the delta is what says by how much;
 * the lap number is an index, read to find a row and never across one. So it is drawn a step down
 * the density ramp from the values, `small` under `mid` and `tiny` under `small`, the way a list
 * draws its car number under its position, and it keeps the label grey the drawings already give it.
 *
 * Three steps, each the canvas's own row at its own numerals: the compact zone's 20 px row, the
 * zone's 26 px row, which is the pit wall's drawing, and the catalogue's 34 px row, which is the
 * face's and the companion's. The last is the top of the ramp, being the largest a list is drawn
 * anywhere on the canvas, so a taller row buys space between the laps and never a larger lap.
 */
interface LapRowType {
  /** The least row height the canvas draws this type in. */
  from: number;
  /** The time and the delta. */
  value: number;
  /** The lap number. */
  index: number;
}

const COMPACT = densityOf('compact');
const ZONE = densityOf('zone');

const LAP_ROW_TYPES: readonly LapRowType[] = [
  { from: COMPACT.rowHeight, value: COMPACT.small, index: COMPACT.tiny },
  { from: ZONE.rowHeight, value: ZONE.small, index: ZONE.tiny },
  { from: tableRowHeight('zone'), value: ZONE.mid, index: ZONE.small },
];

/** Where each column of a row sits, left to right, at a type. */
interface LapColumns {
  lap: { left: number; width: number };
  time: { left: number; width: number };
  /** Absent where the shape draws no delta. */
  delta?: { left: number; width: number };
}

/**
 * The columns a row lays out at a type in this frame.
 *
 * The lap and the time are packed from the left and the delta is spread to the right edge, which is
 * the flexible spacer the catalogue's row draws between them. `fits` is whether the row holds all of
 * them with a cell gap to spare, which is the width edge a type is held to.
 */
function lapColumns(ctx: ModuleContext, type: LapRowType, d: DensitySpec = densityOf(ctx.density)): LapColumns & { fits: boolean } {
  const left = ctx.frame.left + PAD_X;
  const right = ctx.frame.left + ctx.frame.width - PAD_X;
  const lap = { left, width: columnWidth(COLUMN.lap, type.index, LAP_CHARS) };
  const time = { left: lap.left + lap.width + d.cellGap, width: columnWidth(COLUMN.time, type.value, CHARS.lapTime) };
  if (drawnAt(ctx) !== 'wide') return { lap, time, fits: time.left + time.width <= right };
  const width = columnWidth(COLUMN.delta, type.value, CHARS.delta);
  const delta = { left: right - width, width };
  return { lap, time, delta, fits: time.left + time.width + d.cellGap <= delta.left };
}

/** What the page draws in its box: whether it heads its columns, how many laps, how tall, at what type. */
interface LapHistoryPlan {
  head: boolean;
  rows: number;
  rowHeight: number;
  type: LapRowType;
}

/**
 * The page's rows, in the order a list answers its box (#328): the declared count first, then the
 * type, then the rest as space.
 *
 * - **Rows.** The shape's count, cut to what the box holds at the density's own row and to the ten
 *   laps SimHub keeps, never counted at a shorter row.
 * - **Type.** The largest step of {@link LAP_ROW_TYPES} the row's height allows whose columns the
 *   width holds, and never below the step the density draws at, so a box too narrow for its own
 *   type is the canvas's drawing rather than a smaller one.
 * - **Space.** The row is stretched to fill the body, so the laps span the zone instead of stacking
 *   at its top edge over the empty two-thirds of it. What a row buys above the 34 px step is the air
 *   between two laps, and the rule under each row is what keeps them apart at a glance.
 *
 * The count used to be the whole answer: every box drew the density's row at the density's value,
 * so zone C of the 1280 x 720 face listed seven laps of 26 px at the top of 516 px of body.
 */
function lapHistoryPlan(ctx: ModuleContext): LapHistoryPlan {
  const d = densityOf(ctx.density);
  const head = pageKeeps('head', ctx);
  const body = ctx.frame.height - (head ? d.headerHeight : 0);
  const rows = Math.max(1, Math.min(PREVIOUS_LAP_SLOTS, ROWS[drawnAt(ctx)], rowCapacity(ctx.frame, ctx.density, head)));
  const rowHeight = Math.max(d.rowHeight, Math.floor(body / rows));
  const floor = Math.max(0, LAP_ROW_TYPES.findIndex((step) => step.value >= d.small));
  const steps = LAP_ROW_TYPES.slice(floor).filter((step) => step.from <= rowHeight && lapColumns(ctx, step, d).fits);
  return { head, rows, rowHeight, type: steps.at(-1) ?? LAP_ROW_TYPES[floor]! };
}

export const lapHistory = defineModule('lapHistory', (ctx) => {
  const d = densityOf(ctx.density);
  const { head, rows, rowHeight, type } = lapHistoryPlan(ctx);
  const columns = lapColumns(ctx, type, d);
  // Row one is the most recent lap, which SimHub numbers 00, so the slot is the repeat index less one.
  const slot = sub(repeatIndex(), num(1));
  const time = previousLap(slot);
  const delta = isnull(previousLapDelta(slot), num(0));
  const lapNumber = sub(currentLap(), repeatIndex());
  const top = ctx.frame.top + (head ? d.headerHeight : 0);
  // Each cell is centred in the row, as a list's cells are, so the smaller lap number sits on the
  // row's centre line beside the time rather than on its baseline.
  const centred = (fs: number): number => top + (rowHeight - fs) / 2;
  const isBest = lt(abs(delta), num(0.0005));
  const deltaColour = iff(
    isBest,
    str(ds.purpose.lap.sessionBest),
    iff(gt(delta, num(DELTA_THRESHOLDS.danger)), str(ds.purpose.delta.slower), iff(gt(delta, num(DELTA_THRESHOLDS.caution)), str(ds.color.caution.primary), str(ds.color.text.secondary))),
  );
  const children: Item[] = [
    band(`${ctx.prefix}row.rule`, rect(ctx.frame.left, top + rowHeight - 1, ctx.frame.width, 1), ds.color.surface.raised),
    numeral(`${ctx.prefix}row.lap`, 'L12', columns.lap.left, centred(type.index), type.index, LAP_CHARS, {
      bind: concat(str('L'), fmt(lapNumber, '0')),
      color: ds.color.text.label,
      width: columns.lap.width,
    }),
    numeral(`${ctx.prefix}row.time`, '1:42.905', columns.time.left, centred(type.value), type.value, CHARS.lapTime, {
      bind: lapTime(time),
      colorBind: iff(isBest, str(ds.purpose.lap.sessionBest), str(ds.color.text.primary)),
      width: columns.time.width,
    }),
    ...(columns.delta
      ? [
          numeral(`${ctx.prefix}row.delta`, '+0.594', columns.delta.left, centred(type.value), type.value, CHARS.delta, {
            bind: signed(delta, '0.000'),
            colorBind: deltaColour,
            width: columns.delta.width,
            hAlign: 'right',
          }),
        ]
      : []),
  ];
  const row: LayerItem = withMoreBindings({ kind: 'layer', name: `${ctx.prefix}row`, children }, { Visible: hasTime(time) });
  const heading = (name: string, text: string, column: { left: number; width: number }, hAlign?: 'right'): Item =>
    label(`${ctx.prefix}head.${name}`, text, column.left, ctx.frame.top + (d.headerHeight - d.labelSm) / 2, column.width, { size: d.labelSm, hAlign });
  return [
    ...(head ? [heading('lap', 'Lap', columns.lap), heading('time', 'Time', columns.time)] : []),
    ...(head && columns.delta ? [heading('delta', 'Δ best', columns.delta, 'right')] : []),
    { kind: 'layer', name: `${ctx.prefix}rows`, children: [row], repetitions: rows - 1, repeatTopOffset: rowHeight, repeatLeftOffset: 0 },
  ];
});
