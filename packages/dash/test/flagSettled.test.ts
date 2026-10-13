/**
 * Where a flag settles once it has had band D for its few seconds, #731: one rectangle, in one place,
 * carrying the flag's name, over nothing a driver reads.
 *
 * It settled into a block at each end of the band until the author ruled against it. On the four house
 * faces with corner blocks those blocks covered the incidents and the track state at one end and the
 * DRS, push to pass and spotter lamps and both clocks at the other; on every other face, the AiM's and
 * the Porsche's at every size among them, they were sixteen pixels of colour with no word. With the
 * takeover that was the flag's colour in three places, and the readings under the blocks were lost for
 * as long as a caution ran.
 *
 * So, on every face of every theme, both arrangements of each:
 *
 * - The settled group draws one rectangle. Every condition in it lays its ground on the same rectangle
 *   and draws nothing outside it, so whichever is out, the flag is in one place.
 * - The rectangle is on the face and covers no reading. "A reading" is `faceInk.ts`'s rule, and the
 *   zones are read at every page their dashboards hold, so a rectangle that passes is clear of
 *   whatever page a driver has put there, not of the page the build opens on.
 * - Every condition writes a name there, and the name fits: the longest of the condition's names that
 *   fits the rectangle, measured in the Bold it is drawn in, so FULL COURSE YELLOW where the rectangle
 *   holds it and FCY where it does not. The chequer and the meatball are their own flags and write none.
 * - It is listed after every zone widget, so no zone's ground is painted over it.
 *
 * Asked of the faces as the build composes them, theme by theme, because a theme's `settings` pass
 * rewrites the built dashboard after the face is drawn. `composeTheme` builds a theme drawn in colours
 * of its own in a process started with `OPENDASH_THEME` naming it, which is the AiM's case, so each
 * theme's faces are the ones its own process draws.
 */
import { describe, expect, test } from 'bun:test';
import { composeTheme } from '../src/build.ts';
import { ALERT_BAND_BORDER } from '../src/components/alertBand.ts';
import { BLUE_FLAG_ID } from '../src/components/flagStrip.ts';
import { THEME_CATALOGUE } from '../src/contract.ts';
import { measureText } from '../src/design/advances.ts';
import { contains, rect } from '../src/design/geometry.ts';
import { ALERT_CATALOGUE, bandNames } from '../src/flags.ts';
import type { Item, LayerItem, Rect, RectangleItem, TextItem } from '../src/generator.ts';
import { walkItems } from '../src/walk.ts';
import { faceInk, overlapping } from './faceInk.ts';

const SETTLED = 'flagCorner';

/**
 * The faces whose settled flag covers a reading, each with the readings it may cover and why. Empty is
 * the rule; an entry here is a face on which no part of the drawing was free and the least of its
 * readings was given up, which the pull request for #731 puts to the author.
 */
const COVERS: Readonly<Record<string, { covers: RegExp; why: string }>> = {
  'OpenDash AiM 800x286': {
    covers: /^[a-zA-Z]+\.status$/,
    why: 'the nano foot is full from end to end on one page or another, and GPS: GOOD is a cue the LCD writes as it is rather than a reading',
  },
};

const isLayer = (item: Item): item is LayerItem => item.kind === 'layer';
const drawn = (items: readonly Item[]): Item[] => [...walkItems(items)].filter((i) => !isLayer(i));

/** The rectangle a condition lays its ground on, which is the settled flag's rectangle. */
const groundOf = (layer: LayerItem): RectangleItem => {
  const ground = layer.children.find((c): c is RectangleItem => c.kind === 'rect' && c.name === `${layer.name}.band`);
  if (!ground) throw new Error(`${layer.name} lays no ground`);
  return ground;
};

for (const theme of THEME_CATALOGUE) {
  describe(`the settled flag on the ${theme.id} theme's faces`, () => {
    const faces = composeTheme({ theme, version: '0.0.0-test', simHubVersion: '9.12.6' });

    test('is drawn on every face, on both arrangements', () => {
      expect(faces.length).toBe(theme.sizes.length);
      for (const { pkg } of faces) {
        const main = pkg.dashboards[0]!;
        const asked = main.screens.filter((s) => s.items.some((i) => i.name === SETTLED));
        expect({ face: main.name, screens: asked.length }).toEqual({ face: main.name, screens: 2 });
      }
    });

    for (const { pkg, width, height } of faces) {
      const main = pkg.dashboards[0]!;
      for (const screen of main.screens.filter((s) => s.items.some((i) => i.name === SETTLED))) {
        const at = `${main.name}, ${screen.name}`;
        const index = screen.items.findIndex((i) => i.name === SETTLED);
        const settled = screen.items[index] as LayerItem;
        const conditions = settled.children.filter(isLayer);
        const one: Rect = groundOf(conditions[0]!).rect;

        test(`${at}: every condition, in one rectangle`, () => {
          expect(conditions.map((c) => c.name.slice(SETTLED.length + 1))).toEqual(ALERT_CATALOGUE.map((c) => c.id));
          for (const layer of conditions) {
            const ground = groundOf(layer);
            expect({ at, id: layer.name, ground: ground.rect }).toEqual({ at, id: layer.name, ground: one });
            for (const item of drawn(layer.children)) {
              // A name's box is a WPF line box and may be taller than the rectangle, so it is held to
              // the rectangle's width; everything else is inside the rectangle outright.
              const inside = item.kind === 'text' ? item.rect.left === one.left && item.rect.width === one.width : contains(one, (item as RectangleItem).rect);
              expect({ at, item: item.name, inside }).toEqual({ at, item: item.name, inside: true });
            }
          }
        });

        test(`${at}: on the face, and over no reading`, () => {
          expect({ at, rect: one, onFace: contains(rect(0, 0, width, height), one) }).toEqual({ at, rect: one, onFace: true });
          const allowed = COVERS[main.name]?.covers;
          const covered = faceInk(screen, pkg.dashboards, { width, height })
            .filter((ink) => overlapping(ink.rect, one))
            .map((ink) => ink.name)
            .filter((name) => !(allowed?.test(name) ?? false));
          expect({ at, rect: one, covered: [...new Set(covered)] }).toEqual({ at, rect: one, covered: [] });
        });

        test(`${at}: a name for every condition that has one, the longest that fits`, () => {
          const room = one.width - 2 * ALERT_BAND_BORDER;
          for (const condition of ALERT_CATALOGUE) {
            const layer = conditions.find((c) => c.name === `${SETTLED}.${condition.id}`)!;
            const names = drawn(layer.children).filter((i): i is TextItem => i.kind === 'text');
            for (const name of names) {
              const width = measureText(name.fontWeight === 'Bold' ? 'BarlowBold' : 'BarlowMedium', name.widest ?? name.text, name.fontSize);
              expect({ at, item: name.name, text: name.widest ?? name.text, fits: width < room }).toEqual({ at, item: name.name, text: name.widest ?? name.text, fits: true });
            }
            const all = bandNames(condition.band);
            if (all.length === 0) {
              expect({ at, id: condition.id, names: names.map((n) => n.text) }).toEqual({ at, id: condition.id, names: [] });
              continue;
            }
            // The blue flag may write the car behind as well, which is its three runs over one line;
            // its plain run is the name, and the others carry it.
            const plain = names.filter((n) => condition.id !== BLUE_FLAG_ID || n.bindings?.Text === undefined);
            const longest = all.find((text) => measureText('BarlowBold', text, plain[0]?.fontSize ?? 15) < room);
            expect({ at, id: condition.id, names: plain.map((n) => n.text) }).toEqual({ at, id: condition.id, names: longest === undefined ? [] : [longest] });
            expect({ at, id: condition.id, named: longest !== undefined }).toEqual({ at, id: condition.id, named: true });
          }
        });

        test(`${at}: listed after every zone widget, so that no zone paints over it`, () => {
          const widgets = screen.items.map((item, i) => ({ item, i })).filter(({ item }) => item.kind === 'widget');
          expect({ at, after: widgets.filter(({ i }) => i > index).map(({ item }) => item.name) }).toEqual({ at, after: [] });
        });
      }
    }
  });
}
