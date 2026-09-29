/**
 * A small NCalc evaluator, so a reading can be tested by what a driver would see rather than by the
 * shape of the formula behind it.
 *
 * It covers the subset the expressions under test use: `[Property]` reads, `if`, `isnull`, `format`
 * with and without its sign flag, `replace`, `ucase`, `timespantoseconds` (seconds are passed as
 * numbers, which is how SimHub's own TimeSpans arrive once read), `max`, `min`, `abs`, `round`,
 * `truncate`, `rootdashboardscreenname` (answered from {@link ROOT_SCREEN}), the comparisons, `and` /
 * `or` / `!`, and the arithmetic. Anything else is an error rather than a silent `undefined`: a test
 * that evaluates half an expression proves nothing.
 *
 * It lived inside `session.test.ts` until the fuel margin needed the same thing (#387): the margin
 * is a subtraction whose two terms are drawn elsewhere on the same frame, so what is worth pinning
 * is the number it arrives at, and `signed` puts a `replace` and a sign flag in the way.
 */
export type Props = Record<string, unknown>;

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
            .replace(/\band\b/g, '&&')
            .replace(/\bor\b/g, '||')
            .replace(/ = /g, ' === ')
            .replace(/ != /g, ' !== '),
    )
    .join('');
  const fns = {
    P: (name: string): unknown => (name in props ? props[name] : null),
    IF: (c: unknown, a: unknown, b: unknown): unknown => (c ? a : b),
    isnull: (v: unknown, d?: unknown): unknown => (d === undefined ? v === null || v === undefined : (v ?? d)),
    format: formatNumber,
    replace: (v: string, from: string, to: string): string => String(v).split(from).join(to),
    ucase: (v: unknown): string => String(v).toUpperCase(),
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
