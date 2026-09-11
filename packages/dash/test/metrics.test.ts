/** Text metrics: canvas line boxes to SimHub boxes, monospace cells, and the elements that use them. */
import { describe, expect, test } from 'bun:test';
import { CELL, canvasBaseline, canvasYForBaseline, cells, GEAR_CELL, gearCells, monoWidth, SPECIAL_CHARS, textBox } from '../src/design/metrics.ts';
import { label } from '../src/elements/label.ts';
import { numeral } from '../src/elements/numeral.ts';
import { ds } from '../src/tokens.ts';

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

  test('the gear cell comes from the tokens, with the face\'s special cell', () => {
    expect(GEAR_CELL).toBe(ds.font.cell.gear);
    expect(gearCells(260)).toEqual({ charWidth: 136, specialCharsWidth: 73, specialChars: '.,:' });
    expect(gearCells(180)).toEqual({ charWidth: 94, specialCharsWidth: 51, specialChars: '.,:' });
    const gear = numeral('x.gear', 'N', 0, 0, 260, { digits: 1, specials: 0 }, { weight: 'Bold', mono: gearCells(260) });
    // The cell plus a slack of ceil(0.05 x 260), since the box is what WPF clips to.
    expect(gear.rect).toEqual({ left: 0, top: -26, width: 149, height: 313 });
    expect(gear.monospace?.charWidth).toBe(136);
    expect(numeral('x.gear', 'N', 0, 0, 260, { digits: 1, specials: 0 }, { weight: 'Bold' }).monospace?.charWidth).toBe(128);
  });

  test('the digit cells come from the tokens too, rather than a copy of them', () => {
    expect(CELL.SemiBold).toEqual({ digit: ds.font.cell.semiBold.digit, special: ds.font.cell.semiBold.special });
    expect(CELL.Bold).toEqual({ digit: ds.font.cell.bold.digit, special: ds.font.cell.bold.special });
    expect(SPECIAL_CHARS).toBe(ds.font.cell.specialChars);
  });

  /**
   * The cell exists so that no glyph the gear can draw is clipped, and what clips is the font the
   * renderer chooses rather than the one the dashboard names. That used to be a different font:
   * WPF folded the bundled condensed faces into the "Barlow" family, so "Barlow Condensed" reached
   * a wider face, and the cell was widened to 0.68 em to survive whichever Barlow was picked. The
   * bundled family is renamed now, so the renderer can only resolve a face the package ships, and
   * the cell is measured against those. Both measures are checked: the advance, since WPF lays a
   * run out on advances, and the ink, since that is what is drawn.
   */
  test('the gear cell holds every glyph of every face the package ships for it', async () => {
    const { familyOf, loadFont, measure } = await import('../../../tools/measure-font/measure.ts');
    const { fontsForPackage } = await import('../src/dashboard.ts');
    const faces = fontsForPackage().filter((f) => familyOf(loadFont(f)) === ds.font.data);
    expect(faces.length).toBe(2);
    for (const face of faces) {
      const font = loadFont(face);
      for (const ch of '0123456789NR') {
        const m = measure(font, ch);
        if (!m) throw new Error(`${face} has no glyph for ${ch}`);
        expect({ face, ch, advance: m.advance, fits: m.advance <= GEAR_CELL }).toMatchObject({ fits: true });
        expect({ face, ch, ink: m.xMax, fits: m.xMax <= GEAR_CELL }).toMatchObject({ fits: true });
      }
    }
  });

  /**
   * In pixels, which is what actually clips, and at every size a gear is drawn at plus a sweep
   * below them. The test above compares em fractions against the em token and so cannot see a
   * rounding error; that is how a cell rounded to nearest survived a headroom of 0.006 em.
   */
  test('the integer cell holds the widest advance at every size, not only the em fraction', async () => {
    const { familyOf, loadFont, measure } = await import('../../../tools/measure-font/measure.ts');
    const { fontsForPackage } = await import('../src/dashboard.ts');
    const bold = fontsForPackage().find((f) => familyOf(loadFont(f)) === ds.font.data && f.includes('Bold'))!;
    const font = loadFont(bold);
    const widest = Math.max(...[...'0123456789NR'].map((ch) => measure(font, ch)!.advance));
    const ink = Math.max(...[...'0123456789NR'].map((ch) => measure(font, ch)!.xMax));
    for (const fs of [ds.size.gear, ds.size.gearSm, 40, 60, 83, 84, 120, 228, 300]) {
      const cell = gearCells(fs).charWidth;
      expect({ fs, cell, holdsAdvance: cell >= widest * fs, holdsInk: cell >= ink * fs }).toMatchObject({ holdsAdvance: true, holdsInk: true });
    }
  });

  /**
   * And the reason the cell may now be measured against two faces rather than five: no package
   * ships a face under a family whose name carries a width word, so nothing WPF resolves is a
   * stretch of something else. This is the invariant XOR-108 bought; the rest is in fontFiles.test.
   */
  test('nothing the face ships is filed under a family WPF would fold', async () => {
    const { familyOf, loadFont } = await import('../../../tools/measure-font/measure.ts');
    const { fontsForPackage } = await import('../src/dashboard.ts');
    const widths = /\b(Condensed|Narrow|Compressed|Expanded|Extended|Wide|Semi ?Condensed|Ultra ?Condensed)\b/i;
    for (const file of fontsForPackage()) expect([file, widths.test(familyOf(loadFont(file)) ?? '')]).toEqual([file, false]);
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
    expect(n.font).toBe(ds.font.data);
    expect(n.fontWeight).toBe('SemiBold');
    expect(n.monospace?.charWidth).toBe(31);
    expect(n.bindings).toBeUndefined();
  });
});
