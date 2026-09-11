/**
 * Module 3, Sectors: the three sectors of the last lap with their deltas, a colour strip, and the
 * three lap times they add up to.
 */
import { rect } from '../design/geometry.ts';
import { densityOf } from '../second/density.ts';
import { stack } from '../second/layout.ts';
import { sectorFields, sectorStrip } from '../second/sectors.ts';
import { CHARS, bestLap, lapTime, lastLap, sessionBestLap } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { blockRow, defineModule, fieldsRow, fld } from './module.ts';

export const sectors = defineModule('sectors', (ctx) => {
  const d = densityOf(ctx.density);
  const stripHeight = ctx.density === 'companion' ? 10 : 6;
  const sectorRowHeight = d.label + d.fieldGap + d.big;
  return stack(
    ctx.frame,
    [
      blockRow(sectorRowHeight, (bottom) => sectorFields(`${ctx.prefix}sector`, rect(ctx.frame.left, bottom - sectorRowHeight, ctx.frame.width, sectorRowHeight), ctx.density)),
      blockRow(stripHeight, (bottom) => sectorStrip(`${ctx.prefix}strip`, rect(ctx.frame.left, bottom - stripHeight, ctx.frame.width, stripHeight))),
      fieldsRow(
        [
          fld(ctx, 'yourBest', 'Your best', { sample: '1:42.311', bind: lapTime(bestLap()), chars: CHARS.lapTime, fs: d.mid }),
          fld(ctx, 'last', 'Last', { sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime, fs: d.mid }),
          fld(ctx, 'sessionBest', 'Session best', { sample: '1:41.877', bind: lapTime(sessionBestLap()), chars: CHARS.lapTime, fs: d.mid, color: ds.purpose.lap.sessionBest }),
        ],
        ctx,
      ),
    ],
    ctx.density,
  );
});
