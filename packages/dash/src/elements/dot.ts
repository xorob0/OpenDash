/**
 * dot: a 6 px square, never a circle.
 *
 * The page indicator, a table's rank marker and the pit view's corner toggles are the same mark
 * drawn at different sizes, so the square is one element and the size is the caller's wherever the
 * sheet it comes from draws something other than six: the element sheet says 6 and the page
 * indicator's own sheet says 8, and neither of them is wrong about the other.
 */
import type { Hex, RectangleItem } from '../generator.ts';
import { rect } from '../design/geometry.ts';
import { band, type BandOptions } from './band.ts';

/** The side the element sheet draws. */
export const DOT_SIZE = 6;

export interface DotOptions extends BandOptions {
  /** Side, when the sheet the caller works from draws it larger than the element's six. */
  size?: number;
}

/** A square dot with its top-left corner at (x, y). */
export function dot(name: string, x: number, y: number, color: Hex, opts: DotOptions = {}): RectangleItem {
  const { size = DOT_SIZE, ...shape } = opts;
  return band(name, rect(x, y, size, size), color, shape);
}
