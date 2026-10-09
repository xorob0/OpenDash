/**
 * Module 16, Opponents: the one car ahead and the one car behind, with the gap large enough to
 * read at a glance and enough about them to know who they are.
 *
 * The two cars are the two a driver is racing, so they follow the same filter the lists do: the
 * zone's own, where the zone has one, and the rig's `PositionMode` everywhere. Heading a block
 * "AHEAD · P3" with a class position while the car under it is whoever happens to be in front on
 * track is the defect #212 reports about a leaderboard, said of two rows instead of twenty.
 *
 * Each block is a heading, an identity row and a gap row six pixels apart, and the two blocks sit
 * at least twelve either side of a rule. A block is a hand-built row rather than a rank of fields
 * because two of its three lines are not fields: the identity row is a name, a number and a chip
 * centred on one line, and the gap row is a large value with two small labels on its baseline.
 *
 * **The two cars shed the same line.** This is the one page whose rank is two of the same thing, so
 * dropping a field from one of them and not the other draws a page that looks broken, and dropping
 * a whole block draws a page about the car ahead and the car behind that shows only the car ahead.
 * The pieces are therefore cut in pairs here, from the tail of the page's own declared order, and
 * only a box too short for two headings and two gaps moves the size off the ramp -- which is rule
 * 17 in order: the detail, then the identity, then the size, and the size for both cars at once.
 * Each block still declares what it draws, so that a stack it is ever placed in sheds it by the
 * table rather than by taking the last row off.
 *
 * **The identity row is a list row, and its name is the relative's (#341).** The canvas sets the
 * name at 13 px over a 16 px number in a zone and at 15 over 34 on the companion, which are two of
 * the steps of `LIST_ROW_TYPES`: the same name beside the same number the relative draws. So the
 * question #328 answered for the relative is answered here by the same rule rather than a second
 * one. The name is 15 wherever the row can carry it, and the width is the edge, counted in the
 * characters of the name: where 13 px would hold the ten of `Liam Byrne` the larger name keeps ten,
 * and where 13 px already cut it the larger name may cost one character and never two
 * (`nameFloorOf`). It may never cost a piece of the row either -- the number or the class chip a
 * 13 px name left room for -- since a larger name is not a reason to lose the car's number. The name's
 * box is the eight characters of the shortest format at least, and takes the room the row leaves it
 * up to the ten of the default one.
 *
 * **The two blocks grow with the box (#341).** Rule 20 reaches this page as it reaches a rank: the
 * gap, which is what a driver reads the page for, grows from the density's `big` towards the next
 * size up the ramp until it meets the height of the box, the width of its row or that size, and
 * both cars grow together. What the height has left after that is spent as space between the two
 * cars, either side of the rule, which is what separates the car ahead from the car behind at a
 * glance -- the relative spends its own slack between its rows for the same reason. Only the gap
 * grows. The heading and the recaps are labels and keep the size a label is, and the identity row
 * is typed by the list rule above, whose name stops at 15 and whose number is the list's.
 *
 * A wide box too short to stack two blocks draws them side by side with a vertical rule between
 * them, which is the arrangement the canvas gives the wide zone: 576 by 112 has room for two
 * columns of everything and for one column of a heading and a gap.
 *
 * The gain-and-loss bar the design sheet draws is left out: it needs a history of the gap, which
 * neither SimHub nor a generated dashboard keeps. The gap itself, refreshed every frame, tells the
 * same story to anyone watching it for a second.
 *
 * The nationality flag and the licence badge with its safety rating are the canvas's other two
 * pieces of the identity row and are absent for want of their sources: a flag is an image asset
 * (#166), and the badge is drawn now -- `elements/badge.ts` reads the licence ramp -- but
 * nothing publishes a class to put in it, which `second/values.ts` records. A badge added here
 * would also have to take its turn in the shedding order, since both blocks shed together.
 *
 * The 12 by 12 direction triangle the canvas heads each row with waits on artwork rather than on
 * data. An image item carries no colour and no rotation, so up and down in `text.label` are two
 * files, and the two the repository ships -- `RANK_UP` and `RANK_DOWN` in `design/assets.ts` -- are
 * rendered in the delta's green and red for a table's rank column. Drawing the triangle as a
 * rectangle instead is the one thing it must not be, a square being the mark that says nothing
 * about direction. Whether it is worth two more files is the author's, and worth asking: this row
 * is headed "AHEAD · P3" and "BEHIND · P5" in words, which the canvas's compact rank is not.
 *
 * The rating the gap row carries is the iRating, and it is written as a label on the gap's own
 * baseline because that is what the canvas writes there; the 15 px rating the type sheet names is
 * the safety rating that sits beside the badge, and it waits on the same missing source.
 */
import { ncalc } from '../generator.ts';
import { label } from '../elements/label.ts';
import { rule } from '../elements/rule.ts';
import { MINUS, canvasBaseline, canvasYForBaseline } from '../design/metrics.ts';
import { charsThatFit, measureText, widestOf } from '../design/advances.ts';
import { densityOf, grownAtMost, rampOf } from '../second/density.ts';
import { CHIP_WIDEST, chip, chipText, chipWidth } from '../second/chip.ts';
import { field, fieldTail, fieldWidth, valueWidth, type FieldSpec } from '../second/field.ts';
import { ROW_TAIL, stack, type StackRow } from '../second/layout.ts';
import { DEFAULT_NAME_CHARS, LIST_ROW_TYPES, NAME_FACE, SHORTEST_NAME_CHARS, nameColumnFor, nameFloorOf, nameSampleAt, nameText } from '../second/table.ts';
import { CHARS, carBestLap, carClass, carLastLap, carNumber, carPosition,
  positionLabelled, carRating, carRelativeGap, listNeighbour } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineModule, drawnAt, fld, pageKeeps, shapeIn } from './module.ts';
import { keepsAt } from './shedding.ts';
import type { Hex, Item } from '../generator.ts';
import type { Expr } from '../bind.ts';
import type { ModuleContext } from './module.ts';

const { concat, str, fmt } = ncalc;

/** Between the heading, the identity row and the gap row of one block. */
const INNER_GAP = 6;

/**
 * Between the two blocks, which is what the rule between them sits in: the least of it, the canvas's
 * twelve, and the space a stacked page spends its slack on once the gap has grown.
 */
const BLOCK_GAP = 12;

/** Between the pieces of the identity row: the name, the number and the class chip. */
const IDENTITY_GAP = 8;

/** Between the gap and the labels that follow it on its baseline, where a unit takes six. */
const DETAIL_GAP = 10;

/**
 * The name's box and the car number's cell as the canvas fixes them, whatever the density.
 *
 * They are the row's floors rather than its widths. The canvas cuts both on a sheet whose values
 * are 16 px, and the companion draws the number at 34, where the four digits of `CHARS.carNumber`
 * take sixty-four cells of their own: the chip then began twelve pixels inside the number's box and
 * a four-digit number was drawn under it. Each cell is therefore the canvas's figure or the content
 * it holds, whichever is wider, which is rule 18 read across the ramp rather than down it.
 */
const NAME_WIDTH = 64;
const NUMBER_WIDTH = 44;

/** Between the two columns of a side-by-side page, and the width of the rule between them. */
const COLUMN_GAP = 48;
const RULE = 1;

/**
 * The name's box at its least: the canvas's 64 px, or the room the shortest of the four formats needs.
 *
 * The canvas draws a driver's name here and ellipsises it at 64 px, which WPF cannot do, so this box
 * used to hold a three-letter code instead. It holds a name now (#385), cut in the expression and
 * closed with an ellipsis, and 64 px is not enough of one: the cut is counted in characters against
 * the widest glyph the face draws, so 64 px at 13 px is five of them and `L. Byrne` would come out
 * `L. B…`. The cell asks for the eight the shortest format needs, and the number and the class chip
 * behind it shed to make room, which is the order `PIECES` already puts them in.
 */
const nameCellFloor = (fs: number): number => Math.max(NAME_WIDTH, nameColumnFor(SHORTEST_NAME_CHARS, fs));

/**
 * And at its most: the ten characters of `Liam Byrne`, which is what the default format makes of the
 * canvas's own name and the line #339 draws for a list's driver column. A row with room left after
 * its number and its chip gives it to the name up to here, so that a 600 px zone stops cutting the
 * default name to `LIAM BY…` with four hundred pixels unspent beside it; past ten the room is left to
 * the row, a longer name being a trade the rig's format setting makes rather than this box.
 */
const nameCellWant = (fs: number): number => Math.max(nameCellFloor(fs), nameColumnFor(DEFAULT_NAME_CHARS, fs));

/**
 * The sizes the identity row may set a name at, largest first: the name steps of the list ramp.
 *
 * Read off `LIST_ROW_TYPES` rather than written here, because the point of #341 is that there is one
 * answer to how large a driver's name is, and it lives with the list.
 */
const NAME_SIZES: readonly number[] = [...new Set(LIST_ROW_TYPES.map((type) => type.name))].sort((a, b) => b - a);

/** What a block is drawn at: the gap's size, and the name's. */
interface BlockType {
  gap: number;
  name: number;
}

/** The pieces of a block, most important first, which is the order they are shed from the tail of. */
const PIECES = ['gap', 'name', 'num', 'class', 'lastLap', 'rating'] as const;

interface Side {
  id: string;
  /** The car ahead on track is -1 and the car behind 1. */
  offset: number;
  heading: string;
  /** The position and the gap the artboards draw this block with, which is what DashStudio shows. */
  position: number;
  gap: string;
  colour: Hex;
}

const SIDES: readonly Side[] = [
  { id: 'ahead', offset: -1, heading: 'Ahead', position: 3, gap: `${MINUS}1.342`, colour: ds.purpose.delta.faster },
  { id: 'behind', offset: 1, heading: 'Behind', position: 5, gap: '+0.722', colour: ds.purpose.delta.slower },
];

/** One cell of the identity row: the name's box, the number's cell or the class chip. */
interface Cell {
  id: string;
  width: number;
  /** The most the cell takes of what the row leaves over, which only the name's box asks for. */
  want?: number;
  height: number;
}

/**
 * The cells of the identity row that fit the block's width, cut from the tail.
 *
 * The row used to draw all three whatever the width and clamp the chip to the right edge, which on
 * a box too narrow for the three slid the chip back over the number rather than shedding it: an
 * overlap, and the one failure a fit check cannot see, since both cells stay inside the frame. A
 * module is a function of its rectangle and answers one too small by dropping something, so the
 * class goes first and the number after it, which is the order `modules/shedding.ts` names them in.
 *
 * The cells are shed against their least widths, and what the kept ones leave over then goes to the
 * cell that wants more, which is the name's box, up to what it wants. So a wider box never sheds a
 * cell a narrower one kept in order to give the name more letters.
 *
 * The last cell standing is kept and capped instead of dropped, a row with nothing in it being a
 * worse answer than a code WPF clips; that case needs a box narrower than a three-letter name and
 * no shape the build produces is one.
 */
function cellsThatFit(cells: readonly Cell[], width: number): Cell[] {
  const kept = [...cells];
  const taken = (): number => kept.reduce((sum, cell) => sum + cell.width, 0) + IDENTITY_GAP * Math.max(0, kept.length - 1);
  while (kept.length > 1 && taken() > width) kept.pop();
  const last = kept[0];
  if (kept.length === 1 && last !== undefined && last.width > width) return [{ ...last, width }];
  const spare = width - taken();
  return kept.map((cell) => (cell.want === undefined ? cell : { ...cell, width: Math.min(cell.want, cell.width + spare) }));
}

/** Width of one of the labels that follow the gap, with the pixel `label` leaves itself. */
const detailWidth = (text: string, fs: number): number => Math.ceil(measureText('BarlowMedium', text, fs)) + 1;

/**
 * The labels that follow the gap on its baseline, by the piece that keeps each.
 *
 * The last lap carries its "Last" only where the catalogue writes one. The fullest drawing has room
 * to say what the time is; `grid` and `tall` print it bare, a time beside a gap being unambiguous
 * once the page has been read once, and that is the width the prefix costs spent on the reading.
 */
function details(ctx: ModuleContext, side: Side, keep: readonly string[]): { id: string; text: string; bind: Expr }[] {
  const idx = listNeighbour(side.offset, ctx.classOnly);
  // The wide page is "Opponents · best and last", so its label carries the best lap as well as the
  // last one. The sheet writes "Last Best 1:42.994 · 1:43.234" and leaves which time is which to
  // the reader; the times say it themselves, 1:42.994 being the faster, so the best is drawn first.
  const best = ctx.density === 'wide';
  const named = best || drawnAt(ctx) === 'wide';
  return [
    ...(keep.includes('lastLap')
      ? [
          {
            id: 'lastLap',
            text: best ? 'Last Best 1:42.994 · 1:43.234' : named ? 'Last 1:43.234' : '1:43.234',
            bind: best ? concat(str('Last Best '), carBestLap(idx), str(' · '), carLastLap(idx)) : named ? concat(str('Last '), carLastLap(idx)) : carLastLap(idx),
          },
        ]
      : []),
    ...(keep.includes('rating') ? [{ id: 'rating', text: 'iR 3.1k', bind: concat(str('iR '), carRating(idx)) }] : []),
  ];
}

/** The box a block is drawn in: the whole frame's width when stacked, a column's beside another. */
interface Column {
  left: number;
  width: number;
}

/** A block measured before it is drawn: what the page chooses its type and its pieces by. */
interface Measured {
  gapSpec: FieldSpec;
  numSpec: FieldSpec;
  numberSize: number;
  following: { id: string; text: string; bind: Expr }[];
  identity: Cell[];
  identityHeight: number;
  valueHeight: number;
  gapHeight: number;
  height: number;
  /** How far the block's WPF box runs below the canvas line box of its last line, which the height holds. */
  tail: number;
  /** How far the gap row runs across: the gap and the recaps on its baseline, with the gaps between. */
  across: number;
  /** The characters the name's box holds, which is what the cut is counted in; nought where the name is shed. */
  chars: number;
}

/** The detail the page decides by, and the block draws from. */
function measure(ctx: ModuleContext, side: Side, box: Column, type: BlockType, keep: readonly string[]): Measured {
  const d = densityOf(ctx.density);
  const idx = listNeighbour(side.offset, ctx.classOnly);
  const has = (piece: string): boolean => keep.includes(piece);
  // The canvas draws the number at the fourth size of the companion ramp and at the last of the
  // zone one, which is not the same rung of the two ladders, so the instrument says which.
  const numberSize = ctx.density === 'companion' ? d.small : d.tiny;
  const gapSpec: FieldSpec = fld(ctx, `${side.id}.gap`, '', { sample: side.gap, bind: carRelativeGap(idx), chars: CHARS.relativeGap, fs: type.gap, color: side.colour });
  // The hash has gone with the label it was: the canvas draws the number alone in its cell, which
  // is also what the lists do since a `#` overruns a cell cut for digits.
  const numSpec: FieldSpec = fld(ctx, `${side.id}.num`, '', { sample: '41', bind: carNumber(idx), chars: CHARS.carNumber, fs: numberSize, color: ds.color.text.label });
  const following = details(ctx, side, keep);
  const identity = cellsThatFit(
    [
      ...(has('name') ? [{ id: 'name', width: nameCellFloor(type.name), want: nameCellWant(type.name), height: type.name }] : []),
      ...(has('num') ? [{ id: 'num', width: Math.max(NUMBER_WIDTH, fieldWidth(numSpec, ctx.density)), height: numberSize }] : []),
      ...(has('class') ? [{ id: 'class', width: chipWidth(ctx.density), height: d.chipHeight }] : []),
    ],
    box.width,
  );
  const identityHeight = identity.reduce((tallest, cell) => Math.max(tallest, cell.height), 0);
  const valueHeight = has('gap') ? type.gap : d.label;
  const gapHeight = has('gap') || following.length > 0 ? valueHeight + fieldTail(gapSpec, ctx.density) : 0;
  const lines = [d.label, identityHeight, gapHeight].filter((line) => line > 0);
  const height = lines.reduce((sum, line) => sum + line, 0) + INNER_GAP * (lines.length - 1);
  const across = [...(has('gap') ? [Math.ceil(valueWidth(gapSpec, d))] : []), ...following.map((detail) => detailWidth(detail.text, d.label))];
  const name = identity.find((cell) => cell.id === 'name');
  return {
    gapSpec,
    numSpec,
    numberSize,
    following,
    identity,
    identityHeight,
    valueHeight,
    gapHeight,
    height,
    tail: gapHeight > 0 ? fieldTail(gapSpec, ctx.density) : 0,
    across: across.reduce((sum, width) => sum + width, 0) + DETAIL_GAP * Math.max(0, across.length - 1),
    chars: name === undefined ? 0 : charsThatFit(NAME_FACE, type.name, name.width),
  };
}

/** One block, at a type and holding the pieces still kept. */
function block(ctx: ModuleContext, side: Side, box: Column, type: BlockType, keep: readonly string[]): StackRow | undefined {
  if (keep.length === 0) return undefined;
  const d = densityOf(ctx.density);
  const idx = listNeighbour(side.offset, ctx.classOnly);
  const has = (piece: string): boolean => keep.includes(piece);
  const { gapSpec, numSpec, numberSize, following, identity, identityHeight, valueHeight, gapHeight, height } = measure(ctx, side, box, type, keep);

  const draw = (bottom: number): Item[] => {
    const items: Item[] = [];
    let top = bottom - height;
    items.push(
      label(`${ctx.prefix}${side.id}.heading`, `${side.heading} · P${side.position}`, box.left, top, box.width, {
        size: d.label,
        bind: concat(str(`${side.heading} · `), positionLabelled(idx)),
        widest: `${side.heading} · P99`,
      }),
    );
    top += d.label + INNER_GAP;
    if (identityHeight > 0) {
      // Centred on the line, as the canvas sets the row: a 20 px chip beside a 13 px name shares
      // the line's middle rather than a baseline neither of them would sit on comfortably.
      const centred = (size: number): number => top + (identityHeight - size) / 2;
      let x = box.left;
      for (const cell of identity) {
        if (cell.id === 'name') {
          // The cell may have been clamped to a box too narrow for what it asked for, so the budget
          // is taken from the width it actually got rather than from the width it wanted.
          const chars = charsThatFit(NAME_FACE, type.name, cell.width);
          items.push(label(`${ctx.prefix}${side.id}.name`, nameSampleAt(type.name), x, centred(type.name), cell.width, {
            size: type.name,
            color: ds.color.text.primary,
            bind: nameText(idx, chars, type.name),
            widest: widestOf(NAME_FACE, chars),
          }));
        }
        if (cell.id === 'num') items.push(...field(numSpec, x, centred(numberSize) + numberSize, ctx.density, cell.width));
        if (cell.id === 'class') {
          items.push(...chip(`${ctx.prefix}${side.id}.class`, 'GT3', x, centred(d.chipHeight), ctx.density, { bind: chipText(carClass(idx)), widest: CHIP_WIDEST, width: cell.width }));
        }
        x += cell.width + IDENTITY_GAP;
      }
      top += identityHeight + INNER_GAP;
    }
    if (gapHeight > 0) {
      let x = box.left;
      // On the line box's own bottom rather than on the row's, which is `fieldTail` lower: the row
      // reserves that tail so the line box may hang into it, and a field placed on the reserved
      // edge spends it above the value instead, which at 46 px doubles the six above the row.
      const lineBottom = top + valueHeight;
      if (has('gap')) {
        items.push(...field(gapSpec, x, lineBottom, ctx.density));
        x += Math.ceil(valueWidth(gapSpec, d)) + DETAIL_GAP;
      }
      // On the gap's own baseline, where the canvas sets them: the reading and the recaps of it
      // read as one line rather than as a line with a caption under it.
      const baseline = canvasBaseline(lineBottom - valueHeight, valueHeight);
      for (const detail of following) {
        const width = detailWidth(detail.text, d.label);
        items.push(label(`${ctx.prefix}${side.id}.${detail.id}`, detail.text, x, canvasYForBaseline(baseline, d.label), width, { size: d.label, bind: detail.bind, widest: detail.text }));
        x += width + DETAIL_GAP;
      }
    }
    return items;
  };

  // No `fill`: the page grows its blocks itself, below, because the two cars have to be typed as
  // one and the space the growth leaves is spent between them rather than around them, neither of
  // which a stack's rule 20 does for a row.
  return {
    height,
    draw,
    shed: {
      ids: keep.map((piece) => `${side.id}.${piece}`),
      order: keepsAt(ctx.page, drawnAt(ctx)) ?? keep,
      without: (ids) => block(ctx, side, box, type, keep.filter((piece) => !ids.includes(`${side.id}.${piece}`))),
    },
  };
}

/** Whether two lists name the same pieces in the same order. */
const same = (a: readonly string[], b: readonly string[]): boolean => a.length === b.length && a.every((piece, i) => piece === b[i]);

export const opponents = defineModule('opponents', (ctx) => {
  const d = densityOf(ctx.density);
  const shape = shapeIn(ctx);
  const ahead = SIDES[0]!;
  // Two columns when the box has room across and not down: the wide zone, which the canvas draws
  // that way, and any wide box too short to stack two blocks in.
  const columns = ctx.density === 'wide' || (shape.width === 'wide' && shape.height === 'short');
  const width = columns ? Math.floor((ctx.frame.width - 2 * COLUMN_GAP - RULE) / 2) : ctx.frame.width;
  const boxes: readonly Column[] = columns
    ? [
        { left: ctx.frame.left, width },
        { left: ctx.frame.left + ctx.frame.width - width, width },
      ]
    : [{ left: ctx.frame.left, width }, { left: ctx.frame.left, width }];
  const room = ctx.frame.height - 2 * ROW_TAIL;
  // Both cars carry the same pieces, so the declaration is read off one of them; the table names
  // this page's fields in pairs for exactly that reason.
  const declared = PIECES.filter((piece) => pageKeeps(`${ahead.id}.${piece}`, ctx));
  // Two blocks and the rule, with the canvas's twelve either side of it. Twelve from the line box of
  // the gap above it rather than from the bottom of its row, which is the row's tail lower: the row
  // reserves the tail so that WPF's taller box may hang into it, and a rule set under the reserve
  // would sit the tail further from the car ahead than from the car behind.
  const stacked = (one: Measured): number => (columns ? one.height : 2 * one.height + RULE + 2 * BLOCK_GAP - one.tail);
  const measured = (type: BlockType, keep: readonly string[]): Measured => measure(ctx, ahead, boxes[0]!, type, keep);
  // A block fits when both of them stack in the height and its gap row fits across its column.
  const fits = (type: BlockType, keep: readonly string[]): boolean => {
    const one = measured(type, keep);
    return stacked(one) <= room && one.across <= width;
  };

  /**
   * Rule 17 in order, for a name of this size. The size is the last thing to move and moves for both
   * cars at once, so it is the largest on the ramp up to the density's `big` at which two headings
   * and two gaps still fit; what fits beside them is then cut from the tail of the page's own order.
   */
  const settle = (name: number): { type: BlockType; keep: string[] } => {
    const sizes = rampOf(ctx.density).filter((size) => size <= d.big);
    const gap = [...sizes].reverse().find((size) => fits({ gap: size, name }, ['gap'])) ?? sizes[0] ?? d.big;
    const keep = [...declared];
    while (keep.length > 1 && !fits({ gap, name }, keep)) keep.pop();
    return { type: { gap, name }, keep };
  };

  // The name, by the relative's rule. The canvas's own drawing is the smallest step, and a larger one
  // is taken where it keeps every piece and the gap's size the smaller one kept, and costs the name no
  // more characters than `nameFloorOf` allows.
  const canvas = settle(NAME_SIZES[NAME_SIZES.length - 1]!);
  const had = measured(canvas.type, canvas.keep);
  const least = nameFloorOf(had.chars);
  const settled =
    NAME_SIZES.map(settle).find((at) => {
      const now = measured(at.type, at.keep);
      return at.type.gap === canvas.type.gap && same(at.keep, canvas.keep) && same(now.identity.map((cell) => cell.id), had.identity.map((cell) => cell.id)) && now.chars >= least;
    }) ?? canvas;

  // Rule 20. A page that has shed nothing and drawn its gap at the density's `big` spends the room it
  // has left on the gap, a pixel at a time from as far as the rule lets it grow, the first size at
  // which both blocks still fit being the one taken.
  let type = settled.type;
  if (type.gap === d.big && settled.keep.length === declared.length) {
    for (let size = Math.floor(grownAtMost(d.big, ctx.density)); size > d.big; size--) {
      if (fits({ ...type, gap: size }, settled.keep)) {
        type = { ...type, gap: size };
        break;
      }
    }
  }
  const keep = settled.keep;

  const rows = SIDES.map((side, i) => block(ctx, side, boxes[i]!, type, keep));
  if (columns) {
    const height = Math.max(...rows.map((row) => row?.height ?? 0));
    const left = ctx.frame.left + Math.floor((ctx.frame.width - RULE) / 2);
    return stack(
      ctx.frame,
      [{ height, draw: (bottom) => [rule(`${ctx.prefix}rule`, left, bottom - height, RULE, height), ...rows.flatMap((row) => row?.draw(bottom) ?? [])] }],
      ctx.density,
      BLOCK_GAP,
    );
  }
  // And what the height has left after the gap is space between the cars, either side of the rule:
  // the canvas's twelve at least, and the page's whole height where the gap could grow no further.
  const drawn = measured(type, keep);
  const between = BLOCK_GAP + Math.max(0, Math.floor((room - stacked(drawn)) / 2));
  /**
   * The rule travels with the block under it, the way delta's rank carries the rule above it: a
   * 1 px line with nothing beneath it is a line drawn for its own sake, and a separate row would
   * leave one behind on a box with room for a rule and none for the car it separates.
   */
  const ruled = (row: StackRow | undefined): StackRow | undefined => {
    if (row === undefined) return undefined;
    const { shed } = row;
    return {
      ...row,
      height: row.height + between + RULE,
      // On the row's own top edge, and the stack leaves the same space above it less the tail the
      // row over it already reserves, so the rule is as far from the gap's line box as from the
      // heading's.
      draw: (bottom) => [rule(`${ctx.prefix}rule`, ctx.frame.left, bottom - row.height - between - RULE, ctx.frame.width, RULE), ...row.draw(bottom)],
      ...(shed ? { shed: { ...shed, without: (ids: readonly string[]) => ruled(shed.without(ids)) } } : {}),
    };
  };
  const live = [rows[0], ruled(rows[1])].filter((row): row is StackRow => row !== undefined);
  return stack(ctx.frame, live, ctx.density, between - drawn.tail);
});
