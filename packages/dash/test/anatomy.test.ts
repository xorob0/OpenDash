/**
 * The anatomy as a theme declaration (#196): the default theme's anatomy is the zone anatomy, a
 * theme that declares another builds at every size it claims and refuses every other, and no theme
 * takes a page out of a zone's catalogue.
 *
 * ADR 0015 lets a theme add a page of its own to a catalogue, the Porsche's foot row in band D for
 * instance, and never remove one, so the catalogue check names an added page rather than failing on
 * it. A page is also held to its place, since the zone setting the plugin writes is the page's index.
 */
import { afterAll, beforeAll, describe, expect, test } from 'bun:test';
import { FACE_SIZES, pagesForZone, type FaceZone } from '../src/contract.ts';
import type { Dashboard, WidgetItem } from '../src/generator.ts';
import { checkRegions, type Regions } from '../src/themes/anatomy.ts';
import { buildThemeFace, buildThemeFaces, type ThemeFace } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEMES } from '../src/themes/index.ts';
import { FACE_SCREEN_NAME, FACE_SCREEN_NAME_NO_REV_BAR, ZONE_FACES, buildZoneFace, zoneRegions } from '../src/zones/index.ts';
import { catalogueChanges } from './conformance.ts';
import { GEAR_LEFT_THEME_ID, gearLeftTheme } from './fixtures/gearLeftTheme.ts';
import greenTheme from './fixtures/greenTheme.json';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

beforeAll(() => {
  THEMES[GEAR_LEFT_THEME_ID] = gearLeftTheme;
});
afterAll(() => {
  delete THEMES[GEAR_LEFT_THEME_ID];
});

/** The pages a theme has added, by zone, which is what the test names; and nothing removed or moved, which it refuses. */
function expectWholeCatalogue(face: ThemeFace, added: Partial<Record<FaceZone, string[]>> = {}): void {
  for (const change of catalogueChanges(face)) {
    const where = { face: face.layout.folder, screen: change.screen, zone: change.zone };
    expect({ ...where, removed: change.removed, moved: change.moved }).toEqual({ ...where, removed: [], moved: [] });
    expect({ ...where, added: change.added }).toEqual({ ...where, added: added[change.zone] ?? [] });
  }
}

const DEFAULT_FACES = buildThemeFaces(DEFAULT_THEME_ID, OPTS);

describe('the default theme', () => {
  test('claims every size the contract names, in its order', () => {
    expect(DEFAULT_FACES.map((f) => [f.layout.width, f.layout.height])).toEqual(FACE_SIZES.map((s) => [s.width, s.height]));
  });

  test('is the zone anatomy: its regions are the house face’s rectangles, and it builds what the build builds', () => {
    for (const [i, face] of DEFAULT_FACES.entries()) {
      expect(face.layout).toBe(ZONE_FACES[i]!);
      expect(face.regions).toEqual(zoneRegions(face.layout));
      expect(face.built).toEqual(buildZoneFace(face.layout, OPTS));
    }
  });

  test('carries every page of every catalogue, in its place, and adds none', () => {
    for (const face of DEFAULT_FACES) expectWholeCatalogue(face);
  });
});

describe('a theme that declares another arrangement', () => {
  const faces = (): ThemeFace[] => buildThemeFaces(GEAR_LEFT_THEME_ID, OPTS);
  const widget = (face: ThemeFace, screen: string, zone: FaceZone): WidgetItem =>
    face.built.main.screens.find((s) => s.name === screen)!.items.find((i): i is WidgetItem => i.kind === 'widget' && i.name === `zone${zone}`)!;

  test('builds at every size it claims, and draws its zones where it put them', () => {
    const built = faces();
    expect(built.map((f) => `${f.layout.width}x${f.layout.height}`)).toEqual(['1280x480', '1920x480']);
    for (const face of built) {
      const house = face.layout.zones;
      for (const screen of [FACE_SCREEN_NAME, FACE_SCREEN_NAME_NO_REV_BAR]) {
        expect({ face: face.layout.folder, screen, a: widget(face, screen, 'A').rect.left }).toEqual({ face: face.layout.folder, screen, a: 0 });
        expect({ face: face.layout.folder, screen, b: widget(face, screen, 'B').rect.left }).toEqual({ face: face.layout.folder, screen, b: house.zoneA.width + 1 });
        expect({ face: face.layout.folder, screen, c: widget(face, screen, 'C').rect.left }).toEqual({ face: face.layout.folder, screen, c: house.zoneC.left });
      }
      // The rules follow the zones: one where the gear meets B and one where B meets C, and none
      // where the house face's gear began.
      const rules = face.built.main.screens[0]!.items.flatMap((i) => ('rect' in i && i.name.startsWith('rule.') && i.rect.width === 1 ? [[i.name, i.rect.left]] : []));
      expect(rules).toEqual([
        ['rule.ab', house.zoneA.width],
        ['rule.bc', house.zoneC.left - 1],
      ]);
      // The limiter is drawn over the gear, wherever the gear is.
      const limiter = face.regions.find((r) => r.role === 'pitAlert')!.rect;
      expect(limiter.left).toBe(house.pitLimiter.left - house.zoneA.left);
    }
  });

  test('hands the modules no rectangle the house faces do not already build and measure', () => {
    const house = new Set(DEFAULT_FACES.flatMap((f) => f.built.zones.map((d) => d.name)));
    for (const face of faces()) {
      for (const dashboard of face.built.zones) expect({ face: face.layout.folder, dashboard: dashboard.name, known: house.has(dashboard.name) }).toMatchObject({ known: true });
    }
  });

  test('carries every page of every catalogue, in its place, and adds none', () => {
    for (const face of faces()) expectWholeCatalogue(face);
  });

  test('refuses a size it does not claim, and says which it does', () => {
    expect(() => buildThemeFace(GEAR_LEFT_THEME_ID, { width: 850, height: 480 }, OPTS)).toThrow(
      'the test-gear-left theme does not draw 850x480; it draws 1280x480, 1920x480',
    );
  });
});

describe('the catalogue check', () => {
  const large = (): ThemeFace => DEFAULT_FACES.find((f) => f.layout.folder === 'OpenDash 1280x480')!;
  const withBand = (face: ThemeFace, edit: (d: Dashboard) => Dashboard): ThemeFace => ({
    ...face,
    built: { ...face.built, zones: face.built.zones.map((d) => (d.name.startsWith('zoneface-band-') ? edit(d) : d)) },
  });

  test('names a page a theme adds at the end of a catalogue, and lets it through', () => {
    const face = withBand(large(), (d) => ({ ...d, screens: [...d.screens, { ...d.screens[0]!, name: 'porscheFoot' }] }));
    const band = catalogueChanges(face).filter((c) => c.zone === 'D');
    expect(band.map((c) => c.added)).toEqual([['porscheFoot'], ['porscheFoot']]);
    expectWholeCatalogue(face, { D: ['porscheFoot'] });
  });

  test('refuses a page taken out, and one pushed out of its place', () => {
    const removed = withBand(large(), (d) => ({ ...d, screens: d.screens.slice(1) }));
    const first = pagesForZone('D')[0]!.id;
    expect(catalogueChanges(removed).find((c) => c.zone === 'D')!.removed).toEqual([first]);
    const inserted = withBand(large(), (d) => ({ ...d, screens: [{ ...d.screens[0]!, name: 'porscheFoot' }, ...d.screens] }));
    expect(catalogueChanges(inserted).find((c) => c.zone === 'D')!.moved).toEqual(pagesForZone('D').map((p) => p.id));
  });
});

describe('regions a face cannot draw', () => {
  const house = ZONE_FACES[1]!;
  const size = { width: house.width, height: house.height };

  test('are refused with what is wrong with them', () => {
    const withoutC: Regions = zoneRegions(house).filter((r) => !(r.role === 'zone' && r.zone === 'C'));
    expect(() => checkRegions(withoutC, size, 'here')).toThrow('here: 0 regions for zone C');
    const twoBands: Regions = [...zoneRegions(house), { role: 'band', rect: { left: 0, top: 0, width: 10, height: 10 } }];
    expect(() => checkRegions(twoBands, size, 'here')).toThrow('here: more than one band region');
    const outside: Regions = zoneRegions(house).map((r) => (r.role === 'hero' ? { ...r, rect: { ...r.rect, left: 1200 } } : r));
    expect(() => checkRegions(outside, size, 'here')).toThrow('the hero region');
  });

  test('and the house faces have none', () => {
    for (const face of ZONE_FACES) expect(() => checkRegions(zoneRegions(face), { width: face.width, height: face.height }, face.folder)).not.toThrow();
  });
});

test('a theme with colours of its own is not built in a process drawing another theme’s', () => {
  THEMES['test-green-anatomy'] = { ...gearLeftTheme, overlay: greenTheme };
  try {
    expect(() => buildThemeFaces('test-green-anatomy', OPTS)).toThrow('start it with OPENDASH_THEME=test-green-anatomy');
  } finally {
    delete THEMES['test-green-anatomy'];
  }
});
