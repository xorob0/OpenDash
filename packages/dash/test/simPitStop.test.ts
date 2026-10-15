/**
 * The pit view's stop time is the stop in the box on every sim, #1130.
 *
 * SimHub times a stop from its `IsInPit`, which is the box on iRacing, Assetto Corsa and ACC and the whole
 * pit lane on Le Mans Ultimate, so there `IsInPitSince` runs from the entry line and `LastPitStopDuration`
 * is the lane. The plugin times the box from the player's `mPitState` and publishes it as
 * `OpenDash.PitStopSeconds` and `OpenDash.LastPitStopSeconds`, null on the sims whose own timer is right.
 * Here the pit view's `Pit time` and its progress gauge are evaluated over a frame of each sim as SimHub
 * and the plugin would publish it. There is no Le Mans Ultimate emulator, so its frames are written by hand
 * from `docs/research/sims.md`, as the flags' are in `simFlags.test.ts`.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { ncalcEvaluator as E, type Item } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { walkItems } from '../src/walk.ts';

type Frame = Record<string, unknown>;

/** The pit view at the wide zone body, which keeps every field. */
const items = (): Item[] => {
  const module = MODULES.find((m) => m.id === 'pitView')!;
  return [...walkItems(module.build({ frame: rect(0, 0, 1007, 211), density: 'zone', prefix: '' }))];
};

const formula = (name: string, binding: string): string => {
  const item = items().find((i) => i.name === name) as { bindings?: Record<string, { formula?: unknown }> } | undefined;
  const f = item?.bindings?.[binding]?.formula;
  if (typeof f !== 'string') throw new Error(`no ${binding} formula on ${name}`);
  return f;
};

const pitTime = (frame: Frame): unknown => E.toJs(E.evaluate(formula('pitTime.value', 'Text'), { properties: frame }));
const progress = (frame: Frame): unknown => E.toJs(E.evaluate(formula('gauge', 'Value'), { properties: frame }));

/**
 * SimHub's own timer, as it publishes it: whether the car is in the lane, the seconds since its `IsInPit`
 * became true and the length of the last spell it was. Doubles, as the pit trace records them.
 */
const simHub = (inLane: boolean, since: number, last: number): Frame => ({
  'DataCorePlugin.GameData.IsInPitLane': inLane ? 1 : 0,
  'DataCorePlugin.GameData.IsInPitSince': E.double(since),
  'DataCorePlugin.GameData.LastPitStopDuration': E.double(last),
});

/** The plugin's box timer, as it publishes it on Le Mans Ultimate. */
const plugin = (seconds: number, last: number): Frame => ({
  'OpenDash.PitStopSeconds': E.double(seconds),
  'OpenDash.LastPitStopSeconds': E.double(last),
});

describe('on Le Mans Ultimate the stop is the time in the box, not the lane, #1130', () => {
  test('in the box, the stop counts from the box: 12.3 s stopped, after 25 s down the lane', () => {
    // SimHub's IsInPitSince is mInPits, the lane, so it reads the 25 s from the entry line as well.
    expect(pitTime({ ...simHub(true, 37.3, 0), ...plugin(12.3, 0) })).toBe('12.3');
  });

  test('down the lane before the box, no stop is under way yet', () => {
    expect(pitTime({ ...simHub(true, 14, 0), ...plugin(0, 0) })).toBe('0.0');
  });

  test('after the stop, its time is the 30 s in the box rather than the 67 s from the entry line to the exit', () => {
    expect(pitTime({ ...simHub(false, 0, 67), ...plugin(0, 30) })).toBe('30.0');
  });

  test('the progress gauge is the stop under way over the last stop, both in the box', () => {
    // 15 s into the second stop, the first having taken 30 s in the box and 67 s in the lane: half done,
    // where SimHub's lane timer would read 40 s over 67 s.
    expect(progress({ ...simHub(true, 40, 67), ...plugin(15, 30) })).toBe(50);
  });
});

describe('where SimHub times the box itself, or with no plugin, the pit view reads SimHub as it always did', () => {
  // iRacing's, Assetto Corsa's and ACC's IsInPit are the box, and the plugin publishes null there, which a
  // frame reads as the property being absent; a package with no plugin reads the same.
  test('the stop under way is SimHub\'s IsInPitSince', () => {
    expect(pitTime(simHub(true, 24.3, 0))).toBe('24.3');
    expect(pitTime({ ...simHub(true, 24.3, 0), 'OpenDash.PitStopSeconds': null, 'OpenDash.LastPitStopSeconds': null })).toBe('24.3');
  });

  test('the last stop is SimHub\'s LastPitStopDuration', () => {
    expect(pitTime(simHub(false, 0, 28.6))).toBe('28.6');
  });

  test('the gauge is SimHub\'s two over each other', () => {
    expect(progress(simHub(true, 5, 8))).toBe(62.5);
  });

  test('and with nothing published at all, the stop reads 0.0', () => {
    expect(pitTime({})).toBe('0.0');
  });
});
