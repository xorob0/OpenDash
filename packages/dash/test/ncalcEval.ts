/**
 * A small NCalc evaluator, so a reading can be tested by what a driver would see rather than by the
 * shape of the formula behind it.
 *
 * It covers the subset the expressions under test use: `[Property]` reads, `if`, `isnull`, `format`
 * with and without its sign flag, `replace`, `ucase`, `timespantoseconds` (seconds are passed as
 * numbers, which is how SimHub's own TimeSpans arrive once read), `max`, `min`, `abs`, `round`,
 * `truncate`, `sin`, `cos`, `in`, `rootdashboardscreenname` (answered from {@link ROOT_SCREEN}), the
 * comparisons, `and` / `or` / `!`, and the arithmetic. A date is passed as a `Date` and formatted by
 * the hour and minute specifiers a clock uses, `HH`, `H`, `hh`, `h`, `mm` and `m`, in en-US's colon,
 * which is the culture SimHub sets at startup. Anything else is an error rather than a silent
 * `undefined`: a test that evaluates half an expression proves nothing.
 *
 * It lived inside `session.test.ts` until the fuel margin needed the same thing (#387): the margin
 * is a subtraction whose two terms are drawn elsewhere on the same frame, so what is worth pinning
 * is the number it arrives at, and `signed` puts a `replace` and a sign flag in the way.
 *
 * Every number is a JavaScript double, except one passed as a {@link Single}, which is how a raw
 * iRacing float reaches a binding.
 *
 * A literal is typed by its spelling, as NCalc types it: `0` is an Int32 and `0.0` a double. Running
 * as JavaScript, the expression cannot tell the two apart, and `max` and `min` need to, since NCalc
 * answers both in their left operand's type and an Int32 there rounds a reading to a whole number
 * (#831). So each of them is handed the parsed tree of its left operand and types it before it picks,
 * and `max(0, 0.5002)` is 1 here as it is on the dash. Until #1046 they were `Math.max` and
 * `Math.min`, which let a reading SimHub rounds pass every test that drew it.
 */
import { ncalcEvaluator as E } from '../src/generator.ts';

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

/** The CLR type of a number as NCalc holds it, and `other` for anything that is not a number. */
type Kind = 'int' | 'double' | 'single' | 'decimal' | 'other';

const REAL: ReadonlySet<Kind> = new Set<Kind>(['double', 'single', 'decimal']);

/**
 * The type NCalc's `Numbers` gives an arithmetic result: the wider operand's, with a float and a
 * decimal having no operator between them, as in C#. A string or a null on either side is `other`.
 */
function promote(a: Kind, b: Kind): Kind {
  if (a === 'other' || b === 'other') return 'other';
  if (a === b) return a;
  if (a === 'decimal' || b === 'decimal') {
    if (a === 'int' || b === 'int') return 'decimal';
    throw new Error(`ncalcEval: NCalc has no arithmetic between a ${a} and a ${b}`);
  }
  return a === 'double' || b === 'double' ? 'double' : 'single';
}

/**
 * What NCalc would type `node` as on this frame, reading the props as {@link evalNcalc} does: a
 * number a double, a {@link Single} a Single, and a literal by its spelling. Where the type depends
 * on what a branch or a fallback chose, the choosing operand is evaluated.
 */
function kindOf(node: E.Node, props: Props): Kind {
  const value = (n: E.Node): unknown => evalNcalc(E.print(n), props);
  switch (node.type) {
    case 'literal':
      return E.isNumber(node.value) ? node.value.kind : 'other';
    case 'property': {
      const v = node.name in props ? props[node.name] : null;
      return v instanceof Single ? 'single' : typeof v === 'number' ? 'double' : 'other';
    }
    case 'unary':
      // NCalc negates by subtracting from an Int32 zero.
      return node.op === '-' ? promote('int', kindOf(node.operand, props)) : 'other';
    case 'binary': {
      if (!['+', '-', '*', '/', '%'].includes(node.op)) return 'other';
      const a = kindOf(node.left, props);
      const b = kindOf(node.right, props);
      // `/` converts its left operand to a double when neither side is real, so 7 / 2 is 3.5: so the
      // EvaluationVisitor of the NCalc.dll on the test VM's share reads, decompiled for #1046.
      if (node.op === '/' && !REAL.has(a) && !REAL.has(b)) return promote('double', b);
      return promote(a, b);
    }
    case 'call': {
      const [first, second, third] = node.args;
      switch (node.name) {
        case 'if':
          return kindOf(value(first!) ? second! : third!, props);
        case 'isnull':
          return second === undefined ? 'other' : kindOf(value(first!) == null ? second : first!, props);
        case 'max':
        case 'min': {
          const left = value(first!);
          if (left == null) return kindOf(second!, props);
          return typeof left === 'string' ? 'decimal' : kindOf(first!, props);
        }
        case 'abs':
          return 'decimal';
        case 'round':
        case 'truncate':
        case 'sin':
        case 'cos':
        case 'timespantoseconds':
          return 'double';
        default:
          return 'other';
      }
    }
  }
}

/** `Convert.ToInt32(double)`: to the nearest whole number, a half to the even one. */
function toInt32(x: number): number {
  if (!(Math.abs(x) < 2 ** 31)) throw new Error(`ncalcEval: ${x} overflows an Int32`);
  const floor = Math.floor(x);
  const diff = x - floor;
  if (diff !== 0.5) return diff < 0.5 ? floor : floor + 1;
  return floor % 2 === 0 ? floor : floor + 1;
}

/**
 * NCalc 1.3.8's `Numbers.Max` and `Min`: a null side gives the other side, and otherwise the answer
 * has the left operand's type, the right one converted to it. `max(0, 0.5002)` is the Int32 1 and
 * `max(0.0, 0.5002)` the double 0.5002. A string is a decimal first, as `ConvertIfString` makes it,
 * and a left operand that is no number at all answers null.
 */
function extreme(pick: (a: number, b: number) => number, left: E.Node, props: Props, a: unknown, b: unknown): unknown {
  if (a === null || a === undefined) return b ?? null;
  if (b === null || b === undefined) return a;
  const kind: Kind = typeof a === 'string' ? 'decimal' : a instanceof Single ? 'single' : typeof a === 'number' ? kindOf(left, props) : 'other';
  const x = Number(a);
  const y = Number(b);
  switch (kind) {
    case 'int':
      return pick(x, toInt32(y));
    case 'double':
      return pick(x, y);
    case 'single':
      return new Single(pick(Math.fround(x), Math.fround(y)));
    case 'decimal':
      return pick(x, Number(y.toPrecision(15)));
    case 'other':
      if (typeof a === 'number') throw new Error(`ncalcEval: cannot tell what NCalc types ${E.print(left)} as`);
      return null;
  }
}

/** The max and min calls of an expression, outermost first, each with its left operand. */
function extremesOf(expression: string): { name: 'max' | 'min'; left: E.Node; nameSpan: E.Span }[] {
  if (!/\b(max|min)\s*\(/.test(expression)) return [];
  const out: { name: 'max' | 'min'; left: E.Node; nameSpan: E.Span }[] = [];
  for (const node of E.nodesOf(E.parseCached(expression).root)) {
    if (node.type !== 'call' || (node.name !== 'max' && node.name !== 'min')) continue;
    if (node.args.length !== 2) throw new Error(`ncalcEval: ${node.name}() takes two arguments, and has ${node.args.length}`);
    out.push({ name: node.name, left: node.args[0]!, nameSpan: node.nameSpan });
  }
  return out;
}

export function evalNcalc(expression: string, props: Props): unknown {
  // Each max and min becomes `EXTREME(i, a, b)`, which knows the tree of its left operand. Rewritten
  // from the end, so that the offsets of the calls before each one still hold.
  const extremes = extremesOf(expression);
  let source = expression;
  for (const [i, call] of [...extremes.entries()].sort(([, a], [, b]) => b.nameSpan.start - a.nameSpan.start)) {
    const open = source.indexOf('(', call.nameSpan.end);
    source = `${source.slice(0, call.nameSpan.start)}EXTREME(${i}, ${source.slice(open + 1)}`;
  }
  // Split on string literals so that operator rewriting never touches their contents.
  const js = source
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
    timespantoseconds: (v: unknown): number => Number(v),
    EXTREME: (i: number, a: unknown, b: unknown): unknown => {
      const call = extremes[i]!;
      return extreme(call.name === 'max' ? Math.max : Math.min, call.left, props, a, b);
    },
    abs: Math.abs,
    // NCalc's `Round(value, digits)`. .NET rounds a midpoint to even where this rounds it up; no
    // expression under test lands on one, and a reading that did would be a fault in the reading.
    round: (value: number, decimals = 0): number => {
      const scale = 10 ** decimals;
      return Math.round(value * scale) / scale;
    },
    truncate: Math.trunc,
    sin: Math.sin,
    cos: Math.cos,
    rootdashboardscreenname: (): unknown => (ROOT_SCREEN in props ? props[ROOT_SCREEN] : null),
  };
  return new Function(...Object.keys(fns), `return (${js});`)(...Object.values(fns));
}
