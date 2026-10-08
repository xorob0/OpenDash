/**
 * The parser for the subset of NCalc that `ncalc.ts` writes: that what the builders write comes back
 * as written, that precedence is NCalc's, and that everything outside the subset is refused with the
 * offset it starts at.
 */
import { describe, expect, test } from 'bun:test';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import * as ncalc from '../src/ncalc.ts';
import { NCalcSyntaxError, parse, print, type Node } from '../src/ncalc/index.ts';

/** A tree without its spans, so that two parses can be compared by what they mean. */
const shape = (node: Node): unknown => {
  switch (node.type) {
    case 'literal':
      return node.value;
    case 'property':
      return `[${node.name}]`;
    case 'call':
      return { [node.name.toLowerCase()]: node.args.map(shape) };
    case 'unary':
      return { [node.op]: shape(node.operand) };
    case 'binary':
      return [shape(node.left), node.op, shape(node.right)];
  }
};

const tree = (source: string): unknown => shape(parse(source).root);

const refusal = (source: string): NCalcSyntaxError => {
  try {
    parse(source);
  } catch (e) {
    if (e instanceof NCalcSyntaxError) return e;
    throw e;
  }
  throw new Error(`${source} parsed`);
};

describe('what the builders write comes back as written', () => {
  const written = [
    ncalc.and(ncalc.eq(ncalc.game('Gear'), ncalc.str('N')), ncalc.not(ncalc.game('IsInPit'))),
    ncalc.or(ncalc.gt(ncalc.game('Rpms'), ncalc.num(7000)), ncalc.le(ncalc.game('SpeedKmh'), ncalc.num(0.5))),
    ncalc.iff(ncalc.isNull(ncalc.raw('dcABS')), ncalc.str('--'), ncalc.fmt(ncalc.raw('dcABS'), '0')),
    ncalc.signed(ncalc.prop('PersistantTrackerPlugin.SessionBestLiveDeltaSeconds'), '0.00'),
    ncalc.toShortTime(ncalc.game('LastLapTime'), 3),
    ncalc.hms(ncalc.timespanToSeconds(ncalc.game('SessionTimeLeft'))),
    ncalc.isIn(ncalc.fmt(ncalc.computed('CurrentDateTime'), 'HH'), ncalc.str('12'), ncalc.str('13')),
    ncalc.driver('name', ncalc.add(ncalc.repeatIndex(), '1')),
    ncalc.driverSector('lastlap', ncalc.playerPosition(), 2),
    ncalc.left(ncalc.driver('carclass', '3'), 4),
    ncalc.propByName(ncalc.concat(ncalc.str('PersistantTrackerPlugin.PreviousLap_'), ncalc.fmt(ncalc.sub(ncalc.repeatIndex(), '1'), '00'))),
    ncalc.changed('3000', ncalc.game('Position')),
    ncalc.mod(ncalc.truncate(ncalc.div(ncalc.mul(ncalc.game('Fuel'), '1.0'), '3')), '2'),
    ncalc.str("it's a \\ backslash"),
  ];

  for (const source of written) {
    test(source.length > 90 ? `${source.slice(0, 90)}...` : source, () => {
      expect(print(parse(source).root)).toBe(source);
    });
  }

  test('a printed tree parses back to the same tree, keywords and names in any case', () => {
    const source = "IF(([A] > 1) AND (NOT_A_KEYWORD([B])), 'x', Null) + -(2.0)";
    const printed = print(parse(source).root);
    expect(tree(printed)).toEqual(tree(source));
  });
});

describe('precedence and association are NCalc\'s', () => {
  test('multiplication binds tighter than addition, which binds tighter than comparison', () => {
    expect(tree('1 + 2 * 3 > 4')).toEqual([[{ kind: 'int', value: 1 }, '+', [{ kind: 'int', value: 2 }, '*', { kind: 'int', value: 3 }]], '>', { kind: 'int', value: 4 }]);
  });

  test('and binds tighter than or, and equality looser than the relations', () => {
    expect(tree('[A] or [B] and [C]')).toEqual(['[A]', 'or', ['[B]', 'and', '[C]']]);
    expect(tree('[A] < [B] = [C] < [D]')).toEqual([['[A]', '<', '[B]'], '=', ['[C]', '<', '[D]']]);
  });

  test('a chain of one operator associates to the left', () => {
    expect(tree('[A] - [B] - [C]')).toEqual([['[A]', '-', '[B]'], '-', '[C]']);
    expect(tree('[A] / [B] % [C]')).toEqual([['[A]', '/', '[B]'], '%', '[C]']);
  });

  test('! and unary minus take the primary after them and nothing more', () => {
    expect(tree('!([A]) and ([B])')).toEqual([{ '!': '[A]' }, 'and', '[B]']);
    expect(tree('-1 + 2')).toEqual([{ '-': { kind: 'int', value: 1 } }, '+', { kind: 'int', value: 2 }]);
  });

  test('keywords and function names are read without regard to case', () => {
    expect(tree('[A] AND [B] Or TRUE')).toEqual(tree('[A] and [B] or true'));
    expect(tree('IsNull([A], FALSE)')).toEqual({ isnull: ['[A]', false] });
  });

  test('an integer literal is an int and a literal with a point a double', () => {
    expect(tree('7')).toEqual({ kind: 'int', value: 7 });
    expect(tree('1.0')).toEqual({ kind: 'double', value: 1 });
    expect(tree('.5')).toEqual({ kind: 'double', value: 0.5 });
  });

  test('a call with no arguments has none, and a property name keeps every character but the bracket', () => {
    expect(tree('getplayerleaderboardposition()')).toEqual({ getplayerleaderboardposition: [] });
    expect(tree('[DataCorePlugin.GameRawData.Telemetry.SessionFlagsDetails.IsyellowWaving]')).toBe('[DataCorePlugin.GameRawData.Telemetry.SessionFlagsDetails.IsyellowWaving]');
  });

  test('every node knows the span of source it came from', () => {
    const source = "format([A], '0.0')";
    const root = parse(source).root;
    expect(root.span).toEqual({ start: 0, end: source.length });
    if (root.type !== 'call') throw new Error('not a call');
    expect(source.slice(root.args[1]!.span.start, root.args[1]!.span.end)).toBe("'0.0'");
  });
});

describe('everything outside the subset is refused, at the offset it starts', () => {
  const cases: [string, number, RegExp][] = [
    ['[A] && [B]', 4, /write `and`/],
    ['[A] || [B]', 4, /write `or`/],
    ['[A] == 1', 4, /write `=`/],
    ['[A] <> 1', 4, /write `!=`/],
    ['not [A]', 0, /write `!\(\.\.\.\)`/],
    ['[A] ? 1 : 2', 4, /if\(c, a, b\)/],
    ['[A] & 1', 4, /bitwise/],
    ['#2026-01-01#', 0, /date literal/],
    ['"text"', 0, /single-quoted/],
    ['Gear = 1', 0, /bare name/],
    ['!!([A])', 1, /not to another/],
    ["'a\\nb'", 2, /escape/],
    ["'open", 0, /not closed/],
    ['[Open', 0, /not closed/],
    ['([A]', 4, /close the bracket/],
    ['max([A], 1', 10, /close the call/],
    ['[A] [B]', 4, /end of the expression/],
    ['', 0, /empty/],
    ['1 +', 3, /ends where a value was expected/],
    ['1abc', 0, /not a number/],
  ];
  for (const [source, offset, reason] of cases) {
    test(JSON.stringify(source), () => {
      const e = refusal(source);
      expect(e.offset).toBe(offset);
      expect(e.reason).toMatch(reason);
      expect(e.message).toContain(JSON.stringify(source));
    });
  }
});

describe('the evaluator is pure TypeScript a browser can bundle', () => {
  test('nothing under ncalc/, nor the tables it reads, imports from node', () => {
    const dir = path.join(import.meta.dir, '..', 'src', 'ncalc');
    const files = [...readdirSync(dir).map((f) => path.join(dir, f)), path.join(dir, '..', 'ncalcFunctions.ts'), path.join(dir, '..', 'opponentCalls.ts')];
    for (const file of files) {
      const source = readFileSync(file, 'utf8');
      expect({ file: path.basename(file), node: /from ['"](node:|fs['"]|path['"])|import\.meta\.dir/.test(source) }).toEqual({ file: path.basename(file), node: false });
    }
  });
});
