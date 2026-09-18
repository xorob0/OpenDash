/**
 * The density table against the design canvas.
 *
 * These are the numbers every second-screen module measures itself with, and several of them sit
 * off the token scales -- the 5 px field gap, the 6 px unit gap -- so nothing but a test says where
 * they came from. A value that moves here moves every page at once, which is exactly the kind of
 * change that should be deliberate.
 */
import { describe, expect, test } from 'bun:test';
import { densityOf, rampOf } from '../src/second/density.ts';
import { resolveToken } from '../src/tokens.ts';
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

describe('the type ramp', () => {
  /**
   * The sizes design/tokens.json carries that a second screen may draw at. The card ramp covers the
   * three large steps and the ui scale, whose own scope line names the pit wall tables, the two
   * small ones; a ramp step that is not one of these is a number the token file cannot move.
   */
  const RAMP_TOKENS = ['font.size.hero', 'font.size.lapTime', 'font.size.value', 'font.size.valueSm', 'font.size.ui.numeralLg', 'font.size.ui.numeral'];

  test('every step of the companion and zone ramps is a token value', () => {
    const sizes = new Set(RAMP_TOKENS.map((path) => resolveToken(path)));
    for (const density of ['companion', 'zone', 'wide'] as const) {
      for (const size of rampOf(density)) expect({ density, size, fromToken: sizes.has(size) }).toEqual({ density, size, fromToken: true });
    }
  });

  test('and the compact ramp is the zone ramp stepped down, but for its last two rungs', () => {
    // 18 and 14 are the two numbers in this file that no token carries; see the TODO beside them.
    expect(rampOf('compact')).toEqual([14, 18, ...rampOf('zone').slice(1, 4)]);
  });
});

describe('the label ramp', () => {
  test('a label is a step above its small label at every density, never level with it', () => {
    // The distinction the sheets draw and the one a page cannot lose: `.lbl` over `.lbl-sm`. The
    // zone ramp used to set both from `labelSm`, so a label and the unit after it read as one run.
    for (const density of ['companion', 'zone', 'wide'] as const) {
      const d = densityOf(density);
      expect({ density, label: d.label, labelSm: d.labelSm }).toEqual({ density, label: 15, labelSm: 13 });
    }
    const compact = densityOf('compact');
    expect({ label: compact.label, labelSm: compact.labelSm }).toEqual({ label: 13, labelSm: 12 });
  });

  test('and sits in the sheets’ own row, which the ramp does not move', () => {
    // Every artboard centres its label in a `height: 13px` row, so the step from 13 to 15 is bought
    // in width alone. A row that grew with the type would push the pit wall panels, whose heights
    // the sheets fix to the pixel, past their own frames.
    for (const density of ['companion', 'zone', 'wide'] as const) {
      expect({ density, labelRow: densityOf(density).labelRow }).toEqual({ density, labelRow: 13 });
    }
    expect(densityOf('compact').labelRow).toBe(12);
  });
});

describe('the companion ramp', () => {
  test('fields are 24 apart across and rows 16 apart down', () => {
    const d = densityOf('companion');
    expect({ gapX: d.gapX, gapY: d.gapY }).toEqual({ gapX: 24, gapY: 16 });
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
