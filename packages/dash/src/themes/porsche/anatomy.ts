/**
 * The Porsche 992 display at every face size (#205, #713).
 *
 * The rectangles are `geometry.ts`'s, which holds the ticket's geometry table for the reference
 * 1280 x 480, the car's own 8:3, and the rules the other sizes follow from it. Each zone fills the
 * inside of the 3 px border the chrome draws round it, so that the cells a page draws stand flush
 * against the border as the render's blocks do, and the gear's tile holds zone A the same way. The
 * settings column at the left and the telltale column at the right are not zones and are drawn by
 * the chrome around them.
 *
 * Only rectangles here, because the registry reads this file before `ds` exists.
 */
import { rect } from '../../design/geometry.ts';
import type { Anatomy } from '../anatomy.ts';
import { insideBorder, PIT_BANNER, porscheFace, PORSCHE_SIZES } from './geometry.ts';

export const porscheAnatomy: Anatomy = {
  sizes: PORSCHE_SIZES,
  regions: (layout) => {
    const face = porscheFace(layout);
    const { dots } = face;
    const tile = insideBorder(face.panels.A);
    // Zone A begins under the pit banner where the tile is too short for the gear to clear it alone.
    const gear = rect(tile.left, tile.top + face.gearClear, tile.width, tile.height - face.gearClear);
    return [
      // The car's dots sit on the glass with nothing behind them, so the well is only the room they
      // take, a few pixels either side, which is what the arrangement without them gives back.
      { role: 'revBarWell', rect: rect(dots.left - 10, dots.top - 2, dots.width + 20, dots.height + 4) },
      { role: 'revBar', rect: dots },
      // The top strip: the page name, the speed box, the lap and the track state.
      { role: 'bar', rect: face.strip },
      { role: 'zone', zone: 'B', rect: insideBorder(face.panels.B) },
      { role: 'zone', zone: 'A', rect: gear },
      { role: 'zone', zone: 'C', rect: insideBorder(face.panels.C) },
      // The foot: the badge's place, the TC and ABS boxes, the tyre box and the brake bias.
      { role: 'band', rect: face.foot },
      // The house's pit family keeps its banner at the top of the gear, inside the tile.
      { role: 'pitAlert', rect: rect(tile.left + PIT_BANNER.inset, tile.top + PIT_BANNER.top, tile.width - 2 * PIT_BANNER.inset, PIT_BANNER.height) },
      { role: 'hero', rect: gear },
      // The body between the settings column and the telltales, zones B, A and C with their panels,
      // which the full-screen flag takes as the limiter does.
      { role: 'flagBody', rect: face.body },
    ];
  },
};
