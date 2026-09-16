/** Card 12, Speed: the rounded speed with SimHub's unit label following it. */
import { ncalc } from '../generator.ts';
import { readoutRow } from '../components/readoutRow.ts';
import { defineCard } from './card.ts';
import { SPEED_CHARS, SPEED_DIGITS } from './chars.ts';

const { game, eq, str, iff, fmt, add, mul, num, digitCount } = ncalc;

export const speed = defineCard('speed', (slot, rung, prefix, meta) => {
  const s = game('SpeedLocal');
  return readoutRow(
    slot,
    rung,
    prefix,
    { text: meta.label },
    { sample: '187', bind: fmt(s, '0'), chars: SPEED_CHARS },
    {
      kind: 'unit',
      // Lower case, as the canvas and the speedo module both draw it: a unit is not a field label.
      sample: 'km/h',
      bind: iff(eq(game('SpeedLocalUnit'), str('MPH')), str('mph'), str('km/h')),
      after: SPEED_CHARS,
      // x + digits * digit cell + gap
      leftBind: ({ x, mono, gap }) => add(num(x), mul(digitCount(s, SPEED_DIGITS), num(mono.charWidth)), num(gap)),
    },
  );
});
