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

const { prop, eq, lt, gt, num, iff, str, fmt, isnull, isNull } = ncalc;

/** Deltas within this many seconds of zero are drawn in delta.zero. */
export const DELTA_DEADBAND = 0.005;

export const SESSION_DELTA = 'PersistantTrackerPlugin.SessionBestLiveDeltaSeconds';
export const ALLTIME_DELTA = 'PersistantTrackerPlugin.AllTimeBestLiveDeltaSeconds';

/** The delta the card reads, per the setting. */
export const deltaSeconds = (): string => iff(eq(setting.deltaReference(), str('alltime')), prop(ALLTIME_DELTA), prop(SESSION_DELTA));

export const delta = defineCard('delta', (slot, rung, prefix, meta) => {
  const d = deltaSeconds();
  const safe = isnull(d, num(0));
  return readout(
    slot,
    rung,
    prefix,
    { text: meta.label },
    {
      sample: '-0.21',
      bind: iff(isNull(d), str('+0.00'), fmt(d, '0.00', true)),
      chars: DELTA_CHARS,
      color: ds.purpose.delta.zero,
      colorBind: iff(lt(safe, num(-DELTA_DEADBAND)), str(ds.purpose.delta.faster), iff(gt(safe, num(DELTA_DEADBAND)), str(ds.purpose.delta.slower), str(ds.purpose.delta.zero))),
    },
  );
});
