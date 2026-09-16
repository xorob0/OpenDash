/** Card 2, Best lap: the session best as m:ss.fff, dim glyphs when there is none. */
import { ncalc } from '../generator.ts';
import { readout } from '../components/readout.ts';
import { ds } from '../tokens.ts';
import { NO_TIME } from '../second/values.ts';
import { defineCard } from './card.ts';
import { LAP_TIME_CHARS } from './chars.ts';

const { game, timespanToSeconds, le, num, iff, str, toShortTime } = ncalc;

export const bestLap = defineCard('bestLap', (slot, rung, prefix, meta) => {
  const best = game('BestLapTime');
  const noData = le(timespanToSeconds(best), num(0));
  return readout(
    slot,
    rung,
    prefix,
    { text: meta.label },
    {
      sample: '1:41.877',
      bind: iff(noData, str(NO_TIME), toShortTime(best, 3, false, true)),
      chars: LAP_TIME_CHARS,
      color: ds.purpose.lap.nominal,
      colorBind: iff(noData, str(ds.purpose.lap.noData), str(ds.purpose.lap.nominal)),
    },
  );
});
