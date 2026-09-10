/** Card 1, Last lap: m:ss.fff, purple when it equals the session best, dim glyphs when there is none. */
import { ncalc } from '../generator.ts';
import { readout } from '../components/readout.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { LAP_TIME_CHARS } from './chars.ts';

const { game, timespanToSeconds, le, lt, abs, sub, num, iff, str, toShortTime } = ncalc;

/** Two lap times closer than this are the same lap. */
export const SAME_LAP_EPSILON = 0.0005;

export const lastLap = defineCard('lastLap', (slot, rung, prefix, meta) => {
  const last = game('LastLapTime');
  const seconds = timespanToSeconds(last);
  const noData = le(seconds, num(0));
  const isSessionBest = lt(abs(sub(seconds, timespanToSeconds(game('BestLapTime')))), num(SAME_LAP_EPSILON));
  return readout(
    slot,
    rung,
    prefix,
    { text: meta.label },
    {
      sample: '1:42.905',
      bind: iff(noData, str('-:--.---'), toShortTime(last, 3, false, true)),
      chars: LAP_TIME_CHARS,
      color: ds.purpose.lap.nominal,
      colorBind: iff(noData, str(ds.purpose.lap.noData), iff(isSessionBest, str(ds.purpose.lap.sessionBest), str(ds.purpose.lap.nominal))),
    },
  );
});
