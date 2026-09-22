/**
 * 480 round, the round and square 480 x 480 DDUs: the face is the display, so the outer 12 px
 * are the flag ring and everything else sits in the 456 px inner disc. The rev arc runs over the
 * top at radius 206, the gear stands alone in the middle, the pit limiter sits above it, and two
 * 140 x 108 slots (rung S) flank it. Geometry from design/canvas/DashRound480.dc.html, whose
 * positions are relative to the inner disc.
 *
 * Alone, where the 800 draws the gear with its two neighbours ghosted: the artboard lays that
 * cluster across the whole 456 px disc, and the gear's rect here is the 160 px between the two
 * slots, which a 286 px cluster would have to overhang. Which of the two gives way is the
 * author's, and until it is settled this face draws the gear the canvas's own size.
 */
import { rect } from '../design/geometry.ts';
import { ds } from '../tokens.ts';
import type { Layout } from './layout.ts';
import { roundLayout } from './round.ts';

const SIZE = 480;

export const layout480round: Layout = roundLayout({
  folder: 'OpenDash 480 round',
  size: SIZE,
  revArc: { r: 206, segment: { width: 22, height: 14 } },
  // The gap between the two slots, not the whole disc, so that the gear component can tell
  // whether its cell fits instead of overhanging a slot.
  gear: { rect: rect(148, 96, 160, 340) },
  pitLimiter: rect(153, 100, 150, ds.indicator.pitLimiter.heightSm),
  slotSize: { width: 140, height: 108 },
  slotOrigins: [
    [8, 174],
    [308, 174],
  ],
});
