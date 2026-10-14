/**
 * The pit stop's time, timed in the box on every sim (#1130).
 *
 * SimHub times a stop from its `IsInPit`, publishing `IsInPitSince` while it holds and
 * `LastPitStopDuration` when it drops. On iRacing, Assetto Corsa and ACC that is the box. On Le Mans
 * Ultimate the reader answers it with the whole pit lane, so pit view's `Pit time` ran from the entry
 * line, a 30 s stop down a 25 s lane read nearer a minute, and the service gauge, which divides one by
 * the other, read a stop as further through than it was. The plugin times the box there and publishes
 * `OpenDash.PitStopSeconds` and `OpenDash.LastPitStopSeconds`, and null where SimHub's own timer is
 * already the box. These evaluate what the build emits with both, and with neither.
 */
import { describe, expect, test } from 'bun:test';
import { PIT_STOP_SECONDS, LAST_PIT_STOP_SECONDS, PIT_STOP_TIMES, declaredProperties, propertyName } from '../src/contract.ts';
import { rect } from '../src/design/geometry.ts';
import type { TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { inPitSeconds, lastPitDuration, pitServiceProgress } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const IN_LANE = 'DataCorePlugin.GameData.IsInPitLane';
const SIMHUB_SINCE = 'DataCorePlugin.GameData.IsInPitSince';
const SIMHUB_LAST = 'DataCorePlugin.GameData.LastPitStopDuration';
const PLUGIN_SINCE = `OpenDash.${PIT_STOP_SECONDS}`;
const PLUGIN_LAST = `OpenDash.${LAST_PIT_STOP_SECONDS}`;

const pitTime = (): string => {
  const items = [...walkItems(MODULES.find((m) => m.id === 'pitView')!.build({ frame: rect(0, 0, 1007, 211), density: 'zone', prefix: '' }))];
  const value = items.find((item): item is TextItem => item.kind === 'text' && item.name === 'pitTime.value');
  const formula = value?.bindings?.Text?.formula;
  if (typeof formula !== 'string') throw new Error('pit view has no bound Pit time');
  return formula;
};

/**
 * Twelve and a half seconds stopped in the box after twenty-five down the lane, with a last stop of 30.5 s
 * in the box and 55.5 s from line to line: what Le Mans Ultimate's SimHub and the plugin each publish.
 */
const lmu = (inLane: boolean): Props => ({
  [IN_LANE]: inLane ? 1 : 0,
  [SIMHUB_SINCE]: inLane ? 37.5 : 0,
  [SIMHUB_LAST]: 55.5,
  [PLUGIN_SINCE]: inLane ? 12.5 : 0,
  [PLUGIN_LAST]: 30.5,
});

/** iRacing's stop, the box by SimHub's own timer, with the plugin publishing null, or no plugin at all. */
const iracing = (inLane: boolean, plugin: 'null' | 'absent'): Props => ({
  [IN_LANE]: inLane ? 1 : 0,
  [SIMHUB_SINCE]: inLane ? 12.5 : 0,
  [SIMHUB_LAST]: 30.5,
  ...(plugin === 'null' ? { [PLUGIN_SINCE]: null, [PLUGIN_LAST]: null } : {}),
});

describe('the pit stop time', () => {
  test("where the plugin times the box, Pit time is the box's and not the lane's", () => {
    expect(evalNcalc(pitTime(), lmu(true))).toBe('12.5');
    expect(evalNcalc(pitTime(), lmu(false))).toBe('30.5');
  });

  test('where it publishes nothing, Pit time is SimHub\'s own, as it was before', () => {
    for (const plugin of ['null', 'absent'] as const) {
      expect({ plugin, inLane: evalNcalc(pitTime(), iracing(true, plugin)) }).toEqual({ plugin, inLane: '12.5' });
      expect({ plugin, out: evalNcalc(pitTime(), iracing(false, plugin)) }).toEqual({ plugin, out: '30.5' });
    }
    // And nothing at all is a level zero rather than an error, as it always was.
    expect(evalNcalc(inPitSeconds(), {})).toBe(0);
    expect(evalNcalc(lastPitDuration(), {})).toBe(0);
  });

  test("the service gauge divides the box's time by the last stop's box time", () => {
    // 12.5 of 30.5 is 41 percent; SimHub's lane times on LMU, 37.5 of 55.5, would have read 68.
    expect(Number(evalNcalc(pitServiceProgress(), lmu(true)))).toBeCloseTo((100 * 12.5) / 30.5, 6);
    expect(Number(evalNcalc(pitServiceProgress(), iracing(true, 'null')))).toBeCloseTo((100 * 12.5) / 30.5, 6);
  });

  test('both are declared under the names the plugin attaches', () => {
    expect([...PIT_STOP_TIMES]).toEqual(['PitStopSeconds', 'LastPitStopSeconds']);
    const declared = declaredProperties();
    for (const time of PIT_STOP_TIMES) expect({ time, declared: declared.includes(propertyName(time)) }).toEqual({ time, declared: true });
  });
});
