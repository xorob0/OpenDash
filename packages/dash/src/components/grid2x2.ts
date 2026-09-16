/**
 * grid2x2: a label over two rows of two numerals at the grid rung size, in car orientation
 * (front left, front right / rear left, rear right). Rows 4 apart, columns 16 apart, columns
 * splitting the inner width equally; the block is centred vertically. A cell's character budget
 * must fit its column (`gridColumnWidth`), and the box takes the rest of the column so that the
 * last glyph is never clipped. The layout tests check every grid card of every layout for it.
 *
 * A cell may carry a second, smaller value under its numeral: the tread left under a tyre
 * temperature. The canvas draws that at rung L only, where the slot has the height for two rows of
 * it; the numeral then takes the smaller size the other rungs draw so the pair fits the card, and
 * the gaps go to the 6 and 18 the canvas gives a grid of two-line cells.
 */
import type { Hex, Item, Rect } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { cells, monoWidth, type Chars } from '../design/metrics.ts';
import type { RungSpec } from '../design/rung.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { ds } from '../tokens.ts';
import { cardFrame, centredTop } from './frame.ts';
import type { LabelSpec } from './readout.ts';

export interface SubSpec {
  sample: string;
  bind: Expr;
  chars: Chars;
  /**
   * What follows the sub-value, drawn as a label rather than through a cell: the per-cent sign is
   * one of the glyphs `font.cell.excluded` names as overrunning the digit cell.
   */
  unit: string;
  colorBind?: Expr;
}

export interface CellSpec {
  sample: string;
  bind: Expr;
  chars: Chars;
  color?: Hex;
  colorBind?: Expr;
  /** The second value under the numeral, drawn only where every cell of the grid carries one. */
  sub?: SubSpec;
}

export const CORNERS = ['fl', 'fr', 'rl', 'rr'] as const;

/** Gap between the two columns. */
const COLUMN_GAP = ds.space[4];

/**
 * The gaps of a grid whose cells carry a sub-value, which the canvas draws two pixels wider each
 * way than the plain grid's 4 and 16: a row is two lines tall there and wants the air. Off the
 * `space` scale, as `READOUT_GAP` is, and the canvas is their citation.
 */
const SUB_ROW_GAP = 6;
const SUB_COLUMN_GAP = 18;

/** Gap between a cell's numeral and the sub-value under it. */
const SUB_GAP = 2;

/** Width of one column of the grid in `slot` at `rung`: the inner width less the gap, halved. */
export const gridColumnWidth = (slot: Rect, rung: RungSpec, columnGap: number = COLUMN_GAP): number => (cardFrame(slot, rung).innerWidth - columnGap) / 2;

export function grid2x2(slot: Rect, rung: RungSpec, prefix: string, lbl: LabelSpec, cellSpecs: readonly [CellSpec, CellSpec, CellSpec, CellSpec]): Item[] {
  const f = cardFrame(slot, rung);
  const labelFs = ds.size.label;
  // All four or none: a grid with a sub-value under one numeral and not the next reads as a fault.
  const subFs = ds.size.labelSm;
  const withSubs = rung.rung === 'L' && cellSpecs.every((c) => c.sub !== undefined);
  const rowGap = withSubs ? SUB_ROW_GAP : ds.space[1];
  const columnGap = withSubs ? SUB_COLUMN_GAP : COLUMN_GAP;
  const fs = withSubs ? ds.size.valueSm : rung.grid;
  const rowHeight = withSubs ? fs + SUB_GAP + subFs : fs;
  const top = centredTop(slot, labelFs + rowGap + rowHeight + rowGap + rowHeight);
  const colWidth = gridColumnWidth(slot, rung, columnGap);
  const xs = [f.x, f.x + colWidth + columnGap] as const;
  const row0 = top + labelFs + rowGap;
  const ys = [row0, row0 + rowHeight + rowGap] as const;
  const items: Item[] = [label(`${prefix}label`, lbl.text, f.x, top, f.innerWidth, { bind: lbl.bind })];
  cellSpecs.forEach((cell, i) => {
    const x = xs[i % 2] ?? f.x;
    const y = ys[i < 2 ? 0 : 1];
    const name = `${prefix}${CORNERS[i] ?? String(i)}`;
    const right = i % 2 === 0 ? x + colWidth + columnGap : slot.left + slot.width - rung.padding.x;
    items.push(
      numeral(name, cell.sample, x, y, fs, cell.chars, {
        maxWidth: i % 2 === 0 ? colWidth + columnGap : slot.left + slot.width - x,
        bind: cell.bind,
        color: cell.color,
        colorBind: cell.colorBind,
      }),
    );
    const sub = withSubs ? cell.sub : undefined;
    if (!sub) return;
    const subY = y + fs + SUB_GAP;
    // The unit sits after the cells rather than after the sample, so it does not move under a
    // value that gains a digit; the column is what caps its box.
    const unitX = x + monoWidth(cells('SemiBold', subFs), sub.chars);
    items.push(numeral(`${name}.sub`, sub.sample, x, subY, subFs, sub.chars, { maxWidth: colWidth, bind: sub.bind, color: ds.color.text.secondary, colorBind: sub.colorBind }));
    items.push(unit(`${name}.subunit`, sub.unit, unitX, subY, Math.max(0, right - unitX), { colorBind: sub.colorBind }));
  });
  return items;
}
