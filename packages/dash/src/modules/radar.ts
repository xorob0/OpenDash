/**
 * Module 12, Radar: SimHub's proximity radar with the spotter either side of it.
 *
 * The radar draws the player at the centre and the cars around from the recorded track map, so it
 * needs a lap on the track before it shows anything. The two blocks beside it come from SimHub's
 * own spotter and turn red when a car is alongside.
 *
 * The grid the canvas draws under the cars is four rectangles of this module's own: the radar item
 * paints no lines and takes no grid setting, and its background is transparent, so they are drawn
 * behind it. The cars themselves are dots, and the spotter flanks are bars rather than the
 * canvas's arrows; `docs/second-screens.md` records both, and what the item would need to do
 * better.
 */
import { ncalc } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { rect, type Rect } from '../design/geometry.ts';
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
 */
export const radarScaleFor = (plot: Rect): number => Math.max(0.55, Math.min(1.4, Math.round((100 * Math.min(plot.width, plot.height)) / 260) / 100));

/**
 * The spotter flank: a proportion of the frame's width, bounded, with the canvas's own gap beside
 * it. The canvas draws it 64 px wide on the 802 px companion page and 46 on a 607 px zone, which
 * is about a twelfth of the width in both.
 */
export const spotterWidthFor = (width: number): number => Math.max(12, Math.min(64, Math.round(width / 12)));

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
    playerStyle: { dotColor: ds.color.text.primary, dotRadius: 14, labelFontSize: 1, labelColor: ds.color.surface.base },
    opponentStyle: { dotColor: ds.color.text.secondary, dotRadius: 12, labelFontSize: 1, labelColor: ds.color.surface.base },
  };
  // The canvas's three horizontals and its centre line, at the quarters of the plot: what they say
  // is how far away a dot is, which is the one thing a bare field of dots does not.
  const grid: Item[] = [0.25, 0.5, 0.75].map((fraction, i) =>
    band(`${ctx.prefix}grid${i}`, rect(plot.left, Math.round(plot.top + fraction * plot.height), plot.width, 1), ds.color.surface.raised),
  );
  grid.push(band(`${ctx.prefix}centre`, rect(Math.round(plot.left + plot.width / 2), plot.top, 1, plot.height), ds.color.surface.raised));
  const side = (id: 'left' | 'right', x: number): Item => ({
    ...band(`${ctx.prefix}${id}`, rect(x, blockTop, spotter, blockHeight), ds.color.text.dim),
    ...withBindings({ BackgroundColor: iff(spotterOn(id === 'left' ? 'Left' : 'Right'), str(ds.purpose.delta.slower), str(ds.color.text.dim)) }),
  });
  return [side('left', ctx.frame.left), ...grid, item, side('right', ctx.frame.left + ctx.frame.width - spotter)];
});
