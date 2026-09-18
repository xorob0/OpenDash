/**
 * Module 16, Opponents: the one car ahead and the one car behind, with the gap large enough to
 * read at a glance and enough about them to know who they are.
 *
 * Each block is a heading, an identity row and a gap row six pixels apart, and the two blocks sit
 * twelve either side of a rule. A block is a hand-built row rather than a rank of fields because
 * two of its three lines are not fields: the identity row is a name, a number and a chip centred on
 * one line, and the gap row is a large value with two small labels on its baseline.
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
import { measureText } from '../design/advances.ts';
import { densityOf, rampOf } from '../second/density.ts';
import { chip, chipText, chipWidth } from '../second/chip.ts';
import { field, fieldTail, fieldWidth, valueWidth, type FieldSpec } from '../second/field.ts';
import { ROW_TAIL, stack, type StackRow } from '../second/layout.ts';
import { CHARS, carBestLap, carClass, carLastLap, carNumber, carPosition, carRating, carRelativeGap, driverCode, neighbour } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineModule, drawnAt, fld, pageKeeps, shapeIn } from './module.ts';
import { keepsAt } from './shedding.ts';
import type { Hex, Item } from '../generator.ts';
import type { Expr } from '../bind.ts';
import type { ModuleContext } from './module.ts';

const { concat, str, fmt } = ncalc;

/** Between the heading, the identity row and the gap row of one block. */
const INNER_GAP = 6;

/** Between the two blocks, which is what the rule between them sits in. */
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
 * The widest three-letter code, which is what the name's box holds.
 *
 * The canvas draws a driver's name here and ellipsises it at 64 px. WPF has no ellipsis to give, so
 * `driverCode` cuts the name to its three-letter code in NCalc instead and the box holds that; the
 * canvas's 64 stays as the column's floor, which leaves it about twenty pixels wider than the code
 * needs. Narrowing it to the code is a change to the row's rhythm and is the author's.
 */
const CODE_WIDEST = 'WWW';

/** The box the three-letter code is drawn in, with the pixel `label` leaves itself. */
const codeWidth = (fs: number): number => Math.ceil(measureText('BarlowMedium', CODE_WIDEST, fs)) + 1;

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
  { id: 'ahead', offset: -1, heading: 'AHEAD', position: 3, gap: `${MINUS}1.342`, colour: ds.purpose.delta.faster },
  { id: 'behind', offset: 1, heading: 'BEHIND', position: 5, gap: '+0.722', colour: ds.purpose.delta.slower },
];

/** One cell of the identity row: the name's box, the number's cell or the class chip. */
interface Cell {
  id: string;
  width: number;
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
  return kept;
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
  const idx = neighbour(side.offset);
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

/** One block, at a value size and holding the pieces still kept. */
function block(ctx: ModuleContext, side: Side, box: { left: number; width: number }, fs: number, keep: readonly string[]): StackRow | undefined {
  if (keep.length === 0) return undefined;
  const d = densityOf(ctx.density);
  const idx = neighbour(side.offset);
  const has = (piece: string): boolean => keep.includes(piece);
  // The canvas draws the number at the fourth size of the companion ramp and at the last of the
  // zone one, which is not the same rung of the two ladders, so the instrument says which.
  const numberSize = ctx.density === 'companion' ? d.small : d.tiny;
  const gapSpec: FieldSpec = fld(ctx, `${side.id}.gap`, '', { sample: side.gap, bind: carRelativeGap(idx), chars: CHARS.relativeGap, fs, color: side.colour });
  // The hash has gone with the label it was: the canvas draws the number alone in its cell, which
  // is also what the lists do since a `#` overruns a cell cut for digits.
  const numSpec: FieldSpec = fld(ctx, `${side.id}.num`, '', { sample: '41', bind: carNumber(idx), chars: CHARS.carNumber, fs: numberSize, color: ds.color.text.label });
  const following = details(ctx, side, keep);
  const identity = cellsThatFit(
    [
      ...(has('name') ? [{ id: 'name', width: Math.max(NAME_WIDTH, codeWidth(d.name)), height: d.name }] : []),
      ...(has('num') ? [{ id: 'num', width: Math.max(NUMBER_WIDTH, fieldWidth(numSpec, ctx.density)), height: numberSize }] : []),
      ...(has('class') ? [{ id: 'class', width: chipWidth(ctx.density), height: d.chipHeight }] : []),
    ],
    box.width,
  );
  const identityHeight = identity.reduce((tallest, cell) => Math.max(tallest, cell.height), 0);
  const valueHeight = has('gap') ? fs : d.label;
  const gapHeight = has('gap') || following.length > 0 ? valueHeight + fieldTail(gapSpec, ctx.density) : 0;
  const lines = [d.label, identityHeight, gapHeight].filter((line) => line > 0);
  const height = lines.reduce((sum, line) => sum + line, 0) + INNER_GAP * (lines.length - 1);

  const draw = (bottom: number): Item[] => {
    const items: Item[] = [];
    let top = bottom - height;
    items.push(
      label(`${ctx.prefix}${side.id}.heading`, `${side.heading} · P${side.position}`, box.left, top, box.width, {
        size: d.label,
        bind: concat(str(`${side.heading} · P`), fmt(carPosition(idx), '0')),
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
          items.push(label(`${ctx.prefix}${side.id}.name`, 'TSA', x, centred(d.name), cell.width, { size: d.name, color: ds.color.text.primary, bind: driverCode(idx), widest: CODE_WIDEST }));
        }
        if (cell.id === 'num') items.push(...field(numSpec, x, centred(numberSize) + numberSize, ctx.density, cell.width));
        if (cell.id === 'class') {
          items.push(...chip(`${ctx.prefix}${side.id}.class`, 'GT3', x, centred(d.chipHeight), ctx.density, { bind: chipText(carClass(idx)), width: cell.width }));
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

  // No `fill`, which is rule 20 declined on purpose. The canvas names this page's gap at every
  // shape it draws, 64 on the companion, 46 in a zone and 34 on the compact faces, and those are
  // exactly the three densities' `big`; a stack that spent its slack on the next rung up would
  // draw 64 where the sheet writes 46 at every one of them. So the room a tall zone has over its
  // two blocks stays slack, and the growth chips on the face sheets are read as the size of the
  // face against the catalogue rather than of the drawing inside it.
  return {
    height,
    draw,
    shed: {
      ids: keep.map((piece) => `${side.id}.${piece}`),
      order: keepsAt(ctx.page, drawnAt(ctx)) ?? keep,
      without: (ids) => block(ctx, side, box, fs, keep.filter((piece) => !ids.includes(`${side.id}.${piece}`))),
    },
  };
}

export const opponents = defineModule('opponents', (ctx) => {
  const d = densityOf(ctx.density);
  const shape = shapeIn(ctx);
  const ahead = SIDES[0]!;
  // Two columns when the box has room across and not down: the wide zone, which the canvas draws
  // that way, and any wide box too short to stack two blocks in.
  const columns = ctx.density === 'wide' || (shape.width === 'wide' && shape.height === 'short');
  const width = columns ? Math.floor((ctx.frame.width - 2 * COLUMN_GAP - RULE) / 2) : ctx.frame.width;
  const boxes = columns
    ? [
        { left: ctx.frame.left, width },
        { left: ctx.frame.left + ctx.frame.width - width, width },
      ]
    : [{ left: ctx.frame.left, width }, { left: ctx.frame.left, width }];
  const room = ctx.frame.height - 2 * ROW_TAIL;
  // Both cars carry the same pieces, so the declaration is read off one of them; the table names
  // this page's fields in pairs for exactly that reason.
  const declared = PIECES.filter((piece) => pageKeeps(`${ahead.id}.${piece}`, ctx));
  const stacked = (one: number): number => (columns ? one : 2 * one + RULE + 2 * BLOCK_GAP);
  const tooTall = (fs: number, keep: readonly string[]): boolean => stacked(block(ctx, ahead, boxes[0]!, fs, keep)?.height ?? 0) > room;
  // Rule 17 in order. The size is the last thing to move and moves for both cars at once, so it is
  // the largest on the ramp at which two headings and two gaps still fit; what fits beside them is
  // then cut from the tail of the page's own order.
  const sizes = rampOf(ctx.density).filter((size) => size <= d.big);
  const fs = [...sizes].reverse().find((size) => !tooTall(size, ['gap'])) ?? sizes[0] ?? d.big;
  const keep = [...declared];
  while (keep.length > 1 && tooTall(fs, keep)) keep.pop();

  const rows = SIDES.map((side, i) => block(ctx, side, boxes[i]!, fs, keep));
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
  /**
   * The rule travels with the block under it, the way delta's rank carries the rule above it: a
   * 1 px line with nothing beneath it is a line drawn for its own sake, and a separate row would
   * leave one behind on a box with room for a rule and none for the car it separates.
   */
  const ruled = (row: StackRow | undefined): StackRow | undefined => {
    if (row === undefined) return undefined;
    const { fill, shed } = row;
    return {
      ...row,
      height: row.height + BLOCK_GAP + RULE,
      // On the row's own top edge, so that the twelve the stack leaves above it and the twelve it
      // leaves below are the same twelve the canvas puts either side of the line.
      draw: (bottom) => [rule(`${ctx.prefix}rule`, ctx.frame.left, bottom - row.height - BLOCK_GAP - RULE, ctx.frame.width, RULE), ...row.draw(bottom)],
      ...(fill ? { fill: { ...fill, at: (factor: number) => ruled(fill.at(factor)) } } : {}),
      ...(shed ? { shed: { ...shed, without: (ids: readonly string[]) => ruled(shed.without(ids)) } } : {}),
    };
  };
  const live = [rows[0], ruled(rows[1])].filter((row): row is StackRow => row !== undefined);
  return stack(ctx.frame, live, ctx.density, BLOCK_GAP);
});
