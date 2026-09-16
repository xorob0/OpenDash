/** Card 0, Current lap: the running lap time as m:ss.f, no-data glyphs in lap.noData when there is no lap. */
import { ncalc } from '../generator.ts';
import { readout } from '../components/readout.ts';
import { ds } from '../tokens.ts';
import { noTime } from '../second/values.ts';
import { defineCard } from './card.ts';
import { CURRENT_LAP_CHARS } from './chars.ts';

const { game, timespanToSeconds, le, num, iff, str, toShortTime } = ncalc;

export const currentLap = defineCard('currentLap', (slot, rung, prefix, meta) => {
  const t = game('CurrentLapTime');
  const noData = le(timespanToSeconds(t), num(0));
  return readout(
    slot,
    rung,
    prefix,
    { text: meta.label },
    {
      sample: '1:42.3',
      bind: iff(noData, str(noTime(1)), toShortTime(t, 1, false, true)),
      chars: CURRENT_LAP_CHARS,
      color: ds.purpose.lap.nominal,
      colorBind: iff(noData, str(ds.purpose.lap.noData), str(ds.purpose.lap.nominal)),
    },
  );
});
