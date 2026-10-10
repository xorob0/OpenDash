/**
 * The Gap and Int columns outside a race, where a leaderboard is ordered by best lap (#1040).
 *
 * SimHub measures every gap it publishes as distance run. `GameManagerBase.ComputeOpponentsData` in
 * 9.12.6 sets a car's `GaptoLeader` from the first car's `CurrentLapHighPrecision` less its own,
 * times the reference best lap, and `LapsToLeader` from the truncated difference of the same two
 * distances, in every session. In a race that is the gap, since the order is the order on the road.
 * In practice and qualifying iRacing orders its results by best lap, so the first car is the fastest
 * rather than the furthest round, and the distance between two cars says how long each has been out.
 * A car 0.4 s off the best lap and five laps short of the fastest car's running read `+5L`, and one
 * 0.6 s off that had run five laps more read `−432.0`.
 *
 * The frame below is the ticket's practice: P1 by best lap has run 10.3 laps, P2 0.4 s slower 4.8
 * and P3 0.6 s slower 15.1, all GT3s, with the gaps and lap counts SimHub derives from those
 * distances on a 90 s reference lap. Two GT4s follow, and last a GT3 that has run a lap with no time.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { MINUS } from '../src/design/metrics.ts';
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
  /** The best lap in seconds, 0 for a car with none, which is what SimHub leaves its TimeSpan at. */
  best: number;
  /** `CurrentLapHighPrecision`: laps begun and the fraction of the lap run. */
  run: number;
}

const PRACTICE: readonly Car[] = [
  { cls: 'GT3', best: 90.0, run: 10.3 },
  { cls: 'GT3', best: 90.4, run: 4.8 },
  { cls: 'GT3', best: 90.6, run: 15.1 },
  { cls: 'GT4', best: 95.2, run: 7.9 },
  { cls: 'GT4', best: 95.9, run: 12.2 },
  { cls: 'GT3', best: 0, run: 1.6 },
];

/** The same cars in a race: P2 3.6 s behind the leader on its lap, P3 a lap down by distance. */
const RACE: readonly Car[] = [
  { cls: 'GT3', best: 90.0, run: 10.3 },
  { cls: 'GT3', best: 90.4, run: 10.26 },
  { cls: 'GT3', best: 90.6, run: 9.2 },
  { cls: 'GT4', best: 95.2, run: 9.0 },
  { cls: 'GT4', best: 95.9, run: 8.95 },
  { cls: 'GT3', best: 0, run: 1.6 },
];

/** SimHub's distance-to-time gap mode measures on this lap. */
const REFERENCE_LAP = 90;

/**
 * A frame of `field` as SimHub publishes it, in the session SimHub names `session`. The first car is
 * not the player, so its own gap and lap counts are null, as `ComputeOpponentsData` leaves them.
 */
const frame = (session: string, mode: 'class' | 'overall', field: readonly Car[] = PRACTICE, player = 2, row = 1): Props => {
  const playerClass = field[player - 1]!.cls;
  const props: Props = {
    'DataCorePlugin.GameData.SessionTypeName': session,
    'OpenDash.PositionMode': mode,
    'DataCorePlugin.GameData.OpponentsCount': field.length,
    'DataCorePlugin.GameData.PlayerClassOpponentsCount': field.filter((car) => car.cls === playerClass).length,
    'getplayerleaderboardposition()': player,
    'repeatindex()': row,
  };
  const leader = field[0]!;
  const classLeader = field.find((car) => car.cls === playerClass)!;
  const places = new Map<string, number>();
  let inClass = 0;
  field.forEach((car, i) => {
    const n = i + 1;
    const classPlace = (places.get(car.cls) ?? 0) + 1;
    places.set(car.cls, classPlace);
    props[`driveravailable(${n})`] = true;
    props[`drivercarclass(${n})`] = car.cls;
    props[`driverposition(${n})`] = n;
    props[`driverclassposition(${n})`] = classPlace;
    props[`driverbestlap(${n})`] = car.best;
    props[`drivergaptoleader(${n})`] = n === 1 ? null : (leader.run - car.run) * REFERENCE_LAP;
    props[`driverlapstoleader(${n})`] = n === 1 ? null : Math.trunc(leader.run - car.run);
    props[`driverlapstoclassleader(${n})`] = car === classLeader ? null : -Math.trunc(car.run - classLeader.run);
    if (car.cls === playerClass) props[`getopponentleaderboardposition_playerclassonly(${++inClass})`] = n;
  });
  return props;
};

const gaps = (session: string, mode: 'class' | 'overall', rows: number[], field: readonly Car[] = PRACTICE, player = 2): unknown[] =>
  rows.map((idx) => evalNcalc(mode === 'class' ? carClassRaceGap(num(idx)) : carRaceGap(num(idx)), frame(session, mode, field, player)));

describe('a practice, which is ordered by best lap', () => {
  test("P2 and P3 read their best laps less the leader's, not the distance each has run", () => {
    expect(gaps('Practice', 'overall', [2, 3])).toEqual(['+0.4', '+0.6']);
    expect(gaps('Practice', 'class', [2, 3])).toEqual(['+0.4', '+0.6']);
  });

  test('the leader still reads the word', () => {
    expect(gaps('Practice', 'overall', [1])).toEqual(['Lead']);
    expect(gaps('Practice', 'class', [1])).toEqual(['Lead']);
  });

  test('a car with no best lap reads the placeholder', () => {
    expect(gaps('Practice', 'overall', [6])).toEqual([NO_VALUE]);
    expect(gaps('Practice', 'class', [6])).toEqual([NO_VALUE]);
  });

  test('so does every car while the first car has no best lap either', () => {
    const field = PRACTICE.map((car, i) => (i === 0 ? { ...car, best: 0 } : car));
    expect(gaps('Practice', 'overall', [2, 3], field)).toEqual([NO_VALUE, NO_VALUE]);
  });

  test('the overall list measures every class from the fastest car of the field', () => {
    expect(gaps('Practice', 'overall', [4, 5])).toEqual(['+5.2', '+5.9']);
  });

  test("a class list measures from its own class's first car", () => {
    // The player is the second GT4, whose class leader is the GT4 95.2 s round.
    expect(gaps('Practice', 'class', [5], PRACTICE, 5)).toEqual(['+0.7']);
  });

  test("the Int column reads each car's best lap less the one above it", () => {
    const overall = [2, 3, 4, 5].map((idx) => evalNcalc(carInterval(num(idx)), frame('Practice', 'overall')));
    expect(overall).toEqual(['+0.4', '+0.2', '+4.6', '+0.7']);
    expect([2, 3].map((idx) => evalNcalc(carClassInterval(num(idx)), frame('Practice', 'class')))).toEqual(['+0.4', '+0.2']);
    expect(evalNcalc(carClassInterval(num(5)), frame('Practice', 'class', PRACTICE, 5))).toBe('+0.7');
  });

  test('an Int beside a car with no best lap is empty, as it is beside a gap SimHub has not measured', () => {
    expect(evalNcalc(carInterval(num(6)), frame('Practice', 'overall'))).toBe('');
    expect(evalNcalc(carClassInterval(num(6)), frame('Practice', 'class'))).toBe('');
  });

  test('a car the sim has not placed still reads the placeholder, as it does in a race', () => {
    const props = { ...frame('Practice', 'overall'), 'driverposition(3)': 0 };
    expect(evalNcalc(carRaceGap(num(3)), props)).toBe(NO_VALUE);
    expect(evalNcalc(carInterval(num(3)), props)).toBe(NO_VALUE);
  });
});

describe('every session that is not a race reads the same', () => {
  for (const session of ['Practice', 'Open Qualify', 'Lone Qualify', 'Qualify', 'Offline Testing', 'Warmup', 'PRACTICE']) {
    test(session, () => {
      expect(gaps(session, 'overall', [2, 3])).toEqual(['+0.4', '+0.6']);
    });
  }
});

describe('a race, which is ordered on the road', () => {
  test('reads the distance gap, as it did', () => {
    expect(gaps('Race', 'overall', [2, 3], RACE)).toEqual(['+3.6', '+1L']);
    expect(gaps('Race', 'class', [2, 3], RACE)).toEqual(['+3.6', '+1L']);
    expect([2, 3].map((idx) => evalNcalc(carInterval(num(idx)), frame('Race', 'overall', RACE)))).toEqual(['+3.6', '+95.4']);
  });

  test('the practice frame in a race still reads its distances, which is what a race measures', () => {
    expect(gaps('Race', 'overall', [2, 3])).toEqual(['+5L', `${MINUS}432.0`]);
  });

  test('a session the sim names nothing, or names in a word not listed, is measured as a race', () => {
    // Measuring a race by best lap would draw small, believable figures that are false; the distance
    // gap is the reading the dash has always given, and is right in every race.
    expect(gaps('', 'overall', [2, 3], RACE)).toEqual(['+3.6', '+1L']);
    expect(gaps('Heat', 'overall', [2, 3], RACE)).toEqual(['+3.6', '+1L']);
  });
});

/** The boards the column is drawn on, as they are shipped: the pit wall's race board and a face's leaderboard. */
describe('the boards that draw the two columns, in qualifying', () => {
  const formula = (items: Iterable<Item>, suffix: string): string => {
    const found = [...items].find((i) => i.name.endsWith(suffix));
    const bound = found?.bindings?.Text;
    if (!bound || typeof bound !== 'object' || !('formula' in bound)) throw new Error(`no ${suffix} formula`);
    return String(bound.formula);
  };
  const PIT_WALL = [...walkItems(racePage(1920, 1080).items)];
  const ZONE = [...walkItems(MODULES.find((m) => m.id === 'leaderboard')!.build({ frame: rect(0, 0, 769, 358), density: 'zone', prefix: '' }))];
  const column = (items: Item[], suffix: string, mode: 'class' | 'overall', rows: number): unknown[] =>
    Array.from({ length: rows }, (_, i) => evalNcalc(formula(items, suffix), frame('Lone Qualify', mode, PRACTICE, 2, i + 1)));

  test("the pit wall's race board, counting overall", () => {
    expect(column(PIT_WALL, 'row.gap', 'overall', 6)).toEqual(['Lead', '+0.4', '+0.6', '+5.2', '+5.9', NO_VALUE]);
    expect(column(PIT_WALL, 'row.int', 'overall', 6)).toEqual(['', '+0.4', '+0.2', '+4.6', '+0.7', '']);
  });

  test("the pit wall's race board, counting in class", () => {
    expect(column(PIT_WALL, 'row.gap', 'class', 4)).toEqual(['Lead', '+0.4', '+0.6', NO_VALUE]);
    expect(column(PIT_WALL, 'row.int', 'class', 4)).toEqual(['', '+0.4', '+0.2', '']);
  });

  test("a face's leaderboard, counting in class", () => {
    expect(column(ZONE, 'row.gap', 'class', 3)).toEqual(['Lead', '+0.4', '+0.6']);
  });
});
