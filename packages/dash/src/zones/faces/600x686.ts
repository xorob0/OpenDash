/**
 * 600 x 686 as zones.
 *
 * Portrait, so the body stacks **A over B over C** rather than B beside A beside C: the gear goes
 * at the top where a driver looks, and the two catalogue zones under it. One field per end of the
 * bar, because 600 px does not hold four and a half-drawn field is worse than a missing one.
 *
 * Every rectangle is read off `design/canvas/Dash600x686.dc.html` and tabulated in
 * `docs/design/zones.md`.
 */
import { rect } from '../../design/geometry.ts';
import { ds } from '../../tokens.ts';
import { zoneLayoutDescription, type ZoneLayout } from '../layout.ts';

export const zoneFace600x686: ZoneLayout = {
  folder: 'openDash 600x686',
  description: zoneLayoutDescription(600, 686),
  width: 600,
  height: 686,
  background: ds.color.surface.base,
  bandCorners: false,
  barFieldsPerEnd: 1,
  bar: { gap: 12, valueSize: 28, denominatorSize: 20, stripCell: 54 },
  revBarGap: ds.space[1],
  zones: {
    revBarWell: rect(6, 2, 588, 32),
    revBar: rect(12, 6, 576, 24),
    bar: rect(0, 36, 600, 46),
    zoneB: rect(0, 318, 600, 160),
    zoneA: rect(0, 83, 600, 234),
    zoneC: rect(0, 479, 600, 150),
    band: rect(0, 630, 600, 56),
    pitLimiter: rect(200, 90, 200, 28),
  },
};
