/**
 * What the Int32 bound guard in `ncalcCoverage.test.ts` holds a build to, case by case (#1046).
 *
 * #831's guard caught a `max` or `min` with an Int32 literal on its left and let through anything
 * on the right that was a truncate, a max or a min, so `max(0, min(100.0, x))` passed it and rounded
 * on the dash, as did a bound written as `-1`, an `if` or an `isnull` fallback. These are the forms
 * it now catches, and the whole ones it still lets through.
 */
import { describe, expect, test } from 'bun:test';
import { ncalcEvaluator as E } from '../src/generator.ts';
import { roundingBounds } from './int32Bounds.ts';

const flagged = (expression: string): string[] => roundingBounds(E.parse(expression).root).map((node) => E.print(node));

describe('the bounds that may round what they bound', () => {
  test('an Int32 literal on the left of a reading, bare or behind a minus', () => {
    expect(flagged('max(0, [X])')).toEqual(['max(0, [X])']);
    expect(flagged('min(100, [X])')).toEqual(['min(100, [X])']);
    expect(flagged('max(-1, [X])')).toEqual(['max(-(1), [X])']);
  });

  test('an Int32 on the left of an inner bound whose answer may have a fraction', () => {
    expect(flagged('max(0, min(100.0, [X]))')).toEqual(['max(0, min(100.0, [X]))']);
    expect(flagged('min(1, max(0.0, [X]))')).toEqual(['min(1, max(0.0, [X]))']);
    // The inner one is caught in its own turn, and its Int32 answer is whole, so the outer is not.
    expect(flagged('max(0, min(100, [X]))')).toEqual(['min(100, [X])']);
  });

  test('a left operand that is an Int32 on some frame: an if, an isnull fallback, Int32 arithmetic, an inner bound', () => {
    expect(flagged('max(if([C], 0, 0.0), [X])')).toHaveLength(1);
    expect(flagged('max(isnull([Y], 0), [X])')).toHaveLength(1);
    expect(flagged('max((1) - (1), [X])')).toHaveLength(1);
    expect(flagged('min(max(isnull([Y], 0), -3.5), 3.5)')).toEqual(['min(max(isnull([Y], 0), -(3.5)), 3.5)', 'max(isnull([Y], 0), -(3.5))']);
    expect(flagged('max(repeatindex(), [X])')).toHaveLength(1);
  });

  test('a right operand that may have a fraction however it is reached', () => {
    expect(flagged('max(0, ([X]) / (2))')).toHaveLength(1);
    expect(flagged('max(0, (3) / (2))')).toHaveLength(1);
    expect(flagged('max(0, if([C], 1, [X]))')).toHaveLength(1);
    expect(flagged('max(0, round([X], 1))')).toHaveLength(1);
    expect(flagged('max(0, timespantoseconds([T]))')).toHaveLength(1);
    expect(flagged('max(0, abs([X]))')).toHaveLength(1);
  });

  test('let through: a double on the left, a property on the left, and a right operand that is whole', () => {
    expect(flagged('max(0.0, [X])')).toEqual([]);
    expect(flagged('max(-3.5, min(3.5, [X]))')).toEqual([]);
    expect(flagged('max((1.0) - (1), [X])')).toEqual([]);
    expect(flagged('max((1) / (2), [X])')).toEqual([]);
    expect(flagged('max([X], 0)')).toEqual([]);
    expect(flagged('max(0, truncate([X]))')).toEqual([]);
    expect(flagged('max(0, round([X], 0))')).toEqual([]);
    expect(flagged('max(6, min(13, truncate([X])))')).toEqual([]);
    expect(flagged('min(max(isnull([R], 0), 0), 10000)')).toEqual([]);
    expect(flagged('min(if(isnull([V]), 24, ((if([C], 3, 2)) + (1)) * (12)), 55)')).toEqual([]);
    expect(flagged('max(isnull([Y], 0.0), [X])')).toEqual([]);
    // The plugin publishes the brightnesses as Int32s, so the night clamp has nothing to round.
    expect(flagged('min(isnull([OpenDash.LedBrightness], 100), isnull([OpenDash.LightsNightBrightness], 25))')).toEqual([]);
    expect(flagged('min(isnull([OpenDash.LedBrightness], 100), [X])')).toHaveLength(1);
  });
});
