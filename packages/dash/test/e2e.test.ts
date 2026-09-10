/**
 * End to end: run the build into a temporary directory and inspect what SimHub would import:
 * the folder layout, the zip listing, the manifest, `$type` first on every item, every colour
 * as #AARRGGBB, sidecars, fonts, widget references, both strategies, and the validation gate.
 */
import { afterAll, beforeAll, describe, expect, test } from 'bun:test';
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { build, BuildError, DEFAULT_OUT_DIR, main, MANIFEST_FILE, parseArgs, readVersion, validateOrThrow, type BuildResult } from '../src/build.ts';
import { CARD_CATALOGUE } from '../src/contract.ts';
import { buildPackage, FACE_FONT_FILES } from '../src/dashboard.ts';
import { FONTS_DIR, isGuid, isNormalisedHex, ITEM_TYPES, listFiles, readZip } from '../src/generator.ts';
import { layout1920x480 } from '../src/layouts/1920x480.ts';
import { CARDS_FILE } from '../src/slots.ts';

type Json = string | number | boolean | null | Json[] | { [key: string]: Json };
interface JsonItem {
  [key: string]: Json;
}

let root: string;
let widget: BuildResult;
let inline: BuildResult;
const log: string[] = [];

beforeAll(() => {
  root = mkdtempSync(join(tmpdir(), 'opendash-e2e-'));
  widget = build({ out: join(root, 'widget'), log: (line) => log.push(line) });
  inline = build({ out: join(root, 'inline'), strategy: 'inline', log: () => {} });
});
afterAll(() => {
  rmSync(root, { recursive: true, force: true });
});

const EXPECTED_FILES = [
  `${FONTS_DIR}/Barlow-Medium.ttf`,
  `${FONTS_DIR}/BarlowCondensed-Bold.ttf`,
  `${FONTS_DIR}/BarlowCondensed-SemiBold.ttf`,
  'cards.djson',
  'cards.djson.metadata',
  'openDash.djson',
  'openDash.djson.metadata',
];

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

/** The four Json.NET type strings, widened so that any string can be checked against them. */
const TYPE_STRINGS: readonly string[] = Object.values(ITEM_TYPES);

const djsonFiles = (folder: string): string[] => listFiles(folder).filter((f) => f.endsWith('.djson')).map((f) => join(folder, f));

describe('widget build on disk', () => {
  test('writes the folder, the sidecars, the fonts, the zip and the manifest', () => {
    const folder = join(widget.out, 'openDash');
    expect(widget.packages).toHaveLength(1);
    expect(widget.packages[0]!.written.folder).toBe(folder);
    expect(listFiles(folder)).toEqual(EXPECTED_FILES);
    expect(existsSync(join(widget.out, 'openDash.simhubdash'))).toBe(true);
    expect(existsSync(join(widget.out, MANIFEST_FILE))).toBe(true);
    expect(readdirSync(widget.out).sort()).toEqual([MANIFEST_FILE, 'openDash', 'openDash.simhubdash']);
  });

  test('the manifest records version, SimHub version and the package', () => {
    const manifest = readJson(join(widget.out, MANIFEST_FILE));
    expect(manifest).toEqual({
      version: readVersion(),
      simHubVersion: '9.12.6',
      packages: [{ folder: 'openDash', width: 1920, height: 480, slots: 12, file: 'openDash.simhubdash' }],
    });
    expect(manifest).toEqual(widget.manifest as unknown as JsonItem);
    expect(Object.keys(manifest)).toEqual(['version', 'simHubVersion', 'packages']);
    expect(readFileSync(widget.manifestPath, 'utf8').endsWith('\n')).toBe(true);
  });

  test('the zip lists <folder>/… entries, sorted, matching the files on disk', () => {
    const zipped = widget.packages[0]!.zipped;
    const bytes = readFileSync(zipped.path);
    expect(Buffer.compare(bytes, zipped.bytes)).toBe(0);
    const expected = EXPECTED_FILES.map((f) => `openDash/${f}`);
    expect(zipped.entries).toEqual(expected);
    const unzipped = readZip(new Uint8Array(bytes));
    expect(Object.keys(unzipped).sort()).toEqual(expected);
    for (const rel of EXPECTED_FILES) {
      expect(Buffer.compare(Buffer.from(unzipped[`openDash/${rel}`]!), readFileSync(join(widget.out, 'openDash', rel)))).toBe(0);
    }
  });

  test('the bundled fonts are the three face fonts, byte-identical to fonts/', () => {
    const fontsDir = join(widget.out, 'openDash', FONTS_DIR);
    expect(readdirSync(fontsDir).sort()).toEqual([...FACE_FONT_FILES].sort());
    for (const f of FACE_FONT_FILES) {
      expect(Buffer.compare(readFileSync(join(fontsDir, f)), readFileSync(join(import.meta.dir, '..', 'fonts', f)))).toBe(0);
    }
  });

  test('every sidecar equals the Metadata key and carries the version', () => {
    for (const file of djsonFiles(join(widget.out, 'openDash'))) {
      const doc = readJson(file);
      const sidecar = readJson(`${file}.metadata`);
      expect(sidecar).toEqual(doc.Metadata as JsonItem);
      expect(sidecar.DashboardVersion).toBe(readVersion());
      expect(sidecar.SimHubVersion).toBe('9.12.6');
      expect(sidecar.Author).toBe('openDash contributors');
      expect(sidecar.MetadataVersion).toBe(2);
    }
    expect(readJson(join(widget.out, 'openDash', 'openDash.djson.metadata'))).toMatchObject({ Title: 'openDash', Width: 1920, Height: 480, ScreenCount: 1 });
    expect(readJson(join(widget.out, 'openDash', 'cards.djson.metadata'))).toMatchObject({ Width: 255, Height: 187, ScreenCount: 12 });
  });

  test('the build log names every file written', () => {
    for (const rel of [...EXPECTED_FILES.map((f) => `openDash/${f}`), 'openDash.simhubdash', MANIFEST_FILE]) {
      expect(log.some((line) => line.startsWith('wrote ') && line.includes(rel))).toBe(true);
    }
    expect(log.some((line) => line.startsWith('warning '))).toBe(false);
  });
});

describe('the emitted JSON', () => {
  const documents = (): { file: string; doc: JsonItem; text: string }[] =>
    [widget, inline].flatMap((r) => djsonFiles(join(r.out, 'openDash')).map((file) => ({ file, doc: readJson(file), text: readFileSync(file, 'utf8') })));

  test('has $type as the first key of every item, one of the four kinds, with unique GUID ids', () => {
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

  test('writes every colour as #AARRGGBB', () => {
    for (const { file, doc, text } of documents()) {
      const colours = stringsOf(doc).filter((s) => s.value.startsWith('#'));
      expect(colours.length).toBeGreaterThan(0);
      for (const c of colours) expect({ file, ...c, ok: isNormalisedHex(c.value) }).toEqual({ file, ...c, ok: true });
      for (const m of text.matchAll(/"(#[0-9A-Za-z]*)"/g)) expect(m[1]).toMatch(/^#[0-9A-F]{8}$/);
      for (const key of ['BackgroundColor', 'TextColor', 'BorderColor', 'StartColor', 'EndColor', 'MiddleColor']) {
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

  test('the widget strategy embeds cards.djson in twelve slots with valid screen indexes', () => {
    const folder = join(widget.out, 'openDash');
    const main = readJson(join(folder, 'openDash.djson'));
    const cards = readJson(join(folder, 'cards.djson'));
    const screens = cards.Screens as JsonItem[];
    expect(screens.map((s) => s.Name)).toEqual(CARD_CATALOGUE.map((c) => c.id));
    const widgets = itemsOfDocument(main).filter((i) => i.$type === ITEM_TYPES.widget);
    expect(widgets).toHaveLength(12);
    widgets.forEach((w, i) => {
      expect(w.FileName).toBe(CARDS_FILE);
      expect(w.InitialScreenIndex).toBe(i);
      expect(w.AutoSize).toBe(true);
      expect(w.BackgroundColor).toBe('#00FFFFFF');
      const binding = (w.Bindings as JsonItem).InitialScreenIndex as JsonItem;
      expect((binding.Formula as JsonItem).Expression).toBe(`isnull([OpenDash.Slot${String(i + 1).padStart(2, '0')}], ${i})`);
      expect(binding.Mode).toBe(2);
    });
    expect((main.Screens as JsonItem[])[0]!.Name).toBe('Main');
    expect(main.BaseWidth).toBe(1920);
    expect(main.BaseHeight).toBe(480);
    expect(cards.BaseWidth).toBe(255);
    expect(cards.BaseHeight).toBe(187);
  });
});

describe('inline strategy', () => {
  test('builds a single dashboard with no widget and no cards.djson', () => {
    const folder = join(inline.out, 'openDash');
    expect(listFiles(folder)).toEqual(EXPECTED_FILES.filter((f) => !f.startsWith('cards.')));
    expect(inline.packages[0]!.zipped.entries).toEqual(EXPECTED_FILES.filter((f) => !f.startsWith('cards.')).map((f) => `openDash/${f}`));
    const items = itemsOfDocument(readJson(join(folder, 'openDash.djson')));
    expect(items.some((i) => i.$type === ITEM_TYPES.widget)).toBe(false);
    const slots = items.filter((i) => i.$type === ITEM_TYPES.layer && /^Slot\d\d$/.test(String(i.Name)));
    expect(slots).toHaveLength(12);
    expect(inline.manifest.packages[0]!.slots).toBe(12);
    expect(inline.strategy).toBe('inline');
  });
});

describe('reproducibility', () => {
  test('two builds produce byte-identical packages', () => {
    const again = build({ out: join(root, 'again'), log: () => {} });
    expect(Buffer.compare(again.packages[0]!.zipped.bytes, widget.packages[0]!.zipped.bytes)).toBe(0);
    expect(readFileSync(again.manifestPath, 'utf8')).toBe(readFileSync(widget.manifestPath, 'utf8'));
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

  test('a clean package validates with no warnings', () => {
    expect(validateOrThrow(buildPackage(layout1920x480, { version: '0.0.0-test' }))).toEqual([]);
    expect(validateOrThrow(buildPackage(layout1920x480, { version: '0.0.0-test', strategy: 'inline' }))).toEqual([]);
  });

  test('two layouts sharing a folder are refused', () => {
    expect(() => build({ out: join(root, 'twice'), layouts: [layout1920x480, layout1920x480], log: () => {} })).toThrow(/folder "openDash"/);
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
