/**
 * 1280 x 720 as zones.
 *
 * The tall body is what lets zone C list the field. A 469 by 554 zone is the `tall` shape, so a
 * list grows rows rather than columns and eighteen drivers fit where seven do at 1920.
 *
 * Every rectangle is read off `design/canvas/Dash1280x720.dc.html` and tabulated in
 * `docs/design/zones.md`.
 */
import { rect } from '../../design/geometry.ts';
import { ds } from '../../tokens.ts';
import { zoneLayoutDescription, type ZoneLayout } from '../layout.ts';

export const zoneFace1280x720: ZoneLayout = {
  folder: 'openDash 1280x720',
  description: zoneLayoutDescription(1280, 720),
  width: 1280,
  height: 720,
  background: ds.color.surface.base,
  bandCorners: true,
  barFieldsPerEnd: 2,
  bar: { gap: 22, valueSize: 34, denominatorSize: 24, stripCell: 57 },
  revBarGap: 6,
  zones: {
    revBarWell: rect(14, 4, 1252, 40),
    revBar: rect(20, 8, 1240, 32),
    bar: rect(0, 48, 1280, 56),
    zoneB: rect(0, 105, 469, 554),
    zoneA: rect(470, 105, 340, 554),
    zoneC: rect(811, 105, 469, 554),
    band: rect(0, 660, 1280, 60),
    pitLimiter: rect(504, 111, 272, 30),
  },
};
