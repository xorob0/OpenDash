/**
 * The licence badge: that the ramp it draws is the token's, and that no letter is wider than the
 * block it is drawn in.
 *
 * The second half is the one that matters at the renderer. SimHub hands a text box to WPF as
 * `MaxTextWidth` and WPF clips silently, so a badge whose block was sized by hand rather than
 * measured from its letter loses the edge of the glyph, and nothing in a screenshot says which
 * pixel went. The check below is the one `secondScreens.test.ts` runs over every package, brought
 * down onto the element so that it fails here first, and it is run in the wider of the two Barlow
 * faces because the ramp draws the same letter at three weights.
 */
import { describe, expect, test } from 'bun:test';
import { BADGE_HEIGHT, BADGE_SIZE, LICENCE_CLASSES, LICENCE_LETTERS, badge, badgeWidth } from '../src/elements/badge.ts';
import { measureText } from '../src/design/advances.ts';
import { LINE_SPACING } from '../src/design/metrics.ts';
import type { RectangleItem, TextItem } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';

const textOf = (name: string, cls: Parameters<typeof badge>[1]): TextItem => badge(name, cls, 0, 0).find((i) => i.kind === 'text') as TextItem;
const blockOf = (cls: Parameters<typeof badge>[1]): RectangleItem | undefined => badge('b', cls, 0, 0).find((i) => i.kind === 'rect') as RectangleItem | undefined;

describe('licence badge', () => {
  test('draws each class at the weight and in the ink purpose.licence names', () => {
    const drawn = LICENCE_CLASSES.map((cls) => {
      const text = textOf('badge', cls);
      return { cls, weight: text.fontWeight, color: text.textColor, font: text.font, size: text.fontSize };
    });
    expect(drawn).toEqual([
      { cls: 'r', weight: 'Medium', color: ds.purpose.licence.r.value, font: ds.font.label, size: BADGE_SIZE },
      { cls: 'd', weight: 'Medium', color: ds.purpose.licence.d.value, font: ds.font.label, size: BADGE_SIZE },
      { cls: 'c', weight: 'Medium', color: ds.purpose.licence.c.value, font: ds.font.label, size: BADGE_SIZE },
      { cls: 'b', weight: 'SemiBold', color: ds.purpose.licence.b.value, font: ds.font.label, size: BADGE_SIZE },
      { cls: 'a', weight: 'SemiBold', color: ds.purpose.licence.a.value, font: ds.font.label, size: BADGE_SIZE },
      { cls: 'pro', weight: 'Bold', color: ds.purpose.licence.pro.value, font: ds.font.label, size: BADGE_SIZE },
    ]);
  });

  test('the ramp gains weight and never loses it', () => {
    const weights = LICENCE_CLASSES.map((cls) => ds.purpose.licence[cls].weight);
    expect(weights).toEqual([...weights].sort((a, b) => a - b));
    expect(weights[0]).toBeLessThan(weights[weights.length - 1]!);
  });

  test('only Pro carries a block, and it is the token fill', () => {
    expect(LICENCE_CLASSES.filter((cls) => blockOf(cls) !== undefined)).toEqual(['pro']);
    expect(blockOf('pro')?.backgroundColor).toBe(ds.purpose.licence.pro.fill);
  });

  test('every letter fits the block it is drawn in, at every size a caller asks for', () => {
    for (const size of [BADGE_SIZE, 14, 15]) {
      const height = Math.ceil(size * 1.5);
      for (const cls of LICENCE_CLASSES) {
        const text = badge('badge', cls, 0, 0, { size, height }).find((i) => i.kind === 'text') as TextItem;
        const face = text.fontWeight === 'Bold' ? 'BarlowBold' : 'BarlowMedium';
        const width = measureText(face, text.text, size);
        expect({ cls, size, fits: width <= text.rect.width }).toEqual({ cls, size, fits: true });
        expect({ cls, size, fits: Math.ceil(LINE_SPACING * size) <= text.rect.height }).toEqual({ cls, size, fits: true });
      }
    }
  });

  test('the block is never narrower than it is tall, and only the letters widen it', () => {
    for (const cls of LICENCE_CLASSES) expect({ cls, atLeastSquare: badgeWidth(cls) >= BADGE_HEIGHT }).toEqual({ cls, atLeastSquare: true });
    // "PRO" is three letters where the rest are one, so it is the badge that grows, and the five
    // single letters stay within a pixel of each other rather than each taking its own box.
    const singles = LICENCE_CLASSES.filter((cls) => LICENCE_LETTERS[cls].length === 1).map((cls) => badgeWidth(cls));
    expect(Math.max(...singles) - Math.min(...singles)).toBeLessThanOrEqual(1);
    expect(badgeWidth('pro')).toBeGreaterThan(Math.max(...singles));
  });

  test('the letter is centred in the block, both ways', () => {
    const [block, text] = badge('badge', 'pro', 40, 10) as [RectangleItem, TextItem];
    expect(text.hAlign).toBe('center');
    expect(text.rect.left).toBe(block.rect.left);
    expect(text.rect.width).toBe(block.rect.width);
    // The letter's line box is centred on the block and, once `textBox` has lifted it by a tenth
    // of an em and given it its 1.2 em of height, still lands inside the block rather than over it.
    expect({ top: text.rect.top >= block.rect.top, bottom: text.rect.top + text.rect.height <= block.rect.top + block.rect.height }).toEqual({ top: true, bottom: true });
  });
});
