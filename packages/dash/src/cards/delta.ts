/**
 * Card 3, Delta: the live delta to the session best, the all-time best or the last lap per
 * OpenDash.DeltaReference, signed with two decimals, green when faster, red when slower, white
 * within 5 ms.
 *
 * The reading is `referenceDelta()`, the one every second screen draws. The card used to carry a
 * switch of its own between the two plugin properties, which is a second place for a third reference
 * to be forgotten; one reading is what keeps the card and the delta module on the same lap.
 */
import { ncalc } from '../generator.ts';
import { readout } from '../components/readout.ts';
import { referenceDelta } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { DELTA_CHARS } from './chars.ts';

const { lt, le, abs, num, iff, str, signed } = ncalc;

/** Deltas within this many seconds of zero are drawn in delta.zero. */
export const DELTA_DEADBAND = 0.005;

export const delta = defineCard('delta', (slot, rung, prefix, meta) => {
  // Null-safe already: each of the three readings defaults to zero where its property is absent.
  const safe = referenceDelta();
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
