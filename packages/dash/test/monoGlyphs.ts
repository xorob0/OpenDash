/**
 * What a monospaced item can actually draw, and whether each glyph of it fits its cell.
 *
 * `metrics.ts` has said since the cells were cut that "the cell holds every glyph a value can
 * draw, not only the digits", and nothing enforced it. `textFit.test.ts` came closest: it measured
 * glyphs against cells, but only over the dash faces and only against a hard-coded
 * `0123456789-+/OFF`. That is exactly why thirty-one `#` survived on the second screens — they are
 * not in that loop, and `#` is not in that string.
 *
 * So the set under test is the item's own text rather than a fixed one: its sample, the widest
 * value it declares, and every string literal its `Text` binding can produce. A value that gains a
 * character nobody measured fails here rather than on somebody's dash.
 */
import { measureText, type MeasuredFace } from '../src/design/advances.ts';
import { MINUS } from '../src/design/metrics.ts';
import type { TextItem } from '../src/generator.ts';

/** Which measured face an item draws in: the family it names, at the weight it asks for. */
export const faceOf = (item: TextItem): MeasuredFace => {
  if (item.font === 'Barlow') return item.fontWeight === 'Bold' ? 'BarlowBold' : 'BarlowMedium';
  if (item.fontWeight === 'Bold') return 'BarlowCondensedBold';
  if (item.fontWeight === 'Light') return 'BarlowCondensedLight';
  return 'BarlowCondensedSemiBold';
};

/** The expression bound to an item's text, whatever shape the binding takes. */
function textExpression(item: TextItem): string {
  const binding = item.bindings?.Text;
  if (!binding) return '';
  const f: unknown = (binding as { formula?: unknown }).formula;
  if (typeof f === 'string') return f;
  if (f && typeof f === 'object') {
    const o = f as { expression?: unknown };
    if (typeof o.expression === 'string') return o.expression;
  }
  return '';
}

/**
 * Which argument of a function reaches the screen as the characters it is written with.
 *
 * Most literals in an expression are never drawn. `format(x, 'HH:mm')` draws `14:32` and not an
 * `m`; `isnull([Mode], 'session') = 'alltime'` draws neither word. Collecting every literal reports
 * those as glyphs the item can draw and fails on an `m` nobody will ever see, which is a test that
 * gets switched off rather than fixed.
 *
 * So a literal counts only where every function enclosing it passes its characters through. `if`
 * and `isnull` do, for their branches. `ucase` and `left` do, for the text they operate on.
 * Everything else does not, and that is transitive: nothing inside `format(...)` is drawn as
 * written, however deeply it is nested.
 */
const DRAWS_ARGUMENT: Record<string, (index: number) => boolean> = {
  if: (i) => i >= 1,
  isnull: (i) => i >= 1,
  replace: (i) => i === 2,
  ucase: (i) => i === 0,
  lcase: (i) => i === 0,
  tcase: (i) => i === 0,
  left: (i) => i === 0,
  right: (i) => i === 0,
  padleft: (i) => i === 0,
  scroll: (i) => i === 0,
};

/**
 * Every `'literal'` an expression can put on the screen as written.
 *
 * A literal outside any call is drawable: string concatenation in NCalc is `+`, so
 * `('#') + (drivercarnumber(1))` has its hash at the top level. That is the shape this is really
 * looking for, and the shape that shipped thirty-one clipped glyphs.
 */
export function drawableLiterals(expression: string): string[] {
  const out: string[] = [];
  /** Names of the calls enclosing the cursor, with whether each one draws its current argument. */
  const stack: { drawing: boolean }[] = [];
  const drawing = (): boolean => stack.every((s) => s.drawing);
  /** The call whose argument list the cursor is in, innermost last. */
  const open: { name: string; index: number }[] = [];

  let i = 0;
  while (i < expression.length) {
    const ch = expression[i]!;
    if (ch === "'") {
      i += 1;
      let literal = '';
      while (i < expression.length) {
        if (expression[i] === '\\' && i + 1 < expression.length) {
          literal += expression[i + 1];
          i += 2;
          continue;
        }
        if (expression[i] === "'") {
          i += 1;
          break;
        }
        literal += expression[i];
        i += 1;
      }
      if (drawing()) out.push(literal);
      continue;
    }
    if (ch === '[') {
      const close = expression.indexOf(']', i);
      i = close < 0 ? expression.length : close + 1;
      continue;
    }
    if (ch === ',' && open.length > 0) {
      const top = open[open.length - 1]!;
      top.index += 1;
      const frame = stack[stack.length - 1];
      if (frame) frame.drawing = (DRAWS_ARGUMENT[top.name] ?? (() => false))(top.index);
      i += 1;
      continue;
    }
    if (ch === '(') {
      // Is this a call, or just a grouping bracket? Look back for an identifier.
      let k = i - 1;
      while (k >= 0 && expression[k] === ' ') k -= 1;
      let end = k + 1;
      while (k >= 0 && /[A-Za-z0-9_]/.test(expression[k]!)) k -= 1;
      const name = expression.slice(k + 1, end).toLowerCase();
      if (name !== '' && !['and', 'or', 'not', 'true', 'false', 'null'].includes(name)) {
        open.push({ name, index: 0 });
        stack.push({ drawing: (DRAWS_ARGUMENT[name] ?? (() => false))(0) });
      } else {
        // A grouping bracket passes whatever is inside it through unchanged.
        open.push({ name: '', index: 0 });
        stack.push({ drawing: true });
      }
      i += 1;
      continue;
    }
    if (ch === ')') {
      open.pop();
      stack.pop();
      i += 1;
      continue;
    }
    i += 1;
  }
  return out;
}

/**
 * Digits are always drawable whether or not the sample happens to show them, because every
 * monospaced value is a number at heart and any of the ten can arrive at runtime. The minus is in
 * for the same reason: a value is signed wherever it can go negative, its sample is as often the
 * positive one, and the glyph a signed value reaches the screen with is U+2212 rather than the
 * hyphen the .NET formatter writes.
 */
const ALWAYS_DRAWN = `0123456789${MINUS}`;

/** Every character a monospaced item can put on the screen. */
export function drawableGlyphs(item: TextItem): Set<string> {
  const glyphs = new Set<string>(ALWAYS_DRAWN);
  for (const source of [item.text, item.widest ?? '', ...drawableLiterals(textExpression(item))]) {
    for (const ch of source) glyphs.add(ch);
  }
  glyphs.delete(' ');
  return glyphs;
}

export interface Overrun {
  item: string;
  glyph: string;
  advance: number;
  cell: number;
}

/**
 * Every glyph of a monospaced item that is wider than the cell it would be laid in.
 *
 * SimHub's monospace mode puts each character in a fixed cell — `CharWidth`, or
 * `SpecialCharsWidth` for the ones named in `SpecialChars` — and WPF clips whatever overruns it.
 * The clip looks exactly like a font problem, which is how the `#` was read for months as the
 * condensed face being wrong.
 */
export function cellOverruns(item: TextItem): Overrun[] {
  const mono = item.monospace;
  if (!mono) return [];
  const face = faceOf(item);
  const specials = mono.specialChars ?? '';
  const out: Overrun[] = [];
  for (const glyph of drawableGlyphs(item)) {
    const cell = specials.includes(glyph) ? mono.specialCharsWidth : mono.charWidth;
    const advance = measureText(face, glyph, item.fontSize);
    if (advance > cell) out.push({ item: item.name, glyph, advance: Math.round(advance * 100) / 100, cell });
  }
  return out;
}
