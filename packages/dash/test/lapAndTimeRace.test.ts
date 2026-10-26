/**
 * A race given both a lap count and a time limit is measured by whichever ends it first (#1026).
 *
 * iRacing lets a race be set to twenty laps or an hour, whichever comes first, and publishes both: a
 * running `SessionTimeRemain` and a `SessionLapsTotal` of 20, where a race with no lap limit writes
 * 32767. The dash used to ask only whether the session had a clock, so a twenty-lap race with an
 * hour's cap drew the time left rather than `13 / 20`, predicted its laps left from the clock, and
 * read the fuel margin in minutes against the cap. These tests evaluate what the build emits, the
 * way `fuelMargin.test.ts` does, on the frames the ticket names and on the cases either side.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import type { TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { isTimedSession, sessionClock } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { evalTyped, type Props } from './ncalcEval.ts';

/** A wide zone body, which is the shape that keeps every field of the page. */
const pageItems = (id: string): TextItem[] => {
  const module = MODULES.find((m) => m.id === id)!;
  return [...walkItems(module.build({ frame: rect(0, 0, 1007, 211), density: 'zone', prefix: '' }))].filter(
    (item): item is TextItem => item.kind === 'text',
  );
};

const sessionItems = pageItems('session');
const fuelItems = pageItems('fuel');

/** What the named item draws in `props`: its text, or null while it is hidden. */
const drawn = (items: readonly TextItem[], name: string, props: Props): unknown => {
  const item = items.find((i) => i.name === name);
  if (item === undefined) throw new Error(`no ${name}`);
  const visible = item.bindings?.Visible?.formula;
  if (typeof visible === 'string' && evalTyped(visible, props) !== true) return null;
  const text = item.bindings?.Text?.formula;
  return typeof text === 'string' ? evalTyped(text, props) : item.text;
};

const NO_LAP_LIMIT = 32767;

/**
 * A race with fifty minutes on its clock, twelve laps run at 2:18 and the thirteenth just begun, the
 * tank good for 10.9 laps. `SessionLapsTotal` is what decides it, and `TotalLaps` and
 * `RemainingLaps` are what SimHub publishes beside it: the race's length and its real laps left where
 * the race has a lap count (#1008), the leader's completed laps where it has none.
 */
const race = (lapLimit: number | null, extra: Props = {}): Props => {
  const counted = lapLimit !== null && lapLimit <= 999;
  return {
    'DataCorePlugin.GameData.SessionTypeName': 'Race',
    'DataCorePlugin.GameData.SessionTimeLeft': 3000,
    'DataCorePlugin.GameRawData.Telemetry.SessionTimeTotal': 3600,
    ...(lapLimit === null ? {} : { 'DataCorePlugin.GameRawData.Telemetry.SessionLapsTotal': lapLimit }),
    'DataCorePlugin.GameData.CompletedLaps': 12,
    'DataCorePlugin.GameData.CurrentLap': 13,
    'DataCorePlugin.GameData.TotalLaps': counted ? lapLimit : 12,
    'DataCorePlugin.GameData.RemainingLaps': counted ? lapLimit - 12 : 0,
    'DataCorePlugin.GameData.BestLapTime': 138,
    'DataCorePlugin.GameData.CurrentLapTime': 0,
    'DataCorePlugin.GameData.TrackPositionPercent': 0,
    'DataCorePlugin.Computed.Fuel_LitersPerLap': 2.8,
    'DataCorePlugin.Computed.Fuel_RemainingLaps': 10.9,
    'DataCorePlugin.Computed.Fuel_RemainingTime': 10.9 * 138,
    'driverisplayer(1)': true,
    ...extra,
  };
};

/** What the session page and the fuel margin draw for the counter, in the default `auto`. */
const reading = (props: Props) => ({
  timed: evalTyped(isTimedSession(), props),
  lap: drawn(sessionItems, 'lap.value', props),
  total: drawn(sessionItems, 'lap.denominator', props),
  timeLeft: drawn(sessionItems, 'timeLeft.value', props),
  lapsLeft: drawn(sessionItems, 'lapsLeft.value', props),
  margin: drawn(fuelItems, 'toEnd.value', props),
  unit: drawn(fuelItems, 'toEnd.unit', props),
});

describe('a race with a lap count and a time limit', () => {
  test('twenty laps with fifty minutes on the clock and twelve run is counted in laps', () => {
    // Eight laps to the count, where the clock holds twenty-two more at 2:18.
    expect(reading(race(20))).toEqual({
      timed: false,
      lap: '13',
      total: '/ 20',
      timeLeft: null,
      lapsLeft: '8',
      margin: '+2.9',
      unit: 'laps',
    });
  });

  test('a purely timed race, whose lap count is 32767, is measured by the clock', () => {
    const r = reading(race(NO_LAP_LIMIT));
    expect({ timed: r.timed, lap: r.lap, total: r.total, timeLeft: r.timeLeft, unit: r.unit }).toEqual({
      timed: true,
      lap: null,
      total: null,
      timeLeft: '0:50:00',
      unit: 'min',
    });
    // Twenty-one whole laps of the fifty minutes and the one the clock runs out on.
    expect(r.lapsLeft).toBe('22');
  });

  test('a lap count further off than the clock leaves the race measured by the clock', () => {
    // Fifty laps with twelve run is thirty-eight to go, and the clock ends it after twenty-two.
    const r = reading(race(50));
    expect({ timed: r.timed, lap: r.lap, timeLeft: r.timeLeft, lapsLeft: r.lapsLeft, unit: r.unit }).toEqual({
      timed: true,
      lap: null,
      timeLeft: '0:50:00',
      lapsLeft: '22',
      unit: 'min',
    });
  });

  test('the count takes over from the clock as the clock comes nearer', () => {
    // Forty laps, twenty-eight to go at 2:18, which is more than fifty minutes and less than three
    // hours: the clock is the nearer end with the one and the count with the other.
    expect(evalTyped(isTimedSession(), race(40))).toBe(true);
    expect(evalTyped(isTimedSession(), race(40, { 'DataCorePlugin.GameData.SessionTimeLeft': 3 * 3600 }))).toBe(false);
  });

  test('before a lap is timed the count is taken, since the clock has no lap to count in', () => {
    const untimed = race(20, { 'DataCorePlugin.GameData.BestLapTime': 0 });
    expect(evalTyped(isTimedSession(), untimed)).toBe(false);
    expect(drawn(sessionItems, 'lapsLeft.value', untimed)).toBe('8');
  });

  test('a sim that publishes no lap count is measured as it always was', () => {
    expect(evalTyped(isTimedSession(), race(null))).toBe(true);
  });

  test('the clock is still read where the count is the nearer end', () => {
    // The race is measured in laps, and the clock the session card and the bar draw still runs.
    expect(evalTyped(sessionClock(), race(20))).toBe('0:50:00');
  });
});
