/**
 * The tyre drawing, measured against the canvas's own SVG.
 *
 * It answers to rule 18 rather than to rule 17: it is one picture cut from whatever rectangle it is
 * given, at a ratio of its own, and dropped rather than shrunk past the width at which three tread
 * columns stop reading as three tread columns. The canvas draws it at 34, 37, 42, 72, 74 and 84 px
 * wide and every one of those is the same picture, so what is pinned below is the picture in
 * fractions of its box and the 74 by 117 drawing in pixels, which is the one written out in full.
 *
 * Nothing leaving the frame is checked here as well as in `secondScreens.test.ts`, because that
 * suite measures the boxes the build happens to produce and a drawing has to answer any rectangle at
 * all, including the ones no screen draws.
 */
import { describe, expect, test } from 'bun:test';
import type { BindingTarget, Bindings, Item, LinearGaugeItem, RectangleItem } from '../src/generator.ts';
import { ncalc } from '../src/generator.ts';
import { contains, rect, right, type Rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import type { Density } from '../src/second/density.ts';
import { ARCHETYPES, SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { GLYPH, GLYPH_MIN_WIDTH, GLYPH_SHARE, tyreGlyph, tyreGlyphSize } from '../src/second/tyreGlyph.ts';
import { CORNERS, tyreChangeScheduled, tyreWear, type Corner } from '../src/second/values.ts';
import { WEAR_CAUTION, temperatureColour } from '../src/second/wheel.ts';
import { TRANSPARENT, ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';

const tyres = MODULES.find((m) => m.id === 'tyres')!;

/** The parts a drawing is made of, which is what has to stay inside the box it was cut to. */
const PARTS = ['body', 'track1', 'tread1', 'track2', 'tread2', 'track3', 'tread3', 'groove1', 'groove2', 'change', 'change.tick'] as const;

/** Every item of a drawing, flattened, with the layers dropped: a layer has no rect of its own. */
const drawn = (items: readonly Item[]): Exclude<Item, { kind: 'layer' }>[] =>
  items.flatMap((item) => [...walkItems([item])]).filter((item): item is Exclude<Item, { kind: 'layer' }> => item.kind !== 'layer');

/** What an item binds to a target, as the expression text; the model allows a bare string or a `Formula`. */
const formulaOf = (item: { bindings?: Bindings } | undefined, target: BindingTarget): string => {
  const binding = item?.bindings?.[target];
  if (binding === undefined || !('formula' in binding)) return '';
  return typeof binding.formula === 'string' ? binding.formula : binding.formula.expression;
};

const glyphIn = (box: Rect, corner: Corner = 'FrontLeft', badge: 'left' | 'right' = 'right'): Exclude<Item, { kind: 'layer' }>[] =>
  drawn(tyreGlyph('t', box, corner, { fillBind: temperatureColour(corner), sample: 79, badge }));

const named = (items: readonly Exclude<Item, { kind: 'layer' }>[], name: string): Exclude<Item, { kind: 'layer' }> => {
  const item = items.find((i) => i.name === name);
  if (!item) throw new Error(`no item named ${name}`);
  return item;
};

/** Everything a tyres page draws in a box. */
const tyresIn = (box: { width: number; height: number }, density: Density = 'zone'): Exclude<Item, { kind: 'layer' }>[] =>
  drawn(tyres.build({ frame: rect(0, 0, box.width, box.height), density, prefix: 'p.' }));

/** A rect grown to whole pixels, for the one part of a drawing that is not on the grid. */
const outward = (r: Rect): Rect => rect(Math.floor(r.left), Math.floor(r.top), Math.ceil(r.left + r.width) - Math.floor(r.left), Math.ceil(r.top + r.height) - Math.floor(r.top));

describe('the tyre is cut from its box', () => {
  for (const archetype of ARCHETYPES) {
    test(`at ${archetype} no part of a corner's drawing leaves it`, () => {
      const items = tyresIn(SHAPE_ARCHETYPES[archetype]);
      for (const corner of CORNERS) {
        const body = items.find((item) => item.name === `p.${corner}.body`);
        expect({ archetype, corner, drew: body !== undefined }).toMatchObject({ drew: true });
        for (const part of PARTS.slice(1)) {
          const item = items.find((i) => i.name === `p.${corner}.${part}`);
          if (!item) continue; // a short drawing has two grooves rather than three
          // The tick is an image stretched to its box, which `assetBox` may place a fraction of a
          // pixel off the badge it fills; every other part is a rect on the grid.
          const inside = contains(body!.rect, item.kind === 'image' ? outward(item.rect) : item.rect);
          expect({ archetype, corner, part, rect: item.rect, body: body!.rect, inside }).toMatchObject({ inside: true });
        }
      }
    });
  }

  test('and keeps the canvas ratio whatever the box it is cut from', () => {
    for (const frame of [rect(0, 0, 600, 280), rect(0, 0, 430, 300), rect(0, 0, 274, 300), rect(0, 0, 360, 470), rect(0, 0, 1007, 211)]) {
      const size = tyreGlyphSize(frame);
      expect({ frame, ratio: size.width / size.height, held: Math.abs(size.width / size.height - GLYPH.aspect) < 0.02 }).toMatchObject({ held: true });
      expect({ frame, within: size.width <= GLYPH_SHARE * frame.width && size.height <= frame.height }).toMatchObject({ within: true });
    }
    // And the canvas's own drawings are that one ratio, within the pixel they disagree over: the
    // artboards write 37 by 58, 42 by 66, 72 by 113, 74 by 117 and 84 by 132 for the same object,
    // which is 0.6379 at the pit wall and 0.6325 at the 1280 by 480 face. The catalogue's 72 by 113
    // is the one the proportion is taken from, so 74 is drawn 116 tall rather than 117.
    for (const [width, height] of [
      [37, 58],
      [42, 66],
      [72, 113],
      [74, 117],
      [84, 132],
    ]) {
      expect({ width, off: Math.abs(Math.round(width! / GLYPH.aspect) - height!) <= 1 }).toEqual({ width, off: true });
    }
  });

  test('and a cell with no room for one is given none rather than three stripes', () => {
    expect(tyreGlyphSize({ width: 1000, height: 1000 }, GLYPH_MIN_WIDTH - 1)).toEqual({ width: 0, height: 0 });
    expect(tyresIn({ width: 120, height: 300 }).filter((item) => item.name.endsWith('.body'))).toEqual([]);
  });
});

describe('the drawing is the canvas drawing', () => {
  // dash1280x480-14, -16 and -21 write it out in pixels: the outline, the three tread bands on their
  // tracks, the grooves across them and the badge at the top corner.
  const items = glyphIn(rect(0, 0, 74, 117));
  const boxOf = (name: string): Rect => named(items, `t.${name}`).rect;
  const radiusOf = (name: string): number | undefined => {
    const radius = (named(items, `t.${name}`) as RectangleItem).border?.radius;
    return typeof radius === 'number' ? Number(radius.toFixed(2)) : undefined;
  };

  test('the body is the rounded casing, filled block.well and outlined text.dim', () => {
    const body = named(items, 't.body') as RectangleItem;
    expect(body.backgroundColor).toBe(ds.purpose.block.well);
    expect({ colour: body.border?.color, width: body.border?.top }).toEqual({ colour: ds.purpose.illustration.dim, width: 3 });
    expect(radiusOf('body')).toBe(14.8);
  });

  test('three tread columns on their own tracks, the shoulders round and the crown square', () => {
    expect([1, 2, 3].map((i) => boxOf(`track${i}`))).toEqual([rect(10, 8, 15, 101), rect(29, 8, 15, 101), rect(49, 8, 15, 101)]);
    expect([1, 2, 3].map((i) => radiusOf(`track${i}`))).toEqual([6.66, 1.2, 6.66]);
    for (const i of [1, 2, 3]) {
      const track = named(items, `t.track${i}`) as RectangleItem;
      const gauge = named(items, `t.tread${i}`) as LinearGaugeItem;
      expect(track.backgroundColor).toBe(ds.color.surface.raised);
      expect({ i, rect: gauge.rect, orientation: gauge.orientation, alignment: gauge.alignment }).toEqual({ i, rect: track.rect, orientation: 'vertical', alignment: 'start' });
      // The gauge is the fill alone. A rounded track behind it is the only rounded end the format
      // has, so the gauge's own background has to let that track through.
      expect(gauge.backgroundColor).toBe(TRANSPARENT);
    }
  });

  test('and the grooves cross all three of them, at the quarters of the tread', () => {
    // Measured from where the columns really landed: the canvas's 9.62 and 54.76 are the rounded
    // columns' own left edge and span, not the unrounded band they were cut from.
    const grooves = [1, 2, 3].map((i) => boxOf(`groove${i}`));
    expect(grooves.map((g) => g.top)).toEqual([32, 57, 82]);
    for (const groove of grooves) {
      expect({ left: groove.left, right: right(groove), height: groove.height }).toEqual({ left: boxOf('track1').left, right: right(boxOf('track3')), height: 3 });
    }
    expect((named(items, 't.groove1') as RectangleItem).backgroundColor).toBe(ds.purpose.block.well);
  });

  test('and a short drawing takes two of them, because a quarter of 58 px is a stripe', () => {
    const short = glyphIn(rect(0, 0, 37, 58));
    expect(short.filter((item) => item.name.startsWith('t.groove')).map((item) => item.rect.top)).toEqual([20, 37]);
  });

  test('the change badge sits in the drawing at the corner the readings are on', () => {
    expect(boxOf('change')).toEqual(rect(46, 3, 25, 25));
    expect((named(items, 't.change') as RectangleItem).backgroundColor).toBe(ds.color.text.secondary);
    expect(radiusOf('change')).toBe(1.5);
    expect(named(items, 't.change.tick').kind).toBe('image');
    // Both halves of the badge are one reading, so neither can be drawn without the other.
    for (const part of ['t.change', 't.change.tick']) expect(formulaOf(named(items, part), 'Visible')).toBe(tyreChangeScheduled('FrontLeft'));
    expect(named(glyphIn(rect(0, 0, 74, 117), 'FrontLeft', 'left'), 't.change').rect).toEqual(rect(3, 3, 25, 25));
  });
});

describe('what a tread column reads', () => {
  const items = glyphIn(rect(0, 0, 74, 117), 'FrontRight');

  test('every column fills from the bottom with the tread the corner has left', () => {
    for (const i of [1, 2, 3]) {
      const gauge = named(items, `t.tread${i}`) as LinearGaugeItem;
      expect({ i, value: formulaOf(gauge, 'Value'), max: gauge.maximum, from: gauge.alignment }).toEqual({ i, value: tyreWear('FrontRight'), max: 100, from: 'start' });
    }
  });

  test('and takes the tyre colour the cell hands it, temperature before wear', () => {
    // Read off the page rather than off a call written here, since the colour is the cell's to
    // decide. The nesting is the one the canvas's sample shows: a hot section is red although its
    // tread is low, and an in-range one with a low tread is amber, so temperature wins.
    const page = tyresIn(SHAPE_ARCHETYPES.grid);
    const wear = tyreWear('FrontRight');
    const amber = ncalc.iff(ncalc.lt(wear, ncalc.num(WEAR_CAUTION)), ncalc.str(ds.color.caution.primary), ncalc.str(ds.color.text.primary));
    const colour = formulaOf(named(page, 'p.FrontRight.tread1'), 'GaugeColor');
    expect(colour).toBe(temperatureColour('FrontRight', amber));
    expect(colour.indexOf(ds.purpose.tyre.hot)).toBeLessThan(colour.indexOf(ds.color.caution.primary));
  });

  test('and the three of them share one figure, which is what the sections are waiting on', () => {
    // The canvas fills each column from its own third of the tyre, and `tyreWearMin` already spells
    // those three sections. No committed trace carries them, so a package that bound them would
    // draw three empty columns in every recorded video and in the preview renderer.
    expect(new Set([1, 2, 3].map((i) => formulaOf(named(items, `t.tread${i}`), 'Value'))).size).toBe(1);
  });
});
