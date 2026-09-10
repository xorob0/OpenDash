/** Rung selection from the slot size, and the sizes each rung carries. */
import { describe, expect, test } from 'bun:test';
import { rungFor, rungForSlot, rungSpec } from '../src/design/rung.ts';
import { rect } from '../src/design/geometry.ts';

describe('rung', () => {
  test('L needs 250 wide and 150 tall, M 180 wide, S otherwise', () => {
    expect(rungFor(255, 187)).toBe('L');
    expect(rungFor(250, 150)).toBe('L');
    expect(rungFor(249, 187)).toBe('M');
    expect(rungFor(255, 149)).toBe('M');
    expect(rungFor(223, 156)).toBe('M');
    expect(rungFor(180, 100)).toBe('M');
    expect(rungFor(179, 200)).toBe('S');
    expect(rungFor(140, 104)).toBe('S');
  });

  test('sizes and padding per rung', () => {
    expect(rungSpec('L')).toEqual({ rung: 'L', value: 64, denominator: 46, grid: 46, padding: { y: 12, x: 16 } });
    expect(rungSpec('M')).toEqual({ rung: 'M', value: 46, denominator: 34, grid: 34, padding: { y: 12, x: 16 } });
    expect(rungSpec('S')).toEqual({ rung: 'S', value: 34, denominator: 34, grid: 34, padding: { y: 8, x: 12 } });
    expect(rungForSlot(rect(0, 0, 255, 187)).rung).toBe('L');
  });
});
