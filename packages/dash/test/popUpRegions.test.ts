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
 * conformance harness builds a test theme: a theme's colours can only be drawn in a process of their
 * own, and the colours move no rectangle. A theme's own drawing that moves a member of the family
 * elsewhere, as the Porsche's change notification is drawn in zone C's panel, is checked by the
 * harness against its frame; what is checked here is the house's family over the theme's regions,
 * which is the stricter question, since it asks it of every theme's hero.
 */
import { afterAll, describe, expect, test } from 'bun:test';
import type { Item, Rect } from '../src/generator.ts';
import { contains, overlaps } from '../src/design/geometry.ts';
import { optionalRegionRect, regionRect, zoneRect, type Regions } from '../src/themes/anatomy.ts';
import { THEMES } from '../src/themes/index.ts';
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
  layout: ZoneLayout;
  regions: Regions;
  revBar: boolean;
  items: Item[];
}

/** Every face of every theme, in both arrangements. */
const faces: Face[] = Object.entries(THEMES).flatMap(([id, theme]) =>
  theme.anatomy.sizes.flatMap((size) => {
    const layout = ZONE_FACES.find((face) => face.width === size.width && face.height === size.height)!;
    const on = theme.anatomy.regions(layout);
    const off = regionsWithoutRevBar(on);
    const offLayout = layoutWithoutRevBar(layout);
    return [
      { name: `${id} ${size.width}x${size.height}`, layout, regions: on, revBar: true, items: faceItems(layout, { regions: on }) },
      { name: `${id} ${size.width}x${size.height}, rev bar off`, layout: offLayout, regions: off, revBar: false, items: faceItems(offLayout, { regions: off, revBar: false }) },
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
