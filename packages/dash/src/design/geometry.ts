/** Rect helpers: grids of equal cells separated by rules, integer-snapped segment edges, insets, and placement on a circle for round faces. */
import type { Rect } from '../generator.ts';

export type { Rect };

export const rect = (left: number, top: number, width: number, height: number): Rect => ({ left, top, width, height });

export const right = (r: Rect): number => r.left + r.width;
export const bottom = (r: Rect): number => r.top + r.height;

export const roundRect = (r: Rect): Rect => ({
  left: Math.round(r.left),
  top: Math.round(r.top),
  width: Math.round(r.width),
  height: Math.round(r.height),
});

export const translate = (r: Rect, dx: number, dy: number): Rect => ({ ...r, left: r.left + dx, top: r.top + dy });

/** Shrinks a rect by a vertical and a horizontal padding (CSS "y x" order). */
export const inset = (r: Rect, y: number, x: number = y): Rect => rect(r.left + x, r.top + y, r.width - 2 * x, r.height - 2 * y);

/** True when `inner` lies entirely within `outer` (edges may touch). */
export const contains = (outer: Rect, inner: Rect): boolean =>
  inner.left >= outer.left && inner.top >= outer.top && right(inner) <= right(outer) && bottom(inner) <= bottom(outer);

/** True when the two rects share any area (touching edges do not count). */
export const overlaps = (a: Rect, b: Rect): boolean =>
  a.left < right(b) && b.left < right(a) && a.top < bottom(b) && b.top < bottom(a);

export interface Span {
  left: number;
  width: number;
}

/**
 * `count` segments filling `width` from `left` with `gap` between them, every edge snapped to an
 * integer so that adjacent gaps are exactly `gap` wide and the last segment ends at left + width.
 */
export function snapEdges(left: number, width: number, count: number, gap: number): Span[] {
  if (count < 1) return [];
  const pitch = (width + gap) / count;
  const spans: Span[] = [];
  for (let k = 0; k < count; k++) {
    const start = Math.round(left + k * pitch);
    const end = Math.round(left + (k + 1) * pitch - gap);
    spans.push({ left: start, width: end - start });
  }
  return spans;
}

export interface Size {
  width: number;
  height: number;
}

/** Row-major rects of a cols x rows grid of equal cells, `gap` apart (the gap is where a rule goes). */
export function grid(origin: { left: number; top: number }, cols: number, rows: number, cell: Size, gap: number): Rect[] {
  const out: Rect[] = [];
  for (let r = 0; r < rows; r++) {
    for (let c = 0; c < cols; c++) {
      out.push(rect(origin.left + c * (cell.width + gap), origin.top + r * (cell.height + gap), cell.width, cell.height));
    }
  }
  return out;
}

/** The rules between the cells of `grid(...)`: verticals first, then horizontals, each `gap` thick. */
export function gridRules(origin: { left: number; top: number }, cols: number, rows: number, cell: Size, gap: number): Rect[] {
  const totalW = cols * cell.width + (cols - 1) * gap;
  const totalH = rows * cell.height + (rows - 1) * gap;
  const out: Rect[] = [];
  for (let c = 1; c < cols; c++) out.push(rect(origin.left + c * (cell.width + gap) - gap, origin.top, gap, totalH));
  for (let r = 1; r < rows; r++) out.push(rect(origin.left, origin.top + r * (cell.height + gap) - gap, totalW, gap));
  return out;
}

/** A circle on the face: the disc of a round DDU, or the ring the rev arc segments sit on. */
export interface Circle {
  cx: number;
  cy: number;
  r: number;
}

/** Centre of a rect. */
export const centre = (r: Rect): { x: number; y: number } => ({ x: r.left + r.width / 2, y: r.top + r.height / 2 });

/** Distance between two points. */
export const distance = (a: { x: number; y: number }, b: { x: number; y: number }): number => Math.hypot(a.x - b.x, a.y - b.y);

const toRadians = (degrees: number): number => (degrees * Math.PI) / 180;

/**
 * A `size` rect whose centre lies on `circle` at `angle` degrees clockwise from the top, unrounded:
 * `left = cx + r sin a - w / 2`, `top = cy - r cos a - h / 2`. Rotating the rect by `angle` makes
 * it tangent to the circle.
 */
export function onCircle(circle: Circle, angle: number, size: Size): Rect {
  const a = toRadians(angle);
  return rect(circle.cx + circle.r * Math.sin(a) - size.width / 2, circle.cy - circle.r * Math.cos(a) - size.height / 2, size.width, size.height);
}

/** The bounding square of a circle. */
export const squareOf = (circle: Circle): Rect => rect(circle.cx - circle.r, circle.cy - circle.r, 2 * circle.r, 2 * circle.r);
