/**
 * What `scripts/rig.ts` decides without a VM: that every face it names exists, that a screen takes
 * the stock namespace for its size, that the pages it sets are pages the catalogues have, and that
 * the panel rig is written in fields the plugin's settings classes read.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { BAND_D_PAGES, MODULE_CATALOGUE, ZONE_A_PAGES } from '../packages/dash/src/contract.ts';
import { shapeById, stripLength } from '../packages/dash/src/leds/strip.ts';
import { browShape, catalogueMask, GALLERY, galleryRig, panelRig, RIM_FOLDER, RIM_SHAPE, RIM_ZONE_B, RIM_ZONE_C, screenFor, type ManifestPackage } from './rig.ts';

const repoRoot = path.resolve(import.meta.dir, '..');
const manifestPath = path.join(repoRoot, 'build', 'manifest.json');
const manifest: { packages: ManifestPackage[] } | null = existsSync(manifestPath) ? JSON.parse(readFileSync(manifestPath, 'utf8')) : null;

describe('a seeded screen', () => {
  const pkg: ManifestPackage = { folder: 'OpenDash 1280x480', kind: 'dash', width: 1280, height: 480, file: 'OpenDash 1280x480.simhubdash' };

  test('takes the stock namespace for its size, so its package is extracted unchanged', () => {
    expect(screenFor(pkg, ['gearSpeedRevs', 'lapTimes', 'relative', 'fuel']).Namespace).toBe('Face1280x480');
  });

  test('is named after the package, because SimHub lists a dashboard by its title', () => {
    expect(screenFor(pkg, ['gearSpeedRevs', 'lapTimes', 'relative', 'fuel']).Name).toBe('OpenDash 1280x480');
  });

  test('sets the start pages as well as the current ones', () => {
    const face = screenFor(pkg, ['speed', 'tyres', 'leaderboard', 'stint']).Face as { Zones: number[]; Starts: number[] };
    expect(face.Zones).toEqual(face.Starts);
    expect(face.Zones[0]).toBe(ZONE_A_PAGES.find((p) => p.id === 'speed')!.number);
    expect(face.Zones[1]).toBe(MODULE_CATALOGUE.find((m) => m.id === 'tyres')!.number - 1);
    expect(face.Zones[3]).toBe(BAND_D_PAGES.find((p) => p.id === 'stint')!.number);
  });

  test('refuses a page no catalogue has', () => {
    expect(() => screenFor(pkg, ['gearSpeedRevs', 'nonesuch', 'relative', 'fuel'])).toThrow(/catalogue/);
  });
});

describe('the gallery rig', () => {
  test('shows a different pair of zones on every face', () => {
    const pairs = Object.values(GALLERY).map((g) => `${g.zones[1]}/${g.zones[2]}`);
    expect(new Set(pairs).size).toBe(pairs.length);
  });

  test('never opens a zone on a page that ships switched off', () => {
    const off = new Set(MODULE_CATALOGUE.filter((m) => !m.enabled).map((m) => m.id));
    for (const [folder, g] of Object.entries(GALLERY)) {
      expect({ folder, off: [g.zones[1], g.zones[2]].filter((id) => off.has(id)) }).toEqual({ folder, off: [] });
    }
  });

  test.if(manifest !== null)('names only packages the build produced', () => {
    expect(galleryRig(manifest!)).toHaveLength(Object.keys(GALLERY).length);
  });
});

/**
 * The public settable properties a C# settings class declares, which are the JSON fields Json.NET
 * reads into it. Read from the source as text, the way ThemeTests reads tokens.json: the plugin's
 * classes cannot be loaded from here, and a field they do not declare is dropped without a word.
 */
function csharpFields(file: string, className: string): Set<string> {
  const text = readFileSync(path.join(repoRoot, 'plugin', 'OpenDash', file), 'utf8');
  const start = text.indexOf(`class ${className}`);
  if (start < 0) throw new Error(`${file} has no class ${className}`);
  const next = text.slice(start + 1).search(/\n {4}public (sealed |static )?class /);
  const body = next < 0 ? text.slice(start) : text.slice(start, start + 1 + next);
  return new Set([...body.matchAll(/public [\w<>[\],?. ]+? (\w+) \{ get; set; \}/g)].map((m) => m[1]!));
}

/** A `public const string` of Contract.cs, which is where the kind names are spelled. */
function contractString(name: string): string {
  const text = readFileSync(path.join(repoRoot, 'plugin', 'OpenDash', 'Contract.cs'), 'utf8');
  const found = new RegExp(`public const string ${name} = "([^"]*)";`).exec(text);
  if (!found) throw new Error(`Contract.cs has no string ${name}`);
  return found[1]!;
}

describe('the panel rig', () => {
  // The five packages the rig is built from, as build/manifest.json lists them, so the test holds
  // without a build; the real manifest is checked below when there is one.
  const packages: ManifestPackage[] = [
    { folder: 'OpenDash 1280x480', kind: 'dash', width: 1280, height: 480, file: 'OpenDash 1280x480.simhubdash' },
    { folder: 'OpenDash Pit wall', kind: 'pitwall', width: 1920, height: 1080, file: 'OpenDash Pit wall.simhubdash' },
    { folder: 'OpenDash Companion portrait', kind: 'companion', width: 480, height: 850, file: 'OpenDash Companion portrait.simhubdash' },
    { folder: 'OpenDash 480 round', kind: 'dash', width: 480, height: 480, file: 'OpenDash 480 round.simhubdash' },
  ];
  const settings = panelRig({ packages });
  const rig = settings.Rig as Record<string, unknown>[];
  const bars = settings.LedBars as Record<string, unknown>[];

  test('writes only fields the plugin reads', () => {
    const fields = {
      settings: csharpFields('OpenDashSettings.cs', 'OpenDashSettings'),
      screen: csharpFields('ScreenInstance.cs', 'ScreenInstance'),
      face: csharpFields('FaceSettings.cs', 'FaceSettings'),
      bar: csharpFields('LedBar.cs', 'LedBar'),
    };
    const unread = (keys: string[], known: Set<string>) => keys.filter((k) => !known.has(k));
    expect(unread(Object.keys(settings), fields.settings)).toEqual([]);
    for (const screen of rig) {
      expect({ screen: screen.Name, unread: unread(Object.keys(screen), fields.screen) }).toEqual({ screen: screen.Name, unread: [] });
      if (screen.Face) expect(unread(Object.keys(screen.Face as object), fields.face)).toEqual([]);
    }
    for (const bar of bars) expect(unread(Object.keys(bar), fields.bar)).toEqual([]);
  });

  test('holds five screens of every kind, by the names the panel shows', () => {
    expect(rig.map((s) => `${s.Name} ${s.Kind} ${s.Width}x${s.Height}`)).toEqual([
      `Main dash ${contractString('KindFace')} 1280x480`,
      `Rim ${contractString('KindFace')} 1280x480`,
      `Pit wall ${contractString('KindPitWall')} 1920x1080`,
      `Phone ${contractString('KindCompanion')} 480x850`,
      `Round ${contractString('KindSlots')} 480x480`,
    ]);
  });

  test('gives Main dash the stock namespace and folder, and Rim its own of each', () => {
    const [main, rim] = rig;
    expect([main!.Namespace, main!.Folder]).toEqual(['Face1280x480', 'OpenDash 1280x480']);
    expect([rim!.Namespace, rim!.Folder]).toEqual(['Rim', RIM_FOLDER]);
    // What PackageCatalogue.UniqueFolder gives a second screen called Rim, and so the folder the
    // plugin writes and the preset deletes.
    expect(RIM_FOLDER).toBe('OpenDash Rim');
    expect(new Set(rig.map((s) => (s.Namespace as string).toLowerCase())).size).toBe(rig.length);
    expect(new Set(rig.map((s) => (s.Folder as string).toLowerCase())).size).toBe(rig.length);
  });

  test('takes the stock namespace for every kind it holds one of', () => {
    expect(rig.slice(2).map((s) => s.Namespace)).toEqual([contractString('PitWallPrefix'), contractString('CompanionPrefix'), 'Slots480x480']);
  });

  test("cycles Rim's zone B through six pages from lap times and zone C through four from the relative", () => {
    const face = rig[1]!.Face as { Zones: number[]; Masks: number[]; Starts: number[] };
    const bits = (mask: number) => MODULE_CATALOGUE.filter((m) => (mask & (1 << (m.number - 1))) !== 0).map((m) => m.id);
    expect(bits(face.Masks[1]!)).toEqual([...RIM_ZONE_B].sort((a, b) => MODULE_CATALOGUE.findIndex((m) => m.id === a) - MODULE_CATALOGUE.findIndex((m) => m.id === b)));
    expect(bits(face.Masks[1]!)).toHaveLength(6);
    expect(bits(face.Masks[2]!)).toHaveLength(4);
    expect(face.Zones[1]).toBe(MODULE_CATALOGUE.find((m) => m.id === 'lapTimes')!.number - 1);
    expect(face.Zones[2]).toBe(MODULE_CATALOGUE.find((m) => m.id === 'relative')!.number - 1);
    expect(face.Starts).toEqual(face.Zones);
    expect(face.Masks[2]).toBe(catalogueMask(RIM_ZONE_C));
    const off = new Set(MODULE_CATALOGUE.filter((m) => !m.enabled).map((m) => m.id));
    expect([...RIM_ZONE_B, ...RIM_ZONE_C].filter((id) => off.has(id))).toEqual([]);
  });

  test('puts a Fanatec wheel rim and a plain fifteen-LED brow on the Arduino device', () => {
    expect(bars.map((b) => [b.Name, b.Namespace, b.Shape, b.Device])).toEqual([
      ['Wheel rim', 'LedWheelRim', RIM_SHAPE, 'arduino'],
      ['Dash brow', 'LedDashBrow', browShape(), 'arduino'],
    ]);
    expect(shapeById(RIM_SHAPE)?.positions).toBeDefined();
    const brow = shapeById(browShape())!;
    expect([stripLength(brow), brow.positions]).toEqual([15, undefined]);
  });

  test('names two matrix panels, the flag box on both sides at gear and a left pillar dark', () => {
    const slot = (i: number) =>
      [settings.FlagBoxMatrixName, settings.FlagBoxSide, settings.FlagBoxRest, settings.FlagBoxFlags].map((a) => (a as unknown[])[i]);
    expect([0, 1, 2, 3].map(slot)).toEqual([
      ['Flag box', 'both', 'gear', true],
      ['Left pillar', 'left', 'dark', true],
      [null, 'both', 'dark', false],
      [null, 'both', 'dark', false],
    ]);
    const sides = contractString('DefaultFlagBoxSide');
    expect(sides).toBe('both');
  });

  test.if(manifest !== null)('names only packages the build produced', () => {
    expect((panelRig(manifest!).Rig as unknown[]).length).toBe(5);
  });
});
