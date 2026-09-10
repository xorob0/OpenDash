/**
 * Card 7, Fuel laps: SimHub's computed laps of fuel, one decimal. Red below one lap; `-.-` in
 * text.dim, like every other no-data glyph, when SimHub has no estimate (null or zero).
 */
import { ncalc } from '../generator.ts';
import { readout } from '../components/readout.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { FUEL_LAPS_CHARS } from './chars.ts';

const { computed, isnull, le, lt, gt, and, num, iff, str, fmt } = ncalc;

export const fuelLaps = defineCard('fuelLaps', (slot, rung, prefix, meta) => {
  const laps = isnull(computed('Fuel_RemainingLaps'), num(0));
  const noData = le(laps, num(0));
  const low = and(gt(laps, num(0)), lt(laps, num(1)));
  return readout(
    slot,
    rung,
    prefix,
    { text: meta.label },
    {
      sample: '11.2',
      bind: iff(noData, str('-.-'), fmt(laps, '0.0')),
      chars: FUEL_LAPS_CHARS,
      color: ds.purpose.fuel.nominal,
      colorBind: iff(noData, str(ds.color.text.dim), iff(low, str(ds.purpose.fuel.low), str(ds.purpose.fuel.nominal))),
    },
  );
});
