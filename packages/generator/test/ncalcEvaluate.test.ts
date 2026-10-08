/**
 * The evaluator, function by function, against what SimHub's NCalc does.
 *
 * A test marked **Unverified** pins the reading chosen where the behaviour could not be measured
 * without Windows; each is listed in the pull request that brought this in (#395) so trace version
 * 2 can carry a probe column for it. The rest follow docs/research/simhub-dash-format.md or the
 * decompiled engine.
 */
import { describe, expect, test } from 'bun:test';
import { arityAccepts, NCALC_FUNCTIONS } from '../src/ncalcFunctions.ts';
import {
  CallState,
  date,
  decimal,
  double,
  evaluate,
  evaluateBinding,
  formatNumber,
  int,
  NCalcRuntimeError,
  NCalcUnsupportedError,
  numberToString,
  parseDateTime,
  parseTimeSpan,
  single,
  SUPPORTED_FUNCTIONS,
  TICKS_PER_SECOND,
  timespan,
  toJs,
  type Scope,
  type Value,
} from '../src/ncalc/index.ts';

const scope = (properties: Record<string, unknown> = {}, extra: Partial<Scope> = {}): Scope => ({ properties, ...extra });
const ev = (source: string, properties: Record<string, unknown> = {}, extra: Partial<Scope> = {}): Value => evaluate(source, scope(properties, extra));
const js = (source: string, properties: Record<string, unknown> = {}, extra: Partial<Scope> = {}): unknown => toJs(ev(source, properties, extra));
const failsAtRuntime = (source: string, properties: Record<string, unknown> = {}): NCalcRuntimeError => {
  try {
    ev(source, properties);
  } catch (e) {
    if (e instanceof NCalcRuntimeError) return e;
    throw e;
  }
  throw new Error(`${source} did not fail`);
};
const unsupported = (source: string, properties: Record<string, unknown> = {}, extra: Partial<Scope> = {}): NCalcUnsupportedError => {
  try {
    ev(source, properties, extra);
  } catch (e) {
    if (e instanceof NCalcUnsupportedError) return e;
    throw e;
  }
  throw new Error(`${source} was computed`);
};
const seconds = (s: number) => timespan(Math.round(s * TICKS_PER_SECOND));

describe('numbers keep their CLR type', () => {
  test('an int over an int is integer division, and a double anywhere makes it a double', () => {
    expect(ev('7 / 2')).toEqual(int(3));
    expect(ev('-7 / 2')).toEqual(int(-3));
    expect(ev('7.0 / 2')).toEqual(double(3.5));
    expect(ev('[A] / 2', { A: 7.5 })).toEqual(double(3.75));
    expect(ev('7 % 3')).toEqual(int(1));
    expect(ev('[A] % 60', { A: 125.5 })).toEqual(double(5.5));
  });

  test('a whole number from the property map is an int, and a typed value keeps its type', () => {
    expect(ev('[A] / 2', { A: 7 })).toEqual(int(3));
    expect(ev('[A] / 2', { A: double(7) })).toEqual(double(3.5));
  });

  test('an int divided by zero throws; a double divided by zero is infinite', () => {
    expect(failsAtRuntime('1 / 0').reason).toMatch(/divide by zero/);
    expect(js('1.0 / 0')).toBe(Number.POSITIVE_INFINITY);
  });

  test('a Single times an int stays a Single, and times a double becomes one (simhub-dash-format.md)', () => {
    expect((ev('[S] * 1', { S: single(0.21) }) as { kind: string }).kind).toBe('single');
    expect((ev('[S] * 1.0', { S: single(0.21) }) as { kind: string }).kind).toBe('double');
  });

  test('unary minus is 0 minus the value, so it keeps an int an int', () => {
    expect(ev('-(3)')).toEqual(int(-3));
    expect(ev('-([A])', { A: 2.5 })).toEqual(double(-2.5));
  });
});

describe('null', () => {
  test('a property nobody publishes is null', () => {
    expect(ev('[Missing]')).toBeNull();
  });

  test('null in a comparison or in arithmetic throws, as `null > 0` does on the dash (#454)', () => {
    expect(failsAtRuntime('[Missing] > 0').reason).toMatch(/null/);
    expect(failsAtRuntime('[Missing] = 0').reason).toMatch(/null/);
    expect(failsAtRuntime('[Missing] + 1').reason).toMatch(/null/);
    expect(failsAtRuntime('1 - [Missing]').reason).toMatch(/null/);
  });

  test('a binding catches that and draws nothing, the way SimHub does', () => {
    const result = evaluateBinding('[Missing] > 0', scope());
    expect(result.ok).toBe(false);
    expect(evaluateBinding('1 + 1', scope())).toEqual({ ok: true, value: int(2) });
  });

  test('isnull tests with one argument and substitutes with two', () => {
    expect(ev('isnull([Missing])')).toBe(true);
    expect(ev('isnull([A])', { A: 0 })).toBe(false);
    expect(ev('isnull([Missing], 5)')).toEqual(int(5));
    expect(ev("isnull([A], 'x')", { A: 'set' })).toBe('set');
  });

  test('Unverified: isnull evaluates its fallback only when it needs it', () => {
    expect(ev('isnull([A], ([Missing]) > (0))', { A: 1 })).toEqual(int(1));
  });

  test('Convert.ToBoolean reads a null as false, so `and`, `or`, `!` and `if` do not throw on one', () => {
    expect(ev('!([Missing])')).toBe(true);
    expect(ev('([Missing]) and (true)')).toBe(false);
    expect(ev('([Missing]) or (true)')).toBe(true);
    expect(js("if([Missing], 'a', 'b')")).toBe('b');
  });

  test('timespantoseconds of anything but a TimeSpan is null, the number 0 included (#454)', () => {
    expect(ev('timespantoseconds(0)')).toBeNull();
    expect(ev('timespantoseconds([Missing])')).toBeNull();
    expect(failsAtRuntime('(timespantoseconds(isnull([T], 0))) > (0)').reason).toMatch(/null/);
    expect(ev('(isnull(timespantoseconds([T]), 0)) > (0)')).toBe(false);
  });
});

describe('evaluation order', () => {
  test('and and or stop at the left operand when it decides (#762)', () => {
    expect(ev('(false) and (([Missing]) > (0))')).toBe(false);
    expect(ev('(true) or (([Missing]) > (0))')).toBe(true);
    expect(failsAtRuntime('(true) and (([Missing]) > (0))').reason).toMatch(/null/);
  });

  test('if evaluates the branch it takes and no other', () => {
    expect(ev('if(true, 1, ([Missing]) > (0))')).toEqual(int(1));
  });

  test('a changed() behind a false term is never asked, so its first ask is still to come', () => {
    const state = new CallState();
    const at = (now: number, gate: boolean, v: number) => ev('(isnull([G], false)) and (changed(3000, [V]))', { G: gate, V: v }, { state, now });
    expect(at(0, false, 1)).toBe(false);
    expect(at(100, true, 2)).toBe(false);
    expect(at(200, true, 3)).toBe(true);
  });
});

describe('+ and strings', () => {
  test('a string on the left concatenates, with .NET\'s ToString of the right', () => {
    expect(ev("'a' + 1")).toBe('a1');
    expect(ev("'a' + 1.5")).toBe('a1.5');
    expect(ev("'a' + true")).toBe('aTrue');
    expect(ev("'a' + [Missing]")).toBe('a');
    expect(ev("'lap ' + (0.1 + 0.2)")).toBe('lap 0.3');
  });

  test('Unverified: a number on the left makes it arithmetic, so a numeric string is parsed and any other throws', () => {
    expect(ev("1 + '2'")).toEqual(decimal(3));
    expect(failsAtRuntime("1 + 'a'").reason).toMatch(/not in a correct format/);
  });

  test('a double is written with fifteen significant digits, a Single with seven, and an exponent outside 1E-05..1E+15', () => {
    expect(numberToString(double(0.1 + 0.2))).toBe('0.3');
    expect(numberToString(double(1 / 3))).toBe('0.333333333333333');
    expect(numberToString(single(0.1))).toBe('0.1');
    expect(numberToString(double(1e-7))).toBe('1E-07');
    expect(numberToString(double(1.5e15))).toBe('1.5E+15');
    expect(numberToString(double(-42))).toBe('-42');
  });
});

describe('comparison converts to the most precise type', () => {
  test('an int and a double compare as doubles', () => {
    expect(ev('1 = 1.0')).toBe(true);
    expect(ev('[A] > 2', { A: 2.5 })).toBe(true);
  });

  test('a bool and an int compare as bools, so a bool property satisfies `= 1` (#762)', () => {
    expect(ev('[P] = 1', { P: true })).toBe(true);
    expect(ev('[P] = 0', { P: true })).toBe(false);
  });

  test('Unverified: an int and a string compare as strings, and a double and a word throw', () => {
    expect(ev("'3' = 3")).toBe(true);
    expect(ev("'03' = 3")).toBe(false);
    expect(failsAtRuntime("'class' = 1.5").reason).toMatch(/not in a correct format/);
  });

  test('two strings compare as strings, and two TimeSpans by their length', () => {
    expect(ev("'class' = 'class'")).toBe(true);
    expect(ev("'a' != 'b'")).toBe(true);
    expect(ev('[A] < [B]', { A: seconds(1), B: seconds(2) })).toBe(true);
  });

  test('in() compares each candidate as = does and stops at the first match', () => {
    expect(ev("in('b', 'a', 'b')")).toBe(true);
    expect(ev('in(2, 1, 2.0)')).toBe(true);
    expect(ev('in(3, 1, 2)')).toBe(false);
    expect(failsAtRuntime('in([Missing], 1)').reason).toMatch(/null/);
  });
});

describe('format', () => {
  test('the patterns the build writes', () => {
    expect(js("format(7, '00')")).toBe('07');
    expect(js("format(7, '0.0')")).toBe('7.0');
    expect(js("format([A], '0')", { A: 2.5 })).toBe('3');
    expect(js("format([A], '0.000')", { A: 1.23456 })).toBe('1.235');
    expect(js("format(1234567, '#,##0')")).toBe('1,234,567');
    expect(js("format(1234, '#,0')")).toBe('1,234');
    expect(js("format([A], '0.00')", { A: 0.125 })).toBe('0.13');
  });

  test('a half is rounded away from zero after fifteen significant digits, so 1.005 is 1.01 (.NET reference source)', () => {
    expect(formatNumber(double(1.005), '0.00')).toBe('1.01');
    expect(formatNumber(double(9.9995), '0.000')).toBe('10.000');
    expect(formatNumber(double(12.345), '0.00')).toBe('12.35');
    expect(formatNumber(double(-2.5), '0')).toBe('-3');
  });

  test('the sign flag puts a + on a double or an int, and a - stays on a small negative double', () => {
    expect(js("format([A], '0.0', true)", { A: 1.5 })).toBe('+1.5');
    expect(js("format([A], '0.0', true)", { A: -1.5 })).toBe('-1.5');
    expect(js("format([A], '0.00', true)", { A: -0.001 })).toBe('-0.00');
    expect(js("format(3, '0', true)")).toBe('+3');
  });

  test('Unverified: the sign flag puts a + on a zero', () => {
    expect(js("format([A], '0.00', true)", { A: double(0) })).toBe('+0.00');
  });

  test('a Single is not signed, and drops its minus where the figure is zero (#322)', () => {
    expect(js("format([S], '0.00', true)", { S: single(0.21) })).toBe('0.21');
    expect(js("format([S], '0.00', true)", { S: single(-0.001) })).toBe('0.00');
    expect(js("format([S], '0.00', true)", { S: single(-0.21) })).toBe('-0.21');
  });

  test('a date is formatted by its wall clock, in the hour and minute specifiers a clock uses', () => {
    const t = parseDateTime('2026-09-29T08:05:52.1576540-07:00');
    expect(js("format([T], 'HH:mm')", { T: t })).toBe('08:05');
    expect(js("format([T], 'h:mm')", { T: t })).toBe('8:05');
    expect(js("format([T], 'hh')", { T: date(Date.UTC(2026, 0, 1, 0, 30)) })).toBe('12');
    expect(js("format([T], 'H')", { T: date(Date.UTC(2026, 0, 1, 13, 30)) })).toBe('13');
  });

  test('Unverified: a null formats as null, and a string comes back as itself', () => {
    expect(ev("format([Missing], '0.0')")).toBeNull();
    expect(ev("format('N', '0')")).toBe('N');
  });

  test('a pattern outside the ones the build writes is refused, not approximated', () => {
    expect(unsupported("format(1, '0.0%')").reason).toMatch(/number format/);
    expect(unsupported("format([T], 'yyyy')", { T: date(0) }).reason).toMatch(/date format/);
    expect(unsupported("format([T], 'mm')", { T: seconds(1) }).reason).toMatch(/TimeSpan/);
  });
});

describe('TimeSpans', () => {
  test('timespantoseconds and secondstotimespan, which rounds to the millisecond as .NET Framework does', () => {
    expect(js('timespantoseconds([T])', { T: parseTimeSpan('00:01:38.4120000') })).toBeCloseTo(98.412, 9);
    expect(ev('secondstotimespan(1.23456)')).toEqual(timespan(12_350_000));
    expect(ev('secondstotimespan(90)')).toEqual(timespan(900_000_000));
  });

  test('Unverified: secondstotimespan of a null is null', () => {
    expect(ev('secondstotimespan([Missing])')).toBeNull();
  });

  test('toshorttime with forced minutes is m:ss.fff, and h:mm:ss.fff from the hour (verified on the VM)', () => {
    const lap = parseTimeSpan('00:01:38.4120000');
    expect(js('toshorttime([T], 3, false, true)', { T: lap })).toBe('1:38.412');
    expect(js('toshorttime([T], 1, false, true)', { T: lap })).toBe('1:38.4');
    expect(js('toshorttime([T], 3, false, true)', { T: parseTimeSpan('00:00:27.0730000') })).toBe('0:27.073');
    expect(js('toshorttime([T], 1, false, true)', { T: parseTimeSpan('01:02:03.5000000') })).toBe('1:02:03.5');
  });

  test('Unverified: toshorttime truncates the fraction, signs on request, and drops the minutes when not forced', () => {
    expect(js('toshorttime([T], 3, false, true)', { T: parseTimeSpan('00:01:38.4129000') })).toBe('1:38.412');
    expect(js('toshorttime([T], 2, true, true)', { T: seconds(1.5) })).toBe('+0:01.50');
    expect(js('toshorttime([T], 2, true, true)', { T: seconds(-1.5) })).toBe('-0:01.50');
    expect(js('toshorttime([T], 1, false, false)', { T: seconds(5.2) })).toBe('5.2');
    expect(js('toshorttime([T], 0, false, true)', { T: seconds(65) })).toBe('1:05');
    expect(ev('toshorttime([Missing], 3, false, true)')).toBeNull();
  });

  test('the constant format a trace carries reads back, days and negatives included', () => {
    expect(parseTimeSpan('00:01:38')).toEqual(timespan(980_000_000));
    expect(parseTimeSpan('-1.02:00:00')).toEqual(timespan(-(26 * 3600) * TICKS_PER_SECOND));
    expect(() => parseTimeSpan('1:38.412')).toThrow();
  });
});

describe('NCalc\'s maths', () => {
  test('Unverified: round is to even at the midpoint, on the scaled double as .NET Framework does it', () => {
    expect(js('round(2.5, 0)')).toBe(2);
    expect(js('round(3.5, 0)')).toBe(4);
    expect(js('round(-2.5, 0)')).toBe(-2);
    expect(js('round(1.25, 1)')).toBe(1.2);
    // 2.675 * 100 is exactly 267.5 as a double, so the scaled value is a midpoint and goes to even;
    // 1.005 * 100 is 100.49999999999999, so it goes down. .NET Core rounds these differently.
    expect(js('round(2.675, 2)')).toBe(2.68);
    expect(js('round(1.005, 2)')).toBe(1);
    expect((ev('round(7, 0)') as { kind: string }).kind).toBe('double');
  });

  test('truncate is a double toward zero', () => {
    expect(ev('truncate(-1.7)')).toEqual(double(-1));
    expect(ev('truncate(7 / 2)')).toEqual(double(3));
  });

  test('Unverified: truncate and round read a null as zero, through Convert.ToDouble', () => {
    expect(ev('truncate([Missing])')).toEqual(double(0));
    expect(ev('round([Missing], 1)')).toEqual(double(0));
  });

  test('Unverified: abs is a decimal, so abs(7) / 2 is 3.5 and not 3', () => {
    expect(ev('abs(-7)')).toEqual(decimal(7));
    expect(js('abs(-7) / 2')).toBe(3.5);
    expect(failsAtRuntime('abs(-7) * 1.0').reason).toMatch(/decimal/);
  });

  test('Unverified: max and min take the left operand\'s type, so max(0, 2.6) is the int 3', () => {
    expect(ev('max(0, 2.6)')).toEqual(int(3));
    expect(ev('max(2.6, 0)')).toEqual(double(2.6));
    expect(ev('min([A], 3.5)', { A: 4.25 })).toEqual(double(3.5));
    expect(ev('max([Missing], 3)')).toEqual(int(3));
    expect(ev('max([Missing], [Missing])')).toBeNull();
  });

  test('cos and sin of a double', () => {
    expect(js('cos(0)')).toBe(1);
    expect(js('sin(0)')).toBe(0);
  });
});

describe('strings', () => {
  test('ucase, lcase and replace, every occurrence', () => {
    expect(ev("ucase('Liam')")).toBe('LIAM');
    expect(ev("lcase('LIAM')")).toBe('liam');
    expect(ev("replace('-1.5 -2', '-', '−')")).toBe('−1.5 −2');
  });

  test('replace with an empty needle throws, as String.Replace does', () => {
    expect(failsAtRuntime("replace('x', '', 'y')").reason).toMatch(/zero length/);
  });

  test('Unverified: the string functions read a null as the empty string', () => {
    expect(ev('ucase([Missing])')).toBe('');
    expect(ev("left([Missing], 0, 4)")).toBe('');
  });

  test('left and right take a value, a start index and a length', () => {
    expect(ev("left('Porsche', 0, 3)")).toBe('Por');
    expect(ev("left('GT3', 0, 4)")).toBe('GT3');
    expect(ev("left('Porsche', 2, 3)")).toBe('rsc');
    expect(ev("right('Porsche', 0, 3)")).toBe('che');
  });

  test('tcase is ToTitleCase: McDonald becomes Mcdonald and a shouted word is left alone', () => {
    expect(ev("tcase('liam McDonald BYRNE')")).toBe('Liam Mcdonald BYRNE');
  });
});

describe('what the scope supplies', () => {
  test('prop() reads a property by a computed name', () => {
    expect(js("prop('PersistantTrackerPlugin.PreviousLap_' + format(repeatindex() - 1, '00'))", { 'PersistantTrackerPlugin.PreviousLap_02': 7 }, { repeat: [3] })).toBe(7);
  });

  test('a dashboard variable is read without regard to case', () => {
    expect(ev('[variable.CompanionShown]', { 'variable.companionShown': true })).toBe(true);
  });

  test('repeatindex() is the innermost repeated layer\'s copy, and a depth counts outward', () => {
    expect(ev('repeatindex()', {}, { repeat: [2, 5] })).toEqual(int(5));
    expect(ev('repeatindex(1)', {}, { repeat: [2, 5] })).toEqual(int(2));
    expect(unsupported('repeatindex()').reason).toMatch(/outside any repeated layer/);
  });

  test('rootdashboardscreenname() is the previous frame\'s screen, and a scope without one is refused', () => {
    expect(ev('rootdashboardscreenname()', {}, { rootScreenName: 'Race' })).toBe('Race');
    expect(unsupported('rootdashboardscreenname()').reason).toMatch(/previous frame/);
  });
});

describe('changed, isincreasing and isdecreasing', () => {
  test('the first ask records and answers false; a move opens the window and the clock closes it (#762)', () => {
    const state = new CallState();
    const at = (now: number, v: number) => ev('changed(3000, [V])', { V: v }, { state, now });
    expect(at(0, 1)).toBe(false);
    expect(at(100, 1)).toBe(false);
    expect(at(200, 2)).toBe(true);
    expect(at(3100, 2)).toBe(true);
    expect(at(3200, 2)).toBe(false);
  });

  test('the state is keyed by the value expression\'s text, so two calls with the same text share it', () => {
    const state = new CallState();
    ev('changed(3000, [V])', { V: 1 }, { state, now: 0 });
    expect(ev('changed(5000, [V])', { V: 2 }, { state, now: 10 })).toBe(true);
  });

  test('Unverified: isincreasing and isdecreasing answer by the direction of the last move, inside the window', () => {
    const state = new CallState();
    const up = (now: number, v: number) => ev('isincreasing(1000, [V])', { V: v }, { state, now });
    const down = (now: number, v: number) => ev('isdecreasing(1000, [V])', { V: v }, { state, now });
    expect(up(0, 5)).toBe(false);
    expect(down(0, 5)).toBe(false);
    expect(up(10, 6)).toBe(true);
    expect(down(10, 6)).toBe(false);
    expect(down(20, 4)).toBe(true);
    expect(down(2000, 4)).toBe(false);
  });

  test('a scope with no state store or clock is refused rather than answered', () => {
    expect(unsupported('changed(3000, [V])', { V: 1 }).reason).toMatch(/state/);
  });
});

describe('the opponent calls', () => {
  const calls = { 'drivername(3)': 'L. Byrne', 'getplayerleaderboardposition()': 4, 'driversectorlastlap(4, 2, false)': seconds(31.2), 'getopponentleaderboardposition_aheadbehind(-1)': 3 };

  test('a call is answered from the recorded column of its canonical text, its arguments computed first', () => {
    expect(ev('drivername((repeatindex()) + (2))', {}, { repeat: [1], calls })).toBe('L. Byrne');
    expect(ev('DriverName(getopponentleaderboardposition_aheadbehind(-1))', {}, { calls })).toBe('L. Byrne');
    expect(js('timespantoseconds(driversectorlastlap(getplayerleaderboardposition(), 2, false))', {}, { calls })).toBeCloseTo(31.2, 6);
  });

  test('a call with no column is null, as a position naming no car is in SimHub', () => {
    expect(ev('drivername(9)', {}, { calls })).toBeNull();
    expect(ev('drivername(-1)', {}, { calls })).toBeNull();
    expect(ev('drivername(2.5)', {}, { calls })).toBeNull();
    expect(ev('drivername(3)')).toBeNull();
  });
});

describe('what the evaluator refuses', () => {
  test('a call SimHub would not dispatch is refused loudly, the left([Class], 4) bug first among them', () => {
    expect(unsupported('left([Class], 4)').reason).toMatch(/SimHub would draw nothing/);
    expect(unsupported('nosuchfunction(1)').reason).toMatch(/not a function SimHub implements/);
  });

  test('a function SimHub has and the evaluator does not is refused, not guessed at', () => {
    expect(unsupported('floor(1.5)').reason).toMatch(/not one the evaluator implements/);
    expect(unsupported('round(1.5)').reason).toMatch(/implemented with 2 arguments/);
  });

  test('every arity the evaluator accepts is one SimHub dispatches, so it can never compute a call SimHub would not', () => {
    for (const [name, arity] of SUPPORTED_FUNCTIONS) {
      const simhub = NCALC_FUNCTIONS.get(name);
      expect({ name, inSimHubsTable: simhub !== undefined }).toEqual({ name, inSimHubsTable: true });
      for (let count = 0; count <= 16; count++) {
        if (arityAccepts(arity, count)) expect({ name, count, simhub: arityAccepts(simhub!.arity, count) }).toEqual({ name, count, simhub: true });
      }
    }
  });

  test('an error names the expression and the offset of the node that failed', () => {
    const e = failsAtRuntime("'ok' + format(([Missing]) > (0), '0')");
    // The comparison failed, and a node's span starts at its left operand, not at the bracket.
    expect(e.offset).toBe(15);
    expect(e.message).toContain("'ok' + format(([Missing]) > (0), '0')");
  });
});
