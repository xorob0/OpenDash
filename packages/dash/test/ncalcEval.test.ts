/**
 * The test evaluator answers `max` and `min` as SimHub does, #1046.
 *
 * NCalc 1.3.8's `Numbers.Max` and `Min` answer in the type of their left operand, the right one
 * converted to it, so an Int32 on the left rounds a reading to a whole number before anything
 * formats it (#831). `ncalcEval.ts` ran them as `Math.max` and `Math.min`, and every test that drew a
 * reading through it agreed with a dash that did not exist: #831's clocks and refuel figure, and the
 * fraction of the lap #1036 clamped, passed their tests and read wrong on the VM. These hold the
 * evaluator to the generator's own model of the rule (`extreme()` in
 * `packages/generator/src/ncalc/functions.ts`), which was read from the NCalc that SimHub ships.
 */
import { describe, expect, test } from 'bun:test';
import { ncalcEvaluator as E } from '../src/generator.ts';
import { evalNcalc, Single, type Props } from './ncalcEval.ts';

/** What the generator's evaluator makes of an expression, with every number in `props` a double. */
const typed = (expression: string, props: Props): unknown => {
  const properties = Object.fromEntries(Object.entries(props).map(([k, v]) => [k, typeof v === 'number' ? E.double(v) : v]));
  return E.toJs(E.evaluate(expression, { properties }));
};

describe('max and min answer in their left operand type', () => {
  test('an Int32 on the left rounds the right operand, and a double keeps its fraction', () => {
    expect(evalNcalc('max(0, 0.5002)', {})).toBe(1);
    expect(evalNcalc('max(0.0, 0.5002)', {})).toBe(0.5002);
    expect(evalNcalc('min(1, 0.4998)', {})).toBe(0);
    expect(evalNcalc('min(1.0, 0.4998)', {})).toBe(0.4998);
  });

  test('the right operand is rounded as Convert.ToInt32 rounds, a half to the even number', () => {
    expect(evalNcalc('max(0, 2.5)', {})).toBe(2);
    expect(evalNcalc('max(0, 3.5)', {})).toBe(4);
    expect(evalNcalc('min(0, -2.5)', {})).toBe(-2);
    expect(evalNcalc('max(0, [X])', { X: 59.6 })).toBe(60);
  });

  test('the type of a left operand that is not a literal is the type NCalc gives it', () => {
    // A property is a double, so the clamp keeps the fraction of what it clamps.
    expect(evalNcalc('max([X], 0)', { X: 0.25 })).toBe(0.25);
    expect(evalNcalc('min([X], 1)', { X: 0.25 })).toBe(0.25);
    // Int32 arithmetic stays an Int32; a double anywhere in it makes a double.
    expect(evalNcalc('max((1) - (1), [X])', { X: 0.6 })).toBe(1);
    expect(evalNcalc('max((1.0) - (1), [X])', { X: 0.6 })).toBe(0.6);
    expect(evalNcalc('max(-(1), [X])', { X: -0.6 })).toBe(-1);
    // An Int32 over an Int32 is a double, since NCalc divides with the left operand made a double.
    expect(evalNcalc('max((1) / (2), [X])', { X: 0.6 })).toBe(0.6);
    // The branch an `if` takes and the side `isnull` hands on decide it.
    expect(evalNcalc('max(if([C], 0, 0.0), [X])', { C: true, X: 0.6 })).toBe(1);
    expect(evalNcalc('max(if([C], 0, 0.0), [X])', { C: false, X: 0.6 })).toBe(0.6);
    expect(evalNcalc('max(isnull([Y], 0), [X])', { X: 0.6 })).toBe(1);
    expect(evalNcalc('max(isnull([Y], 0), [X])', { Y: 0, X: 0.6 })).toBe(0.6);
    // An inner max or min passes its own left operand type outward.
    expect(evalNcalc('max(0.0, min(100, [X]))', { X: 0.6 })).toBe(1);
    expect(evalNcalc('max(0, min(100.0, [X]))', { X: 0.6 })).toBe(1);
    expect(evalNcalc('max(0.0, min(100.0, [X]))', { X: 0.6 })).toBe(0.6);
    // truncate and round are doubles, abs a decimal.
    expect(evalNcalc('max(truncate([X]), [Y])', { X: 2.7, Y: 2.4 })).toBe(2.4);
    expect(evalNcalc('max(abs([X]), [Y])', { X: -0.25, Y: 0.5 })).toBe(0.5);
  });

  test('a Single on the left keeps the answer a Single, which format does not sign', () => {
    const answer = evalNcalc('max([S], 0)', { S: new Single(0.25) });
    expect(answer).toBeInstanceOf(Single);
    expect(Number(answer)).toBe(0.25);
    expect(evalNcalc("format(max([S], 0), '0.00', true)", { S: new Single(0.25) })).toBe('0.25');
    expect(evalNcalc("format(max(0.0, [S]), '0.00', true)", { S: new Single(0.25) })).toBe('+0.25');
  });

  test('a null side gives the other side, as NCalc answers it', () => {
    expect(evalNcalc('max([Y], 0.6)', {})).toBe(0.6);
    expect(evalNcalc('min(0, [Y])', {})).toBe(0);
    expect(evalNcalc('max([Y], [Z])', {})).toBeNull();
  });

  test('every case above is what the generator evaluator answers', () => {
    const cases: [string, Props][] = [
      ['max(0, 0.5002)', {}],
      ['max(0.0, 0.5002)', {}],
      ['min(1, 0.4998)', {}],
      ['min(1.0, 0.4998)', {}],
      ['max(0, 2.5)', {}],
      ['max(0, 3.5)', {}],
      ['min(0, -2.5)', {}],
      ['max(0, [X])', { X: 59.6 }],
      ['max([X], 0)', { X: 0.25 }],
      ['max((1) - (1), [X])', { X: 0.6 }],
      ['max((1.0) - (1), [X])', { X: 0.6 }],
      ['max(-(1), [X])', { X: -0.6 }],
      ['max(if([C], 0, 0.0), [X])', { C: true, X: 0.6 }],
      ['max(if([C], 0, 0.0), [X])', { C: false, X: 0.6 }],
      ['max(isnull([Y], 0), [X])', { X: 0.6 }],
      ['max(0.0, min(100, [X]))', { X: 0.6 }],
      ['max(0, min(100.0, [X]))', { X: 0.6 }],
      ['max(truncate([X]), [Y])', { X: 2.7, Y: 2.4 }],
      ['max([Y], 0.6)', {}],
    ];
    for (const [expression, props] of cases) expect({ expression, answer: evalNcalc(expression, props) }).toEqual({ expression, answer: typed(expression, props) });
  });
});
