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

import { IRACING, SIMS, simFromArgs, type Sim } from './sims.ts';
import { TRACE_EXTENSION, TraceError, frame, parseTrace, type Trace } from './traceFormat.ts';

export * from './traceFormat.ts';

/** Where the committed traces live, one per emulator scenario: iRacing's here, and each other sim's in a folder of its own. */
export const TRACE_DIR = path.resolve(import.meta.dir, '..', 'traces');

/**
 * A sim's traces: `traces/` itself for iRacing, whose nine were there before any other sim was, and
 * `traces/ac/` for Assetto Corsa. A folder per sim rather than a suffix per file, so that a scenario
 * keeps its own name in both (`green` is `green` on either) and everything that lists `traces/` for
 * iRacing's, the replays and the goldens, goes on seeing exactly what it saw.
 */
export const traceDirOf = (sim: Sim): string => path.join(TRACE_DIR, sim.traceSubdir);

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

/**
 * The columns of a trace of `sim` that belong to another sim's reader and hold a value somewhere.
 *
 * Every trace carries every property any package reads, so a trace of Assetto Corsa carries
 * iRacing's raw `Telemetry.*` and `SessionData.*`, and those are null, because SimHub's AC reader
 * publishes no such thing. A value in one means SimHub was reading another game than the one the
 * trace says, which a header can claim and a column cannot, so this is the check that holds the
 * header to the recording.
 */
export function foreignRawColumns(trace: Trace, sim: Sim): string[] {
  const foreign = SIMS.filter((other) => other !== sim).flatMap((other) => other.rawPrefixes);
  return trace.columns
    .filter((c) => foreign.some((prefix) => c.p.startsWith(prefix)))
    .filter((c) => (Array.isArray(c.v) ? c.v.some((v) => v !== null) : c.v !== null))
    .map((c) => c.p);
}

// --------------------------------------------------------------------------------- the command

const USAGE = `trace: read the recorded telemetry under traces/.

  bun run trace list [--sim ac]                    every committed trace, with its size and shape
  bun run trace show <scenario> [frame] [--sim ac] one frame as a property map; default frame 0
  bun run trace check [--sim ac]                   parse every trace and report what moves in it

--sim picks the sim's traces: iracing, the default, in traces/, or ac in traces/ac/.
Recording is \`bun run record <scenario>\`, which needs the Windows VM.
`;

const sizeOf = (file: string): number => (existsSync(file) ? readFileSync(file).byteLength : 0);

const kb = (bytes: number): string => `${(bytes / 1024).toFixed(0)} KB`;

const moving = (trace: Trace): number => trace.columns.filter((c) => Array.isArray(c.v)).length;

export function main(argv: readonly string[]): number {
  const [command, ...args] = argv;
  if (command === undefined || command === '--help' || command === '-h' || command === 'help') {
    console.log(USAGE);
    return 0;
  }
  const sim = simFromArgs(args);
  const dir = traceDirOf(sim);
  const rest = args.filter((a, i) => a !== '--sim' && !a.startsWith('--sim=') && args[i - 1] !== '--sim');
  const names = tracedScenarios(dir);
  switch (command) {
    case 'list': {
      if (names.length === 0) {
        console.log('no trace is committed yet');
        return 0;
      }
      for (const name of names) {
        const trace = readTrace(name, dir);
        console.log(`${name.padEnd(10)} ${String(trace.header.frames).padStart(4)} frames at ${trace.header.hz} Hz  ${String(trace.columns.length).padStart(3)} properties, ${moving(trace)} moving  ${kb(sizeOf(traceFile(name, dir)))}  recorded ${trace.header.recorded}`);
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
      const trace = readTrace(name, dir);
      const values = frame(trace, index);
      for (const property of Object.keys(values).sort()) console.log(`${property} = ${JSON.stringify(values[property])}`);
      return 0;
    }
    case 'check': {
      if (names.length === 0) {
        console.error(`no ${sim === IRACING ? '' : `${sim.label} `}trace is committed yet`);
        return 1;
      }
      for (const name of names) {
        const trace = readTrace(name, dir);
        console.log(`${name}: ${trace.columns.length} properties, ${moving(trace)} of them moving over ${trace.header.frames} frames, ${kb(sizeOf(traceFile(name, dir)))}`);
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
