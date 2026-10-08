/**
 * NCalc's operators and the conversions under them, as NCalc 1.3.8's `EvaluationVisitor` and
 * `Numbers` do them. SimHub 9.12.6 ships that NCalc; the comparison rule below is the one
 * decompiled and seen on the VM for #762, and the rest is read from the same library's source.
 *
 * - **Comparison** (`=`, `!=`, `<`, ...) converts both sides to "the most precise type" and compares
 *   with `Comparer.Default`. The most precise type is the first of Int64, Double, Boolean, String,
 *   Decimal that either side is, and otherwise the left side's type. So an int and a double compare
 *   as doubles, a bool and an int as bools, an int and a string as strings, and a double and a
 *   non-numeric string throw. A null on either side throws, which is what `null > 0` does on the dash.
 * - **Arithmetic** keeps the operands' type: an int over an int is integer division, and an int
 *   divided by zero throws. A string operand is parsed as a decimal first, and throws when it is not
 *   a number. A null or a boolean operand throws.
 * - **`+`** concatenates when the **left** operand is a string, and is arithmetic otherwise: `'a' +
 *   1` is `a1`, and `1 + 'a'` throws because `'a'` is not a number. Not verified on the VM.
 * - **`and`, `or`, `!` and `if`** convert with `Convert.ToBoolean`, under which a null is false, and
 *   `and` and `or` stop at the left operand when it decides (#762).
 */
import { Fails, Unsupported } from './errors.ts';
import { valueToString } from './dotnetFormat.ts';
import { decimal, double, int, isDate, isNumber, isTimeSpan, single, typeName, type NumberKind, type NumberValue, type Value } from './values.ts';

/** `Math.Round(double)`: to the nearest integer, a half to the even one. */
export function roundHalfEven(x: number): number {
  const floor = Math.floor(x);
  const diff = x - floor;
  if (diff > 0.5) return floor + 1;
  if (diff < 0.5) return floor;
  return floor % 2 === 0 ? floor : floor + 1;
}

/** `Double.Parse` in en-US, with what `NumberStyles.Float | AllowThousands` admits. */
function parseNumber(s: string): number | undefined {
  const t = s.trim().replace(/,(?=\d{3}(\D|$))/g, '');
  if (!/^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$/.test(t)) return undefined;
  return Number(t);
}

/** `Convert.ToBoolean(object)`. */
export function toBoolean(v: Value): boolean {
  if (v === null) return false;
  if (typeof v === 'boolean') return v;
  if (typeof v === 'string') {
    const t = v.trim().toLowerCase();
    if (t === 'true') return true;
    if (t === 'false') return false;
    throw new Fails(`String ${JSON.stringify(v)} was not recognized as a valid Boolean`);
  }
  if (isNumber(v)) return v.value !== 0;
  throw new Fails(`a ${typeName(v)} cannot be converted to a Boolean`);
}

/** `Convert.ToDouble(object)`: a null is 0, a boolean 1 or 0, a string parsed. */
export function toDouble(v: Value): number {
  if (v === null) return 0;
  if (typeof v === 'boolean') return v ? 1 : 0;
  if (typeof v === 'string') {
    const n = parseNumber(v);
    if (n === undefined) throw new Fails(`Input string ${JSON.stringify(v)} was not in a correct format`);
    return n;
  }
  if (isNumber(v)) return v.value;
  throw new Fails(`a ${typeName(v)} cannot be converted to a Double`);
}

/** `Convert.ToInt32(object)`: a double is rounded half to even, as .NET converts one. */
export function toInt32(v: Value): number {
  if (isNumber(v) && v.kind === 'int') return v.value;
  if (typeof v === 'string') {
    const t = v.trim();
    if (!/^[+-]?\d+$/.test(t)) throw new Fails(`Input string ${JSON.stringify(v)} was not in a correct format`);
    return Number(t);
  }
  return roundHalfEven(toDouble(v));
}

/** NCalc's `ConvertIfString`: a string operand of arithmetic is `Decimal.Parse`d. */
function numericOperand(v: Value, op: string): NumberValue {
  if (typeof v === 'string') {
    const n = parseNumber(v);
    if (n === undefined) throw new Fails(`Input string ${JSON.stringify(v)} was not in a correct format, as an operand of ${op}`);
    return decimal(n);
  }
  if (v === null) throw new Fails(`null is an operand of ${op}`);
  if (isNumber(v)) return v;
  if (isTimeSpan(v) || isDate(v)) throw new Unsupported(`${op} on a ${typeName(v)} is something NCalc does in its own way, and the evaluator does not`);
  throw new Fails(`Operator '${op}' can't be applied to a ${typeName(v)}`);
}

/** The type of an arithmetic result, or undefined where C# has no such operator (float and decimal). */
function promote(a: NumberKind, b: NumberKind): NumberKind | undefined {
  if (a === b) return a;
  const pair = new Set([a, b]);
  if (pair.has('decimal')) return pair.has('int') ? 'decimal' : undefined;
  if (pair.has('double')) return 'double';
  return 'single';
}

const make = (kind: NumberKind, value: number): NumberValue => {
  switch (kind) {
    case 'int':
      return int(value);
    case 'double':
      return double(value);
    case 'single':
      return single(value);
    case 'decimal':
      return decimal(value);
  }
};

export type ArithmeticOperator = '+' | '-' | '*' | '/' | '%';

/** NCalc's `Numbers.Add`, `Soustract`, `Multiply`, `Divide` and `Modulo`. */
export function arithmetic(op: ArithmeticOperator, left: Value, right: Value): NumberValue {
  const a = numericOperand(left, op);
  const b = numericOperand(right, op);
  const kind = promote(a.kind, b.kind);
  if (kind === undefined) throw new Fails(`Operator '${op}' can't be applied to operands of types '${typeName(a)}' and '${typeName(b)}'`);
  const x = a.value;
  const y = b.value;
  if ((kind === 'int' || kind === 'decimal') && (op === '/' || op === '%') && y === 0) throw new Fails('Attempted to divide by zero');
  switch (op) {
    case '+':
      return make(kind, kind === 'single' ? Math.fround(x) + Math.fround(y) : x + y);
    case '-':
      return make(kind, x - y);
    case '*':
      return make(kind, x * y);
    case '/':
      return make(kind, kind === 'int' ? Math.trunc(x / y) : x / y);
    case '%':
      return make(kind, x % y);
  }
}

/** `+`: concatenation when the left operand is a string, arithmetic otherwise. */
export function plus(left: Value, right: Value): Value {
  if (typeof left === 'string') return left + valueToString(right);
  return arithmetic('+', left, right);
}

/** Unary minus, which NCalc evaluates as `0 - v` with an Int32 zero. */
export const negate = (v: Value): Value => arithmetic('-', int(0), v);

type ClrType = 'Int32' | 'Int64' | 'Double' | 'Single' | 'Decimal' | 'Boolean' | 'String' | 'TimeSpan' | 'DateTime';

/**
 * The CLR type of a value. An `int` is Int32 here: NCalc reads a small literal as one, and the
 * difference from Int64 never changes which comparison is made, since Int64 heads the list and
 * Int32 converts to it exactly.
 */
function clrType(v: Exclude<Value, null>): ClrType {
  if (typeof v === 'boolean') return 'Boolean';
  if (typeof v === 'string') return 'String';
  switch (v.kind) {
    case 'int':
      return 'Int32';
    case 'double':
      return 'Double';
    case 'single':
      return 'Single';
    case 'decimal':
      return 'Decimal';
    case 'timespan':
      return 'TimeSpan';
    case 'date':
      return 'DateTime';
  }
}

const COMMON_TYPES: readonly ClrType[] = ['Int64', 'Double', 'Boolean', 'String', 'Decimal'];

/** NCalc's `GetMostPreciseType`: the first common type either side is, else the left side's. */
function mostPreciseType(a: ClrType, b: ClrType): ClrType {
  for (const t of COMMON_TYPES) if (a === t || b === t) return t;
  return a;
}

/** `Convert.ChangeType(v, type)` for the types a comparison converts to, as a comparable JavaScript value. */
function changeType(v: Exclude<Value, null>, type: ClrType): number | boolean | string {
  switch (type) {
    case 'Int32':
    case 'Int64':
      if (isTimeSpan(v) || isDate(v)) throw new Fails(`Invalid cast from '${typeName(v)}' to '${type}'`);
      return toInt32(v);
    case 'Double':
    case 'Decimal':
      if (isTimeSpan(v) || isDate(v)) throw new Fails(`Invalid cast from '${typeName(v)}' to '${type}'`);
      return toDouble(v);
    case 'Single':
      if (isTimeSpan(v) || isDate(v)) throw new Fails(`Invalid cast from '${typeName(v)}' to 'Single'`);
      return Math.fround(toDouble(v));
    case 'Boolean':
      return toBoolean(v);
    case 'String':
      return valueToString(v);
    case 'TimeSpan':
      if (!isTimeSpan(v)) throw new Fails(`Invalid cast from '${typeName(v)}' to 'TimeSpan'`);
      return v.ticks;
    case 'DateTime':
      if (!isDate(v)) throw new Fails(`Invalid cast from '${typeName(v)}' to 'DateTime'`);
      return v.wallMs;
  }
}

/**
 * NCalc's `CompareUsingMostPreciseType`: negative, zero or positive.
 *
 * Two strings compare with the current culture's comparer, as `Comparer.Default` does in en-US;
 * equality is ordinal here, which agrees with it for every string a dashboard compares.
 */
export function compare(left: Value, right: Value): number {
  if (left === null || right === null) throw new Fails('a comparison has a null operand');
  const type = mostPreciseType(clrType(left), clrType(right));
  const a = changeType(left, type);
  const b = changeType(right, type);
  if (typeof a === 'string' && typeof b === 'string') return a === b ? 0 : a.localeCompare(b, 'en-US');
  if (typeof a === 'boolean' && typeof b === 'boolean') return a === b ? 0 : a ? 1 : -1;
  const x = a as number;
  const y = b as number;
  return x < y ? -1 : x > y ? 1 : 0;
}

export type ComparisonOperator = '=' | '!=' | '<' | '<=' | '>' | '>=';

export function comparison(op: ComparisonOperator, left: Value, right: Value): boolean {
  const c = compare(left, right);
  switch (op) {
    case '=':
      return c === 0;
    case '!=':
      return c !== 0;
    case '<':
      return c < 0;
    case '<=':
      return c <= 0;
    case '>':
      return c > 0;
    case '>=':
      return c >= 0;
  }
}
