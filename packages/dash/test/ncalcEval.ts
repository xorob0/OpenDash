/**
 * A small NCalc evaluator, so a reading can be tested by what a driver would see rather than by the
 * shape of the formula behind it.
 *
 * It covers the subset the expressions under test use: `[Property]` reads, `if`, `isnull`, `format`
 * with and without its sign flag, `replace`, `ucase`, SimHub's three-argument `left(value, start,
 * count)`, `timespantoseconds` (seconds are passed as numbers, which is how SimHub's own TimeSpans
 * arrive once read), `max`, `min`, `abs`, `round`, `truncate`, `in`, `rootdashboardscreenname`
 * (answered from {@link ROOT_SCREEN}), the leaderboard reads `getplayerleaderboardposition`,
 * `getopponentleaderboardposition_aheadbehind`, `drivercarclass`, `driverclassposition`,
 * `driverposition`, `driverpositiongain`, `driverpositiongainclass` and `driveravailable` (each answered from the props by its own call, as
 * `drivercarclass(3)`, and null where they leave it out), the comparisons, `and` / `or` / `!`, and
 * the arithmetic. A date is passed as a `Date` and formatted by the hour and
 * minute specifiers a clock uses, `HH`, `H`, `hh`, `h`, `mm` and `m`, in en-US's colon, which is the
 * culture SimHub sets at startup. Anything else is an error rather than a silent `undefined`: a test
 * that evaluates half an expression proves nothing.
 *
 * It lived inside `session.test.ts` until the fuel margin needed the same thing (#387): the margin
 * is a subtraction whose two terms are drawn elsewhere on the same frame, so what is worth pinning
 * is the number it arrives at, and `signed` puts a `replace` and a sign flag in the way.
 *
 * Every number is a JavaScript double, except one passed as a {@link Single}, which is how a raw
 * iRacing float reaches a binding.
 */
export type Props = Record<string, unknown>;

/**
 * A boxed System.Single: an iRacing irsdk_float, which SimHub passes to a binding unchanged, and
 * which `if` and `isnull` hand on unchanged.
 *
 * SimHub's `format(v, pattern, true)` writes its `+` only when the value is a double, a decimal or an
 * int, so a Single is formatted by .NET alone: a minus where the figure is negative and not a zero,
 * and nothing in front of anything else. The arithmetic, `abs` and the comparisons read it through
 * `valueOf`, which is the double it widens to, as NCalc promotes it; `=` does not, since it is
 * JavaScript's strict equality here, and nothing compares a Single with it. What the evaluator
 * cannot model is the other operand's type: NCalc keeps a Single times the Int32 `1` a Single, where
 * JavaScript's `* 1` makes a number of it like `* 1.0` does.
 */
export class Single {
  constructor(readonly v: number) {}
  valueOf(): number {
    return Math.fround(this.v);
  }
  toString(): string {
    return String(this.valueOf());
  }
}

/**
 * The key `rootdashboardscreenname()` answers from: the name of the screen SimHub drew last. Not a
 * name a `[Property]` read can reach, since the parentheses are outside what one is scanned for.
 */
export const ROOT_SCREEN = 'rootdashboardscreenname()';

/** .NET's `0`, `0.0`, `00` and so on, with the leading `+` NCalc's third argument asks for. */
const formatNumber = (value: number, pattern: string, addSign = false): string => {
  const [int = '0', frac = ''] = pattern.split('.');
  const [i = '0', f] = Math.abs(value).toFixed(frac.length).split('.');
  const sign = value < 0 ? '-' : addSign ? '+' : '';
  return `${sign}${i.padStart(int.length, '0')}${f ? `.${f}` : ''}`;
};

/**
 * A Single formatted with no sign asked of SimHub: .NET Framework writes a minus only where the
 * rounded figure has a digit that is not zero, so -0.004 to two places is `0.00`.
 */
const formatSingle = (value: Single, pattern: string): string => {
  const text = formatNumber(Math.abs(value.valueOf()), pattern);
  return value.valueOf() < 0 && /[1-9]/.test(text) ? `-${text}` : text;
};

/**
 * .NET's custom date format, for the specifiers a clock is written with. The twelve-hour hour runs
 * 12, 1, ... 11, which is `h`'s own reading of midnight and noon.
 */
const formatDate = (value: Date, pattern: string): string => {
  const hour = value.getHours();
  const minute = value.getMinutes();
  const twelve = hour % 12 === 0 ? 12 : hour % 12;
  const specifiers: Record<string, string> = {
    HH: String(hour).padStart(2, '0'),
    H: String(hour),
    hh: String(twelve).padStart(2, '0'),
    h: String(twelve),
    mm: String(minute).padStart(2, '0'),
    m: String(minute),
  };
  return pattern.replace(/HH|H|hh|h|mm|m|./g, (token) => {
    const written = specifiers[token] ?? (token === ':' ? ':' : undefined);
    if (written === undefined) throw new Error(`ncalcEval: the date specifier ${JSON.stringify(token)} is not one a clock is written with`);
    return written;
  });
};

export function evalNcalc(expression: string, props: Props): unknown {
  // Split on string literals so that operator rewriting never touches their contents.
  const js = expression
    .split(/('(?:[^'\\]|\\.)*')/)
    .map((part, i) =>
      i % 2 === 1
        ? part
        : part
            .replace(/\[([A-Za-z0-9_.]+)\]/g, (_, name: string) => `P(${JSON.stringify(name)})`)
            .replace(/\bif\(/g, 'IF(')
            .replace(/\bin\(/g, 'IN(')
            .replace(/\band\b/g, '&&')
            .replace(/\bor\b/g, '||')
            .replace(/ = /g, ' === ')
            .replace(/ != /g, ' !== '),
    )
    .join('');
  const called = (call: string): unknown => (call in props ? props[call] : null);
  const fns = {
    P: (name: string): unknown => (name in props ? props[name] : null),
    IF: (c: unknown, a: unknown, b: unknown): unknown => (c ? a : b),
    isnull: (v: unknown, d?: unknown): unknown => (d === undefined ? v === null || v === undefined : (v ?? d)),
    format: (value: unknown, pattern: string, addSign = false): string =>
      value instanceof Date
        ? formatDate(value, pattern)
        : value instanceof Single
          ? formatSingle(value, pattern)
          : formatNumber(value as number, pattern, addSign),
    // NCalc's `in`, which compares as `=` does: two strings as strings.
    IN: (value: unknown, ...options: unknown[]): boolean => options.some((option) => option === value),
    replace: (v: string, from: string, to: string): string => String(v).split(from).join(to),
    ucase: (v: unknown): string => String(v).toUpperCase(),
    left: (v: unknown, start: number, count: number): string => String(v).slice(start, start + count),
    getplayerleaderboardposition: (): unknown => called('getplayerleaderboardposition()'),
    drivercarclass: (position: unknown): unknown => called(`drivercarclass(${String(position)})`),
    driverclassposition: (position: unknown): unknown => called(`driverclassposition(${String(position)})`),
    driverposition: (position: unknown): unknown => called(`driverposition(${String(position)})`),
    driverpositiongain: (position: unknown): unknown => called(`driverpositiongain(${String(position)})`),
    driverpositiongainclass: (position: unknown): unknown => called(`driverpositiongainclass(${String(position)})`),
    driveravailable: (position: unknown): unknown => called(`driveravailable(${String(position)})`),
    getopponentleaderboardposition_aheadbehind: (offset: unknown): unknown => called(`getopponentleaderboardposition_aheadbehind(${String(offset)})`),
    timespantoseconds: (v: unknown): number => Number(v),
    max: Math.max,
    min: Math.min,
    abs: Math.abs,
    // NCalc's `Round(value, digits)`. .NET rounds a midpoint to even where this rounds it up; no
    // expression under test lands on one, and a reading that did would be a fault in the reading.
    round: (value: number, decimals = 0): number => {
      const scale = 10 ** decimals;
      return Math.round(value * scale) / scale;
    },
    truncate: Math.trunc,
    rootdashboardscreenname: (): unknown => (ROOT_SCREEN in props ? props[ROOT_SCREEN] : null),
  };
  return new Function(...Object.keys(fns), `return (${js});`)(...Object.values(fns));
}
