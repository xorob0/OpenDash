/**
 * Rule 17: a page answers to the shape of its zone, and sheds its secondary rows before it shrinks
 * its numerals.
 *
 * Two things are proved here. That the band model expresses the four shapes the catalogue actually
 * draws — a single ratio cannot, which is why shape is a pair — and that every one of the
 * twenty-one pages fits every one of them. The zone face gives a page whichever of the four its
 * layout produces, so a page that only fits the wide one is a page that breaks a face.
 */
import { describe, expect, test } from 'bun:test';
import { MODULES } from '../src/modules/index.ts';
import { rect } from '../src/design/geometry.ts';
import { walkItems } from '../src/walk.ts';
import {
  ARCHETYPES,
  columnsAt,
  describeShape,
  heightBandOf,
  keepsSecondaryRanks,
  promotesLead,
  SHAPE_ARCHETYPES,
  shapeOf,
  widthBandOf,
} from '../src/second/shape.ts';
import { cellOverruns } from './monoGlyphs.ts';
import type { TextItem } from '../src/generator.ts';

describe('shape is a pair of bands, not a ratio', () => {
  test('the four the catalogue draws land on four distinct shapes', () => {
    const shapes = ARCHETYPES.map((name) => describeShape(shapeOf(SHAPE_ARCHETYPES[name])));
    expect(new Set(shapes).size).toBe(4);
    expect(shapes).toEqual(['wide/medium', 'medium/medium', 'narrow/medium', 'medium/tall']);
  });

  test('and a single ratio threshold could not tell two of them apart', () => {
    // This is the reason the model is a pair. `tall narrow` is 274/300 = 0.91 and `tall` is
    // 360/470 = 0.77, so they are not even close in ratio -- but `grid` at 430/300 = 1.43 sits
    // between `wide` at 2.14 and them, and lap times shows six fields at `tall` and two at
    // `tall narrow`. Any ordering by ratio puts those two adjacent and they behave oppositely.
    const ratio = (name: keyof typeof SHAPE_ARCHETYPES): number => SHAPE_ARCHETYPES[name].width / SHAPE_ARCHETYPES[name].height;
    const byRatio = [...ARCHETYPES].sort((a, b) => ratio(a) - ratio(b));
    expect(byRatio).toEqual(['tall', 'tallNarrow', 'grid', 'wide']);
    // Ordered by ratio, `tall` and `tallNarrow` are neighbours; by column count they are not.
    expect(columnsAt(shapeOf(SHAPE_ARCHETYPES.tall))).not.toBe(columnsAt(shapeOf(SHAPE_ARCHETYPES.tallNarrow)));
  });

  test('a narrow box is one column whatever its height', () => {
    expect(columnsAt({ width: 'narrow', height: 'short' })).toBe(1);
    expect(columnsAt({ width: 'narrow', height: 'tall' })).toBe(1);
    expect(columnsAt({ width: 'medium', height: 'medium' })).toBe(2);
    expect(columnsAt({ width: 'wide', height: 'medium' })).toBe(3);
  });

  test('a short box keeps one rank, which is what a pit wall strip is', () => {
    expect(keepsSecondaryRanks({ width: 'wide', height: 'short' })).toBe(false);
    expect(keepsSecondaryRanks({ width: 'wide', height: 'medium' })).toBe(true);
    expect(promotesLead({ width: 'medium', height: 'tall' })).toBe(true);
    expect(promotesLead({ width: 'medium', height: 'medium' })).toBe(false);
  });

  test('the real boxes the build produces land in bands that make sense', () => {
    // Not fixtures: these are the boxes companionGeometry and zoneFrame actually hand a module.
    expect(describeShape(shapeOf({ width: 802, height: 336 }))).toBe('wide/medium'); // companion page
    expect(describeShape(shapeOf({ width: 432, height: 706 }))).toBe('medium/tall'); // companion portrait
    expect(describeShape(shapeOf({ width: 607, height: 158 }))).toBe('wide/short'); // pit wall race zone
    expect(describeShape(shapeOf({ width: 1007, height: 211 }))).toBe('wide/medium'); // the wide zone
    expect(describeShape(shapeOf({ width: 455, height: 284 }))).toBe('medium/medium'); // tower zone
  });

  test('a band boundary is a threshold, and the bands are ordered', () => {
    expect(widthBandOf(319)).toBe('narrow');
    expect(widthBandOf(320)).toBe('medium');
    expect(widthBandOf(519)).toBe('medium');
    expect(widthBandOf(520)).toBe('wide');
    expect(heightBandOf(199)).toBe('short');
    expect(heightBandOf(200)).toBe('medium');
    expect(heightBandOf(399)).toBe('medium');
    expect(heightBandOf(400)).toBe('tall');
  });
});

describe('every page fits every shape the catalogue draws it at', () => {
  for (const name of ARCHETYPES) {
    const size = SHAPE_ARCHETYPES[name];
    test(`${name} ${size.width} by ${size.height}`, () => {
      const frame = rect(0, 0, size.width, size.height);
      for (const module of MODULES) {
        const items = module.build({ frame, density: 'zone', prefix: `${module.id}.` });

        // A page that draws nothing at a shape is a page that breaks a face at that shape.
        expect({ module: module.id, shape: name, drew: items.length > 0 }).toMatchObject({ drew: true });

        for (const item of items.flatMap((i) => [...walkItems([i])])) {
          if (item.kind === 'layer') continue;
          const r = item.rect;
          // A WPF line box is taller than its ink at both ends: a tenth of the size above the line
          // it is given, about a fifth below the baseline. Both tails are transparent.
          const below = item.kind === 'text' ? Math.ceil(0.25 * item.fontSize) + 2 : 1;
          const above = item.kind === 'text' ? Math.ceil(0.1 * item.fontSize) + 2 : 1;
          const inside =
            r.left >= frame.left - 1 &&
            r.top >= frame.top - above &&
            r.left + r.width <= frame.left + frame.width + 1 &&
            r.top + r.height <= frame.top + frame.height + below;
          expect({ module: module.id, shape: name, item: item.name, rect: r, inside }).toMatchObject({ inside: true });
        }
      }
    });
  }

  test('and draws no glyph that overruns its cell at any of them', () => {
    for (const name of ARCHETYPES) {
      const size = SHAPE_ARCHETYPES[name];
      for (const module of MODULES) {
        const items = module.build({ frame: rect(0, 0, size.width, size.height), density: 'zone', prefix: `${module.id}.` });
        for (const item of items.flatMap((i) => [...walkItems([i])])) {
          if (item.kind !== 'text') continue;
          expect({ module: module.id, shape: name, overruns: cellOverruns(item as TextItem) }).toMatchObject({ overruns: [] });
        }
      }
    }
  });
});

describe('shedding comes before shrinking', () => {
  // The mechanism, not a module: a box too short for everything drops the least important field
  // rather than making the most important one smaller. Under the old order the ladder ran first,
  // so a box one pixel short shrank every value in it -- including the one the page exists for --
  // while keeping a field nobody would miss.
  const sizesOf = (items: readonly { kind: string }[]): number[] =>
    items.filter((i): i is { kind: 'text'; fontSize: number } & typeof i => i.kind === 'text').map((i) => (i as unknown as TextItem).fontSize);

  test('a page in a short box keeps its lead value at full size', () => {
    const lapTimes = MODULES.find((m) => m.id === 'lapTimes')!;
    const roomy = lapTimes.build({ frame: rect(0, 0, 600, 280), density: 'zone', prefix: 'a.' });
    const tight = lapTimes.build({ frame: rect(0, 0, 600, 120), density: 'zone', prefix: 'b.' });

    const biggest = (items: readonly unknown[]): number => Math.max(...sizesOf(items as never), 0);
    // The tight box holds fewer things...
    expect(tight.length).toBeLessThan(roomy.length);
    // ...and what it does hold is drawn at the size the roomy box drew it.
    expect({ roomy: biggest(roomy), tight: biggest(tight) }).toMatchObject({ tight: biggest(roomy) });
  });
});
