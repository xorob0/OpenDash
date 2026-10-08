/**
 * A recorded trace as a property map at any moment, which is what the engine evaluates against.
 *
 * A trace is ten frames a second (`traces/README.md`), and a dashboard drawn at ten steps a second
 * looks like a slideshow of one, so a moment between two frames is filled in. Only where filling in
 * is honest, though:
 *
 * - A column that carries a fractional number anywhere is a measured quantity (revs as a
 *   percentage, the throttle, a temperature), and between two frames it is interpolated linearly.
 *   Its values are doubles, which is what SimHub holds for them, whole or not.
 * - A column of whole numbers only is a count or a state (the lap, the gear, a flag, a position).
 *   Halfway between lap 13 and lap 14 is not lap 13.5, so it takes the nearest frame, and its
 *   values stay Int32 as the evaluator reads a whole JSON number.
 * - Strings, booleans and nulls take the nearest frame. So do TimeSpans and DateTimes, which a
 *   trace carries as text: they are parsed, never interpolated.
 *
 * `frame()` from `scripts/traceFormat.ts` reads the two frames either side; nothing here re-reads
 * the file's layout.
 */
import { E, frame, type Trace, type TraceValue } from './ncalc.ts';

/** The trace's columns that are interpolated: those holding a fractional number on some frame. */
export function continuousColumns(trace: Trace): Set<string> {
  const out = new Set<string>();
  for (const column of trace.columns) {
    if (column.t !== undefined) continue;
    const values = Array.isArray(column.v) ? column.v : [column.v];
    if (values.some((v) => typeof v === 'number' && !Number.isInteger(v))) out.add(column.p);
  }
  return out;
}

/** A trace value as the evaluator holds it, by the column's declared type. */
function valueOf(raw: TraceValue | undefined, type: 'timespan' | 'datetime' | undefined): E.Value {
  if (raw === null || raw === undefined) return null;
  if (type === 'timespan') return E.parseTimeSpan(String(raw));
  if (type === 'datetime') return E.parseDateTime(String(raw));
  return E.toValue(raw);
}

export class Replay {
  readonly frames: number;
  readonly hz: number;
  /** From the first frame to the last, in milliseconds. */
  readonly duration: number;
  private readonly continuous: Set<string>;
  private readonly types = new Map<string, 'timespan' | 'datetime'>();

  constructor(readonly trace: Trace) {
    this.frames = trace.header.frames;
    this.hz = trace.header.hz;
    this.duration = ((this.frames - 1) / this.hz) * 1000;
    this.continuous = continuousColumns(trace);
    for (const c of trace.columns) if (c.t !== undefined) this.types.set(c.p, c.t);
  }

  /** The frame index a moment falls in, and how far it is towards the next one. */
  position(ms: number): { index: number; next: number; fraction: number } {
    const f = Math.min(Math.max((ms / 1000) * this.hz, 0), this.frames - 1);
    const index = Math.floor(f);
    return { index, next: Math.min(index + 1, this.frames - 1), fraction: f - index };
  }

  /** Every property at a moment, in milliseconds from the first frame. */
  at(ms: number): Record<string, E.Value> {
    const { index, next, fraction } = this.position(ms);
    const a = frame(this.trace, index);
    const b = next === index ? a : frame(this.trace, next);
    const nearest = fraction < 0.5 ? a : b;
    const out: Record<string, E.Value> = {};
    for (const name of Object.keys(a)) {
      const x = a[name];
      const y = b[name];
      if (this.continuous.has(name) && typeof x === 'number') {
        out[name] = E.double(typeof y === 'number' ? x + (y - x) * fraction : x);
        continue;
      }
      if (this.continuous.has(name) && typeof y === 'number' && fraction >= 0.5) {
        out[name] = E.double(y);
        continue;
      }
      out[name] = valueOf(nearest[name], this.types.get(name));
    }
    return out;
  }
}
