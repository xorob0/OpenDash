/**
 * A parser for the NCalc that `packages/generator/src/ncalc.ts` writes, and for nothing more.
 *
 * The builder emits strings (`export type Expr = string`) with every operand in brackets, so the
 * language it reaches is small: `[Property]` references, string, number, boolean and null literals,
 * `and`, `or` and `!`, the six comparisons, `+ - * / %`, unary minus, brackets and function calls.
 * NCalc itself is larger (`&&`, `||`, `not`, `==`, `<>`, the bitwise operators, `?:`, bare
 * parameters, `#date#` literals, string escapes such as `\n`), and every one of those is refused
 * here with the offset it starts at. An evaluator that quietly accepted more than the builder writes
 * would be checking a language nobody uses.
 *
 * Precedence is NCalc's, loosest first: `or`; `and`; `=` `!=`; `<` `<=` `>` `>=`; `+` `-`; `*` `/`
 * `%`; then unary `!` and `-`, which in NCalc's grammar apply to a primary only, so `!!x` and `--1`
 * are refused as NCalc refuses them. Binary operators associate to the left. Keywords and function
 * names are matched without regard to case, as NCalc matches them; a function name is kept as
 * written and compared lower-cased.
 */
import { NCalcSyntaxError, type Span } from './errors.ts';
import { double, int, type Value } from './values.ts';

export type BinaryOperator = 'or' | 'and' | '=' | '!=' | '<' | '<=' | '>' | '>=' | '+' | '-' | '*' | '/' | '%';
export type UnaryOperator = '!' | '-';

export type Node =
  | { readonly type: 'literal'; readonly value: Value; readonly span: Span }
  | { readonly type: 'property'; readonly name: string; readonly span: Span }
  | { readonly type: 'call'; readonly name: string; readonly args: readonly Node[]; readonly span: Span; readonly nameSpan: Span }
  | { readonly type: 'unary'; readonly op: UnaryOperator; readonly operand: Node; readonly span: Span }
  | { readonly type: 'binary'; readonly op: BinaryOperator; readonly left: Node; readonly right: Node; readonly span: Span };

/** A parsed expression with its source, which every error message quotes. */
export interface Parsed {
  readonly source: string;
  readonly root: Node;
}

type Token =
  | { kind: 'number'; text: string; span: Span }
  | { kind: 'string'; value: string; span: Span }
  | { kind: 'property'; name: string; span: Span }
  | { kind: 'word'; text: string; span: Span }
  | { kind: 'op'; text: string; span: Span }
  | { kind: 'end'; span: Span };

const OPERATORS = ['<=', '>=', '!=', '=', '<', '>', '+', '-', '*', '/', '%', '!', '(', ')', ','] as const;

/** Spellings NCalc accepts that the builder never writes, each with what to write instead. */
const REFUSED_OPERATORS: ReadonlyMap<string, string> = new Map([
  ['&&', '`and`'],
  ['||', '`or`'],
  ['==', '`=`'],
  ['<>', '`!=`'],
  ['<<', 'nothing: the builder has no shift'],
  ['>>', 'nothing: the builder has no shift'],
  ['&', 'nothing: the builder has no bitwise operator'],
  ['|', 'nothing: the builder has no bitwise operator'],
  ['^', 'nothing: the builder has no bitwise operator'],
  ['~', 'nothing: the builder has no bitwise operator'],
  ['?', '`if(c, a, b)`'],
  [':', '`if(c, a, b)`'],
  ['#', 'a value the expression computes: the builder writes no date literal'],
  ['"', 'a single-quoted string'],
]);

function tokenize(source: string): Token[] {
  const tokens: Token[] = [];
  const fail = (reason: string, at: number): never => {
    throw new NCalcSyntaxError(reason, source, at);
  };
  let i = 0;
  while (i < source.length) {
    const c = source[i]!;
    if (/\s/.test(c)) {
      i += 1;
      continue;
    }
    const start = i;
    if (c === "'") {
      let value = '';
      i += 1;
      for (;;) {
        if (i >= source.length) fail('a string literal is not closed', start);
        const ch = source[i]!;
        if (ch === "'") {
          i += 1;
          break;
        }
        if (ch === '\\') {
          const next = source[i + 1];
          if (next === '\\' || next === "'") {
            value += next;
            i += 2;
            continue;
          }
          fail(`the escape \\${next ?? ''} is not one ncalc.ts writes; str() escapes only a backslash and a quote`, i);
        }
        value += ch;
        i += 1;
      }
      tokens.push({ kind: 'string', value, span: { start, end: i } });
      continue;
    }
    if (c === '[') {
      const close = source.indexOf(']', i);
      if (close < 0) fail('a property reference is not closed', start);
      const name = source.slice(i + 1, close);
      if (name.trim() === '') fail('a property reference names nothing', start);
      i = close + 1;
      tokens.push({ kind: 'property', name, span: { start, end: i } });
      continue;
    }
    if (/[0-9]/.test(c) || (c === '.' && /[0-9]/.test(source[i + 1] ?? ''))) {
      const m = /^(\d*\.\d+|\d+)([eE][+-]?\d+)?/.exec(source.slice(i))!;
      i += m[0].length;
      if (/[A-Za-z_.]/.test(source[i] ?? '')) fail(`${JSON.stringify(source.slice(start, i + 1))} is not a number`, start);
      tokens.push({ kind: 'number', text: m[0], span: { start, end: i } });
      continue;
    }
    if (/[A-Za-z_]/.test(c)) {
      const m = /^[A-Za-z_][A-Za-z0-9_]*/.exec(source.slice(i))!;
      i += m[0].length;
      tokens.push({ kind: 'word', text: m[0], span: { start, end: i } });
      continue;
    }
    const two = source.slice(i, i + 2);
    const refused = REFUSED_OPERATORS.get(two) ?? REFUSED_OPERATORS.get(c);
    if (refused !== undefined && !OPERATORS.includes(two as (typeof OPERATORS)[number])) {
      const spelled = REFUSED_OPERATORS.has(two) ? two : c;
      fail(`${JSON.stringify(spelled)} is NCalc but not the subset ncalc.ts writes; write ${refused}`, start);
    }
    const op = OPERATORS.find((o) => source.startsWith(o, i));
    if (op === undefined) fail(`${JSON.stringify(c)} is not part of an NCalc expression`, start);
    i += op!.length;
    tokens.push({ kind: 'op', text: op!, span: { start, end: i } });
  }
  tokens.push({ kind: 'end', span: { start: source.length, end: source.length } });
  return tokens;
}

const BINARY_LEVELS: readonly (readonly BinaryOperator[])[] = [['or'], ['and'], ['=', '!='], ['<', '<=', '>', '>='], ['+', '-'], ['*', '/', '%']];

/** Words NCalc reads as something other than a function or a parameter. */
const KEYWORDS = new Set(['and', 'or', 'not', 'true', 'false', 'null', 'in']);

class Parser {
  private at = 0;
  constructor(
    private readonly source: string,
    private readonly tokens: readonly Token[],
  ) {}

  private fail(reason: string, offset: number): never {
    throw new NCalcSyntaxError(reason, this.source, offset);
  }

  private peek(): Token {
    return this.tokens[this.at]!;
  }

  private next(): Token {
    return this.tokens[this.at++]!;
  }

  private isOp(text: string): boolean {
    const t = this.peek();
    return t.kind === 'op' && t.text === text;
  }

  private expectOp(text: string, what: string): Token {
    const t = this.peek();
    if (t.kind !== 'op' || t.text !== text) this.fail(`expected ${JSON.stringify(text)} ${what}, found ${describe(t)}`, t.span.start);
    return this.next();
  }

  /** The binary operator at the cursor, if it is one of `ops`. `and` and `or` are words. */
  private binaryAt(ops: readonly BinaryOperator[]): BinaryOperator | undefined {
    const t = this.peek();
    if (t.kind === 'op' && (ops as readonly string[]).includes(t.text)) return t.text as BinaryOperator;
    if (t.kind === 'word') {
      const lower = t.text.toLowerCase();
      if ((ops as readonly string[]).includes(lower)) return lower as BinaryOperator;
    }
    return undefined;
  }

  parseExpression(level = 0): Node {
    if (level === BINARY_LEVELS.length) return this.parseUnary();
    const ops = BINARY_LEVELS[level]!;
    let left = this.parseExpression(level + 1);
    for (let op = this.binaryAt(ops); op !== undefined; op = this.binaryAt(ops)) {
      this.next();
      const right = this.parseExpression(level + 1);
      left = { type: 'binary', op, left, right, span: { start: left.span.start, end: right.span.end } };
    }
    return left;
  }

  private parseUnary(): Node {
    const t = this.peek();
    if (t.kind === 'word' && t.text.toLowerCase() === 'not') this.fail('`not` is NCalc but not the subset ncalc.ts writes; write `!(...)`', t.span.start);
    if (t.kind === 'op' && (t.text === '!' || t.text === '-')) {
      this.next();
      const after = this.peek();
      if (after.kind === 'op' && (after.text === '!' || after.text === '-')) {
        this.fail(`NCalc applies ${JSON.stringify(t.text)} to a value, a call or a bracket, not to another ${JSON.stringify(after.text)}`, after.span.start);
      }
      const operand = this.parsePrimary();
      return { type: 'unary', op: t.text as UnaryOperator, operand, span: { start: t.span.start, end: operand.span.end } };
    }
    return this.parsePrimary();
  }

  private parsePrimary(): Node {
    const t = this.next();
    switch (t.kind) {
      case 'number': {
        const isInteger = /^\d+$/.test(t.text);
        const n = Number(t.text);
        // NCalc reads an integer literal as Int32, or Int64 when it does not fit; both are `int` here.
        return { type: 'literal', value: isInteger ? int(n) : double(n), span: t.span };
      }
      case 'string':
        return { type: 'literal', value: t.value, span: t.span };
      case 'property':
        return { type: 'property', name: t.name, span: t.span };
      case 'word': {
        const lower = t.text.toLowerCase();
        if (lower === 'true' || lower === 'false') return { type: 'literal', value: lower === 'true', span: t.span };
        if (lower === 'null') return { type: 'literal', value: null, span: t.span };
        if (!this.isOp('(')) {
          if (KEYWORDS.has(lower)) this.fail(`${JSON.stringify(t.text)} is an operator here, and an operand was expected`, t.span.start);
          this.fail(`${JSON.stringify(t.text)} is a bare name; NCalc would read it as a parameter, which ncalc.ts never writes: a property is [Name]`, t.span.start);
        }
        if (lower === 'and' || lower === 'or' || lower === 'not') this.fail(`${JSON.stringify(t.text)} is an operator, not a function`, t.span.start);
        this.next();
        const args: Node[] = [];
        if (!this.isOp(')')) {
          for (;;) {
            args.push(this.parseExpression());
            if (this.isOp(',')) {
              this.next();
              continue;
            }
            break;
          }
        }
        const close = this.expectOp(')', `to close the call to ${t.text}`);
        return { type: 'call', name: t.text, args, span: { start: t.span.start, end: close.span.end }, nameSpan: t.span };
      }
      case 'op':
        if (t.text === '(') {
          const inner = this.parseExpression();
          this.expectOp(')', 'to close the bracket');
          return inner;
        }
        return this.fail(`expected a value, found ${describe(t)}`, t.span.start);
      case 'end':
        return this.fail('the expression ends where a value was expected', t.span.start);
    }
  }

  parseAll(): Node {
    if (this.peek().kind === 'end') this.fail('the expression is empty', 0);
    const root = this.parseExpression();
    const t = this.peek();
    if (t.kind !== 'end') this.fail(`expected the end of the expression, found ${describe(t)}`, t.span.start);
    return root;
  }
}

const describe = (t: Token): string => {
  switch (t.kind) {
    case 'number':
      return `the number ${t.text}`;
    case 'string':
      return `the string ${JSON.stringify(t.value)}`;
    case 'property':
      return `[${t.name}]`;
    case 'word':
      return JSON.stringify(t.text);
    case 'op':
      return JSON.stringify(t.text);
    case 'end':
      return 'the end of the expression';
  }
};

/** Parses one expression. Throws {@link NCalcSyntaxError} naming the expression and the offset. */
export function parse(source: string): Parsed {
  return { source, root: new Parser(source, tokenize(source)).parseAll() };
}

const cache = new Map<string, Parsed>();

/** {@link parse}, remembered by text: a dashboard evaluates the same few hundred expressions every frame. */
export function parseCached(source: string): Parsed {
  let parsed = cache.get(source);
  if (!parsed) {
    parsed = parse(source);
    cache.set(source, parsed);
  }
  return parsed;
}

const quote = (s: string): string => `'${s.replace(/\\/g, '\\\\').replace(/'/g, "\\'")}'`;

const printLiteral = (v: Value): string => {
  if (v === null) return 'null';
  if (typeof v === 'boolean') return v ? 'true' : 'false';
  if (typeof v === 'string') return quote(v);
  if (typeof v === 'object' && (v.kind === 'int' || v.kind === 'double')) {
    const text = String(v.value);
    // A double literal keeps its point, so it reads back as a double.
    return v.kind === 'double' && /^\d+$/.test(text) ? `${text}.0` : text;
  }
  throw new Error(`a ${typeof v === 'object' ? v.kind : typeof v} is not a literal`);
};

/**
 * The expression written back the way `ncalc.ts` writes one: every operand of an operator in
 * brackets, a chain of one operator flat, calls as `name(a, b)`. Parsing the result gives the same tree, which is what the round
 * trip tests hold it to.
 */
export function print(node: Node): string {
  switch (node.type) {
    case 'literal':
      return printLiteral(node.value);
    case 'property':
      return `[${node.name}]`;
    case 'call':
      return `${node.name}(${node.args.map(print).join(', ')})`;
    case 'unary':
      return `${node.op}(${print(node.operand)})`;
    case 'binary': {
      // A left-leaning chain of one operator is written flat, as `and(a, b, c)` writes it: the
      // operators associate to the left, so `(a) + (b) + (c)` reads back as the same tree.
      const left = node.left.type === 'binary' && node.left.op === node.op ? print(node.left) : `(${print(node.left)})`;
      return `${left} ${node.op} (${print(node.right)})`;
    }
  }
}

/** Every node of a tree, the root first, depth first. */
export function* nodesOf(node: Node): Generator<Node> {
  yield node;
  if (node.type === 'call') for (const a of node.args) yield* nodesOf(a);
  else if (node.type === 'unary') yield* nodesOf(node.operand);
  else if (node.type === 'binary') {
    yield* nodesOf(node.left);
    yield* nodesOf(node.right);
  }
}
