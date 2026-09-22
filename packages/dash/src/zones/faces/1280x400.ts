/**
 * 1280 x 400 as zones.
 *
 * A shorter body, the same zones. Everything gives a little: the well, the bar and the band all
 * lose a few pixels rather than any one of them losing all of it.
 *
 * Every rectangle is read off `design/canvas/Dash1280x400.dc.html` and tabulated in
 * `docs/design/zones.md`.
 */
import { rect } from '../../design/geometry.ts';
import { ds } from '../../tokens.ts';
import { zoneLayoutDescription, type ZoneLayout } from '../layout.ts';

export const zoneFace1280x400: ZoneLayout = {
  folder: 'OpenDash 1280x400',
  description: zoneLayoutDescription(1280, 400),
  width: 1280,
  height: 400,
  background: ds.color.surface.base,
  bandCorners: true,
  barFieldsPerEnd: 2,
  bar: { gap: 22, valueSize: 28, denominatorSize: 20, stripCell: 54 },
  revBarGap: 6,
  zones: {
    revBarWell: rect(10, 2, 1260, 32),
    revBar: rect(16, 6, 1248, 24),
    bar: rect(0, 36, 1280, 50),
    zoneB: rect(0, 87, 469, 258),
    zoneA: rect(470, 87, 340, 258),
    zoneC: rect(811, 87, 469, 258),
    band: rect(0, 346, 1280, 54),
    pitLimiter: rect(504, 93, 272, 30),
  },
};
