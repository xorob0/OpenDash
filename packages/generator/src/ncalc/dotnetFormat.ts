/**
 * .NET's number and date formatting, for the patterns the build writes, in en-US, which is the
 * culture SimHub sets at startup.
 *
 * Started from the formatter in `packages/dash/test/ncalcEval.ts`, which used `toFixed` and so
 * rounded the double itself. .NET Framework does not: a custom pattern first takes the double to
 * fifteen significant digits and then rounds the last kept place half away from zero
 * (docs/research/simhub-dash-format.md, "`format` rounds a half away from zero"), so 1.005 to two
 * places is `1.01` on the dash and `1.00` under `toFixed`. That is what this does.
 *
 * The patterns are the custom numeric ones built from `0`, `#`, `.` and `,` (`0`, `00`, `0.0`,
 * `0.00`, `0.000`, `#,##0`, `#,0`) and the date ones built from `H`, `h`, `m` and `s` with `:`
 * (`HH:mm`, `h:mm`, `HH`, `hh`). Anything else is refused rather than approximated.
 */
import { Unsupported } from './errors.ts';
import { timeSpanToString } from './timespan.ts';
import { isDate, isNumber, isTimeSpan, type DateValue, type NumberValue, type Value } from './values.ts';

/** How many significant digits .NET Framework keeps before it formats: 15 for a double, 7 for a Single. */
const precisionOf = (v: NumberValue): number => (v.kind === 'single' ? 7 : 15);

interface Digits {
  /** The digits of the magnitude, no sign, no leading zeros, at least one digit. */
  digits: string;
  /** The value is `0.digits * 10^point`: how many of `digits` stand before the decimal point. */
  point: number;
}

/** The magnitude of `x` to `significant` digits, rounded half away from zero, as a digit string. */
function significantDigits(x: number, significant: number): Digits {
  const a = Math.abs(x);
  if (a === 0) return { digits: '0', point: 1 };
  const [mantissa, exponent] = a.toExponential(significant - 1).split('e') as [string, string];
  return { digits: mantissa.replace('.', ''), point: Number(exponent) + 1 };
}

/** An integer's digits exactly. */
const integerDigits = (x: number): Digits => {
  const s = String(Math.abs(Math.trunc(x)));
  return { digits: s, point: s.length };
};

/**
 * Rounds a digit string to `places` after the point, half away from zero, and returns the integer
 * and fraction parts as strings, the fraction exactly `places` long.
 */
function roundTo(d: Digits, places: number): { int: string; frac: string } {
  // Lay the digits out as a plain decimal with enough zeros either side.
  let int: string;
  let frac: string;
  if (d.point <= 0) {
    int = '0';
    frac = '0'.repeat(-d.point) + d.digits;
  } else if (d.point >= d.digits.length) {
    int = d.digits + '0'.repeat(d.point - d.digits.length);
    frac = '';
  } else {
    int = d.digits.slice(0, d.point);
    frac = d.digits.slice(d.point);
  }
  const next = frac.charCodeAt(places) - 48;
  frac = frac.slice(0, places).padEnd(places, '0');
  if (next >= 5) {
    // Carry one into the last kept place.
    const all = (int + frac).split('').map(Number);
    let i = all.length - 1;
    while (i >= 0) {
      if (all[i]! < 9) {
        all[i]! += 1;
        break;
      }
      all[i] = 0;
      i -= 1;
    }
    let joined = all.join('');
    if (i < 0) joined = `1${joined}`;
    int = joined.slice(0, joined.length - places);
    frac = joined.slice(joined.length - places);
  }
  int = int.replace(/^0+(?=\d)/, '');
  return { int, frac };
}

interface NumericPattern {
  minInt: number;
  minFrac: number;
  maxFrac: number;
  grouping: boolean;
}

const patternCache = new Map<string, NumericPattern>();

function numericPattern(pattern: string): NumericPattern {
  const cached = patternCache.get(pattern);
  if (cached) return cached;
  if (!/^[0#,]*(\.[0#]*)?$/.test(pattern) || !/[0#]/.test(pattern)) {
    throw new Unsupported(`the .NET number format ${JSON.stringify(pattern)} is not one the evaluator knows; it reads 0, #, a point and grouping commas`);
  }
  const [intPart = '', fracPart = ''] = pattern.split('.');
  if (/,$/.test(intPart)) throw new Unsupported(`the .NET number format ${JSON.stringify(pattern)} scales by a thousand, which the evaluator does not do`);
  const firstZero = intPart.indexOf('0');
  const minInt = firstZero < 0 ? 0 : intPart.slice(firstZero).replace(/,/g, '').length;
  const lastZero = fracPart.lastIndexOf('0');
  const parsed = { minInt, minFrac: lastZero + 1, maxFrac: fracPart.length, grouping: intPart.includes(',') };
  patternCache.set(pattern, parsed);
  return parsed;
}

const group = (int: string): string => int.replace(/\B(?=(\d{3})+(?!\d))/g, ',');

/**
 * A number in a .NET custom numeric pattern.
 *
 * The sign follows .NET Framework: a double, a decimal or an int that is negative keeps its `-` even
 * where the rounded figure is zero (`-0.001` to `0.00` is `-0.00`), and a Single keeps it only where
 * the figure is not zero (docs/research/simhub-dash-format.md). With `addSign`, SimHub writes a `+`
 * in front of a double, a decimal or an int that is not negative, zero included, and never in front
 * of a Single. The `+` on a zero is not verified on the VM; the evaluator it was ported from wrote
 * one, and no committed test has said otherwise.
 */
export function formatNumber(v: NumberValue, pattern: string, addSign = false): string {
  const p = numericPattern(pattern);
  if (!Number.isFinite(v.value)) return Number.isNaN(v.value) ? 'NaN' : v.value > 0 ? 'Infinity' : '-Infinity';
  const digits = v.kind === 'int' ? integerDigits(v.value) : significantDigits(v.value, precisionOf(v));
  const { int: rawInt, frac: rawFrac } = roundTo(digits, p.maxFrac);
  let frac = rawFrac;
  while (frac.length > p.minFrac && frac.endsWith('0')) frac = frac.slice(0, -1);
  let int = rawInt === '0' ? '' : rawInt;
  if (int.length < p.minInt) int = int.padStart(p.minInt, '0');
  if (p.grouping) int = group(int);
  const body = `${int}${frac ? `.${frac}` : ''}`;
  const negative = v.value < 0 || Object.is(v.value, -0);
  const figureIsZero = !/[1-9]/.test(rawInt + rawFrac);
  if (v.kind === 'single') return negative && !figureIsZero ? `-${body}` : body;
  if (v.value < 0) return `-${body}`;
  return addSign ? `+${body}` : body;
}

/** A DateTime in a .NET custom date pattern made of the hour, minute and second specifiers. */
export function formatDate(v: DateValue, pattern: string): string {
  const d = new Date(v.wallMs);
  const hour = d.getUTCHours();
  const twelve = hour % 12 === 0 ? 12 : hour % 12;
  const fields: Record<string, string> = {
    HH: String(hour).padStart(2, '0'),
    H: String(hour),
    hh: String(twelve).padStart(2, '0'),
    h: String(twelve),
    mm: String(d.getUTCMinutes()).padStart(2, '0'),
    m: String(d.getUTCMinutes()),
    ss: String(d.getUTCSeconds()).padStart(2, '0'),
    s: String(d.getUTCSeconds()),
    ':': ':',
  };
  return pattern.replace(/HH|H|hh|h|mm|m|ss|s|./g, (token) => {
    const written = fields[token];
    if (written === undefined) throw new Unsupported(`the .NET date format ${JSON.stringify(pattern)} uses ${JSON.stringify(token)}, which the evaluator does not know; it reads H, h, m, s and a colon`);
    return written;
  });
}

/**
 * `ToString()` of a number, as `String.Concat` and a string comparison see it: .NET Framework's
 * general format, fifteen significant digits for a double and a decimal and seven for a Single,
 * trailing zeros dropped, and an exponent outside 1E-05 to 1E+15.
 */
export function numberToString(v: NumberValue): string {
  const x = v.value;
  if (Number.isNaN(x)) return 'NaN';
  if (!Number.isFinite(x)) return x > 0 ? 'Infinity' : '-Infinity';
  if (v.kind === 'int') return String(Math.trunc(x));
  if (x === 0) return '0';
  const precision = precisionOf(v);
  const { digits, point } = significantDigits(x, precision);
  const trimmed = digits.replace(/0+$/, '') || '0';
  const exponent = point - 1;
  const sign = x < 0 ? '-' : '';
  if (exponent >= precision || exponent < -5) {
    const mantissa = trimmed.length > 1 ? `${trimmed[0]}.${trimmed.slice(1)}` : trimmed;
    return `${sign}${mantissa}E${exponent < 0 ? '-' : '+'}${String(Math.abs(exponent)).padStart(2, '0')}`;
  }
  if (point <= 0) return `${sign}0.${'0'.repeat(-point)}${trimmed}`;
  if (point >= trimmed.length) return `${sign}${trimmed}${'0'.repeat(point - trimmed.length)}`;
  return `${sign}${trimmed.slice(0, point)}.${trimmed.slice(point)}`;
}

/** `DateTime.ToString()` in en-US: `M/d/yyyy h:mm:ss tt`. */
export function dateToString(v: DateValue): string {
  const d = new Date(v.wallMs);
  const hour = d.getUTCHours();
  const twelve = hour % 12 === 0 ? 12 : hour % 12;
  return `${d.getUTCMonth() + 1}/${d.getUTCDate()}/${d.getUTCFullYear()} ${twelve}:${String(d.getUTCMinutes()).padStart(2, '0')}:${String(d.getUTCSeconds()).padStart(2, '0')} ${hour < 12 ? 'AM' : 'PM'}`;
}

/**
 * Any value as `$"{v}"` and `String.Concat` write it: null as the empty string, a boolean as
 * `True` or `False`, a TimeSpan in its constant format.
 */
export function valueToString(v: Value): string {
  if (v === null) return '';
  if (typeof v === 'string') return v;
  if (typeof v === 'boolean') return v ? 'True' : 'False';
  if (isNumber(v)) return numberToString(v);
  if (isTimeSpan(v)) return timeSpanToString(v);
  if (isDate(v)) return dateToString(v);
  return String(v);
}
