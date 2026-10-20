/**
 * The BMW display at every face size (#203): the row of shift lights, zones B, A and C in the body's
 * columns, and band D as the foot. The rectangles are `geometry.ts`'s, which ports the canvas's rules.
 *
 * Only rectangles here, because the registry reads this file before `ds` exists.
 */
import { rect } from '../../design/geometry.ts';
import type { Anatomy } from '../anatomy.ts';
import { bmwFace, BMW_SIZES, ledRow } from './geometry.ts';

/**
 * The pit family's banner, at the canvas's label size and its padding. The display draws none: in the
 * lane its Pit Time box takes the lap time's place. So the banner keeps clear of the gear and the speed
 * and takes the top of column C, as the AiM's does, where the face takes the rest of any row it cuts
 * (#1165) and the pit time under it stays in sight.
 */
const PIT_BANNER = 44;

export const bmwAnatomy: Anatomy = {
  sizes: BMW_SIZES,
  regions: (layout) => {
    const face = bmwFace(layout);
    const { zoneA: a, zoneC: c } = face;
    // Zones A and C stand side by side on every face, the gear column at the left of column C.
    const hero = rect(a.left, a.top, c.left + c.width - a.left, Math.min(a.height, c.height));
    const banner = rect(c.left, c.top, c.width, Math.round(PIT_BANNER * face.s));
    return [
      // The lights sit on the black ground with nothing behind them, so the well is the row they stand in.
      { role: 'revBarWell', rect: face.leds },
      { role: 'revBar', rect: ledRow(face) },
      { role: 'zone', zone: 'B', rect: face.zoneB },
      { role: 'zone', zone: 'A', rect: a },
      { role: 'zone', zone: 'C', rect: c },
      { role: 'band', rect: face.foot },
      { role: 'pitAlert', rect: banner },
      { role: 'hero', rect: hero },
      { role: 'flagBody', rect: face.body },
    ];
  },
};
