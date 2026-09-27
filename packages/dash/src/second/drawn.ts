/**
 * Where a reading ends, as against where its budget does.
 *
 * A monospaced value is cut for the longest reading it can ever hold and left aligned in those
 * cells, so the cells past the figure are empty and anything placed at the end of them stands off
 * the figure by however many digits it happens not to have. `MARGIN −4` on band D is the case the
 * VM photographed: the budget holds `−999.9`, the reading is two cells, and `MIN` sat four cells
 * out -- nearer `EST. LAPS` than its own number, so it read as a field of its own rather than as
 * the unit of the one beside it. `FUEL 30.35 L` had the same shape a cell smaller.
 *
 * The answer is the one `cards/speed.ts` and `cards/fuel.ts` have always used and that #384 gave
 * the tyre corners: the follower's `Left` is bound to what the value really draws, so the figure
 * stays in its cells and only the mark after it moves. The arithmetic lives here rather than in
 * each drawing because three surfaces place followers -- the second-screen fields, the tyre
 * corners and band D -- and a rule written three times is a rule with three answers. #387.
 */
import type { Monospace } from '../generator.ts';
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { SPECIAL_CHARS, monoWidth, type Chars } from '../design/metrics.ts';

const { abs, add, digitCount, ge, iff, mul, num, round } = ncalc;

/** The cells a literal string takes: `.,:` get the narrow cell and everything else the wide one. */
export function charsOfText(text: string, mono: Monospace): Chars {
  const specials = [...text].filter((c) => (mono.specialChars ?? SPECIAL_CHARS).includes(c)).length;
  return { digits: text.length - specials, specials };
}

/** The width a literal string takes in `mono`'s cells. */
export const textWidth = (text: string, mono: Monospace): number => monoWidth(mono, charsOfText(text, mono));

/** How wide a value really draws, in the cells it is set in. */
export type DrawnFigure = (mono: Monospace) => Expr;

/**
 * A value's answer to "how wide are you really".
 *
 * `'fixed'` is the answer for a reading that never changes length -- a clock, a lap time, the
 * `-:--:--` that replaces either -- where the design-time place is already the only place and a
 * binding would say nothing. Every other bound value with a follower gives an expression; one that
 * gives neither is refused where it is drawn, for the reason a bound unit with no `widest` is,
 * namely that the declaration is the only place the answer can be.
 */
export type DrawnWidth = DrawnFigure | 'fixed';

/** A number as a format writes it: what it is, how many cells its whole part may take, and its shape. */
export interface Figure {
  /** The number itself, before formatting. */
  value: Expr;
  /** Whole digits the budget allows, the sign not counted. */
  digits: number;
  /** Decimals the format writes; none by default. */
  decimals?: number;
  /** True where the format always writes a sign, which takes a digit cell of its own. */
  signed?: boolean;
  /**
   * True where the format groups thousands, `#,0`, so the figure carries a comma every three digits
   * and each comma is one of the narrow cells `.,:` are given. The engine speed is the only reading
   * that does: `7,420` is five cells and `12,450` is six.
   */
  grouped?: boolean;
}

/**
 * The width of a grouped whole number: its digits, and the comma every three of them.
 *
 * Written as its own ladder rather than added to {@link figureWidth}'s, because the commas are a
 * step function of the digit count and not a constant: 999 has none, 1,000 has one, 1,000,000 two.
 */
const groupedWidth = (value: Expr, maxDigits: number, mono: Monospace): Expr => {
  const at = (digits: number): number => digits * mono.charWidth + Math.floor((digits - 1) / 3) * mono.specialCharsWidth;
  let e: Expr = num(at(1));
  for (let d = 2; d <= maxDigits; d++) e = iff(ge(value, num(10 ** (d - 1))), num(at(d)), e);
  return e;
};

/**
 * The width `figure` really draws in `mono`'s cells, as an expression.
 *
 * Rounded to the decimals it is formatted to before its digits are counted, because 9.96 drawn to
 * one decimal is `10.0` and has the second whole digit the unrounded number does not.
 */
export const figureWidth = (figure: Figure, mono: Monospace): Expr => {
  if (figure.grouped === true) {
    if (figure.decimals !== undefined || figure.signed === true) throw new Error('a grouped figure is a whole number and unsigned; nothing draws one that is not');
    return groupedWidth(figure.value, figure.digits, mono);
  }
  const decimals = figure.decimals ?? 0;
  const magnitude = figure.signed ? abs(figure.value) : figure.value;
  const whole = digitCount(round(magnitude, decimals), figure.digits);
  const extra = (figure.signed ? 1 : 0) + decimals;
  const drawn = mul(extra === 0 ? whole : add(whole, num(extra)), num(mono.charWidth));
  // One decimal point, in the narrow cell `.` is given.
  return decimals === 0 ? drawn : add(drawn, num(mono.specialCharsWidth));
};

/** {@link figureWidth} as a {@link DrawnFigure}. */
export const drawnFigure =
  (figure: Figure): DrawnFigure =>
  (mono) =>
    figureWidth(figure, mono);

/** A literal reading, which is the same width whenever it is the one on the screen. */
export const drawnText =
  (text: string): DrawnFigure =>
  (mono) =>
    num(textWidth(text, mono));

/**
 * One reading while `when` is true and another while it is not: the absence a gated field draws in
 * place of its figure, or the two shapes a unit setting switches a value between.
 */
export const drawnEither =
  (when: Expr, then: DrawnFigure, otherwise: DrawnFigure): DrawnFigure =>
  (mono) =>
    iff(when, then(mono), otherwise(mono));

/** A reading drawn after a fixed prefix, as the companion header writes `L` before the lap. */
export const drawnAfter =
  (prefix: string, drawn: DrawnFigure): DrawnFigure =>
  (mono) =>
    add(num(textWidth(prefix, mono)), drawn(mono));

/**
 * A reading that draws `text` in place of its figure while `absent` is true, which is every field
 * behind a settled-consumption gate and every one the sim does not publish.
 */
export const drawnOr = (absent: Expr, text: string, drawn: DrawnFigure): DrawnFigure => drawnEither(absent, drawnText(text), drawn);

/**
 * A drawn width that stops at the cells its value is laid in.
 *
 * A declaration wider than the budget is a mark bound outside the region the box was measured for,
 * and the least wrong place for a mark whose figure WPF has already clipped is the end of the ink
 * that survived, which is the end of the budget. Band D's energy and refuel are the two: cut for
 * three bare digit cells and formatted `0.0`, so `68.0` overruns the box before the `%` is placed at
 * all. Widening those two budgets is the other half, is recorded in `docs/design/zones.md` §10 and
 * is not this branch's; this is what keeps the mark inside the field until it happens.
 */
export const drawnAtMost =
  (chars: Chars, drawn: DrawnFigure): DrawnFigure =>
  (mono) =>
    ncalc.min(drawn(mono), num(monoWidth(mono, chars)));

/** The narrowest and the widest a placement expression can come to, in pixels. */
export interface DrawnRange {
  min: number;
  max: number;
}

/** Split `s` at every `op` outside a bracket and outside a quoted literal. */
function splitTop(s: string, op: string): string[] {
  const parts: string[] = [];
  let depth = 0;
  let start = 0;
  let quoted = false;
  for (let i = 0; i < s.length; i++) {
    const c = s[i];
    if (quoted) {
      if (c === '\\') i += 1;
      else if (c === "'") quoted = false;
      continue;
    }
    if (c === "'") quoted = true;
    else if (c === '(' || c === '[') depth += 1;
    else if (c === ')' || c === ']') depth -= 1;
    else if (depth === 0 && c === op) {
      parts.push(s.slice(start, i));
      start = i + 1;
    }
  }
  parts.push(s.slice(start));
  return parts;
}

/** Whether one bracket opens `s` and closes at its end, which is how `add` and `mul` wrap a term. */
function bracketed(s: string): boolean {
  if (!s.startsWith('(') || !s.endsWith(')')) return false;
  let depth = 0;
  for (let i = 0; i < s.length; i++) {
    if (s[i] === '(') depth += 1;
    else if (s[i] === ')') {
      depth -= 1;
      if (depth === 0) return i === s.length - 1;
    }
  }
  return false;
}

const spread = (values: number[]): DrawnRange => ({ min: Math.min(...values), max: Math.max(...values) });

/**
 * Every place a placement expression can put a follower: the leftmost and the rightmost.
 *
 * A width expression is arithmetic over literals once the conditions are set aside. `digitCount` is
 * a ladder of `if`s whose branches are digit counts, a grouped figure is the same ladder over its
 * commas, and what surrounds them is a sum or a product of pixel constants; so taking both branches
 * of every `if` and never reading what chooses between them bounds the whole expression without a
 * single reading, and the interval that comes out is where the mark can land.
 *
 * That is the one question no screenshot of DashStudio answers. A rect says where the editor draws
 * the mark, a binding says where the dash draws it, and the two are written in different places:
 * the companion header laid its pair from zero and moved the rectangles afterwards, so both
 * denominators were bound to a place the header never draws at and jumped to its top-left corner on
 * all 21 pages. The rect was right, the formula read the right lap, and only a live session showed
 * it. Holding the rect against this interval is what catches that. #387.
 *
 * Undefined where the expression is not arithmetic over literals -- a bare property read, a
 * division, a function this does not know -- because a bound arrived at by guessing is worse than
 * no bound at all.
 */
export function drawnRange(expr: Expr): DrawnRange | undefined {
  const s = expr.trim();
  if (s === '') return undefined;
  const literal = Number(s);
  if (Number.isFinite(literal)) return { min: literal, max: literal };

  const sum = splitTop(s, '+');
  if (sum.length > 1) return fold(sum, (a, b) => ({ min: a.min + b.min, max: a.max + b.max }));

  // Only where every term is there: a leading `-` is a negative literal and not a subtraction.
  const difference = splitTop(s, '-');
  if (difference.length > 1 && difference.every((part) => part.trim() !== '')) {
    return fold(difference, (a, b) => ({ min: a.min - b.max, max: a.max - b.min }));
  }

  const product = splitTop(s, '*');
  if (product.length > 1) return fold(product, (a, b) => spread([a.min * b.min, a.min * b.max, a.max * b.min, a.max * b.max]));

  // A rank that centres its row halves the slack it has left, which is where a division comes from.
  const quotient = splitTop(s, '/');
  if (quotient.length > 1) {
    let range: DrawnRange | undefined;
    for (const term of quotient) {
      const bound = drawnRange(term);
      // A divisor that can be zero bounds nothing, and nothing the build writes divides by a reading.
      if (bound === undefined || (range !== undefined && bound.min <= 0 && bound.max >= 0)) return undefined;
      range = range === undefined ? bound : spread([range.min / bound.min, range.min / bound.max, range.max / bound.min, range.max / bound.max]);
    }
    return range;
  }

  if (bracketed(s)) return drawnRange(s.slice(1, -1));

  const call = /^([A-Za-z_][A-Za-z0-9_]*)\s*\(([\s\S]*)\)$/.exec(s);
  if (call !== null && bracketed(s.slice((call[1] ?? '').length))) {
    const name = (call[1] ?? '').toLowerCase();
    const args = splitTop(call[2] ?? '', ',').map((arg) => drawnRange(arg));
    const [first, second, third] = args;
    if (name === 'if' && args.length === 3 && second && third) return spread([second.min, second.max, third.min, third.max]);
    if ((name === 'min' || name === 'max') && args.length === 2 && first && second) {
      const pick = name === 'min' ? Math.min : Math.max;
      return { min: pick(first.min, second.min), max: pick(first.max, second.max) };
    }
  }
  return undefined;
}

/** `combine` over the bounds of every term, and undefined as soon as one term has none. */
function fold(terms: string[], combine: (a: DrawnRange, b: DrawnRange) => DrawnRange): DrawnRange | undefined {
  let range: DrawnRange | undefined;
  for (const term of terms) {
    const bound = drawnRange(term);
    if (bound === undefined) return undefined;
    range = range === undefined ? bound : combine(range, bound);
  }
  return range;
}

/**
 * `drawn`'s expression, held against the cells its value is laid in.
 *
 * A follower is bound to what its figure draws and its box is measured from the end of the figure's
 * *budget*, that being the rightmost place a figure filling its cells can push it. So a value
 * declaring that it draws wider than its own budget binds the mark outside the region anything was
 * measured for, and no fit test can see it: `textFit` and `faceFit` measure boxes, not bound
 * `Left`s. Band D's energy and refuel were the case -- cut for three bare digit cells, formatted
 * `0.0`, so the declaration said 71 px where the budget holds 48 and the `%` went past the far side
 * of a box WPF had already clipped. {@link drawnAtMost} is the answer where the budget cannot widen
 * yet; this is what makes the next mismatch between a format and a budget a failed build. #387.
 */
export function drawnWithin(what: string, drawn: DrawnFigure, chars: Chars, mono: Monospace): Expr {
  const expr = drawn(mono);
  const range = drawnRange(expr);
  if (range === undefined) {
    throw new Error(`${what}: the drawn width ${JSON.stringify(expr)} is not arithmetic over literals, so nothing can say where it puts the mark; build it from second/drawn.ts`);
  }
  const budget = monoWidth(mono, chars);
  if (range.max > budget) {
    throw new Error(
      `${what}: the value says it can draw ${range.max} px where its budget holds ${budget}, so the mark after it would sit past the cells its box was measured for; widen the budget, or clamp the declaration to it with drawnAtMost`,
    );
  }
  return expr;
}
