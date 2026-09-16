/**
 * The density table against the design canvas.
 *
 * These are the numbers every second-screen module measures itself with, and several of them sit
 * off the token scales -- the 5 px field gap, the 6 px unit gap -- so nothing but a test says where
 * they came from. A value that moves here moves every page at once, which is exactly the kind of
 * change that should be deliberate.
 */
import { describe, expect, test } from 'bun:test';
import { densityOf } from '../src/second/density.ts';
import { DENOMINATOR_GAP, UNIT_GAP, denominatorSize } from '../src/second/field.ts';
import { READOUT_GAP } from '../src/components/readout.ts';

describe('the gaps the canvas draws', () => {
  test('a label row is five pixels above its value row, at every density', () => {
    for (const density of ['companion', 'zone', 'compact', 'wide'] as const) {
      expect({ density, fieldGap: densityOf(density).fieldGap }).toEqual({ density, fieldGap: 5 });
    }
    expect(READOUT_GAP).toBe(5);
  });

  test('a unit follows its value by six pixels and a denominator by eight', () => {
    expect({ unit: UNIT_GAP, denominator: DENOMINATOR_GAP }).toEqual({ unit: 6, denominator: 8 });
  });

  test('a denominator is 32 beside a 46 px value and 44 beside a 64 px one', () => {
    expect([denominatorSize(34), denominatorSize(46), denominatorSize(64), denominatorSize(76)]).toEqual([23, 32, 44, 53]);
  });
});

describe('the class chip', () => {
  test('is 20 high with 6 either side wherever a row can hold it', () => {
    for (const density of ['companion', 'zone', 'wide'] as const) {
      const d = densityOf(density);
      expect({ density, height: d.chipHeight, padding: d.chipPadding }).toEqual({ density, height: 20, padding: 6 });
    }
  });

  test('keeps its own smaller pair on the compact ramp, whose row is 20 px tall', () => {
    const d = densityOf('compact');
    expect({ height: d.chipHeight, padding: d.chipPadding, rowHeight: d.rowHeight }).toEqual({ height: 15, padding: 4, rowHeight: 20 });
  });
});
