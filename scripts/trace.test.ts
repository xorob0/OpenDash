/**
 * The trace format, and the promise the committed traces make: that everything the packages read
 * is in every one of them.
 *
 * That last test is the point of the whole thing. A trace is recorded once, on the Windows VM, and
 * replayed by everything afterwards, so nothing except this test would notice a package that
 * started reading a property no trace carries. It would render as a blank field in a video that
 * nobody thinks to distrust.
 */
import { describe, expect, test } from 'bun:test';
import { statSync } from 'node:fs';
import { propertiesRead } from '../packages/dash/src/properties.ts';
import { scenarios, tracedScenarioNames, UNTRACED_SCENARIOS } from './emulator.ts';
import { PROVENANCE_PROPERTIES } from './record.ts';
import { TRACE_VERSION, formatTrace, frame, parseTrace, propertiesOf, readTrace, traceFile, tracedScenarios, type Trace } from './trace.ts';

const header = { trace: TRACE_VERSION, scenario: 'sample', frames: 3, hz: 10, ticks: [300, 6] as [number, number], recorded: '2026-09-13', simHub: '9.12.6' };

const sample: Trace = {
  header,
  columns: [
    { p: 'A.Constant', v: 7 },
    { p: 'B.Moving', v: [1, 2, 3] },
    { p: 'C.Lap', t: 'timespan', v: ['00:01:38.4120000', '00:01:38.4120000', '00:01:39.0000000'] },
    { p: 'D.Missing', v: [null, 'x', true] },
  ],
};

const lines = (trace: Trace): string[] => formatTrace(trace).trimEnd().split('\n');

describe('the format', () => {
  test('a trace survives being written and read back', () => {
    expect(parseTrace(formatTrace(sample))).toEqual(sample);
  });

  test('it is one line of header and one line per property, sorted, with a trailing newline', () => {
    expect(lines(sample)).toHaveLength(5);
    expect(JSON.parse(lines(sample)[0] as string)).toEqual(header);
    expect(formatTrace(sample).endsWith('\n')).toBe(true);
    expect(propertiesOf(parseTrace(formatTrace(sample)))).toEqual(['A.Constant', 'B.Moving', 'C.Lap', 'D.Missing']);
  });

  test('writing sorts the columns, so a recorder cannot commit them in its own order', () => {
    const shuffled: Trace = { header, columns: [...sample.columns].reverse() };
    expect(lines(shuffled)).toEqual(lines(sample));
  });

  test('a column keeps its type only when a JSON scalar would lose it', () => {
    expect(lines(sample)[1]).toBe('{"p":"A.Constant","v":7}');
    expect(lines(sample)[3]).toContain('"t":"timespan"');
  });

  const rejected: [string, string][] = [
    ['an empty file', ''],
    ['a first line that is not JSON', 'not json\n'],
    ['a version this reader does not know', `${JSON.stringify({ ...header, trace: 99 })}\n{"p":"A","v":1}\n`],
    ['a header with no scenario', `${JSON.stringify({ ...header, scenario: '' })}\n{"p":"A","v":1}\n`],
    ['a header with no frames', `${JSON.stringify({ ...header, frames: 0 })}\n{"p":"A","v":1}\n`],
    ['a header whose ticks are not a pair', `${JSON.stringify({ ...header, ticks: [1, 2, 3] })}\n{"p":"A","v":1}\n`],
    ['a header and no property', `${JSON.stringify(header)}\n`],
    ['a column with no property name', `${JSON.stringify(header)}\n{"v":1}\n`],
    ['two columns for one property', `${JSON.stringify(header)}\n{"p":"A","v":1}\n{"p":"A","v":2}\n`],
    ['columns out of order', `${JSON.stringify(header)}\n{"p":"B","v":1}\n{"p":"A","v":2}\n`],
    ['an array of the wrong length', `${JSON.stringify(header)}\n{"p":"A","v":[1,2]}\n`],
    ['a value that is not a scalar', `${JSON.stringify(header)}\n{"p":"A","v":{"nested":1}}\n`],
    ['a type nothing understands', `${JSON.stringify(header)}\n{"p":"A","t":"duration","v":1}\n`],
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
    expect(frame(sample, 1)).toEqual({ 'A.Constant': 7, 'B.Moving': 2, 'C.Lap': '00:01:38.4120000', 'D.Missing': 'x' });
    expect(frame(sample, 0)['D.Missing']).toBeNull();
  });

  test('a frame outside the trace is an error rather than a map of undefined', () => {
    expect(() => frame(sample, 3)).toThrow();
    expect(() => frame(sample, -1)).toThrow();
  });
});

describe('the committed traces', () => {
  const required = propertiesRead();

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

      test('it records which tick each frame came from, so a re-recording is comparable', () => {
        for (const property of PROVENANCE_PROPERTIES) expect(propertiesOf(trace)).toContain(property);
      });

      test('it is small enough to commit', () => {
        // Two hundred frames of two hundred properties. Well past this and the columnar shape has
        // stopped working, which is worth failing over rather than noticing in a repository clone.
        expect(statSync(traceFile(scenario)).size).toBeLessThan(512 * 1024);
      });
    });
  }
});
