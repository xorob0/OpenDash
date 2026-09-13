/**
 * What `scripts/record.ts` decides without a VM: how a recording is read, how it becomes the
 * committed columnar trace, and what the command line means. Driving SimHub is a remote side
 * effect and is proved by running it.
 */
import { describe, expect, test } from 'bun:test';
import { propertiesRead } from '../packages/dash/src/properties.ts';
import { scenarios } from './emulator.ts';
import {
  DEFAULT_FRAMES,
  DEFAULT_HZ,
  DEFAULT_WARM_UP_SECONDS,
  PROVENANCE_PROPERTIES,
  parseArgs,
  parseEmulatorScenario,
  parseRecording,
  parseSimHubVersion,
  recordedProperties,
  toTrace,
} from './record.ts';
import { TRACE_DIR, TRACE_VERSION } from './trace.ts';
import type { RecordOptions } from './record.ts';

/** parseArgs answers --help instead of options; every case below passes real arguments. */
const opts = (argv: string[]): RecordOptions => {
  const parsed = parseArgs(argv);
  if ('help' in parsed) throw new Error(`${argv.join(' ')} was answered with the usage text`);
  return parsed;
};

const header = { recorder: 1, scenario: 'green', hz: 10, frames: 3, warmUpTicks: 7200, step: 6, started: '2026-09-13T06:50:00.0000000Z' };

const recording = [
  JSON.stringify(header),
  JSON.stringify({ tick: 300, v: { 'A.Constant': 7, 'B.Moving': 1, 'C.Lap': '00:01:38.4120000' } }),
  JSON.stringify({ tick: 306, v: { 'A.Constant': 7, 'B.Moving': 2, 'C.Lap': '00:01:38.4120000' } }),
  JSON.stringify({ tick: 312, v: { 'A.Constant': 7, 'B.Moving': 3, 'C.Lap': null } }),
  JSON.stringify({ types: { 'C.Lap': 'timespan' } }),
].join('\n');

describe('reading what the recorder wrote', () => {
  test('the header, the frames and the types come back', () => {
    const parsed = parseRecording(`${recording}\n`);
    expect(parsed.header).toEqual(header);
    expect(parsed.frames).toHaveLength(3);
    expect(parsed.frames[1]).toEqual({ tick: 306, v: { 'A.Constant': 7, 'B.Moving': 2, 'C.Lap': '00:01:38.4120000' } });
    expect(parsed.types).toEqual({ 'C.Lap': 'timespan' });
  });

  test('a recording with no trailer was interrupted and is refused', () => {
    const short = recording.split('\n').slice(0, 3).join('\n');
    expect(() => parseRecording(short)).toThrow(/stops after 2 of 3 frames/);
  });

  test('a recording that wrote nothing says so, rather than parsing to an empty trace', () => {
    expect(() => parseRecording('')).toThrow(/wrote nothing/);
  });

  test('a format this reader does not know is refused', () => {
    expect(() => parseRecording(`${JSON.stringify({ ...header, recorder: 2 })}\n`)).toThrow(/recorder format/);
  });
});

describe('transposing a recording into a trace', () => {
  const trace = toTrace(parseRecording(recording), '2026-09-13', '9.12.6');

  test('the header carries the scenario, the shape and where it came from', () => {
    // The first tick is the one the first frame actually came from, not the one asked for: the
    // recorder starts its grid where the game appeared, which no caller can know in advance.
    expect(trace.header).toEqual({ trace: TRACE_VERSION, scenario: 'green', frames: 3, hz: 10, ticks: [300, 6], recorded: '2026-09-13', simHub: '9.12.6' });
  });

  test('a property that never moved becomes one value, which is what keeps a trace small', () => {
    expect(trace.columns.find((c) => c.p === 'A.Constant')).toEqual({ p: 'A.Constant', v: 7 });
  });

  test('a property that moved keeps one value per frame', () => {
    expect(trace.columns.find((c) => c.p === 'B.Moving')).toEqual({ p: 'B.Moving', v: [1, 2, 3] });
  });

  test('a property whose type a scalar would lose keeps it', () => {
    expect(trace.columns.find((c) => c.p === 'C.Lap')).toEqual({ p: 'C.Lap', t: 'timespan', v: ['00:01:38.4120000', '00:01:38.4120000', null] });
  });

  test('the first tick is the one the first frame came from, whatever was asked for', () => {
    // The recorder starts its grid where SimHub first reported the game running, which is a tick no
    // caller can know in advance: the emulator's SessionTick begins at the scenario's own session
    // time. A header that repeated the request would say the trace began somewhere it did not.
    const late = [
      JSON.stringify({ ...header, frames: 2 }),
      JSON.stringify({ tick: 72721, v: { 'A.X': 1 } }),
      JSON.stringify({ tick: 72727, v: { 'A.X': 2 } }),
      JSON.stringify({ types: {} }),
    ].join('\n');
    expect(toTrace(parseRecording(late), '2026-09-13', '9.12.6').header.ticks).toEqual([72721, 6]);
  });

  test('a property missing from a frame is recorded as null rather than dropped', () => {
    const patchy = [JSON.stringify({ ...header, frames: 2 }), JSON.stringify({ tick: 300, v: { 'A.X': 1 } }), JSON.stringify({ tick: 306, v: {} }), JSON.stringify({ types: {} })].join('\n');
    expect(toTrace(parseRecording(patchy), '2026-09-13', '9.12.6').columns).toEqual([{ p: 'A.X', v: [1, null] }]);
  });
});

describe('what the VM says it is running', () => {
  test("SimHub's version comes off the line it writes when it starts", () => {
    // Neither the file version nor the assembly version of SimHubWPF.exe carries it: both read
    // 1.0.0.0 on 9.12.6, which is how a trace came to claim it had been recorded against 1.0.0.0.
    expect(parseSimHubVersion('[2026-09-13 06:47:53,616] INFO - Starting SimHub v9.12.6 (build time : 09/09/2026 14:18:48)')).toBe('9.12.6');
    expect(parseSimHubVersion('== C:\\Program Files (x86)\\SimHub\\Logs\\SimHub.txt ==\nnothing about a version')).toBe('unknown');
  });

  test('the emulator names the scenario file it loaded, and the last run is the one running', () => {
    const log = [
      String.raw`IrsdkEmulator - scenario 'Race' (C:\Temp\irsdk-emulator\scenarios\race.json)`,
      String.raw`IrsdkEmulator - scenario 'Green flag race lap, P3 of 24' (C:\Temp\irsdk-emulator\scenarios\green.json)`,
    ].join('\n');
    expect(parseEmulatorScenario(log)).toBe('green');
    expect(parseEmulatorScenario('[   61.0s] tick 3660 | rpm 4528')).toBeUndefined();
  });
});

describe('what a recording asks SimHub for', () => {
  test('it is everything the packages read, plus the tick each frame came from', () => {
    const recorded = new Set(recordedProperties());
    for (const property of propertiesRead()) expect(recorded.has(property)).toBe(true);
    for (const property of PROVENANCE_PROPERTIES) expect(recorded.has(property)).toBe(true);
  });

  test('it is sorted and holds no duplicate, since it is written into a request as it is', () => {
    const recorded = recordedProperties();
    expect(recorded).toEqual([...new Set(recorded)].sort());
  });
});

describe('the command line', () => {
  test('with no argument it records every scenario the emulator ships', () => {
    expect(parseArgs([])).toEqual({ scenarios: scenarios(), hz: DEFAULT_HZ, frames: DEFAULT_FRAMES, warmUpSeconds: DEFAULT_WARM_UP_SECONDS, noBuild: false, keep: false, outDir: TRACE_DIR });
  });

  test('scenarios are positional and flags are not mistaken for them', () => {
    expect(opts(['green', 'pit', '--frames', '60', '--no-build']).scenarios).toEqual(['green', 'pit']);
    expect(opts(['green', '--frames', '60']).frames).toBe(60);
    expect(opts(['green', '--frames=60']).frames).toBe(60);
    expect(opts(['--hz', '30', 'green']).hz).toBe(30);
  });

  test('a count that is not a whole number is refused rather than silently defaulted', () => {
    expect(() => parseArgs(['--frames', 'lots'])).toThrow();
    expect(() => parseArgs(['--hz', '0'])).toThrow();
  });

  test('a rate the emulator tick does not divide is refused, since the header would then be a lie', () => {
    expect(() => parseArgs(['--hz', '7'])).toThrow(/60/);
    expect(opts(['--hz', '20']).hz).toBe(20);
  });

  test('--help is answered rather than recorded', () => {
    expect(parseArgs(['--help'])).toEqual({ help: true });
  });
});
