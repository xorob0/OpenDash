/**
 * Card 4, Position: overall or class position per OpenDash.PositionMode, with the car count as
 * a denominator whose Left follows what the position draws.
 */
import { ncalc } from '../generator.ts';
import { readoutRow } from '../components/readoutRow.ts';
import { setting } from '../contract.ts';
import { drawnEither, drawnFigure, drawnText } from '../second/drawn.ts';
import { NO_VALUE } from '../second/values.ts';
import { defineCard } from './card.ts';
import { POSITION_CHARS } from './chars.ts';

const { game, eq, gt, str, iff, isnull, fmt, concat, add, num } = ncalc;

/** NCalc has no class position property; this asks the leaderboard for the player's PositionInClass. */
export const CLASS_POSITION = 'driverclassposition(getplayerleaderboardposition())';

/**
 * The place reads `--` until the sim has placed the car, as every other position OpenDash draws
 * does: SimHub reports a zero before then, on the grid and in a practice session before anyone has
 * a time, and the card drew it as `0 / 24` (#931). The denominator follows whichever of the two is
 * on the screen, the placeholder being two digit cells where the zero was one.
 */
export const position = defineCard('position', (slot, rung, prefix, meta) => {
  const byClass = eq(setting.positionMode(), str('class'));
  const overall = game('Position');
  const pos = iff(byClass, isnull(CLASS_POSITION, overall), overall);
  const placed = gt(isnull(pos, num(0)), num(0));
  const drawn = drawnEither(placed, drawnFigure({ value: pos, digits: POSITION_CHARS.digits }), drawnText(NO_VALUE));
  const count = iff(byClass, game('PlayerClassOpponentsCount'), game('OpponentsCount'));
  return readoutRow(
    slot,
    rung,
    prefix,
    { text: meta.label },
    { sample: '3', bind: iff(placed, fmt(pos, '0'), str(NO_VALUE)), chars: POSITION_CHARS },
    {
      kind: 'denominator',
      sample: '/ 24',
      bind: concat(str('/ '), fmt(count, '0')),
      after: { digits: 1, specials: 0 },
      leftBind: ({ x, mono, gap }) => add(num(x), drawn(mono), num(gap)),
    },
  );
});
