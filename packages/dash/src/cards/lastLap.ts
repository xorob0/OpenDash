/**
 * Card 1, Last lap: m:ss.fff, and to hundredths from ten minutes (see `lapReading`), purple when it
 * is the session best (`sessionBestLap`), dim glyphs when there is none.
 *
 * Purple is the session best and nothing else, as the brand and Lap times have it: a lap that equals
 * only your own best (`BestLapTime`) while someone has gone faster is drawn in the nominal colour,
 * because Lap times draws the faster session best in purple beside it (#1042).
 */
import { ncalc } from '../generator.ts';
import { readout } from '../components/readout.ts';
import { ds } from '../tokens.ts';
import { LAP_TIME_WIDEST, lapReading, NO_TIME, hasTime, isSessionBestLap } from '../second/values.ts';
import { defineCard } from './card.ts';
import { LAP_TIME_CHARS } from './chars.ts';

const { game, not, iff, str } = ncalc;

export const lastLap = defineCard('lastLap', (slot, rung, prefix, meta) => {
  const last = game('LastLapTime');
  const noData = not(hasTime(last));
  return readout(
    slot,
    rung,
    prefix,
    { text: meta.label },
    {
      sample: '1:42.905',
      bind: iff(noData, str(NO_TIME), lapReading(last)),
      chars: LAP_TIME_CHARS,
      widest: LAP_TIME_WIDEST,
      color: ds.purpose.lap.nominal,
      colorBind: iff(noData, str(ds.purpose.lap.noData), iff(isSessionBestLap(last), str(ds.purpose.lap.sessionBest), str(ds.purpose.lap.nominal))),
    },
  );
});
