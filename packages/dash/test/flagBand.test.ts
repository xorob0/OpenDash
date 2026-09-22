/**
/**
 * The flag band: the phase of its chequer, the weight its name is set in, the border every coloured
 * state carries, and the flash that must not uncover the page beneath it.
 *
 * A flag takes band D over for as long as it is out, because an alert outranks fuel, so whatever
 * the band does it has to leave nothing of the page showing. Every artboard fills the chequered
 * state with `repeating-conic-gradient(#F5F7FA 0 25%, #0A0B0D 0 50%)` over a tile the height of the
 * band, which sweeps clockwise from twelve o'clock, so the band opens dark at its left edge and the
 * first light square begins one square in; the round faces draw the same board as marks around the
 * rim, and whatever the band does at its left edge the ring does at twelve o'clock. The coloured
 * states carry the artboards' 3 px border in their own fill, their names are set in the artboards'
 * 700 rather than the label weight, and the yellow flashes an opaque band over its fill rather than
 * blinking the whole layer away.
 */
import { describe, expect, test } from 'bun:test';
import { BLACK_FLAG_BORDER, BLUE_FLAG_ID, FLAG_BLINK_MS, FLAG_NAME_WEIGHT } from '../src/components/flagStrip.ts';
import { chequerCount, chequerStep } from '../src/components/flagRing.ts';
import { BLUE_FLAG_DETAILS } from '../src/contract.ts';
import { FLAG_CATALOGUE } from '../src/flags.ts';
import { contains, rect } from '../src/design/geometry.ts';
import type { Item, LayerItem, RectangleItem, TextItem } from '../src/generator.ts';
import { hero } from '../src/hero/hero.ts';
import { layout480round, layout800round, type Layout } from '../src/layouts/index.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, faceItems, type ZoneLayout } from '../src/zones/index.ts';

/** The two colours every artboard quotes for the chequer. */
const CHEQUER = '#F5F7FA';
const GROUND = '#0A0B0D';

/** The tile each artboard quotes, which is the band's height. */
const TILE: Record<string, number> = {
  OpenDash: 60,
  'OpenDash 1280x480': 60,
  'OpenDash 1280x400': 54,
  'OpenDash 850x480': 60,
  'OpenDash 800x480': 60,
  'OpenDash 1280x720': 60,
  'OpenDash 800x286': 58,
  'OpenDash 600x686': 56,
};

/**
 * The band format's states, which the face now draws inside a group of their own: the two formats
 * `OpenDash.<Face>FlagFormat` chooses between are both built and one is shown, so the six states are
 * one level further down than they were. Everything this file asserts is about the band format and
 * is found by its `flag.` prefix; the full-screen block is `flagFull.` and is held in
 * `flagFormat.test.ts`.
 */
const bandFormatItems = (items: readonly Item[]): Item[] => [...walkItems(items)];

const chequerChildren = (items: readonly Item[]): RectangleItem[] => {
  const layer = bandFormatItems(items).find((i): i is LayerItem => i.kind === 'layer' && i.name === 'flag.chequered');
  if (!layer) throw new Error('no chequered flag');
  return layer.children.map((c) => {
    if (c.kind !== 'rect') throw new Error(`${c.name} is not a rect`);
    return c;
  });
};

/** How far into a tile the first light square sits, as a fraction of the tile. */
const bandPhase = (face: ZoneLayout): number => {
  const { band } = face.zones;
  const top = chequerChildren(faceItems(face)).slice(1).filter((c) => c.rect.top === band.top);
  return (Math.min(...top.map((c) => c.rect.left)) - band.left) / band.height;
};

/** The face a round layout hangs its ring on, which is what the count and the step are read from. */
const ringFace = (layout: Layout) => {
  const { flags } = layout.hero;
  if (flags.kind !== 'flagRing') throw new Error(`${layout.folder} draws no flag ring`);
  return flags.face;
};

/** The same fraction on a rim, where a tile is one check and the gap after it. */
const ringPhase = (layout: Layout): number => (chequerChildren(hero(layout.hero))[0]!.rotation ?? 0) / chequerStep(ringFace(layout));

describe('the chequered band', () => {
  for (const face of ZONE_FACES) {
    describe(face.folder, () => {
      const { band } = face.zones;
      const check = band.height / 2;
      const children = chequerChildren(faceItems(face));
      const ground = children[0]!;
      const squares = children.slice(1);

      test('lays the ground over the whole band and tiles it with light squares half the band high', () => {
        expect(ds.color.surface.base).toBe(GROUND);
        expect(ds.purpose.flag.chequer).toBe(CHEQUER);
        expect(TILE[face.folder]).toBe(band.height);
        expect(ground.name).toBe('flag.chequered.band');
        expect(ground.rect).toEqual(band);
        expect(ground.backgroundColor).toBe(GROUND);
        expect(squares.length).toBe(Math.ceil(band.width / check));
        for (const square of squares) {
          expect({ name: square.name, color: square.backgroundColor }).toEqual({ name: square.name, color: CHEQUER });
          expect(square.rect.height).toBe(check);
          expect(square.rect.width).toBeLessThanOrEqual(check);
          expect({ name: square.name, inside: contains(band, square.rect) }).toEqual({ name: square.name, inside: true });
        }
      });

      test('opens on the ground: the top row starts one square in, and the bottom row is the one that starts at the left edge', () => {
        const columnOf = (square: RectangleItem): number => (square.rect.left - band.left) / check;
        const top = squares.filter((s) => s.rect.top === band.top);
        const bottom = squares.filter((s) => s.rect.top === band.top + check);
        expect(top.map(columnOf)).not.toContain(0);
        expect(top.every((s) => columnOf(s) % 2 === 1)).toBe(true);
        expect(bottom.every((s) => columnOf(s) % 2 === 0)).toBe(true);
        expect(top.find((s) => s.rect.left === band.left + check)?.rect).toEqual(rect(band.left + check, band.top, check, check));
        expect(bottom.find((s) => s.rect.left === band.left)?.rect).toEqual(rect(band.left, band.top + check, check, check));
        expect(squares.filter((s) => s.rect.top !== band.top && s.rect.top !== band.top + check)).toEqual([]);
      });

      test('carries no label', () => {
        expect([...walkItems(children)].filter((i) => i.kind === 'text')).toEqual([]);
      });
    });
  }
});

/** The four states the band fills with one colour and names; the chequer and the black differ. */
const COLOURED = ['yellow', 'blue', 'white', 'green'] as const;

const flagLayers = (face: ZoneLayout): Map<string, LayerItem> =>
  new Map(
    bandFormatItems(faceItems(face))
      .filter((i): i is LayerItem => i.kind === 'layer' && i.name.startsWith('flag.') && i.name.split('.').length === 2)
      .map((l) => [l.name.slice('flag.'.length), l]),
  );

const layerOf = (face: ZoneLayout, id: string): LayerItem => {
  const layer = flagLayers(face).get(id);
  if (!layer) throw new Error(`no ${id} flag on ${face.folder}`);
  return layer;
};

const labelsOf = (items: readonly Item[]): TextItem[] => [...walkItems(items)].filter((i): i is TextItem => i.kind === 'text');

const bandOf = (face: ZoneLayout, id: string): RectangleItem => {
  const fill = layerOf(face, id).children.find((c): c is RectangleItem => c.kind === 'rect' && c.name === `flag.${id}.band`);
  if (!fill) throw new Error(`no band under the ${id} flag`);
  return fill;
};

describe('the flag name', () => {
  for (const face of ZONE_FACES) {
    test(`${face.folder} writes every name in the label family at ${FLAG_NAME_WEIGHT}`, () => {
      // 700 is what every FaceVariants sheet sets the band's name in, against the 500 of every
      // other label; Barlow Bold is shipped and measured so that the fit tests measure the face
      // the band is drawn in rather than the one it used to be.
      for (const id of [...COLOURED, 'black']) {
        const names = labelsOf(layerOf(face, id).children);
        // One run each, and three on the blue: it is the one band that can say more than its own
        // name, one run per value of BlueFlagDetail with one of the three visible at a time. They
        // are the same line box in the same face, which is what this test is about.
        expect({ id, drawn: names.length }).toEqual({ id, drawn: id === BLUE_FLAG_ID ? BLUE_FLAG_DETAILS.length : 1 });
        for (const name of names) expect({ id, font: name.font, weight: name.fontWeight }).toEqual({ id, font: ds.font.label, weight: FLAG_NAME_WEIGHT });
      }
      expect(labelsOf(layerOf(face, 'chequered').children)).toEqual([]);
    });
  }
});

describe('the chequered ring', () => {
  for (const layout of [layout480round, layout800round]) {
    test(`${layout.folder} draws ${chequerCount(ringFace(layout))} checks in the band's phase, none of them at twelve o'clock`, () => {
      const checks = chequerChildren(hero(layout.hero));
      const step = chequerStep(ringFace(layout));
      expect(checks).toHaveLength(chequerCount(ringFace(layout)));
      checks.forEach((check, k) => {
        expect({ name: check.name, color: check.backgroundColor }).toEqual({ name: check.name, color: CHEQUER });
        expect(check.rotation).toBe((k + 0.5) * step);
      });
      expect(checks.map((c) => (c.rotation ?? 0) % 360)).not.toContain(0);
    });
  }

  test('the ring and the band open on the ground by the same fraction of a tile', () => {
    for (const face of ZONE_FACES) expect({ face: face.folder, phase: bandPhase(face) }).toEqual({ face: face.folder, phase: 0.5 });
    for (const layout of [layout480round, layout800round]) expect({ face: layout.folder, phase: ringPhase(layout) }).toEqual({ face: layout.folder, phase: 0.5 });
  });
});

describe('the coloured bands', () => {
  for (const face of ZONE_FACES) {
    test(`${face.folder} fills band D and borders it ${BLACK_FLAG_BORDER} px in its own fill`, () => {
      // The artboards draw the border on every state and leave it transparent over the fill, which
      // over that fill is these three pixels of it; what the code cannot do is omit the border on
      // four states and carry it on one, which is what it did.
      for (const id of COLOURED) {
        const fill = bandOf(face, id);
        const colour = ds.purpose.flag[id];
        expect({ id, rect: fill.rect, colour: fill.backgroundColor }).toEqual({ id, rect: face.zones.band, colour });
        expect({ id, border: fill.border }).toEqual({
          id,
          border: { color: colour, top: BLACK_FLAG_BORDER, bottom: BLACK_FLAG_BORDER, left: BLACK_FLAG_BORDER, right: BLACK_FLAG_BORDER },
        });
      }
    });
  }
});

describe('the waved yellow flash', () => {
  for (const face of ZONE_FACES) {
    test(`${face.folder} is opaque in both phases, the page never showing through`, () => {
      // The flash was BlinkEnabled on the whole layer, so half of every cycle drew no band at all
      // and band D's page read through the flag that had taken it over. What blinks is one opaque
      // band over another, at the rate design/tokens.json states.
      //
      // It is the waved yellow that flashes and not the standing one, which is the rule the box
      // keeps under "waving is blinking". The band used to flash on SimHub's `Flag_Yellow`, which
      // folds `yellow`, `yellowWaving`, `caution` and `cautionWaving` into one, so a standing yellow
      // strobed for as long as it was out; band D reads the bits now and can tell them apart.
      const layer = layerOf(face, 'yellowWaving');
      expect(layer.blink).toBeUndefined();

      const blinking = layer.children.filter((c) => c.blink?.enabled);
      expect(blinking.map((c) => c.name)).toEqual(['flag.yellowWaving.flash']);

      const flash = blinking[0]!;
      if (flash.kind !== 'rect') throw new Error('the flash is a band');
      expect(flash.blink).toEqual({ enabled: true, delayMs: FLAG_BLINK_MS });
      expect(flash.backgroundColor).toBe(ds.color.surface.base);
      expect(contains(face.zones.band, flash.rect)).toBe(true);
      // Last, so that it is drawn over the fill it alternates with rather than under it.
      expect(layer.children[layer.children.length - 1]?.name).toBe(flash.name);

      // The ground under it holds the whole band whichever phase the flash is in.
      const ground = bandOf(face, 'yellowWaving');
      expect(ground.blink).toBeUndefined();
      expect({ rect: ground.rect, colour: ground.backgroundColor }).toEqual({ rect: face.zones.band, colour: ds.purpose.flag.yellow });
    });
  }

  test('and no other state flashes at all', () => {
    for (const face of ZONE_FACES) {
      for (const id of FLAG_CATALOGUE.map((c) => c.id).filter((id) => id !== 'yellowWaving')) {
        const layer = layerOf(face, id);
        expect({ face: face.folder, id, blinking: [...walkItems([layer])].filter((i) => i.blink?.enabled).map((i) => i.name) }).toEqual({
          face: face.folder,
          id,
          blinking: [],
        });
      }
    }
  });
});
