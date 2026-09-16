/**
 * Every text a package draws has to fit the box it is drawn in. SimHub hands the box to WPF as
 * `MaxTextWidth` and `MaxTextHeight` and WPF clips whatever does not fit, so a box sized to the
 * exact text loses the last glyph's final pixels and a label one pixel too long loses its last
 * letter. These tests measure, from the bundled fonts, what every item of every layout draws.
 */
import { describe, expect, test } from 'bun:test';
import { measureText, type MeasuredFace } from '../src/design/advances.ts';
import { CELL, LINE_SPACING } from '../src/design/metrics.ts';
import { buildLayout } from '../src/dashboard.ts';
import { LAYOUTS } from '../src/layouts/index.ts';
import { itemsOf } from '../src/walk.ts';
import { cellOverruns } from './monoGlyphs.ts';
import type { Dashboard, TextItem } from '../src/generator.ts';

const opts = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

/** Which measured face an item draws in: the family it names, at the weight it asks for. */
const faceOf = (item: TextItem): MeasuredFace => {
  if (item.font === 'Barlow') return item.fontWeight === 'Bold' ? 'BarlowBold' : 'BarlowMedium';
  if (item.fontWeight === 'Bold') return 'BarlowCondensedBold';
  if (item.fontWeight === 'Light') return 'BarlowCondensedLight';
  return 'BarlowCondensedSemiBold';
};

/**
 * What the item will draw. A bound item draws its binding rather than its sample, so where one
 * declares `widest` that is what has to fit: a box cut to the sample is how "NO FLAG" came out as
 * "NO FLA" on the pit wall header.
 */
const drawnText = (item: TextItem): string => item.widest ?? item.text;

/** Width of what the item draws: its cells when monospaced, the measured advances otherwise. */
function drawnWidth(item: TextItem): number {
  const text = drawnText(item);
  const mono = item.monospace;
  if (!mono) return measureText(faceOf(item), text, item.fontSize);
  const specials = [...text].filter((c) => mono.specialChars?.includes(c) ?? false).length;
  return (text.length - specials) * mono.charWidth + specials * mono.specialCharsWidth;
}

const texts = (dashboard: Dashboard): TextItem[] => [...itemsOf(dashboard)].filter((i): i is TextItem => i.kind === 'text');

describe('every text fits the box SimHub clips it to', () => {
  for (const layout of LAYOUTS) {
    const { main, cards } = buildLayout(layout, opts);
    for (const [file, dashboard] of [
      ['main', main],
      ['cards', cards],
    ] as const) {
      test(`${layout.folder} ${file}`, () => {
        const items = texts(dashboard);
        expect(items.length).toBeGreaterThan(0);
        for (const item of items) {
          const width = drawnWidth(item);
          expect({ item: item.name, text: drawnText(item), width, box: item.rect.width, fits: width < item.rect.width }).toMatchObject({ fits: true });
          const line = LINE_SPACING * item.fontSize;
          expect({ item: item.name, line, box: item.rect.height, fits: line <= item.rect.height }).toMatchObject({ fits: true });
        }
      });
    }
  }

  test('a monospace cell is never narrower than the glyphs it holds', () => {
    // The floor: whatever else a value draws, it draws digits and the separators, and every cell
    // has to hold those at the weight the item is drawn in.
    const glyphs = '0123456789-+/OFF';
    const specials = '.,:';
    for (const layout of LAYOUTS) {
      const { main, cards } = buildLayout(layout, opts);
      for (const item of [...texts(main), ...texts(cards)]) {
        const mono = item.monospace;
        if (!mono) continue;
        const face: MeasuredFace = 'BarlowCondensedSemiBold';
        for (const ch of glyphs) {
          const advance = measureText(face, ch, item.fontSize);
          expect({ item: item.name, ch, advance, cell: mono.charWidth, fits: advance <= mono.charWidth }).toMatchObject({ fits: true });
        }
        for (const ch of specials) {
          const advance = measureText(face, ch, item.fontSize);
          expect({ item: item.name, ch, advance, cell: mono.specialCharsWidth, fits: advance <= mono.specialCharsWidth }).toMatchObject({ fits: true });
        }
      }
    }
  });

  test('and it holds every glyph the item itself can draw', () => {
    // The test above is a fixed set, which is exactly why it missed thirty-one `#` on the second
    // screens: `#` is not in that string and the second screens are not in this loop. This one
    // asks each item what it draws -- its sample, its widest, and every literal its binding can
    // reach the screen with -- so a value that gains a character nobody measured fails here.
    for (const layout of LAYOUTS) {
      const { main, cards } = buildLayout(layout, opts);
      for (const item of [...texts(main), ...texts(cards)]) {
        if (!item.monospace) continue;
        expect({ layout: layout.folder, item: item.name, overruns: cellOverruns(item) }).toMatchObject({ overruns: [] });
      }
    }
  });

  test('the cell fractions cover every glyph of the value set at any size', () => {
    const face: MeasuredFace = 'BarlowCondensedSemiBold';
    for (const ch of '0123456789-+/OF') expect(measureText(face, ch, 1)).toBeLessThanOrEqual(CELL.SemiBold.digit);
    for (const ch of '.,:') expect(measureText(face, ch, 1)).toBeLessThanOrEqual(CELL.SemiBold.special);
  });
});
