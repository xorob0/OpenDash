/**
 * The Gap column just after the leader crosses the line (#1023).
 *
 * The column asked whether a car was lapped by taking its `currentlap` from the leader's. That is the
 * lap in progress, which goes up as a car crosses the line, so for the seconds between the leader
 * crossing it and a car on the lead lap crossing it too, that car's counter was one below the
 * leader's. Counted overall the row then fell through to SimHub's `gaptoleadercombined`, which writes
 * seconds to two decimals, and the pit wall read `+36.4` above `+42.30` in one frame. On a list drawn
 * from one class, which is every leaderboard counting in class, the column built `+1L` itself and a
 * classmate seconds behind the class leader read a lap down, every lap.
 *
 * Whether a car is a lap down is a question of distance, and SimHub answers it per car:
 * `GameManagerBase` in 9.12.6 sets `LapsToLeader` to the truncated difference of the leader's
 * `CurrentLapHighPrecision` and the car's, which is laps and the fraction of the lap run, and
 * `LapsToClassLeader` the same against the first car of the player's class. The column reads those.
 *
 * The frame below is one class of five GT3s in a race, the leader having just crossed the line to
 * start its sixth lap. The second and third cars are on its lap, 12.3 s and 40 s behind, and still
 * on their fifth; the fourth is a lap down by distance and two by the counter; the fifth two down by
 * both.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import type { Item } from '../src/generator.ts';
import { ncalc } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { racePage } from '../src/screens/pitwall.ts';
import { carClassRaceGap, carRaceGap } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const { num } = ncalc;

interface Car {
  currentlap: number;
  gaptoleader: number;
  /** SimHub's laps to the leader, by distance: null on the leader's own row, as SimHub leaves it. */
  laps: number | null;
  /** What SimHub's `gaptoleadercombined` writes for the car. */
  combined: string;
}

const FIELD: readonly Car[] = [
  { currentlap: 6, gaptoleader: 0, laps: null, combined: '' },
  { currentlap: 5, gaptoleader: 12.31, laps: 0, combined: '+12.31' },
  { currentlap: 5, gaptoleader: 40.04, laps: 0, combined: '+40.04' },
  { currentlap: 4, gaptoleader: 131.62, laps: 1, combined: '+1 lap' },
  { currentlap: 3, gaptoleader: 252.9, laps: 2, combined: '+2 laps' },
];

/** The car 40 s behind the leader on the leader's lap, with a lap counter one below it. */
const BEHIND = 3;
const LAPPED = 4;
const TWICE = 5;

const frame = (mode: 'class' | 'overall', row = BEHIND): Props => {
  const props: Props = {
    'OpenDash.PositionMode': mode,
    'DataCorePlugin.GameData.OpponentsCount': FIELD.length,
    'DataCorePlugin.GameData.PlayerClassOpponentsCount': FIELD.length,
    'repeatindex()': row,
  };
  FIELD.forEach((car, i) => {
    const n = i + 1;
    props[`driveravailable(${n})`] = true;
    props[`drivercarclass(${n})`] = 'GT3';
    props[`driverposition(${n})`] = n;
    props[`driverclassposition(${n})`] = n;
    props[`getopponentleaderboardposition_playerclassonly(${n})`] = n;
    props[`drivergaptoleader(${n})`] = car.gaptoleader;
    props[`drivercurrentlap(${n})`] = car.currentlap;
    props[`drivergaptoleadercombined(${n})`] = car.combined;
    props[`driverlapstoleader(${n})`] = car.laps;
    props[`driverlapstoclassleader(${n})`] = car.laps;
  });
  return props;
};

describe('a car on the lead lap, with the leader across the line ahead of it', () => {
  test('reads its gap to one decimal on the overall list, not SimHub\'s two', () => {
    expect(evalNcalc(carRaceGap(num(BEHIND)), frame('overall'))).toBe('+40.0');
  });

  test('reads its gap on the class list, not a lap it is not down', () => {
    expect(evalNcalc(carClassRaceGap(num(BEHIND)), frame('class'))).toBe('+40.0');
  });

  test('every lead-lap row of either list reads one decimal', () => {
    const rows = [2, BEHIND];
    expect(rows.map((row) => evalNcalc(carRaceGap(num(row)), frame('overall')))).toEqual(['+12.3', '+40.0']);
    expect(rows.map((row) => evalNcalc(carClassRaceGap(num(row)), frame('class')))).toEqual(['+12.3', '+40.0']);
  });
});

describe('a car a lap or more down', () => {
  test('reads the laps it is down by distance, not by the lap counter, on both lists', () => {
    // Two laps by the counter and one by distance: the leader has just started its sixth lap and
    // this car is most of the way round its fourth.
    expect(evalNcalc(carRaceGap(num(LAPPED)), frame('overall'))).toBe('+1L');
    expect(evalNcalc(carClassRaceGap(num(LAPPED)), frame('class'))).toBe('+1L');
  });

  test('two laps down reads two, spelled the same on both lists', () => {
    // The overall list used to draw SimHub's own string here, `+2 laps`, where the class list drew
    // `+2L`: one column spelling one quantity two ways, and the longer of them wider than its cells.
    expect(evalNcalc(carRaceGap(num(TWICE)), frame('overall'))).toBe('+2L');
    expect(evalNcalc(carClassRaceGap(num(TWICE)), frame('class'))).toBe('+2L');
  });
});

describe('the class list measures laps from the class leader', () => {
  test('a car on its class leader\'s lap reads seconds even where the class is a lap down on the race', () => {
    // A GT4 class whose leader runs a lap behind the GT3 leading the race: the GT4 behind it is a
    // lap down overall and on its class leader's lap, and each list says which.
    const props: Props = {
      'OpenDash.PositionMode': 'class',
      'DataCorePlugin.GameData.OpponentsCount': 3,
      'DataCorePlugin.GameData.PlayerClassOpponentsCount': 2,
      'repeatindex()': 3,
    };
    const cars = [
      { cls: 'GT3', classpos: 1, gap: 0, laps: null, classLaps: null, currentlap: 6 },
      { cls: 'GT4', classpos: 1, gap: 101.5, laps: 1, classLaps: null, currentlap: 5 },
      { cls: 'GT4', classpos: 2, gap: 141.5, laps: 1, classLaps: 0, currentlap: 4 },
    ];
    cars.forEach((car, i) => {
      const n = i + 1;
      props[`driveravailable(${n})`] = true;
      props[`drivercarclass(${n})`] = car.cls;
      props[`driverposition(${n})`] = n;
      props[`driverclassposition(${n})`] = car.classpos;
      props[`drivergaptoleader(${n})`] = car.gap;
      props[`drivercurrentlap(${n})`] = car.currentlap;
      props[`driverlapstoleader(${n})`] = car.laps;
      props[`driverlapstoclassleader(${n})`] = car.classLaps;
    });
    props['getopponentleaderboardposition_playerclassonly(1)'] = 2;
    props['getopponentleaderboardposition_playerclassonly(2)'] = 3;
    expect(evalNcalc(carClassRaceGap(num(3)), props)).toBe('+40.0');
    expect(evalNcalc(carRaceGap(num(3)), { ...props, 'OpenDash.PositionMode': 'overall' })).toBe('+1L');
  });
});

/**
 * The two boards the VM read the fault on, drawn as they are shipped and evaluated on the frame
 * under both position modes: the pit wall's race board and a face's leaderboard zone.
 */
describe('the boards that draw the column', () => {
  const formula = (items: Iterable<Item>, suffix: string): string => {
    const found = [...items].find((i) => i.name.endsWith(suffix));
    const bound = found?.bindings?.Text;
    if (!bound || typeof bound !== 'object' || !('formula' in bound)) throw new Error(`no ${suffix} formula`);
    return String(bound.formula);
  };
  const PIT_WALL = formula(walkItems(racePage(1920, 1080).items), 'row.gap');
  const ZONE = formula(walkItems(MODULES.find((m) => m.id === 'leaderboard')!.build({ frame: rect(0, 0, 769, 358), density: 'zone', prefix: '' })), 'row.gap');
  const rows = [BEHIND, LAPPED, TWICE];

  for (const [board, bound] of [["the pit wall's race board", PIT_WALL], ["a face's leaderboard", ZONE]] as const) {
    for (const mode of ['overall', 'class'] as const) {
      test(`${board}, counting ${mode}`, () => {
        expect(rows.map((row) => evalNcalc(bound, frame(mode, row)))).toEqual(['+40.0', '+1L', '+2L']);
      });
    }
  }
});
