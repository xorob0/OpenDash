/**
 * The Gap and Int columns before the sim has placed anyone (#1028).
 *
 * Before iRacing places a car, SimHub still publishes a `GapToLeader` for every car on the
 * leaderboard, measured from whichever car is first in its driver list, and some of those gaps are
 * negative. Counting overall, the Gap column asked only whether the car's place was 1 before drawing
 * it and the Int column took the difference of two of them, so a practice before anyone had set a
 * time read `+33.4`, `−20.8` and `+9.2` down a column of `P--`: a gap to a leader that does not exist,
 * some of it negative. Both columns now ask {@link hasPosition}, the guard #1014 made the one answer
 * to whether a car is placed, of the car and of the car it is measured from, and read `--` until both
 * are placed.
 *
 * The frame below is a leaderboard of four GT3s, one class, so the class-only lookup answers each
 * place with the row of the same number. Unplaced, every overall place is 0 and the gaps are the
 * VM's; placed, the places run 1 to 4 and the gaps are a race's.
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

/** The leaderboard row every read below is about, and the third of its class. */
const ROW = 3;

/** SimHub's gaps before anyone is placed, measured from the first car of the driver list. */
const UNPLACED_GAPS = [0, 33.4, -20.8, 9.2];
/** The same four cars' gaps once the sim has placed them. */
const PLACED_GAPS = [0, 1.4, 2.6, 4.1];

/**
 * One frame of the board. `leaderPlaced` places every car but the first, which is the case of a
 * gap measured from a car the sim has not placed.
 */
const frame = (placed: boolean, mode: 'class' | 'overall' = 'overall', leaderPlaced = placed): Props => {
  const gaps = placed ? PLACED_GAPS : UNPLACED_GAPS;
  const props: Props = {
    'OpenDash.PositionMode': mode,
    'DataCorePlugin.GameData.OpponentsCount': gaps.length,
    'DataCorePlugin.GameData.PlayerClassOpponentsCount': gaps.length,
    'repeatindex()': ROW,
  };
  gaps.forEach((gap, i) => {
    const row = i + 1;
    props[`driveravailable(${row})`] = true;
    props[`drivercarclass(${row})`] = 'GT3';
    props[`driverposition(${row})`] = placed && (row > 1 || leaderPlaced) ? row : 0;
    props[`driverclassposition(${row})`] = row;
    props[`getopponentleaderboardposition_playerclassonly(${row})`] = row;
    props[`drivergaptoleader(${row})`] = gap;
    props[`drivercurrentlap(${row})`] = 5;
  });
  return props;
};

const UNPLACED = frame(false);
const PLACED = frame(true);

describe('the Gap column', () => {
  test('a car the sim has not placed reads the placeholder, not a gap to a car that leads nothing', () => {
    expect(evalNcalc(carRaceGap(num(ROW)), UNPLACED)).toBe(NO_VALUE);
    expect(evalNcalc(carClassRaceGap(num(ROW)), frame(false, 'class'))).toBe(NO_VALUE);
  });

  test('a placed car reads its gap', () => {
    expect(evalNcalc(carRaceGap(num(ROW)), PLACED)).toBe('+2.6');
    expect(evalNcalc(carClassRaceGap(num(ROW)), frame(true, 'class'))).toBe('+2.6');
  });

  test('a placed car measured from a leader the sim has not placed reads the placeholder too', () => {
    // The figure is then a gap to a car whose place says nothing, so it says nothing either: the
    // guard asks both ends of the gap, not only the car the row draws.
    expect(evalNcalc(carRaceGap(num(ROW)), frame(true, 'overall', false))).toBe(NO_VALUE);
    expect(evalNcalc(carClassRaceGap(num(ROW)), frame(true, 'class', false))).toBe(NO_VALUE);
  });
});

describe('the Int column', () => {
  test('a car the sim has not placed reads the placeholder, not the difference of two such gaps', () => {
    expect(evalNcalc(carInterval(num(ROW)), UNPLACED)).toBe(NO_VALUE);
    expect(evalNcalc(carClassInterval(num(ROW)), frame(false, 'class'))).toBe(NO_VALUE);
  });

  test('a placed car reads its interval', () => {
    expect(evalNcalc(carInterval(num(ROW)), PLACED)).toBe('+1.2');
    expect(evalNcalc(carClassInterval(num(ROW)), frame(true, 'class'))).toBe('+1.2');
  });

  test('the first row stays empty either way, there being nothing in front of it', () => {
    expect([evalNcalc(carInterval(num(1)), UNPLACED), evalNcalc(carInterval(num(1)), PLACED)]).toEqual(['', '']);
  });
});

/**
 * The two boards the VM read the fault on, drawn as they are shipped and evaluated on the frame:
 * the pit wall's race board and a face's leaderboard zone, each counting overall.
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

  test("the pit wall's race board", () => {
    const read = (props: Props): unknown[] => [evalNcalc(formula(PIT_WALL, 'row.gap'), props), evalNcalc(formula(PIT_WALL, 'row.int'), props)];
    expect(read(UNPLACED)).toEqual([NO_VALUE, NO_VALUE]);
    expect(read(PLACED)).toEqual(['+2.6', '+1.2']);
  });

  test("a face's leaderboard", () => {
    expect(evalNcalc(formula(ZONE, 'row.gap'), UNPLACED)).toBe(NO_VALUE);
    expect(evalNcalc(formula(ZONE, 'row.gap'), PLACED)).toBe('+2.6');
  });
});
