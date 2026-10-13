/**
 * TimeSpan and DateTime, as far as a dashboard expression reaches them.
 *
 * A trace carries a TimeSpan as .NET's constant ("c") format, `[-][d.]hh:mm:ss[.fffffff]`, and a
 * DateTime as ISO 8601 with its offset; these read them back, and do the three things SimHub's
 * functions do with them: `timespantoseconds`, `secondstotimespan` and `toshorttime`.
 */
import { date, TICKS_PER_MS, TICKS_PER_SECOND, timespan, type DateValue, type TimeSpanValue } from './values.ts';

const TICKS_PER_MINUTE = 60 * TICKS_PER_SECOND;
const TICKS_PER_HOUR = 60 * TICKS_PER_MINUTE;
const TICKS_PER_DAY = 24 * TICKS_PER_HOUR;

/** Parses .NET's constant TimeSpan format, which is what `TimeSpan.ToString()` writes and a trace carries. */
export function parseTimeSpan(text: string): TimeSpanValue {
  const m = /^(-)?(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?$/.exec(text.trim());
  if (!m) throw new Error(`${JSON.stringify(text)} is not a TimeSpan in .NET's constant format`);
  const [, minus, days = '0', h, min, s, frac = ''] = m;
  const ticks =
    Number(days) * TICKS_PER_DAY + Number(h) * TICKS_PER_HOUR + Number(min) * TICKS_PER_MINUTE + Number(s) * TICKS_PER_SECOND + Number(frac.padEnd(7, '0'));
  return timespan(minus ? -ticks : ticks);
}

/**
 * Parses an ISO 8601 DateTime, keeping its wall clock and dropping its offset: a dashboard formats
 * the time the recording machine showed, not the time it is where the page is read.
 */
export function parseDateTime(text: string): DateValue {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?(?:Z|[+-]\d{2}:\d{2})?$/.exec(text.trim());
  if (!m) throw new Error(`${JSON.stringify(text)} is not an ISO 8601 DateTime`);
  const [, y, mo, d, h, mi, s, frac = ''] = m;
  return date(Date.UTC(Number(y), Number(mo) - 1, Number(d), Number(h), Number(mi), Number(s), Math.trunc(Number(frac.padEnd(7, '0')) / TICKS_PER_MS)));
}

/** `TimeSpan.TotalSeconds`. */
export const totalSeconds = (ts: TimeSpanValue): number => ts.ticks / TICKS_PER_SECOND;

/**
 * `TimeSpan.FromSeconds`, which on .NET Framework rounds to the nearest millisecond, half away from
 * zero, before it makes ticks: `FromSeconds(1.23456)` is 1.235 s, not 1.23456.
 */
export function fromSeconds(seconds: number): TimeSpanValue {
  if (!Number.isFinite(seconds)) throw new RangeError('TimeSpan overflowed because the duration is too long');
  const ms = seconds * 1000;
  const rounded = ms >= 0 ? Math.trunc(ms + 0.5) : Math.trunc(ms - 0.5);
  return timespan(rounded * TICKS_PER_MS);
}

const pad = (n: number, width: number): string => String(n).padStart(width, '0');

/** `TimeSpan.ToString()`, the constant format: `[-][d.]hh:mm:ss[.fffffff]`. */
export function timeSpanToString(ts: TimeSpanValue): string {
  const neg = ts.ticks < 0;
  let t = Math.abs(ts.ticks);
  const days = Math.floor(t / TICKS_PER_DAY);
  t -= days * TICKS_PER_DAY;
  const h = Math.floor(t / TICKS_PER_HOUR);
  t -= h * TICKS_PER_HOUR;
  const m = Math.floor(t / TICKS_PER_MINUTE);
  t -= m * TICKS_PER_MINUTE;
  const s = Math.floor(t / TICKS_PER_SECOND);
  const frac = t - s * TICKS_PER_SECOND;
  return `${neg ? '-' : ''}${days ? `${days}.` : ''}${pad(h, 2)}:${pad(m, 2)}:${pad(s, 2)}${frac ? `.${pad(frac, 7)}` : ''}`;
}

/**
 * SimHub's `toshorttime(timespan, decimals, alwaysSign, forceMinutes)`.
 *
 * Verified on the VM for the shapes the packages use: `toshorttime(t, 3, false, true)` is
 * `m:ss.fff`, and an hour or more is `h:mm:ss.fff`. The fraction is **truncated** rather than
 * rounded, which was verified by decompiling WoteverCommon's `StringExtensions.ToShortTime` (SimHub
 * 9.12.x): it appends the TimeSpan custom format `\.fff`, and a TimeSpan `f` truncates. The lap
 * time leans on that (#883): a lap of ten minutes drops its thousandth, and the digit dropped is
 * the one the box already clipped. Not verified, and written as the most likely reading of the
 * same method: without `forceMinutes` and under a minute the seconds are drawn without a leading
 * zero (`5.2`); a negative span takes a `-` and `alwaysSign` puts a `+` on the others, zero
 * included.
 */
export function toShortTime(ts: TimeSpanValue, decimals: number, alwaysSign: boolean, forceMinutes: boolean): string {
  const neg = ts.ticks < 0;
  const t = Math.abs(ts.ticks);
  const hours = Math.floor(t / TICKS_PER_HOUR);
  const minutes = Math.floor((t % TICKS_PER_HOUR) / TICKS_PER_MINUTE);
  const seconds = Math.floor((t % TICKS_PER_MINUTE) / TICKS_PER_SECOND);
  const places = Math.max(0, Math.min(7, Math.trunc(decimals)));
  const fraction = places > 0 ? `.${pad(t % TICKS_PER_SECOND, 7).slice(0, places)}` : '';
  const sign = neg ? '-' : alwaysSign ? '+' : '';
  if (hours > 0) return `${sign}${hours}:${pad(minutes, 2)}:${pad(seconds, 2)}${fraction}`;
  if (forceMinutes || minutes > 0) return `${sign}${minutes}:${pad(seconds, 2)}${fraction}`;
  return `${sign}${seconds}${fraction}`;
}
