/** Card 6, Fuel: fuel remaining with one decimal in SimHub's unit, the unit label following the value. */
import { ncalc } from '../generator.ts';
import { readoutRow } from '../components/readoutRow.ts';
import { defineCard } from './card.ts';
import { FUEL_CHARS, FUEL_INT_DIGITS } from './chars.ts';

const { game, eq, str, iff, fmt, add, mul, num, digitCount, round } = ncalc;

export const fuel = defineCard('fuel', (slot, rung, prefix, meta) => {
  const f = game('Fuel');
  return readoutRow(
    slot,
    rung,
    prefix,
    { text: meta.label },
    { sample: '38.4', bind: fmt(f, '0.0'), chars: FUEL_CHARS },
    {
      kind: 'unit',
      sample: 'L',
      bind: iff(eq(game('FuelUnit'), str('Gallons')), str('GAL'), str('L')),
      after: { digits: FUEL_INT_DIGITS, specials: 1 },
      // x + (integer digits + 1 decimal) * digit cell + one '.' cell + gap
      leftBind: ({ x, mono, gap }) =>
        add(num(x), mul(add(digitCount(round(f, 1), FUEL_INT_DIGITS), num(1)), num(mono.charWidth)), num(mono.specialCharsWidth), num(gap)),
    },
  );
});
