/**
 * Module 12, Radar: SimHub's proximity radar with the spotter either side of it.
 *
 * The radar draws the player at the centre and the cars around from the recorded track map, so it
 * needs a lap on the track before it shows anything. The two blocks beside it come from SimHub's
 * own spotter and turn red when a car is alongside.
 *
 * The grid the canvas draws under the cars is four rectangles of this module's own: the radar item
 * paints no lines and takes no grid setting, and its background is transparent, so they are drawn
 * behind it.
 *
 * Three of the canvas's marks are outside what `RadarItem` will draw, and each is answered as far
 * as the format allows rather than dropped:
 *
 *   - The player's car is a 24 by 44 block. The item always draws the player at its own centre, so
 *     a rectangle over that centre states it exactly; the player's dot is set to radius 0 and the
 *     block takes its place.
 *   - The other cars are the same block, outlined, and they stay dots because a dot is the only
 *     mark the item draws for them. What they can take is the canvas's fill and stroke, as the
 *     dot's own border, which leaves the shape wrong and the colours right.
 *   - The red outline on the car alongside has no equivalent at all. The item styles every
 *     opponent alike and takes no per-car condition, so the state lives in the spotter flanks
 *     beside the plot, which are bars rather than the canvas's arrows.
 *
 * `docs/second-screens.md` records these, and what the item would need to do better.
 */
import { ncalc } from '../generator.ts';
import { withMoreBindings } from '../bind.ts';
import { assetBox } from '../design/assets.ts';
import { rect, type Rect, type Size } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { densityOf } from '../second/density.ts';
import type { Item, RadarItem } from '../generator.ts';
import { ds } from '../tokens.ts';
import { defineModule } from './module.ts';

const { game, gt, num, isnull, iff, str } = ncalc;

/**
 * Pixels per metre are ten times the scale, so the scale is how large the car beside you is drawn
 * and how much track the plot covers.
 *
 * Cut from the plot rather than fixed per density, which is rule 18: a literal 0.8 drew the same
 * twenty metres into a 158 px nano zone as into a 510 px tall one, so the box changed and what it
 * showed did not. The divisor puts the companion's own plot back at the 1.25 it was drawn at, and
 * the two bounds are where a radar stops being readable at either end.
 *
 * The divisor was 260, fitted against a companion page 336 px tall, and that page was the one the
 * build produced while the flag band was wrongly taking the 32 px `heightSm` token instead of the
 * artboard's 12. The band is twelve now and the page is 356, at which 260 gives 1.37, a hair under
 * the ceiling and well past the figure the sheet draws. 285 is what returns the page that exists to
 * the canvas's 1.25. The three shortest boxes -- the race zone, the nano and the 600 x 686 strip --
 * come to rest on the 0.55 floor rather than a little above it, which is the floor doing its work:
 * a plot that short has reached the most track it can show and stay readable.
 */
export const radarScaleFor = (plot: Rect): number => Math.max(0.55, Math.min(1.4, Math.round((100 * Math.min(plot.width, plot.height)) / 285) / 100));

/**
 * The spotter flank: a proportion of the frame's width, bounded, with the canvas's own gap beside
 * it. The canvas draws it 64 px wide on the 802 px companion page and 46 on a 607 px zone, which
 * is about a twelfth of the width in both.
 */
export const spotterWidthFor = (width: number): number => Math.max(12, Math.min(64, Math.round(width / 12)));

/**
 * The car the canvas draws, which is what the player's block is cut from.
 *
 * It is a size and not a proportion of the plot because the cars around it are dots of a fixed
 * radius: a block that grew with the box would leave the field it stands in. A plot too small to
 * hold it gets it smaller at this ratio, which is rule 18, and never larger than this.
 */
const CAR: Size = { width: 24, height: 44 };

const spotterOn = (side: 'Left' | 'Right') => gt(isnull(game(`SpotterCar${side}`), num(0)), num(0));

export const radar = defineModule('radar', (ctx) => {
  const d = densityOf(ctx.density);
  const height = ctx.frame.height;
  const spotter = spotterWidthFor(ctx.frame.width);
  const plot = rect(ctx.frame.left + spotter + d.gapX, ctx.frame.top, Math.max(0, ctx.frame.width - 2 * (spotter + d.gapX)), height);
  const blockHeight = Math.round(height * 0.5);
  const blockTop = ctx.frame.top + Math.round((height - blockHeight) / 2);
  const item: RadarItem = {
    kind: 'radar',
    name: `${ctx.prefix}radar`,
    rect: plot,
    scale: radarScaleFor(plot),
    useSmoothedPlayerAngle: true,
    // Every colour of both styles is named even where nothing is drawn in it: a key left unset is
    // filled by the serialiser from SimHub's own initialisers, and those are literals that never
    // passed through design/tokens.json. The player's dot has radius 0, so this border is the
    // ground colour of a mark that is never drawn.
    playerStyle: { dotColor: ds.color.text.primary, dotRadius: 0, dotBorderColor: ds.color.surface.base, labelFontSize: 1, labelColor: ds.color.surface.base },
    opponentStyle: {
      dotColor: ds.color.surface.raised,
      dotRadius: CAR.width / 2,
      dotBorderThickness: 1,
      dotBorderColor: ds.color.text.dim,
      labelFontSize: 1,
      labelColor: ds.color.surface.base,
    },
  };
  // The canvas's three horizontals and its centre line, at the quarters of the plot: what they say
  // is how far away a dot is, which is the one thing a bare field of dots does not.
  const grid: Item[] = [0.25, 0.5, 0.75].map((fraction, i) =>
    band(`${ctx.prefix}grid${i}`, rect(plot.left, Math.round(plot.top + fraction * plot.height), plot.width, 1), ds.color.surface.raised),
  );
  grid.push(band(`${ctx.prefix}centre`, rect(Math.round(plot.left + plot.width / 2), plot.top, 1, plot.height), ds.color.surface.raised));
  const you = band(`${ctx.prefix}you`, assetBox(plot, CAR, { maxWidth: CAR.width, maxHeight: CAR.height }), ds.color.text.primary);
  const side = (id: 'left' | 'right', x: number): Item => withMoreBindings(band(`${ctx.prefix}${id}`, rect(x, blockTop, spotter, blockHeight), ds.color.text.dim), { BackgroundColor: iff(spotterOn(id === 'left' ? 'Left' : 'Right'), str(ds.purpose.delta.slower), str(ds.color.text.dim)) });
  return [side('left', ctx.frame.left), ...grid, item, you, side('right', ctx.frame.left + ctx.frame.width - spotter)];
});
