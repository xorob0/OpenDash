/**
 * The Position card on a grid the sim has not placed yet, read by evaluating the NCalc it emits.
 *
 * A grid before the green flag has no positions: iRacing answers 0 for the player's place overall
 * and in class until the race starts, and SimHub passes the zero on. Every zone surface draws `--`
 * there through `positionDigits`, and the card drew the zero, so a round face read `0 / 24` before
 * every start. #992.
 *
 * `ncalcEval.ts` knows no leaderboard function, so the two the card can ask about the player are
 * answered here by replacing their text with the frame's answer before it is evaluated, as
 * `splitList.test.ts` does with the player's row. Anything left over that is not in the evaluator's
 * subset throws, which is what keeps this honest about which reads the card makes.
 */
import { describe, expect, test } from 'bun:test';
import { position } from '../src/cards/position.ts';
import { rect } from '../src/design/geometry.ts';
import { NO_VALUE } from '../src/second/values.ts';
import type { TextItem } from '../src/generator.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const items = position.build(rect(0, 0, 255, 187), 'position.');
const item = (name: string): TextItem => {
  const found = items.find((i) => i.name === `position.${name}`);
  if (found?.kind !== 'text') throw new Error(`position.${name} is not a text item`);
  return found;
};

/** One frame: what SimHub publishes, and what its leaderboard answers about the player. */
interface Frame {
  mode: 'overall' | 'class';
  /** `getplayerleaderboardposition()`, -1 where the player has no row yet. */
  row: number;
  /** The player's place overall and in class, 0 before the green flag; null where there is no row. */
  overall: number | null;
  inClass: number | null;
}

const props = (frame: Frame): Props => ({
  'OpenDash.PositionMode': frame.mode,
  'DataCorePlugin.GameData.Position': frame.overall ?? 0,
  'DataCorePlugin.GameData.OpponentsCount': 24,
  'DataCorePlugin.GameData.PlayerClassOpponentsCount': 12,
});

const bound = (name: string, target: 'Text' | 'Left', frame: Frame): unknown => {
  const b = item(name).bindings?.[target];
  if (!b || typeof b.formula !== 'string') throw new Error(`position.${name} has no ${target} formula`);
  const answered = b.formula
    .replaceAll('driverposition(getplayerleaderboardposition())', String(frame.overall))
    .replaceAll('driverclassposition(getplayerleaderboardposition())', String(frame.inClass))
    .replaceAll('getplayerleaderboardposition()', String(frame.row));
  return evalNcalc(answered, props(frame));
};

/** What the card reads, and where the "/ 24" after it starts. */
const reading = (frame: Frame) => ({
  value: bound('value', 'Text', frame),
  denominator: bound('denominator', 'Text', frame),
  left: Number(bound('denominator', 'Left', frame)),
});

const MODES = ['overall', 'class'] as const;

/** The grid before the start: the player has a row, and both places are zero. */
const grid = (mode: Frame['mode']): Frame => ({ mode, row: 3, overall: 0, inClass: 0 });
/** A session the player has joined but the leaderboard has no row for yet. */
const noRow = (mode: Frame['mode']): Frame => ({ mode, row: -1, overall: null, inClass: null });
/** Placed: third overall, second in class, or twelfth and seventh. */
const placed = (mode: Frame['mode'], overall = 3, inClass = 2): Frame => ({ mode, row: overall, overall, inClass });

describe('the Position card before the sim has placed the car (#992)', () => {
  for (const mode of MODES) {
    test(`reads ${NO_VALUE} on a grid, counting ${mode}`, () => {
      expect({ mode, value: reading(grid(mode)).value }).toEqual({ mode, value: NO_VALUE });
    });

    test(`reads ${NO_VALUE} with no leaderboard row, counting ${mode}`, () => {
      expect({ mode, value: reading(noRow(mode)).value }).toEqual({ mode, value: NO_VALUE });
    });

    test(`reads the place once placed, counting ${mode}`, () => {
      expect({ mode, value: reading(placed(mode)).value }).toEqual({ mode, value: mode === 'class' ? '2' : '3' });
      expect({ mode, value: reading(placed(mode, 12, 7)).value }).toEqual({ mode, value: mode === 'class' ? '7' : '12' });
    });

    test(`is out of the field the place is counted in, counting ${mode}`, () => {
      expect(reading(placed(mode)).denominator).toBe(mode === 'class' ? '/ 12' : '/ 24');
    });

    test(`moves the "/ 24" with what is drawn, ${NO_VALUE} taking two cells, counting ${mode}`, () => {
      // `--` is two digit cells, so the denominator sits where it sits after a two-digit place and a
      // cell further out than after a one-digit place. Placed for the zero it was one cell in, over
      // the second dash.
      const one = reading(placed(mode, 3, 3)).left;
      const two = reading(placed(mode, 12, 12)).left;
      expect(two).toBeGreaterThan(one);
      expect({ mode, grid: reading(grid(mode)).left, noRow: reading(noRow(mode)).left }).toEqual({ mode, grid: two, noRow: two });
    });
  }
});
