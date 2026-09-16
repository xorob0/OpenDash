/**
 * Module 1, Lap times: the three times a driver compares, then the lap count, the estimate and
 * the live delta.
 *
 * The session best is the best lap of the car holding it, which SimHub can name; it is always
 * purple, because purple means "the best anyone has done" everywhere in this design.
 */
import { ncalc } from '../generator.ts';
import { ds } from '../tokens.ts';
import { stack } from '../second/layout.ts';
import { densityOf } from '../second/density.ts';
import { CHARS, bestLap, currentLap, deltaColour, estimatedLap, lapTime, lastLap, referenceDelta, sessionBestLap } from '../second/values.ts';
import { defineModule, fieldsRow, fld } from './module.ts';

const { fmt, signed } = ncalc;

export const lapTimes = defineModule('lapTimes', (ctx) => {
  const d = densityOf(ctx.density);
  const delta = referenceDelta();
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          fld(ctx, 'last', 'Last lap', { sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime, fs: d.big }),
          fld(ctx, 'sessionBest', 'Session best', { sample: '1:41.877', bind: lapTime(sessionBestLap()), chars: CHARS.lapTime, fs: d.big, color: ds.purpose.lap.sessionBest }),
          fld(ctx, 'yourBest', 'Your best', { sample: '1:42.311', bind: lapTime(bestLap()), chars: CHARS.lapTime, fs: d.big }),
        ],
        ctx,
      ),
      fieldsRow(
        [
          fld(ctx, 'laps', 'Laps', { sample: '12', bind: fmt(currentLap(), '0'), chars: CHARS.position, fs: d.mid }),
          fld(ctx, 'estimated', 'Estimated', { sample: '1:42.1', bind: lapTime(estimatedLap(), 1), chars: CHARS.lapTime, fs: d.mid }),
          fld(ctx, 'delta', 'Delta to your best', { sample: '\u22120.21', bind: signed(delta, '0.00'), chars: CHARS.delta, fs: d.mid, colorBind: deltaColour(delta) }),
        ],
        ctx,
      ),
    ],
    ctx.density,
  );
});
