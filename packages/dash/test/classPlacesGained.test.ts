/**
 * The rank column counting in class, frame after frame, from a class start SimHub made up (#1022).
 *
 * SimHub's `PositionGainClass` is `StartPositionClass - PositionInClass`, and `GameManagerBase` in
 * 9.12.6 takes `StartPositionClass` from `PositionInClass` on the first frame it sees the car and
 * keeps it until the session changes. `PositionInClass` is numbered from 1 for every listed car,
 * placed or not (#1014), so a car SimHub first sees unplaced starts from its order in the driver list
 * within its class. In a practice, a GT3 first in that list starts first in class; three GT3s set
 * times, then it sets one and is placed fourth in class, and SimHub counts three places lost that it
 * never lost. The overall start has no such fault: it is replaced while it is 0, so it becomes the
 * car's first real place, and `PositionGain` waits for it.
 *
 * Each test below draws the pit wall race board's rank cell for one car over a sequence of frames,
 * each frame giving what SimHub would publish for it, and reads what the row shows.
 */
import { describe, expect, test } from 'bun:test';
import type { Item } from '../src/generator.ts';
import { racePage } from '../src/screens/pitwall.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

/** The leaderboard row of the car, and the row of the class-filtered board that draws it. */
const ROW = 9;
const BOARD_ROW = 4;

/** What one frame publishes for the car. */
interface Frame {
  /** The car's overall place, 0 until the sim has placed it. */
  overall: number;
  /** SimHub's `PositionInClass`, which is never 0. */
  inClass: number;
  /** `StartPosition`, the first overall place the sim gave the car, or 0 before it has one. */
  start: number;
  /** `StartPositionClass`, the class place of the first frame SimHub saw the car. */
  classStart: number;
}

/** A field of 24 cars, 12 of them GT3s like the car, or a field of 12 GT3s and nothing else. */
type Field = 'two classes' | 'one class';

/** The props of one frame, as SimHub's leaderboard functions answer them for the board's row. */
const props = (f: Frame, field: Field, mode: 'class' | 'overall' = 'class'): Props => ({
  'OpenDash.PositionMode': mode,
  'DataCorePlugin.GameData.OpponentsCount': field === 'one class' ? 12 : 24,
  'DataCorePlugin.GameData.PlayerClassOpponentsCount': 12,
  // The class board draws the car on its fourth row and the field board on its own.
  'repeatindex()': mode === 'class' ? BOARD_ROW : ROW,
  [`getopponentleaderboardposition_playerclassonly(${BOARD_ROW})`]: ROW,
  [`driveravailable(${ROW})`]: true,
  [`drivercarclass(${ROW})`]: 'GT3',
  [`driverposition(${ROW})`]: f.overall,
  [`driverclassposition(${ROW})`]: f.inClass,
  // `GameManagerBase` sets each gain only while both of its terms are above 0.
  [`driverpositiongain(${ROW})`]: f.start > 0 && f.overall > 0 ? f.start - f.overall : null,
  [`driverpositiongainclass(${ROW})`]: f.classStart > 0 && f.inClass > 0 ? f.classStart - f.inClass : null,
});

const ITEMS = [...walkItems(racePage(1920, 1080).items)];
const item = (suffix: string): Item => {
  const found = ITEMS.find((i) => i.name.endsWith(`.row.rank.${suffix}`));
  if (!found) throw new Error(`the race board has no rank ${suffix}`);
  return found;
};
const formula = (i: Item, target: 'Text' | 'Visible'): string => {
  const bound = i.bindings?.[target];
  if (!bound || typeof bound !== 'object' || !('formula' in bound)) throw new Error(`${i.name} has no ${target} formula`);
  return String(bound.formula);
};

/** What the rank cell shows on a frame: the triangle, the count, or the dash, or nothing at all. */
const rankCell = (p: Props): string => {
  const shown = (suffix: string): boolean => evalNcalc(formula(item(suffix), 'Visible'), p) === true;
  const count = item('count');
  const drawn = [
    shown('up') ? '▲' : '',
    shown('down') ? '▼' : '',
    shown('count') ? String(evalNcalc(formula(count, 'Text'), p)) : '',
    shown('flat') ? '-' : '',
  ].join('');
  return drawn === '' ? 'nothing' : drawn;
};

/** First unplaced with a class place of 1, then placed fourth in class, seventh overall. */
const PRACTICE: readonly Frame[] = [
  { overall: 0, inClass: 1, start: 0, classStart: 1 },
  { overall: 7, inClass: 4, start: 7, classStart: 1 },
];

describe('a car SimHub first saw unplaced, counting in class', () => {
  test('SimHub itself counts three places lost on the frame the car is placed', () => {
    // The fault, as SimHub publishes it: whatever draws `driverpositiongainclass` draws this.
    expect(evalNcalc(`driverpositiongainclass(${ROW})`, props(PRACTICE[1]!, 'two classes'))).toBe(-3);
  });

  test('in a field of two classes the cell draws no places lost, and no count at all', () => {
    expect(PRACTICE.map((f) => rankCell(props(f, 'two classes')))).toEqual(['nothing', 'nothing']);
  });

  test('in a field of one class it draws no movement, and then the movement from its first real place', () => {
    const field: Field = 'one class';
    const frames: Frame[] = [
      { overall: 0, inClass: 1, start: 0, classStart: 1 },
      { overall: 4, inClass: 4, start: 4, classStart: 1 },
      // Two of the cars ahead fall back behind it: two places gained, where SimHub's class count
      // reads one lost.
      { overall: 2, inClass: 2, start: 4, classStart: 1 },
    ];
    expect(frames.map((f) => rankCell(props(f, field)))).toEqual(['-', '-', '▲2']);
  });
});

describe('a car placed from its first frame', () => {
  test('in a field of one class the count is its class start less its class place', () => {
    const f: Frame = { overall: 3, inClass: 3, start: 6, classStart: 6 };
    expect(rankCell(props(f, 'one class'))).toBe(`▲${f.classStart - f.inClass}`);
    expect(rankCell(props({ ...f, overall: 8, inClass: 8 }, 'one class'))).toBe('▼2');
  });

  test('in a field of two classes nothing SimHub publishes tells it from a made-up start, so the cell is empty', () => {
    // Fifth in class at the start, third now. SimHub's class count of 2 is right for this car, and
    // reads exactly as the made-up one above does; the overall count of 4 is a different field's.
    const f: Frame = { overall: 5, inClass: 3, start: 9, classStart: 5 };
    expect(rankCell(props(f, 'two classes'))).toBe('nothing');
  });

  test('counting overall the cell is the overall count, in either field', () => {
    const f: Frame = { overall: 5, inClass: 3, start: 9, classStart: 5 };
    expect(rankCell(props(f, 'two classes', 'overall'))).toBe('▲4');
    expect(rankCell(props({ overall: 0, inClass: 3, start: 0, classStart: 1 }, 'two classes', 'overall'))).toBe('-');
  });
});
