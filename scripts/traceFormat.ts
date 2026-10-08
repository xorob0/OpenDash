/**
 * traceFormat: the recorded telemetry format itself, with nothing that touches a disk.
 *
 * Split out of `trace.ts` so that a browser page can read a trace: the demo (#395) replays one, and a
 * module that imports `node:fs` at its top cannot be bundled into a page. Everything here is pure,
 * the types, the parser, the serialiser and `frame()`; `trace.ts` re-exports all of it and keeps
 * the parts that read the repository and the command line.
 */

export const TRACE_VERSION = 1;
export const TRACE_EXTENSION = '.ndjson';

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
  /**
   * Columns in this file that were written by hand rather than observed, and are therefore claims
   * rather than recordings.
   *
   * Everything else in a trace came out of a real SimHub on the VM, and the rest of the header says
   * which one and when. A hand-written column has none of that, so it says so here instead: without
   * this field a reader replaying the file cannot tell an asserted value from a recorded one, and
   * the `recorded` date above quietly covers both.
   *
   * It exists because `trace.test.ts` demands every property any binding reads, and a binding can
   * start reading one between two trips to the VM, which this step is not always allowed to make.
   * `record.ts` builds a header from scratch and never copies this field, so the next re-record
   * supersedes every entry in it -- which is the point, and is the only way an entry leaves.
   * Anything in here should be a constant: a value nobody watched cannot honestly move frame by
   * frame.
   */
  asserted?: readonly string[];
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

export function parseHeader(line: string): TraceHeader {
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
  const asserted = h.asserted;
  if (asserted !== undefined && (!Array.isArray(asserted) || !asserted.every((p) => typeof p === 'string' && p !== ''))) {
    throw new TraceError('asserted must be a list of property names');
  }
  return {
    trace: h.trace,
    scenario: h.scenario,
    frames: h.frames,
    hz: h.hz,
    ticks: [ticks[0] as number, ticks[1] as number],
    recorded: h.recorded,
    simHub: h.simHub,
    ...(asserted === undefined ? {} : { asserted: asserted as string[] }),
  };
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
