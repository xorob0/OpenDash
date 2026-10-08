/**
 * A theme's band pages are part of the contract (#718): the theme catalogue lists them, band D's
 * catalogue on a face of the theme is the house's eight and then those, and the face opens the band
 * on the first of them, with no plugin as with one.
 *
 * The Porsche's foot was appended to the band's dashboard as its ninth screen while the catalogue the
 * plugin cycles by still said eight, so the plugin clamped index 8 away and no rig could open the
 * page. The plugin's half of this is `ContractTests.Theme_band_pages_agree_with_contract_ts_when_present`
 * and `FaceSettingsTests`; this is the dash's half, and the rule that the drawing and the catalogue
 * name the same pages.
 */
import { afterAll, describe, expect, test } from 'bun:test';
import { composeTheme } from '../src/build.ts';
import {
  BAND_D_PAGES,
  bandDPages,
  DEFAULT_THEME_ID,
  DEFAULT_ZONE_PAGE,
  defaultZoneMask,
  defaultZonePage,
  FACE_ZONE_LETTERS,
  pagesForZone,
  THEME_CATALOGUE,
  themeEntry,
} from '../src/contract.ts';
import type { Item, WidgetItem } from '../src/generator.ts';
import type { ThemeDrawing } from '../src/themes/drawing.ts';
import { THEME_DRAWINGS } from '../src/themes/drawings.ts';
import { buildThemeFace } from '../src/themes/faces.ts';
import { THEMES } from '../src/themes/index.ts';
import { GEAR_LEFT_THEME_ID, gearLeftTheme } from './fixtures/gearLeftTheme.ts';

const porsche = themeEntry('porsche')!;
const house = themeEntry(DEFAULT_THEME_ID)!;
const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

describe('the theme catalogue', () => {
  test('gives the default theme no band pages and the Porsche its foot, under the theme’s name', () => {
    expect(house.bandPages).toEqual([]);
    expect(porsche.bandPages).toEqual([{ id: 'porscheFoot', name: 'Porsche' }]);
  });

  test('lists, for every theme with a drawing, exactly the band pages the drawing draws, in its order', () => {
    for (const theme of THEME_CATALOGUE) {
      const drawn = (THEME_DRAWINGS[theme.id]?.bandPages ?? []).map((p) => ({ id: p.id, name: p.name }));
      expect({ theme: theme.id, pages: theme.bandPages }).toEqual({ theme: theme.id, pages: drawn });
    }
  });
});

describe('band D’s catalogue on a themed face', () => {
  test('is the house’s eight for no theme and for the default, exactly as before', () => {
    expect(pagesForZone('D')).toBe(BAND_D_PAGES);
    expect(bandDPages(house)).toBe(BAND_D_PAGES);
    expect(BAND_D_PAGES).toHaveLength(8);
    expect(defaultZonePage('D', house)).toBe(DEFAULT_ZONE_PAGE.D);
    expect(defaultZoneMask('D', house)).toBe(defaultZoneMask('D'));
    expect(defaultZoneMask('D')).toBe(255);
  });

  test('is the house’s eight and then the theme’s, numbered on, so no house page leaves its index', () => {
    const pages = pagesForZone('D', porsche);
    expect(pages.slice(0, 8)).toEqual([...BAND_D_PAGES]);
    expect(pages.slice(8)).toEqual([{ number: 8, id: 'porscheFoot', name: 'Porsche' }]);
    for (const [i, page] of pages.entries()) expect(page.number).toBe(i);
    expect(defaultZoneMask('D', porsche)).toBe(511);
  });

  test('opens on the theme’s first page, and changes no other zone', () => {
    expect(defaultZonePage('D', porsche)).toBe(8);
    for (const zone of FACE_ZONE_LETTERS.filter((z) => z !== 'D')) {
      expect(pagesForZone(zone, porsche)).toEqual(pagesForZone(zone));
      expect(defaultZonePage(zone, porsche)).toBe(DEFAULT_ZONE_PAGE[zone]);
      expect(defaultZoneMask(zone, porsche)).toBe(defaultZoneMask(zone));
    }
  });
});

describe('the Porsche face', () => {
  // Composed as the build composes it, in a process drawn in the Porsche's colours.
  const faces = composeTheme({ theme: porsche, version: '0.0.0-test', simHubVersion: '9.12.6' });
  const flatten = (list: readonly Item[]): Item[] => list.flatMap((item) => (item.kind === 'layer' ? [item, ...flatten(item.children)] : [item]));

  test('opens band D on the foot, the widget’s own index and its binding’s fallback alike', () => {
    expect(faces).toHaveLength(porsche.sizes.length);
    for (const { pkg } of faces) {
      const main = pkg.dashboards[0]!;
      const widgets = main.screens.flatMap((s) => flatten(s.items)).filter((i): i is WidgetItem => i.kind === 'widget' && i.name === 'zoneD');
      expect(widgets.length).toBeGreaterThan(0);
      for (const widget of widgets) {
        expect(widget.initialScreenIndex).toBe(8);
        expect(widget.bindings?.InitialScreenIndex).toEqual({ mode: 'formula', formula: `isnull([OpenDash.Face${main.width}x${main.height}ZoneD], 8)` });
        // And the screen at that index of the dashboard it points at is the foot.
        const band = pkg.dashboards.find((d) => `${d.name}.djson` === widget.fileName)!;
        expect(band.screens.map((s) => s.name)).toEqual(pagesForZone('D', porsche).map((p) => p.id));
      }
    }
  });
});

describe('the build', () => {
  const drawings = THEME_DRAWINGS as Record<string, ThemeDrawing>;
  THEMES[GEAR_LEFT_THEME_ID] = gearLeftTheme;
  afterAll(() => {
    delete THEMES[GEAR_LEFT_THEME_ID];
    delete drawings[GEAR_LEFT_THEME_ID];
  });

  test('refuses a theme that draws a band page its catalogue entry does not list', () => {
    drawings[GEAR_LEFT_THEME_ID] = { bandPages: [{ id: 'testFoot', name: 'Test', items: () => [] }] };
    expect(() => buildThemeFace(GEAR_LEFT_THEME_ID, { width: 1280, height: 480 }, OPTS)).toThrow(
      `the ${GEAR_LEFT_THEME_ID} theme draws band pages ["testFoot"] and its catalogue entry lists []`,
    );
  });
});
