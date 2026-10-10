/**
 * The laps left and the fuel to add in a timed race, read as a driver would read them (#1008).
 *
 * SimHub's `RemainingLaps` is `TotalLaps` less `CompletedLaps` for every game, never below nought
 * (GameReaderCommon 9.12.6, `GameManagerBase`), and in a purely timed iRacing race `TotalLaps` is
 * the leader's laps. So `RemainingLaps` there is how many laps the car is behind the leader: nought
 * on the lead lap, three for a car three laps down, whatever the clock says. The fuel page's Refuel
 * and the session page's Laps left both multiplied or drew it, and told a car on the lead lap with
 * half an hour to run that it needed no fuel.
 *
 * These tests evaluate the NCalc the build emits for the fuel module and the session module, on the
 * frames SimHub publishes: a timed race for a car on the lead lap and for a lapped car, the same race
 * before a lap has been timed, its laps after the clock, and a race counted in laps, where both
 * readings still come from `RemainingLaps`.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { MINUS } from '../src/design/metrics.ts';
import type { TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { charsOfText } from '../src/second/drawn.ts';
import { CHARS, LAP_WIDEST, NO_VALUE } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

/** A wide zone body, which is the shape that keeps every field of the page. */
const pageItems = (id: string): TextItem[] => {
  const module = MODULES.find((m) => m.id === id)!;
  const items = [...walkItems(module.build({ frame: rect(0, 0, 1007, 211), density: 'zone', prefix: '' }))];
  return items.filter((item): item is TextItem => item.kind === 'text');
};

const fuelItems = pageItems('fuel');
const sessionItems = pageItems('session');

const formulaOf = (items: readonly TextItem[], name: string, target: 'Text' | 'Visible'): string => {
  const formula = items.find((item) => item.name === name)?.bindings?.[target]?.formula;
  if (typeof formula !== 'string') throw new Error(`no ${target} formula on ${name}`);
  return formula;
};

/** A week of remaining time is what iRacing publishes for a session with no limit. */
const A_WEEK = 604800;

/** Twenty litres in the tank at 2.8 a lap, in a race; the session's shape is the case's. */
const frame = (session: Props, car: Props = {}): Props => ({
  'DataCorePlugin.GameData.SessionTypeName': 'Race',
  'DataCorePlugin.GameData.Fuel': 20,
  'DataCorePlugin.Computed.Fuel_LitersPerLap': 2.8,
  'DataCorePlugin.Computed.Fuel_RemainingLaps': 7.1,
  'DataCorePlugin.Computed.Fuel_RemainingTime': 720,
  'DataCorePlugin.GameData.BestLapTime': 100,
  'DataCorePlugin.GameData.LastLapTime': 102.4,
  ...session,
  ...car,
});

/**
 * A forty-five minute iRacing race with half an hour to run, as SimHub publishes it: the leader on
 * lap 15 with 14 done, which is `TotalLaps`, and `RemainingLaps` the car's laps behind that.
 */
const timed = (completed: number, extra: Props = {}): Props =>
  frame({
    'DataCorePlugin.GameData.SessionTimeLeft': 1800,
    'DataCorePlugin.GameRawData.Telemetry.SessionTimeTotal': 2700,
    'DataCorePlugin.GameData.TotalLaps': 14,
    'DataCorePlugin.GameData.CompletedLaps': completed,
    'DataCorePlugin.GameData.CurrentLap': completed + 1,
    'DataCorePlugin.GameData.RemainingLaps': Math.max(14 - completed, 0),
    ...extra,
  });

/** A thirty-lap race on lap 13, with iRacing's week of time left. */
const lapRace = (extra: Props = {}): Props =>
  frame({
    'DataCorePlugin.GameData.SessionTimeLeft': A_WEEK,
    'DataCorePlugin.GameRawData.Telemetry.SessionTimeTotal': A_WEEK,
    'DataCorePlugin.GameData.TotalLaps': 30,
    'DataCorePlugin.GameData.CompletedLaps': 12,
    'DataCorePlugin.GameData.CurrentLap': 13,
    'DataCorePlugin.GameData.RemainingLaps': 18,
    ...extra,
  });

const refuel = (props: Props): unknown => evalNcalc(formulaOf(fuelItems, 'toAdd.value', 'Text'), props);
const lapsLeft = (props: Props): unknown =>
  evalNcalc(formulaOf(sessionItems, 'lapsLeft.value', 'Visible'), props) === true ? evalNcalc(formulaOf(sessionItems, 'lapsLeft.value', 'Text'), props) : null;
const margin = (props: Props): unknown => evalNcalc(formulaOf(fuelItems, 'toEnd.value', 'Text'), { ...props, 'OpenDash.SessionProgress': 'laps' });

describe('a timed race counts its laps left from the time left and a lap', () => {
  test('a car on the lead lap is told what to add, and how many laps are left', () => {
    // Half an hour at the best lap's 100 s is eighteen whole laps and the one the clock runs out on,
    // which the leader finishes: nineteen, at 2.8 a lap, less the twenty litres in the tank.
    expect({ lapsLeft: lapsLeft(timed(14)), refuel: refuel(timed(14)) }).toEqual({ lapsLeft: '19', refuel: '33.2' });
  });

  test('a lapped car is counted from the clock too, rather than by its laps behind the leader', () => {
    // `RemainingLaps` is 3 here, and drew `3` as the laps left with half an hour to run. The count
    // is the car's own laps to its first crossing after the clock; a car behind the leader on track
    // can run a lap more, where the clock runs out between the leader's crossing and its own.
    expect({ lapsLeft: lapsLeft(timed(11)), refuel: refuel(timed(11)) }).toEqual({ lapsLeft: '19', refuel: '33.2' });
  });

  test('mid-lap, the time already run in the lap counts, so the count is not a lap short', () => {
    // 1050 s to run and 60 s into a 100 s lap: at the line in 40 s with 1010 s left, ten laps on
    // with 10 s still on the clock, and one more. Twelve, where the time left alone made it eleven.
    const midLap = timed(14, { 'DataCorePlugin.GameData.SessionTimeLeft': 1050, 'DataCorePlugin.GameData.CurrentLapTime': 60 });
    expect({ lapsLeft: lapsLeft(midLap), refuel: refuel(midLap) }).toEqual({ lapsLeft: '12', refuel: '13.6' });
    // The count is whole, and the Refuel takes off the part of the lap already run, by distance,
    // since that part is out of the tank already: 11.4 laps at 2.8, less the twenty litres (#1024).
    const placed = { ...midLap, 'DataCorePlugin.GameData.TrackPositionPercent': 0.6 };
    expect({ lapsLeft: lapsLeft(placed), refuel: refuel(placed) }).toEqual({ lapsLeft: '12', refuel: '11.9' });
    // 50 s to run and 40 s from the line: the line comes with 10 s on the clock, and a lap after it.
    expect(lapsLeft(timed(14, { 'DataCorePlugin.GameData.SessionTimeLeft': 50, 'DataCorePlugin.GameData.CurrentLapTime': 60 }))).toBe('2');
    // At the line the lap run is nought and the count is the time left's alone.
    expect(lapsLeft(timed(14, { 'DataCorePlugin.GameData.SessionTimeLeft': 1050, 'DataCorePlugin.GameData.CurrentLapTime': 0 }))).toBe('11');
  });

  test('a lap slower than the best counts as one about to end, not as two', () => {
    // 150 s into the pit lap with 1050 s to run: taken as a full lap run, so the car is counted at
    // the line, with the eleven whole laps of the time left still to come after it.
    expect(lapsLeft(timed(14, { 'DataCorePlugin.GameData.SessionTimeLeft': 1050, 'DataCorePlugin.GameData.CurrentLapTime': 150 }))).toBe('12');
    // After the clock the slow lap is still the last one.
    expect(lapsLeft(timed(14, { 'DataCorePlugin.GameData.SessionTimeLeft': 0, 'DataCorePlugin.GameData.CurrentLapTime': 150 }))).toBe('1');
  });

  test('the count is the best lap where there is one, and the last lap until there is', () => {
    // The best, because a lap through the pit lane or behind the safety car is a lap the race will
    // not be run at, and counting from it would put too little in the tank.
    expect(lapsLeft(timed(14, { 'DataCorePlugin.GameData.LastLapTime': 160 }))).toBe('19');
    expect(lapsLeft(timed(14, { 'DataCorePlugin.GameData.BestLapTime': null, 'DataCorePlugin.GameData.LastLapTime': 120 }))).toBe('16');
  });

  test('the lap the clock runs out on is still a lap to run', () => {
    // The clock at nought, the session still timed by its declared length (#1017): the lap in
    // progress is the last, and it is one lap rather than none.
    const atNought = timed(14, { 'DataCorePlugin.GameData.SessionTimeLeft': 0, 'DataCorePlugin.GameData.CurrentLapTime': 40 });
    expect({ lapsLeft: lapsLeft(atNought), refuel: refuel(atNought) }).toEqual({ lapsLeft: '1', refuel: '0.0' });
  });

  test('before a lap has been timed, both say so rather than draw nought', () => {
    const untimedLap = { 'DataCorePlugin.GameData.BestLapTime': null, 'DataCorePlugin.GameData.LastLapTime': null };
    // On the grid: no lap done and no lap time.
    expect({ lapsLeft: lapsLeft(timed(0, untimedLap)), refuel: refuel(timed(0, untimedLap)) }).toEqual({ lapsLeft: NO_VALUE, refuel: NO_VALUE });
    // A lap done and a consumption to show for it, but no time the sim will give for that lap: the
    // refuel's own gate has passed, and `0.0` would be the fault this ticket is about.
    expect({ lapsLeft: lapsLeft(timed(1, untimedLap)), refuel: refuel(timed(1, untimedLap)) }).toEqual({ lapsLeft: NO_VALUE, refuel: NO_VALUE });
  });

  test('the margin forced onto laps counts against the same laps left', () => {
    // 7.1 laps in the tank against nineteen to run, where `RemainingLaps` made it `+7.1` to spare.
    expect(margin(timed(14))).toBe(`${MINUS}11.9`);
    expect(margin(timed(14, { 'DataCorePlugin.GameData.BestLapTime': null, 'DataCorePlugin.GameData.LastLapTime': null }))).toBe(NO_VALUE);
  });

  test('no reading in either module reads the leader’s laps as the laps left while the race is timed', () => {
    // Whatever the leader's laps make `RemainingLaps`, neither field moves while the race is timed.
    for (const completed of [0, 5, 11, 14]) {
      const base = timed(completed);
      const moved = { ...base, 'DataCorePlugin.GameData.RemainingLaps': 40 };
      expect({ completed, lapsLeft: lapsLeft(moved), refuel: refuel(moved) }).toEqual({ completed, lapsLeft: lapsLeft(base), refuel: refuel(base) });
    }
  });

  test('a day at Daytona is three digits, which the field is cut for', () => {
    // The longest count a driver meets: 86400 s at a 1:35 lap is 909 whole laps and one. The field
    // is cut for `999`, which a day reaches only at a lap under 86.5 s, shorter than any lap of the
    // day-long races iRacing runs.
    const daytona = timed(0, { 'DataCorePlugin.GameData.SessionTimeLeft': 86400, 'DataCorePlugin.GameRawData.Telemetry.SessionTimeTotal': 86400, 'DataCorePlugin.GameData.BestLapTime': 95 });
    expect(lapsLeft(daytona)).toBe('910');
    expect(String(lapsLeft(daytona)).length).toBeLessThanOrEqual(LAP_WIDEST.length);
  });

  test('a refuel past a thousand is drawn whole, so it stays inside the four cells and the point', () => {
    // 910 laps at 3.5 a lap, less a full hundred-litre tank: `3085.0` was five digit cells in a
    // field cut for four, and WPF drew `3085.` with the last glyph gone.
    const daytona = (perLap: number, tank: number): Props =>
      timed(1, {
        'DataCorePlugin.GameData.SessionTimeLeft': 86400,
        'DataCorePlugin.GameRawData.Telemetry.SessionTimeTotal': 86400,
        'DataCorePlugin.GameData.BestLapTime': 95,
        'DataCorePlugin.Computed.Fuel_LitersPerLap': perLap,
        'DataCorePlugin.GameData.Fuel': tank,
      });
    expect(refuel(daytona(3.5, 100))).toBe('3085');
    // Under a thousand it keeps its tenth, and the step sits where `0.0` would write a fourth digit.
    expect(refuel(daytona(1.1, 1.0))).toBe('1000');
    expect(refuel(daytona(1.1, 1.06))).toBe('999.9');
    // Every reading from a sprint to a day, at a light and a thirsty car, is inside the budget.
    const mono = { charWidth: 10, specialCharsWidth: 5, specialChars: '.,:' };
    for (const seconds of [600, 2700, 21600, 43200, 86400]) {
      for (const perLap of [0.4, 2.8, 4.5, 9.9]) {
        const text = String(refuel(timed(1, { 'DataCorePlugin.GameData.SessionTimeLeft': seconds, 'DataCorePlugin.GameRawData.Telemetry.SessionTimeTotal': seconds, 'DataCorePlugin.GameData.BestLapTime': 95, 'DataCorePlugin.Computed.Fuel_LitersPerLap': perLap, 'DataCorePlugin.GameData.Fuel': 0 })));
        const drawn = charsOfText(text, mono);
        expect({ seconds, perLap, text, fits: drawn.digits <= CHARS.fuel.digits && drawn.digits + drawn.specials <= CHARS.fuel.digits + CHARS.fuel.specials }).toMatchObject({ fits: true });
      }
    }
  });
});

describe('a race counted in laps still reads them from RemainingLaps', () => {
  test('the laps left and the refuel are the lap count’s', () => {
    // Eighteen laps at 2.8, less twenty: the best lap is in the frame and changes nothing.
    expect({ lapsLeft: lapsLeft(lapRace()), refuel: refuel(lapRace()) }).toEqual({ lapsLeft: '18', refuel: '30.4' });
    expect(lapsLeft(lapRace({ 'DataCorePlugin.GameData.BestLapTime': 50 }))).toBe('18');
  });

  test('and with no laps left, as before, there is nothing to draw and nothing to add', () => {
    const over = lapRace({ 'DataCorePlugin.GameData.RemainingLaps': 0 });
    expect({ lapsLeft: lapsLeft(over), refuel: refuel(over) }).toEqual({ lapsLeft: null, refuel: '0.0' });
  });

  test('an open practice with no clock and no lap count draws no laps left', () => {
    const open = frame({
      'DataCorePlugin.GameData.SessionTimeLeft': A_WEEK,
      'DataCorePlugin.GameRawData.Telemetry.SessionTimeTotal': A_WEEK,
      'DataCorePlugin.GameData.TotalLaps': 0,
      'DataCorePlugin.GameData.CompletedLaps': 6,
      'DataCorePlugin.GameData.RemainingLaps': 0,
      'DataCorePlugin.GameData.SessionTypeName': 'Practice',
    });
    expect(lapsLeft(open)).toBeNull();
  });
});
