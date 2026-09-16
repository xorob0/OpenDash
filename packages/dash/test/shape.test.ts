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
  promotesLead,
  SHAPE_ARCHETYPES,
  shapeOf,
  widthBandOf,
} from '../src/second/shape.ts';
import { densityOf, nextOnRamp, rampOf } from '../src/second/density.ts';
import { cellOverruns } from './monoGlyphs.ts';
import type { Item, TextItem } from '../src/generator.ts';

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

  test('a tall box promotes its lead value and a medium one does not', () => {
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
    // ...and what it does hold is drawn at the size the ramp names it, never below. This used to
    // compare the two boxes to each other and cannot any more: under rule 20 the roomy box grows
    // past its ramp size, which is the other half of the same rule rather than a violation of this
    // one. What matters is still that the tight box shed instead of shrinking.
    expect(biggest(tight)).toBe(densityOf('zone').big);
    expect(biggest(roomy)).toBeGreaterThanOrEqual(biggest(tight));
  });
});

/**
 * Rule 20: a rank fills the box it is given, and meets one of three edges doing it.
 *
 * The mechanism rather than a module. What every module does at every real box is
 * `secondScreens.test.ts` and `textFit.test.ts`, which measure the drawn items; these are the four
 * promises the growing itself makes.
 */
describe('a rank fills the box it is given', () => {
  const lapTimes = (): (typeof MODULES)[number] => MODULES.find((m) => m.id === 'lapTimes')!;
  const valuesOf = (items: readonly Item[]): TextItem[] => items.flatMap((i) => [...walkItems([i])]).filter((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.value'));

  test('a zone with height to spare draws its values larger than the ramp names them', () => {
    // The base face's zone B: 274 x 328 of face, 254 x 292 of body once the title bar is off it.
    const grown = valuesOf(lapTimes().build({ frame: rect(0, 0, 254, 292), density: 'compact', prefix: 'b.' }));
    const lead = Math.max(...grown.map((i) => i.fontSize));
    expect(lead).toBeGreaterThan(densityOf('compact').big);
    // One column: two lap times fit side by side at 34 px and do not at 46, so growing wraps them.
    expect(new Set(grown.map((i) => i.rect.left)).size).toBe(1);
  });

  test('and never past the next size up its ramp', () => {
    for (const density of ['compact', 'zone', 'companion'] as const) {
      // A box far larger than any zone, so neither the height nor the width is what stops it.
      const items = valuesOf(lapTimes().build({ frame: rect(0, 0, 4000, 4000), density, prefix: 'b.' }));
      const lead = Math.max(...items.map((i) => i.fontSize));
      const big = densityOf(density).big;
      // It grew, and it stopped at the ramp. Not *on* the next size: the ceiling is the smallest
      // step in the stack, so lap times over a 34 px delta stop when the delta reaches 46 and the
      // times are at 62 rather than 64. A rank grows by one factor or its sizes stop meaning what
      // they meant.
      expect({ density, grew: lead > big, past: lead > nextOnRamp(big, density) }).toEqual({ density, grew: true, past: false });
    }
  });

  test('and keeps the order of its sizes while it grows', () => {
    const sizes = (height: number): number[] => {
      const items = valuesOf(lapTimes().build({ frame: rect(0, 0, 600, height), density: 'zone', prefix: 'b.' }));
      return items.map((i) => i.fontSize);
    };
    // The six values are three lap times over three smaller ones at every height that holds them.
    for (const height of [280, 340, 400, 470]) {
      const drawn = sizes(height);
      if (drawn.length < 6) continue;
      expect({ height, ordered: Math.min(...drawn.slice(0, 3)) > Math.max(...drawn.slice(3)) }).toEqual({ height, ordered: true });
    }
  });

  test('and a stack it cannot grow whole it does not grow at all', () => {
    // Sectors is three drawn sector deltas over a rank of lap times. The drawing cannot grow, so
    // neither may the rank: times grown to the size of the sectors above them are a page with no
    // hierarchy left.
    const sectors = MODULES.find((m) => m.id === 'sectors')!;
    const items = valuesOf(sectors.build({ frame: rect(0, 0, 737, 270), density: 'zone', prefix: 'b.' }));
    for (const item of items) expect({ name: item.name, onRamp: rampOf('zone').includes(item.fontSize) }).toEqual({ name: item.name, onRamp: true });
  });
});
