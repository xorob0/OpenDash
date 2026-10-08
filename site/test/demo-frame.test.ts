/**
 * A trace as a property map at any moment (#395): a measured quantity is interpolated between two
 * frames, and nothing else is.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { continuousColumns, Replay } from '../lib/demo/frame.ts';
import { E, parseTrace } from '../lib/demo/ncalc.ts';

const trace = parseTrace(
  [
    JSON.stringify({ trace: 1, scenario: 'test', frames: 3, hz: 10, ticks: [0, 6], recorded: '2026-10-08', simHub: '9.12.6' }),
    JSON.stringify({ p: 'A.Gear', v: ['5', '6', '6'] }),
    JSON.stringify({ p: 'A.Lap', v: [13, 14, 14] }),
    JSON.stringify({ p: 'A.LapTime', t: 'timespan', v: ['00:01:25.0270000', '00:01:25.1270000', '00:01:25.2270000'] }),
    JSON.stringify({ p: 'A.Limiter', v: [true, false, false] }),
    JSON.stringify({ p: 'A.Missing', v: [null, 2.5, 3.5] }),
    JSON.stringify({ p: 'A.Throttle', v: [10, 20.5, 30] }),
    JSON.stringify({ p: 'A.Track', v: 'spa gp' }),
  ].join('\n'),
);

describe('which columns are interpolated', () => {
  test('a column with a fractional number anywhere, and no other', () => {
    expect([...continuousColumns(trace)].sort()).toEqual(['A.Missing', 'A.Throttle']);
  });
});

describe('a moment between two frames', () => {
  const replay = new Replay(trace);
  const at = replay.at(25); // a quarter of the way from frame 0 to frame 1

  test('interpolates a measured quantity linearly, as a double', () => {
    expect(at['A.Throttle']).toEqual(E.double(10 + (20.5 - 10) * 0.25));
  });

  test('takes the nearest frame for a count, a string, a boolean and a TimeSpan', () => {
    expect(at['A.Lap']).toEqual(E.int(13));
    expect(at['A.Gear']).toBe('5');
    expect(at['A.Limiter']).toBe(true);
    expect(at['A.LapTime']).toEqual(E.parseTimeSpan('00:01:25.0270000'));
    expect(at['A.Track']).toBe('spa gp');
    const later = replay.at(75);
    expect(later['A.Lap']).toEqual(E.int(14));
    expect(later['A.Gear']).toBe('6');
    expect(later['A.LapTime']).toEqual(E.parseTimeSpan('00:01:25.1270000'));
  });

  test('never interpolates from a null: the nearest frame says whether there is a value', () => {
    expect(at['A.Missing']).toBeNull();
    expect(replay.at(75)['A.Missing']).toEqual(E.double(2.5));
  });

  test('clamps before the first frame and after the last', () => {
    expect(replay.duration).toBe(200);
    expect(replay.at(-50)['A.Throttle']).toEqual(E.double(10));
    expect(replay.at(10_000)['A.Throttle']).toEqual(E.double(30));
  });
});

describe('the race trace the demo replays', () => {
  const race = new Replay(parseTrace(readFileSync(path.resolve(import.meta.dir, '..', '..', 'traces', 'race.ndjson'), 'utf8')));

  test('reads at every tick of its length without a value the evaluator cannot hold', () => {
    for (let t = 0; t <= race.duration; t += 50) {
      for (const v of Object.values(race.at(t))) expect(() => E.toValue(v)).not.toThrow();
    }
  });

  test('sweeps the revs it records as a percentage, and steps the gear', () => {
    const name = 'DataCorePlugin.GameData.CarSettings_CurrentDisplayedRPMPercent';
    const a = race.at(0)[name] as E.NumberValue;
    const b = race.at(50)[name] as E.NumberValue;
    const c = race.at(100)[name] as E.NumberValue;
    expect(b.value).toBeCloseTo((a.value + c.value) / 2, 6);
    expect(race.at(50)['DataCorePlugin.GameRawData.Telemetry.Gear']).toEqual(E.int(6));
  });
});
