/**
 * 800 x 286 as zones.
 *
 * The nano has **no bar at all**: the height is not there, so the body starts straight under the
 * rev bar and the band carries the one changeable region a settled strip would have taken.
 *
 * Every rectangle is read off `design/canvas/Dash800x286.dc.html` and tabulated in
 * `docs/design/zones.md`.
 */
import { rect } from '../../design/geometry.ts';
import { ds } from '../../tokens.ts';
import { zoneLayoutDescription, type ZoneLayout } from '../layout.ts';

export const zoneFace800x286: ZoneLayout = {
  folder: 'openDash 800x286',
  description: zoneLayoutDescription(800, 286),
  width: 800,
  height: 286,
  background: ds.color.surface.base,
  bandCorners: false,
  barFieldsPerEnd: 2,
  revBarGap: ds.space[1],
  zones: {
    revBarWell: rect(6, 1, 788, 30),
    revBar: rect(12, 5, 776, 22),
    // No bar: the height is not there.
    zoneB: rect(0, 33, 269, 194),
    zoneA: rect(270, 33, 260, 194),
    zoneC: rect(531, 33, 269, 194),
    band: rect(0, 228, 800, 58),
    pitLimiter: rect(294, 39, 212, 30),
  },
};
