/**
 * 800 round, the 5 inch round DDU: the face is the display, so the outer 12 px are the flag
 * ring and everything else sits in the 776 px inner disc. The rev arc runs over the top at
 * radius 352, the gear stands in the middle of a 320 x 280 column with the gear below and the
 * gear above ghosted either side of it, the pit limiter sits above them, and six 180 x 110 slots
 * sit two per side and two below: left column, right column, then the bottom row. The slots are
 * rung M by width but only 110 high, so the cards take the S padding, as the canvas draws them
 * (8 / 12). Geometry from design/canvas/DashRound800.dc.html, whose positions are relative to
 * the inner disc.
 */
import { rect } from '../design/geometry.ts';
import { rungSpec } from '../design/rung.ts';
import { ds } from '../tokens.ts';
import type { Layout } from './layout.ts';
import { roundLayout } from './round.ts';

const SIZE = 800;

export const layout800round: Layout = roundLayout({
  folder: 'OpenDash 800 round',
  size: SIZE,
  revArc: { r: 352, segment: { width: 30, height: 18 } },
  // Two fifths of the gear, not the 0.42 the three zone sheets draw: this artboard sets 104 px
  // either side of a 260 px gear, and 16 px of air between each ghost and the gear's cell.
  gear: { rect: rect(228, 248, 320, 280), neighbours: { gap: ds.space[4], ratio: 0.4 } },
  pitLimiter: rect(288, 164, 200, ds.indicator.pitLimiter.height),
  slotSize: { width: 180, height: 110 },
  slotOrigins: [
    [32, 278],
    [32, 392],
    [564, 278],
    [564, 392],
    [198, 550],
    [398, 550],
  ],
  cardPadding: rungSpec('S').padding,
});
