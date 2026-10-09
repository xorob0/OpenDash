/**
 * A chip's text box holds the widest text its binding can draw (#569).
 *
 * Both chips on the leaderboard rows were cut for less than they were bound to. The tyre column's
 * box was 14 px, one letter, under a binding that kept four; the class chip was measured from
 * `LMP2`, which `MERC`, `MCLA` and `MAZD` are all wider than. Neither label declared a `widest`, so
 * the fit tests measured each by its sample, `M` and `GT3`, and both passed on every box.
 *
 * So the cut is read off the binding here rather than off the call that made it: every text item
 * of every package, and of every module in every box the second screens are proved against, whose
 * binding is `chipText`'s shape declares a `widest`, the `widest` is at least the count it cuts to
 * of the widest glyph, and its box holds the `widest`.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages } from '../src/build.ts';
import { measureText, widestOf } from '../src/design/advances.ts';
import { MODULES } from '../src/modules/index.ts';
import { CHIP_CHARS, CHIP_GLYPH, CHIP_WIDEST, chip, chipText, chipWidth } from '../src/second/chip.ts';
import { densityOf, type Density } from '../src/second/density.ts';
import { ncalc } from '../src/generator.ts';
import { itemsOf, walkItems } from '../src/walk.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { moduleBoxes } from './secondScreens.test.ts';

const PACKAGES = composePackages({ version: '0.0.0-test', log: () => {} }, true);
const DENSITIES: readonly Density[] = ['companion', 'zone', 'compact', 'wide', 'panel'];

/** The Text binding's formula, or undefined for an unbound item. */
const formulaOf = (item: TextItem): string | undefined => {
  const binding = item.bindings?.Text;
  if (binding === undefined || binding.mode !== 'formula') return undefined;
  return typeof binding.formula === 'string' ? binding.formula : binding.formula.expression;
};

/** The count a chip's binding cuts to, read off `chipText`'s expression; undefined for any other binding. */
const cutOf = (item: TextItem): number | undefined => {
  const match = /^ucase\(left\(isnull\(.*, ''\), 0, (\d+)\)\)$/.exec(formulaOf(item) ?? '');
  return match ? Number(match[1]) : undefined;
};

/**
 * What a chip cut to `chars` owes: that many of the widest glyph, but for four, which is four M.
 * `CHIP_GLYPH` says why W is left out of the class chip; a shorter cut has no such reason.
 */
const owed = (chars: number): string => (chars >= CHIP_CHARS ? CHIP_WIDEST : widestOf('BarlowMedium', chars));

const texts = (items: Iterable<Item>): TextItem[] => [...items].filter((i): i is TextItem => i.kind === 'text');

/** Every chip label among `items`, with what it declares, what it owes and what its box holds. */
function chipsIn(items: Iterable<Item>): { item: string; chars: number; widest?: string; holdsOwed: boolean; boxHolds: boolean }[] {
  return texts(items).flatMap((item) => {
    const chars = cutOf(item);
    if (chars === undefined) return [];
    const width = (text: string): number => measureText('BarlowMedium', text, item.fontSize);
    const widest = item.widest;
    return [{
      item: item.name,
      chars,
      widest,
      holdsOwed: widest !== undefined && width(widest) >= width(owed(chars)),
      // Strictly inside, as `textFit.test.ts` asks: WPF clips at the edge.
      boxHolds: widest !== undefined && width(widest) < item.rect.width,
    }];
  });
}

describe('the widest a chip is sized by', () => {
  test('is four of the widest capital but W', () => {
    const advance = (ch: string): number => measureText('BarlowMedium', ch, 1);
    const others = [...'ABCDEFGHIJKLMNOPQRSTUVXYZ0123456789'];
    expect(others.filter((ch) => advance(ch) > advance(CHIP_GLYPH))).toEqual([]);
    expect(CHIP_WIDEST).toBe(CHIP_GLYPH.repeat(CHIP_CHARS));
  });

  test('and is wider than the class names that clipped under LMP2', () => {
    for (const name of ['LMP2', 'MERC', 'MCLA', 'MAZD', 'BMW ']) {
      expect({ name, wider: measureText('BarlowMedium', CHIP_WIDEST, 1) >= measureText('BarlowMedium', name, 1) }).toEqual({ name, wider: true });
    }
  });

  test('and a chip at every density holds it', () => {
    for (const density of DENSITIES) {
      const d = densityOf(density);
      const room = chipWidth(density) - 2 * d.chipPadding;
      const text = measureText('BarlowMedium', CHIP_WIDEST, d.labelSm);
      expect({ density, text, room, holds: text < room }).toMatchObject({ holds: true });
    }
  });
});

describe('a bound chip says what it can draw', () => {
  test('or is refused', () => {
    expect(() => chip('probe', 'GT3', 0, 0, 'zone', { bind: chipText(ncalc.str('GT3')) })).toThrow(/bound and declares no widest/);
  });

  test('and its label carries the widest to the fit tests', () => {
    const label = texts(chip('probe', 'GT3', 0, 0, 'zone', { bind: chipText(ncalc.str('GT3')), widest: CHIP_WIDEST }))[0]!;
    expect({ widest: label.widest, cut: cutOf(label) }).toEqual({ widest: CHIP_WIDEST, cut: CHIP_CHARS });
  });
});

describe("every chip's box holds the widest its binding can draw", () => {
  test('in every package', () => {
    let seen = 0;
    const cuts = new Set<number>();
    for (const composed of PACKAGES) {
      for (const dashboard of composed.pkg.dashboards) {
        for (const found of chipsIn(itemsOf(dashboard))) {
          seen += 1;
          cuts.add(found.chars);
          expect({ folder: composed.pkg.folderName, dashboard: dashboard.name, ...found }).toMatchObject({ holdsOwed: true, boxHolds: true });
        }
      }
    }
    expect(seen).toBeGreaterThan(50);
    // The class chip's four and the tyre chip's one: a pit wall board draws both.
    expect([...cuts].sort()).toEqual([1, CHIP_CHARS]);
  });

  for (const box of moduleBoxes()) {
    test(`in every module on a ${box.name}`, () => {
      for (const module of MODULES) {
        for (const found of chipsIn(walkItems(module.build({ frame: box.frame, density: box.density, prefix: '' })))) {
          expect({ module: module.id, ...found }).toMatchObject({ holdsOwed: true, boxHolds: true });
        }
      }
    });
  }
});
