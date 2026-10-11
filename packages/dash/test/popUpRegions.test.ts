/**
 * Every box of the pop-up family, on every face of every theme, inside the region it belongs to
 * (#1047).
 *
 * A pop-up is a function of the rectangle the face calls its hero, as a module is a function of its
 * zone, so it is held to that rectangle the way `secondScreens.test.ts` holds a module to its frame:
 * the face is built, and every item the family draws is found inside the region. The pop-ups and
 * the change notifications keep to the hero, which is zone A on the house face and the gear's tile
 * on the Porsche's and the AiM's; that is the sheet's "it never covers a slot", and it is what a box
 * of the sheet's 560 centred on a 380 px zone A broke, hiding the session name and the time left in
 * zone C while low fuel was flagged. The lap review is the one box of the family that is wider than
 * the hero on purpose, and the layout says so: it is clamped to the body, zones B, A and C, and
 * never covers the rev bar, the bar of settled values or band D.
 *
 * The faces are built from each theme's anatomy with the house's drawing, which is how the
 * conformance harness builds a test theme. A theme's own drawing that moves a member of the family
 * elsewhere, as the Porsche's change notification is drawn in zone C's panel, is checked by the
 * harness against its frame; what is checked here is the house's family over the theme's regions,
 * which is the stricter question, since it asks it of every theme's hero.
 *
 * **Each theme is measured in a process of its own.** The family's rectangles are the theme's
 * regions, but what is drawn inside them is measured, and a theme's overlay changes the face the
 * figures are set in: the Porsche's are Barlow at normal width and the AiM's are the seven-segment
 * face, both wider than the house's condensed figures. A fit taken in the default process is one
 * the theme's package never draws, which is how the Porsche's portrait review was once said to keep
 * both deltas that its own build sheds (#1047). So the default process checks the themes that draw
 * in its fonts and starts this file again once for each of the others, as `conformance.test.ts` does.
 */
import { afterAll, describe, expect, test } from 'bun:test';
import { LAP_REVIEW_PAD_X, LAP_REVIEW_PAD_X_NARROW } from '../src/components/lapReview.ts';
import type { Item, LayerItem, Rect } from '../src/generator.ts';
import { contains, overlaps } from '../src/design/geometry.ts';
import { optionalRegionRect, regionRect, zoneRect, type Regions } from '../src/themes/anatomy.ts';
import { buildThemeFace, drawsInThisProcess } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, faceItems, layoutWithoutRevBar, regionsWithoutRevBar, type ZoneLayout } from '../src/zones/index.ts';
import { GEAR_LEFT_THEME_ID, gearLeftTheme } from './fixtures/gearLeftTheme.ts';

// The test theme with the gear at the left edge is the case a centred box was most wrong on, so it
// is asked too, and taken out again once this file has run.
THEMES[GEAR_LEFT_THEME_ID] = gearLeftTheme;
afterAll(() => {
  delete THEMES[GEAR_LEFT_THEME_ID];
});

interface Face {
  name: string;
  theme: string;
  /** `1920x480`, the key the tables below are written in. */
  size: string;
  layout: ZoneLayout;
  regions: Regions;
  revBar: boolean;
  items: Item[];
}

/** The themes this process draws in their own fonts: the others are checked by a process of their own. */
const here = Object.keys(THEMES).filter(drawsInThisProcess);

/** Every face of every theme this process draws, in both arrangements. */
const faces: Face[] = here.flatMap((id) =>
  THEMES[id]!.anatomy.sizes.flatMap((size) => {
    const layout = ZONE_FACES.find((face) => face.width === size.width && face.height === size.height)!;
    const on = THEMES[id]!.anatomy.regions(layout);
    const off = regionsWithoutRevBar(on);
    const offLayout = layoutWithoutRevBar(layout);
    const key = `${size.width}x${size.height}`;
    return [
      { name: `${id} ${key}`, theme: id, size: key, layout, regions: on, revBar: true, items: faceItems(layout, { regions: on }) },
      { name: `${id} ${key}, rev bar off`, theme: id, size: key, layout: offLayout, regions: off, revBar: false, items: faceItems(offLayout, { regions: off, revBar: false }) },
    ];
  }),
);

/** The parts a member of the family draws, found by the prefix the face gives it. */
const partsOf = (items: Item[], prefix: string): Item[] =>
  items.flatMap((item) => [...walkItems([item])]).filter((item) => item.kind !== 'layer' && item.name.startsWith(prefix));

/** What no box of the family may cover, on any face: what carries a state of its own outside the body. */
const settled = (face: Face): [string, Rect | undefined][] => [
  ['the rev bar well', face.revBar ? regionRect(face.regions, 'revBarWell') : undefined],
  ['the bar', optionalRegionRect(face.regions, 'bar')],
  ['band D', regionRect(face.regions, 'band')],
];

describe('the pop-ups and the change notifications keep to the hero', () => {
  for (const face of faces) {
    test(`${face.name}`, () => {
      const hero = regionRect(face.regions, 'hero');
      const parts = [...partsOf(face.items, 'popUp.'), ...partsOf(face.items, 'notice.')];
      expect(parts.length).toBeGreaterThan(0);
      for (const item of parts) {
        if (item.kind === 'layer') continue;
        expect({ face: face.name, item: item.name, rect: item.rect, hero, inside: contains(hero, item.rect) }).toMatchObject({ inside: true });
      }
      // And so clear of the zones either side of the hero, which is the fault this file is for.
      const others = (['B', 'C'] as const).map((zone) => [zone, zoneRect(face.regions, zone)] as const);
      for (const item of parts) {
        if (item.kind === 'layer') continue;
        for (const [zone, rect] of others) {
          expect({ face: face.name, item: item.name, zone, covered: overlaps(rect, item.rect) }).toMatchObject({ covered: false });
        }
      }
    });
  }
});

/**
 * The pit banner is inside the hero on every face, at the top of zone A on the house's and of the
 * gear's tile on the Porsche's, and it is the one thing there with a state of its own: a pop-up
 * centred on a short hero once covered its bottom on the Porsche's 1280 x 400 and 600 x 686, and on
 * the portrait face the lower half of `Engage limiter` with it (#1052). Asked of the family over the
 * theme's regions, and again of the face as the theme ships it, where the banner is the one the face
 * draws and a theme's own notifications are drawn where the theme puts them.
 */
describe('the pop-ups and the change notifications keep clear of the pit banner', () => {
  for (const face of faces) {
    test(`${face.name}`, () => {
      const banner = regionRect(face.regions, 'pitAlert');
      const parts = [...partsOf(face.items, 'popUp.'), ...partsOf(face.items, 'notice.')];
      expect(parts.length).toBeGreaterThan(0);
      for (const item of parts) {
        if (item.kind === 'layer') continue;
        expect({ face: face.name, item: item.name, rect: item.rect, banner, covered: overlaps(banner, item.rect) }).toMatchObject({ covered: false });
      }
    });
  }
});

describe('as each theme ships, no pop-up or change notification covers the pit banner the face draws', () => {
  const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
  for (const id of here) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const built = buildThemeFace(id, size, OPTS).built.main;
        const screens = built.screens
          .map((screen) => ({ screen: screen.name, items: [...walkItems(screen.items)].filter((item): item is Exclude<Item, LayerItem> => item.kind !== 'layer') }))
          .filter(({ items }) => items.some((item) => item.name.startsWith('pitAlert.')));
        // Both arrangements, the rev bar on and off, each with its own banner.
        expect(screens.length).toBeGreaterThanOrEqual(2);
        for (const { screen, items } of screens) {
          const banners = items.filter((item) => item.name.startsWith('pitAlert.'));
          const family = items.filter((item) => /^(popUp|notice)\./.test(item.name));
          expect(family.length).toBeGreaterThan(0);
          for (const item of family) {
            for (const banner of banners) {
              expect({ face: `${id} ${size.width}x${size.height}`, screen, item: item.name, rect: item.rect, banner: banner.name, covered: overlaps(banner.rect, item.rect) }).toMatchObject({ covered: false });
            }
          }
        }
      });
    }
  }
});

describe('the lap review keeps to the body, and says so', () => {
  for (const face of faces) {
    test(`${face.name}`, () => {
      const body = regionRect(face.regions, 'flagBody');
      const parts = partsOf(face.items, 'lapReview.');
      expect(parts.length).toBeGreaterThan(0);
      for (const item of parts) {
        if (item.kind === 'layer') continue;
        expect({ face: face.name, item: item.name, rect: item.rect, body, inside: contains(body, item.rect) }).toMatchObject({ inside: true });
        for (const [what, box] of settled(face)) {
          if (!box) continue;
          expect({ face: face.name, item: item.name, what, covered: overlaps(box, item.rect) }).toMatchObject({ covered: false });
        }
      }
    });
  }
});

/**
 * The faces whose lap review draws the lap time alone, by theme, measured in the theme's own fonts.
 *
 * Pinned rather than tolerated, the way `conformance.test.ts` pins what a theme gets wrong: a face
 * listed here that comes to keep its deltas fails, and so does one left out that loses them. The
 * deltas are the review's floor (`lapReviewFit`), and the house's condensed figures keep it on every
 * face. A 64 px lap time and two 46 px deltas are 680 px of Barlow at normal width and 808 of the
 * seven-segment face, and these bodies are narrower than that inside even the tighter padding: the
 * Porsche's 694, 705 and 582, the AiM's 850, 800 and 600. The Porsche's 800 x 480 and 800 x 286
 * kept their deltas while the review was clamped to the face rather than to the body, and gave them
 * up for not covering the settings column and the telltales (#1047).
 */
const LAP_TIME_ALONE: Record<string, readonly string[]> = {
  porsche: ['800x480', '800x286', '600x686'],
  aim: ['850x480', '800x480', '800x286', '600x686'],
};

/**
 * The faces whose review tightens its side padding from the sheet's 32 to 24 to keep its deltas: the
 * Porsche's 850 x 480, whose 743 px body is one pixel short of them inside the sheet's padding.
 */
const NARROW_PADDING: Record<string, readonly string[]> = {
  porsche: ['850x480'],
};

describe('the lap review keeps its deltas wherever the face it is drawn in holds them', () => {
  for (const face of faces) {
    test(`${face.name}`, () => {
      const parts = partsOf(face.items, 'lapReview.');
      const has = (id: string): boolean => parts.some((item) => item.name === `lapReview.${id}.value`);
      const alone = LAP_TIME_ALONE[face.theme]?.includes(face.size) ?? false;
      expect({ face: face.name, lap: has('lap'), vsBest: has('vsBest'), vsPrevious: has('vsPrevious') }).toEqual({
        face: face.name,
        lap: true,
        vsBest: !alone,
        vsPrevious: !alone,
      });
      const box = parts.find((item): item is Exclude<Item, LayerItem> => item.kind !== 'layer' && item.name === 'lapReview.box')!;
      const lap = parts.find((item): item is Exclude<Item, LayerItem> => item.kind !== 'layer' && item.name === 'lapReview.lap.value')!;
      const narrow = NARROW_PADDING[face.theme]?.includes(face.size) ?? false;
      expect({ face: face.name, padX: lap.rect.left - box.rect.left }).toEqual({ face: face.name, padX: narrow ? LAP_REVIEW_PAD_X_NARROW : LAP_REVIEW_PAD_X });
    });
  }
});

if (THEME_ID === DEFAULT_THEME_ID) {
  for (const id of Object.keys(THEMES).filter((each) => !drawsInThisProcess(each))) {
    test(
      `the ${id} theme, in a process drawing its fonts`,
      () => {
        const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: id }, stdout: 'pipe', stderr: 'pipe' });
        const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
        expect({ theme: id, exit: run.exitCode, output }).toEqual({ theme: id, exit: 0, output: '' });
      },
      120_000,
    );
  }
}
