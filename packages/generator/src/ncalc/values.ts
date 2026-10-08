/**
 * The values an NCalc expression computes with, as SimHub's engine holds them.
 *
 * NCalc works on boxed CLR objects and decides what an operator does by their types, so a number is
 * not one thing here. `7 / 2` is 3 because both are Int32 and `7.0 / 2` is 3.5; `format(v, '0.00',
 * true)` signs a double and not a Single; `abs` hands back a decimal. A JavaScript number cannot say
 * which of those it is, so a number carries its kind.
 *
 * - `int` is Int32 or Int64: an integer literal, or a property whose value is a whole number.
 * - `double` is System.Double: a literal with a point, and most of SimHub's telemetry.
 * - `single` is System.Single: a raw iRacing float, which SimHub passes on unboxed.
 * - `decimal` is System.Decimal: what NCalc's `abs` returns and what a numeric string becomes
 *   in arithmetic. Held as a double rounded to fifteen significant digits, which is what
 *   `Convert.ToDecimal(double)` keeps.
 *
 * A TimeSpan is held in ticks (100 ns), as .NET does; a DateTime as its wall clock, so that a
 * recording made at 08:20 in UTC-7 formats as 08:20 in any browser.
 */

export type NumberKind = 'int' | 'double' | 'single' | 'decimal';

export interface NumberValue {
  readonly kind: NumberKind;
  readonly value: number;
}

export interface TimeSpanValue {
  readonly kind: 'timespan';
  /** 100-nanosecond ticks, as `TimeSpan.Ticks`. */
  readonly ticks: number;
}

export interface DateValue {
  readonly kind: 'date';
  /** The wall clock as milliseconds since 1970-01-01T00:00 read as UTC, so that getUTC* gives the local fields. */
  readonly wallMs: number;
}

export type Value = null | boolean | string | NumberValue | TimeSpanValue | DateValue;

export const int = (value: number): NumberValue => ({ kind: 'int', value: Math.trunc(value) });
export const double = (value: number): NumberValue => ({ kind: 'double', value });
export const single = (value: number): NumberValue => ({ kind: 'single', value: Math.fround(value) });
export const decimal = (value: number): NumberValue => ({ kind: 'decimal', value: toDecimalPrecision(value) });

/** `Convert.ToDecimal(double)`: the double rounded to fifteen significant digits. */
export function toDecimalPrecision(value: number): number {
  if (!Number.isFinite(value) || value === 0) return value === 0 ? 0 : value;
  return Number(value.toPrecision(15));
}

export const TICKS_PER_MS = 10_000;
export const TICKS_PER_SECOND = 10_000_000;

export const timespan = (ticks: number): TimeSpanValue => ({ kind: 'timespan', ticks: Math.trunc(ticks) });
export const date = (wallMs: number): DateValue => ({ kind: 'date', wallMs });

export const isNumber = (v: Value): v is NumberValue => typeof v === 'object' && v !== null && (v.kind === 'int' || v.kind === 'double' || v.kind === 'single' || v.kind === 'decimal');
export const isTimeSpan = (v: Value): v is TimeSpanValue => typeof v === 'object' && v !== null && v.kind === 'timespan';
export const isDate = (v: Value): v is DateValue => typeof v === 'object' && v !== null && v.kind === 'date';

/** The CLR type name of a value, for a message that says what NCalc would have said. */
export function typeName(v: Value): string {
  if (v === null) return 'null';
  if (typeof v === 'boolean') return 'bool';
  if (typeof v === 'string') return 'string';
  switch (v.kind) {
    case 'int':
      return 'int';
    case 'double':
      return 'double';
    case 'single':
      return 'float';
    case 'decimal':
      return 'decimal';
    case 'timespan':
      return 'TimeSpan';
    case 'date':
      return 'DateTime';
  }
}

/**
 * A value from outside the evaluator, a property map or a recorded call column, as NCalc would hold
 * it. A JavaScript number is an `int` when it is whole and a `double` otherwise, which is how a JSON
 * trace carries them: a double property that happens to be whole on a frame reads as an int here,
 * and a trace that knows better passes a {@link NumberValue}. A JavaScript Date is read by its own
 * local fields. A Value passes through.
 */
export function toValue(raw: unknown): Value {
  if (raw === null || raw === undefined) return null;
  if (typeof raw === 'boolean' || typeof raw === 'string') return raw;
  if (typeof raw === 'number') return Number.isInteger(raw) ? int(raw) : double(raw);
  if (raw instanceof Date) {
    return date(Date.UTC(raw.getFullYear(), raw.getMonth(), raw.getDate(), raw.getHours(), raw.getMinutes(), raw.getSeconds(), raw.getMilliseconds()));
  }
  if (typeof raw === 'object' && 'kind' in raw) {
    const kind = (raw as { kind: unknown }).kind;
    if (kind === 'int' || kind === 'double' || kind === 'single' || kind === 'decimal' || kind === 'timespan' || kind === 'date') return raw as Value;
  }
  // A boxed Single from the older test evaluator, or anything else with a numeric valueOf.
  if (typeof raw === 'object' && typeof (raw as { valueOf?: unknown }).valueOf === 'function') {
    const n = (raw as { valueOf(): unknown }).valueOf();
    if (typeof n === 'number') return single(n);
  }
  throw new TypeError(`${String(raw)} is not a value an NCalc expression can hold`);
}

/** A value as plain JavaScript, for a caller or a test that does not care about the CLR type. */
export function toJs(v: Value): null | boolean | string | number {
  if (v === null || typeof v === 'boolean' || typeof v === 'string') return v;
  if (isNumber(v)) return v.value;
  if (isTimeSpan(v)) return v.ticks / TICKS_PER_SECOND;
  return v.wallMs;
}

/** `Object.Equals` between two values: the same type and the same value. What `changed` compares with. */
export function sameValue(a: Value, b: Value): boolean {
  if (a === null || b === null || typeof a !== 'object' || typeof b !== 'object') return a === b;
  if (a.kind !== b.kind) return false;
  if (isNumber(a) && isNumber(b)) return a.value === b.value;
  if (isTimeSpan(a) && isTimeSpan(b)) return a.ticks === b.ticks;
  return (a as DateValue).wallMs === (b as DateValue).wallMs;
}
