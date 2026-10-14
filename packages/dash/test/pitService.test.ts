/**
 * Pit view where the sim publishes no pit service order (#564).
 *
 * The order is iRacing's `PitSvFlags` and `PitSvFuel`. On Assetto Corsa, and on every other sim, both
 * are absent, every bit of the flags read 0, and pit view drew the whole order as cancelled: four
 * empty corners, the fast repair and tear-off off, "No tyres" and a refuel of nothing. A driver read
 * that as an order to take no tyres. So these evaluate the NCalc the build emits against an iRacing
 * frame and against one with no order, and hold the toggles, the notice in their place, the refuel
 * figure and the pit time, which reads SimHub's own pit members and so counts on every sim.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import type { Density } from '../src/second/density.ts';
import { MODULES } from '../src/modules/index.ts';
import { SERVICE_NOTICES } from '../src/modules/pitView.ts';
import { NO_VALUE } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, Single, type Props } from './ncalcEval.ts';
import { moduleBoxes } from './secondScreens.test.ts';

const FLAGS = 'DataCorePlugin.GameRawData.Telemetry.PitSvFlags';
const FUEL = 'DataCorePlugin.GameRawData.Telemetry.PitSvFuel';

/** The members pit time reads, which SimHub fills on every sim: in the lane, 12 s into a stop. */
const lane: Props = {
  'DataCorePlugin.GameRunning': 1,
  'DataCorePlugin.GameData.SessionTypeName': 'Race',
  'DataCorePlugin.GameData.IsInPitLane': 1,
  'DataCorePlugin.GameData.IsInPitSince': 12.4,
  'DataCorePlugin.GameData.LastPitStopDuration': 31.0,
  'DataCorePlugin.GameData.FuelUnit': 'Liters',
};
/** iRacing ordering fast repair (64) and the two left tyres (1 + 4), and 30 litres. */
const iracing: Props = { ...lane, [FLAGS]: 64 + 1 + 4, [FUEL]: new Single(30) };
/** The same order with every box cleared, which is iRacing saying no. */
const iracingNothing: Props = { ...lane, [FLAGS]: 0, [FUEL]: new Single(0) };
/** Assetto Corsa, and every sim but iRacing: no order at all. */
const ac: Props = { ...lane };

type Item = { kind: string; name: string; bindings?: Record<string, { formula?: string }> };
const build = (frame = rect(0, 0, 1007, 211), density: Density = 'zone'): Item[] =>
  [...walkItems(MODULES.find((m) => m.id === 'pitView')!.build({ frame, density, prefix: '' }))] as Item[];
const items = build();
const named = (name: string): Item => {
  const found = items.find((it) => it.name === name);
  if (!found) throw new Error(`no item ${name}`);
  return found;
};
/** Whether the item draws: an item with no Visible binding always does. */
const shows = (name: string, props: Props): boolean => {
  const formula = named(name).bindings?.Visible?.formula;
  return formula === undefined ? true : Boolean(evalNcalc(formula, props));
};
const text = (name: string, props: Props): unknown => evalNcalc(named(name).bindings!.Text!.formula!, props);

const CORNERS = ['FrontLeft', 'FrontRight', 'RearLeft', 'RearRight'] as const;
const TOGGLES = ['fastRepair', 'tearOff', ...CORNERS] as const;

describe('on iRacing, the order is drawn as it always was', () => {
  test('the toggles and the tyre word show, the ordered ones lit and the rest off, and there is no notice', () => {
    for (const name of TOGGLES) expect({ name, label: shows(`${name}.label`, iracing) }).toEqual({ name, label: true });
    expect(shows('tyres.label', iracing)).toBe(true);
    expect(shows('tyres.value', iracing)).toBe(true);
    const lit = Object.fromEntries(TOGGLES.map((name) => [name, [shows(`${name}.badge`, iracing), shows(`${name}.off`, iracing)]]));
    expect(lit).toEqual({
      fastRepair: [true, false],
      tearOff: [false, true],
      FrontLeft: [true, false],
      FrontRight: [false, true],
      RearLeft: [true, false],
      RearRight: [false, true],
    });
    expect(shows('service.notice', iracing)).toBe(false);
    expect(text('refuel.value', iracing)).toBe('30.0');
  });

  test('an order of nothing is still an order: every toggle off, and still no notice', () => {
    for (const name of TOGGLES) {
      expect({ name, label: shows(`${name}.label`, iracingNothing), off: shows(`${name}.off`, iracingNothing) }).toEqual({ name, label: true, off: true });
    }
    expect(shows('tyres.value', iracingNothing)).toBe(true);
    expect(shows('service.notice', iracingNothing)).toBe(false);
  });
});

describe('where the sim publishes no order, pit view says so rather than drawing one of nothing', () => {
  test('no toggle, no off state, no tick and no tyre word: the notice stands in their place', () => {
    for (const name of TOGGLES) {
      expect({ name, label: shows(`${name}.label`, ac), off: shows(`${name}.off`, ac), badge: shows(`${name}.badge`, ac), tick: shows(`${name}.tick`, ac) }).toEqual({
        name,
        label: false,
        off: false,
        badge: false,
        tick: false,
      });
    }
    expect(shows('tyres.label', ac)).toBe(false);
    expect(shows('tyres.value', ac)).toBe(false);
    for (const corner of CORNERS) expect({ corner, tick: shows(`car.${corner}.tick`, ac) }).toEqual({ corner, tick: false });
    expect(shows('service.notice', ac)).toBe(true);
  });

  test('the refuel figure is the dash it is for any value that is not there, never 0.00', () => {
    expect(text('refuel.value', ac)).toBe(NO_VALUE);
  });

  test("and the pit time still counts, because it reads SimHub's own pit members", () => {
    expect(shows('pitTime.value', ac)).toBe(true);
    expect(text('pitTime.value', ac)).toBe(text('pitTime.value', iracing));
    expect(text('pitTime.value', ac)).toBe('12.4');
    expect(text('pitTime.value', { ...ac, 'DataCorePlugin.GameData.IsInPitLane': 0 })).toBe('31.0');
  });
});

describe('the notice', () => {
  test('names no sim and no game', () => {
    for (const notice of SERVICE_NOTICES) expect(notice).not.toMatch(/iRacing|Assetto|ACC|Le Mans|LMU|rFactor/i);
  });

  // Every box a module is built into, so that no rectangle the options row is drawn in loses the
  // reason: where the row draws a toggle, the notice is there too.
  for (const box of moduleBoxes()) {
    test(`is drawn wherever the toggles are, on a ${box.name}`, () => {
      const drawn = build(box.frame, box.density);
      const names = new Set(drawn.map((it) => it.name));
      if (names.has('fastRepair.label') || names.has('FrontLeft.label')) expect(names.has('service.notice')).toBe(true);
    });
  }
});
