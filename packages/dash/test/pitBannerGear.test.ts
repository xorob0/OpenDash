/**
 * The pit banner never covers the gear, on any page of zone A, on any face of any theme (#1182).
 *
 * The house's pit family, the limiter banner and the states that take its place, is drawn opaque over
 * the top of zone A on the house's faces and the Porsche's, and is out for as long as a driver is in
 * the lane. On the house's 800x286 face and the Porsche's 1280x400 and 600x686 faces the gear of the
 * gearSpeedRevs page reached up into it, and a driver serving a stop read the gear without its top.
 * The lasting pop-ups are already held clear of the gear (`popUpRegions.test.ts`, #1047); this holds
 * the pit family to the same rule, which is the one the lasting pop-ups took their row from.
 *
 * Asked twice, as the lasting pop-ups are: of the regions, through `zoneAGearInk`, which is what the
 * face places the lasting pop-ups' row by, and of each built package, where the ink is read off the
 * pages of the zone A dashboard the face's widget points at and the banner off the items the face
 * draws, so that a page, a theme's drawing of zone A or a face that moves either is caught whatever
 * the build thought it was clearing. Both arrangements of every face, and every page of zone A a
 * driver can cycle to while the banner is out.
 *
 * Each theme is measured in a process of its own, since a theme's type is set from the environment
 * when the tokens load: the default theme here, and every other theme in a child process at the end
 * of the file, as `conformance.test.ts` runs them.
 */
import { describe, expect, test } from 'bun:test';
import type { Item, LayerItem, Rect, TextItem } from '../src/generator.ts';
import { overlaps, rect, translate } from '../src/design/geometry.ts';
import { inkOf } from '../src/design/metrics.ts';
import { regionRect, zoneRect } from '../src/themes/anatomy.ts';
import { buildThemeFace, drawsInThisProcess } from '../src/themes/faces.ts';
import { THEME_DRAWINGS } from '../src/themes/drawings.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { FACE_SIZES } from '../src/contract.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { regionsWithoutRevBar, zoneAPage } from '../src/zones/index.ts';
import { bannerClearance, zoneAGearInk, zoneDashboardsFor } from '../src/zones/pages.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const METADATA = { title: 'test', ...OPTS };

/** The themes whose type this process draws. */
const here = Object.keys(THEMES).filter(drawsInThisProcess);

/** Every item a screen draws, layers walked through. */
const drawnOn = (items: readonly Item[]): Exclude<Item, LayerItem>[] => [...walkItems(items)].filter((item): item is Exclude<Item, LayerItem> => item.kind !== 'layer');

/** A run of zone A that draws the gear, its ghosted neighbours and an LCD's unlit cell included. */
const GEAR_RUN = /(^|\.)gear(\.(value|ghost|below|above))?$/;

/** The gear's ink on every page of zone A as the face ships it, in the face's coordinates. */
function gearInkOn(screen: { items: Item[] }, zones: readonly { name: string; screens: { name: string; items: Item[] }[] }[]): { page: string; ink: Rect }[] {
  const widget = drawnOn(screen.items).find((item) => item.kind === 'widget' && item.name === 'zoneA');
  if (!widget || widget.kind !== 'widget') throw new Error('the face has no zone A widget');
  const dashboard = zones.find((zone) => `${zone.name}.djson` === widget.fileName);
  if (!dashboard) throw new Error(`no zone dashboard ${widget.fileName}`);
  return dashboard.screens.flatMap((page) =>
    drawnOn(page.items)
      .filter((item): item is TextItem => item.kind === 'text' && GEAR_RUN.test(item.name))
      .map((item) => ({ page: page.name, ink: translate(inkOf(item), widget.rect.left, widget.rect.top) })),
  );
}

describe('the gear begins below the pit banner on every page of zone A, in both arrangements of every face', () => {
  for (const id of here) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const { regions } = buildThemeFace(id, size, OPTS);
        const drawing = THEME_DRAWINGS[id] ?? {};
        for (const [arrangement, each] of [['rev bar on', regions], ['rev bar off', regionsWithoutRevBar(regions)]] as const) {
          const banner = regionRect(each, 'pitAlert');
          const gear = zoneAGearInk(each, drawing);
          expect(gear.length).toBeGreaterThan(0);
          for (const ink of gear) {
            const where = { face: `${id} ${size.width}x${size.height}`, arrangement };
            expect({ ...where, banner, ink, covered: overlaps(ink, banner) }).toMatchObject({ ...where, covered: false });
          }
        }
      });
    }
  }
});

describe('as each theme ships, the pit family covers no gear on any page of zone A', () => {
  for (const id of here) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const { main, zones } = buildThemeFace(id, size, OPTS).built;
        const screens = main.screens.filter((screen) => screen.name.startsWith('Main'));
        expect(screens.length).toBeGreaterThanOrEqual(2);
        for (const screen of screens) {
          const gear = gearInkOn(screen, zones);
          // Three of zone A's four pages draw the gear; the track page draws none.
          expect(new Set(gear.map(({ page }) => page)).size).toBeGreaterThanOrEqual(3);
          const family = drawnOn(screen.items).filter((item) => item.name.startsWith('pitAlert.'));
          expect(family.length).toBeGreaterThan(0);
          for (const item of family) {
            for (const { page, ink } of gear) {
              const where = { face: `${id} ${size.width}x${size.height}`, screen: screen.name, item: item.name, page };
              expect({ ...where, rect: item.rect, ink, covered: overlaps(item.rect, ink) }).toMatchObject({ ...where, covered: false });
            }
          }
        }
      });
    }
  }
});

/**
 * The gear is moved down into the room under the stack rather than drawn smaller: every face whose
 * banner reaches into zone A has the room, so the gear, the speed and the revs keep the sizes the
 * column gives them, and the page is the page it was with its runs lower.
 */
describe('a gear the banner reached is moved, not shrunk', () => {
  for (const id of here) {
    const drawing = THEME_DRAWINGS[id] ?? {};
    if (drawing.zoneAPage) continue;
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const { regions } = buildThemeFace(id, size, OPTS);
        for (const each of [regions, regionsWithoutRevBar(regions)]) {
          const clear = bannerClearance(each);
          if (clear === 0) continue;
          const zoneA = zoneRect(each, 'A');
          const frame = rect(0, 0, zoneA.width, zoneA.height);
          const ghosts = drawing.gearGhosts ?? true;
          const runs = (items: Item[]) => drawnOn(items).filter((item): item is TextItem => item.kind === 'text');
          const before = runs(zoneAPage('gearSpeedRevs', frame, 'p.', ghosts));
          const after = runs(zoneAPage('gearSpeedRevs', frame, 'p.', ghosts, clear));
          expect(after.map((run) => [run.name, run.fontSize])).toEqual(before.map((run) => [run.name, run.fontSize]));
          for (const run of after) expect(run.rect.top + run.rect.height).toBeLessThanOrEqual(frame.height);
          for (const run of after.filter((each) => GEAR_RUN.test(each.name))) expect(inkOf(run).top).toBeGreaterThanOrEqual(clear);
        }
      });
    }
  }
});

/**
 * A theme's own page of zone A is not told where the banner reaches, so it is held to it instead: one
 * that draws its gear under the banner fails the build. Here a theme that draws the house's pages as
 * they were before #1182, on the house nano's zone A, under a banner reaching 36 px down it as the
 * nano's does. Asked in the house's type only, which is the type those pages are measured in.
 */
test.if(THEME_ID === DEFAULT_THEME_ID)('a page of zone A whose gear the banner would still cover is refused rather than built', () => {
  const face = FACE_SIZES.find((each) => each.width === 800 && each.height === 286)!;
  const size = { width: 260, height: 194 };
  const unmoved = { zoneAPage: (page: string, frame: Rect, prefix: string) => zoneAPage(page, frame, prefix) };
  expect(zoneDashboardsFor(face, [{ zone: 'A', size, clear: 0 }], METADATA, unmoved)).toHaveLength(1);
  expect(zoneDashboardsFor(face, [{ zone: 'A', size, clear: 36 }], METADATA)).toHaveLength(1);
  expect(() => zoneDashboardsFor(face, [{ zone: 'A', size, clear: 36 }], METADATA, unmoved)).toThrow(/pit banner reaches 36 px down a 260 x 194 column and covers the top of gearSpeedRevs\./);
  expect(() => zoneDashboardsFor(face, [{ zone: 'A', size, clear: 30 }, { zone: 'A', size, clear: 0 }], METADATA)).toThrow(/two reaches of the pit banner/);
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
