/**
 * The leaderboard, relative and lap-history tables: one row definition that SimHub stamps N times.
 *
 * A repeated Layer clones its children per row and gives each copy a `repeatindex()`, so every
 * cell addresses its own car through one index expression. The rows layer holds an inner layer per
 * row whose Visible is the row's "is there a car here" test: SimHub evaluates a child of a repeated
 * layer inside that copy's repeat context, whereas the repeated layer's own Visible would be
 * evaluated once, for row one, and hide or show all of them together.
 *
 * Class rows cannot be grouped under class headings: SimHub exposes per-class rows only for the
 * player's own class, so the table is one continuous list in leaderboard order and the class is a
 * chip on each row.
 */
import type { HAlign, Item, LayerItem, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { cells, monoWidth, type Chars } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { ds } from '../tokens.ts';
import { COMPOUND_CHARS, COMPOUND_WIDEST, chip, chipText, chipWidth } from './chip.ts';
import { densityOf, type Density, type DensitySpec } from './density.ts';
import { CHARS, carAvailable, carBestLap, carClass, carCompound, carInPit, carInterval, carIsPlayer, carIsSessionBest, carLastLap, carName, carNumber, carPitCount, carPosition, carRaceGap, carRankChange, carRating, carRelativeGap, carSector, carStintLaps, rowIndex } from './values.ts';

const { iff, str, fmt, eq, ne, num, and, not, gt, abs } = ncalc;

/** How a table picks the car on each row. */
export type TableMode = 'full' | 'class' | 'relative';

export type ColumnId =
  | 'pos'
  | 'rank'
  | 'num'
  | 'name'
  | 'class'
  | 'gap'
  | 'int'
  | 'last'
  | 'best'
  | 's1'
  | 's2'
  | 's3'
  | 'stint'
  | 'pit'
  | 'tyre'
  | 'rating';

interface CellContext {
  /** Item name prefix, unique within the screen. */
  name: string;
  /** The leaderboard index of the car on this row. */
  idx: Expr;
  x: number;
  width: number;
  /** Top and height of the row. */
  top: number;
  height: number;
  d: DensitySpec;
  density: Density;
  /** True on the player's own row. */
  isPlayer: Expr;
  /** True when the car is in the pit lane, which dims its row. */
  inPit: Expr;
  mode: TableMode;
  /** How the column this cell belongs to is aligned. */
  align: HAlign;
}

interface ColumnDef {
  header: string;
  align: HAlign;
  /** Column width at a density; 0 makes the column take the row's remaining width. */
  width: (d: DensitySpec) => number;
  cell: (ctx: CellContext) => Item[];
}

/** The colour of an ordinary cell: bright on your row, dim for a car in the pit lane. */
const inkBind = (ctx: CellContext): Expr =>
  iff(ctx.isPlayer, str(ds.color.text.primary), iff(ctx.inPit, str(ds.color.text.dim), str(ds.color.text.secondary)));

/** A numeral cell, vertically centred in the row. */
function cellValue(ctx: CellContext, id: string, sample: string, bind: Expr, chars: Chars, opts: { fs?: number; color?: string; colorBind?: Expr; align?: HAlign } = {}): Item[] {
  const fs = opts.fs ?? ctx.d.small;
  const mono = cells('SemiBold', fs);
  const drawn = monoWidth(mono, chars);
  const align = opts.align ?? ctx.align;
  const x = align === 'right' ? ctx.x + ctx.width - drawn : ctx.x;
  return [
    numeral(`${ctx.name}.${id}`, sample, x, ctx.top + (ctx.height - fs) / 2, fs, chars, {
      bind,
      color: (opts.color as `#${string}`) ?? ds.color.text.secondary,
      colorBind: opts.colorBind ?? inkBind(ctx),
      // The room left from where the cell starts, not the column's whole width: a right-aligned
      // cell starts partway into its column and its box must still end at the column's edge.
      maxWidth: ctx.x + ctx.width - x,
    }),
  ];
}

/** A proportional text cell: the driver name, which is Barlow Medium and never monospaced. */
function cellName(ctx: CellContext): Item[] {
  const fs = ctx.d.name;
  return [
    label(`${ctx.name}.name`, 'KLX', ctx.x, ctx.top + (ctx.height - fs) / 2, ctx.width, {
      size: fs,
      color: ds.color.text.secondary,
      bind: carName(ctx.idx),
    }),
  ].map((item) => ({ ...item, ...withBindings({ Text: carName(ctx.idx), TextColor: inkBind(ctx) }) }));
}

/**
 * The rank column. SimHub cannot draw a triangle, so a gain is a 6 px square in the gain's colour
 * with the count beside it, and an unchanged position is a short dash.
 */
function cellRank(ctx: CellContext): Item[] {
  const change = carRankChange(ctx.idx);
  const gained = gt(change, num(0));
  const moved = ne(change, num(0));
  const fs = ctx.d.tiny;
  const mono = cells('SemiBold', fs);
  const countWidth = monoWidth(mono, { digits: 2, specials: 0 });
  const marker = 6;
  const gap = 3;
  const right = ctx.x + ctx.width;
  const markerX = right - countWidth - gap - marker;
  const colour = iff(gained, str(ds.purpose.delta.faster), str(ds.purpose.delta.slower));
  return [
    {
      ...band(`${ctx.name}.rank.marker`, rect(markerX, ctx.top + (ctx.height - marker) / 2, marker, marker), ds.purpose.delta.faster),
      ...withBindings({ Visible: moved, BackgroundColor: colour }),
    },
    {
      ...band(`${ctx.name}.rank.flat`, rect(right - 8, ctx.top + ctx.height / 2 - 1, 8, 2), ds.color.text.dim),
      ...withBindings({ Visible: not(moved) }),
    },
    numeral(`${ctx.name}.rank.count`, '2', right - countWidth, ctx.top + (ctx.height - fs) / 2, fs, { digits: 2, specials: 0 }, {
      bind: iff(moved, fmt(abs(change), '0'), str('')),
      colorBind: colour,
      visibleBind: moved,
      maxWidth: countWidth + 2,
    }),
  ];
}

/** The pit column: the stop count, replaced by an inverted PIT chip while the car is in the lane. */
function cellPit(ctx: CellContext): Item[] {
  const chipW = Math.min(ctx.width, chipWidth(ctx.density, 'PIT'));
  return [
    ...cellValue(ctx, 'pit', '1', carPitCount(ctx.idx), { digits: 2, specials: 0 }, { fs: ctx.d.tiny, align: 'right' }).map((item) => ({
      ...item,
      ...withBindings({ Text: carPitCount(ctx.idx), TextColor: inkBind(ctx), Visible: not(ctx.inPit) }),
    })),
    ...chip(`${ctx.name}.pitChip`, 'PIT', ctx.x + ctx.width - chipW, ctx.top + (ctx.height - ctx.d.chipHeight) / 2, ctx.density, {
      inverted: true,
      visibleBind: ctx.inPit,
      width: chipW,
    }),
  ];
}

const COLUMNS: Record<ColumnId, ColumnDef> = {
  pos: {
    header: 'POS',
    align: 'right',
    width: (d) => (d.small >= 30 ? 46 : 38),
    cell: (ctx) =>
      cellValue(ctx, 'pos', '4', fmt(carPosition(ctx.idx), '0'), CHARS.position, {
        colorBind: iff(ctx.isPlayer, str(ds.color.text.primary), str(ds.color.text.label)),
      }),
  },
  rank: { header: '±', align: 'right', width: (d) => (d.small >= 30 ? 44 : 36), cell: cellRank },
  num: {
    header: '#',
    align: 'left',
    width: (d) => (d.small >= 30 ? 52 : 44),
    cell: (ctx) => cellValue(ctx, 'num', '#22', carNumber(ctx.idx), CHARS.carNumber, { fs: ctx.d.tiny, color: ds.color.text.label, colorBind: str(ds.color.text.label) }),
  },
  name: { header: 'DRIVER', align: 'left', width: () => 0, cell: cellName },
  class: {
    header: 'CLASS',
    align: 'left',
    width: (d) => Math.ceil(2 * d.chipPadding + 34),
    cell: (ctx) =>
      chip(`${ctx.name}.class`, 'GT3', ctx.x, ctx.top + (ctx.height - ctx.d.chipHeight) / 2, ctx.density, {
        bind: chipText(carClass(ctx.idx)),
        invertedBind: ctx.isPlayer,
        width: Math.ceil(2 * ctx.d.chipPadding + 34),
      }),
  },
  gap: {
    header: 'GAP',
    align: 'right',
    width: (d) => (d.small >= 30 ? 100 : 82),
    cell: (ctx) =>
      ctx.mode === 'relative'
        ? cellValue(ctx, 'gap', '-5.886', carRelativeGap(ctx.idx), CHARS.relativeGap)
        : cellValue(ctx, 'gap', '+12.6', carRaceGap(ctx.idx), CHARS.gap),
  },
  int: { header: 'INT', align: 'right', width: (d) => (d.small >= 30 ? 88 : 72), cell: (ctx) => cellValue(ctx, 'int', '+2.6', carInterval(ctx.idx), CHARS.gap) },
  last: { header: 'LAST', align: 'right', width: (d) => (d.small >= 30 ? 118 : 92), cell: (ctx) => cellValue(ctx, 'last', '1:42.905', carLastLap(ctx.idx), CHARS.lapTime) },
  best: {
    header: 'BEST',
    align: 'right',
    width: (d) => (d.small >= 30 ? 118 : 92),
    cell: (ctx) =>
      cellValue(ctx, 'best', '1:41.877', carBestLap(ctx.idx), CHARS.lapTime, {
        colorBind: iff(carIsSessionBest(ctx.idx), str(ds.purpose.lap.sessionBest), inkBind(ctx)),
      }),
  },
  s1: { header: 'S1', align: 'right', width: (d) => (d.small >= 30 ? 72 : 58), cell: (ctx) => cellValue(ctx, 's1', '28.41', carSector(ctx.idx, 1), CHARS.sector, { fs: ctx.d.tiny }) },
  s2: { header: 'S2', align: 'right', width: (d) => (d.small >= 30 ? 72 : 58), cell: (ctx) => cellValue(ctx, 's2', '41.07', carSector(ctx.idx, 2), CHARS.sector, { fs: ctx.d.tiny }) },
  s3: { header: 'S3', align: 'right', width: (d) => (d.small >= 30 ? 72 : 58), cell: (ctx) => cellValue(ctx, 's3', '32.83', carSector(ctx.idx, 3), CHARS.sector, { fs: ctx.d.tiny }) },
  stint: { header: 'STINT', align: 'right', width: (d) => (d.small >= 30 ? 52 : 44), cell: (ctx) => cellValue(ctx, 'stint', '12', carStintLaps(ctx.idx), { digits: 2, specials: 0 }, { fs: ctx.d.tiny }) },
  pit: { header: 'PIT', align: 'right', width: (d) => Math.ceil(2 * d.chipPadding + 26), cell: cellPit },
  tyre: {
    header: 'TYRE',
    align: 'right',
    width: (d) => Math.ceil(2 * d.chipPadding + 14),
    cell: (ctx) =>
      chip(`${ctx.name}.tyre`, 'M', ctx.x, ctx.top + (ctx.height - ctx.d.chipHeight) / 2, ctx.density, {
        bind: chipText(carCompound(ctx.idx), COMPOUND_CHARS),
        widest: COMPOUND_WIDEST,
        width: Math.ceil(2 * ctx.d.chipPadding + 14),
      }),
  },
  rating: { header: 'RATING', align: 'right', width: (d) => (d.small >= 30 ? 80 : 66), cell: (ctx) => cellValue(ctx, 'rating', '2.4k', carRating(ctx.idx), CHARS.rating, { fs: ctx.d.tiny }) },
};

export interface TableSpec {
  /** Item name prefix. */
  name: string;
  frame: Rect;
  columns: readonly ColumnId[];
  mode: TableMode;
  density: Density;
  /** Rows to stamp. Defaults to what the frame holds. */
  rows?: number;
  /** Draw the header row. On by default. */
  header?: boolean;
  /** Row height; the density's by default. */
  rowHeight?: number;
}

/** The widths of a table's columns, the name column taking what is left. */
export function columnWidths(columns: readonly ColumnId[], width: number, density: Density): number[] {
  const d = densityOf(density);
  const raw = columns.map((id) => COLUMNS[id].width(d));
  const fixed = raw.reduce((sum, w) => sum + w, 0);
  const gaps = d.cellGap * Math.max(0, columns.length - 1);
  const flexColumns = raw.filter((w) => w === 0).length;
  const spare = Math.max(0, width - fixed - gaps);
  return raw.map((w) => (w === 0 ? Math.floor(spare / Math.max(1, flexColumns)) : w));
}

/** How many rows of `rowHeight` fit under the header. */
export function rowCapacity(frame: Rect, density: Density, header: boolean, rowHeight?: number): number {
  const d = densityOf(density);
  const h = rowHeight ?? d.rowHeight;
  const body = frame.height - (header ? d.headerHeight : 0);
  return Math.max(0, Math.floor(body / h));
}

/** The header row: a label per column, aligned as its cells are. */
function headerRow(spec: TableSpec, widths: number[], top: number): Item[] {
  const d = densityOf(spec.density);
  const items: Item[] = [];
  let x = spec.frame.left;
  spec.columns.forEach((id, i) => {
    const width = widths[i] ?? 0;
    const column = COLUMNS[id];
    const text = column.header;
    const drawn = Math.ceil(measureText('BarlowMedium', text, d.labelSm));
    const left = column.align === 'right' ? x + width - drawn : x;
    items.push(label(`${spec.name}.head.${id}`, text, left, top + (d.headerHeight - d.labelSm) / 2, Math.max(drawn, 0), { size: d.labelSm }));
    x += width + d.cellGap;
  });
  return items;
}

/**
 * The table. The row template is stamped `rows` times by the repeated layer; the inner row layer
 * carries the "this car exists" test so that empty rows draw nothing at all.
 */
export function table(spec: TableSpec): Item[] {
  const d = densityOf(spec.density);
  const header = spec.header ?? true;
  const rowHeight = spec.rowHeight ?? d.rowHeight;
  const capacity = rowCapacity(spec.frame, spec.density, header, rowHeight);
  const rows = Math.max(1, Math.min(spec.rows ?? capacity, capacity));
  const widths = columnWidths(spec.columns, spec.frame.width, spec.density);
  const top = spec.frame.top + (header ? d.headerHeight : 0);
  // The player sits in the middle of a relative table, so the row index counts from that row.
  const centre = Math.ceil(rows / 2);
  const idx = spec.mode === 'relative' ? rowIndex.relative(centre) : spec.mode === 'class' ? rowIndex.inClass() : rowIndex.full();
  const isPlayer = carIsPlayer(idx);
  const inPit = carInPit(idx);

  const children: Item[] = [
    { ...band(`${spec.name}.row.background`, rect(spec.frame.left, top, spec.frame.width, rowHeight), ds.color.surface.zone), ...withBindings({ Visible: isPlayer }) },
    band(`${spec.name}.row.rule`, rect(spec.frame.left, top + rowHeight - 1, spec.frame.width, 1), ds.color.surface.raised),
  ];
  let x = spec.frame.left;
  spec.columns.forEach((id, i) => {
    const width = widths[i] ?? 0;
    children.push(
      ...COLUMNS[id].cell({
        name: `${spec.name}.row`,
        idx,
        x,
        width,
        top,
        height: rowHeight,
        d,
        density: spec.density,
        isPlayer,
        inPit,
        mode: spec.mode,
        align: COLUMNS[id].align,
      }),
    );
    x += width + d.cellGap;
  });

  const row: LayerItem = { kind: 'layer', name: `${spec.name}.row`, children, ...withBindings({ Visible: carAvailable(idx) }) };
  const stamped: LayerItem = { kind: 'layer', name: `${spec.name}.rows`, children: [row], repetitions: rows - 1, repeatTopOffset: rowHeight, repeatLeftOffset: 0 };
  return [...(header ? headerRow(spec, widths, spec.frame.top) : []), stamped];
}

/** Every column the tables can show, for the docs and for a test that keeps them in step. */
export const COLUMN_IDS = Object.keys(COLUMNS) as ColumnId[];
