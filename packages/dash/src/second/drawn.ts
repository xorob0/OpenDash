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
