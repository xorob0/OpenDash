/**
 * 1280 x 480 as zones.
 *
 * The same five parts at a lower body height. Zone C lists nine drivers here against seven
 * at 1920, because a zone is 320 tall rather than 314 and the rows are what a page buys first.
 *
 * Every rectangle is read off `design/canvas/Dash1280x480.dc.html` and tabulated in
 * `docs/design/zones.md`.
 */
import { rect } from '../../design/geometry.ts';
import { ds } from '../../tokens.ts';
import { zoneLayoutDescription, type ZoneLayout } from '../layout.ts';

export const zoneFace1280x480: ZoneLayout = {
  folder: 'openDash 1280x480',
  description: zoneLayoutDescription(1280, 480),
  width: 1280,
  height: 480,
  background: ds.color.surface.base,
  bandCorners: true,
  barFieldsPerEnd: 2,
  zones: {
    revBarWell: rect(10, 3, 1260, 38),
    revBar: rect(16, 7, 1248, 30),
    bar: rect(0, 44, 1280, 54),
    zoneB: rect(0, 99, 469, 320),
    zoneA: rect(470, 99, 340, 320),
    zoneC: rect(811, 99, 469, 320),
    band: rect(0, 420, 1280, 60),
    pitLimiter: rect(504, 105, 272, 30),
  },
};
