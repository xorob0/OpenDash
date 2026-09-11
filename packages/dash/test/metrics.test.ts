/** Text metrics: canvas line boxes to SimHub boxes, monospace cells, and the elements that use them. */
import { describe, expect, test } from 'bun:test';
import { canvasBaseline, canvasYForBaseline, cells, GEAR_CELL, gearCells, monoWidth, textBox } from '../src/design/metrics.ts';
import { label } from '../src/elements/label.ts';
import { numeral } from '../src/elements/numeral.ts';

describe('metrics', () => {
  test('a canvas line box (y, fs) becomes Top = y - 0.1 fs, Height = ceil(1.2 fs) + slack', () => {
    // The height is rounded up and given a pixel: WPF clips a line that does not fit MaxTextHeight.
    expect(textBox(71, 64)).toEqual({ top: 71 - 6.4, height: 78 });
    expect(textBox(0, 46).height).toBe(57);
    expect(textBox(0, 46).height).toBeGreaterThan(1.2 * 46);
    expect(canvasBaseline(71, 64)).toBeCloseTo(128.6);
    expect(canvasYForBaseline(128.6, 46)).toBeCloseTo(87.2);
    expect(canvasYForBaseline(128.6, 13)).toBeCloseTo(116.9);
  });

  test('monospace cells per face and size, always rounded up', () => {
    expect(cells('SemiBold', 64)).toEqual({ charWidth: 31, specialCharsWidth: 17, specialChars: '.,:' });
    expect(cells('SemiBold', 46)).toEqual({ charWidth: 22, specialCharsWidth: 12, specialChars: '.,:' });
    expect(cells('SemiBold', 116)).toEqual({ charWidth: 55, specialCharsWidth: 31, specialChars: '.,:' });
    expect(cells('Bold', 260)).toEqual({ charWidth: 128, specialCharsWidth: 73, specialChars: '.,:' });
    expect(monoWidth(cells('SemiBold', 64), { digits: 6, specials: 2 })).toBe(220);
    // A cell never rounds below the glyph advance it has to hold.
    for (const fs of [13, 15, 34, 46, 64, 116, 180, 260]) {
      expect(cells('SemiBold', fs).charWidth).toBeGreaterThanOrEqual(0.47 * fs);
      expect(cells('SemiBold', fs).specialCharsWidth).toBeGreaterThanOrEqual(0.26 * fs);
    }
  });

  test('the gear cell is 0.52 em, wide enough for Bold "N" (0.514 em), with the face\'s special cell', () => {
    expect(GEAR_CELL).toBe(0.52);
    expect(gearCells(260)).toEqual({ charWidth: 135, specialCharsWidth: 73, specialChars: '.,:' });
    expect(gearCells(180)).toEqual({ charWidth: 94, specialCharsWidth: 51, specialChars: '.,:' });
    const gear = numeral('x.gear', 'N', 0, 0, 260, { digits: 1, specials: 0 }, { weight: 'Bold', mono: gearCells(260) });
    // The cell plus a slack of ceil(0.05 x 260), since the box is what WPF clips to.
    expect(gear.rect).toEqual({ left: 0, top: -26, width: 148, height: 313 });
    expect(gear.monospace?.charWidth).toBe(135);
    expect(numeral('x.gear', 'N', 0, 0, 260, { digits: 1, specials: 0 }, { weight: 'Bold' }).monospace?.charWidth).toBe(128);
  });

  test('label and numeral produce integer, top-aligned boxes', () => {
    const l = label('x.label', 'current', 16, 52, 223);
    expect(l.rect).toEqual({ left: 16, top: 51, width: 223, height: 19 });
    expect(l.text).toBe('CURRENT');
    expect(l.vAlign).toBe('top');
    expect(l.font).toBe('Barlow');
    expect(l.fontWeight).toBe('Medium');
    expect(l.monospace).toBeUndefined();
    const n = numeral('x.value', '1:42.905', 16, 71, 64, { digits: 6, specials: 2 });
    // 220 of budget plus a slack of ceil(0.05 x 64), capped by a maxWidth when the caller gives one.
    expect(n.rect).toEqual({ left: 16, top: 65, width: 224, height: 78 });
    expect(numeral('x.value', '1:42.905', 16, 71, 64, { digits: 6, specials: 2 }, { maxWidth: 222 }).rect.width).toBe(222);
    expect(n.font).toBe('Barlow Condensed');
    expect(n.fontWeight).toBe('SemiBold');
    expect(n.monospace?.charWidth).toBe(31);
    expect(n.bindings).toBeUndefined();
  });
});
