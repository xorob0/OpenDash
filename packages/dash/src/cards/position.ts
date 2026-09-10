/**
 * Card 4, Position: overall or class position per OpenDash.PositionMode, with the car count as
 * a denominator whose Left follows the position's digit count.
 */
import { ncalc } from '../generator.ts';
import { readoutRow } from '../components/readoutRow.ts';
import { setting } from '../contract.ts';
import { defineCard } from './card.ts';
import { POSITION_CHARS } from './chars.ts';

const { game, eq, str, iff, isnull, fmt, concat, add, mul, num, digitCount } = ncalc;

/** NCalc has no class position property; this asks the leaderboard for the player's PositionInClass. */
export const CLASS_POSITION = 'driverclassposition(getplayerleaderboardposition())';

export const position = defineCard('position', (slot, rung, prefix, meta) => {
  const byClass = eq(setting.positionMode(), str('class'));
  const overall = game('Position');
  const pos = iff(byClass, isnull(CLASS_POSITION, overall), overall);
  const count = iff(byClass, game('PlayerClassOpponentsCount'), game('OpponentsCount'));
  return readoutRow(
    slot,
    rung,
    prefix,
    { text: meta.label },
    { sample: '3', bind: fmt(pos, '0'), chars: POSITION_CHARS },
    {
      kind: 'denominator',
      sample: '/ 24',
      bind: concat(str('/ '), fmt(count, '0')),
      after: { digits: 1, specials: 0 },
      leftBind: ({ x, mono, gap }) => add(num(x), mul(digitCount(pos, POSITION_CHARS.digits), num(mono.charWidth)), num(gap)),
    },
  );
});
