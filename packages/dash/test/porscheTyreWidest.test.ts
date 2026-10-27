/**
 * The Porsche foot's tyre boxes are cut from the widest figure each unit can draw (#995). SimHub hands
 * the tyre temperature and pressure over already converted to the driver's unit, so a corner reads up
 * to `299` °F and `299.9` kPa, and in Barlow Medium the 0, the 2 and the 4 are wider than the 8: a box
 * measured by `188` or `188.8` clips the last digit from 200 °F or 200 kPa.
 */
import { describe, expect, test } from 'bun:test';
import { measureText, widestFigure } from '../src/design/advances.ts';
import { PRESSURE_RANGE, PRESSURE_WIDEST, TEMPERATURE_RANGE, TEMPERATURE_WIDEST } from '../src/themes/porsche/foot.ts';

const width = (text: string): number => measureText('BarlowMedium', text, 100);

describe('the Porsche tyre boxes are measured from the units the corners draw in', () => {
  test('the ranges reach 299 °F and 299.9 kPa', () => {
    expect(TEMPERATURE_RANGE).toBe('299');
    expect(PRESSURE_RANGE).toBe('299.9');
  });

  test('each widest is the widest figure of its range in Barlow Medium', () => {
    expect(TEMPERATURE_WIDEST).toBe(widestFigure('BarlowMedium', '299'));
    expect(PRESSURE_WIDEST).toBe(widestFigure('BarlowMedium', '299.9'));
  });

  test('each widest holds every reading of 200 and over, which an 8-based widest does not', () => {
    for (const reading of ['200', '240', '299']) expect(width(TEMPERATURE_WIDEST)).toBeGreaterThanOrEqual(width(reading));
    for (const reading of ['200.0', '240.4', '299.9']) expect(width(PRESSURE_WIDEST)).toBeGreaterThanOrEqual(width(reading));
    expect(width('188')).toBeLessThan(width('200'));
    expect(width('188.8')).toBeLessThan(width('200.0'));
  });
});
