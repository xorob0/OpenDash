#!/usr/bin/env bun
/**
 * trace: the recorded telemetry format, and everything that reads one.
 *
 * A trace is one scenario's SimHub-visible property values, frame by frame, in a file that a
 * renderer can replay on any machine. It is what the preview renderer, the per-pull-request video
 * and the pixel goldens are fed with, so it is committed, and being committed is what shapes the
 * format: it has to stay small and its diff has to be readable when a scenario changes.
 *
 * Hence columnar rather than one record per frame. A frame-shaped file repeats all two hundred
 * property names on every line and puts everything that moves on the same line, so a change to the
 * fuel load rewrites every line of the file and none of them can be read. Here each property is one
 * line: a scalar when it never moves, an array of one value per frame when it does. A scenario that
 * changes the fuel load changes the fuel line and nothing else.
 *
 * `scripts/record.ts` writes them, from a real SimHub on the Windows VM; this file only reads. A
 * column that was typed rather than observed is named in the header's `asserted` list, because a
 * committed file that mixes the two without saying so is a file a later reader has to trust blindly.
 */
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

import { TRACE_EXTENSION, TraceError, callsOf, frame, parseTrace, propertiesOf, type Trace } from './traceFormat.ts';

export * from './traceFormat.ts';

/** Where the committed traces live, one per emulator scenario. */
export const TRACE_DIR = path.resolve(import.meta.dir, '..', 'traces');

/** The trace file for a scenario, whether or not it exists. */
export const traceFile = (scenario: string, dir: string = TRACE_DIR): string => path.join(dir, `${scenario}${TRACE_EXTENSION}`);

/** The scenarios that have a committed trace, sorted. */
export function tracedScenarios(dir: string = TRACE_DIR): string[] {
  if (!existsSync(dir)) return [];
  return readdirSync(dir)
    .filter((f) => f.endsWith(TRACE_EXTENSION))
    .map((f) => f.slice(0, -TRACE_EXTENSION.length))
    .sort();
}

/** Reads and parses one committed trace. The scenario name is in the error, since a parse failure names a file nobody chose by hand. */
export function readTrace(scenario: string, dir: string = TRACE_DIR): Trace {
  const file = traceFile(scenario, dir);
  if (!existsSync(file)) throw new TraceError(`no trace for ${JSON.stringify(scenario)} at ${file}; record it with \`bun run record ${scenario}\``);
  try {
    return parseTrace(readFileSync(file, 'utf8'));
  } catch (e) {
    throw new TraceError(`${file}: ${e instanceof Error ? e.message : String(e)}`);
  }
}

// --------------------------------------------------------------------------------- the command

const USAGE = `trace: read the recorded telemetry under traces/.

  bun run trace list                    every committed trace, with its size and shape
  bun run trace show <scenario> [frame] one frame as a map of properties and calls; default frame 0
  bun run trace check                   parse every trace and report what moves in it

Recording is \`bun run record <scenario>\`, which needs the Windows VM.
`;

const sizeOf = (file: string): number => (existsSync(file) ? readFileSync(file).byteLength : 0);

const kb = (bytes: number): string => `${(bytes / 1024).toFixed(0)} KB`;

const moving = (trace: Trace): number => trace.columns.filter((c) => Array.isArray(c.v)).length;

/** What a trace holds, as `list` and `check` say it: the version, the properties and, from version 2, the calls. */
const shape = (trace: Trace): string => {
  const calls = callsOf(trace).length;
  return `v${trace.header.trace}, ${String(propertiesOf(trace).length).padStart(3)} properties${trace.header.trace >= 2 ? `, ${calls} calls` : ''}, ${moving(trace)} columns moving`;
};

export function main(argv: readonly string[]): number {
  const [command, ...rest] = argv;
  if (command === undefined || command === '--help' || command === '-h' || command === 'help') {
    console.log(USAGE);
    return 0;
  }
  const names = tracedScenarios();
  switch (command) {
    case 'list': {
      if (names.length === 0) {
        console.log('no trace is committed yet');
        return 0;
      }
      for (const name of names) {
        const trace = readTrace(name);
        console.log(`${name.padEnd(10)} ${String(trace.header.frames).padStart(4)} frames at ${trace.header.hz} Hz  ${shape(trace)}  ${kb(sizeOf(traceFile(name)))}  recorded ${trace.header.recorded}`);
      }
      return 0;
    }
    case 'show': {
      const name = rest[0];
      if (name === undefined) {
        console.error('show needs a scenario');
        return 2;
      }
      const index = rest[1] === undefined ? 0 : Number.parseInt(rest[1], 10);
      if (!Number.isInteger(index)) {
        console.error(`${JSON.stringify(rest[1])} is not a frame number`);
        return 2;
      }
      const trace = readTrace(name);
      const values = frame(trace, index);
      for (const property of Object.keys(values).sort()) console.log(`${property} = ${JSON.stringify(values[property])}`);
      return 0;
    }
    case 'check': {
      if (names.length === 0) {
        console.error('no trace is committed yet');
        return 1;
      }
      for (const name of names) {
        const trace = readTrace(name);
        console.log(`${name}: ${shape(trace)} over ${trace.header.frames} frames, ${kb(sizeOf(traceFile(name)))}`);
      }
      return 0;
    }
    default:
      console.error(`unknown command ${JSON.stringify(command)}\n${USAGE}`);
      return 2;
  }
}

if (import.meta.main) {
  try {
    process.exit(main(process.argv.slice(2)));
  } catch (e) {
    console.error(e instanceof Error ? e.message : String(e));
    process.exit(1);
  }
}
