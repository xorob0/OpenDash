/**
 * Placing rows inside a module's box. A module is a column of rows centred in its rect, which is
 * what the design canvas draws with `justify-content: center`, and a row is placed by its bottom
 * edge because the fields in it are bottom-aligned.
 *
 * A row that does not fit is not drawn: zones come in six heights (195 to 356 px) and a module
 * drops its last row rather than letting SimHub clip it.
 */
import type { Item, Rect } from '../generator.ts';
import { rect } from '../design/geometry.ts';
import { densityOf, type Density } from './density.ts';

/** One row of a module: how tall it is, and how it draws itself once its bottom edge is known. */
export interface StackRow {
  height: number;
  draw(bottom: number): Item[];
}

/** A row helper for the common case of items that do not depend on where they land. */
export const fixedRow = (height: number, draw: (bottom: number) => Item[]): StackRow => ({ height, draw });

/** The content box of a module: its rect less the density's padding. */
export function contentRect(frame: Rect, density: Density): Rect {
  const d = densityOf(density);
  return rect(frame.left + d.padX, frame.top + d.padY, Math.max(0, frame.width - 2 * d.padX), Math.max(0, frame.height - 2 * d.padY));
}

/** Total height of a stack of rows, gaps included. */
export const stackHeight = (rows: readonly StackRow[], gap: number): number =>
  rows.reduce((h, r) => h + r.height, 0) + gap * Math.max(0, rows.length - 1);

/**
 * The tail a row's last line box hangs below the bottom edge it was placed on.
 *
 * A row declares the height of its content; WPF draws a line box that runs a little below the
 * baseline row, and the box is what gets clipped. Two pixels covers it at every size the second
 * screens and the zones use, and reserving them is what keeps a stack exactly filling its frame
 * from putting its last row one pixel past the edge -- which is the whole of the overflow the zone
 * faces found on the narrow sizes.
 */
export const ROW_TAIL = 2;

/**
 * The rows that fit `height`, longest prefix first. Modules list their rows in importance order,
 * so a short zone keeps the reading that matters and drops the recap under it.
 */
export function rowsThatFit(rows: readonly StackRow[], height: number, gap: number): StackRow[] {
  const kept = [...rows];
  const room = height - 2 * ROW_TAIL;
  while (kept.length > 1 && stackHeight(kept, gap) > room) kept.pop();
  return kept;
}

/**
 * Draws rows as a column centred in `frame`, dropping trailing rows that do not fit. The gap
 * defaults to the density's row gap.
 */
export function stack(frame: Rect, rows: readonly StackRow[], density: Density, gap?: number): Item[] {
  const step = gap ?? densityOf(density).gapY;
  const kept = rowsThatFit(rows, frame.height, step);
  const total = stackHeight(kept, step);
  let y = Math.round(frame.top + (frame.height - total) / 2);
  const items: Item[] = [];
  for (const row of kept) {
    items.push(...row.draw(y + row.height));
    y += row.height + step;
  }
  return items;
}

/** Draws rows from the top of `frame` instead of centring them; tables fill downwards. */
export function stackFromTop(frame: Rect, rows: readonly StackRow[], gap: number): Item[] {
  const kept = rowsThatFit(rows, frame.height, gap);
  let y = frame.top;
  const items: Item[] = [];
  for (const row of kept) {
    items.push(...row.draw(y + row.height));
    y += row.height + gap;
  }
  return items;
}
