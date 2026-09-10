/**
 * revArc: the fifteen rev segments on a circle, the header of a round face. Segment k sits at
 * angle a_k = -63 + 9 k degrees from the top with its centre on the circle and is rotated by
 * a_k so that it lies tangent to it. Same layers and bindings as the rev bar (revSegments.ts).
 *
 * The design canvas steps 12 degrees (168 degrees, -84 to 84), which puts the end segments
 * inside the slots flanking the gear on both round faces: at 84 degrees a segment centre sits
 * only 0.1 r above the horizontal diameter, whatever r is, so no radius clears a slot whose top
 * is well above the centre line, and the 480's slots already touch the inner disc. A 9 degree
 * step keeps the arc a cap over the top whose ends stop 26 px (480) and 32 px (800) above the
 * slot tops; 10 degrees would still cut 7 px into the 800's slots.
 */
import type { Item } from '../generator.ts';
import { onCircle, type Circle, type Size } from '../design/geometry.ts';
import { ds } from '../tokens.ts';
import { revLayers } from './revSegments.ts';

export interface RevArcFrame {
  /** The circle the segment centres lie on: the face centre and the arc radius. */
  circle: Circle;
  /** Segment size before rotation: width along the arc, height across it. */
  segment: Size;
}

/** Degrees between neighbouring segments. Fifteen of them span 126 degrees, -63 to 63. */
export const REV_ARC_STEP = 9;

/** Angle of segment k of `count`, degrees clockwise from the top, symmetric about the top. */
export const revArcAngle = (k: number, count: number, step = REV_ARC_STEP): number => -((count - 1) * step) / 2 + k * step;

export function revArc(frame: RevArcFrame, prefix = 'revArc'): Item[] {
  const count = ds.shiftLights.segments;
  return revLayers(
    prefix,
    Array.from({ length: count }, (_, k) => {
      const angle = revArcAngle(k, count);
      return { rect: onCircle(frame.circle, angle, frame.segment), rotation: angle };
    }),
  );
}
