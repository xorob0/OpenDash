/**
 * 1920 x 480 as zones: the reference face.
 *
 * Every rectangle is read off `design/canvas/Dash.dc.html`, which places each part absolutely, and
 * is tabulated in `docs/design/zones.md`. They are not derived from one another and not rounded
 * from a ratio, because the artboard is the design and a derivation would be a second opinion.
 */
import { rect } from '../../design/geometry.ts';
import { ds } from '../../tokens.ts';
import { zoneLayoutDescription, type ZoneLayout } from '../layout.ts';

const WIDTH = 1920;
const HEIGHT = 480;

export const zoneFace1920x480: ZoneLayout = {
  folder: 'OpenDash',
  description: zoneLayoutDescription(WIDTH, HEIGHT),
  width: WIDTH,
  height: HEIGHT,
  background: ds.color.surface.base,
  bandCorners: true,
  barFieldsPerEnd: 2,
  bar: { gap: 22, valueSize: 34, denominatorSize: 24, stripCell: 57 },
  revBarGap: ds.space[2],
  zones: {
    revBarWell: rect(18, 4, 1884, 40),
    revBar: rect(24, 8, 1872, 32),
    bar: rect(0, 48, 1920, 56),
    zoneB: rect(0, 105, 769, 314),
    zoneA: rect(770, 105, 380, 314),
    zoneC: rect(1151, 105, 769, 314),
    band: rect(0, 420, 1920, 60),
    pitLimiter: rect(824, 111, 272, 30),
  },
};
