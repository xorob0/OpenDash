/** band: a full-width flag or indicator rectangle, square-cornered unless a drawing asks otherwise, optionally outlined; also the hard-edged check of a chequered flag, rotated on a ring. */
import type { Hex, Rect, RectangleItem } from '../generator.ts';
import { formula, withBindings, type Expr } from '../bind.ts';
import { roundRect } from '../design/geometry.ts';

export interface BandOptions {
  /** A border of `width` px on all four sides, its colour optionally bound: an outlined chip whose
   *  ink and edge change together binds the same expression to both. */
  border?: { color: Hex; width: number; colorBind?: Expr };
  /** Corner radius, for the rectangles a drawing is built from: the tyre's body and its change badge. */
  radius?: number;
  visibleBind?: Expr;
  /** Degrees clockwise around the rect's centre; the chequered ring rotates its checks onto the rim. */
  rotation?: number;
}

export function band(name: string, r: Rect, color: Hex, opts: BandOptions = {}): RectangleItem {
  const b = opts.border;
  const border =
    b || opts.radius !== undefined
      ? {
          ...(b ? { color: b.color, top: b.width, bottom: b.width, left: b.width, right: b.width } : {}),
          ...(b?.colorBind ? { colorBinding: formula(b.colorBind) } : {}),
          ...(opts.radius === undefined ? {} : { radius: opts.radius }),
        }
      : undefined;
  return {
    kind: 'rect',
    name,
    rect: roundRect(r),
    ...(opts.rotation ? { rotation: opts.rotation } : {}),
    backgroundColor: color,
    ...(border ? { border } : {}),
    ...withBindings({ Visible: opts.visibleBind }),
  };
}
