/**
 * revBar: the fifteen rev segments in a row with integer-snapped edges, the header of every
 * rectangular face. The per-segment logic (stages, redline flash, plain RPM bar) is shared
 * with the rev arc; see revSegments.ts.
 */
import type { Item } from '../generator.ts';
import { rect, snapEdges } from '../design/geometry.ts';
import { ds } from '../tokens.ts';
import { revLayers } from './revSegments.ts';

export { REDLINE_BLINK_MS } from './revSegments.ts';

export interface RevBarFrame {
  left: number;
  top: number;
  width: number;
  height: number;
  /** Gap between segments: 8 at 1920, 6 at 1280, 4 at 850 and below. */
  gap: number;
}

export function revBar(frame: RevBarFrame, prefix = 'revBar'): Item[] {
  const spans = snapEdges(frame.left, frame.width, ds.shiftLights.segments, frame.gap);
  return revLayers(
    prefix,
    spans.map((span) => ({ rect: rect(span.left, frame.top, span.width, frame.height) })),
  );
}
