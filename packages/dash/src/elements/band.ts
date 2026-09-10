/** band: a full-width flag or indicator rectangle, radius 0, optionally outlined. */
import type { Hex, Rect, RectangleItem } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { roundRect } from '../design/geometry.ts';

export interface BandOptions {
  /** A border of `width` px on all four sides. */
  border?: { color: Hex; width: number };
  visibleBind?: Expr;
}

export function band(name: string, r: Rect, color: Hex, opts: BandOptions = {}): RectangleItem {
  const b = opts.border;
  return {
    kind: 'rect',
    name,
    rect: roundRect(r),
    backgroundColor: color,
    ...(b ? { border: { color: b.color, top: b.width, bottom: b.width, left: b.width, right: b.width } } : {}),
    ...withBindings({ Visible: opts.visibleBind }),
  };
}
