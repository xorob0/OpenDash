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
 */
import { ncalc } from '../generator.ts';
import { readout } from '../components/readout.ts';
import { setting } from '../contract.ts';
import { CHARS, REFERENCE_DELTA_WIDEST, referenceDelta, referenceDeltaColour, referenceDeltaLevel, referenceDeltaText } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';

const { iff, str } = ncalc;

export const delta = defineCard('delta', (slot, rung, prefix, meta) => {
  // Null-safe already: each of the three readings defaults to zero where its property is absent.
  const safe = referenceDelta();
  // One predicate behind both the text and the colour. A forced sign wrote "+0.00" for a delta the
  // band had already called level, so the card drew a plus in the resting white; the canvas draws a
  // bare "0.00" there, and the two cannot disagree while they are read off the same test. The band
  // is half a unit of the last place drawn, so it narrows to 0.0005 when the card draws thousandths
  // and the resting figure gains the third zero with it.
  const resting = referenceDeltaLevel(safe);
  return readout(
    slot,
    rung,
    prefix,
    { text: meta.label },
    {
      sample: '−0.21',
      // The box is cut for three places whichever is drawn, and measured by the longest of them.
      widest: REFERENCE_DELTA_WIDEST,
      bind: iff(resting, iff(setting.deltaPrecisionIs('thousandths'), str('0.000'), str('0.00')), referenceDeltaText(safe)),
      chars: CHARS.referenceDelta,
      color: ds.purpose.delta.zero,
      colorBind: referenceDeltaColour(safe),
    },
  );
});
