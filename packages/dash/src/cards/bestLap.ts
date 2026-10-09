/**
 * Card 2, Best lap: the player's own best lap of the session as m:ss.fff, dim glyphs when there is
 * none. `BestLapTime` is the player's best and not the field's; the field's is `sessionBestLap()` in
 * `second/values.ts`, which is a different number and follows `PositionMode`.
 */
import { ncalc } from '../generator.ts';
import { readout } from '../components/readout.ts';
import { ds } from '../tokens.ts';
import { NO_TIME, hasTime } from '../second/values.ts';
import { defineCard } from './card.ts';
import { LAP_TIME_CHARS } from './chars.ts';

const { game, not, iff, str, toShortTime } = ncalc;

export const bestLap = defineCard('bestLap', (slot, rung, prefix, meta) => {
  const best = game('BestLapTime');
  const noData = not(hasTime(best));
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
