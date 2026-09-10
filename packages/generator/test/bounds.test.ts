/** rotatedBounds and itemBounds: the axis-aligned footprint of a rect rotated around its centre. */

import { describe, expect, test } from 'bun:test';
import { itemBounds, rotatedBounds } from '../src/bounds.ts';
import { rect } from './fixtures.ts';

const close = (a: { left: number; top: number; width: number; height: number }, b: { left: number; top: number; width: number; height: number }): void => {
  expect(a.left).toBeCloseTo(b.left, 6);
  expect(a.top).toBeCloseTo(b.top, 6);
  expect(a.width).toBeCloseTo(b.width, 6);
  expect(a.height).toBeCloseTo(b.height, 6);
};

describe('rotatedBounds', () => {
  test('no rotation returns a copy of the rect', () => {
    const r = { left: 10, top: 20, width: 30, height: 40 };
    expect(rotatedBounds(r)).toEqual(r);
    expect(rotatedBounds(r, 0)).not.toBe(r);
    expect(rotatedBounds(r, 360)).toEqual(r);
  });

  test('a quarter turn swaps width and height around the same centre', () => {
    close(rotatedBounds({ left: 464, top: 234, width: 20, height: 12 }, 90), { left: 468, top: 230, width: 12, height: 20 });
    close(rotatedBounds({ left: 464, top: 234, width: 20, height: 12 }, -90), { left: 468, top: 230, width: 12, height: 20 });
    close(rotatedBounds({ left: 0, top: 0, width: 20, height: 12 }, 180), { left: 0, top: 0, width: 20, height: 12 });
  });

  test('an eighth turn grows the box to the rotated diagonal', () => {
    const b = rotatedBounds({ left: 0, top: 0, width: 20, height: 12 }, 45);
    const extent = (20 + 12) / Math.SQRT2;
    close(b, { left: 10 - extent / 2, top: 6 - extent / 2, width: extent, height: extent });
  });

  test('the centre never moves', () => {
    for (const a of [-84, -30, 12, 45, 84, 135, 270]) {
      const b = rotatedBounds({ left: 100, top: 50, width: 22, height: 14 }, a);
      expect(b.left + b.width / 2).toBeCloseTo(111, 6);
      expect(b.top + b.height / 2).toBeCloseTo(57, 6);
    }
  });
});

describe('itemBounds', () => {
  test('reads the item rotation, defaulting to none', () => {
    const r = { left: 0, top: 0, width: 20, height: 12 };
    expect(itemBounds(rect('r', { rect: r }))).toEqual(r);
    close(itemBounds(rect('r', { rect: r, rotation: 90 })), { left: 4, top: -4, width: 12, height: 20 });
  });
});
