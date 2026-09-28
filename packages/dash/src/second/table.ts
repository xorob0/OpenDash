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
import { withMoreBindings, type Expr } from '../bind.ts';
import { charsThatFit, dottedLetterSize, measureText, widestGlyph, widestOf, type MeasuredFace } from '../design/advances.ts';
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
import { CHARS, carAvailable, carBestLap, carClass, carClassInterval, carClassRaceGap, carCompound, carInPit, carInterval, carIsPlayer, carIsSessionBest, carLastLap, carNumber, carPitCount, carPosition,
  positionLabelled, carRaceGap, carRankChange, carRating, carRelativeGap, carSector, carStintLaps, driverName, ellipsised, rowIndex, rowsInClass, splitHiddenCars } from './values.ts';

const { iff, str, fmt, num, ne, not, gt, lt, abs, concat } = ncalc;

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
 * The row the canvas draws a list at, at each density.
 *
 * What a table takes when its caller states nothing, and the floor a {@link listPlan} counts its
 * rows at: a page's declared count is cut to what a box holds at this row, never at a shorter one.
 * It used to be the whole answer, and that was Mechanism 1 of the readability pass (#328): a table
 * answered a taller box with more rows of this size, so a 437 x 214 box and a 437 x 510 one differed
 * only in how many drivers they listed, and the relative drew the column that says *who* at the
 * smallest size in the design however much height it had.
 */
const ROW_HEIGHT: Record<Density, number> = { companion: 38, zone: 34, wide: 34, compact: 28, panel: 34 };

/** The row height a table takes at a density unless its caller declares one. */
export const tableRowHeight = (density: Density): number => ROW_HEIGHT[density];

/**
 * The type a list row can carry, smallest first, and the least row height each one needs.
 *
 * Four steps where there used to be three, and the one added is the one #328 is about. The canvas
 * draws 34 / 24 in its 34 px row and steps both down one in the 28 px row a narrow zone takes; the
 * companion's 38 px row promotes the car number to the position's size. Those are three drawings of
 * the whole row, and between the first two the name and the numerals move together: a row tall
 * enough for a 15 px name also took its position and its gap from 24 to 34. In a narrow zone that
 * is a trade the row cannot make. The position and the gap are monospaced columns of a fixed budget,
 * so their growth comes out of the one flexible column beside them, and on the 850 x 480 face it
 * would take the name from 82 px to 61 — which is why that face drew its name at 13 under however
 * much height it had.
 *
 * **The second step is the name alone**: the 28 px row's numerals and the 34 px row's name, for a
 * row tall enough to carry the larger name in a box too narrow to carry the larger numerals. It is
 * not a size the canvas draws; it is the canvas's two sizes of the one cell that flexes, which is
 * rule 20 read for a table whose width edge is met by the numerals before its height edge is met by
 * anything. Its height is the 34 px row's, the height the canvas draws a 15 px name in.
 *
 * `rowTypeOf` reads a height as the largest step it allows, so a caller that states only a height
 * gets the canvas's drawing as before; a {@link listPlan} chooses among the steps its height allows
 * and takes the name-only one where the width refuses the rest.
 */
export interface RowType {
  /** The least row height this type is drawn in. */
  from: number;
  /** Position, gap and the lap times: the numbers a driver reads across a row. */
  lead: number;
  /** The car number, which the companion's taller row promotes to the lead size. */
  minor: number;
  /** The iRating, which the canvas draws at one size wherever it appears. */
  rating: number;
  /** The driver name, which is Barlow Medium and never monospaced. */
  name: number;
}

export const LIST_ROW_TYPES: readonly RowType[] = [
  { from: 0, lead: 24, minor: 16, rating: 24, name: 13 },
  { from: 34, lead: 24, minor: 16, rating: 24, name: 15 },
  { from: 34, lead: 34, minor: 24, rating: 24, name: 15 },
  { from: 38, lead: 34, minor: 34, rating: 24, name: 15 },
];

/**
 * The type a row of this height carries: the largest step of {@link LIST_ROW_TYPES} it is tall enough
 * for, which is the canvas's own drawing at that height.
 *
 * **The name is 15 from the 34 px row up and 13 in the 28 px row**, where it used to be 13 in both.
 * That is #339: the relative is the page a driver reads most and it drew the column that says *who* at
 * the floor of the design, 13 px under a gap drawn at 34, on a screen 600 mm from the eye. The 34 px
 * row is the row of a zone read at arm's length, and 15 is what that face labels at everywhere else;
 * `density.ts` calls 13 the floor rather than the size.
 *
 * **The 28 px row keeps 13, which is the canvas's own number.** What changed with #328 is that a narrow
 * zone is no longer held to the 28 px row: a page that declares fewer rows than its box would hold at
 * 28 has the height for the name-only step, and `listPlan` takes it there. The trade that step makes
 * is one character of budget for two pixels of every letter left, and it is counted where it is made;
 * `tables.test.ts` holds the counts.
 *
 * A board is read across a garage rather than at arm's length and types the other way about: the
 * pit wall artboards draw every `.trow` with a 15 px name under 24 px numerals, with the car
 * number and the rank held at 16 beside them, and the tower brings the numerals down to 16 as well
 * rather than shortening the name. Panels.dc.html states the same ramp in words.
 */
function rowTypeOf(rowHeight: number, board = false): RowType {
  if (board) {
    const lead = rowHeight >= 32 ? 24 : 16;
    return { from: 0, lead, minor: 16, rating: lead, name: 15 };
  }
  let type = LIST_ROW_TYPES[0]!;
  for (const step of LIST_ROW_TYPES) if (step.from <= rowHeight) type = step;
  return type;
}

/** A row stated either way: as the height it is drawn at, or as the type a plan chose for it. */
export type RowSize = number | RowType;

/** The type a row stated either way carries; a bare height is read as the canvas reads it. */
const typeOfRow = (row: RowSize | undefined, density: Density, board = false): RowType =>
  typeof row === 'object' ? row : rowTypeOf(row ?? tableRowHeight(density), board);

/** The face a driver name is set in, which is Barlow Medium at every size and in both drawings. */
export const NAME_FACE: MeasuredFace = 'BarlowMedium';

/**
 * The shortest name any of the four formats draws, in characters: `L. Byrne` is eight.
 *
 * The floor a column that names a driver at all is measured against, and the reason it is a count
 * rather than a width. The cut is made in the expression, where nothing can measure a glyph, so the
 * budget is characters of the widest glyph the name face draws; a column sized to what those eight
 * letters actually need would be 49 px at 13 px and would cut `L. Byrne` to `L. B…`.
 *
 * `Byrne Liam` and a full name are longer and ellipsise where the room is only this. That is the
 * trade, and the shortest format is the one a driver picks when the room is tight. It replaces
 * `NAME_TO_FIT`, a sixteen-character `Tomasz Kowalczyk` that decided between a name and a code; there
 * is no code to decide about any more, so what a column needs is the least a name can be rather than
 * the most.
 */
export const SHORTEST_NAME_CHARS = 8;

/**
 * The name the canvas writes into the driver column, and the count {@link DEFAULT_NAME_CHARS} takes
 * from it.
 *
 * `Liam Byrne` is what every artboard draws there, what the `label` below draws at design time, and
 * what the default format — `full`, which is what a rig that never opens the setting gets — makes of
 * the entry the traces carry. So it is the one name whose fate a reader can check against a
 * screenshot, and the count is read off the string rather than written down beside it.
 *
 * Ten characters is not a promise that every full name fits: `Hannah Fischer` is fourteen and
 * ellipsises wherever the column holds ten. It is the width below which the *default* drawing of the
 * *default* format starts losing letters, which is the one place a reader would call the ellipsis a
 * fault rather than a trade, and it is therefore the floor a larger type is not allowed to take a
 * column under. `nameFloorOf` states the rule and `listPlan` is what enforces it.
 */
export const NAME_SAMPLE = 'Liam Byrne';

/** The characters the default format's own sample needs: `Liam Byrne` is ten. */
export const DEFAULT_NAME_CHARS = NAME_SAMPLE.length;

/**
 * The size from which a name is drawn as the sim spells it, and under which it is shouted.
 *
 * 25 px in the name face, and every size any table draws a name at is under it, so in practice every
 * list on every face shouts. It is written as a rule rather than as "always" because it is a rule: the
 * number is `2 / TITTLE_BREAK.BarlowMedium`, and a table that ever drew a name at 25 px would have
 * earned the sim's own spelling back.
 *
 * #339's relative is where it was found. At 13 px in the 28 px row, WPF welded the dot of the
 * lowercase `i` to its stem in four of the nine names on the VM's 850 x 480 face: `Liam Byrne` came
 * back `Llam B…`, `Nina Hartmann` `NIna H…`, `Sofia Rossi` `Sofla …` and `Henrik Solberg` `Henrlk…`.
 * That is not a blurred letter, it is a different one — Barlow's `i` and `l` are the same height to
 * within 0.017 em — on the one column of the one page whose whole job is to say *who*.
 *
 * The remedy is measured rather than chosen, and `advances.ts` holds the measurement. The gap between
 * the tittle and the stem is 0.080 em, which is 1.04 device pixels at 13 and 1.20 at 15: under two
 * pixels there is no pixel row the gap is certain to fall wholly inside, so the raster can shade both
 * rows and bridge them, and 13 is simply where that came up first. The other two candidates lose to
 * the same numbers. **A heavier weight makes it worse**: Barlow Bold's break is 0.057 em against
 * Medium's 0.080, a fatter stem and a fatter dot being drawn into the same vertical. **A bigger size
 * costs letters**: 15 px in the three narrow boxes buys 6, 5 and 4 characters where 13 buys 7, 6 and 4,
 * and it does not clear the bound either. **Upper case costs nothing at all**, which is the part worth
 * saying twice: the budget is counted in characters of the face's *widest* glyph, so shouting a name
 * changes no budget anywhere — `tables.test.ts`'s six counts are the same numbers after this as before
 * it — and no *unaccented* upper-case letter in any bundled face is drawn in two pieces, so the letter
 * that can be misread as a different letter has none left to be.
 *
 * What upper case does not buy is the construction. An accented capital is a mark floating over a
 * letter, exactly the shape the `i` failed at, and four of them are tighter than it: `É`, `Å`, `Í` and
 * `Ö` break at 0.064 to 0.076 em in the name face against the `i`'s 0.080, all under two device pixels
 * at 13 px and at 15. So RÄIKKÖNEN may still come back with an umlaut welded to its A. What that costs
 * is a letter drawn badly and not a name read wrongly — a welded tittle makes `Liam` into the legal
 * `Llam`, where a welded acute makes `É` into a misdrawn `É`, which is still the letter and still the
 * driver — and there is no tighter bound to reach for, these being the marks the bundled faces draw.
 * `advances.test.ts` measures both halves of that, the alphabet that is safe and the marks that are not.
 *
 * What it costs is the catalogue: every artboard draws `Liam Byrne` in the driver column. The face
 * upper-cases every other label it draws, the code this column replaced was `LIA`, and the player's
 * own row already says `YOU`, so the column is now the one thing on the row that is not shouted rather
 * than the one thing that is. `docs/design/zones.md` records the divergence.
 */
export const MIXED_CASE_NAME_SIZE = dottedLetterSize(NAME_FACE);

/** Whether a name set at this size is shouted, which is the whole of the rule above. */
export const nameIsUpperCased = (fs: number): boolean => fs < MIXED_CASE_NAME_SIZE;

/**
 * What a driver column draws: the rig's format, cut to the column's budget, in the case the size can
 * carry.
 *
 * One function because there are two callers — every table on every face and both blocks of the
 * opponents page — and a case rule applied in one of them would be a page that disagrees with the page
 * beside it about what a driver is called.
 *
 * The `ucase` goes outside the cut rather than inside it. It is the same string either way, the cut
 * being counted in characters, and outside it names the value once where inside it would name it
 * three times; `ellipsised` explains what a mention of this particular value costs.
 */
export const nameText = (idx: Expr, chars: number, fs: number): Expr => {
  const cut = ellipsised(driverName(idx), chars);
  return nameIsUpperCased(fs) ? ncalc.ucase(cut) : cut;
};

/**
 * And the design-time sample, in the case the row will really draw.
 *
 * `label` keeps a bound item's sample verbatim, so a column left with a mixed-case `Liam Byrne` would
 * show one thing in Dash Studio's editor and another on the rig. `elements/unit.ts` settled the same
 * question the same way. The Overview panel is how a whole package is read at once, and a sample that
 * is a lie about the built thing is worse there than anywhere.
 */
export const nameSampleAt = (fs: number): string => (nameIsUpperCased(fs) ? NAME_SAMPLE.toUpperCase() : NAME_SAMPLE);

/** The width a column wants before it draws a name: the shortest form's budget, with the pixel `label` leaves itself. */
export const nameColumnFloor = (fs: number): number => Math.ceil(SHORTEST_NAME_CHARS * widestGlyph(NAME_FACE).advance * fs) + 1;

/** The size a row sets a name in, which is what a caller measuring the column has to measure at. */
export const nameSizeForRow = (row: RowSize, board = false): number => (typeof row === 'object' ? row : rowTypeOf(row, board)).name;

/** The same, for a row in this drawing: what `fittingColumns` sheds a column to reach. */
export const nameFloorForRow = (row: RowSize, board = false): number => nameColumnFloor(nameSizeForRow(row, board));

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
 * One form, in the format the rig asks for, cut to what the column holds and closed with an ellipsis
 * where it was cut. There used to be two, the full name and a three-letter code for a column too
 * narrow for one, and the code was `left(name, 3)`: Liam Byrne was LIA and Hannah Fischer HAN, which
 * identifies nobody and collides for any two drivers sharing a first name. #385 took it out of every
 * list on the face, the companion and the pit wall.
 *
 * The budget is characters and the box is pixels, so the count comes from the widest glyph the name
 * face draws and the column declares that many of it as its `widest`. The fit tests therefore measure
 * the budget rather than the sample, which is what makes a column too narrow for its own cut a failing
 * test instead of a clipped name on somebody's rim.
 *
 * The own row says YOU, which is the one row a driver does not need to read a name to identify — and
 * which is upper case, as {@link MIXED_CASE_NAME_SIZE} now makes the rest of the column.
 */
function cellName(ctx: CellContext): Item[] {
  const fs = ctx.type.name;
  const chars = charsThatFit(NAME_FACE, fs, ctx.width);
  const bind = iff(ctx.isPlayer, str('YOU'), nameText(ctx.idx, chars, fs));
  return [
    withMoreBindings(
      label(`${ctx.name}.name`, nameSampleAt(fs), ctx.x, ctx.top + (ctx.height - fs) / 2, ctx.width, {
        size: fs,
        color: ds.color.text.secondary,
        bind,
        widest: widestOf(NAME_FACE, chars),
      }),
      { TextColor: liftBind(ctx, inkBind(ctx)) },
    ),
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
    ...([[RANK_UP, gained, 'up'], [RANK_DOWN, lost, 'down']] as const).map(([asset, visible, id]) => withMoreBindings({
      kind: 'image' as const,
      name: `${ctx.name}.rank.${id}`,
      image: asset.name,
      rect: assetBox(markerBox, imageOf(asset)),
    }, { Visible: visible })),
    withMoreBindings(band(`${ctx.name}.rank.flat`, rect(right - 8, ctx.top + ctx.height / 2 - 1, 8, 2), ds.color.text.dim), { Visible: not(moved) }),
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
    ...cellValue(ctx, 'pit', '1', carPitCount(ctx.idx), { digits: 2, specials: 0 }, { fs: ctx.type.minor, align: 'right' }).map((item) => withMoreBindings(item, { Visible: not(ctx.inPit) })),
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
   * The type the row's cells are drawn at, where a {@link listPlan} chose one; the largest the row
   * height allows by default. A row taller than its type is the same row with more space around it.
   */
  rowType?: RowType;
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
export function columnWidths(columns: readonly ColumnId[], width: number, density: Density, rowHeight?: RowSize, board = false): number[] {
  const raw = fixedWidths(columns, density, rowHeight, board);
  const flexColumns = raw.filter((w) => w === 0).length;
  const spare = Math.max(0, rowSlack(columns, width, density, rowHeight, board));
  return raw.map((w) => (w === 0 ? Math.floor(spare / Math.max(1, flexColumns)) : w));
}

/** Each column's own width in a row of this type, 0 for the one that takes what is left. */
function fixedWidths(columns: readonly ColumnId[], density: Density, rowHeight?: RowSize, board = false): number[] {
  const row: RowSpec = { d: densityOf(density), type: typeOfRow(rowHeight, density, board), board };
  return columns.map((id) => COLUMNS[id].width(row));
}

/**
 * What a padded row of `width` has left once its fixed columns and the gaps between them are laid out:
 * the width the flexible column gets, or, when it is negative, how far the fixed ones overrun the box.
 *
 * {@link columnWidths} floors it at nothing, since a column cannot be narrower than that, and a row
 * whose fixed columns overrun is then laid out past its box without a word. A page's shedding asks
 * this to know when that is about to happen, which is the one question the widths cannot answer.
 */
export function rowSlack(columns: readonly ColumnId[], width: number, density: Density, rowHeight?: RowSize, board = false): number {
  const fixed = fixedWidths(columns, density, rowHeight, board).reduce((sum, w) => sum + w, 0);
  const gaps = cellGapOf(board) * Math.max(0, columns.length - 1);
  return width - 2 * padXOf(board) - fixed - gaps;
}

/** Where a column of a row begins, and how wide it is. */
export interface ColumnSpan {
  id: ColumnId;
  left: number;
  width: number;
}

/**
 * Where each column of a row sits: laid out from the padded edge, one cell gap apart, at the widths
 * {@link columnWidths} gives them.
 *
 * The one place that walks a row from left to right, so that the header, every row block and a test
 * holding a cell against its column all agree on where the column is.
 */
export function columnSpans(columns: readonly ColumnId[], frame: Rect, density: Density, row?: RowSize, board = false): ColumnSpan[] {
  const widths = columnWidths(columns, frame.width, density, row, board);
  let x = frame.left + padXOf(board);
  return columns.map((id, i) => {
    const span = { id, left: x, width: widths[i] ?? 0 };
    x += span.width + cellGapOf(board);
    return span;
  });
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

/**
 * The tallest row that `rows` of them fill this frame: {@link rowsThatFit} solved the other way round.
 *
 * What a page calls when it has declared its row count and the box has height over. `table()` centres a
 * declared block in whatever the frame leaves, which is right for a list that has run out of cars and
 * wrong for one that was told to stop counting: eleven rows of 28 px in a 560 px zone is 328 px of list
 * and 232 px of nothing, which reads as a page that failed to draw rather than as a page with a
 * declared length. Stretching the row instead spends the height on the space between the rows, and
 * the type stops at 38 so what is bought above that is spacing and never size; {@link listPlan} is
 * what chooses the type.
 *
 * Never below `rowHeight`, which is the caller's floor: a frame too short for the rows asked for is
 * `table()`'s to clamp, and it does.
 */
export function rowHeightThatFills(frame: Rect, opts: { density: Density; header: boolean; rowHeight?: number; board?: boolean }, rows: number): number {
  const board = opts.board ?? false;
  const h = opts.rowHeight ?? tableRowHeight(opts.density);
  const gap = rowGapOf(board);
  const body = frame.height - (opts.header ? headerHeightOf(board, h) : 0);
  return Math.max(h, Math.floor((body + gap) / Math.max(1, rows)) - gap);
}

/**
 * What a list page declares: the columns and the rows it wants, and how it sheds a column.
 *
 * The count is the page's answer to its own question rather than what the box divides out, which is
 * the half of #328 the table could not supply for itself. `fit` is the page's shedding, since which
 * columns survive a narrow box is a decision each page takes and not one the table can.
 */
export interface ListDeclaration {
  density: Density;
  header: boolean;
  /** The columns the page draws at this shape, before a narrow box sheds any. */
  columns: readonly ColumnId[];
  /**
   * The rows the page wants. A box too short for them at the canvas's row lists fewer. `Infinity` is
   * a list that wants every row its box holds at that row, which is the pit wall's answer.
   */
  rows: number;
  /** The fewest it asks for however short the box; `table()` clamps what the frame cannot hold. */
  least?: number;
  /** Whether the count must be odd, for a list centred on a row of its own. */
  odd?: boolean;
  /** The columns that fit a width at a row type, which is the page's shedding. */
  fit: (columns: readonly ColumnId[], width: number, density: Density, row: RowType) => ColumnId[];
}

/** What a list page draws: how many rows, at what height, at what type, in which columns. */
export interface ListPlan {
  rows: number;
  rowHeight: number;
  rowType: RowType;
  columns: ColumnId[];
}

/**
 * The characters a name column holds at a row type in this frame, which is what the cut is counted in.
 *
 * Characters and not pixels, because pixels are what the first guard of #339 got wrong: a step that
 * promotes the position and the gap takes their width out of the name, and a column that still cleared
 * the eight-character floor could hold nine characters of the budget where it had held ten.
 */
export function nameBudget(frame: Rect, list: ListDeclaration, row: RowType): number {
  const kept = list.fit(list.columns, frame.width, list.density, row);
  const index = kept.indexOf('name');
  if (index < 0) return 0;
  return charsThatFit(NAME_FACE, row.name, columnWidths(kept, frame.width, list.density, row)[index] ?? 0);
}

/**
 * The least a step may leave the name, given what the canvas's own row left it.
 *
 * **Ten characters is the line where the canvas's row held ten**: `Liam Byrne` is what the default
 * format makes of the entry every artboard and every trace carries, and a larger type is never the
 * reason it lost a letter. That is #339's rule and it stands; it is what keeps zone B of the
 * 1280 x 480 face at the 34 px type rather than the 38, whose car number would take the name from ten
 * characters to nine.
 *
 * **Where the canvas's row already cut it, a step may cost one character and never two.** Below ten
 * the default sample is ellipsised whatever the row does, so the question the column answers stops
 * being *is the name whole* and becomes *can it be read from the seat*, and one character of a name
 * that is already cut buys two pixels of every character left. That is the 850 x 480 face, whose
 * 82 px column holds seven at 13 and six at 15. A step costing two is refused, which is what keeps a
 * narrow zone's numerals at 24: at 34 they would take the same column to five.
 */
export const nameFloorOf = (had: number): number => (had >= DEFAULT_NAME_CHARS ? DEFAULT_NAME_CHARS : Math.max(0, had - 1));

/**
 * The row a list page draws: its declared rows first, then the type they can carry, then the rest as
 * space between them.
 *
 * `fitFields` answers a box for a rank in that order and this answers it for a table, which is #328.
 *
 * - **Rows.** The page's count, cut to what the box holds at the canvas's row for the density and
 *   never counted at a shorter one; odd where the page centres on a row of its own.
 * - **Type.** The largest step of {@link LIST_ROW_TYPES} the rows' height allows that the width
 *   allows too, the width being the name's budget: {@link nameFloorOf} is the edge. The canvas's own
 *   row always passes, since the edge is measured from it, so the type is never smaller than the
 *   canvas draws at the density.
 * - **Space.** The row is stretched to fill the body, whatever type it carries, so a declared list
 *   spans its box instead of sitting centred in a pool of slack. A row taller than its type is the
 *   same row with air between it and the next, and air between the rows of a relative is what
 *   separates the car ahead from the car behind at a glance.
 *
 * The type stops at the 38 px row, the tallest the canvas draws a list at, so rule 20's three edges
 * all hold: the height of the box, the width of the box, and the top of the ramp.
 *
 * **A step never costs a column the canvas's row kept** (#340). The name's budget is the width edge
 * wherever the row has a name, but a page's shedding can give the name up, and a row with no name has
 * no budget to lose: every step would pass, and the larger numerals would take their width from the
 * position and then from nothing. So a step is also held to the columns, which is the same edge said
 * about the whole row rather than about its one flexible column. No box the build produces meets it
 * today; the rule is here so that the first one to do so gets the canvas's type and keeps its columns.
 */
export function listPlan(frame: Rect, list: ListDeclaration): ListPlan {
  const floor = tableRowHeight(list.density);
  const fit = { density: list.density, header: list.header, rowHeight: floor };
  const fits = Math.min(list.rows, rowsThatFit(frame, fit));
  const counted = list.odd && fits % 2 === 0 ? fits - 1 : fits;
  const rows = Math.max(list.least ?? 1, counted);
  const rowHeight = rowHeightThatFills(frame, fit, rows);
  const canvas = rowTypeOf(floor);
  const least = nameFloorOf(nameBudget(frame, list, canvas));
  const columns = list.fit(list.columns, frame.width, list.density, canvas).join();
  const keepsColumns = (step: RowType): boolean => list.fit(list.columns, frame.width, list.density, step).join() === columns;
  const steps = LIST_ROW_TYPES.slice(LIST_ROW_TYPES.indexOf(canvas)).filter((step) => step.from <= rowHeight).reverse();
  const rowType = steps.find((step) => nameBudget(frame, list, step) >= least && keepsColumns(step)) ?? canvas;
  return { rows, rowHeight, rowType, columns: list.fit(list.columns, frame.width, list.density, rowType) };
}

/** The header row: a label per column, aligned as its cells are, closed on a board by its rule. */
function headerRow(spec: TableSpec, spans: readonly ColumnSpan[], top: number, geometry: { height: number; board: boolean }): Item[] {
  const d = densityOf(spec.density);
  const { height, board } = geometry;
  const items: Item[] = board ? [rule(`${spec.name}.head.rule`, spec.frame.left, top + height - 1, spec.frame.width, 1)] : [];
  spans.forEach(({ id, left: x, width }) => {
    const column = COLUMNS[id];
    const text = column.header;
    // Measured as `label` draws it: a header carries no binding, so it is upper-cased on the way in
    // and a box measured from the canvas's own capitalisation is a box the drawn text overruns.
    const drawn = Math.ceil(measureText('BarlowMedium', text.toUpperCase(), d.labelSm));
    const left = column.align === 'right' ? x + width - drawn : x;
    items.push(label(`${spec.name}.head.${id}`, text, left, top + (height - d.labelSm) / 2, Math.max(drawn, 0), { size: d.labelSm }));
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
function rowBlock(spec: TableSpec, spans: readonly ColumnSpan[], rowHeight: number, type: RowType, block: { name: string; top: number; rows: number; idx: Expr; inClass?: Expr }): LayerItem {
  const d = densityOf(spec.density);
  const board = spec.board ?? false;
  const { name, top, rows, idx, inClass } = block;
  const isPlayer = carIsPlayer(idx);
  const inPit = carInPit(idx);
  const children: Item[] = [
    withMoreBindings(band(`${spec.name}.${name}.background`, rect(spec.frame.left, top, spec.frame.width, rowHeight), ds.color.surface.zone), { Visible: isPlayer }),
    // The board's rows are flush and each is closed by a rule; a list's are two apart and closed by
    // the gap. Both run the frame's full width, under the padding the cells are inset by.
    ...(board ? [rule(`${spec.name}.${name}.rule`, spec.frame.left, top + rowHeight - 1, spec.frame.width, 1)] : []),
  ];
  spans.forEach(({ id, left: x, width }) => {
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
        type,
        isPlayer,
        inPit,
        mode: spec.mode,
        ...(inClass === undefined ? {} : { inClass }),
        align: COLUMNS[id].align,
      }),
    );
  });
  const row: LayerItem = withMoreBindings({ kind: 'layer', name: `${spec.name}.${name}`, children }, { Visible: carAvailable(idx) });
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
  const type = spec.rowType ?? rowTypeOf(rowHeight, board);
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
  const spans = columnSpans(spec.columns, spec.frame, spec.density, type, board);
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
  const head = header ? headerRow(spec, spans, spec.frame.top, { height: headerHeight, board }) : [];
  if (!splits) {
    // The player sits in the middle of a relative table, so the row index counts from that row.
    const idx = rowIndexFor(spec, Math.ceil(rows / 2));
    return [...head, rowBlock(spec, spans, rowHeight, type, { name: 'row', top, rows, idx, inClass: rowsInClass(spec.classOnly) })];
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
    rowBlock(spec, spans, rowHeight, type, { name: 'row', top, rows: topRows, idx: rowIndex.full() }),
    ...limitLine(spec, bandTop, splitHiddenCars(topRows, windowRows)),
    rowBlock(spec, spans, rowHeight, type, { name: 'splitRow', top: bandTop + SPLIT_HEIGHT + rowGap, rows: windowRows, idx: rowIndex.split(topRows, windowRows) }),
  ];
}

/** Every column the tables can show, for the docs and for a test that keeps them in step. */
export const COLUMN_IDS = Object.keys(COLUMNS) as ColumnId[];
