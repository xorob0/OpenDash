/**
 * Module 3, Sectors: the three sectors of the last lap with their deltas, a colour strip, the
 * three lap times they add up to, and the session's best of each sector.
 *
 * The sector rank is a row of fields rather than a drawing, so it is sized and shed by the stack
 * like every other rank: a box with height to spare grows it, and a box without one takes the
 * recap off underneath rather than shrinking the reading the page exists for.
 */
import { rect } from '../design/geometry.ts';
import { densityOf } from '../second/density.ts';
import { stack } from '../second/layout.ts';
import { SECTORS, sectorSpecs, sectorStrip } from '../second/sectors.ts';
import { CHARS, bestLap, lapTime, lastLap, sectorTime, sessionBestLap } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { ncalc } from '../generator.ts';
import { blockRow, defineModule, fieldsRow, fld, shapeIn } from './module.ts';
import { archetypeOf } from './shedding.ts';

/**
 * The gaps the canvas draws between the columns of this page: 20 between the sectors and 18
 * between the recaps under them, neither of them on the `space` scale.
 */
const SECTOR_GAP = 20;
const RECAP_GAP = 18;

const BEST_SAMPLES = ['28.12', '40.84', '32.52'] as const;

export const sectors = defineModule('sectors', (ctx) => {
  const d = densityOf(ctx.density);
  const shape = shapeIn(ctx);
  const stripHeight = ctx.density === 'companion' ? 10 : 6;
  // A zone one column wide labels its sectors "S1" and leaves the delta to the colour, which is
  // what the catalogue draws and what lets the rank keep the size the drawing gives it.
  const bare = shape.width === 'narrow';
  // The catalogue's tall drawing writes "Best" where the wider ones write "Session best": one
  // column has no room for two words, and the page is already about sectors rather than laps.
  const sessionBestLabel = archetypeOf(shape) === 'tall' ? 'Best' : 'Session best';
  return stack(
    ctx.frame,
    [
      fieldsRow(sectorSpecs(ctx.prefix, d.big, { bare }), ctx, SECTOR_GAP),
      blockRow(stripHeight, (bottom) => sectorStrip(`${ctx.prefix}strip`, rect(ctx.frame.left, bottom - stripHeight, ctx.frame.width, stripHeight))),
      fieldsRow(
        [
          fld(ctx, 'yourBest', 'Your best', { sample: '1:42.311', bind: lapTime(bestLap()), chars: CHARS.lapTime, fs: d.small }),
          fld(ctx, 'last', 'Last', { sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime, fs: d.small }),
          fld(ctx, 'sessionBest', sessionBestLabel, { sample: '1:41.877', bind: lapTime(sessionBestLap()), chars: CHARS.lapTime, fs: d.small, color: ds.purpose.lap.sessionBest }),
        ],
        ctx,
        RECAP_GAP,
      ),
      fieldsRow(
        SECTORS.map((sector) =>
          fld(ctx, `bestS${sector}`, `Best S${sector}`, {
            sample: BEST_SAMPLES[sector - 1] ?? '0.00',
            bind: sectorTime(ncalc.bestSplitTime(sector), 2),
            chars: CHARS.sector,
            fs: d.small,
            color: ds.purpose.lap.sessionBest,
          }),
        ),
        ctx,
        RECAP_GAP,
      ),
    ],
    ctx.density,
    // The catalogue spreads this page at every shape it draws it at: the sectors on the top edge
    // of the zone and the times they add up to on the bottom.
    { justify: 'spaceBetween' },
  );
});
