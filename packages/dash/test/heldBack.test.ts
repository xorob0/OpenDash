/**
 * Band D's car page, D8, is held back from 1.0 (#969): twelve outlined boxes with no pictogram read
 * as a broken page, so no built package reaches it, and a rig that had chosen it lands on a page.
 *
 * Held back rather than taken out, which is what most of this file is about: the page keeps its
 * number, so every page after it keeps its own, and a saved setting of a driver who never chose it
 * still means what it meant. The telltale rank itself is still built and measured by `bandPages.test.ts`
 * and `zoneFace.test.ts`, so that #148 re-enables it rather than rewriting it.
 *
 * The plugin's half, the stored selection normalised forward and the panel offering it nowhere, is
 * `plugin/OpenDash.Tests/HeldBackBandPagesTests.cs`.
 */
import { describe, expect, test } from 'bun:test';
import type { Dashboard, Item } from '../src/generator.ts';
import { composePackages, themesToBuild } from '../src/build.ts';
import {
  BAND_D_PAGES,
  DEFAULT_ZONE_PAGE,
  FACE_SIZES,
  defaultZoneMask,
  defaultZonePage,
  HELD_BACK_BAND_PAGES,
  isHeldBack,
  offeredPages,
  pagesForZone,
  THEME_CATALOGUE,
  zone,
} from '../src/contract.ts';
import { walkItems } from '../src/walk.ts';
import { TELLTALE_PAGE, TELLTALES } from '../src/zones/telltales.ts';

const HELD = BAND_D_PAGES.find((page) => page.id === TELLTALE_PAGE)!;

const names = (items: readonly Item[]): string[] => [...walkItems(items)].map((item) => item.name);

describe('the car page is held back, and keeps its place', () => {
  test('is the one page held back, and only in band D', () => {
    expect(HELD_BACK_BAND_PAGES).toEqual([TELLTALE_PAGE]);
    expect(isHeldBack('D', TELLTALE_PAGE)).toBe(true);
    for (const z of ['A', 'B', 'C'] as const) for (const page of pagesForZone(z)) expect({ z, id: page.id, held: isHeldBack(z, page.id) }).toMatchObject({ held: false });
  });

  test('keeps its number, so the other seven and a theme’s band page keep theirs', () => {
    expect(HELD.number).toBe(7);
    expect(BAND_D_PAGES.map((p) => p.id)).toEqual(['fuel', 'energy', 'stint', 'tyres', 'weather', 'sectors', 'relative', 'car']);
    expect(offeredPages('D').map((p) => [p.number, p.id])).toEqual([
      [0, 'fuel'],
      [1, 'energy'],
      [2, 'stint'],
      [3, 'tyres'],
      [4, 'weather'],
      [5, 'sectors'],
      [6, 'relative'],
    ]);
    for (const theme of THEME_CATALOGUE) {
      const added = pagesForZone('D', theme).slice(BAND_D_PAGES.length);
      expect(offeredPages('D', theme).slice(7)).toEqual(added);
      for (const [i, page] of added.entries()) expect(page.number).toBe(BAND_D_PAGES.length + i);
    }
  });

  test('is in no default cycle and is no face’s opening page, on any theme', () => {
    for (const theme of [undefined, ...THEME_CATALOGUE]) {
      const mask = defaultZoneMask('D', theme);
      expect({ theme: theme?.id, held: (mask >> HELD.number) & 1 }).toEqual({ theme: theme?.id, held: 0 });
      // Every other page is in it.
      for (const page of offeredPages('D', theme)) expect((mask >> page.number) & 1).toBe(1);
      expect(defaultZonePage('D', theme)).not.toBe(HELD.number);
      // And what a package with no plugin reads is those same two.
      expect(String(zone.mask(FACE_SIZES[0]!, 'D', theme))).toContain(`, ${mask})`);
      expect(String(zone.page(FACE_SIZES[0]!, 'D', theme))).not.toContain(`, ${HELD.number})`);
    }
    expect(DEFAULT_ZONE_PAGE.D).not.toBe(HELD.number);
  });
});

describe('no built package reaches the telltale page from band D', () => {
  const themes = themesToBuild({ themes: [], allThemes: true, touchedThemes: false }, () => {});
  const bands: { folder: string; band: Dashboard }[] = composePackages({ version: '0.0.0-test', themes, log: () => {} }).flatMap(({ pkg }) =>
    pkg.dashboards.filter((d) => d.name.startsWith('zoneface-band-')).map((band) => ({ folder: pkg.folderName, band })),
  );

  test('there is a band to look at for every theme, the ones that add a band page among them', () => {
    expect(themes.length).toBeGreaterThan(1);
    expect(bands.length).toBeGreaterThanOrEqual(themes.length);
    expect(bands.some(({ band }) => band.screens.length > BAND_D_PAGES.length)).toBe(true);
  });

  test('no band dashboard draws a telltale, in any theme’s drawing of the page', () => {
    // Every drawing of the car page names a lamp's items after the lamp, as `car.limiter.chip`: the
    // house's boxes, the Porsche's lamps on their grey cell and the AiM's lamp words alike.
    const lamps = TELLTALES.map((lamp) => `${TELLTALE_PAGE}.${lamp.id}.`);
    for (const { folder, band } of bands) {
      const drawn = band.screens.flatMap((screen) => names(screen.items)).filter((name) => lamps.some((lamp) => name.startsWith(lamp)));
      expect({ folder, band: band.name, drawn }).toEqual({ folder, band: band.name, drawn: [] });
    }
  });

  test('the car page’s screen keeps its index and falls back to fuel rather than drawing nothing', () => {
    for (const { folder, band } of bands) {
      const held = band.screens[HELD.number]!;
      const fuel = band.screens[DEFAULT_ZONE_PAGE.D]!;
      expect({ folder, band: band.name, name: held.name }).toEqual({ folder, band: band.name, name: TELLTALE_PAGE });
      expect(names(held.items).length).toBeGreaterThan(0);
      // Item for item, but for what a theme draws on every screen under the screen's own name, as the
      // AiM's backlight.
      const asFuel = names(held.items).map((name) => (name.startsWith(`${TELLTALE_PAGE}.`) ? `${fuel.name}.${name.slice(TELLTALE_PAGE.length + 1)}` : name));
      expect({ folder, band: band.name, items: asFuel }).toEqual({ folder, band: band.name, items: names(fuel.items) });
      // Every page after it is where it was.
      for (const page of BAND_D_PAGES.filter((p) => p.number > HELD.number)) expect(band.screens[page.number]!.name).toBe(page.id);
    }
  });
});
