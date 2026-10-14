/**
 * The trace format, and the promise the committed traces make: that everything the packages read,
 * through any expression of any package of any shipped theme, is in every one of them, for every
 * sim the loop can play.
 *
 * That last test is the point of the whole thing. A trace is recorded once, on the Windows VM, and
 * replayed by everything afterwards, so nothing except this test would notice a package that
 * started reading a property no trace carries. It would render as a blank field in a video that
 * nobody thinks to distrust.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { propertiesRead } from '../packages/dash/src/properties.ts';
import { scenarios, tracedScenarioNames } from './emulator.ts';
import { ASSETTO_CORSA, IRACING, SIMS } from './sims.ts';
import {
  TRACE_DIR,
  TRACE_VERSION,
  foreignRawColumns,
  formatTrace,
  frame,
  parseTrace,
  propertiesOf,
  readTrace,
  traceDirOf,
  traceFile,
  traceGame,
  tracedScenarios,
  type Trace,
} from './trace.ts';

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

  test('a header naming the game keeps it, and one from before the field reads as iRacing', () => {
    const ac: Trace = { ...sample, header: { ...header, game: 'AssettoCorsa' } };
    expect(parseTrace(formatTrace(ac)).header.game).toBe('AssettoCorsa');
    expect(traceGame(parseTrace(formatTrace(ac)))).toBe('AssettoCorsa');
    expect(lines(sample)[0]).not.toContain('game');
    expect(traceGame(sample)).toBe('IRacing');
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
    ['a column with no property name', `${JSON.stringify(header)}\n{"v":1}\n`],
    ['two columns for one property', `${JSON.stringify(header)}\n{"p":"A","v":1}\n{"p":"A","v":2}\n`],
    ['columns out of order', `${JSON.stringify(header)}\n{"p":"B","v":1}\n{"p":"A","v":2}\n`],
    ['an array of the wrong length', `${JSON.stringify(header)}\n{"p":"A","v":[1,2]}\n`],
    ['a value that is not a scalar', `${JSON.stringify(header)}\n{"p":"A","v":{"nested":1}}\n`],
    ['a type nothing understands', `${JSON.stringify(header)}\n{"p":"A","t":"duration","v":1}\n`],
    ['an asserted list that is not a list of names', `${JSON.stringify({ ...header, asserted: 'A' })}\n{"p":"A","v":1}\n`],
    ['a game that is not a name', `${JSON.stringify({ ...header, game: 7 })}\n{"p":"A","v":1}\n`],
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

describe('the format a browser can read', () => {
  // The demo (#395) bundles the reader into a page, where node:fs does not exist: one import of it
  // at the top of traceFormat.ts and the page fails to build, so the file is held to having none.
  test('traceFormat.ts imports nothing from node', () => {
    const source = readFileSync(new URL('./traceFormat.ts', import.meta.url), 'utf8');
    expect(source).not.toMatch(/from ['"](node:|fs['"]|path['"])/);
    expect(source).not.toMatch(/import\.meta\.dir/);
  });
});

describe('a trace of one sim holding another sim\'s raw values', () => {
  // The check that holds a header's `game` to the recording under it: SimHub reads one game at a
  // time, so a trace of Assetto Corsa with a value in an iRacing column was recorded with SimHub on
  // iRacing, whatever its header says.
  const ac: Trace = {
    header: { ...header, game: 'AssettoCorsa' },
    columns: [
      { p: 'DataCorePlugin.GameData.Rpms', v: [5000, 5100, 5200] },
      { p: 'DataCorePlugin.GameRawData.Graphics.PacketId', v: [300, 306, 312] },
      { p: 'DataCorePlugin.GameRawData.SessionData.WeekendInfo.TrackName', v: null },
      { p: 'DataCorePlugin.GameRawData.Telemetry.SteeringWheelAngle', v: null },
    ],
  };

  test('is nothing when the other sim\'s columns are null, which is what SimHub publishes for them', () => {
    expect(foreignRawColumns(ac, ASSETTO_CORSA)).toEqual([]);
  });

  test('names the column when one holds a value, in any frame', () => {
    const leaked: Trace = { ...ac, columns: ac.columns.map((c) => (c.p.endsWith('SteeringWheelAngle') ? { ...c, v: [null, 0.2, null] } : c)) };
    expect(foreignRawColumns(leaked, ASSETTO_CORSA)).toEqual(['DataCorePlugin.GameRawData.Telemetry.SteeringWheelAngle']);
  });

  test('works the other way round: AC\'s raw columns are foreign to an iRacing trace', () => {
    expect(foreignRawColumns(ac, IRACING)).toEqual(['DataCorePlugin.GameRawData.Graphics.PacketId']);
  });
});

/**
 * The traces each sim has committed. iRacing's are in `traces/`, Assetto Corsa's in `traces/ac/`,
 * and both are held to the same list: every property any package reads (`propertiesRead()`), which
 * is iRacing's raw names, the normalised `GameData.*` and the plugin's `OpenDash.*` together. An AC
 * trace carries iRacing's raw columns as null, because the recorder asks SimHub for them and SimHub's
 * AC reader publishes none, and that null is exactly what a dashboard on AC is handed, which is the
 * point of replaying one. What is particular to a sim is checked on top: the game in the header, its
 * own tick column, and no value in another sim's raw columns.
 */
for (const sim of SIMS) {
  describe(`the committed traces of ${sim.label}`, () => {
    const required = propertiesRead();
    const dir = traceDirOf(sim);

    test('there is one for every scenario the emulator ships', () => {
      // Every scenario except the ones its untraced list names and says why for; see scripts/sims.ts.
      expect(tracedScenarios(dir)).toEqual(tracedScenarioNames(sim));
      for (const name of sim.untraced) {
        expect({ name, runnable: scenarios(sim).includes(name) }).toEqual({ name, runnable: true });
        expect({ name, traced: tracedScenarios(dir).includes(name) }).toEqual({ name, traced: false });
      }
    });

    for (const scenario of tracedScenarios(dir)) {
      describe(scenario, () => {
        const trace = readTrace(scenario, dir);

        test('it is a trace of that scenario, from that sim', () => {
          expect(trace.header.scenario).toBe(scenario);
          expect(trace.header.frames).toBeGreaterThan(0);
          expect({ scenario, game: traceGame(trace) }).toEqual({ scenario, game: sim.game });
        });

        test('it carries every property any expression of any package of any shipped theme reads', () => {
          const present = new Set(propertiesOf(trace));
          const missing = required.filter((p) => !present.has(p));
          // A failure here is not a broken trace, it is an out-of-date one: something now reads a
          // property that was not recorded. Re-record with `bun run record <scenario>`, and
          // `--sim ${sim.id}` for this sim.
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
          expect(propertiesOf(trace)).toContain(sim.tickProperty);
        });

        test("no other sim's raw column holds a value, so the game it names is the game it is of", () => {
          expect({ scenario, foreign: foreignRawColumns(trace, sim) }).toEqual({ scenario, foreign: [] });
        });

        test('it is small enough to commit', () => {
          // Two hundred frames of two hundred properties. Well past this and the columnar shape has
          // stopped working, which is worth failing over rather than noticing in a repository clone.
          expect(statSync(traceFile(scenario, dir)).size).toBeLessThan(512 * 1024);
        });
      });
    }
  });
}

describe('where each sim keeps its traces', () => {
  test("iRacing's are where they always were, and every other sim's are in a folder of its own", () => {
    expect(traceDirOf(IRACING)).toBe(TRACE_DIR);
    const others = SIMS.filter((s) => s !== IRACING).map(traceDirOf);
    expect(new Set(others).size).toBe(others.length);
    for (const dir of others) expect(path.dirname(dir)).toBe(TRACE_DIR);
  });

  test("a folder of a sim's traces is never read as a trace of iRacing", () => {
    // tracedScenarios lists the files of one folder, so traces/ac/ is not a scenario of iRacing's.
    for (const sim of SIMS.filter((s) => s !== IRACING)) expect(tracedScenarios(TRACE_DIR)).not.toContain(sim.traceSubdir);
  });
});
