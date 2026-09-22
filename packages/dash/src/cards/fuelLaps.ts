/**
 * Card 7, Fuel laps: SimHub's computed laps of fuel, one decimal. Red below one lap; `--` in
 * text.dim, the no-data glyph of every other card, until a lap has said what one costs.
 */
import { ncalc } from '../generator.ts';
import { readout } from '../components/readout.ts';
import { fuelIsSettled, NO_VALUE } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { FUEL_LAPS_CHARS } from './chars.ts';

const { computed, isnull, lt, num, iff, str, fmt } = ncalc;

export const fuelLaps = defineCard('fuelLaps', (slot, rung, prefix, meta) => {
  const laps = isnull(computed('Fuel_RemainingLaps'), num(0));
  // The question band D's fuel page and the fuel module ask of this same property, so that the main
  // face and the second screen answer it alike: SimHub extrapolates the lap in progress before the
  // first crossing and publishes an estimate that moves every frame, which this card used to name
  // while the two faces beside it drew their absence. #382. A settled estimate of zero is an empty
  // tank rather than a missing reading, and it is drawn, in the red a tank under a lap earns.
  const settled = fuelIsSettled();
  const low = lt(laps, num(1));
  return readout(
    slot,
    rung,
    prefix,
    { text: meta.label },
    {
      sample: '11.2',
      bind: iff(settled, fmt(laps, '0.0'), str(NO_VALUE)),
      chars: FUEL_LAPS_CHARS,
      color: ds.purpose.fuel.nominal,
      colorBind: iff(settled, iff(low, str(ds.purpose.fuel.low), str(ds.purpose.fuel.nominal)), str(ds.color.text.dim)),
    },
  );
});
