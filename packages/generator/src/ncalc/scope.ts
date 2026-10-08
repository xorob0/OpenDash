/**
 * What an expression is evaluated against: the frame's properties, the recorded opponent calls, the
 * repeated layers it sits in, the state SimHub keeps between frames, and the clock.
 */
import { toValue, type Value } from './values.ts';

/** A map of names to values: a Map, a plain record, or a function that answers by name. */
export type Source = ReadonlyMap<string, unknown> | Readonly<Record<string, unknown>> | ((name: string) => unknown);

/**
 * The state `changed`, `isincreasing` and `isdecreasing` keep between frames.
 *
 * SimHub keeps it per dashboard, keyed by the text of the value expression (decompiled for #109), so
 * a renderer holds one of these per dashboard it draws and passes the same one every frame. Two
 * calls with the same value text share an entry, as they do in SimHub.
 */
export class CallState {
  private readonly entries = new Map<string, unknown>();
  get<T>(key: string): T | undefined {
    return this.entries.get(key) as T | undefined;
  }
  set(key: string, value: unknown): void {
    this.entries.set(key, value);
  }
  clear(): void {
    this.entries.clear();
  }
}

export interface Scope {
  /**
   * Property values by full name, as `[Name]` and `prop(name)` read them. A name with no entry is
   * null, which is what SimHub answers for a property nobody publishes. A dashboard variable is read
   * as `variable.<name>`, matched without regard to case as SimHub matches it.
   */
  readonly properties: Source;
  /**
   * The opponent calls' values by canonical call text (`opponentCalls.ts`), as trace version 2
   * records them. A call with no entry is null, as a position that names no car is in SimHub.
   *
   * Absent, a call is looked up in {@link properties} by the same text. That is where a version 2
   * trace's frame holds it: `frame()` in `scripts/traceFormat.ts` returns the properties and the
   * calls in one map, so a renderer hands the frame over once and the opponent calls resolve. A
   * version 1 frame has no call text in it, and every call is null, as it was before.
   */
  readonly calls?: Source;
  /**
   * The `repeatindex()` of each repeated layer the expression sits in, the outermost first. Absent
   * or empty outside every repeated layer.
   */
  readonly repeat?: readonly number[];
  /** The between-frames state of `changed` and its two siblings; required by an expression that calls them. */
  readonly state?: CallState;
  /** The clock in milliseconds, for `changed`, `isincreasing`, `isdecreasing` and `blink`. */
  readonly now?: number;
  /** What `rootdashboardscreenname()` answers: the screen drawn on the previous frame. */
  readonly rootScreenName?: string | null;
}

function lookup(source: Source, name: string): unknown {
  if (typeof source === 'function') return source(name);
  if (source instanceof Map) return source.get(name);
  const record = source as Readonly<Record<string, unknown>>;
  return Object.hasOwn(record, name) ? record[name] : undefined;
}

/** The value of a property, null when the scope has none. */
export function readProperty(scope: Scope, name: string): Value {
  const direct = lookup(scope.properties, name);
  if (direct !== undefined) return toValue(direct);
  if (/^variable\./i.test(name) && typeof scope.properties !== 'function') {
    const lower = name.toLowerCase();
    const keys = scope.properties instanceof Map ? [...scope.properties.keys()] : Object.keys(scope.properties);
    const key = keys.find((k) => k.toLowerCase() === lower);
    if (key !== undefined) return toValue(lookup(scope.properties, key));
  }
  return null;
}

/**
 * The recorded value of an opponent call by its canonical text, null when there is none: from the
 * scope's own calls when it has them, and otherwise from the frame map the properties came in.
 */
export function readCall(scope: Scope, text: string): Value {
  const v = lookup(scope.calls ?? scope.properties, text);
  return v === undefined || v === null ? null : toValue(v);
}
