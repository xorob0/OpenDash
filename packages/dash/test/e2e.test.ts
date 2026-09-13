/**
 * End to end: run the build into a temporary directory and inspect what SimHub would import:
 * one folder and zip per layout (folder names with spaces included), the manifest, `$type`
 * first on every item, every colour as #AARRGGBB, sidecars, fonts, widget references, both
 * strategies, reproducibility, and the validation gate.
 */
import { afterAll, beforeAll, describe, expect, test } from 'bun:test';
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';
import {
  build,
  BuildError,
  DEFAULT_OUT_DIR,
  main,
  MANIFEST_FILE,
  PANEL_FONTS_DIR,
  parseArgs,
  readVersion,
  validateOrThrow,
  validateProfileOrThrow,
  type BuildResult,
} from '../src/build.ts';
import { CARD_CATALOGUE, defaultCardForSlot } from '../src/contract.ts';
import { buildPackage, FACE_FONT_FILES } from '../src/dashboard.ts';
import { fontsForPanel, needsRename, renameFamily, renamedFileName } from '../src/design/fontFiles.ts';
import { FONT_LICENCE } from '../src/design/notices.ts';
import { FONTS_DIR, isGuid, isNormalisedHex, ITEM_TYPES, leds, listFiles, PACKAGE_EXTENSION, readZip, stableGuid } from '../src/generator.ts';
import { layout1920x480 } from '../src/layouts/1920x480.ts';
import { LAYOUTS, rungOf, type Layout } from '../src/layouts/index.ts';
import { SCREEN_PACKAGES } from '../src/screens/index.ts';
import { ZONE_FACES, type ZoneLayout } from '../src/zones/index.ts';
import { CARDS_FILE } from '../src/slots.ts';
import { ALL_SHAPES, deviceLength } from '../src/leds/strip.ts';
import { rpmStripFileName, rpmStripProfileName } from '../src/leds/rpmStrip.ts';
import { SHIFT_RPM_PROPERTIES } from '../src/shift.ts';
import { ds } from '../src/tokens.ts';

type Json = string | number | boolean | null | Json[] | { [key: string]: Json };
interface JsonItem {
  [key: string]: Json;
}

let root: string;
let widget: BuildResult;
let inline: BuildResult;
let second: BuildResult;
const log: string[] = [];

beforeAll(() => {
  root = mkdtempSync(join(tmpdir(), 'opendash-e2e-'));
  // The faces and the second screens are built into separate directories so each block can assert
  // on exactly what its own build wrote.
  widget = build({ out: join(root, 'widget'), screens: [], ledProfiles: [], log: (line) => log.push(line) });
  inline = build({ out: join(root, 'inline'), strategy: 'inline', screens: [], zoneFaces: [], ledProfiles: [], log: () => {} });
  second = build({ out: join(root, 'second'), layouts: [], zoneFaces: [], ledProfiles: [], log: () => {} });
});
afterAll(() => {
  rmSync(root, { recursive: true, force: true });
});

const FONT_FILES = [`${FONTS_DIR}/Barlow-Medium.ttf`, `${FONTS_DIR}/openDashDisplay-Bold.ttf`, `${FONTS_DIR}/openDashDisplay-SemiBold.ttf`];

const byCodeUnit = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/**
 * The files of a package folder as listFiles orders them (sorted by code unit). `OFL.txt` is in
 * every one of them because every one of them ships the Barlow faces, and the licence has to
 * travel with what it licenses; see packages/dash/src/design/notices.ts.
 */
const expectedFiles = (folder: string, cards = true): string[] =>
  [...FONT_FILES, FONT_LICENCE.name, ...(cards ? [CARDS_FILE, `${CARDS_FILE}.metadata`] : []), `${folder}.djson`, `${folder}.djson.metadata`].sort(byCodeUnit);

/** The reference card face, which since XOR-118 is named for its slots rather than "openDash". */
const REFERENCE_CARD_FACE = layout1920x480.folder;
const EXPECTED_FILES = expectedFiles(REFERENCE_CARD_FACE);
const FOLDERS = LAYOUTS.map((l) => l.folder);
/** The zone faces, which are built beside the card faces until the card path is retired. */
const ZONE_FOLDERS = ZONE_FACES.map((f) => f.folder);
const zipName = (folder: string): string => `${folder}${PACKAGE_EXTENSION}`;

const readJson = (file: string): JsonItem => JSON.parse(readFileSync(file, 'utf8')) as JsonItem;

/** Every item object of a parsed .djson, layers flattened, in document order. */
const itemsOfDocument = (doc: JsonItem): JsonItem[] => {
  const out: JsonItem[] = [];
  const walk = (items: Json): void => {
    if (!Array.isArray(items)) throw new Error('Items is not an array');
    for (const item of items) {
      if (typeof item !== 'object' || item === null || Array.isArray(item)) throw new Error('item is not an object');
      out.push(item);
      const children = item.Childrens;
      if (children !== undefined) walk(children);
    }
  };
  const screens = doc.Screens;
  if (!Array.isArray(screens)) throw new Error('Screens is not an array');
  for (const screen of screens) {
    if (typeof screen !== 'object' || screen === null || Array.isArray(screen)) throw new Error('screen is not an object');
    const items = screen.Items;
    if (items === undefined) throw new Error('screen has no Items');
    walk(items);
  }
  return out;
};

/** Every string value anywhere in a JSON tree, with the key it sits under. */
const stringsOf = (value: Json, key = '', out: { key: string; value: string }[] = []): { key: string; value: string }[] => {
  if (typeof value === 'string') out.push({ key, value });
  else if (Array.isArray(value)) value.forEach((v) => stringsOf(v, key, out));
  else if (value && typeof value === 'object') for (const [k, v] of Object.entries(value)) stringsOf(v, k, out);
  return out;
};

/** The five Json.NET type strings, widened so that any string can be checked against them. */
const TYPE_STRINGS: readonly string[] = Object.values(ITEM_TYPES);

const djsonFiles = (folder: string): string[] => listFiles(folder).filter((f) => f.endsWith('.djson')).map((f) => join(folder, f));

describe('widget build on disk', () => {
  test('writes a folder, its sidecars, the fonts and a zip per layout, plus the manifest', () => {
    expect(widget.packages.filter((p) => p.layout).map((p) => p.layout!.folder)).toEqual(FOLDERS);
    expect(widget.packages.filter((p) => p.zoneFace).map((p) => p.zoneFace!.folder)).toEqual(ZONE_FOLDERS);
    for (const p of widget.packages.filter((x) => x.layout)) {
      const folder = join(widget.out, p.layout!.folder);
      expect(p.written.folder).toBe(folder);
      expect(listFiles(folder)).toEqual(expectedFiles(p.layout!.folder));
      expect(existsSync(join(widget.out, zipName(p.layout!.folder)))).toBe(true);
    }
    expect(listFiles(join(widget.out, REFERENCE_CARD_FACE))).toEqual(EXPECTED_FILES);
    expect(existsSync(join(widget.out, MANIFEST_FILE))).toBe(true);
    expect(readdirSync(widget.out).sort()).toEqual([MANIFEST_FILE, PANEL_FONTS_DIR, ...FOLDERS, ...ZONE_FOLDERS, ...FOLDERS.map(zipName), ...ZONE_FOLDERS.map(zipName)].sort());
    // The panel's fonts sit beside the packages rather than in one, because the plugin embeds them
    // and its build never runs this one; see plugin/OpenDash/OpenDash.csproj.
    expect(readdirSync(join(widget.out, PANEL_FONTS_DIR)).sort()).toEqual([...fontsForPanel().map((f) => basename(f)), FONT_LICENCE.name].sort());
  });

  test('folder names with spaces are written and zipped as they are', () => {
    const spaced = LAYOUTS.filter((l) => l.folder.includes(' '));
    expect(spaced.length).toBeGreaterThan(0);
    for (const layout of spaced) {
      const folder = join(widget.out, layout.folder);
      expect(existsSync(join(folder, `${layout.folder}.djson`))).toBe(true);
      expect(existsSync(join(folder, `${layout.folder}.djson.metadata`))).toBe(true);
      const zipped = widget.packages.find((p) => p.layout === layout)!.zipped;
      expect(zipped.path).toBe(join(widget.out, zipName(layout.folder)));
      expect(zipped.entries.every((e) => e.startsWith(`${layout.folder}/`))).toBe(true);
    }
  });

  test('the manifest records version, SimHub version and every package with its slot count and rung', () => {
    const manifest = readJson(join(widget.out, MANIFEST_FILE));
    const entry = (l: Layout): JsonItem => ({ folder: l.folder, kind: 'dash', width: l.width, height: l.height, slots: l.slots.length, rung: rungOf(l), file: zipName(l.folder) });
    // A zone face records no slots and no rung. A zone is not a slot, and reporting one as twelve
    // would tell the plugin to draw twelve dropdowns for a face that has four zones.
    const zoneEntry = (f: ZoneLayout): JsonItem => ({ folder: f.folder, kind: 'dash', width: f.width, height: f.height, slots: 0, file: zipName(f.folder) });
    expect(manifest).toEqual({ version: readVersion(), simHubVersion: '9.12.6', packages: [...LAYOUTS.map(entry), ...ZONE_FACES.map(zoneEntry)], profiles: [] });
    for (const face of ZONE_FACES) {
      const row = (manifest.packages as JsonItem[]).find((p) => p.folder === face.folder)!;
      expect(row).toMatchObject({ slots: 0 });
      expect(Object.keys(row)).toEqual(['folder', 'kind', 'width', 'height', 'slots', 'file']);
    }
    // Written out rather than derived, because a rename is exactly what this should catch. Since
    // XOR-118 the card faces carry "slots" in their names and the zone faces carry the shipped ones;
    // the two round faces keep theirs, having no zone equivalent while XOR-94 is undecided.
    expect((manifest.packages as JsonItem[])[0]).toEqual({ folder: 'openDash slots 1920x480', kind: 'dash', width: 1920, height: 480, slots: 12, rung: 'L', file: 'openDash slots 1920x480.simhubdash' });
    expect(manifest.packages as JsonItem[]).toContainEqual({ folder: 'openDash slots 850x480', kind: 'dash', width: 850, height: 480, slots: 6, rung: 'M', file: 'openDash slots 850x480.simhubdash' });
    expect(manifest.packages as JsonItem[]).toContainEqual({ folder: 'openDash 480 round', kind: 'dash', width: 480, height: 480, slots: 2, rung: 'S', file: 'openDash 480 round.simhubdash' });
    expect(manifest.packages as JsonItem[]).toContainEqual({ folder: 'openDash slots 1280x480', kind: 'dash', width: 1280, height: 480, slots: 8, rung: 'M', file: 'openDash slots 1280x480.simhubdash' });
    expect(manifest.packages as JsonItem[]).toContainEqual({ folder: 'openDash slots 1280x400', kind: 'dash', width: 1280, height: 400, slots: 8, rung: 'M', file: 'openDash slots 1280x400.simhubdash' });
    expect(manifest.packages as JsonItem[]).toContainEqual({ folder: 'openDash slots 800x480', kind: 'dash', width: 800, height: 480, slots: 6, rung: 'M', file: 'openDash slots 800x480.simhubdash' });
    expect(manifest.packages as JsonItem[]).toContainEqual({ folder: 'openDash slots 1280x720', kind: 'dash', width: 1280, height: 720, slots: 12, rung: 'M', file: 'openDash slots 1280x720.simhubdash' });
    expect(manifest.packages as JsonItem[]).toContainEqual({ folder: 'openDash slots 800x286', kind: 'dash', width: 800, height: 286, slots: 4, rung: 'M', file: 'openDash slots 800x286.simhubdash' });
    expect(manifest.packages as JsonItem[]).toContainEqual({ folder: 'openDash slots 600x686', kind: 'dash', width: 600, height: 686, slots: 6, rung: 'M', file: 'openDash slots 600x686.simhubdash' });
    expect(manifest.packages as JsonItem[]).toContainEqual({ folder: 'openDash 800 round', kind: 'dash', width: 800, height: 800, slots: 6, rung: 'M', file: 'openDash 800 round.simhubdash' });
    expect((manifest.packages as JsonItem[]).map((p) => p.folder)).toEqual([
      'openDash slots 1920x480',
      'openDash slots 1280x480',
      'openDash slots 1280x400',
      'openDash slots 850x480',
      'openDash slots 800x480',
      'openDash slots 1280x720',
      'openDash slots 800x286',
      'openDash slots 600x686',
      'openDash 480 round',
      'openDash 800 round',
      // The zone faces follow the card faces in the manifest, in the same order LAYOUTS lists them,
      // so the two halves read alike for as long as both exist. They are the ones a user installs.
      'openDash',
      'openDash 1280x480',
      'openDash 1280x400',
      'openDash 850x480',
      'openDash 800x480',
      'openDash 1280x720',
      'openDash 800x286',
      'openDash 600x686',
    ]);
    expect((manifest.packages as JsonItem[]).map((p) => p.folder).slice(10)).toEqual(ZONE_FACES.map((f) => f.folder));
    expect(manifest).toEqual(widget.manifest as unknown as JsonItem);
    // `profiles` is its own list rather than a package kind: a profile has no width, no height and
    // no slots, and is installed against a device rather than into DashTemplates. ADR 0013.
    expect(Object.keys(manifest)).toEqual(['version', 'simHubVersion', 'packages', 'profiles']);
    // Every card face carries a rung; a zone face does not, because it has no cards to size.
    for (const p of manifest.packages as JsonItem[]) {
      const keys = ['folder', 'kind', 'width', 'height', 'slots', ...(p.rung === undefined ? [] : ['rung']), 'file'];
      expect({ folder: p.folder, keys: Object.keys(p) }).toMatchObject({ keys });
    }
    expect(readFileSync(widget.manifestPath, 'utf8').endsWith('\n')).toBe(true);
  });

  test('each zip lists <folder>/… entries, sorted, matching the files on disk', () => {
    for (const p of widget.packages) {
      const folder = p.pkg.folderName;
      const bytes = readFileSync(p.zipped.path);
      expect(Buffer.compare(bytes, p.zipped.bytes)).toBe(0);
      // A card face carries cards.djson; a zone face carries one dashboard per distinct zone
      // rectangle and catalogue, which the package itself is the list of.
      const expected = [
        ...FONT_FILES,
        FONT_LICENCE.name,
        ...p.pkg.dashboards.flatMap((d) => [`${d.name}.djson`, `${d.name}.djson.metadata`]),
      ]
        .sort(byCodeUnit)
        .map((f) => `${folder}/${f}`);
      expect(p.zipped.entries).toEqual(expected);
      const unzipped = readZip(new Uint8Array(bytes));
      expect(Object.keys(unzipped).sort(byCodeUnit)).toEqual(expected);
      for (const entry of expected) {
        const rel = entry.slice(folder.length + 1);
        expect(Buffer.compare(Buffer.from(unzipped[entry]!), readFileSync(join(widget.out, folder, rel)))).toBe(0);
      }
    }
  });

  /**
   * The two condensed faces are not byte-identical to `fonts/` any more, because a package ships
   * them under a family WPF will not fold into Barlow. What has to hold is that the difference is
   * exactly the rename and nothing else, which is checked by doing the rename here and comparing.
   */
  test('the bundled fonts are the three face fonts as fonts/ holds them, renamed, in every package', () => {
    for (const folder of FOLDERS) {
      const fontsDir = join(widget.out, folder, FONTS_DIR);
      expect(readdirSync(fontsDir).sort()).toEqual(FACE_FONT_FILES.map(renamedFileName).sort());
      for (const f of FACE_FONT_FILES) {
        const source = new Uint8Array(readFileSync(join(import.meta.dir, '..', 'fonts', f)));
        expect(renameFamily(source) > 0).toBe(needsRename(f));
        expect(Buffer.compare(readFileSync(join(fontsDir, renamedFileName(f))), Buffer.from(source))).toBe(0);
      }
    }
  });

  test('every sidecar equals the Metadata key and carries the version; titles are the folder names', () => {
    for (const layout of LAYOUTS) {
      const folder = join(widget.out, layout.folder);
      for (const file of djsonFiles(folder)) {
        const doc = readJson(file);
        const sidecar = readJson(`${file}.metadata`);
        expect(sidecar).toEqual(doc.Metadata as JsonItem);
        expect(sidecar.DashboardVersion).toBe(readVersion());
        expect(sidecar.SimHubVersion).toBe('9.12.6');
        expect(sidecar.Author).toBe('OpenDash contributors');
        expect(sidecar.MetadataVersion).toBe(2);
      }
      expect(readJson(join(folder, `${layout.folder}.djson.metadata`))).toMatchObject({ Title: layout.folder, Description: layout.description, Width: layout.width, Height: layout.height, ScreenCount: 1 });
      expect(readJson(join(folder, `${CARDS_FILE}.metadata`))).toMatchObject({ Title: `${layout.folder} cards`, Width: layout.slotSize.width, Height: layout.slotSize.height, ScreenCount: CARD_CATALOGUE.length });
    }
    // The reference card face, which the zone face has taken the plain name from.
    expect(readJson(join(widget.out, REFERENCE_CARD_FACE, `${REFERENCE_CARD_FACE}.djson.metadata`))).toMatchObject({ Title: REFERENCE_CARD_FACE, Description: '1920 x 480, 12 slots', Width: 1920, Height: 480 });
    // And the zone face now under it, which is what a user installing "openDash" gets.
    expect(readJson(join(widget.out, 'openDash', 'openDash.djson.metadata'))).toMatchObject({ Title: 'openDash', Width: 1920, Height: 480 });
    expect(readJson(join(widget.out, 'openDash 480 round', 'openDash 480 round.djson.metadata'))).toMatchObject({ Description: '2 slots, round', Width: 480, Height: 480 });
    expect(readJson(join(widget.out, 'openDash 800 round', 'openDash 800 round.djson.metadata'))).toMatchObject({ Description: '6 slots, round', Width: 800, Height: 800 });
  });

  test('the build log names every file written', () => {
    const files = [...FOLDERS.flatMap((folder) => expectedFiles(folder).map((f) => `${folder}/${f}`)), ...FOLDERS.map(zipName), MANIFEST_FILE];
    for (const rel of files) expect({ rel, logged: log.some((line) => line.startsWith('wrote ') && line.includes(rel)) }).toEqual({ rel, logged: true });
    expect(log.some((line) => line.startsWith('warning '))).toBe(false);
  });
});

describe('the emitted JSON', () => {
  const documents = (): { file: string; doc: JsonItem; text: string }[] =>
    [widget, inline].flatMap((r) => FOLDERS.flatMap((folder) => djsonFiles(join(r.out, folder)).map((file) => ({ file, doc: readJson(file), text: readFileSync(file, 'utf8') }))));

  test('has $type as the first key of every item, one of the five kinds, with unique GUID ids', () => {
    for (const { file, doc } of documents()) {
      const items = itemsOfDocument(doc);
      expect(items.length).toBeGreaterThan(0);
      const ids = new Set<string>();
      for (const item of items) {
        expect({ file, name: item.Name, firstKey: Object.keys(item)[0] }).toEqual({ file, name: item.Name, firstKey: '$type' });
        expect(TYPE_STRINGS).toContain(item.$type as string);
        expect(isGuid(item.Id)).toBe(true);
        expect(ids.has(item.Id as string)).toBe(false);
        ids.add(item.Id as string);
      }
    }
  });

  test('the round faces write ellipses and rotated rectangles; Rotation follows Height and is never 0; the rectangular faces write neither', () => {
    const round = LAYOUTS.filter((l) => l.shape === 'round');
    expect(round.map((l) => l.folder)).toEqual(['openDash 480 round', 'openDash 800 round']);
    for (const layout of round) {
      const doc = readJson(join(widget.out, layout.folder, `${layout.folder}.djson`));
      const items = itemsOfDocument(doc);
      const ellipses = items.filter((i) => i.$type === ITEM_TYPES.ellipse);
      expect(ellipses).toHaveLength(5);
      for (const e of ellipses) {
        expect(Object.keys(e).slice(0, 4)).toEqual(['$type', 'FillColor', 'EllipseColor', 'EllipseThickness']);
        expect(e.FillColor).toBe('#00FFFFFF');
        expect(e.EllipseThickness === 12 || e.EllipseThickness === 3).toBe(true);
      }
      const rotated = items.filter((i) => 'Rotation' in i);
      // 14 rev segments per layer (the one at the top is not rotated) and 23 checks (the one at the top is not).
      // Three rev layers since ADR 0014: the car's own ladder, SimHub's bands, and the plain RPM bar.
      expect(rotated).toHaveLength(14 * 3 + 23);
      for (const r of rotated) {
        const keys = Object.keys(r);
        expect(keys.indexOf('Rotation')).toBe(keys.indexOf('Height') + 1);
        expect(r.Rotation).not.toBe(0);
      }
    }
    for (const layout of LAYOUTS.filter((l) => l.shape === 'rect')) {
      const items = itemsOfDocument(readJson(join(widget.out, layout.folder, `${layout.folder}.djson`)));
      expect(items.some((i) => 'Rotation' in i || i.$type === ITEM_TYPES.ellipse)).toBe(false);
    }
  });

  test('writes every colour as #AARRGGBB', () => {
    for (const { file, doc, text } of documents()) {
      const colours = stringsOf(doc).filter((s) => s.value.startsWith('#'));
      expect(colours.length).toBeGreaterThan(0);
      for (const c of colours) expect({ file, ...c, ok: isNormalisedHex(c.value) }).toEqual({ file, ...c, ok: true });
      for (const m of text.matchAll(/"(#[0-9A-Za-z]*)"/g)) expect(m[1]).toMatch(/^#[0-9A-F]{8}$/);
      for (const key of ['BackgroundColor', 'TextColor', 'BorderColor', 'StartColor', 'EndColor', 'MiddleColor', 'FillColor', 'EllipseColor']) {
        for (const c of colours.filter((s) => s.key === key)) expect(c.value).toMatch(/^#[0-9A-F]{8}$/);
      }
    }
  });

  test('carries no brand colour and no unresolved token alias', () => {
    for (const { text } of documents()) {
      expect(text).not.toMatch(/#(33D9F2|5CE1F5|22909F)/i);
      expect(text).not.toMatch(/\{[a-z]+\.[a-z.]+\}/i);
    }
  });

  test('the widget strategy embeds cards.djson in one slot per layout slot with valid screen indexes', () => {
    for (const layout of LAYOUTS) {
      const folder = join(widget.out, layout.folder);
      const main = readJson(join(folder, `${layout.folder}.djson`));
      const cards = readJson(join(folder, CARDS_FILE));
      const screens = cards.Screens as JsonItem[];
      expect(screens.map((s) => s.Name)).toEqual(CARD_CATALOGUE.map((c) => c.id));
      const widgets = itemsOfDocument(main).filter((i) => i.$type === ITEM_TYPES.widget);
      expect(widgets).toHaveLength(layout.slots.length);
      widgets.forEach((w, i) => {
        expect(w.FileName).toBe(CARDS_FILE);
        expect(w.InitialScreenIndex).toBe(defaultCardForSlot(i + 1));
        expect(w.AutoSize).toBe(true);
        expect(w.BackgroundColor).toBe('#00FFFFFF');
        const binding = (w.Bindings as JsonItem).InitialScreenIndex as JsonItem;
        expect((binding.Formula as JsonItem).Expression).toBe(`isnull([OpenDash.Slot${String(i + 1).padStart(2, "0")}], ${defaultCardForSlot(i + 1)})`);
        expect(binding.Mode).toBe(2);
      });
      expect((main.Screens as JsonItem[])[0]!.Name).toBe('Main');
      expect(main.BaseWidth).toBe(layout.width);
      expect(main.BaseHeight).toBe(layout.height);
      expect(cards.BaseWidth).toBe(layout.slotSize.width);
      expect(cards.BaseHeight).toBe(layout.slotSize.height);
    }
  });
});

describe('inline strategy', () => {
  test('builds a single dashboard per layout with no widget and no cards.djson', () => {
    for (const [i, layout] of LAYOUTS.entries()) {
      const folder = join(inline.out, layout.folder);
      expect(listFiles(folder)).toEqual(expectedFiles(layout.folder, false));
      expect(inline.packages[i]!.zipped.entries).toEqual(expectedFiles(layout.folder, false).map((f) => `${layout.folder}/${f}`));
      const items = itemsOfDocument(readJson(join(folder, `${layout.folder}.djson`)));
      expect(items.some((i) => i.$type === ITEM_TYPES.widget)).toBe(false);
      const slots = items.filter((i) => i.$type === ITEM_TYPES.layer && /^Slot\d\d$/.test(String(i.Name)));
      expect(slots).toHaveLength(layout.slots.length);
      expect(inline.manifest.packages[i]!.slots).toBe(layout.slots.length);
    }
    expect(inline.strategy).toBe('inline');
  });
});

describe('reproducibility', () => {
  test('two builds produce byte-identical packages', () => {
    const again = build({ out: join(root, 'again'), screens: [], ledProfiles: [], log: () => {} });
    expect(again.packages).toHaveLength(widget.packages.length);
    again.packages.forEach((p, i) => expect(Buffer.compare(p.zipped.bytes, widget.packages[i]!.zipped.bytes)).toBe(0));
    expect(readFileSync(again.manifestPath, 'utf8')).toBe(readFileSync(widget.manifestPath, 'utf8'));
  });

  test('the second screens are reproducible too', () => {
    const again = build({ out: join(root, 'againSecond'), layouts: [], zoneFaces: [], ledProfiles: [], log: () => {} });
    expect(again.packages).toHaveLength(second.packages.length);
    again.packages.forEach((p, i) => expect(Buffer.compare(p.zipped.bytes, second.packages[i]!.zipped.bytes)).toBe(0));
  });
});

describe('LED profiles on disk', () => {
  // Built on its own, because every other fixture in this file passes ledProfiles: [].
  let lit: BuildResult;
  beforeAll(() => {
    lit = build({ out: join(root, 'leds'), layouts: [], zoneFaces: [], screens: [], log: () => {} });
  });

  test('writes one .ledsprofile per strip shape, and nothing that looks like a package', () => {
    expect(lit.profiles.map((p) => p.shape.id)).toEqual(ALL_SHAPES.map((s) => s.id));
    expect(readdirSync(lit.out).sort()).toEqual([MANIFEST_FILE, PANEL_FONTS_DIR, ...ALL_SHAPES.map((s) => `${rpmStripFileName(s)}.ledsprofile`)].sort());
    // A profile is not a dashboard: no folder, no .djson, no zip.
    expect(readdirSync(lit.out).filter((f) => f.endsWith('.simhubdash'))).toEqual([]);
  });

  test('the manifest records each profile with the strip it is for', () => {
    const manifest = readJson(lit.manifestPath) as unknown as { profiles: JsonItem[]; packages: JsonItem[] };
    expect(manifest.packages).toEqual([]);
    expect(manifest.profiles.map((p) => p.name)).toEqual(ALL_SHAPES.map(rpmStripProfileName));
    for (const row of manifest.profiles) expect(Object.keys(row)).toEqual(['name', 'shape', 'leds', 'file']);
    expect(manifest.profiles.map((p) => p.leds)).toEqual(ALL_SHAPES.map(deviceLength));
  });

  test('every profile is the JSON SimHub reads, and every ContainerType is one it resolves', () => {
    for (const { shape, path: file } of lit.profiles) {
      const doc = JSON.parse(readFileSync(file, 'utf8')) as { Name: string; ProfileId: string; LedContainers: unknown[] };
      expect(doc.Name).toBe(rpmStripProfileName(shape));
      expect(doc.ProfileId).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
      const types: string[] = [];
      const walk = (c: Record<string, unknown>): void => {
        types.push(c.ContainerType as string);
        for (const k of (c.LedContainers as Record<string, unknown>[]) ?? []) walk(k);
      };
      for (const c of doc.LedContainers as Record<string, unknown>[]) walk(c);
      expect(types.length).toBeGreaterThan(0);
      for (const t of types) expect({ shape: shape.id, type: t, known: leds.KNOWN_CONTAINER_TYPES.has(t) }).toMatchObject({ known: true });
    }
  });

  test('the strip reads the same shift thresholds the rev bar does, so the two cannot disagree', () => {
    const text = readFileSync(lit.profiles.find((p) => p.shape.id === '4-14-4')!.path, 'utf8');
    // The one definition in src/shift.ts reaches both artefacts; if it ever forked, this is what says so.
    for (const name of Object.values(SHIFT_RPM_PROPERTIES)) expect({ name, inProfile: text.includes(name) }).toMatchObject({ inProfile: true });
    // ...and every colour on the strip is a token rather than a copy of one.
    expect(text).toContain(ds.purpose.shift.stage1);
    expect(text).toContain(ds.purpose.shift.stage3);
  });

  test('a profile whose effect runs off the end of the strip fails the build rather than going dark', () => {
    // The LED half of the text-fit rule: nothing complains at runtime, the light is simply not there.
    // A generated shape always fits itself — deviceLength is derived from it — so this is the gate
    // the build puts every profile through, exercised with a profile that does overrun.
    const over = { name: 'too long', profileId: stableGuid('test/leds/over'), containers: [{ kind: 'staticColor' as const, ledCount: 9, color: '#FFFFFF', startPosition: 10 }] };
    expect(() => validateProfileOrThrow(over, 15)).toThrow(/off-strip/);
    expect(validateProfileOrThrow({ ...over, containers: [{ kind: 'staticColor', ledCount: 6, color: '#FFFFFF', startPosition: 10 }] }, 15)).toEqual([]);
  });
});

describe('second screens on disk', () => {
  // The pit wall wordmark sets "open" in Barlow Condensed Light, which the face does not use.
  const SECOND_FONTS = ['Barlow-Medium.ttf', 'openDashDisplay-Bold.ttf', 'openDashDisplay-Light.ttf', 'openDashDisplay-SemiBold.ttf'].map((f) => `_SHFonts/${f}`);

  test('writes a folder and a zip per companion and pit wall', () => {
    expect(second.packages.map((p) => p.pkg.folderName)).toEqual(SCREEN_PACKAGES.map((s) => s.folder));
    expect(readdirSync(second.out).sort()).toEqual(
      [MANIFEST_FILE, PANEL_FONTS_DIR, ...SCREEN_PACKAGES.map((s) => s.folder), ...SCREEN_PACKAGES.map((s) => zipName(s.folder))].sort(),
    );
  });

  test('each package carries its main dashboard, its zone dashboards and the four fonts', () => {
    for (const p of second.packages) {
      const folder = join(second.out, p.pkg.folderName);
      const files = listFiles(folder);
      for (const font of SECOND_FONTS) expect(files).toContain(font);
      for (const dashboard of p.pkg.dashboards) {
        expect(files).toContain(`${dashboard.name}.djson`);
        expect(files).toContain(`${dashboard.name}.djson.metadata`);
      }
      // A companion is one dashboard; a pit wall page embeds its zones as separate ones.
      expect(p.pkg.dashboards.length).toBeGreaterThanOrEqual(p.kind === 'companion' ? 1 : 2);
    }
  });

  test('the manifest records the kind and no rung, because a second screen has no cards', () => {
    const manifest = readJson(join(second.out, MANIFEST_FILE));
    const entries = manifest.packages as JsonItem[];
    expect(entries.map((e) => e.folder)).toEqual(SCREEN_PACKAGES.map((s) => s.folder));
    expect(entries.map((e) => e.kind)).toEqual(['companion', 'companion', 'pitwall', 'pitwall']);
    for (const entry of entries) {
      expect(entry.slots).toBe(0);
      expect(Object.keys(entry)).toEqual(['folder', 'kind', 'width', 'height', 'slots', 'file']);
    }
    expect(entries[0]).toEqual({ folder: 'openDash Companion', kind: 'companion', width: 850, height: 480, slots: 0, file: 'openDash Companion.simhubdash' });
  });

  test('every written .djson names its types first and normalises its colours', () => {
    for (const p of second.packages) {
      for (const file of djsonFiles(join(second.out, p.pkg.folderName))) {
        const doc = readJson(file);
        for (const item of itemsOfDocument(doc)) {
          expect(Object.keys(item)[0]).toBe('$type');
          expect(TYPE_STRINGS).toContain(item.$type as string);
        }
        for (const { key, value } of stringsOf(doc)) {
          if (!key.toLowerCase().includes('color')) continue;
          expect({ file, key, value, normalised: isNormalisedHex(value) }).toMatchObject({ normalised: true });
        }
      }
    }
  });
});

describe('validation gate', () => {
  test('a package that fails validation throws and writes nothing', () => {
    const rules = layout1920x480.rules;
    const broken = { ...layout1920x480, rules: [...rules, rules[0]!] };
    const out = join(root, 'broken');
    let error: unknown;
    try {
      build({ out, layouts: [broken], log: () => {} });
    } catch (e) {
      error = e;
    }
    expect(error).toBeInstanceOf(BuildError);
    expect((error as BuildError).issues.map((i) => i.code)).toContain('name/duplicate');
    expect(existsSync(out)).toBe(false);
  });

  test('an undeclared OpenDash property is an error', () => {
    const pkg = buildPackage(layout1920x480, { version: '0.0.0-test' });
    const first = pkg.dashboards[0]!.screens[0]!.items[0]!;
    first.bindings = { Visible: { mode: 'formula', formula: 'isnull([OpenDash.Nope], true)' } };
    expect(() => validateOrThrow(pkg)).toThrow(/OpenDash\.Nope/);
    try {
      validateOrThrow(pkg);
    } catch (e) {
      expect((e as BuildError).issues.map((i) => i.code)).toEqual(['property/undeclared']);
    }
  });

  test('every clean package validates with no warnings', () => {
    for (const layout of LAYOUTS) {
      expect(validateOrThrow(buildPackage(layout, { version: '0.0.0-test' }))).toEqual([]);
      expect(validateOrThrow(buildPackage(layout, { version: '0.0.0-test', strategy: 'inline' }))).toEqual([]);
    }
  });

  test('two layouts sharing a folder are refused', () => {
    expect(() => build({ out: join(root, 'twice'), layouts: [layout1920x480, layout1920x480], log: () => {} })).toThrow(new RegExp(`folder "${layout1920x480.folder}"`));
  });
});

describe('command line', () => {
  const env = {};
  test('defaults to <repo>/build and the widget strategy', () => {
    expect(parseArgs([], env)).toEqual({ out: DEFAULT_OUT_DIR, strategy: 'widget', help: false });
    expect(DEFAULT_OUT_DIR.endsWith('/build')).toBe(true);
  });

  test('--out resolves against the working directory, --strategy selects the strategy', () => {
    expect(parseArgs(['--out', 'dist', '--strategy', 'inline'], env, '/work')).toEqual({ out: '/work/dist', strategy: 'inline', help: false });
    expect(parseArgs(['--out=dist/x', '--strategy=widget'], env, '/work').out).toBe('/work/dist/x');
    expect(parseArgs(['-o', '/abs', '-s', 'INLINE'], env)).toMatchObject({ out: '/abs', strategy: 'inline' });
    expect(parseArgs(['--help'], env).help).toBe(true);
  });

  test('SLOT_STRATEGY is the fallback and the flag wins', () => {
    expect(parseArgs([], { SLOT_STRATEGY: 'inline' }).strategy).toBe('inline');
    expect(parseArgs(['--strategy', 'widget'], { SLOT_STRATEGY: 'inline' }).strategy).toBe('widget');
    expect(parseArgs([], { SLOT_STRATEGY: '' }).strategy).toBe('widget');
    expect(() => parseArgs([], { SLOT_STRATEGY: 'bogus' })).toThrow(/SLOT_STRATEGY/);
  });

  test('bad arguments are rejected', () => {
    expect(() => parseArgs(['--strategy', 'bogus'], env)).toThrow(/unknown strategy/);
    expect(() => parseArgs(['--out'], env)).toThrow(/needs a value/);
    expect(() => parseArgs(['--nope'], env)).toThrow(/unknown argument/);
  });

  test('main returns 2 for bad arguments and 1 for a failed build', () => {
    const errors: string[] = [];
    const out: string[] = [];
    const original = { error: console.error, log: console.log };
    console.error = (...args: unknown[]): void => {
      errors.push(args.map(String).join(' '));
    };
    console.log = (...args: unknown[]): void => {
      out.push(args.map(String).join(' '));
    };
    try {
      expect(main(['--strategy', 'bogus'])).toBe(2);
      expect(main(['--help'])).toBe(0);
    } finally {
      console.error = original.error;
      console.log = original.log;
    }
    expect(errors[0]).toMatch(/unknown strategy/);
    expect(out[0]).toMatch(/^usage: bun run build/);
  });

  test('readVersion reads VERSION and rejects junk', () => {
    expect(readVersion()).toMatch(/^\d+\.\d+\.\d+/);
    expect(readVersion()).toBe(readFileSync(join(import.meta.dir, '..', '..', '..', 'VERSION'), 'utf8').trim());
    expect(() => readVersion(join(root, 'missing'))).toThrow(/cannot read/);
  });
});

/**
 * What the plugin embeds, it installs, so a package embedded by mistake appears in the dashboard
 * list of a user who asked for nothing. Since XOR-118 the zone faces carry the shipped names and
 * are what a user gets; the card faces are still built so that the two can be compared on a rig
 * before XOR-95 deletes the card path, and they are the ones that must not be installed.
 *
 * The direction of that exclusion inverted with the rename, which is the reason this test reads the
 * csproj rather than restating a list: a pattern left pointing at the old names would exclude
 * nothing and quietly embed everything, which is the shape of XOR-123.
 *
 * It is checked here rather than left to the packaging script because that is exactly how it went
 * wrong the first time: the rule lived only in `scripts/package.sh`, CI never runs that script, and
 * a release would have embedded all twenty-two.
 */
describe('what a released plugin embeds', () => {
  const csproj = readFileSync(join(import.meta.dir, '..', '..', '..', 'plugin', 'OpenDash', 'OpenDash.csproj'), 'utf8');
  const attrs = /<EmbeddedResource\s+Include="Resources\/\*\.simhubdash"([^>]*)\/>/.exec(csproj)?.[1] ?? '';
  const exclusion = /Exclude="([^"]+)"/.exec(attrs)?.[1] ?? '';

  /** A package folder matches an MSBuild glob of the `openDash slots *` shape. */
  const excludedBy = (pattern: string, folder: string): boolean =>
    new RegExp(`^${pattern.replace('Resources/', '').replace(/[.+^${}()|[\]\\]/g, '\\$&').replace(/\*/g, '.*')}$`).test(`${folder}.simhubdash`);

  test('the packages glob carries an exclusion', () => {
    expect(attrs).not.toBe('');
    expect(exclusion).not.toBe('');
  });

  test('every card face is excluded, and nothing else is', () => {
    expect(LAYOUTS.length).toBeGreaterThan(0);
    // The two round faces are the exception and are deliberate: they have no zone equivalent while
    // XOR-94 is undecided, so they are the only face at their size and a user who has one must keep
    // getting it. They keep the shipped name and are embedded.
    const rounds = LAYOUTS.filter((l) => l.folder.includes('round'));
    expect(rounds).toHaveLength(2);
    for (const layout of LAYOUTS.filter((l) => !l.folder.includes('round'))) {
      expect([layout.folder, excludedBy(exclusion, layout.folder)]).toEqual([layout.folder, true]);
    }
    for (const layout of rounds) expect([layout.folder, excludedBy(exclusion, layout.folder)]).toEqual([layout.folder, false]);
    for (const folder of ZONE_FOLDERS) expect([folder, excludedBy(exclusion, folder)]).toEqual([folder, false]);
    for (const screen of SCREEN_PACKAGES) expect([screen.folder, excludedBy(exclusion, screen.folder)]).toEqual([screen.folder, false]);
  });

  test('the packaging script copies everything and leaves the choice to the csproj', () => {
    // Both build paths have to apply one rule. CI hands the whole dash artefact over and never runs
    // this script, so a filter here that the csproj did not repeat would hold locally and not in a
    // release, which is the shape of the bug this replaced.
    const script = readFileSync(join(import.meta.dir, '..', '..', '..', 'scripts', 'package.sh'), 'utf8');
    expect(script).toContain('cp build/*.simhubdash plugin/OpenDash/Resources/');
    expect(script).not.toMatch(/^\s*case .*openDash slots/m);
  });
});
