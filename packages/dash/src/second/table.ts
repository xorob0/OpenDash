/**
 * The leaderboard, relative and lap-history tables: one row definition that SimHub stamps N times.
 *
 * A repeated Layer clones its children per row and gives each copy a `repeatindex()`, so every
 * cell addresses its own car through one index expression. The rows layer holds an inner layer per
 * row whose Visible is the row's "is there a car here" test: SimHub evaluates a child of a repeated
 * layer inside that copy's repeat context, whereas the repeated layer's own Visible would be
 * evaluated once, for row one, and hide or show all of them together.
 *
 * Class rows cannot be grouped under class headings, which is what the three pit wall artboards
 * and `Panels.dc.html` draw: a 28 px row in `purpose.block.well` heading each class with that
 * class's leader, `GT3 · P1` and then `GT4 · P9`. Two things are missing and neither is a colour.
 * SimHub names the classes in a session -- `getleaderboardcarclasscount`,
 * `getleaderboardcarclassname(n)` and `getleaderboardcarclassopponentscount(n)` -- and publishes
 * exactly one per-class ordering, `getopponentleaderboardposition_playerclassonly`, the player's
 * own; the m-th car of an arbitrary class has no expression, so neither the cars under a heading
 * nor the heading's own leader can be addressed. And a repeated layer stamps one row at one
 * `repeatTopOffset`, so a heading inserted between two groups has no row of its own to sit in and
 * nothing below it can be pushed down by it. The table is therefore one continuous list in
 * leaderboard order with the class as a chip on each row, and the well stays unpainted until both
 * halves exist.
 */
import type { HAlign, Item, LayerItem, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { assetBox, imageOf, RANK_DOWN, RANK_UP } from '../design/assets.ts';
import { rect } from '../design/geometry.ts';
import { cells, monoWidth, MINUS, type Chars } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { rule } from '../elements/rule.ts';
import { ds } from '../tokens.ts';
import { chip, chipText, chipWidth } from './chip.ts';
import { densityOf, type Density, type DensitySpec } from './density.ts';
import { CHARS, carAvailable, carBestLap, carClass, carClassInterval, carClassRaceGap, carCompound, carInPit, carInterval, carIsPlayer, carIsSessionBest, carLastLap, carName, carNumber, carPitCount, carPosition,
  positionLabelled, carRaceGap, carRankChange, carRating, carRelativeGap, carSector, carStintLaps, driverCode, rowIndex, rowsInClass, splitHiddenCars } from './values.ts';

const { iff, str, fmt, eq, ne, num, and, not, gt, lt, abs, concat, left, ucase, isnull } = ncalc;

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
 * A board butts its cells where a list spaces them.
 *
 * Neither `.trow` nor `.th` declares a `gap` on any of the four artboards, and their cells are
 * `flex: none`, so a board's columns meet: what separates two values is the slack inside the wider
 * of the two columns rather than a gap between them. That is not a detail. The race board's
 * eighteen columns come to 1216 inside 1248 of padded frame, and twelve pixels seventeen times over
 * is another 204, so the canvas's own column set only fits a board at this gap.
 */
const BOARD_CELL_GAP = 0;

/**
 * The limit line: what a split list draws where it cuts the field.
 *
 * `Panels.dc.html` draws a 20 px band padded like a row it stands between, a 13 px uppercase label
 * centred in it and a 1 px line in `text.primary` running from either side of the label out to the
 * row's own inset, twelve pixels clear of the text. The lines flank the label rather than close the
 * band above and below: two full-width lines are what a board already draws under every row, and
 * the reader would count them as rules rather than read them as a cut.
 */
const SPLIT_HEIGHT = 20;
const SPLIT_CLEARANCE = 12;

/**
 * The copy the canvas writes on the limit line, and the widest count that can precede it.
 *
 * The count is bound, so it is the `widest` that is measured and not the sample: two digits, since
 * no grid a sim publishes reaches a hundred cars, and the four because it is the widest digit
 * Barlow Medium draws. A `widest` left undeclared is what once turned "NO FLAG" into "NO FLA".
 */
const SPLIT_COPY = 'CARS NOT SHOWN';
const SPLIT_WIDEST = `44 ${SPLIT_COPY}`;

/** Side padding of a row: the board's 16, or the catalogue's 6. */
const padXOf = (board: boolean): number => (board ? BOARD_PAD_X : ROW_PAD_X);

/** Gap between two cells of a row. */
const cellGapOf = (board: boolean): number => (board ? BOARD_CELL_GAP : CELL_GAP);

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
const ROW_HEIGHT: Record<Density, number> = { companion: 38, zone: 34, wide: 34, compact: 28, panel: 34 };

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
  /**
   * The condition under which the rows of this block are the player's own class, absent on a block
   * whose rows never are. A cell measuring a gap to a car above it asks this, so that what it
   * counts from is on the list it is drawn in.
   */
  inClass?: Expr;
  /** How the column this cell belongs to is aligned. */
  align: HAlign;
}

/** What a column is measured against: the density it is drawn at, the type its row carries, and which of the two drawings it belongs to. */
interface RowSpec {
  d: DensitySpec;
  type: RowType;
  board: boolean;
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

/**
 * The width the canvas draws a column at, which depends on which of the two tables it is in.
 *
 * The catalogue and the pit wall artboards state a width per column each and they disagree on
 * nearly all of them, because they are not the same drawing at two sizes: a list reads a 34 px
 * position at arm's length in a 40 px column, and a board reads a 24 px one across a garage in a
 * 44 px column with no gap to its neighbour. Both numbers are the canvas's own and both stay here,
 * side by side, rather than one of them being derived from the other.
 */
const drawnWidth = (row: RowSpec, list: number, board: number): number => (row.board ? board : list);

/** The colour of a cell the own row does not lift: dim for a car in the pit lane, secondary otherwise. */
const inkBind = (ctx: CellContext): Expr => iff(ctx.inPit, str(ds.color.text.dim), str(ds.color.text.secondary));

/** The three cells the own row lifts: its position, its name and its gap. The rest keep their ink. */
const liftBind = (ctx: CellContext, otherwise: Expr): Expr => iff(ctx.isPlayer, str(ds.color.text.primary), otherwise);

/**
 * A cell measured against a car above it, which has to be a car the list actually draws.
 *
 * Gap and Int are the two and they answer together, Int being the difference of two neighbouring
 * Gaps: a board counting the one from the class leader while the other counted between leaderboard
 * neighbours would draw two columns of the same quantity that do not add up. The question is the
 * one the rows themselves are drawn by, so the three agree by construction: a table in `mode:
 * 'class'` is one class and asks nothing, a block whose rows are always the whole field asks
 * nothing either, and everything else carries the condition {@link rowIndexFor} carries.
 */
const measuredInList = (ctx: CellContext, inClass: (idx: Expr) => Expr, whole: (idx: Expr) => Expr): Expr => {
  if (ctx.mode === 'class') return inClass(ctx.idx);
  return ctx.inClass === undefined ? whole(ctx.idx) : iff(ctx.inClass, inClass(ctx.idx), whole(ctx.idx));
};

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
 * no ellipsis to give -- it clips, and #172 records that a name longer than its column has
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

/** The rank triangle's side. Ten on every artboard that heads the column, whatever its row height. */
const RANK_MARK = 10;

/**
 * The rank column: the places a car has gained or lost, as the canvas's 10 px triangle with the
 * count beside it, and a short dash where the position has not moved.
 *
 * The triangle is a picture because SimHub draws rectangles, ellipses and text and a triangle is
 * none of the three; it is two pictures rather than one because an `ImageItem` has nothing that
 * tints what it draws, so up and down are two files shown by complementary tests. That is the one
 * mark on a row whose colour is not read from `design/tokens.json` at build time, which
 * `design/assets.ts` records against each file. The count and the dash are text and a rect, so
 * both keep their tokens.
 */
function cellRank(ctx: CellContext): Item[] {
  const change = carRankChange(ctx.idx);
  const gained = gt(change, num(0));
  const lost = lt(change, num(0));
  const moved = ne(change, num(0));
  const fs = ctx.type.minor;
  const mono = cells('SemiBold', fs);
  const countWidth = monoWidth(mono, { digits: 2, specials: 0 });
  const gap = 3;
  const right = ctx.x + ctx.width;
  const markerBox = rect(right - countWidth - gap - RANK_MARK, ctx.top + (ctx.height - RANK_MARK) / 2, RANK_MARK, RANK_MARK);
  const colour = iff(gained, str(ds.purpose.delta.faster), str(ds.purpose.delta.slower));
  return [
    ...([[RANK_UP, gained, 'up'], [RANK_DOWN, lost, 'down']] as const).map(([asset, visible, id]) => ({
      kind: 'image' as const,
      name: `${ctx.name}.rank.${id}`,
      image: asset.name,
      rect: assetBox(markerBox, imageOf(asset)),
      ...withBindings({ Visible: visible }),
    })),
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
const positionText = (idx: Expr): Expr => positionLabelled(idx);

/** `P` and two digits, all four of which are drawn in the digit cell: `P` is 0.457 em against 0.47. */
const POSITION_CHARS: Chars = { digits: 3, specials: 0 };

const COLUMNS: Record<ColumnId, ColumnDef> = {
  pos: {
    header: 'Pos',
    align: 'right',
    width: (row) => cellColumn(drawnWidth(row, 40, 44), row.type.lead, POSITION_CHARS),
    cell: (ctx) =>
      cellValue(ctx, 'pos', 'P4', positionText(ctx.idx), POSITION_CHARS, {
        color: ds.color.text.label,
        colorBind: liftBind(ctx, str(ds.color.text.label)),
      }),
  },
  /**
   * The rank, which is the one column the canvas states twice and not the same both times: a
   * board's `.th` gives the label 34 and its `.trow` gives the cell 36. A column is laid out once
   * for both rows, so the cell's number wins -- the cell is the half with a marker and a count in
   * it, and a header label of ten pixels has nothing to lose to the two.
   */
  rank: { header: '±', align: 'right', width: (row) => cellColumn(drawnWidth(row, 40, 36), row.type.minor, { digits: 2, specials: 0 }), cell: cellRank },
  /**
   * The nationality flag, declared and unfilled.
   *
   * The canvas draws an 18 by 13 tricolour in a 24 px cell, which is a picture and not a text:
   * `design/assets.ts` is the mechanism, an `ImageItem` referencing a file the build packs. The
   * artwork does not exist -- a flag per country, each owing a notice -- and SimHub publishes no
   * per-car nationality this package has verified, so the column holds its place in the row and
   * draws nothing rather than being invented.
   */
  flag: { header: '', align: 'left', width: (row) => drawnWidth(row, 24, 28), cell: () => [] },
  num: {
    header: '#',
    align: 'left',
    width: (row) => cellColumn(drawnWidth(row, 40, 44), row.type.minor, CHARS.carNumber),
    cell: (ctx) => cellValue(ctx, 'num', '22', carNumber(ctx.idx), CHARS.carNumber, { fs: ctx.type.minor, color: ds.color.text.label, colorBind: str(ds.color.text.label) }),
  },
  /**
   * The flexible column, with the floor the canvas gives it: below 60 px the row sheds instead.
   *
   * The pit wall artboards fix it at 190 and let the row stop short of its right edge, which is the
   * one number of a board this file does not take literally. Three of the eighteen columns the race
   * board is headed for have no source to fill them, so a fixed 190 would end that row a sixth of
   * the board from its edge rather than the thirty-two pixels the canvas leaves; the remainder goes
   * to the name instead, which is the column WPF punishes for being narrow.
   * `pitwallColumns.test.ts` holds it above the canvas's 190 so that the floor is what is checked.
   */
  name: { header: 'Driver', align: 'left', width: () => 0, cell: cellName },
  class: {
    header: 'Class',
    align: 'left',
    width: (row) => drawnWidth(row, Math.ceil(2 * row.d.chipPadding + 34), 56),
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
  licence: { header: 'Licence', align: 'left', width: (row) => drawnWidth(row, row.type.name >= 15 ? 62 : 30, 86), cell: () => [] },
  gap: {
    header: 'Gap',
    align: 'right',
    // The one column both drawings size the same, so it takes no board number of its own.
    width: ({ type }) => cellColumn(92, type.lead, CHARS.relativeGap),
    cell: (ctx) =>
      ctx.mode === 'relative'
        ? // The own row is its own reference, so its gap is nought by definition rather than by
          // whatever `relativegaptoplayer` answers for the player's own index. The sample carries
          // the typographic minus `signed` substitutes, so that what Dash Studio shows at design
          // time is the glyph the bound value draws rather than .NET's hyphen.
          cellValue(ctx, 'gap', `${MINUS}5.886`, iff(ctx.isPlayer, str('0.000'), carRelativeGap(ctx.idx)), CHARS.relativeGap, { colorBind: liftBind(ctx, inkBind(ctx)) })
        : cellValue(ctx, 'gap', '+12.6', measuredInList(ctx, carClassRaceGap, carRaceGap), CHARS.gap, { colorBind: liftBind(ctx, inkBind(ctx)) }),
  },
  int: { header: 'Int', align: 'right', width: (row) => cellColumn(drawnWidth(row, 88, 84), row.type.lead, CHARS.gap), cell: (ctx) => cellValue(ctx, 'int', '+2.6', measuredInList(ctx, carClassInterval, carInterval), CHARS.gap) },
  last: { header: 'Last', align: 'right', width: (row) => cellColumn(drawnWidth(row, 98, 92), row.type.lead, CHARS.lapTime), cell: (ctx) => cellValue(ctx, 'last', '1:42.905', carLastLap(ctx.idx), CHARS.lapTime) },
  best: {
    header: 'Best',
    align: 'right',
    width: (row) => cellColumn(drawnWidth(row, 98, 92), row.type.lead, CHARS.lapTime),
    cell: (ctx) =>
      cellValue(ctx, 'best', '1:41.877', carBestLap(ctx.idx), CHARS.lapTime, {
        colorBind: iff(carIsSessionBest(ctx.idx), str(ds.purpose.lap.sessionBest), inkBind(ctx)),
      }),
  },
  // The three samples are one lap's sectors and they add up to the `last` sample beside them, the
  // way every triple the canvas draws adds up to the time on its own row: 28.412, 41.071 and 33.422
  // are 1:42.905, each rounded to the two decimals a sector is drawn to. A row whose sectors and
  // whose lap time disagree is read at design time as a bug in the bindings, which is a morning
  // spent on a number nothing computes.
  s1: { header: 'S1', align: 'right', width: (row) => cellColumn(drawnWidth(row, 72, 58), row.type.minor, CHARS.sector), cell: (ctx) => cellValue(ctx, 's1', '28.41', carSector(ctx.idx, 1), CHARS.sector, { fs: ctx.type.minor }) },
  s2: { header: 'S2', align: 'right', width: (row) => cellColumn(drawnWidth(row, 72, 58), row.type.minor, CHARS.sector), cell: (ctx) => cellValue(ctx, 's2', '41.07', carSector(ctx.idx, 2), CHARS.sector, { fs: ctx.type.minor }) },
  s3: { header: 'S3', align: 'right', width: (row) => cellColumn(drawnWidth(row, 72, 58), row.type.minor, CHARS.sector), cell: (ctx) => cellValue(ctx, 's3', '33.42', carSector(ctx.idx, 3), CHARS.sector, { fs: ctx.type.minor }) },
  /** No board draws it: the canvas's three pages spend the room on Nat, Licence and iRating instead. */
  stint: { header: 'Stint', align: 'right', width: ({ type }) => cellColumn(52, type.minor, { digits: 2, specials: 0 }), cell: (ctx) => cellValue(ctx, 'stint', '12', carStintLaps(ctx.idx), { digits: 2, specials: 0 }, { fs: ctx.type.minor }) },
  pit: { header: 'Pit', align: 'right', width: (row) => drawnWidth(row, Math.ceil(2 * row.d.chipPadding + 26), 44), cell: cellPit },
  tyre: {
    header: 'Tyre',
    align: 'right',
    width: (row) => drawnWidth(row, compoundChipWidth(row.d), 40),
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
  // Headed by the word rather than by the abbreviation: every artboard that draws the column writes
  // "iRating" over it, and 76 px holds the 48 the label takes at 13.
  rating: {
    header: 'iRating',
    align: 'right',
    width: (row) => cellColumn(drawnWidth(row, 54, 76), row.type.rating, CHARS.rating),
    cell: (ctx) => cellValue(ctx, 'rating', '4.6k', carRating(ctx.idx), CHARS.rating, { fs: ctx.type.rating }),
  },
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
  /**
   * Which of the two drawings this table is: the pit wall's board, or the catalogue's list.
   *
   * Declared by the caller rather than inferred. It used to be read off the density and the header
   * together -- a headed table at a zone density could only be a pit wall page -- which was true of
   * the three callers there are and of nothing else: a zone that wanted a legend over its list, or
   * a board without one, would have got the other drawing without asking for it. The three pages
   * say so instead, and every other table keeps the list it already drew.
   */
  board?: boolean;
  /** Row height; {@link tableRowHeight} by default. */
  rowHeight?: number;
  /**
   * How many rows at the top of the field a split list keeps, the rest of them following the player
   * across a limit line. Absent, which is the default, the table is the one contiguous list it has
   * always been, so the face zones and the companion are untouched.
   */
  split?: number;
  /**
   * The zone's own answer to "does this list show the player's class", where the zone has one.
   *
   * An expression, because it is a plugin setting a driver changes mid-session and the file is
   * written once. It only reaches `mode: 'full'` and `mode: 'relative'`: `mode: 'class'` is already
   * one class and has nothing left to filter. It is not the whole condition either, the rig-wide
   * `PositionMode` being the other half of it; {@link rowsInClass} joins the two and a table that
   * passes nothing here still asks that one.
   */
  classOnly?: Expr;
}

/**
 * Which car a row addresses.
 *
 * SimHub has a class-only twin of each of the two lookups a table uses, so filtering to the
 * player's class is the same question asked of a different function rather than a row set built
 * somewhere else: the same rows are drawn either way and only the car each one addresses moves.
 *
 * Two settings reach the condition and {@link rowsInClass} joins them, which is why a table with no
 * `classOnly` of its own is still conditional. The zone's filter is the one a caller passes, and
 * the rig's `PositionMode` is read for every table there is, the companion's included: a column of
 * class positions drawn over the whole field numbers an order it did not sort.
 */
function rowIndexFor(spec: TableSpec, centre: number): Expr {
  if (spec.mode === 'class') return rowIndex.inClass();
  const whole = spec.mode === 'relative' ? rowIndex.relative(centre) : rowIndex.full();
  const inClass = spec.mode === 'relative' ? rowIndex.relativeInClass(centre) : rowIndex.inClass();
  return iff(rowsInClass(spec.classOnly), inClass, whole);
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
  const row: RowSpec = { d, type: rowTypeOf(rowHeight ?? tableRowHeight(density), board), board };
  const raw = columns.map((id) => COLUMNS[id].width(row));
  const fixed = raw.reduce((sum, w) => sum + w, 0);
  const gaps = cellGapOf(board) * Math.max(0, columns.length - 1);
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
export function rowsThatFit(frame: Rect, opts: { density: Density; header: boolean; rowHeight?: number; board?: boolean }): number {
  const board = opts.board ?? false;
  const h = opts.rowHeight ?? tableRowHeight(opts.density);
  const gap = rowGapOf(board);
  const body = frame.height - (opts.header ? headerHeightOf(board, h) : 0);
  return Math.max(0, Math.floor((body + gap) / (h + gap)));
}

/** The header row: a label per column, aligned as its cells are, closed on a board by its rule. */
function headerRow(spec: TableSpec, widths: number[], top: number, geometry: { height: number; padX: number; cellGap: number; board: boolean }): Item[] {
  const d = densityOf(spec.density);
  const { height, padX, cellGap, board } = geometry;
  const items: Item[] = board ? [rule(`${spec.name}.head.rule`, spec.frame.left, top + height - 1, spec.frame.width, 1)] : [];
  let x = spec.frame.left + padX;
  spec.columns.forEach((id, i) => {
    const width = widths[i] ?? 0;
    const column = COLUMNS[id];
    const text = column.header;
    // Measured as `label` draws it: a header carries no binding, so it is upper-cased on the way in
    // and a box measured from the canvas's own capitalisation is a box the drawn text overruns.
    const drawn = Math.ceil(measureText('BarlowMedium', text.toUpperCase(), d.labelSm));
    const left = column.align === 'right' ? x + width - drawn : x;
    items.push(label(`${spec.name}.head.${id}`, text, left, top + (height - d.labelSm) / 2, Math.max(drawn, 0), { size: d.labelSm }));
    x += width + cellGap;
  });
  return items;
}

/**
 * One block of rows: the row template, and the repeated layer that stamps it `rows` times. The
 * inner row layer carries the "this car exists" test so that empty rows draw nothing at all.
 *
 * A split list draws two blocks rather than one row index clever enough to be both. The index of
 * every cell of every row is that expression, so a conditional in it is a conditional SimHub
 * evaluates a few hundred times a tick, and the two halves of a split list differ in one thing
 * only: where they start counting.
 */
function rowBlock(spec: TableSpec, widths: number[], rowHeight: number, block: { name: string; top: number; rows: number; idx: Expr; inClass?: Expr }): LayerItem {
  const d = densityOf(spec.density);
  const board = spec.board ?? false;
  const { name, top, rows, idx, inClass } = block;
  const isPlayer = carIsPlayer(idx);
  const inPit = carInPit(idx);
  const children: Item[] = [
    { ...band(`${spec.name}.${name}.background`, rect(spec.frame.left, top, spec.frame.width, rowHeight), ds.color.surface.zone), ...withBindings({ Visible: isPlayer }) },
    // The board's rows are flush and each is closed by a rule; a list's are two apart and closed by
    // the gap. Both run the frame's full width, under the padding the cells are inset by.
    ...(board ? [rule(`${spec.name}.${name}.rule`, spec.frame.left, top + rowHeight - 1, spec.frame.width, 1)] : []),
  ];
  let x = spec.frame.left + padXOf(board);
  spec.columns.forEach((id, i) => {
    const width = widths[i] ?? 0;
    children.push(
      ...COLUMNS[id].cell({
        name: `${spec.name}.${name}`,
        idx,
        x,
        width,
        top,
        height: rowHeight,
        d,
        density: spec.density,
        type: rowTypeOf(rowHeight, board),
        isPlayer,
        inPit,
        mode: spec.mode,
        ...(inClass === undefined ? {} : { inClass }),
        align: COLUMNS[id].align,
      }),
    );
    x += width + cellGapOf(board);
  });
  const row: LayerItem = { kind: 'layer', name: `${spec.name}.${name}`, children, ...withBindings({ Visible: carAvailable(idx) }) };
  return { kind: 'layer', name: `${spec.name}.${name}s`, children: [row], repetitions: rows - 1, repeatTopOffset: rowHeight + rowGapOf(board), repeatLeftOffset: 0 };
}

/**
 * The limit line, and why it is not a row.
 *
 * SimHub evaluates a repeated layer's own Visible once, for row one, which is what the head of this
 * file records; a band living inside the repeat would therefore show on every row or on none. It is
 * a fixed item between the two blocks instead, and what it says is bound: the count depends on
 * where the player is, and a label whose text moves is measured by its `widest` rather than by the
 * sample Dash Studio shows.
 *
 * The lines either side are drawn from the label outwards, so a band too narrow to hold both the
 * label and its clearance draws the label alone rather than a line through the text.
 */
function limitLine(spec: TableSpec, top: number, hidden: Expr): Item[] {
  const d = densityOf(spec.density);
  const padX = padXOf(spec.board ?? false);
  const fs = d.labelSm;
  // A pixel over what the widest count draws: WPF clips at the box's edge, and a box cut to the
  // exact advance loses the last glyph's final column.
  const width = Math.ceil(measureText('BarlowMedium', SPLIT_WIDEST, fs)) + 1;
  const left = spec.frame.left + Math.round((spec.frame.width - width) / 2);
  const right = left + width;
  const shown = gt(hidden, num(0));
  const lines: [string, number, number][] = [
    ['before', spec.frame.left + padX, left - SPLIT_CLEARANCE],
    ['after', right + SPLIT_CLEARANCE, spec.frame.left + spec.frame.width - padX],
  ];
  return [
    ...lines
      .filter(([, from, to]) => to > from)
      .map(([id, from, to]) => band(`${spec.name}.limit.${id}`, rect(from, top + (SPLIT_HEIGHT - 1) / 2, to - from, 1), ds.color.text.primary, { visibleBind: shown })),
    label(`${spec.name}.limit.count`, `7 ${SPLIT_COPY}`, left, top + (SPLIT_HEIGHT - fs) / 2, width, {
      size: fs,
      hAlign: 'center',
      bind: concat(fmt(hidden, '0'), str(` ${SPLIT_COPY}`)),
      widest: SPLIT_WIDEST,
      visibleBind: shown,
    }),
  ];
}

/**
 * The table: the header, then the rows the frame holds, in one block or in the two a split list
 * draws with its limit line between them.
 */
export function table(spec: TableSpec): Item[] {
  const header = spec.header ?? true;
  const rowHeight = spec.rowHeight ?? tableRowHeight(spec.density);
  const board = spec.board ?? false;
  const padX = padXOf(board);
  const cellGap = cellGapOf(board);
  const rowGap = rowGapOf(board);
  const headerHeight = headerHeightOf(board, rowHeight);
  const fit = { density: spec.density, header, rowHeight, board };
  if (spec.split !== undefined && (spec.mode !== 'full' || spec.classOnly)) {
    // The kept rows are the head of the overall leaderboard and the window below counts in the same
    // numbers; a class filter would restart both at one and the limit line would count a field the
    // rows above it are not drawn from.
    //
    // Which is why the rig-wide `PositionMode` is not refused here as `classOnly` is: it is a
    // runtime setting and nothing built once can throw on it. A split list in class mode would
    // therefore number an overall field by class, which is #212 over again; no page draws one
    // today, and the page that first does has to answer the limit line before it answers this.
    throw new Error(`table ${spec.name}: a split list is the overall leaderboard, so it takes neither a class mode nor classOnly`);
  }
  // The limit line costs a row's worth of the body and is laid out whether or not the field is long
  // enough to hide anything behind it, since nothing about a stamped layout moves at runtime. A box
  // too short to hold a block either side of it draws the plain list instead.
  const short = rect(spec.frame.left, spec.frame.top, spec.frame.width, spec.frame.height - SPLIT_HEIGHT - rowGap);
  const splits = (spec.split ?? 0) > 0 && rowsThatFit(short, fit) >= 2;
  const capacity = rowsThatFit(splits ? short : spec.frame, fit);
  const rows = Math.max(1, Math.min(spec.rows ?? capacity, capacity));
  const topRows = splits ? Math.min(spec.split ?? 0, rows - 1) : 0;
  const widths = columnWidths(spec.columns, spec.frame.width, spec.density, rowHeight, board);
  const headTop = spec.frame.top + (header ? headerHeight : 0);
  // The canvas gives every list body `justify-content: center`, so what a declared row count leaves
  // over is shared above and below the block rather than piled under it.
  //
  // A board is the other drawing here too, and the other way about: the column holding its header
  // and its rows is a plain `flex-direction: column` on all four artboards that draw one, with no
  // `justify-content` at all, so the rows open against the header rule and whatever the field is
  // short of falls at the foot. Centring them is what put 84 px of margin over P1 on the race page
  // and 158 on the tower, which is a board that has lost the edge a reader reads positions down.
  const body = spec.frame.height - (header ? headerHeight : 0);
  const stack = rows * rowHeight + (rows - 1) * rowGap + (splits ? SPLIT_HEIGHT + rowGap : 0);
  const slack = board ? 0 : Math.max(0, Math.round((body - stack) / 2));
  const top = headTop + slack;
  const head = header ? headerRow(spec, widths, spec.frame.top, { height: headerHeight, padX, cellGap, board }) : [];
  if (!splits) {
    // The player sits in the middle of a relative table, so the row index counts from that row.
    const idx = rowIndexFor(spec, Math.ceil(rows / 2));
    return [...head, rowBlock(spec, widths, rowHeight, { name: 'row', top, rows, idx, inClass: rowsInClass(spec.classOnly) })];
  }
  // The player sits in the middle of the window, as in a relative table, so a driver reads as many
  // cars ahead as behind whatever the field does around them.
  //
  // Neither block carries a class condition, because neither block's rows do: both are the overall
  // leaderboard, for the reason `rowIndex.split` records, so a gap measured from the class leader
  // would be measured from a car the list does not draw.
  const windowRows = rows - topRows;
  const bandTop = top + topRows * (rowHeight + rowGap);
  return [
    ...head,
    rowBlock(spec, widths, rowHeight, { name: 'row', top, rows: topRows, idx: rowIndex.full() }),
    ...limitLine(spec, bandTop, splitHiddenCars(topRows, windowRows)),
    rowBlock(spec, widths, rowHeight, { name: 'splitRow', top: bandTop + SPLIT_HEIGHT + rowGap, rows: windowRows, idx: rowIndex.split(topRows, windowRows) }),
  ];
}

/** Every column the tables can show, for the docs and for a test that keeps them in step. */
export const COLUMN_IDS = Object.keys(COLUMNS) as ColumnId[];
