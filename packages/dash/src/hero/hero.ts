/**
 * The hero module: what a driver reads by reflex, fixed per layout. Rev bar at the top, gear
 * and speed in the centre column, pit limiter above the gear, flag strip at the bottom edge.
 */
import type { Item, Rect } from '../generator.ts';
import { flagStrip } from '../components/flagStrip.ts';
import { gearSpeed } from '../components/gearSpeed.ts';
import { pitLimiter } from '../components/pitLimiter.ts';
import { revBar, type RevBarFrame } from '../components/revBar.ts';

export interface HeroGeometry {
  revBar: RevBarFrame;
  /** The column between the two slot grids that holds gear and speed. */
  column: Rect;
  pitLimiter: Rect;
  flagStrip: Rect;
}

export function hero(geometry: HeroGeometry): Item[] {
  return [...revBar(geometry.revBar), ...gearSpeed(geometry.column), ...pitLimiter(geometry.pitLimiter), ...flagStrip(geometry.flagStrip)];
}
