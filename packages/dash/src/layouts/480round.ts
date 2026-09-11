/**
 * 480 round, the round and square 480 x 480 DDUs: the face is the display, so the outer 12 px
 * are the flag ring and everything else sits in the 456 px inner disc. The rev arc runs over the
 * top at radius 206, the gear stands over a row of speed and unit in the middle, the pit
 * limiter sits above the gear, and two 140 x 108 slots (rung S) flank the gear. Geometry from
 * design/canvas/DashRound480.dc.html, whose positions are relative to the inner disc.
 */
import { rect } from '../design/geometry.ts';
import { ds } from '../tokens.ts';
import type { Layout } from './layout.ts';
import { roundLayout } from './round.ts';

const SIZE = 480;

export const layout480round: Layout = roundLayout({
  folder: 'openDash 480 round',
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
