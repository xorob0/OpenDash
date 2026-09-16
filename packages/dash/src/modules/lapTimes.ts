/**
 * Module 1, Lap times: the three times a driver compares, then the lap count, the estimate and
 * the live delta, then the five-lap average, the position and the stint lap, then the three
 * sectors of the last lap.
 *
 * The session best is the best lap of the car holding it, which SimHub can name; it is always
 * purple, because purple means "the best anyone has done" everywhere in this design.
 *
 * Twelve fields is the companion artboard's drawing and four ranks is what a 480 px page holds.
 * The zone catalogue draws nine of them and the narrow zones fewer still, so the last two ranks
 * are declared at `wide` alone and the height of the box takes off what it cannot hold.
 */
import { ncalc } from '../generator.ts';
import { ds } from '../tokens.ts';
import { stack } from '../second/layout.ts';
import { densityOf } from '../second/density.ts';
import { SECTORS, sectorColour } from '../second/sectors.ts';
import {
  CHARS,
  average5,
  bestLap,
  carPosition,
  currentLap,
  deltaColour,
  estimatedLap,
  fieldSize,
  lapTime,
  lastLap,
  player,
  referenceDelta,
  sectorLast,
  sectorTime,
  sessionBestLap,
} from '../second/values.ts';
import { defineModule, fieldsRow, fld } from './module.ts';

const { fmt, signed, concat, str, isnull, num, driver } = ncalc;

/** The sector times of the last lap, `ss.mm`: two decimals and no minutes, as the artboard draws. */
const SECTOR_SAMPLES = ['28.41', '41.07', '32.83'] as const;

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
      fieldsRow(
        [
          fld(ctx, 'average5', 'Average 5', { sample: '1:43.055', bind: average5(), chars: CHARS.lapTime, fs: d.mid }),
          fld(ctx, 'position', 'Position', {
            sample: '4',
            bind: fmt(carPosition(player()), '0'),
            chars: CHARS.position,
            fs: d.mid,
            // The field total is a proportion of the position beside it rather than a label size:
            // 32 px beside 46 on the companion and 23 beside 34 in a zone, which is what the
            // denominator follower already computes.
            follower: { kind: 'denominator', text: '/ 24', bind: concat(str('/ '), fmt(fieldSize(), '0')) },
          }),
          fld(ctx, 'stintLap', 'Stint lap', { sample: '12', bind: fmt(isnull(driver('lapsdonesincelastpitout', player()), num(0)), '0'), chars: CHARS.position, fs: d.mid }),
        ],
        ctx,
      ),
      fieldsRow(
        SECTORS.map((sector) =>
          fld(ctx, `s${sector}`, `Sector ${sector}`, {
            sample: SECTOR_SAMPLES[sector - 1] ?? '0.00',
            bind: sectorTime(sectorLast(sector), 2),
            chars: CHARS.sector,
            fs: d.small,
            colorBind: sectorColour(sector),
          }),
        ),
        ctx,
      ),
    ],
    ctx.density,
  );
});
