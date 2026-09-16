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

/**
 * The margin the artboards leave between the segments and the well they sit in: six pixels either
 * side and four above and below.
 *
 * Read off the zone faces, each of which carries its well as a rect of its own -- 18, 4, 1884, 40
 * around a bar at 24, 8, 1872, 32 at 1920, and the same six and four at 850 and at the nano. It
 * lives here rather than beside either caller, because the well is a fact about how segments are
 * drawn and both the card faces and the speedo need the same answer.
 */
export const REV_WELL_PAD_X = 6;
export const REV_WELL_PAD_Y = 4;

export function revBar(frame: RevBarFrame, prefix = 'revBar'): Item[] {
  const spans = snapEdges(frame.left, frame.width, ds.shiftLights.segments, frame.gap);
  return revLayers(
    prefix,
    spans.map((span) => ({ rect: rect(span.left, frame.top, span.width, frame.height) })),
  );
}
