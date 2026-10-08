/**
 * What `scripts/record.ts` decides without a VM: how a recording is read, how it becomes the
 * committed columnar trace, and what the command line means. Driving SimHub is a remote side
 * effect and is proved by running it.
 */
import { describe, expect, test } from 'bun:test';
import { callsRead, propertiesRead } from '../packages/dash/src/properties.ts';
import { ncalcEvaluator as E, MAX_CARS } from '../packages/generator/src/index.ts';
import { tracedScenarioNames, UNTRACED_SCENARIOS } from './emulator.ts';
import {
  DEFAULT_FRAMES,
  DEFAULT_HZ,
  DEFAULT_WARM_UP_SECONDS,
  PROBE_CALLS,
  PROVENANCE_PROPERTIES,
  RECORDER_VERSION,
  parseArgs,
  parseEmulatorScenario,
  parseRecording,
  parseSimHubVersion,
  recordedCalls,
  recordedProperties,
  request,
  toTrace,
  untracedWarnings,
} from './record.ts';
import { TRACE_DIR, TRACE_VERSION, formatTrace, frame, parseTrace } from './trace.ts';
import type { RecordOptions } from './record.ts';

/** parseArgs answers --help instead of options; every case below passes real arguments. */
const opts = (argv: string[]): RecordOptions => {
  const parsed = parseArgs(argv);
  if ('help' in parsed) throw new Error(`${argv.join(' ')} was answered with the usage text`);
  return parsed;
};

const header = { recorder: 2, scenario: 'green', hz: 10, frames: 3, warmUpTicks: 7200, step: 6, cars: 24, started: '2026-09-13T06:50:00.0000000Z' };

const calls = (gap: number, name: string | null) => ({ 'drivername(2)': name, 'drivergaptoplayer(2)': gap, 'driverlastlap(2)': '00:01:39.1000000', 'drivername(24)': null });

const recording = [
  JSON.stringify(header),
  JSON.stringify({ tick: 300, v: { 'A.Constant': 7, 'B.Moving': 1, 'C.Lap': '00:01:38.4120000' }, c: calls(-1.25, 'L. Byrne') }),
  JSON.stringify({ tick: 306, v: { 'A.Constant': 7, 'B.Moving': 2, 'C.Lap': '00:01:38.4120000' }, c: calls(-1.2, 'L. Byrne') }),
  JSON.stringify({ tick: 312, v: { 'A.Constant': 7, 'B.Moving': 3, 'C.Lap': null }, c: calls(-1.15, 'L. Byrne') }),
  JSON.stringify({ types: { 'C.Lap': 'timespan', 'driverlastlap(2)': 'timespan' } }),
].join('\n');

describe('reading what the recorder wrote', () => {
  test('the header, the frames and the types come back', () => {
    const parsed = parseRecording(`${recording}\n`);
    expect(parsed.header).toEqual(header);
    expect(parsed.frames).toHaveLength(3);
    expect(parsed.frames[1]).toEqual({ tick: 306, v: { 'A.Constant': 7, 'B.Moving': 2, 'C.Lap': '00:01:38.4120000' }, c: calls(-1.2, 'L. Byrne') });
    expect(parsed.types).toEqual({ 'C.Lap': 'timespan', 'driverlastlap(2)': 'timespan' });
  });

  test('a recording with no trailer was interrupted and is refused', () => {
    const short = recording.split('\n').slice(0, 3).join('\n');
    expect(() => parseRecording(short)).toThrow(/stops after 2 of 3 frames/);
  });

  test('a recording that wrote nothing says so, rather than parsing to an empty trace', () => {
    expect(() => parseRecording('')).toThrow(/wrote nothing/);
  });

  test('a format this reader does not know is refused, the first one included, since the recorder is built from this tree', () => {
    expect(RECORDER_VERSION).toBe(2);
    expect(() => parseRecording(`${JSON.stringify({ ...header, recorder: 3 })}\n`)).toThrow(/recorder format/);
    expect(() => parseRecording(`${JSON.stringify({ ...header, recorder: 1 })}\n`)).toThrow(/rebuild the recorder/);
  });
});

describe('transposing a recording into a trace', () => {
  const trace = toTrace(parseRecording(recording), '2026-09-13', '9.12.6');

  test('the header carries the scenario, the shape and where it came from', () => {
    // The first tick is the one the first frame actually came from, not the one asked for: the
    // recorder starts its grid where the game appeared, which no caller can know in advance.
    expect(trace.header).toEqual({ trace: TRACE_VERSION, scenario: 'green', frames: 3, hz: 10, ticks: [300, 6], recorded: '2026-09-13', simHub: '9.12.6', cars: 24 });
    expect(TRACE_VERSION).toBe(2);
  });

  test('a call becomes a call column under its text, after the properties, transposed as a property is', () => {
    expect(trace.columns.map((c) => [c.p, c.k ?? 'property'])).toEqual([
      ['A.Constant', 'property'],
      ['B.Moving', 'property'],
      ['C.Lap', 'property'],
      ['drivergaptoplayer(2)', 'call'],
      ['driverlastlap(2)', 'call'],
      ['drivername(2)', 'call'],
      ['drivername(24)', 'call'],
    ]);
    expect(trace.columns.find((c) => c.p === 'drivergaptoplayer(2)')).toEqual({ p: 'drivergaptoplayer(2)', k: 'call', v: [-1.25, -1.2, -1.15] });
    expect(trace.columns.find((c) => c.p === 'drivername(2)')).toEqual({ p: 'drivername(2)', k: 'call', v: 'L. Byrne' });
    expect(trace.columns.find((c) => c.p === 'driverlastlap(2)')).toEqual({ p: 'driverlastlap(2)', k: 'call', t: 'timespan', v: '00:01:39.1000000' });
    // A position past the end of the field names no car, every frame: one short line.
    expect(trace.columns.find((c) => c.p === 'drivername(24)')).toEqual({ p: 'drivername(24)', k: 'call', v: null });
  });

  test('what it writes is what the reader reads, the calls in the frame beside the properties', () => {
    const back = parseTrace(formatTrace(trace));
    expect(back).toEqual(trace);
    expect(frame(back, 1)).toMatchObject({ 'B.Moving': 2, 'drivername(2)': 'L. Byrne', 'drivergaptoplayer(2)': -1.2 });
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
    const patchy = [JSON.stringify({ ...header, frames: 2 }), JSON.stringify({ tick: 300, v: { 'A.X': 1 }, c: {} }), JSON.stringify({ tick: 306, v: {} }), JSON.stringify({ types: {} })].join('\n');
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
  // Both lists come from composing every package, which is the slow thing in this file and the
  // reason it is asked for out here: a test body is on Bun's five-second clock and collection is
  // not, and a loaded CI runner once took the first test below to 5.1 seconds. `propertiesRead`
  // holds its scan, so asking twice costs one scan either way; this is about which clock it lands on.
  const recorded = recordedProperties();
  const read = propertiesRead();

  test('it is everything the packages read, plus the tick each frame came from', () => {
    const asked = new Set(recorded);
    for (const property of read) expect(asked.has(property)).toBe(true);
    for (const property of PROVENANCE_PROPERTIES) expect(asked.has(property)).toBe(true);
  });

  test('it is sorted and holds no duplicate, since it is written into a request as it is', () => {
    expect(recorded).toEqual([...new Set(recorded)].sort());
  });

  const asked = recordedCalls();
  const made = callsRead();

  test('the calls are every opponent call the packages make, and the probes', () => {
    const set = new Set(asked);
    expect(made.filter((c) => !set.has(c))).toEqual([]);
    expect(PROBE_CALLS.map((p) => p.text).filter((c) => !set.has(c))).toEqual([]);
    expect(asked).toEqual([...new Set(asked)].sort());
    expect(asked.length).toBe(new Set([...made, ...PROBE_CALLS.map((p) => p.text)]).size);
  });

  test('the request names the properties, the calls and the field size they were enumerated over', () => {
    const r = request('green', { hz: 10, frames: 200, warmUpSeconds: 120 }, { properties: recorded, calls: asked });
    expect(r).toMatchObject({ scenario: 'green', hz: 10, frames: 200, step: 6, warmUpTicks: 7200, cars: MAX_CARS });
    expect(r.properties).toEqual(recorded);
    expect(r.calls).toEqual(asked);
  });
});

describe('the probes', () => {
  // A probe is only worth recording if the evaluator can be held to its answer afterwards: it has to
  // parse in the subset the evaluator reads and call nothing it refuses, or the comparison in
  // trace.test.ts could never run.
  for (const probe of PROBE_CALLS) {
    test(`${probe.text} is one the evaluator can answer`, () => {
      const parsed = E.parse(probe.text);
      expect(E.callProblems(parsed)).toEqual([]);
      expect(() => E.evaluateBinding(probe.text, { properties: {} })).not.toThrow();
    });
  }

  test('each is asked once and says what it settles', () => {
    expect(new Set(PROBE_CALLS.map((p) => p.text)).size).toBe(PROBE_CALLS.length);
    for (const probe of PROBE_CALLS) expect(probe.settles.length).toBeGreaterThan(10);
  });
});

describe('the command line', () => {
  test('with no argument it records every scenario a committed trace is expected for', () => {
    expect(parseArgs([])).toEqual({ scenarios: tracedScenarioNames(), hz: DEFAULT_HZ, frames: DEFAULT_FRAMES, warmUpSeconds: DEFAULT_WARM_UP_SECONDS, noBuild: false, keep: false, outDir: TRACE_DIR });
  });

  test('with no argument it leaves out the scenarios meant to have no trace', () => {
    // Each would cost the VM two and a half minutes and write a file trace.test.ts refuses (#519).
    const recorded = opts([]).scenarios;
    for (const name of UNTRACED_SCENARIOS) expect({ name, recorded: recorded.includes(name) }).toEqual({ name, recorded: false });
    expect(untracedWarnings(recorded)).toEqual([]);
  });

  test('an untraced scenario named on its own is recorded, and warned about', () => {
    // Naming one is how a scenario leaves UNTRACED_SCENARIOS, so it is not refused.
    expect(opts(['alerts', 'green']).scenarios).toEqual(['alerts', 'green']);
    const warnings = untracedWarnings(['alerts', 'green']);
    expect(warnings).toHaveLength(1);
    expect(warnings[0]).toMatch(/^alerts is in UNTRACED_SCENARIOS/);
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
