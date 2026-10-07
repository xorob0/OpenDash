/**
 * The Porsche 992 display at 1280 x 480, the one size whose 8:3 is the car's own panel (#205).
 *
 * Every rectangle is the ticket's geometry table, "The drawing, in numbers", which was measured off
 * the Race 1 render and is the `After` artboard of the canvas. The zones are inset inside the
 * panels the chrome draws for them, four pixels in a grey panel and the three of its border in the
 * gear's tile, so that the rounded corners the car has stay visible around the square widget that
 * carries each zone's pages. The settings column at the left and the telltale column at the right
 * are not zones and are drawn by the chrome around them.
 *
 * The other six landscape sizes and the portrait face follow from these numbers by the rules in the
 * ticket and in its first comment, and are not claimed yet; the catalogue entry says 1280 x 480.
 *
 * Only rectangles here, because the registry reads this file before `ds` exists.
 */
import { rect } from '../../design/geometry.ts';
import type { Anatomy } from '../anatomy.ts';

/** The row of sixteen dots: 15 px each, 22 px apart, centred on the face, four pixels from the top. */
const DOTS = rect(355, 4, 570, 15);

export const porscheAnatomy: Anatomy = {
  sizes: [{ width: 1280, height: 480 }],
  regions: () => [
    // The car's dots sit on the glass with nothing behind them, so the well is only the room they
    // take, a few pixels either side, which is what the arrangement without them gives back.
    { role: 'revBarWell', rect: rect(DOTS.left - 10, 2, DOTS.width + 20, 19) },
    { role: 'revBar', rect: DOTS },
    // The top strip: the page name, the speed box, the lap and the track state.
    { role: 'bar', rect: rect(0, 28, 1280, 60) },
    { role: 'zone', zone: 'B', rect: rect(167, 100, 307, 218) },
    { role: 'zone', zone: 'A', rect: rect(486, 100, 277, 218) },
    { role: 'zone', zone: 'C', rect: rect(774, 100, 342, 218) },
    // The foot: the badge's place, the TC and ABS boxes, the tyre box and the brake bias.
    { role: 'band', rect: rect(0, 330, 1280, 142) },
    // The house's pit family keeps its banner at the top of the gear, inside the tile.
    { role: 'pitAlert', rect: rect(489, 103, 271, 30) },
    { role: 'hero', rect: rect(486, 100, 277, 218) },
    // The body between the settings column and the telltales, zones B, A and C with their panels,
    // which the full-screen flag takes as the limiter does.
    { role: 'flagBody', rect: rect(163, 96, 957, 226) },
  ],
};
