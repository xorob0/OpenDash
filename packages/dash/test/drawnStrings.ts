/**
 * What a `Text` binding can put on the screen, read off the expression rather than declared beside it.
 *
 * `widest` on a text item is a promise: the box is measured from it, `textFit.test.ts` and its
 * siblings hold the box to it, and nothing held the promise to the binding. The same defect was
 * then found three times in two days on #384's branch -- a tyre temps card measured by `TYRES °F`
 * where its binding also draws `TYRES °C`, a pressure unit measured by `kPa` where `pressureUnit()`
 * also draws `bar`, which is wider upper-cased -- and each was found by eye, in a review, after
 * every fit test had passed. A declaration that nothing checks is a declaration that drifts.
 *
 * `drawableLiterals` in monoGlyphs.ts is the nearest thing and is not enough on its own, because
 * it reads literals and a binding is usually a *composition* of literals: `'TYRES ' + (if(..,
 * '°F', ..)) + ' · LAST STOP'` has no literal wider than the declared `TYRES °F`, and draws a
 * string that is. So this reads the expression as a tree and works out what the tree can draw.
 *
 * It cannot know everything -- a `[Property]` or a `format(...)` is whatever the sim sends, short of
 * the few properties `VOCABULARIES` lists the words of -- and it does not pretend to. Every string
 * it returns is a **floor**: a string some output of the binding is certain to be at least as wide
 * as, in any face, because it is that output with the unknowable parts left out. A floor wider than `widest` is therefore a real defect and never a false alarm,
 * and a binding whose output is entirely unknowable has no floors and nothing to say, which is the
 * honest answer rather than a guess. A floor built from literals alone is also *exact* -- an
 * output as written -- and the readings that need a whole string, `left` and `replace`, act only on
 * those and give up on the rest, since a prefix of a partly known string is not a floor of anything.
 */
import { measureText } from '../src/design/advances.ts';
import type { TextItem } from '../src/generator.ts';
import { SESSION_NAMES } from '../src/second/values.ts';
import { bindingExpression, faceOf } from './monoGlyphs.ts';

/**
 * The properties whose every value is known, and the values: what the sim writes there is one of
 * these words, so each is an output as written rather than nothing anyone can say.
 *
 * Only a property with a closed vocabulary that a source of record names belongs here. The session
 * type is the one a box was cut too short for while every test passed, because a free property had
 * no floor to hold a `widest` to and the session page declared none (#1029).
 */
export const VOCABULARIES: Readonly<Record<string, readonly string[]>> = {
  'DataCorePlugin.GameData.SessionTypeName': SESSION_NAMES,
};

/**
 * True where an item's text is bound to an expression that reads a property whose words are known.
 *
 * Read off the expression as written, so a binding that only tests the property, as the fuel to the
 * end asks whether the session is a race, counts too. That asks a `widest` of it and nothing more.
 */
export function readsVocabulary(item: TextItem): boolean {
  const expression = bindingExpression(item, 'Text');
  return Object.keys(VOCABULARIES).some((property) => expression.includes(`[${property}]`));
}

type Node =
  | { kind: 'str'; value: string }
  | { kind: 'num'; value: number }
  | { kind: 'prop'; name: string }
  | { kind: 'ident'; name: string }
  | { kind: 'call'; name: string; args: Node[] }
  | { kind: 'unary'; op: string; arg: Node }
  | { kind: 'binary'; op: string; left: Node; right: Node }
  | { kind: 'ternary'; cond: Node; then: Node; else: Node };

type Token = { kind: 'str' | 'num' | 'prop' | 'ident' | 'op'; value: string };

/**
 * NCalc's tokens, as SimHub's `NCalcEngineBase` sees them: single- or double-quoted strings with
 * backslash escapes, bracketed properties, numbers, identifiers and the operators. Anything else
 * is an error rather than a skipped character, because a reading that silently drops part of an
 * expression proves nothing about the rest.
 */
function tokenize(expression: string): Token[] {
  const out: Token[] = [];
  let i = 0;
  while (i < expression.length) {
    const ch = expression[i]!;
    if (/\s/.test(ch)) {
      i += 1;
      continue;
    }
    if (ch === "'" || ch === '"') {
      let literal = '';
      i += 1;
      let closed = false;
      while (i < expression.length) {
        if (expression[i] === '\\' && i + 1 < expression.length) {
          literal += expression[i + 1];
          i += 2;
          continue;
        }
        if (expression[i] === ch) {
          i += 1;
          closed = true;
          break;
        }
        literal += expression[i];
        i += 1;
      }
      if (!closed) throw new Error(`unterminated string literal in ${JSON.stringify(expression)}`);
      out.push({ kind: 'str', value: literal });
      continue;
    }
    if (ch === '[') {
      const close = expression.indexOf(']', i);
      if (close < 0) throw new Error(`unterminated property in ${JSON.stringify(expression)}`);
      out.push({ kind: 'prop', value: expression.slice(i + 1, close) });
      i = close + 1;
      continue;
    }
    const number = /^\d+(?:\.\d+)?(?:[eE][+-]?\d+)?/.exec(expression.slice(i));
    if (number) {
      out.push({ kind: 'num', value: number[0] });
      i += number[0].length;
      continue;
    }
    const ident = /^[A-Za-z_][A-Za-z0-9_]*/.exec(expression.slice(i));
    if (ident) {
      out.push({ kind: 'ident', value: ident[0] });
      i += ident[0].length;
      continue;
    }
    const op = /^(?:==|!=|<>|<=|>=|&&|\|\||[=<>+\-*/%!(),?:])/.exec(expression.slice(i));
    if (op) {
      out.push({ kind: 'op', value: op[0] });
      i += op[0].length;
      continue;
    }
    throw new Error(`cannot read ${JSON.stringify(ch)} at ${i} in ${JSON.stringify(expression)}`);
  }
  return out;
}

/** NCalc's grammar, from the loosest binding to the tightest: ternary, or, and, comparison, sum, product, unary, primary. */
export function parseNcalc(expression: string): Node {
  const tokens = tokenize(expression);
  let pos = 0;
  const peek = (): Token | undefined => tokens[pos];
  const isOp = (...values: string[]): boolean => {
    const t = peek();
    return t !== undefined && t.kind === 'op' && values.includes(t.value);
  };
  const isWord = (...values: string[]): boolean => {
    const t = peek();
    return t !== undefined && t.kind === 'ident' && values.includes(t.value.toLowerCase());
  };
  const take = (): Token => {
    const t = tokens[pos];
    if (!t) throw new Error(`unexpected end of ${JSON.stringify(expression)}`);
    pos += 1;
    return t;
  };
  const expect = (value: string): void => {
    const t = take();
    if (t.kind !== 'op' || t.value !== value) throw new Error(`expected ${JSON.stringify(value)} but found ${JSON.stringify(t.value)} in ${JSON.stringify(expression)}`);
  };

  const ternary = (): Node => {
    const cond = or();
    if (!isOp('?')) return cond;
    take();
    const then = ternary();
    expect(':');
    return { kind: 'ternary', cond, then, else: ternary() };
  };
  const or = (): Node => {
    let left = and();
    while (isOp('||') || isWord('or')) left = { kind: 'binary', op: 'or', left, right: (take(), and()) };
    return left;
  };
  const and = (): Node => {
    let left = comparison();
    while (isOp('&&') || isWord('and')) left = { kind: 'binary', op: 'and', left, right: (take(), comparison()) };
    return left;
  };
  const comparison = (): Node => {
    let left = sum();
    while (isOp('=', '==', '!=', '<>', '<', '>', '<=', '>=')) left = { kind: 'binary', op: take().value, left, right: sum() };
    return left;
  };
  const sum = (): Node => {
    let left = product();
    while (isOp('+', '-')) left = { kind: 'binary', op: take().value, left, right: product() };
    return left;
  };
  const product = (): Node => {
    let left = unary();
    while (isOp('*', '/', '%')) left = { kind: 'binary', op: take().value, left, right: unary() };
    return left;
  };
  const unary = (): Node => {
    if (isOp('!', '-')) return { kind: 'unary', op: take().value, arg: unary() };
    if (isWord('not')) return { kind: 'unary', op: 'not', arg: (take(), unary()) };
    return primary();
  };
  const primary = (): Node => {
    const t = take();
    if (t.kind === 'str') return { kind: 'str', value: t.value };
    if (t.kind === 'num') return { kind: 'num', value: Number(t.value) };
    if (t.kind === 'prop') return { kind: 'prop', name: t.value };
    if (t.kind === 'ident') {
      if (!isOp('(')) return { kind: 'ident', name: t.value.toLowerCase() };
      take();
      const args: Node[] = [];
      if (!isOp(')')) {
        args.push(ternary());
        while (isOp(',')) {
          take();
          args.push(ternary());
        }
      }
      expect(')');
      return { kind: 'call', name: t.value.toLowerCase(), args };
    }
    if (t.kind === 'op' && t.value === '(') {
      const inner = ternary();
      expect(')');
      return inner;
    }
    throw new Error(`unexpected ${JSON.stringify(t.value)} in ${JSON.stringify(expression)}`);
  };

  const root = ternary();
  if (pos !== tokens.length) throw new Error(`trailing ${JSON.stringify(tokens[pos]!.value)} in ${JSON.stringify(expression)}`);
  return root;
}

/**
 * A string some output of the expression is at least as wide as. `exact` when it is that output
 * as written, with nothing unknowable left out of it.
 */
interface Floor {
  text: string;
  exact: boolean;
}

/** The floor of a value nothing can be said about: the empty string, which every output is at least as wide as. */
const UNKNOWN: readonly Floor[] = [{ text: '', exact: false }];

const union = (...sets: readonly (readonly Floor[])[]): Floor[] => {
  const seen = new Map<string, Floor>();
  for (const set of sets) for (const f of set) seen.set(`${f.exact ? 'e' : 'p'}:${f.text}`, f);
  return [...seen.values()];
};

/** Every way of writing one string after another: what `+` draws. */
const product = (left: readonly Floor[], right: readonly Floor[]): Floor[] =>
  union(left.flatMap((a) => right.map((b) => ({ text: a.text + b.text, exact: a.exact && b.exact }))));

const mapText = (floors: readonly Floor[], f: (text: string) => string): Floor[] => union(floors.map((x) => ({ ...x, text: f(x.text) })));

/** On the exact floors only, where a whole string is known; the rest become unknown. */
const onExact = (floors: readonly Floor[], f: (text: string) => string): Floor[] =>
  union(floors.map((x) => (x.exact ? { text: f(x.text), exact: true } : UNKNOWN[0]!)));

/**
 * .NET's `ToTitleCase`, which SimHub's `tcase` is: the first letter of each word up and the rest
 * down, unless the word is entirely upper case, which it leaves alone.
 */
const titleCase = (text: string): string => text.replace(/\S+/g, (word) => (word === word.toUpperCase() ? word : word[0]!.toUpperCase() + word.slice(1).toLowerCase()));

/** `StringExtensions.Left`: `maxLength` characters from `startIndex`, or nothing where the string does not reach. */
const leftOf = (text: string, start: number, count: number): string => (start >= text.length ? '' : text.slice(start, start + count));

/** The same from the other end. */
const rightOf = (text: string, start: number, count: number): string => {
  const end = text.length - start;
  return end <= 0 ? '' : text.slice(Math.max(0, end - count), end);
};

const literalNumber = (node: Node | undefined): number | undefined => (node?.kind === 'num' ? node.value : undefined);
const literalString = (node: Node | undefined): string | undefined => (node?.kind === 'str' ? node.value : undefined);

function floorsOf(node: Node): Floor[] {
  switch (node.kind) {
    case 'str':
      return [{ text: node.value, exact: true }];
    // A number is drawn as its digits, but `+` on two of them is a sum and not a string, so a
    // number's floor is nothing rather than a digit that might be added away.
    case 'prop': {
      const words = VOCABULARIES[node.name];
      return words === undefined ? [...UNKNOWN] : words.map((text) => ({ text, exact: true }));
    }
    case 'num':
    case 'ident':
    case 'unary':
      return [...UNKNOWN];
    case 'ternary':
      return union(floorsOf(node.then), floorsOf(node.else));
    case 'binary':
      // `+` is concatenation wherever a string is involved and addition where none is; with a
      // number's floor being nothing, the product is right in both cases.
      return node.op === '+' ? product(floorsOf(node.left), floorsOf(node.right)) : [...UNKNOWN];
    case 'call': {
      const { name, args } = node;
      const arg = (i: number): Floor[] => (args[i] ? floorsOf(args[i]!) : [...UNKNOWN]);
      switch (name) {
        case 'if':
          return union(arg(1), arg(2));
        case 'isnull':
          // Two arguments substitute; one tests and draws a boolean.
          return args.length === 2 ? union(arg(0), arg(1)) : [...UNKNOWN];
        case 'ucase':
          return mapText(arg(0), (t) => t.toUpperCase());
        case 'lcase':
          return mapText(arg(0), (t) => t.toLowerCase());
        case 'tcase':
          // Title-casing is per word, and a word a floor left half out of is not the word drawn.
          return onExact(arg(0), titleCase);
        case 'left':
        case 'right': {
          const start = literalNumber(args[1]);
          const count = literalNumber(args[2]);
          if (start === undefined || count === undefined) return [...UNKNOWN];
          return onExact(arg(0), (t) => (name === 'left' ? leftOf(t, start, count) : rightOf(t, start, count)));
        }
        case 'padleft':
          // Padding only adds, so what was certain before it is certain after it.
          return arg(0);
        case 'replace': {
          const from = literalString(args[1]);
          const to = literalString(args[2]);
          if (from === undefined || to === undefined || from === '') return [...UNKNOWN];
          return onExact(arg(0), (t) => t.split(from).join(to));
        }
        default:
          // `format`, `toshorttime`, an opponent lookup: whatever the sim gives, as the function
          // rewrites it, and nothing written in the expression reaches the screen as written.
          return [...UNKNOWN];
      }
    }
  }
}

/**
 * The strings an expression's output is certain to be at least as wide as, the empty one left out.
 *
 * Some are the whole output -- `ucase(if(.., 'psi', 'kPa'))` draws exactly `PSI` or `KPA` -- and
 * some are a part of it: `'#' + (drivercarnumber(1))` is at least a `#` wide, and how much wider is
 * the sim's to say. Both kinds are what a `widest` has to hold.
 */
export function drawnFloors(expression: string): string[] {
  return floorsOf(parseNcalc(expression))
    .map((f) => f.text)
    .filter((t) => t !== '');
}

/**
 * What an item is certain to draw, whether or not it is bound: the floors of its binding where
 * it has one, and its own text where it has none, since then the text is what is drawn.
 */
export function certainlyDrawn(item: TextItem): string[] {
  const expression = bindingExpression(item, 'Text');
  return expression === '' ? [item.text] : drawnFloors(expression);
}

/**
 * What an item draws in full where that is known: its exact floors where it is bound, each an output
 * as written, and its own text where it is not. A binding that writes anything the sim chooses has
 * none, and the strings returned are drawn as they are, character for character.
 */
export function exactlyDrawn(item: TextItem): string[] {
  const expression = bindingExpression(item, 'Text');
  if (expression === '') return [item.text];
  return floorsOf(parseNcalc(expression))
    .filter((f) => f.exact && f.text !== '')
    .map((f) => f.text);
}

/**
 * Width of a text the way the fit tests measure the item: its cells when it is monospaced, its
 * advances in the face it is drawn in otherwise. The same rule as `drawnWidth` in the fit tests,
 * so that a floor and a `widest` are compared on the scale the box is cut on.
 */
export function widthAsDrawn(item: TextItem, text: string): number {
  const mono = item.monospace;
  if (!mono) return measureText(faceOf(item), text, item.fontSize);
  const specials = [...text].filter((c) => mono.specialChars?.includes(c) ?? false).length;
  return (text.length - specials) * mono.charWidth + specials * mono.specialCharsWidth;
}
