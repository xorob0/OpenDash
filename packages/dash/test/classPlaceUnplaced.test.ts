/**
 * A class place SimHub made up before the sim placed anyone, read on every surface that draws one
 * (#1014).
 *
 * SimHub never reports a class place of 0 for a car on the leaderboard. `GameManagerBase` in 9.12.6
 * numbers `PositionInClass` itself, from 1 within each class over every opponent, ordered by the
 * overall `Position` with a 0 sorted last, so before iRacing has placed anyone each car's class
 * place is its order in the driver list within its class, while its overall `Position` is 0. A guard
 * that asked the class place whether the car was placed never said no, and under the default
 * `PositionMode` the bar read `GT3 · P12`, the Position card `12 / 12`, the blue band
 * `BLUE · P11 GT4` and the relative a column of P1 to P12, where counting overall drew `--`.
 *
 * The frame below is that grid, for one car that is at once the player and the car behind: its
 * overall place is 0 and SimHub's class place for it is 7. Each surface is evaluated on it, and on
 * the same car once placed, where it reads the class place it always did.
 */
import { describe, expect, test } from 'bun:test';
import { CARDS } from '../src/cards/index.ts';
import { rect } from '../src/design/geometry.ts';
import type { TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { sessionPanel } from '../src/screens/pitwall.ts';
import { carBehindPositionClass, carRankChange, classAndPlace, hasPosition, NO_VALUE, player, positionDigits, positionLabelled } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { BAR_FIELD_SPECS } from '../src/zones/bar.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

/** The leaderboard row of the car every read below is about. */
const ROW = 3;

/** One frame: the car's overall place, SimHub's class place for it, and the rig's PositionMode. */
const frame = (overall: number, inClass: number, mode: 'class' | 'overall' = 'class'): Props => ({
  'OpenDash.PositionMode': mode,
  'getplayerleaderboardposition()': ROW,
  'getopponentleaderboardposition_aheadbehind(1)': ROW,
  [`driveravailable(${ROW})`]: true,
  [`drivercarclass(${ROW})`]: 'GT3',
  [`driverposition(${ROW})`]: overall,
  [`driverclassposition(${ROW})`]: inClass,
  'DataCorePlugin.GameData.Position': overall,
  'DataCorePlugin.GameData.OpponentsCount': 24,
  'DataCorePlugin.GameData.PlayerClassOpponentsCount': 12,
});

/** The grid: nobody placed, and SimHub's class place of 7 all the same. */
const GRID = frame(0, 7);
/** The same car placed nineteenth overall and seventh in its class. */
const PLACED = frame(19, 7);

const formula = (item: TextItem | undefined, target: 'Text' | 'Left' = 'Text'): string => {
  const bound = item?.bindings?.[target]?.formula;
  if (typeof bound !== 'string') throw new Error(`${item?.name ?? 'the item'} has no ${target} formula`);
  return bound;
};
const textNamed = (items: Iterable<{ kind: string; name: string }>, name: string): TextItem | undefined =>
  [...items].find((i): i is TextItem => i.kind === 'text' && i.name === name);

describe('the guard', () => {
  test('a car with an overall place of 0 is not placed, whatever its class place, in either mode', () => {
    expect(evalNcalc(hasPosition(player()), GRID)).toBe(false);
    expect(evalNcalc(hasPosition(player()), frame(0, 7, 'overall'))).toBe(false);
  });

  test('a car placed overall is placed in either mode', () => {
    expect(evalNcalc(hasPosition(player()), PLACED)).toBe(true);
    expect(evalNcalc(hasPosition(player()), frame(19, 7, 'overall'))).toBe(true);
  });
});

describe('every drawing of a class place, counting in class', () => {
  test('the position, which every zone, the relative and the leaderboards draw', () => {
    // The relative's and the leaderboards' rows bind `positionLabelled` of their row, and the
    // session page, the bar and the pit wall `positionDigits` of the player.
    expect([evalNcalc(positionDigits(player()), GRID), evalNcalc(positionLabelled(player()), GRID)]).toEqual([NO_VALUE, `P${NO_VALUE}`]);
    expect([evalNcalc(positionDigits(player()), PLACED), evalNcalc(positionLabelled(player()), PLACED)]).toEqual(['7', 'P7']);
  });

  test('the class readings of the bar, the session page and the pit wall', () => {
    const bar = BAR_FIELD_SPECS.find((f) => f.id === 'classPosition')!.bind as string;
    const session = formula(textNamed(walkItems(MODULES.find((m) => m.id === 'session')!.build({ frame: rect(0, 0, 769, 358), density: 'companion', prefix: '' })), 'class.value'));
    const pitWall = formula(textNamed(walkItems(sessionPanel('session', rect(0, 0, 600, 160))), 'session.class.value'));
    for (const [surface, bound] of Object.entries({ bar, session, pitWall })) {
      expect({ surface, grid: evalNcalc(bound, GRID), placed: evalNcalc(bound, PLACED) }).toEqual({ surface, grid: `GT3 · P${NO_VALUE}`, placed: 'GT3 · P7' });
    }
    // The pair is the class whatever the mode, and the guard is the same counting overall.
    expect(evalNcalc(classAndPlace(player()), frame(0, 7, 'overall'))).toBe(`GT3 · P${NO_VALUE}`);
  });

  test('the places gained beside the position, which the leaderboards\' rank column draws', () => {
    // SimHub takes the class start from the class place it numbered the first frame it saw the
    // car and counts against it whenever the class place is above 0, which it always is: a car
    // first in its class in the driver list and fourth once three others set times reads three
    // places lost. Overall, `PositionGain` stays null until the car is placed.
    const moved = (overall: number, mode: 'class' | 'overall' = 'class', field = 24): Props => ({
      ...frame(overall, 4, mode),
      'DataCorePlugin.GameData.OpponentsCount': field,
      [`driverpositiongainclass(${ROW})`]: -3,
      [`driverpositiongain(${ROW})`]: overall > 0 ? -2 : null,
    });
    expect(evalNcalc(carRankChange(player()), moved(0))).toBe(0);
    expect(evalNcalc(carRankChange(player()), moved(0, 'overall'))).toBe(0);
    expect(evalNcalc(carRankChange(player()), moved(19, 'overall'))).toBe(-2);
    // Placed, the class count is never read (#1022): a field of one class counts the overall
    // places, which are its class places, and a field of two counts nothing.
    expect(evalNcalc(carRankChange(player()), moved(19, 'class', 12))).toBe(-2);
    expect(evalNcalc(carRankChange(player()), moved(19))).toBe(0);
  });

  test('the blue flag band', () => {
    expect(evalNcalc(carBehindPositionClass(), GRID)).toBe(`P${NO_VALUE} GT3`);
    expect(evalNcalc(carBehindPositionClass(), PLACED)).toBe('P7 GT3');
  });

  test('the Position card, and the count after it', () => {
    const items = CARDS.find((c) => c.id === 'position')!.build(rect(0, 0, 255, 187), 'x.');
    const value = textNamed(items, 'x.value');
    const denominator = textNamed(items, 'x.denominator');
    expect(evalNcalc(formula(value), GRID)).toBe(NO_VALUE);
    expect(evalNcalc(formula(value), PLACED)).toBe('7');
    // The placeholder takes two digit cells, so the count stands where it does after a place of 12,
    // and not one cell in, where a class place of 7 would have put it.
    const left = (props: Props): unknown => evalNcalc(formula(denominator, 'Left'), props);
    expect(left(GRID)).toBe(left(frame(19, 12)));
    expect(left(GRID)).not.toBe(left(PLACED));
  });
});
