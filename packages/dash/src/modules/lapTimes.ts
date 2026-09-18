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
import { defineModule, fieldsRow, fld, shapeIn } from './module.ts';

const { fmt, signed, concat, str, isnull, num, driver } = ncalc;

/** The sector times of the last lap, `ss.mm`: two decimals and no minutes, as the artboard draws. */
const SECTOR_SAMPLES = ['28.41', '41.07', '32.83'] as const;

/**
 * The gaps the portrait companion asks for, neither of them on the `space` scale, so the literals
 * stay here with `Companion480x850.dc.html` as their citation: 28 between the five groups of the
 * column, and 40 between the laps and the estimate that share a line.
 */
const PORTRAIT_GROUP_GAP = 28;
const PORTRAIT_PAIR_GAP = 40;

export const lapTimes = defineModule('lapTimes', (ctx) => {
  const d = densityOf(ctx.density);
  const delta = referenceDelta();
  const shape = shapeIn(ctx);
  /**
   * The portrait companion, which is the one drawing of this page that leads with the last lap as
   * a hero, gives every time a line of its own and puts the delta below the laps rather than
   * beside them. The catalogue's `tall` zone drawing stacks the same three times at one size, so
   * the shape alone does not say which of the two is being drawn; the instrument does.
   */
  const portrait = ctx.density === 'companion' && shape.height === 'tall';
  /**
   * The catalogue's `grid` drawing, which is the one that gives the last lap a full-width line of
   * its own and sets the two bests beside each other under it. A greedy wrap cannot produce it: at
   * 430 px two lap times fit a line, so the most important of the three ends up sharing one.
   */
  const grid = shape.width === 'medium' && shape.height === 'medium';
  const last = fld(ctx, 'last', 'Last lap', { sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime, fs: portrait ? d.hero : d.big });
  const bests = [
    fld(ctx, 'sessionBest', 'Session best', { sample: '1:41.877', bind: lapTime(sessionBestLap()), chars: CHARS.lapTime, fs: d.big, color: ds.purpose.lap.sessionBest }),
    fld(ctx, 'yourBest', 'Your best', { sample: '1:42.311', bind: lapTime(bestLap()), chars: CHARS.lapTime, fs: d.big }),
  ];
  const laps = fld(ctx, 'laps', 'Laps', { sample: '12', bind: fmt(currentLap(), '0'), chars: CHARS.position, fs: d.mid });
  const estimated = fld(ctx, 'estimated', 'Estimated', { sample: '1:42.1', bind: lapTime(estimatedLap(), 1), chars: CHARS.lapTime, fs: d.mid });
  const toYourBest = fld(ctx, 'delta', 'Delta to your best', { sample: '\u22120.21', bind: signed(delta, '0.00'), chars: CHARS.delta, fs: d.mid, colorBind: deltaColour(delta) });
  /**
   * The companion draws each rank as three equal columns rather than as three fields of their own
   * widths. The artboard is explicit about it -- `minmax(0, 1fr)` three times, 24 px apart, which
   * is about 251 px each in an 802 px body -- and the reason is that the four ranks then line up
   * as a grid down the page: the estimate sits under the session best and the second sector under
   * the position, where content widths put each of them wherever its own digits ended.
   */
  const columns = ctx.density === 'companion' && !portrait ? ({ lines: 'grid', columns: 3 } as const) : {};
  return stack(
    ctx.frame,
    [
      ...(grid
        ? [fieldsRow([last], ctx), fieldsRow(bests, ctx, { lines: 'grid', columns: 2 })]
        : [fieldsRow([last, ...bests], ctx, portrait ? { lines: 'perLine', lineGap: PORTRAIT_GROUP_GAP } : columns)]),
      ...(portrait
        ? [fieldsRow([laps, estimated], ctx, { gap: PORTRAIT_PAIR_GAP, align: 'top' }), fieldsRow([toYourBest], ctx)]
        : [fieldsRow([laps, estimated, toYourBest], ctx, columns)]),
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
        columns,
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
        columns,
      ),
    ],
    ctx.density,
    // The catalogue spreads this page's ranks over the zone and centres them in a zone of one
    // column; the portrait companion centres its own column too, its artboard setting no height on
    // the group. Everywhere else the first rank sits on the top edge and the last on the bottom.
    { gap: portrait ? PORTRAIT_GROUP_GAP : undefined, justify: portrait || shape.width === 'narrow' ? 'centre' : 'spaceBetween' },
  );
});
