/**
 * The check that would have caught `left([Class], 4)`.
 *
 * A name alone is not the test. `left` is a real SimHub function; called with two arguments it
 * matches no dispatch branch, gets no delegate, and the whole expression evaluates to nothing —
 * which SimHub draws as an empty string and reports nowhere. Arity is the half that catches it.
 */
import { describe, expect, test } from 'bun:test';
import { arityAccepts, arityText, functionCalls, NCALC_FUNCTIONS, unknownFunctions } from '../src/ncalcFunctions.ts';
import * as ncalc from '../src/ncalc.ts';

describe('finding the calls in an expression', () => {
  test('a bare call', () => {
    expect(functionCalls('ucase([Class])')).toMatchObject([{ name: 'ucase', argumentCount: 1 }]);
  });

  test('no arguments is zero, not one', () => {
    expect(functionCalls('getplayerleaderboardposition()')).toMatchObject([{ name: 'getplayerleaderboardposition', argumentCount: 0 }]);
  });

  test('nested calls are each counted at their own depth', () => {
    const calls = functionCalls("if(isnull([A]), format([B], '0.0'), 'x')");
    expect(calls.map((c) => [c.name, c.argumentCount])).toEqual([
      ['if', 3],
      ['isnull', 1],
      ['format', 2],
    ]);
  });

  test('a comma inside a string literal is not an argument separator', () => {
    expect(functionCalls("replace([A], 'a,b', 'c')")).toMatchObject([{ name: 'replace', argumentCount: 3 }]);
  });

  test('a bracket inside a string literal does not open a call', () => {
    expect(functionCalls("format([A], '0);0')")).toMatchObject([{ name: 'format', argumentCount: 2 }]);
  });

  test('an escaped quote does not end the literal early', () => {
    expect(functionCalls("replace([A], '\\'', ',')")).toMatchObject([{ name: 'replace', argumentCount: 3 }]);
  });

  test('a property reference is not a call, even one that looks like a name', () => {
    expect(functionCalls('[DataCorePlugin.GameData.Rpms] + 1')).toEqual([]);
  });

  test('the word operators are operators, not calls', () => {
    // `(a) and (b)` is exactly a name followed by a bracket, which is why this is a test.
    expect(functionCalls('([A] = 1) and ([B] = 2)')).toEqual([]);
    expect(functionCalls('([A] = 1) or ([B] = 2)')).toEqual([]);
    expect(functionCalls('!([A])')).toEqual([]);
  });
});

describe('what SimHub will and will not dispatch', () => {
  test('the call that shipped blank is rejected', () => {
    const bad = unknownFunctions("ucase(left(isnull([Class], ''), 4))");
    expect(bad).toHaveLength(1);
    expect(bad[0]).toMatchObject({ reason: 'arity' });
    expect(bad[0]?.message).toContain('left() takes 3 arguments, given 2');
  });

  test('and the corrected one is accepted', () => {
    expect(unknownFunctions("ucase(left(isnull([Class], ''), 0, 4))")).toEqual([]);
  });

  test('a name SimHub has never had is rejected', () => {
    const bad = unknownFunctions('substring([A], 0, 4)');
    expect(bad).toMatchObject([{ reason: 'unknown' }]);
  });

  test('isnull takes one argument or two, and both are accepted', () => {
    expect(unknownFunctions('isnull([A])')).toEqual([]);
    expect(unknownFunctions("isnull([A], '')")).toEqual([]);
    expect(unknownFunctions("isnull([A], '', 'x')")).toMatchObject([{ reason: 'arity' }]);
  });

  test('toshorttime accepts its two optional trailing arguments', () => {
    for (const call of ['toshorttime([A], 1)', 'toshorttime([A], 1, 0)', 'toshorttime([A], 1, 0, 1)']) {
      expect({ call, bad: unknownFunctions(call) }).toMatchObject({ bad: [] });
    }
    expect(unknownFunctions('toshorttime([A])')).toMatchObject([{ reason: 'arity' }]);
  });

  test('a driver function takes exactly the leaderboard position', () => {
    expect(unknownFunctions('drivercarnumber(repeatindex())')).toEqual([]);
    expect(unknownFunctions('drivercarnumber(1, 2)')).toMatchObject([{ reason: 'arity' }]);
  });

  test('a driver sector function takes three', () => {
    expect(unknownFunctions('driversectorlastlap(1, 2, false)')).toEqual([]);
    expect(unknownFunctions('driversectorlastlap(1, 2)')).toMatchObject([{ reason: 'arity' }]);
  });

  test('the leaderboard-best helpers need their dummy argument', () => {
    // SimHub declares them with no parameter and its delegate takes one anyway.
    expect(unknownFunctions('getbestlapopponentleaderboardposition(0)')).toEqual([]);
    expect(unknownFunctions('getbestlapopponentleaderboardposition()')).toMatchObject([{ reason: 'arity' }]);
  });

  test('NCalc\'s own maths is dispatched even though SimHub does not register it', () => {
    for (const call of ['abs([A])', 'round([A], 2)', 'truncate([A])', 'max([A], [B])', 'if([A], 1, 2)']) {
      expect({ call, bad: unknownFunctions(call) }).toMatchObject({ bad: [] });
    }
  });

  test('case does not matter, because NCalc lower-cases before it dispatches', () => {
    expect(unknownFunctions('UCase([A])')).toEqual([]);
  });
});

describe('every helper in ncalc.ts produces a call SimHub dispatches', () => {
  // The helpers are the only way an expression is built, so proving each one is well formed
  // proves the whole surface. A helper added without a matching row in the function table fails
  // here rather than in a silent blank on somebody's dash.
  const A = '[A]';
  const B = '[B]';
  const built: Record<string, string> = {
    isnull: ncalc.isnull(A, ncalc.str('')),
    isNull: ncalc.isNull(A),
    fmt: ncalc.fmt(A, '0.0'),
    fmtSigned: ncalc.fmt(A, '0.0', true),
    toShortTime: ncalc.toShortTime(A, 1),
    timespanToSeconds: ncalc.timespanToSeconds(A),
    secondsToTimespan: ncalc.secondsToTimespan(A),
    round: ncalc.round(A, 2),
    truncate: ncalc.truncate(A),
    abs: ncalc.abs(A),
    max: ncalc.max(A, B),
    min: ncalc.min(A, B),
    replace: ncalc.replace(A, 'a', 'b'),
    ucase: ncalc.ucase(A),
    lcase: ncalc.lcase(A),
    left: ncalc.left(A, 4),
    right: ncalc.right(A, 4),
    blink: ncalc.blink('x', 500, A),
    hms: ncalc.hms(A),
    digitCount: ncalc.digitCount(A, 3),
    iff: ncalc.iff(A, B, A),
    driver: ncalc.driver('carnumber', '1'),
    driverSector: ncalc.driverSector('lastlap', '1', 2),
    playerPosition: ncalc.playerPosition(),
    aheadBehind: ncalc.aheadBehind('1'),
    aheadBehindInClass: ncalc.aheadBehindInClass('1'),
    classPosition: ncalc.classPosition('1'),
    bestLapPosition: ncalc.bestLapPosition(),
    bestLapPositionInClass: ncalc.bestLapPositionInClass(),
    bestSplitTime: ncalc.bestSplitTime(1),
    repeatIndex: ncalc.repeatIndex(),
    repeatIndexDepth: ncalc.repeatIndex(1),
    propByName: ncalc.propByName(ncalc.str('X.Y')),
    and: ncalc.and(A, B),
    or: ncalc.or(A, B),
    not: ncalc.not(A),
    concat: ncalc.concat(A, B),
  };

  for (const [name, expression] of Object.entries(built)) {
    test(name, () => {
      expect({ name, expression, bad: unknownFunctions(expression) }).toMatchObject({ bad: [] });
    });
  }
});

describe('the table itself', () => {
  test('every name is lower case, because that is what NCalc matches on', () => {
    for (const name of NCALC_FUNCTIONS.keys()) expect({ name, lower: name === name.toLowerCase() }).toMatchObject({ lower: true });
  });

  test('an arity reads as a sentence', () => {
    expect(arityText({ exactly: 3 })).toBe('3');
    expect(arityText({ oneOf: [1, 2] })).toBe('1 or 2');
    expect(arityText({ atLeast: 2 })).toBe('2 or more');
  });

  test('and accepts what it says it does', () => {
    expect(arityAccepts({ exactly: 3 }, 3)).toBe(true);
    expect(arityAccepts({ exactly: 3 }, 2)).toBe(false);
    expect(arityAccepts({ oneOf: [1, 2] }, 2)).toBe(true);
    expect(arityAccepts({ oneOf: [1, 2] }, 3)).toBe(false);
    expect(arityAccepts({ atLeast: 2 }, 9)).toBe(true);
    expect(arityAccepts({ atLeast: 2 }, 1)).toBe(false);
  });

  test('left is three arguments, which is the whole point of this file', () => {
    expect(NCALC_FUNCTIONS.get('left')?.arity).toEqual({ exactly: 3 });
  });
});
