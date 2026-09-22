/** segment: one shift-light or rev-bar cell, radius.seg, the one rounded thing on the face. On a round face it is rotated to lie on the arc. */
import type { Hex, Rect, RectangleItem } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { roundRect } from '../design/geometry.ts';
import { ds } from '../tokens.ts';

export interface SegmentOptions {
  /** BackgroundColor binding, typically `if(lit, colour, unlit)`. */
  colorBind?: Expr;
  /** BlinkEnabled binding. */
  blinkBind?: Expr;
  /** Half period in ms when blinking; SimHub's default is 250. */
  blinkDelayMs?: number;
  visibleBind?: Expr;
  /** Degrees clockwise around the segment's centre; the rev arc sets it to the segment's angle. */
  rotation?: number;
}

export function segment(name: string, r: Rect, color: Hex, opts: SegmentOptions = {}): RectangleItem {
  return withMoreBindings({
    kind: 'rect',
    name,
    rect: roundRect(r),
    ...(opts.rotation ? { rotation: opts.rotation } : {}),
    backgroundColor: color,
    border: { radius: ds.radius.seg },
    ...(opts.blinkBind !== undefined ? { blink: { delayMs: opts.blinkDelayMs ?? 250 } } : {}),
  }, { BackgroundColor: opts.colorBind, BlinkEnabled: opts.blinkBind, Visible: opts.visibleBind });
}
