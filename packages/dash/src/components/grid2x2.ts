/**
 * grid2x2: a label over two rows of two numerals at the grid rung size, in car orientation
 * (front left, front right / rear left, rear right). Rows 4 apart, columns 16 apart, columns
 * splitting the inner width equally; the block is centred vertically.
 */
import type { Hex, Item, Rect } from '../generator.ts';
import type { Expr } from '../bind.ts';
import type { Chars } from '../design/metrics.ts';
import type { RungSpec } from '../design/rung.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { ds } from '../tokens.ts';
import { cardFrame, centredTop } from './frame.ts';
import type { LabelSpec } from './readout.ts';

export interface CellSpec {
  sample: string;
  bind: Expr;
  chars: Chars;
  color?: Hex;
  colorBind?: Expr;
}

export const CORNERS = ['fl', 'fr', 'rl', 'rr'] as const;

export function grid2x2(slot: Rect, rung: RungSpec, prefix: string, lbl: LabelSpec, cellSpecs: readonly [CellSpec, CellSpec, CellSpec, CellSpec]): Item[] {
  const f = cardFrame(slot, rung);
  const labelFs = ds.size.label;
  const rowGap = ds.space[1];
  const colGap = ds.space[4];
  const fs = rung.grid;
  const top = centredTop(slot, labelFs + rowGap + fs + rowGap + fs);
  const colWidth = (f.innerWidth - colGap) / 2;
  const xs = [f.x, f.x + colWidth + colGap] as const;
  const row0 = top + labelFs + rowGap;
  const ys = [row0, row0 + fs + rowGap] as const;
  const items: Item[] = [label(`${prefix}label`, lbl.text, f.x, top, f.innerWidth, { bind: lbl.bind })];
  cellSpecs.forEach((cell, i) => {
    const x = xs[i % 2] ?? f.x;
    const y = ys[i < 2 ? 0 : 1];
    const item = numeral(`${prefix}${CORNERS[i] ?? String(i)}`, cell.sample, x, y, fs, cell.chars, { bind: cell.bind, color: cell.color, colorBind: cell.colorBind });
    // A cell's box is its column at most. Nothing follows a grid cell and SimHub does not clip
    // left-aligned text, so a value wider than its column (kPa pressures at rung S) overflows the
    // box rather than the card.
    item.rect.width = Math.min(item.rect.width, Math.round(colWidth));
    items.push(item);
  });
  return items;
}
