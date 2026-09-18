/**
 * Card 3, Delta: the live delta to the session best or the all-time best per OpenDash.DeltaReference,
 * signed with two decimals, green when faster, red when slower, white within 5 ms.
 */
import { ncalc } from '../generator.ts';
import { readout } from '../components/readout.ts';
import { setting } from '../contract.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { DELTA_CHARS } from './chars.ts';

const { prop, eq, lt, le, abs, num, iff, str, signed, isnull } = ncalc;

/** Deltas within this many seconds of zero are drawn in delta.zero. */
export const DELTA_DEADBAND = 0.005;

export const SESSION_DELTA = 'PersistantTrackerPlugin.SessionBestLiveDeltaSeconds';
export const ALLTIME_DELTA = 'PersistantTrackerPlugin.AllTimeBestLiveDeltaSeconds';

/** The delta the card reads, per the setting. */
export const deltaSeconds = (): string => iff(eq(setting.deltaReference(), str('alltime')), prop(ALLTIME_DELTA), prop(SESSION_DELTA));

export const delta = defineCard('delta', (slot, rung, prefix, meta) => {
  const safe = isnull(deltaSeconds(), num(0));
  // One predicate behind both the text and the colour. A forced sign wrote "+0.00" for a delta the
  // deadband had already called level, so the card drew a plus in the resting white; the canvas
  // draws a bare "0.00" there, and the two cannot disagree while they are read off the same test.
  const resting = le(abs(safe), num(DELTA_DEADBAND));
  return readout(
    slot,
    rung,
    prefix,
    { text: meta.label },
    {
      sample: '−0.21',
      bind: iff(resting, str('0.00'), signed(safe, '0.00')),
      chars: DELTA_CHARS,
      color: ds.purpose.delta.zero,
      colorBind: iff(resting, str(ds.purpose.delta.zero), iff(lt(safe, num(0)), str(ds.purpose.delta.faster), str(ds.purpose.delta.slower))),
    },
  );
});
