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
 * `scripts/record.ts` writes them, from a real SimHub on the Windows VM; this file only reads.
 */
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

export const TRACE_VERSION = 1;
export const TRACE_EXTENSION = '.ndjson';
/** Where the committed traces live, one per emulator scenario. */
export const TRACE_DIR = path.resolve(import.meta.dir, '..', 'traces');

/**
 * A value as a frame holds it. `null` is SimHub reporting no value, which is a state the dash is
 * built to survive and is therefore worth recording rather than dropping.
 */
export type TraceValue = number | string | boolean | null;

/**
 * What a JSON scalar alone would not say. A TimeSpan and a DateTime both travel as strings, so a
 * reader that has to tell `'00:01:38.4120000'` from a lap name needs the column to say which.
 */
export type TraceValueType = 'timespan' | 'datetime';

export interface TraceHeader {
  /** Format version; bumped when a reader written for the old one would misread the new one. */
  trace: number;
  /** The emulator scenario this was recorded from, without its .json. */
  scenario: string;
  frames: number;
  /** Frames per second of recorded time, not of wall clock. */
  hz: number;
  /** The emulator tick each frame was taken at, as `[first, step]`; the emulator runs at 60 Hz. */
  ticks: [number, number];
  /** When it was recorded, to the day. Provenance, and it is why a re-record diffs one line. */
  recorded: string;
  /** The SimHub whose mapping this is. A trace is only as truthful as the build that produced it. */
  simHub: string;
}

export interface TraceColumn {
  /** The full property name, e.g. `DataCorePlugin.GameData.Rpms`. */
  p: string;
  /** Absent for a plain JSON scalar; see {@link TraceValueType}. */
  t?: TraceValueType;
  /** One value per frame, or a single value when the property never moved. */
  v: TraceValue | TraceValue[];
}

export interface Trace {
  header: TraceHeader;
  /** Sorted by property name, one entry per property, never two for the same name. */
  columns: TraceColumn[];
}

export class TraceError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'TraceError';
  }
}

// --------------------------------------------------------------------------------- reading

const isValue = (v: unknown): v is TraceValue => v === null || typeof v === 'number' || typeof v === 'string' || typeof v === 'boolean';

const VALUE_TYPES: readonly string[] = ['timespan', 'datetime'];

function parseHeader(line: string): TraceHeader {
  let raw: unknown;
  try {
    raw = JSON.parse(line);
  } catch (e) {
    throw new TraceError(`the first line is not JSON: ${e instanceof Error ? e.message : String(e)}`);
  }
  if (typeof raw !== 'object' || raw === null) throw new TraceError('the first line is not a header object');
  const h = raw as Record<string, unknown>;
  if (h.trace !== TRACE_VERSION) throw new TraceError(`trace version ${JSON.stringify(h.trace)}; this reader understands ${TRACE_VERSION}`);
  if (typeof h.scenario !== 'string' || h.scenario === '') throw new TraceError('the header names no scenario');
  if (typeof h.frames !== 'number' || !Number.isInteger(h.frames) || h.frames < 1) throw new TraceError(`frames must be a positive integer, got ${JSON.stringify(h.frames)}`);
  if (typeof h.hz !== 'number' || h.hz <= 0) throw new TraceError(`hz must be positive, got ${JSON.stringify(h.hz)}`);
  const ticks = h.ticks;
  if (!Array.isArray(ticks) || ticks.length !== 2 || !ticks.every((n) => typeof n === 'number' && Number.isInteger(n))) {
    throw new TraceError('ticks must be [first, step], both whole numbers');
  }
  if (typeof h.recorded !== 'string') throw new TraceError('the header says nothing about when it was recorded');
  if (typeof h.simHub !== 'string') throw new TraceError('the header says nothing about which SimHub produced it');
  return { trace: h.trace, scenario: h.scenario, frames: h.frames, hz: h.hz, ticks: [ticks[0] as number, ticks[1] as number], recorded: h.recorded, simHub: h.simHub };
}

/**
 * Parses a trace. Everything the readers downstream rely on is checked here rather than left to
 * fail later as an undefined: one column per property, sorted, and every array exactly as long as
 * the header says.
 */
export function parseTrace(text: string): Trace {
  // Line numbers are the file's own, so that a message points at the line an editor shows.
  const lines = text.split('\n').map((line, i) => ({ line, number: i + 1 })).filter((l) => l.line.trim() !== '');
  const first = lines[0];
  if (first === undefined) throw new TraceError('the file is empty');
  const header = parseHeader(first.line);

  const columns: TraceColumn[] = [];
  const seen = new Set<string>();
  for (const { line, number } of lines.slice(1)) {
    let raw: unknown;
    try {
      raw = JSON.parse(line);
    } catch (e) {
      throw new TraceError(`line ${number} is not JSON: ${e instanceof Error ? e.message : String(e)}`);
    }
    if (typeof raw !== 'object' || raw === null || Array.isArray(raw)) throw new TraceError(`line ${number} is not a column object`);
    const c = raw as Record<string, unknown>;
    if (typeof c.p !== 'string' || c.p === '') throw new TraceError(`line ${number} names no property`);
    if (seen.has(c.p)) throw new TraceError(`${c.p} has two columns; a property is recorded once`);
    seen.add(c.p);
    if (c.t !== undefined && !VALUE_TYPES.includes(c.t as string)) throw new TraceError(`${c.p} has type ${JSON.stringify(c.t)}; expected ${VALUE_TYPES.join(' or ')}`);
    const v = c.v;
    if (Array.isArray(v)) {
      if (v.length !== header.frames) throw new TraceError(`${c.p} has ${v.length} values for ${header.frames} frames`);
      if (!v.every(isValue)) throw new TraceError(`${c.p} holds a value that is not a number, string, boolean or null`);
    } else if (!isValue(v)) {
      throw new TraceError(`${c.p} holds a value that is not a number, string, boolean or null`);
    }
    columns.push({ p: c.p, ...(c.t === undefined ? {} : { t: c.t as TraceValueType }), v: v as TraceValue | TraceValue[] });
  }
  if (columns.length === 0) throw new TraceError('the trace has a header and no property');

  const sorted = [...columns].map((c) => c.p).sort();
  const actual = columns.map((c) => c.p);
  const wrong = actual.findIndex((p, i) => p !== sorted[i]);
  if (wrong >= 0) throw new TraceError(`the columns are not sorted by property name: ${JSON.stringify(actual[wrong])} comes after ${JSON.stringify(actual[wrong - 1] ?? '')}`);

  return { header, columns };
}

/** Serialises a trace back to the committed form: header, then one line per column, sorted. */
export function formatTrace(trace: Trace): string {
  const lines = [JSON.stringify(trace.header)];
  for (const column of [...trace.columns].sort((a, b) => (a.p < b.p ? -1 : a.p > b.p ? 1 : 0))) {
    lines.push(JSON.stringify(column.t === undefined ? { p: column.p, v: column.v } : { p: column.p, t: column.t, v: column.v }));
  }
  return `${lines.join('\n')}\n`;
}

/** Every property the trace carries, in file order, which is sorted. */
export const propertiesOf = (trace: Trace): string[] => trace.columns.map((c) => c.p);

/**
 * One frame as a property map, which is what a renderer asks for. Frames are 0-based; a column
 * that never moved gives its one value for every frame.
 */
export function frame(trace: Trace, index: number): Record<string, TraceValue> {
  if (!Number.isInteger(index) || index < 0 || index >= trace.header.frames) {
    throw new TraceError(`frame ${index} is outside 0..${trace.header.frames - 1}`);
  }
  const out: Record<string, TraceValue> = {};
  for (const column of trace.columns) out[column.p] = Array.isArray(column.v) ? (column.v[index] as TraceValue) : column.v;
  return out;
}

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
  bun run trace show <scenario> [frame] one frame as a property map; default frame 0
  bun run trace check                   parse every trace and report what moves in it

Recording is \`bun run record <scenario>\`, which needs the Windows VM.
`;

const sizeOf = (file: string): number => (existsSync(file) ? readFileSync(file).byteLength : 0);

const kb = (bytes: number): string => `${(bytes / 1024).toFixed(0)} KB`;

const moving = (trace: Trace): number => trace.columns.filter((c) => Array.isArray(c.v)).length;

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
        console.log(`${name.padEnd(10)} ${String(trace.header.frames).padStart(4)} frames at ${trace.header.hz} Hz  ${String(trace.columns.length).padStart(3)} properties, ${moving(trace)} moving  ${kb(sizeOf(traceFile(name)))}  recorded ${trace.header.recorded}`);
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
        console.log(`${name}: ${trace.columns.length} properties, ${moving(trace)} of them moving over ${trace.header.frames} frames, ${kb(sizeOf(traceFile(name)))}`);
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
