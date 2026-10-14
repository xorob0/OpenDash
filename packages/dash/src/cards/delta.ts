/**
 * Card 3, Delta: the live delta to the session best, the all-time best or the last lap per
 * OpenDash.DeltaReference, signed, to hundredths or thousandths per OpenDash.DeltaPrecision, green
 * when faster, red when slower, white while it is level.
 *
 * The reading is `referenceDelta()`, the one every second screen draws. The card used to carry a
 * switch of its own between the two plugin properties, which is a second place for a third reference
 * to be forgotten; one reading is what keeps the card and the delta module on the same lap. The
 * figure, its colour and the band inside which it is level are the second screens' too, for the same
 * reason: `referenceDeltaText`, `referenceDeltaColour` and `referenceDeltaLevel` in `second/values.ts`.
 * The bare `0.00` of a level delta is the shared text's as well, where it used to be a branch of the
 * card's own and the other four surfaces drew `+0.00` for the same reading (#614).
 */
import { readout } from '../components/readout.ts';
import { CHARS, REFERENCE_DELTA_WIDEST, referenceDelta, referenceDeltaColour, referenceDeltaText } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';

export const delta = defineCard('delta', (slot, rung, prefix, meta) => {
  // Null-safe already: each of the three readings defaults to zero where its property is absent.
  const safe = referenceDelta();
  return readout(
    slot,
    rung,
    prefix,
    { text: meta.label },
    {
      sample: '−0.21',
      // The box is cut for three places whichever is drawn, and measured by the longest of them.
      widest: REFERENCE_DELTA_WIDEST,
      bind: referenceDeltaText(safe),
      chars: CHARS.referenceDelta,
      color: ds.purpose.delta.zero,
      colorBind: referenceDeltaColour(safe),
    },
  );
});
