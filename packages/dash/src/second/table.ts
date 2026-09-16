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
import { rule } from '../elements/rule.ts';
import { ds } from '../tokens.ts';
import { chip, chipText, chipWidth } from './chip.ts';
import { densityOf, isZone, type Density, type DensitySpec } from './density.ts';
import { CHARS, carAvailable, carBestLap, carClass, carCompound, carInPit, carInterval, carIsPlayer, carIsSessionBest, carLastLap, carName, carNumber, carPitCount, carPosition, carRaceGap, carRankChange, carRating, carRelativeGap, carSector, carStintLaps, driverCode, rowIndex } from './values.ts';

const { iff, str, fmt, eq, ne, num, and, not, gt, abs, concat, left, ucase, isnull } = ncalc;

/** How a table picks the car on each row. */
export type TableMode = 'full' | 'class' | 'relative';

export type ColumnId =
  | 'pos'
  | 'rank'
  | 'flag'
  | 'num'
  | 'name'
  | 'class'
  | 'licence'
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

/**
 * The row the canvas draws, which is not the density's row.
 *
 * `density.ts` gives a module the type ramp of the screen it is on; these five numbers are the
 * table's own geometry and the canvas states them once for every artboard that draws a list: rows
 * two apart rather than flush against a rule, six pixels of padding either side, twelve between
 * cells, and a header row of sixteen where a header is drawn at all.
 */
const ROW_GAP = 2;
const ROW_PAD_X = 6;
const CELL_GAP = 12;
const HEADER_HEIGHT = 16;

/**
 * The board, which is the other table the canvas draws.
 *
 * The three pit wall artboards and the leaderboard panel on `Panels.dc.html` state their row in
 * one stylesheet rule each and all four agree: `.trow` is 36 px -- 34 on the race page, 32 on the
 * portrait one, 28 on the tower -- `.th` is 32, both are padded `0 16px`, and both close on
 * `1px solid #1C1F24` with no gap between two rows. So a board is not a list with wider padding:
 * it is a second drawing, and the pit wall is where it is drawn.
 *
 * The header follows the row where the row is the shorter of the two, which is what the tower's
 * 28 px `.th` is and what leaves its header and its rows the same pitch.
 */
const BOARD_PAD_X = 16;
const BOARD_HEADER_HEIGHT = 32;

/**
 * Which of the two drawings a table is.
 *
 * Its header tells them apart, and only on a pit wall page: a zone carries a title of its own and
 * no list on the catalogue or on a face artboard is headed at all, so a table that draws column
 * labels at a zone density is one of the three boards. The companion's leaderboard draws a legend
 * over the catalogue's row, which is the row it has drawn since the second screens shipped and
 * which no companion artboard contradicts.
 */
const isBoard = (density: Density, header: boolean): boolean => header && isZone(density);

/** Side padding of a row: the board's 16, or the catalogue's 6. */
const padXOf = (board: boolean): number => (board ? BOARD_PAD_X : ROW_PAD_X);

/** Gap between two rows. A board has none: its rows are flush and a 1 px rule closes each. */
const rowGapOf = (board: boolean): number => (board ? 0 : ROW_GAP);

/** Height of the column-label row. */
const headerHeightOf = (board: boolean, rowHeight: number): number => (board ? Math.min(BOARD_HEADER_HEIGHT, rowHeight) : HEADER_HEIGHT);

/**
 * How tall a row is at each density.
 *
 * Mechanism 1 of docs/design/readability-pass.md: a table answered a taller box with more rows of
 * the same size, so a list page never filled its box and the relative -- the page a driver reads
 * most -- drew the column that says *who* at the smallest size in the design. The row is declared
 * here instead, and it is a ceiling rather than a ramp: a table types up to this and no further,
 * and buys rows with whatever height is left.
 */
const ROW_HEIGHT: Record<Density, number> = { companion: 38, zone: 34, wide: 34, compact: 28 };

/** The row height a table takes at a density unless its caller declares one. */
export const tableRowHeight = (density: Density): number => ROW_HEIGHT[density];

/** The sizes the cells of a row of this height are drawn at. */
interface RowType {
  /** Position, gap and the lap times: the numbers a driver reads across a row. */
  lead: number;
  /** The car number, which the companion's taller row promotes to the lead size. */
  minor: number;
  /** The iRating, which the canvas draws at one size wherever it appears. */
  rating: number;
  /** The driver name, which is Barlow Medium and never monospaced. */
  name: number;
}

/**
 * The type a row of this height carries.
 *
 * The canvas draws 34 / 24 in its 34 px row and steps both down one in the 28 px row a narrow zone
 * takes; the companion's 38 px row promotes the car number to the position's size. The name stops
 * at 13 rather than following the compact ramp down to 12, which `density.ts` itself calls the
 * floor below which a label stops being readable at arm's length.
 *
 * A board is read across a garage rather than at arm's length and types the other way about: the
 * pit wall artboards draw every `.trow` with a 15 px name under 24 px numerals, with the car
 * number and the rank held at 16 beside them, and the tower brings the numerals down to 16 as well
 * rather than shortening the name. Panels.dc.html states the same ramp in words.
 */
function rowTypeOf(rowHeight: number, board = false): RowType {
  if (board) {
    const lead = rowHeight >= 32 ? 24 : 16;
    return { lead, minor: 16, rating: lead, name: 15 };
  }
  if (rowHeight >= 38) return { lead: 34, minor: 34, rating: 24, name: 15 };
  if (rowHeight >= 34) return { lead: 34, minor: 24, rating: 24, name: 13 };
  return { lead: 24, minor: 16, rating: 24, name: 13 };
}

/**
 * The name a flexible driver column is measured for: a full first and last name at the row's size.
 *
 * This used to be four characters' worth, which is a column that fits "Toma". WPF clips in silence,
 * so what that produced was not a narrow column but a cut name: at 800 x 480 zone C drew "Tomasz
 * Kowalcz" with the class chip hard against it. A column narrower than this draws the code form
 * instead, and a row that cannot hold even that sheds a column.
 */
const NAME_TO_FIT = 'Tomasz Kowalczyk';

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
  /** The sizes this row draws its cells at. */
  type: RowType;
  /** True on the player's own row. */
  isPlayer: Expr;
  /** True when the car is in the pit lane, which dims its row. */
  inPit: Expr;
  mode: TableMode;
  /** How the column this cell belongs to is aligned. */
  align: HAlign;
}

/** What a column is measured against: the density it is drawn at and the type its row carries. */
interface RowSpec {
  d: DensitySpec;
  type: RowType;
}

interface ColumnDef {
  header: string;
  align: HAlign;
  /** Column width in a row of this type; 0 makes the column take the row's remaining width. */
  width: (row: RowSpec) => number;
  cell: (ctx: CellContext) => Item[];
}

/**
 * A monospaced column's width: the canvas's number, or what its widest value needs, whichever is
 * larger.
 *
 * The canvas sizes each column from the sample it drew -- 40 px holds "P1" at 34 px and 92 px holds
 * "−5.886" -- and a real field reaches P24 and a real gap reaches −12.345, which are a cell wider
 * each. WPF clips whatever overruns `MaxTextWidth` in silence, so rule 19 wins over the drawn
 * number: the column keeps the canvas's width as its floor and grows to hold what it can draw.
 */
const cellColumn = (drawn: number, fs: number, chars: Chars): number => Math.max(drawn, monoWidth(cells('SemiBold', fs), chars));

/** The colour of a cell the own row does not lift: dim for a car in the pit lane, secondary otherwise. */
const inkBind = (ctx: CellContext): Expr => iff(ctx.inPit, str(ds.color.text.dim), str(ds.color.text.secondary));

/** The three cells the own row lifts: its position, its name and its gap. The rest keep their ink. */
const liftBind = (ctx: CellContext, otherwise: Expr): Expr => iff(ctx.isPlayer, str(ds.color.text.primary), otherwise);

/**
 * A numeral cell, vertically centred in the row.
 *
 * A right-aligned cell is drawn right-aligned, which is not the same as a box whose right edge
 * meets the column's. The box used to be placed at the column's edge and left the run `hAlign`
 * left inside cells cut for the widest value the column can hold, so P1 sat a whole digit cell
 * short of the edge and so did a `+9.9` measured against `+12.6`. `numeral` documents `width` as
 * the option for a value that does not fill its cells, and that is what this passes.
 */
function cellValue(ctx: CellContext, id: string, sample: string, bind: Expr, chars: Chars, opts: { fs?: number; color?: string; colorBind?: Expr; align?: HAlign } = {}): Item[] {
  const fs = opts.fs ?? ctx.type.lead;
  const align = opts.align ?? ctx.align;
  return [
    numeral(`${ctx.name}.${id}`, sample, ctx.x, ctx.top + (ctx.height - fs) / 2, fs, chars, {
      bind,
      color: (opts.color as `#${string}`) ?? ds.color.text.secondary,
      colorBind: opts.colorBind ?? inkBind(ctx),
      hAlign: align,
      ...(align === 'right' ? { width: ctx.width } : { maxWidth: ctx.width }),
    }),
  ];
}

/**
 * A proportional text cell: the driver name, which is Barlow Medium and never monospaced.
 *
 * Two forms, and the column picks between them rather than the renderer: a full name where the
 * column can hold one, and the three-letter code the narrow drawings show where it cannot. WPF has
 * no ellipsis to give -- it clips, and XOR-121 records that a name longer than its column has
 * nowhere to go -- so the cut is made in NCalc, the way `chipText` cuts a class name. The own row
 * says YOU, which is the one row a driver does not need to read a name to identify.
 */
function cellName(ctx: CellContext): Item[] {
  const fs = ctx.type.name;
  const full = ctx.width >= Math.ceil(measureText('BarlowMedium', NAME_TO_FIT, fs));
  const bind = iff(ctx.isPlayer, str('YOU'), full ? carName(ctx.idx) : driverCode(ctx.idx));
  return [
    {
      ...label(`${ctx.name}.name`, 'KLX', ctx.x, ctx.top + (ctx.height - fs) / 2, ctx.width, {
        size: fs,
        color: ds.color.text.secondary,
        bind,
      }),
      ...withBindings({ Text: bind, TextColor: liftBind(ctx, inkBind(ctx)) }),
    },
  ];
}

/**
 * The rank column. SimHub cannot draw a triangle, so a gain is a 6 px square in the gain's colour
 * with the count beside it, and an unchanged position is a short dash.
 */
function cellRank(ctx: CellContext): Item[] {
  const change = carRankChange(ctx.idx);
  const gained = gt(change, num(0));
  const moved = ne(change, num(0));
  const fs = ctx.type.minor;
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
    ...cellValue(ctx, 'pit', '1', carPitCount(ctx.idx), { digits: 2, specials: 0 }, { fs: ctx.type.minor, align: 'right' }).map((item) => ({
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

/** A compound chip holds one letter, so it is the padding and a letter's width either side of it. */
const compoundChipWidth = (d: DensitySpec): number => Math.ceil(2 * d.chipPadding + 14);

/** The position, which the canvas prefixes with a P: `P4`, not `4`. */
const positionText = (idx: Expr): Expr => concat(str('P'), fmt(carPosition(idx), '0'));

/** `P` and two digits, all four of which are drawn in the digit cell: `P` is 0.457 em against 0.47. */
const POSITION_CHARS: Chars = { digits: 3, specials: 0 };

const COLUMNS: Record<ColumnId, ColumnDef> = {
  pos: {
    header: 'Pos',
    align: 'right',
    width: ({ type }) => cellColumn(40, type.lead, POSITION_CHARS),
    cell: (ctx) =>
      cellValue(ctx, 'pos', 'P4', positionText(ctx.idx), POSITION_CHARS, {
        color: ds.color.text.label,
        colorBind: liftBind(ctx, str(ds.color.text.label)),
      }),
  },
  rank: { header: '±', align: 'right', width: ({ type }) => cellColumn(40, type.minor, { digits: 2, specials: 0 }), cell: cellRank },
  /**
   * The nationality flag, declared and unfilled.
   *
   * The canvas draws an 18 by 13 tricolour in a 24 px cell, which is a picture and not a text:
   * `design/assets.ts` is the mechanism, an `ImageItem` referencing a file the build packs. The
   * artwork does not exist -- a flag per country, each owing a notice -- and SimHub publishes no
   * per-car nationality this package has verified, so the column holds its place in the row and
   * draws nothing rather than being invented.
   */
  flag: { header: '', align: 'left', width: () => 24, cell: () => [] },
  num: {
    header: '#',
    align: 'left',
    width: ({ type }) => cellColumn(40, type.minor, CHARS.carNumber),
    cell: (ctx) => cellValue(ctx, 'num', '22', carNumber(ctx.idx), CHARS.carNumber, { fs: ctx.type.minor, color: ds.color.text.label, colorBind: str(ds.color.text.label) }),
  },
  /** The flexible column, with the floor the canvas gives it: below 60 px the row sheds instead. */
  name: { header: 'Driver', align: 'left', width: () => 0, cell: cellName },
  class: {
    header: 'Class',
    align: 'left',
    width: ({ d }) => Math.ceil(2 * d.chipPadding + 34),
    cell: (ctx) =>
      chip(`${ctx.name}.class`, 'GT3', ctx.x, ctx.top + (ctx.height - ctx.d.chipHeight) / 2, ctx.density, {
        bind: chipText(carClass(ctx.idx)),
        invertedBind: ctx.isPlayer,
        width: Math.ceil(2 * ctx.d.chipPadding + 34),
      }),
  },
  /**
   * The licence badge and its safety rating, declared and unfilled.
   *
   * A 30 px cell on a zone row and 62 on the companion, holding an 18 px badge whose letter is the
   * iRacing licence class. SimHub publishes the licence out of the session YAML and this package
   * has verified no reader for it, so the slot is declared and the cell draws nothing; `chip()`
   * takes the height and the text size the badge asks for, which is the half of it that is code.
   */
  licence: { header: 'Licence', align: 'left', width: ({ type }) => (type.name >= 15 ? 62 : 30), cell: () => [] },
  gap: {
    header: 'Gap',
    align: 'right',
    width: ({ type }) => cellColumn(92, type.lead, CHARS.relativeGap),
    cell: (ctx) =>
      ctx.mode === 'relative'
        ? // The own row is its own reference, so its gap is nought by definition rather than by
          // whatever `relativegaptoplayer` answers for the player's own index.
          cellValue(ctx, 'gap', '-5.886', iff(ctx.isPlayer, str('0.000'), carRelativeGap(ctx.idx)), CHARS.relativeGap, { colorBind: liftBind(ctx, inkBind(ctx)) })
        : cellValue(ctx, 'gap', '+12.6', carRaceGap(ctx.idx), CHARS.gap, { colorBind: liftBind(ctx, inkBind(ctx)) }),
  },
  int: { header: 'Int', align: 'right', width: ({ type }) => cellColumn(88, type.lead, CHARS.gap), cell: (ctx) => cellValue(ctx, 'int', '+2.6', carInterval(ctx.idx), CHARS.gap) },
  last: { header: 'Last', align: 'right', width: ({ type }) => cellColumn(98, type.lead, CHARS.lapTime), cell: (ctx) => cellValue(ctx, 'last', '1:42.905', carLastLap(ctx.idx), CHARS.lapTime) },
  best: {
    header: 'Best',
    align: 'right',
    width: ({ type }) => cellColumn(98, type.lead, CHARS.lapTime),
    cell: (ctx) =>
      cellValue(ctx, 'best', '1:41.877', carBestLap(ctx.idx), CHARS.lapTime, {
        colorBind: iff(carIsSessionBest(ctx.idx), str(ds.purpose.lap.sessionBest), inkBind(ctx)),
      }),
  },
  s1: { header: 'S1', align: 'right', width: ({ type }) => cellColumn(72, type.minor, CHARS.sector), cell: (ctx) => cellValue(ctx, 's1', '28.41', carSector(ctx.idx, 1), CHARS.sector, { fs: ctx.type.minor }) },
  s2: { header: 'S2', align: 'right', width: ({ type }) => cellColumn(72, type.minor, CHARS.sector), cell: (ctx) => cellValue(ctx, 's2', '41.07', carSector(ctx.idx, 2), CHARS.sector, { fs: ctx.type.minor }) },
  s3: { header: 'S3', align: 'right', width: ({ type }) => cellColumn(72, type.minor, CHARS.sector), cell: (ctx) => cellValue(ctx, 's3', '32.83', carSector(ctx.idx, 3), CHARS.sector, { fs: ctx.type.minor }) },
  stint: { header: 'Stint', align: 'right', width: ({ type }) => cellColumn(52, type.minor, { digits: 2, specials: 0 }), cell: (ctx) => cellValue(ctx, 'stint', '12', carStintLaps(ctx.idx), { digits: 2, specials: 0 }, { fs: ctx.type.minor }) },
  pit: { header: 'Pit', align: 'right', width: ({ d }) => Math.ceil(2 * d.chipPadding + 26), cell: cellPit },
  tyre: {
    header: 'Tyre',
    align: 'right',
    width: ({ d }) => compoundChipWidth(d),
    cell: (ctx) => {
      // A chip narrower than its column is drawn at the column's right edge, the way `cellPit`
      // draws its own: the column declares `right` and a chip filling it aligns nothing.
      const width = Math.min(ctx.width, compoundChipWidth(ctx.d));
      return chip(`${ctx.name}.tyre`, 'M', ctx.x + ctx.width - width, ctx.top + (ctx.height - ctx.d.chipHeight) / 2, ctx.density, {
        bind: chipText(carCompound(ctx.idx)),
        width,
      });
    },
  },
  rating: { header: 'iR', align: 'right', width: ({ type }) => cellColumn(54, type.rating, CHARS.rating), cell: (ctx) => cellValue(ctx, 'rating', '4.6k', carRating(ctx.idx), CHARS.rating, { fs: ctx.type.rating }) },
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
  /** Draw the header row. On by default; the face zones draw none. */
  header?: boolean;
  /** Row height; {@link tableRowHeight} by default. */
  rowHeight?: number;
  /**
   * When true, the table lists the player's own class rather than the whole field.
   *
   * An expression, because it is a plugin setting a driver changes mid-session and the file is
   * written once. It only reaches `mode: 'full'` and `mode: 'relative'`: `mode: 'class'` is already
   * one class and has nothing left to filter.
   */
  classOnly?: Expr;
}

/**
 * Which car a row addresses.
 *
 * SimHub has a class-only twin of each of the two lookups a table uses, so filtering to the
 * player's class is the same question asked of a different function rather than a row set built
 * somewhere else. With no `classOnly` the expression is the bare lookup it has always been, so
 * nothing the companion or the pit wall draws changes shape.
 */
function rowIndexFor(spec: TableSpec, centre: number): Expr {
  if (spec.mode === 'class') return rowIndex.inClass();
  const whole = spec.mode === 'relative' ? rowIndex.relative(centre) : rowIndex.full();
  if (!spec.classOnly) return whole;
  const inClass = spec.mode === 'relative' ? rowIndex.relativeInClass(centre) : rowIndex.inClass();
  return iff(spec.classOnly, inClass, whole);
}

/**
 * The widths of a table's columns, the name column taking what is left of the padded row.
 *
 * The row is inset either side, so the width the columns share is the frame's less that padding; a
 * column laid out against the frame's own edge would start in the padding and end outside it. A
 * board is inset by 16 rather than 6 and types its cells differently, so it asks for its own
 * widths and the name column is what pays the difference.
 */
export function columnWidths(columns: readonly ColumnId[], width: number, density: Density, rowHeight?: number, board = false): number[] {
  const d = densityOf(density);
  const row: RowSpec = { d, type: rowTypeOf(rowHeight ?? tableRowHeight(density), board) };
  const raw = columns.map((id) => COLUMNS[id].width(row));
  const fixed = raw.reduce((sum, w) => sum + w, 0);
  const gaps = CELL_GAP * Math.max(0, columns.length - 1);
  const flexColumns = raw.filter((w) => w === 0).length;
  const spare = Math.max(0, width - 2 * padXOf(board) - fixed - gaps);
  return raw.map((w) => (w === 0 ? Math.floor(spare / Math.max(1, flexColumns)) : w));
}

/** How many rows of `rowHeight` fit under the header. */
export function rowCapacity(frame: Rect, density: Density, header: boolean, rowHeight?: number): number {
  const d = densityOf(density);
  const h = rowHeight ?? d.rowHeight;
  const body = frame.height - (header ? d.headerHeight : 0);
  return Math.max(0, Math.floor(body / h));
}

/**
 * How many of the canvas's rows a box holds: the same question as {@link rowCapacity}, asked of the
 * table's own row rather than the density's, with the 2 px between rows and the 16 px header.
 *
 * Kept apart from `rowCapacity` because lap history draws its own header at the density's height
 * and stacks its rows flush; this is what the two list pages count with.
 */
export function rowsThatFit(frame: Rect, opts: { density: Density; header: boolean; rowHeight?: number }): number {
  const board = isBoard(opts.density, opts.header);
  const h = opts.rowHeight ?? tableRowHeight(opts.density);
  const gap = rowGapOf(board);
  const body = frame.height - (opts.header ? headerHeightOf(board, h) : 0);
  return Math.max(0, Math.floor((body + gap) / (h + gap)));
}

/** The header row: a label per column, aligned as its cells are, closed on a board by its rule. */
function headerRow(spec: TableSpec, widths: number[], top: number, board: boolean): Item[] {
  const d = densityOf(spec.density);
  const height = headerHeightOf(board, spec.rowHeight ?? tableRowHeight(spec.density));
  const items: Item[] = board ? [rule(`${spec.name}.head.rule`, spec.frame.left, top + height - 1, spec.frame.width, 1)] : [];
  let x = spec.frame.left + padXOf(board);
  spec.columns.forEach((id, i) => {
    const width = widths[i] ?? 0;
    const column = COLUMNS[id];
    const text = column.header;
    // Measured as `label` draws it: a header carries no binding, so it is upper-cased on the way in
    // and a box measured from the canvas's own capitalisation is a box the drawn text overruns.
    const drawn = Math.ceil(measureText('BarlowMedium', text.toUpperCase(), d.labelSm));
    const left = column.align === 'right' ? x + width - drawn : x;
    items.push(label(`${spec.name}.head.${id}`, text, left, top + (height - d.labelSm) / 2, Math.max(drawn, 0), { size: d.labelSm }));
    x += width + CELL_GAP;
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
  const rowHeight = spec.rowHeight ?? tableRowHeight(spec.density);
  const board = isBoard(spec.density, header);
  const padX = padXOf(board);
  const rowGap = rowGapOf(board);
  const headerHeight = headerHeightOf(board, rowHeight);
  const type = rowTypeOf(rowHeight, board);
  const capacity = rowsThatFit(spec.frame, { density: spec.density, header, rowHeight });
  const rows = Math.max(1, Math.min(spec.rows ?? capacity, capacity));
  const widths = columnWidths(spec.columns, spec.frame.width, spec.density, rowHeight, board);
  const headTop = spec.frame.top + (header ? headerHeight : 0);
  // The canvas gives every list body `justify-content: center`, so what a declared row count leaves
  // over is shared above and below the block rather than piled under it.
  const body = spec.frame.height - (header ? headerHeight : 0);
  const top = headTop + Math.max(0, Math.round((body - (rows * rowHeight + (rows - 1) * rowGap)) / 2));
  // The player sits in the middle of a relative table, so the row index counts from that row.
  const centre = Math.ceil(rows / 2);
  const idx = rowIndexFor(spec, centre);
  const isPlayer = carIsPlayer(idx);
  const inPit = carInPit(idx);

  const children: Item[] = [
    { ...band(`${spec.name}.row.background`, rect(spec.frame.left, top, spec.frame.width, rowHeight), ds.color.surface.zone), ...withBindings({ Visible: isPlayer }) },
    // The board's rows are flush and each is closed by a rule; a list's are two apart and closed by
    // the gap. Both run the frame's full width, under the padding the cells are inset by.
    ...(board ? [rule(`${spec.name}.row.rule`, spec.frame.left, top + rowHeight - 1, spec.frame.width, 1)] : []),
  ];
  let x = spec.frame.left + padX;
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
        type,
        isPlayer,
        inPit,
        mode: spec.mode,
        align: COLUMNS[id].align,
      }),
    );
    x += width + CELL_GAP;
  });

  const row: LayerItem = { kind: 'layer', name: `${spec.name}.row`, children, ...withBindings({ Visible: carAvailable(idx) }) };
  const stamped: LayerItem = { kind: 'layer', name: `${spec.name}.rows`, children: [row], repetitions: rows - 1, repeatTopOffset: rowHeight + rowGap, repeatLeftOffset: 0 };
  return [...(header ? headerRow(spec, widths, spec.frame.top, board) : []), stamped];
}

/** Every column the tables can show, for the docs and for a test that keeps them in step. */
export const COLUMN_IDS = Object.keys(COLUMNS) as ColumnId[];
