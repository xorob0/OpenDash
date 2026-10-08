/**
 * The demo's fake panel against the contract (#395): it publishes each screen's whole group of
 * properties under the names `contract.ts` gives them (a face's, a car theme's face's, a companion's
 * and a pit wall's), the rig-wide ones it offers are ones the dashboards read, and its buttons and
 * its forces behave as the plugin's do.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import {
  COMPANION_FLAG_FORMAT_SETTING,
  COMPANION_OPEN_ON_BACK,
  COMPANION_OPEN_ON_SETTING,
  COMPANION_PREFIX,
  dashProperties,
  DEFAULT_COMPANION_FLAG_FORMAT,
  DEFAULT_COMPANION_OPEN_ON,
  DEFAULT_PIT_WALL_PAGE,
  DEFAULT_QUICK_GLANCE,
  defaultZoneMask,
  defaultZonePage,
  FACE_SIZES,
  FACE_ZONE_LETTERS,
  facePropertyNames,
  MODULE_CATALOGUE,
  moduleSettingName,
  pagesForZone,
  PIT_WALL_LANDSCAPE_PAGES,
  PIT_WALL_PAGE_SETTING,
  PIT_WALL_PAGES,
  PIT_WALL_PREFIX,
  PIT_WALL_WIDE_ZONE_PAGES,
  PIT_WALL_ZONE_PAGES,
  pitWallZoneSettingName,
  PORSCHE_CREST,
  propertyName,
  screenProperties,
  themeEntry,
  WEB_VIEW_SETTING,
} from '../../packages/dash/src/contract.ts';
import {
  beginCompanionGlance,
  beginGlance,
  companionOpenOn,
  cycle,
  cyclePosition,
  endCompanionGlance,
  endGlance,
  initialPanel,
  nextEnabled,
  openCompanion,
  panelProperties,
  pitWallPagesFor,
  setCompanionStart,
  setModuleEnabled,
  setPage,
  setPageEnabled,
  setPitWallPage,
  setPitWallZone,
  updatePitWall,
  zonesOf,
  type PanelState,
} from '../lib/demo/panel.ts';
import { COMPANION_TIMING, panelCatalogue } from '../scripts/demo-data.ts';
import { repoRoot } from './demoBuild.ts';

const catalogue = panelCatalogue();
const declared = new Set(dashProperties());

describe('the names it writes', () => {
  test.each(FACE_SIZES.map((f) => [`${f.width}x${f.height}`, f] as const))('Face%s: exactly the face group the plugin attaches', (_size, face) => {
    const prefix = `Face${face.width}x${face.height}`;
    const written = [...panelProperties(catalogue, initialPanel(catalogue, prefix)).keys()].filter((n) => n.includes(prefix));
    expect(written.sort()).toEqual(facePropertyNames(face).map(propertyName).sort());
  });

  test('every rig-wide name is one the dashboards read, and the crest is not among them', () => {
    const rig = [...panelProperties(catalogue, initialPanel(catalogue, null)).keys()];
    expect(rig.filter((n) => !declared.has(n))).toEqual([]);
    expect(rig).not.toContain(propertyName(PORSCHE_CREST));
    expect(rig).toContain(propertyName('PositionMode'));
    expect(rig).toContain(propertyName('Slot01'));
  });

  test('a fresh panel publishes the contract defaults, as a fresh install does', () => {
    const p = panelProperties(catalogue, initialPanel(catalogue, 'Face850x480'));
    expect(p.get('OpenDash.Face850x480ZoneC')).toBe(14);
    expect(p.get('OpenDash.Face850x480ZoneBPages')).toBe(2 ** 21 - 1);
    expect(p.get('OpenDash.Face850x480QuickGlance')).toBe(DEFAULT_QUICK_GLANCE);
    expect(p.get('OpenDash.Face850x480RevBar')).toBe('shift');
    expect(p.get('OpenDash.Face850x480ZoneCPosition')).toBe(15);
    expect(p.get('OpenDash.PositionMode')).toBe('class');
  });
});

describe('the wheel buttons', () => {
  const base = initialPanel(catalogue, 'Face850x480');

  test('step to the next enabled page and wrap, and back', () => {
    expect(nextEnabled(3, 0b1111, 4, 1)).toBe(0);
    expect(nextEnabled(0, 0b1111, 4, -1)).toBe(3);
    expect(nextEnabled(0, 0b1010, 4, 1)).toBe(1);
    expect(nextEnabled(1, 0b0010, 4, 1)).toBe(1);
    expect(cycle(catalogue, base, 'A', 1).zones[0]).toBe(1);
    expect(cycle(catalogue, base, 'A', -1).zones[0]).toBe(3);
  });

  test('keep at least one page in a cycle, and move a zone off a page that is turned off', () => {
    let s: PanelState = base;
    for (const p of [1, 2, 3]) s = setPageEnabled(catalogue, s, 'A', p, false);
    expect(s.masks[0]).toBe(1);
    expect(setPageEnabled(catalogue, s, 'A', 0, false)).toBe(s);
    const moved = setPageEnabled(catalogue, setPage(catalogue, base, 'A', 2), 'A', 2, false);
    expect(moved.zones[0]).toBe(3);
  });

  test('count the position in the cycle as the plugin publishes it', () => {
    expect(cyclePosition(0, 0b1111, 4)).toBe(1);
    expect(cyclePosition(3, 0b1010, 4)).toBe(2);
  });
});

describe('the glance', () => {
  test('shows its page in its zone while held and gives the zone back on release', () => {
    const base = initialPanel(catalogue, 'Face850x480');
    const held = beginGlance(base);
    expect(held.zones[2]).toBe(DEFAULT_QUICK_GLANCE % 100);
    expect(beginGlance(held)).toBe(held);
    expect(endGlance(held).zones).toEqual(base.zones);
    expect(endGlance(base)).toBe(base);
  });
});

describe('a car theme', () => {
  const porsche = themeEntry('porsche')!;
  const base = initialPanel(catalogue, 'Face1280x480', { theme: 'porsche' });

  test('reads the names the default face of its size reads, with band D its own catalogue', () => {
    const written = [...panelProperties(catalogue, base).keys()].filter((n) => n.includes('Face1280x480'));
    expect(written.sort()).toEqual(facePropertyNames(FACE_SIZES.find((f) => f.width === 1280 && f.height === 480)!).map(propertyName).sort());
    const zones = zonesOf(catalogue, base);
    expect(zones.map((z) => z.pages.map((p) => p.id))).toEqual(FACE_ZONE_LETTERS.map((z) => pagesForZone(z, porsche).map((p) => p.id)));
    expect(zones[3]!.pages.length).toBe(9);
  });

  test('opens band D on its own page, with all nine in the cycle, and wraps through them', () => {
    const p = panelProperties(catalogue, base);
    expect(p.get('OpenDash.Face1280x480ZoneD')).toBe(defaultZonePage('D', porsche));
    expect(p.get('OpenDash.Face1280x480ZoneD')).toBe(8);
    expect(p.get('OpenDash.Face1280x480ZoneDPages')).toBe(defaultZoneMask('D', porsche));
    expect(cycle(catalogue, base, 'D', 1).zones[3]).toBe(0);
    expect(cycle(catalogue, base, 'D', -1).zones[3]).toBe(7);
    expect(p.get('OpenDash.Face1280x480ZoneDPosition')).toBe(9);
    // The default face of the same size keeps the house's eight.
    expect(panelProperties(catalogue, initialPanel(catalogue, 'Face1280x480')).get('OpenDash.Face1280x480ZoneDPages')).toBe(2 ** 8 - 1);
  });
});

describe('the companion', () => {
  const base = initialPanel(catalogue, null, { screen: 'companion' });
  const openOn = propertyName(COMPANION_OPEN_ON_SETTING);

  test('publishes exactly the group the plugin attaches to a companion', () => {
    const written = [...panelProperties(catalogue, base).keys()].filter((n) => !declared.has(n) || n.includes(COMPANION_PREFIX));
    expect(written.sort()).toEqual(screenProperties(COMPANION_PREFIX).sort());
    expect(panelProperties(catalogue, base).get(propertyName(COMPANION_FLAG_FORMAT_SETTING))).toBe(DEFAULT_COMPANION_FLAG_FORMAT);
    expect(panelProperties(catalogue, base).get(openOn)).toBe(DEFAULT_COMPANION_OPEN_ON);
    // Energy, damage and track rivals are off on a fresh install.
    expect(MODULE_CATALOGUE.filter((m) => panelProperties(catalogue, base).get(propertyName(moduleSettingName(m.number))) === false).map((m) => m.id)).toEqual(['energy', 'damage', 'trackRivals']);
  });

  test('forces the start module for the window after it opens, past a module the rotation has off', () => {
    const opened = openCompanion(catalogue, base, 1000);
    expect(panelProperties(catalogue, opened, 1000).get(openOn)).toBe(catalogue.companion.defaultStart);
    expect(panelProperties(catalogue, opened, 1000 + catalogue.companion.openOnWindowMs - 1).get(openOn)).toBe(catalogue.companion.defaultStart);
    expect(panelProperties(catalogue, opened, 1000 + catalogue.companion.openOnWindowMs).get(openOn)).toBe(DEFAULT_COMPANION_OPEN_ON);
    const energy = MODULE_CATALOGUE.findIndex((m) => m.id === 'energy');
    expect(companionOpenOn(catalogue, setCompanionStart(catalogue, base, energy, 0), 0)).toBe(energy + 1);
  });

  test('forces the glance while it is held, then -2 for the way back, then nothing', () => {
    const held = beginCompanionGlance(base);
    expect(companionOpenOn(catalogue, held, 1e9)).toBe(catalogue.companion.defaultGlance);
    expect(beginCompanionGlance(held)).toBe(held);
    const released = endCompanionGlance(catalogue, held, 5000);
    expect(companionOpenOn(catalogue, released, 5000)).toBe(COMPANION_OPEN_ON_BACK);
    expect(companionOpenOn(catalogue, released, 5000 + catalogue.companion.backWindowMs)).toBe(DEFAULT_COMPANION_OPEN_ON);
    expect(endCompanionGlance(catalogue, released, 6000)).toBe(released);
  });

  test('keeps one module in the rotation', () => {
    let s = base;
    for (let i = 1; i < MODULE_CATALOGUE.length; i++) s = setModuleEnabled(s, i, false);
    expect(s.companion.modules.filter(Boolean).length).toBe(1);
    expect(setModuleEnabled(s, 0, false)).toBe(s);
  });

  test('times its forces and opens on the modules the plugin does', () => {
    const contract = readFileSync(path.join(repoRoot, 'plugin', 'OpenDash', 'Contract.cs'), 'utf8');
    const seconds = (name: string) => Number(new RegExp(`${name} = TimeSpan\\.FromSeconds\\((\\d+)\\)`).exec(contract)?.[1]) * 1000;
    const int = (name: string) => Number(new RegExp(`const int ${name} = (-?\\d+);`).exec(contract)?.[1]);
    expect(COMPANION_TIMING).toEqual({
      openOnWindowMs: seconds('CompanionOpenOnWindow'),
      backWindowMs: seconds('CompanionBackWindow'),
      defaultStart: int('DefaultCompanionStart'),
      defaultGlance: int('DefaultCompanionQuickGlance'),
    });
  });
});

describe('the pit wall', () => {
  const base = initialPanel(catalogue, null, { screen: 'pitwall' });

  test('publishes exactly the group the plugin attaches to a pit wall', () => {
    const written = [...panelProperties(catalogue, base).keys()].filter((n) => !declared.has(n) || n.includes(PIT_WALL_PREFIX) || n === propertyName(WEB_VIEW_SETTING));
    expect(written.sort()).toEqual(screenProperties(PIT_WALL_PREFIX).sort());
    const p = panelProperties(catalogue, base);
    expect(p.get(propertyName(PIT_WALL_PAGE_SETTING))).toBe(DEFAULT_PIT_WALL_PAGE);
    for (const page of PIT_WALL_PAGES) for (const z of page.zones) expect(p.get(propertyName(pitWallZoneSettingName(page.id, z.slot)))).toBe(z.fallback);
  });

  test('offers the three landscape pages and the portrait one, each zone with its own catalogue', () => {
    expect(pitWallPagesFor(catalogue, true).map((p) => p.name)).toEqual(PIT_WALL_LANDSCAPE_PAGES.map((p) => p.name));
    expect(pitWallPagesFor(catalogue, false).map((p) => p.id)).toEqual(['portrait']);
    expect(catalogue.pitWall.standardPages.map((p) => p.id)).toEqual(PIT_WALL_ZONE_PAGES.map((p) => p.id));
    expect(catalogue.pitWall.widePages.map((p) => p.id)).toEqual(PIT_WALL_WIDE_ZONE_PAGES.map((p) => p.id));
    const s = updatePitWall(setPitWallZone(setPitWallPage(base, 1), pitWallZoneSettingName('tower', 'Wide'), 3), 'webViewUrl', 'https://example.com');
    const p = panelProperties(catalogue, s);
    expect(p.get(propertyName(PIT_WALL_PAGE_SETTING))).toBe(1);
    expect(p.get(propertyName('PitWallTowerWide'))).toBe(3);
    expect(p.get(propertyName(WEB_VIEW_SETTING))).toBe('https://example.com');
  });
});
