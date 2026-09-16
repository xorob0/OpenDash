/**
 * The anatomy of the flag band: the weight its name is set in, the border every coloured state
 * carries, and the flash that must not uncover the page it covers.
 *
 * A flag takes band D over for as long as it is out, because an alert outranks fuel, so whatever
 * the band does it has to leave nothing of the page showing. That is one rule with three ways of
 * being broken, and each of them had been broken: a name in the label weight rather than the
 * artboards' 700, a fill with no border where every state is drawn with one, and a yellow that
 * blinked the whole layer away twice a second.
 */
import { describe, expect, test } from 'bun:test';
import { BLACK_FLAG_BORDER, FLAG_BLINK_MS, FLAG_NAME_WEIGHT } from '../src/components/flagStrip.ts';
import { contains } from '../src/design/geometry.ts';
import type { Item, LayerItem, RectangleItem, TextItem } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, faceItems, type ZoneLayout } from '../src/zones/index.ts';

/** The four states the band fills with one colour and names; the chequer and the black differ. */
const COLOURED = ['yellow', 'blue', 'white', 'green'] as const;

const flagLayers = (face: ZoneLayout): Map<string, LayerItem> =>
  new Map(faceItems(face).filter((i): i is LayerItem => i.kind === 'layer' && i.name.startsWith('flag.')).map((l) => [l.name.slice('flag.'.length), l]));

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
        expect({ id, drawn: names.length }).toEqual({ id, drawn: 1 });
        const name = names[0]!;
        expect({ id, font: name.font, weight: name.fontWeight }).toEqual({ id, font: ds.font.label, weight: FLAG_NAME_WEIGHT });
      }
      expect(labelsOf(layerOf(face, 'chequered').children)).toEqual([]);
    });
  }
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

describe('the yellow flash', () => {
  for (const face of ZONE_FACES) {
    test(`${face.folder} is opaque in both phases, the page never showing through`, () => {
      // The flash was BlinkEnabled on the whole layer, so half of every cycle drew no band at all
      // and band D's page read through the flag that had taken it over. What blinks is one opaque
      // band over another, at the rate design/tokens.json states.
      const layer = layerOf(face, 'yellow');
      expect(layer.blink).toBeUndefined();

      const blinking = layer.children.filter((c) => c.blink?.enabled);
      expect(blinking.map((c) => c.name)).toEqual(['flag.yellow.flash']);

      const flash = blinking[0]!;
      if (flash.kind !== 'rect') throw new Error('the flash is a band');
      expect(flash.blink).toEqual({ enabled: true, delayMs: FLAG_BLINK_MS });
      expect(flash.backgroundColor).toBe(ds.color.surface.base);
      expect(contains(face.zones.band, flash.rect)).toBe(true);
      // Last, so that it is drawn over the fill it alternates with rather than under it.
      expect(layer.children[layer.children.length - 1]?.name).toBe(flash.name);

      // The ground under it holds the whole band whichever phase the flash is in.
      const ground = bandOf(face, 'yellow');
      expect(ground.blink).toBeUndefined();
      expect({ rect: ground.rect, colour: ground.backgroundColor }).toEqual({ rect: face.zones.band, colour: ds.purpose.flag.yellow });
    });
  }

  test('and no other state flashes at all', () => {
    for (const face of ZONE_FACES) {
      for (const id of ['blue', 'white', 'green', 'black', 'chequered']) {
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
