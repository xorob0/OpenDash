/**
 * 800 x 480 as zones, derived from 850 x 480.
 *
 * This is the one size with no artboard of its own, and it never had one. It is 850 less fifty
 * pixels of width and the same height, so every part keeps its 850 geometry and the two catalogue
 * zones give up twenty-five pixels each. Deriving it is the honest answer: a hand-drawn 800 would
 * be a second opinion about a face the design never held one about, and the layout says so here
 * exactly as `layouts/800x480.ts` says it for the card face.
 */
import { rect } from '../../design/geometry.ts';
import { ds } from '../../tokens.ts';
import { zoneLayoutDescription, type ZoneLayout } from '../layout.ts';
import { zoneFace850x480 } from './850x480.ts';

const WIDTH = 800;
const HEIGHT = 480;
/** What the two catalogue zones give up, half of the fifty pixels 850 has and this does not. */
const NARROWER = 25;

const from = zoneFace850x480.zones;

export const zoneFace800x480: ZoneLayout = {
  folder: 'openDash 800x480',
  description: zoneLayoutDescription(WIDTH, HEIGHT),
  width: WIDTH,
  height: HEIGHT,
  background: ds.color.surface.base,
  bandCorners: false,
  barFieldsPerEnd: 2,
  bar: zoneFace850x480.bar,
  zones: {
    revBarWell: rect(from.revBarWell.left, from.revBarWell.top, WIDTH - 2 * from.revBarWell.left, from.revBarWell.height),
    revBar: rect(from.revBar.left, from.revBar.top, WIDTH - 2 * from.revBar.left, from.revBar.height),
    bar: rect(0, from.bar!.top, WIDTH, from.bar!.height),
    zoneB: rect(0, from.zoneB.top, from.zoneB.width - NARROWER, from.zoneB.height),
    zoneA: rect(from.zoneA.left - NARROWER, from.zoneA.top, from.zoneA.width, from.zoneA.height),
    zoneC: rect(from.zoneC.left - NARROWER, from.zoneC.top, from.zoneC.width - NARROWER, from.zoneC.height),
    band: rect(0, from.band.top, WIDTH, from.band.height),
    pitLimiter: rect(from.pitLimiter.left - NARROWER, from.pitLimiter.top, from.pitLimiter.width, from.pitLimiter.height),
  },
};
