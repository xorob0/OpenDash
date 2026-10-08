/**
 * The functions the evaluator implements, each with the arities it accepts.
 *
 * The set is what a full build calls (`ncalcCoverage.test.ts` in the dash package holds it to that)
 * plus the few `ncalc.ts` offers beside them, and no more. Every arity here is one SimHub's own
 * table in `ncalcFunctions.ts` accepts, and a test holds it to that too, so the evaluator can never
 * compute a call SimHub would not dispatch. Where an arity SimHub accepts is missing here, the
 * evaluator refuses the call loudly instead of guessing at it.
 *
 * Each implementation says what it was read from. Where the behaviour could not be checked without
 * Windows, the comment says **Unverified** and gives the reading chosen; those are the ones trace
 * version 2 should carry probe columns for.
 */
import { arityAccepts, arityText, DRIVER_PREFIX, DRIVER_SECTOR_PREFIX, NCALC_FUNCTIONS, type Arity } from '../ncalcFunctions.ts';
import { callText, opponentCall } from '../opponentCalls.ts';
import { Fails, Unsupported } from './errors.ts';
import { formatDate, formatNumber, valueToString } from './dotnetFormat.ts';
import { compare, roundHalfEven, toBoolean, toDouble, toInt32 } from './operators.ts';
import { readCall, readProperty, type Scope } from './scope.ts';
import { fromSeconds, toShortTime, totalSeconds } from './timespan.ts';
import { decimal, double, isDate, isNumber, isTimeSpan, sameValue, single, typeName, type Value } from './values.ts';

/** What an implementation is handed: its arguments, evaluated only when asked for. */
export interface CallContext {
  /** The function name in lower case. */
  readonly name: string;
  readonly count: number;
  /** Evaluates argument `i` (once; a second ask returns the first answer). */
  arg(i: number): Value;
  /** The source text of argument `i`, which is what SimHub keys `changed`'s state by. */
  source(i: number): string;
  readonly scope: Scope;
}

type Impl = (c: CallContext) => Value;

interface Supported {
  readonly arity: Arity;
  readonly impl: Impl;
}

const exactly = (n: number): Arity => ({ exactly: n });
const oneOf = (...ns: number[]): Arity => ({ oneOf: ns });

/** `$"{v}"`, which is how SimHub's string functions read their argument: null as the empty string. */
const text = (v: Value): string => valueToString(v);

const numberArg = (c: CallContext, i: number): number => toDouble(c.arg(i));

// --------------------------------------------------------------------------------- the stateful ones

interface ChangeEntry {
  last: Value;
  changedAt: number | undefined;
  direction: number;
}

/**
 * `changed(ms, v)` and its two siblings. SimHub keeps their state per dashboard, keyed by the text
 * of the value expression, and the **first ask records the value and answers false** (decompiled for
 * #109). Afterwards a value that differs (`Object.Equals`) from the last one restarts the window,
 * and the answer is whether the clock is still inside it.
 *
 * Unverified: whether the window is closed (`<=`) or open at its end, which is taken as open here,
 * and whether `isincreasing` shares `changed`'s entry for the same text, which it does not here.
 * `isincreasing` answers true inside the window when the last move was upward, `isdecreasing` when it
 * was downward.
 */
function windowed(kind: 'changed' | 'increasing' | 'decreasing'): Impl {
  return (c) => {
    const { state, now } = c.scope;
    if (!state || now === undefined) throw new Unsupported(`${c.name}() keeps state between frames, and the scope has no state store or clock`);
    const ms = numberArg(c, 0);
    const value = c.arg(1);
    const key = `${c.name}:${c.source(1)}`;
    const entry = state.get<ChangeEntry>(key);
    if (!entry) {
      state.set(key, { last: value, changedAt: undefined, direction: 0 });
      return false;
    }
    if (!sameValue(entry.last, value)) {
      let direction = 0;
      try {
        if (entry.last !== null && value !== null) direction = Math.sign(compare(value, entry.last));
      } catch {
        direction = 0;
      }
      entry.last = value;
      entry.changedAt = now;
      entry.direction = direction;
    }
    const inside = entry.changedAt !== undefined && now - entry.changedAt < ms;
    if (kind === 'changed') return inside;
    return inside && (kind === 'increasing' ? entry.direction > 0 : entry.direction < 0);
  };
}

// --------------------------------------------------------------------------------- the table

const FUNCTIONS: ReadonlyMap<string, Supported> = new Map<string, Supported>([
  // NCalc's own: `if` evaluates the branch it takes and no other.
  ['if', { arity: exactly(3), impl: (c) => (toBoolean(c.arg(0)) ? c.arg(1) : c.arg(2)) }],
  [
    'in',
    {
      arity: { atLeast: 2 },
      // NCalc's `in` compares each candidate as `=` does, and stops at the first match.
      impl: (c) => {
        const v = c.arg(0);
        for (let i = 1; i < c.count; i++) if (compare(v, c.arg(i)) === 0) return true;
        return false;
      },
    },
  ],
  // NCalc 1.3.8's `Abs` is `Math.Abs(Convert.ToDecimal(x))`: a decimal, and a null is 0.
  // Unverified on the VM; it is why `abs(7) / 2` is 3.5 and not 3.
  ['abs', { arity: exactly(1), impl: (c) => decimal(Math.abs(numberArg(c, 0))) }],
  ['cos', { arity: exactly(1), impl: (c) => double(Math.cos(numberArg(c, 0))) }],
  ['sin', { arity: exactly(1), impl: (c) => double(Math.sin(numberArg(c, 0))) }],
  // `Math.Truncate(Convert.ToDouble(x))`, so a null is 0. Unverified on the VM.
  ['truncate', { arity: exactly(1), impl: (c) => double(Math.trunc(numberArg(c, 0))) }],
  [
    'round',
    {
      // NCalc 1.3.8 refuses `round` with one argument, though SimHub's table lists it.
      arity: exactly(2),
      // `Math.Round(Convert.ToDouble(x), Convert.ToInt16(d))`, to even at the midpoint, the way .NET
      // Framework does it: scale, round the scaled double to even, scale back. Unverified on the VM.
      impl: (c) => {
        const x = numberArg(c, 0);
        const digits = toInt32(c.arg(1));
        if (digits < 0 || digits > 15) throw new Fails('Rounding digits must be between 0 and 15, inclusive');
        if (Math.abs(x) >= 1e16) return double(x);
        const power = 10 ** digits;
        return double(roundHalfEven(x * power) / power);
      },
    },
  ],
  ['max', { arity: exactly(2), impl: (c) => extreme(c, Math.max) }],
  ['min', { arity: exactly(2), impl: (c) => extreme(c, Math.min) }],

  // SimHub's engine.
  [
    'isnull',
    {
      arity: oneOf(1, 2),
      // With one argument, whether it is null; with two, the second when the first is null. The
      // second is evaluated only when it is needed. Unverified: whether SimHub evaluates it anyway.
      impl: (c) => {
        const v = c.arg(0);
        if (c.count === 1) return v === null;
        return v === null ? c.arg(1) : v;
      },
    },
  ],
  [
    'format',
    {
      // SimHub's `Function_Format_Core(value, format, addSign)`.
      arity: oneOf(2, 3),
      impl: (c) => {
        const v = c.arg(0);
        const pattern = text(c.arg(1));
        const addSign = c.count === 3 ? toBoolean(c.arg(2)) : false;
        // Unverified: a null formats as null, and a string or a boolean comes back as its own text,
        // which is what `String.Format` does with a pattern it cannot apply.
        if (v === null) return null;
        if (typeof v === 'string' || typeof v === 'boolean') return text(v);
        if (isNumber(v)) return formatNumber(v, pattern, addSign);
        if (isDate(v)) return formatDate(v, pattern);
        throw new Unsupported(`format() of a ${typeName(v)} uses .NET's TimeSpan patterns, which the evaluator does not know`);
      },
    },
  ],
  // A TimeSpan's seconds, and null for anything else, the number 0 included (measured for #454).
  ['timespantoseconds', { arity: exactly(1), impl: (c) => { const v = c.arg(0); return isTimeSpan(v) ? double(totalSeconds(v)) : null; } }],
  // `TimeSpan.FromSeconds`. Unverified: a null gives null rather than a zero span.
  ['secondstotimespan', { arity: exactly(1), impl: (c) => { const v = c.arg(0); return v === null ? null : fromSeconds(toDouble(v)); } }],
  [
    'toshorttime',
    {
      arity: oneOf(2, 3, 4),
      // See `toShortTime` in timespan.ts for what is verified. Unverified: a null gives null, and a
      // value that is not a TimeSpan throws.
      impl: (c) => {
        const v = c.arg(0);
        if (v === null) return null;
        if (!isTimeSpan(v)) throw new Fails(`toshorttime() of a ${typeName(v)}: it takes a TimeSpan`);
        const decimals = toInt32(c.arg(1));
        const alwaysSign = c.count >= 3 ? toBoolean(c.arg(2)) : false;
        const forceMinutes = c.count >= 4 ? toBoolean(c.arg(3)) : false;
        return toShortTime(v, decimals, alwaysSign, forceMinutes);
      },
    },
  ],
  // The string functions read their argument as `$"{v}"`, so a null is the empty string.
  // Unverified for ucase, lcase, tcase, left and right; documented for replace.
  ['ucase', { arity: exactly(1), impl: (c) => text(c.arg(0)).toUpperCase() }],
  ['lcase', { arity: exactly(1), impl: (c) => text(c.arg(0)).toLowerCase() }],
  ['tcase', { arity: exactly(1), impl: (c) => titleCase(text(c.arg(0))) }],
  [
    'replace',
    {
      arity: exactly(3),
      // `$"{val}".Replace($"{search}", $"{replacement}")`: every occurrence, and an empty needle throws.
      impl: (c) => {
        const search = text(c.arg(1));
        if (search === '') throw new Fails('String cannot be of zero length (replace with an empty search)');
        return text(c.arg(0)).split(search).join(text(c.arg(2)));
      },
    },
  ],
  [
    'left',
    {
      arity: exactly(3),
      // `left(value, startIndex, maxLength)`: up to maxLength characters from startIndex. Unverified:
      // how an index past the end is clamped, taken here as an empty string.
      impl: (c) => {
        const s = text(c.arg(0));
        const start = Math.max(0, Math.min(s.length, toInt32(c.arg(1))));
        const length = Math.max(0, toInt32(c.arg(2)));
        return s.slice(start, start + length);
      },
    },
  ],
  [
    'right',
    {
      arity: exactly(3),
      // Unverified, and called by no package: the last maxLength characters before the last startIndex.
      impl: (c) => {
        const s = text(c.arg(0));
        const end = Math.max(0, s.length - Math.max(0, toInt32(c.arg(1))));
        const length = Math.max(0, toInt32(c.arg(2)));
        return s.slice(Math.max(0, end - length), end);
      },
    },
  ],
  // A property whose name is computed. Null when the name is.
  ['prop', { arity: exactly(1), impl: (c) => { const n = c.arg(0); return n === null ? null : readProperty(c.scope, text(n)); } }],
  ['changed', { arity: exactly(2), impl: windowed('changed') }],
  ['isincreasing', { arity: exactly(2), impl: windowed('increasing') }],
  ['isdecreasing', { arity: exactly(2), impl: windowed('decreasing') }],
  [
    'blink',
    {
      arity: exactly(3),
      // Unverified, and called by no package (an item blinks by its own property): on for the first
      // `delayMs` of every two, while `enabled`.
      impl: (c) => {
        if (c.scope.now === undefined) throw new Unsupported('blink() needs the clock, and the scope has none');
        if (!toBoolean(c.arg(2))) return false;
        const delay = Math.max(1, numberArg(c, 1));
        return Math.floor(c.scope.now / delay) % 2 === 0;
      },
    },
  ],

  // The registry.
  [
    'repeatindex',
    {
      arity: oneOf(0, 1),
      // Which copy of the repeated layer this is, 1 for the first. Unverified: `repeatindex(depth)`
      // counts outward from the innermost layer, 0 being the innermost; no package passes a depth.
      impl: (c) => {
        const stack = c.scope.repeat ?? [];
        const depth = c.count === 0 ? 0 : toInt32(c.arg(0));
        const index = stack[stack.length - 1 - depth];
        if (index === undefined) throw new Unsupported(`repeatindex(${c.count === 0 ? '' : depth}) outside ${depth === 0 ? 'any' : 'that many'} repeated layer${depth === 0 ? '' : 's'}`);
        return { kind: 'int', value: index };
      },
    },
  ],
  [
    'rootdashboardscreenname',
    {
      arity: exactly(0),
      impl: (c) => {
        if (c.scope.rootScreenName === undefined) throw new Unsupported('rootdashboardscreenname() needs the previous frame\'s screen, and the scope has none');
        return c.scope.rootScreenName;
      },
    },
  ],
]);

/**
 * NCalc 1.3.8's `Numbers.Max` and `Min`: a null side gives the other side, two nulls give null, and
 * otherwise the result has the **left** operand's type, the right one converted to it. So
 * `max(0, 2.6)` is the Int32 3 (`Convert.ToInt32` rounds), not 2.6. Unverified on the VM.
 */
function extreme(c: CallContext, pick: (a: number, b: number) => number): Value {
  const a = numericOrNull(c.arg(0));
  const b = numericOrNull(c.arg(1));
  if (a === null) return b;
  if (b === null) return a;
  if (!isNumber(a)) throw new Fails(`${c.name}() of a ${typeName(a)}`);
  switch (a.kind) {
    case 'int':
      return { kind: 'int', value: pick(a.value, toInt32(b)) };
    case 'double':
      return double(pick(a.value, toDouble(b)));
    case 'single':
      return single(pick(a.value, toDouble(b)));
    case 'decimal':
      return decimal(pick(a.value, toDouble(b)));
  }
}

/** NCalc's `ConvertIfString` before `Max` and `Min`: a string becomes a decimal. */
function numericOrNull(v: Value): Value {
  if (typeof v !== 'string') return v;
  return decimal(toDouble(v));
}

/**
 * `TextInfo.ToTitleCase` in en-US: each word's first letter upper case and the rest lower, except a
 * word that is entirely upper case, which is left alone. Unverified beyond what ncalc.ts records.
 */
function titleCase(s: string): string {
  return s.replace(/[\p{L}\p{N}']+/gu, (word) => (word === word.toUpperCase() && /\p{Lu}/u.test(word) ? word : word[0]!.toUpperCase() + word.slice(1).toLowerCase()));
}

// --------------------------------------------------------------------------------- the opponent calls

/**
 * An opponent call, answered from the scope's recorded columns by its canonical text. An argument
 * with no canonical spelling (a fraction, a string, a null) has no column, and nor does a position
 * the recorder did not enumerate: both are null, as a position naming no car is in SimHub.
 */
function opponent(c: CallContext): Value {
  const args: Value[] = [];
  for (let i = 0; i < c.count; i++) args.push(c.arg(i));
  const plain = args.map((a) => (typeof a === 'boolean' ? a : isNumber(a) ? a.value : a));
  const key = callText(c.name, plain);
  return key === undefined ? null : readCall(c.scope, key);
}

// --------------------------------------------------------------------------------- the questions

/** Why SimHub would not dispatch `name` with `count` arguments, or undefined when it would. */
export function dispatchProblem(name: string, count: number): string | undefined {
  const n = name.toLowerCase();
  if (n.startsWith(DRIVER_SECTOR_PREFIX)) return count === 3 ? undefined : `${name} takes 3 arguments (position, sector, includePrevious), given ${count}`;
  if (n.startsWith(DRIVER_PREFIX) && n !== 'drivergamespecificdata') return count === 1 ? undefined : `${name} takes 1 argument (the leaderboard position), given ${count}`;
  const fn = NCALC_FUNCTIONS.get(n);
  if (!fn) return `${name}() is not a function SimHub implements`;
  if (!arityAccepts(fn.arity, count)) return `${name}() takes ${arityText(fn.arity)} arguments in SimHub, given ${count}`;
  return undefined;
}

/** Why this evaluator would not compute `name` with `count` arguments, or undefined when it would. */
export function supportProblem(name: string, count: number): string | undefined {
  const n = name.toLowerCase();
  const kinds = opponentCall(n);
  if (kinds) return kinds.length === count ? undefined : `${name}() takes ${kinds.length} argument${kinds.length === 1 ? '' : 's'} as an opponent call, given ${count}`;
  const fn = FUNCTIONS.get(n);
  if (!fn) return `${name}() is not one the evaluator implements`;
  if (!arityAccepts(fn.arity, count)) return `${name}() is implemented with ${arityText(fn.arity)} arguments, given ${count}`;
  return undefined;
}

/** Every function the evaluator implements, by lower-cased name, with its arity. Opponent calls are matched by {@link opponentCall}. */
export const SUPPORTED_FUNCTIONS: ReadonlyMap<string, Arity> = new Map([...FUNCTIONS].map(([name, f]) => [name, f.arity] as const));

/** The implementation for a call the evaluator supports. Check {@link supportProblem} first. */
export function implementationOf(name: string): Impl {
  const n = name.toLowerCase();
  if (opponentCall(n)) return opponent;
  const fn = FUNCTIONS.get(n);
  if (!fn) throw new Error(`no implementation of ${name}`);
  return fn.impl;
}
