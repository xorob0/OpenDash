/**
 * The trace format, and the promise the committed traces make: that everything the packages read
 * is in every one of them.
 *
 * That last test is the point of the whole thing. A trace is recorded once, on the Windows VM, and
 * replayed by everything afterwards, so nothing except this test would notice a package that
 * started reading a property no trace carries. It would render as a blank field in a video that
 * nobody thinks to distrust.
 *
 * Version 2 makes the same promise for the opponent calls (#257): every call `callsRead()` lists has
 * a column in every trace, and so does every probe, whose recorded answer the evaluator is held to.
 * Those tests wait for the re-record: while a committed trace is still version 1 they say so by name
 * and pass, rather than fail on a file nobody here can re-record without the VM.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync, statSync } from 'node:fs';
import { callsRead, propertiesRead } from '../packages/dash/src/properties.ts';
import { ncalcEvaluator as E, MAX_CARS } from '../packages/generator/src/index.ts';
import { scenarios, tracedScenarioNames, UNTRACED_SCENARIOS } from './emulator.ts';
import { PROBE_CALLS, PROVENANCE_PROPERTIES } from './record.ts';
import {
  READABLE_TRACE_VERSIONS,
  TRACE_VERSION,
  callsOf,
  formatTrace,
  frame,
  parseTrace,
  propertiesOf,
  readTrace,
  traceFile,
  tracedScenarios,
  type Trace,
  type TraceColumn,
  type TraceValue,
} from './trace.ts';

const header = { trace: TRACE_VERSION, scenario: 'sample', frames: 3, hz: 10, ticks: [300, 6] as [number, number], recorded: '2026-09-13', simHub: '9.12.6', cars: 24 };

const sample: Trace = {
  header,
  columns: [
    { p: 'A.Constant', v: 7 },
    { p: 'B.Moving', v: [1, 2, 3] },
    { p: 'C.Lap', t: 'timespan', v: ['00:01:38.4120000', '00:01:38.4120000', '00:01:39.0000000'] },
    { p: 'D.Missing', v: [null, 'x', true] },
    { p: 'drivergaptoplayer(2)', k: 'call', v: [-1.25, -1.2, -1.15] },
    { p: 'driverlastlap(2)', k: 'call', t: 'timespan', v: '00:01:39.1000000' },
    { p: 'drivername(2)', k: 'call', v: 'L. Byrne' },
    { p: 'drivername(24)', k: 'call', v: null },
  ],
};

/** The same sample as a version 1 trace: no `cars`, no calls. */
const { cars: _cars, ...v1Header } = header;
const sampleV1: Trace = { header: { ...v1Header, trace: 1 }, columns: sample.columns.filter((c) => c.k !== 'call') };

const lines = (trace: Trace): string[] => formatTrace(trace).trimEnd().split('\n');

describe('the format', () => {
  test('a trace survives being written and read back', () => {
    expect(parseTrace(formatTrace(sample))).toEqual(sample);
  });

  test('it is one line of header and one line per property, sorted, with a trailing newline', () => {
    expect(lines(sample)).toHaveLength(9);
    expect(JSON.parse(lines(sample)[0] as string)).toEqual(header);
    expect(formatTrace(sample).endsWith('\n')).toBe(true);
    expect(propertiesOf(parseTrace(formatTrace(sample)))).toEqual(['A.Constant', 'B.Moving', 'C.Lap', 'D.Missing']);
  });

  test('the calls follow the properties, sorted by text, each marked as a call', () => {
    expect(callsOf(parseTrace(formatTrace(sample)))).toEqual(['drivergaptoplayer(2)', 'driverlastlap(2)', 'drivername(2)', 'drivername(24)']);
    expect(lines(sample)[5]).toBe('{"p":"drivergaptoplayer(2)","k":"call","v":[-1.25,-1.2,-1.15]}');
    expect(lines(sample)[6]).toBe('{"p":"driverlastlap(2)","k":"call","t":"timespan","v":"00:01:39.1000000"}');
  });

  test('a call sorts after every property, whatever its text', () => {
    // A probe can begin with a quote or a digit, which sorts before any capital; it still follows the properties.
    const probe: Trace = { header, columns: [{ p: 'B.X', v: 1 }, { p: "'3' = 3", k: 'call', v: true }] };
    expect(lines(probe).slice(1).map((l) => JSON.parse(l).p)).toEqual(['B.X', "'3' = 3"]);
    expect(parseTrace(formatTrace(probe))).toEqual(probe);
  });

  test('a version 1 trace is still read, as one with no calls, while the committed ones wait to be re-recorded', () => {
    expect(TRACE_VERSION).toBe(2);
    expect(READABLE_TRACE_VERSIONS).toEqual([1, 2]);
    const back = parseTrace(formatTrace(sampleV1));
    expect(back).toEqual(sampleV1);
    expect(callsOf(back)).toEqual([]);
    expect(back.header.cars).toBeUndefined();
  });

  test('writing sorts the columns, so a recorder cannot commit them in its own order', () => {
    const shuffled: Trace = { header, columns: [...sample.columns].reverse() };
    expect(lines(shuffled)).toEqual(lines(sample));
  });

  test('a column keeps its type only when a JSON scalar would lose it', () => {
    expect(lines(sample)[1]).toBe('{"p":"A.Constant","v":7}');
    expect(lines(sample)[3]).toContain('"t":"timespan"');
  });

  test('a header naming a hand-written column keeps that list, and one naming none stays silent', () => {
    const marked: Trace = { ...sample, header: { ...header, asserted: ['A.Constant'] } };
    expect(parseTrace(formatTrace(marked)).header.asserted).toEqual(['A.Constant']);
    expect(lines(sample)[0]).not.toContain('asserted');
  });

  const rejected: [string, string][] = [
    ['an empty file', ''],
    ['a first line that is not JSON', 'not json\n'],
    ['a version this reader does not know', `${JSON.stringify({ ...header, trace: 99 })}\n{"p":"A","v":1}\n`],
    ['a header with no scenario', `${JSON.stringify({ ...header, scenario: '' })}\n{"p":"A","v":1}\n`],
    ['a header with no frames', `${JSON.stringify({ ...header, frames: 0 })}\n{"p":"A","v":1}\n`],
    ['a header whose ticks are not a pair', `${JSON.stringify({ ...header, ticks: [1, 2, 3] })}\n{"p":"A","v":1}\n`],
    ['a header and no property', `${JSON.stringify(header)}\n`],
    ['a header and calls only', `${JSON.stringify(header)}\n{"p":"drivername(1)","k":"call","v":"A"}\n`],
    ['a version 2 header that does not say how many cars', `${JSON.stringify(v1Header)}\n{"p":"A","v":1}\n`],
    ['a version 2 header whose cars is not a whole number', `${JSON.stringify({ ...header, cars: 2.5 })}\n{"p":"A","v":1}\n`],
    ['a version 1 header that says how many cars', `${JSON.stringify({ ...header, trace: 1 })}\n{"p":"A","v":1}\n`],
    ['a call column in a version 1 trace', `${JSON.stringify({ ...v1Header, trace: 1 })}\n{"p":"A","v":1}\n{"p":"drivername(1)","k":"call","v":"A"}\n`],
    ['a column of a kind nothing understands', `${JSON.stringify(header)}\n{"p":"A","k":"leaderboard","v":1}\n`],
    ['a call before a property', `${JSON.stringify(header)}\n{"p":"drivername(1)","k":"call","v":"A"}\n{"p":"A","v":1}\n`],
    ['calls out of order', `${JSON.stringify(header)}\n{"p":"A","v":1}\n{"p":"drivername(2)","k":"call","v":"B"}\n{"p":"drivername(1)","k":"call","v":"A"}\n`],
    ['a call and a property of the same name', `${JSON.stringify(header)}\n{"p":"A","v":1}\n{"p":"A","k":"call","v":1}\n`],
    ['a column with no property name', `${JSON.stringify(header)}\n{"v":1}\n`],
    ['two columns for one property', `${JSON.stringify(header)}\n{"p":"A","v":1}\n{"p":"A","v":2}\n`],
    ['columns out of order', `${JSON.stringify(header)}\n{"p":"B","v":1}\n{"p":"A","v":2}\n`],
    ['an array of the wrong length', `${JSON.stringify(header)}\n{"p":"A","v":[1,2]}\n`],
    ['a value that is not a scalar', `${JSON.stringify(header)}\n{"p":"A","v":{"nested":1}}\n`],
    ['a type nothing understands', `${JSON.stringify(header)}\n{"p":"A","t":"duration","v":1}\n`],
    ['an asserted list that is not a list of names', `${JSON.stringify({ ...header, asserted: 'A' })}\n{"p":"A","v":1}\n`],
  ];
  for (const [what, text] of rejected) {
    test(`it refuses ${what}`, () => {
      expect(() => parseTrace(text)).toThrow();
    });
  }
});

describe('reading a frame', () => {
  test('a property that never moved gives its one value for every frame', () => {
    expect(frame(sample, 0)['A.Constant']).toBe(7);
    expect(frame(sample, 2)['A.Constant']).toBe(7);
  });

  test('a property that moved gives that frame’s value, nulls included', () => {
    expect(frame(sampleV1, 1)).toEqual({ 'A.Constant': 7, 'B.Moving': 2, 'C.Lap': '00:01:38.4120000', 'D.Missing': 'x' });
    expect(frame(sample, 0)['D.Missing']).toBeNull();
  });

  test('a call is in the same map under its text, which is where the evaluator looks for it', () => {
    expect(frame(sample, 1)).toEqual({
      'A.Constant': 7,
      'B.Moving': 2,
      'C.Lap': '00:01:38.4120000',
      'D.Missing': 'x',
      'drivergaptoplayer(2)': -1.2,
      'driverlastlap(2)': '00:01:39.1000000',
      'drivername(2)': 'L. Byrne',
      'drivername(24)': null,
    });
    expect(E.evaluate('drivername(1 + 1)', { properties: frame(sample, 2) })).toBe('L. Byrne');
    expect(E.toJs(E.evaluate('drivergaptoplayer(2)', { properties: frame(sample, 2) }))).toBe(-1.15);
  });

  test('a frame outside the trace is an error rather than a map of undefined', () => {
    expect(() => frame(sample, 3)).toThrow();
    expect(() => frame(sample, -1)).toThrow();
  });
});

describe('the format a browser can read', () => {
  // The demo (#395) bundles the reader into a page, where node:fs does not exist: one import of it
  // at the top of traceFormat.ts and the page fails to build, so the file is held to having none.
  test('traceFormat.ts imports nothing from node', () => {
    const source = readFileSync(new URL('./traceFormat.ts', import.meta.url), 'utf8');
    expect(source).not.toMatch(/from ['"](node:|fs['"]|path['"])/);
    expect(source).not.toMatch(/import\.meta\.dir/);
  });
});

/**
 * Whether a probe's recorded answer is the one the evaluator gives. A throw in SimHub is recorded as
 * null, which is what the dash draws, so a runtime failure here matches a null; numbers are compared
 * at the four decimals the recorder rounds to, and a TimeSpan by its ticks.
 */
function sameAnswer(column: TraceColumn, recorded: TraceValue): { recorded: TraceValue; evaluated: unknown; same: boolean } {
  const result = E.evaluateBinding(column.p, { properties: {} });
  const value = result.ok ? result.value : null;
  if (recorded === null || value === null) return { recorded, evaluated: value === null ? null : E.toJs(value), same: recorded === null && value === null };
  if (column.t === 'timespan') {
    const ticks = E.parseTimeSpan(String(recorded)).ticks;
    return { recorded, evaluated: E.isTimeSpan(value) ? E.timeSpanToString(value) : E.toJs(value), same: E.isTimeSpan(value) && ticks === value.ticks };
  }
  const evaluated = E.toJs(value);
  if (typeof recorded === 'number' && typeof evaluated === 'number') return { recorded, evaluated, same: Math.round(evaluated * 1e4) / 1e4 === recorded };
  return { recorded, evaluated, same: evaluated === recorded };
}

describe('the committed traces', () => {
  const required = propertiesRead();
  const calls = callsRead();

  test('there is one for every scenario the emulator ships', () => {
    // Every scenario except the ones that drive lights rather than a dashboard; see
    // UNTRACED_SCENARIOS for why the flag box has no trace and is not going to get one.
    expect(tracedScenarios()).toEqual(tracedScenarioNames());
    for (const name of UNTRACED_SCENARIOS) {
      expect({ name, runnable: scenarios().includes(name) }).toEqual({ name, runnable: true });
      expect({ name, traced: tracedScenarios().includes(name) }).toEqual({ name, traced: false });
    }
  });

  for (const scenario of tracedScenarios()) {
    describe(scenario, () => {
      const trace = readTrace(scenario);

      test('it is a trace of that scenario', () => {
        expect(trace.header.scenario).toBe(scenario);
        expect(trace.header.frames).toBeGreaterThan(0);
      });

      test('it carries every property any binding of any package reads', () => {
        const present = new Set(propertiesOf(trace));
        const missing = required.filter((p) => !present.has(p));
        // A failure here is not a broken trace, it is an out-of-date one: something now reads a
        // property that was not recorded. Re-record with `bun run record <scenario>`.
        expect({ scenario, missing }).toEqual({ scenario, missing: [] });
      });

      /**
       * The test above is the one that demands a trip to the VM, and the honest way to satisfy it
       * without one is to say so rather than to type a column in quietly. `asserted` is where a
       * hand-written column is named, and this is what keeps that claim worth reading.
       *
       * A re-record removes the entry by itself, `toTrace` building the header from scratch, and
       * `recordedProperties()` derives the list it asks SimHub for from `propertiesRead()`, so
       * anything a binding reads is picked up. What is checked here is that an asserted name is
       * still a property something reads, that the column it names is really in the file, and that
       * it is a constant -- nobody can honestly hand-write two hundred frames of a moving value.
       */
      test('any column it says was typed rather than observed is named, present and constant', () => {
        const asserted = trace.header.asserted ?? [];
        for (const name of asserted) {
          const column = trace.columns.find((c) => c.p === name);
          expect({ scenario, name, present: column !== undefined }).toEqual({ scenario, name, present: true });
          expect({ scenario, name, moves: Array.isArray(column?.v) }).toEqual({ scenario, name, moves: false });
          // Still read by something. An entry for a property nothing reads any more is an entry
          // that should have gone rather than one a re-record will confirm.
          expect({ scenario, name, read: required.includes(name) }).toEqual({ scenario, name, read: true });
        }
      });

      test('it records which tick each frame came from, so a re-recording is comparable', () => {
        for (const property of PROVENANCE_PROPERTIES) expect(propertiesOf(trace)).toContain(property);
      });

      // The opponent calls and the probes arrive with version 2, which the committed traces reach when
      // they are re-recorded on the VM (#257). Until then this says which file is waiting, by name.
      const v2 = trace.header.trace >= 2;
      test.if(!v2)(`skipped: traces/${scenario}.ndjson is trace version ${trace.header.trace} with no call columns; re-record it with \`bun run record ${scenario}\` on the VM (#257)`, () => {});

      test.if(v2)('its calls were enumerated over the field size the evaluator assumes', () => {
        expect(trace.header.cars).toBe(MAX_CARS);
      });

      test.if(v2)('it carries every opponent call any binding of any package can make', () => {
        const present = new Set(callsOf(trace));
        const missing = calls.filter((c) => !present.has(c));
        // Out of date rather than broken: a package now makes a call that was not recorded.
        expect({ scenario, missing: missing.slice(0, 20), count: missing.length }).toEqual({ scenario, missing: [], count: 0 });
      });

      test.if(v2)('it carries every probe', () => {
        const present = new Set(callsOf(trace));
        expect({ scenario, missing: PROBE_CALLS.map((p) => p.text).filter((p) => !present.has(p)) }).toEqual({ scenario, missing: [] });
      });

      /**
       * What the probes are for. Each asks SimHub's NCalc a question the evaluator could only answer
       * from the source; the recording is SimHub's answer, and a difference here is a reading in
       * `packages/generator/src/ncalc/` marked Unverified that turned out wrong. Fix the evaluator,
       * and its unit test, rather than this.
       */
      test.if(v2)('the evaluator gives every probe the answer SimHub recorded', () => {
        const wrong = [];
        for (const probe of PROBE_CALLS) {
          const column = trace.columns.find((c) => c.k === 'call' && c.p === probe.text);
          if (!column) continue;
          const answer = sameAnswer(column, Array.isArray(column.v) ? (column.v[0] as TraceValue) : column.v);
          if (!answer.same) wrong.push({ probe: probe.text, settles: probe.settles, recorded: answer.recorded, evaluated: answer.evaluated });
        }
        expect({ scenario, wrong }).toEqual({ scenario, wrong: [] });
      });

      test('it is small enough to commit', () => {
        // Two hundred frames of two hundred properties. Well past this and the columnar shape has
        // stopped working, which is worth failing over rather than noticing in a repository clone.
        expect(statSync(traceFile(scenario)).size).toBeLessThan(512 * 1024);
      });
    });
  }
});
