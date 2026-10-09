/**
 * The clamps keep the fraction of what they clamp, #831.
 *
 * NCalc's `max` and `min` answer in the type of their left operand, the right one converted to it,
 * so a bound written as the Int32 `0` or `100` on the left rounds a double to a whole number before
 * anything formats it. Each reading here is evaluated with the generator's own evaluator, which
 * models that rule (`packages/generator/src/ncalc/functions.ts`), and asserted by what a driver sees:
 * the session clock at 59.6 seconds, the refuel figure under its one-decimal format, the band D
 * fuel clock and the pit stop gauge.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { ncalcEvaluator as E, type TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { minutesClock, pitServiceProgress, sessionClock } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';

const ev = (expression: string, properties: Record<string, unknown>): E.Value => E.evaluate(expression, { properties });
const js = (expression: string, properties: Record<string, unknown>): unknown => E.toJs(ev(expression, properties));

/** The fuel module's text items at the wide zone body, which keeps every field. */
const fuelItems = (): TextItem[] => {
  const module = MODULES.find((m) => m.id === 'fuel')!;
  const items = [...walkItems(module.build({ frame: rect(0, 0, 1007, 211), density: 'zone', prefix: '' }))];
  return items.filter((item): item is TextItem => item.kind === 'text');
};

const textOf = (items: readonly TextItem[], name: string): string => {
  const formula = items.find((item) => item.name === name)?.bindings?.Text?.formula;
  if (typeof formula !== 'string') throw new Error(`no Text formula on ${name}`);
  return formula;
};

describe('the session clock counts whole seconds down rather than rounding them', () => {
  const at = (seconds: number): unknown => js(sessionClock(), { 'DataCorePlugin.GameData.SessionTimeLeft': E.fromSeconds(seconds) });

  test('59.6 seconds left is still 0:00:59, and the minute turns only when it is reached', () => {
    expect(at(59.6)).toBe('0:00:59');
    expect(at(60)).toBe('0:01:00');
    expect(at(3599.5)).toBe('0:59:59');
    expect(at(3600)).toBe('1:00:00');
    expect(at(1.5)).toBe('0:00:01');
  });
});

describe('the band D fuel clock counts the same way', () => {
  test('59.6 seconds of fuel is 00:59, not a minute', () => {
    expect(js(minutesClock('[S]'), { S: 59.6 })).toBe('00:59');
    expect(js(minutesClock('[S]'), { S: 119.5 })).toBe('01:59');
    expect(js(minutesClock('[S]'), { S: 120 })).toBe('02:00');
  });
});

describe('the refuel figure keeps its tenths', () => {
  const items = fuelItems();
  const refuel = (lapsLeft: number, perLap: number, fuel: number): unknown =>
    js(textOf(items, 'toAdd.value'), {
      'DataCorePlugin.GameData.CompletedLaps': 3,
      'DataCorePlugin.GameData.RemainingLaps': lapsLeft,
      'DataCorePlugin.Computed.Fuel_LitersPerLap': perLap,
      'DataCorePlugin.GameData.Fuel': fuel,
    });

  test('the litres the laps left will burn, less the tank, to a tenth of a litre', () => {
    // 11 laps at 2.84 is 31.24 litres, less 23.84 in the tank: 7.4 to add, where an Int32 floor read 7.0.
    expect(refuel(11, 2.84, 23.84)).toBe('7.4');
    expect(refuel(10, 3.05, 12.2)).toBe('18.3');
    expect(refuel(5, 2.5, 30.6)).toBe('0.0');
  });
});

describe('the pit stop gauge moves by less than a whole percent', () => {
  // Both are doubles in SimHub, as the pit trace records them; passed as such so a whole number of
  // seconds is not read as an Int32 and divided as one.
  const progress = (since: number, last: number): E.Value =>
    ev(pitServiceProgress(), {
      'DataCorePlugin.GameData.IsInPitLane': 1,
      'DataCorePlugin.GameData.IsInPitSince': E.double(since),
      'DataCorePlugin.GameData.LastPitStopDuration': E.double(last),
    });

  test('five seconds into an eight second stop is 62.5 percent, and an overrun is capped at 100', () => {
    expect(progress(5, 8)).toEqual(E.double(62.5));
    expect(E.toJs(progress(1, 3))).toBeCloseTo(33.333, 3);
    expect(E.toJs(progress(12, 8))).toBe(100);
  });
});
