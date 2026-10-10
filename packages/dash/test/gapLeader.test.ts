/**
 * The Gap and Int columns measured from the car leading the race, which SimHub gives no gap (#1041).
 *
 * Every gap SimHub publishes is measured from the first car of its leaderboard, and that car has no
 * gap of its own: `GameManagerBase.ComputeOpponentsData` in 9.12.6 calls `UpdateGapToLeader` for
 * every other car and sets the first car's `GaptoLeaderSimHub` to 0 only when it is the player. So
 * `drivergaptoleader(1)` is null whenever somebody else leads. The class column subtracted the class
 * leader's gap from the car's and drew `--` where either was null, and the Int column did the same
 * with the car in front and drew nothing. Counting in class, the default, every classmate of a race
 * leader read `--` down the Gap column, which is every car of a single-class race, and P2's Int was
 * empty counting either way.
 *
 * The frame below is the VM's `green` race in five cars, the overall leader a GT3 that is not the
 * player. The player is the GT3 third on the road, second in class. Two GT4s run among them.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import type { Item } from '../src/generator.ts';
import { ncalc } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { racePage } from '../src/screens/pitwall.ts';
import { carClassInterval, carClassRaceGap, carInterval, carRaceGap, NO_VALUE } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const { num } = ncalc;

interface Car {
  cls: 'GT3' | 'GT4';
  classPlace: number;
  /** SimHub's gap to the first car: null on that car's own row, as SimHub leaves it. */
  gap: number | null;
}

const FIELD: readonly Car[] = [
  { cls: 'GT3', classPlace: 1, gap: null },
  { cls: 'GT4', classPlace: 1, gap: 10.1 },
  { cls: 'GT3', classPlace: 2, gap: 14.5 },
  { cls: 'GT3', classPlace: 3, gap: 20.0 },
  { cls: 'GT4', classPlace: 2, gap: 25.3 },
];

/** The player's row, and the class the class-only lookup answers from. */
const PLAYER = 3;

const frame = (mode: 'class' | 'overall', field: readonly Car[] = FIELD, player = PLAYER, row = 1): Props => {
  const playerClass = field[player - 1]!.cls;
  const props: Props = {
    'OpenDash.PositionMode': mode,
    'DataCorePlugin.GameData.OpponentsCount': field.length,
    'DataCorePlugin.GameData.PlayerClassOpponentsCount': field.filter((car) => car.cls === playerClass).length,
    'getplayerleaderboardposition()': player,
    'repeatindex()': row,
  };
  let inClass = 0;
  field.forEach((car, i) => {
    const n = i + 1;
    props[`driveravailable(${n})`] = true;
    props[`drivercarclass(${n})`] = car.cls;
    props[`driverposition(${n})`] = n;
    props[`driverclassposition(${n})`] = car.classPlace;
    props[`drivergaptoleader(${n})`] = car.gap;
    props[`driverlapstoleader(${n})`] = n === 1 ? null : 0;
    props[`driverlapstoclassleader(${n})`] = car.classPlace === 1 ? null : 0;
    if (car.cls === playerClass) props[`getopponentleaderboardposition_playerclassonly(${++inClass})`] = n;
  });
  return props;
};

describe('a class whose leader leads the race', () => {
  test('each classmate reads its gap to the class leader in seconds', () => {
    expect([3, 4].map((idx) => evalNcalc(carClassRaceGap(num(idx)), frame('class')))).toEqual(['+14.5', '+20.0']);
  });

  test('the class leader itself still reads the word', () => {
    expect(evalNcalc(carClassRaceGap(num(1)), frame('class'))).toBe('Lead');
  });

  test('the second of the class reads its interval to the leader', () => {
    expect(evalNcalc(carClassInterval(num(3)), frame('class'))).toBe('+14.5');
    expect(evalNcalc(carClassInterval(num(4)), frame('class'))).toBe('+5.5');
  });
});

describe('the overall list', () => {
  test('P2 reads its interval to the leader', () => {
    expect(evalNcalc(carInterval(num(2)), frame('overall'))).toBe('+10.1');
  });

  test('the rows below read the same gaps and intervals as before', () => {
    expect([2, 3, 4, 5].map((idx) => evalNcalc(carRaceGap(num(idx)), frame('overall')))).toEqual(['+10.1', '+14.5', '+20.0', '+25.3']);
    expect([3, 4, 5].map((idx) => evalNcalc(carInterval(num(idx)), frame('overall')))).toEqual(['+4.4', '+5.5', '+5.3']);
  });
});

describe('only the first car of the leaderboard is taken to be 0', () => {
  test("a class whose leader does not lead the race measures from that leader's own gap", () => {
    // The player is the second GT4: its class leader is the GT4 10.1 s behind the race leader.
    const props = frame('class', FIELD, 5);
    expect(evalNcalc(carClassRaceGap(num(5)), props)).toBe('+15.2');
    expect(evalNcalc(carClassInterval(num(5)), props)).toBe('+15.2');
  });

  test('a car further down with no gap still reads the placeholder, and the car behind it nothing', () => {
    // A gap SimHub has not computed, the car having no lap distance yet, is not a gap of 0.
    const field = FIELD.map((car, i) => (i === 2 ? { ...car, gap: null } : car));
    expect(evalNcalc(carClassRaceGap(num(3)), frame('class', field))).toBe(NO_VALUE);
    expect(evalNcalc(carRaceGap(num(3)), frame('overall', field))).toBe(NO_VALUE);
    expect(evalNcalc(carInterval(num(4)), frame('overall', field))).toBe('');
    expect(evalNcalc(carClassInterval(num(4)), frame('class', field))).toBe('');
  });

  test('a leader who is the player, whose gap SimHub sets to 0, reads as before', () => {
    const field = FIELD.map((car, i) => (i === 0 ? { ...car, gap: 0 } : car));
    expect(evalNcalc(carClassRaceGap(num(3)), frame('class', field, 1))).toBe('+14.5');
    expect(evalNcalc(carInterval(num(2)), frame('overall', field, 1))).toBe('+10.1');
  });
});

/**
 * The boards the VM read the fault on, drawn as they are shipped: the pit wall's race board, whose
 * Int column the faces do not draw, and a face's leaderboard zone.
 */
describe('the boards that draw the two columns', () => {
  const formula = (items: Iterable<Item>, suffix: string): string => {
    const found = [...items].find((i) => i.name.endsWith(suffix));
    const bound = found?.bindings?.Text;
    if (!bound || typeof bound !== 'object' || !('formula' in bound)) throw new Error(`no ${suffix} formula`);
    return String(bound.formula);
  };
  const PIT_WALL = [...walkItems(racePage(1920, 1080).items)];
  const ZONE = [...walkItems(MODULES.find((m) => m.id === 'leaderboard')!.build({ frame: rect(0, 0, 769, 358), density: 'zone', prefix: '' }))];
  const column = (items: Item[], suffix: string, mode: 'class' | 'overall', rows: number): unknown[] =>
    Array.from({ length: rows }, (_, i) => evalNcalc(formula(items, suffix), frame(mode, FIELD, PLAYER, i + 1)));

  test("the pit wall's race board, counting in class", () => {
    expect(column(PIT_WALL, 'row.gap', 'class', 3)).toEqual(['Lead', '+14.5', '+20.0']);
    expect(column(PIT_WALL, 'row.int', 'class', 3)).toEqual(['', '+14.5', '+5.5']);
  });

  test("the pit wall's race board, counting overall", () => {
    expect(column(PIT_WALL, 'row.gap', 'overall', 5)).toEqual(['Lead', '+10.1', '+14.5', '+20.0', '+25.3']);
    expect(column(PIT_WALL, 'row.int', 'overall', 5)).toEqual(['', '+10.1', '+4.4', '+5.5', '+5.3']);
  });

  test("a face's leaderboard, counting in class", () => {
    expect(column(ZONE, 'row.gap', 'class', 3)).toEqual(['Lead', '+14.5', '+20.0']);
  });
});
