/**
 * Card 4, Position: overall or class position per OpenDash.PositionMode, with the car count as
 * a denominator whose Left follows what the position draws.
 *
 * The place and the field it is out of are `second/values.ts`'s, the same `positionDigits` and
 * `fieldSize` the bar, the session module and the pit wall draw. A grid before the green flag has
 * no positions, iRacing answering 0 for the player's place in both modes, and the card drew that
 * zero: a round face read `0 / 24` before every start where every zone read `-- / 24`. #992.
 */
import { ncalc } from '../generator.ts';
import { readoutRow } from '../components/readoutRow.ts';
import { drawnWithin } from '../second/drawn.ts';
import { fieldSize, player, positionDigits, positionDrawn } from '../second/values.ts';
import { defineCard } from './card.ts';
import { POSITION_CHARS } from './chars.ts';

const { str, fmt, concat, add, num } = ncalc;

export const position = defineCard('position', (slot, rung, prefix, meta) =>
  readoutRow(
    slot,
    rung,
    prefix,
    { text: meta.label },
    { sample: '3', bind: positionDigits(player()), chars: POSITION_CHARS },
    {
      kind: 'denominator',
      sample: '/ 24',
      bind: concat(str('/ '), fmt(fieldSize(), '0')),
      after: { digits: 1, specials: 0 },
      // `--` is two digit cells, so before the car is placed the denominator sits where it sits
      // after a two-digit place rather than one cell in, over the second dash.
      leftBind: ({ x, mono, gap }) => add(num(x), drawnWithin('the Position card', positionDrawn(player()), POSITION_CHARS, mono), num(gap)),
    },
  ),
);
