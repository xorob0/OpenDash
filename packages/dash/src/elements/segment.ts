/** segment: one shift-light or rev-bar cell, radius.seg, the one rounded thing on the face. */
import type { Hex, Rect, RectangleItem } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
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
}

export function segment(name: string, r: Rect, color: Hex, opts: SegmentOptions = {}): RectangleItem {
  return {
    kind: 'rect',
    name,
    rect: roundRect(r),
    backgroundColor: color,
    border: { radius: ds.radius.seg },
    ...(opts.blinkBind !== undefined ? { blink: { delayMs: opts.blinkDelayMs ?? 250 } } : {}),
    ...withBindings({ BackgroundColor: opts.colorBind, BlinkEnabled: opts.blinkBind, Visible: opts.visibleBind }),
  };
}
