/**
 * 850 x 480 as zones.
 *
 * Narrow zones, and the first real `tall narrow` in the repository: 274 by 328 is where a page
 * stacks one column and keeps only the reading it exists for. Five settings in the bar rather than
 * seven, and no corner blocks on the band -- the artboards draw neither.
 *
 * Every rectangle is read off `design/canvas/Dash850x480.dc.html` and tabulated in
 * `docs/design/zones.md`.
 */
import { rect } from '../../design/geometry.ts';
import { ds } from '../../tokens.ts';
import { zoneLayoutDescription, type ZoneLayout } from '../layout.ts';

export const zoneFace850x480: ZoneLayout = {
  folder: 'OpenDash 850x480',
  description: zoneLayoutDescription(850, 480),
  width: 850,
  height: 480,
  background: ds.color.surface.base,
  bandCorners: false,
  barFieldsPerEnd: 2,
  bar: { gap: 22, valueSize: 28, denominatorSize: 20, stripCell: 54 },
  revBarGap: ds.space[1],
  zones: {
    revBarWell: rect(8, 2, 834, 36),
    revBar: rect(14, 6, 822, 28),
    bar: rect(0, 40, 850, 50),
    zoneB: rect(0, 91, 274, 328),
    zoneA: rect(275, 91, 300, 328),
    zoneC: rect(576, 91, 274, 328),
    band: rect(0, 420, 850, 60),
    pitLimiter: rect(299, 97, 252, 30),
  },
};
