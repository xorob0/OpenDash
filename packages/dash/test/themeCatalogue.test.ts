/**
 * The theme catalogue (#202) and the build it drives (#201): one list in `contract.ts`, which the
 * build walks rather than a directory, a size the catalogue claims and the anatomy does not draw
 * refused, themed packages named `OpenDash <Theme> <W>x<H>` and recorded with their theme, and item
 * identifiers that are stable across rebuilds and unique across themes.
 *
 * `ContractTests.cs` reads the catalogue from the other side, and `contract.test.ts` reads
 * `Contract.cs` back, so the two halves cannot drift.
 */
import { afterAll, beforeAll, describe, expect, test } from 'bun:test';
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path, { join } from 'node:path';
import { build, composeTheme, composeThemeHere, parseArgs, themesToBuild, type BuildResult } from '../src/build.ts';
import { DEFAULT_THEME_ID, FACE_SIZES, THEME_CATALOGUE, themedFolder, themeEntry, type FaceSize, type ThemeEntry } from '../src/contract.ts';
import { serializeDashboard } from '../src/generator.ts';
import { THEMES } from '../src/themes/index.ts';
import { GEAR_LEFT_THEME_ID, gearLeftTheme, GREEN_GEAR_LEFT_THEME_ID, greenGearLeftTheme } from './fixtures/gearLeftTheme.ts';
import greenTheme from './fixtures/greenTheme.json';
import { touchedThemes } from '../src/themes/touched.ts';
import { themesToCheck } from './conformance.ts';

const THEMES_DIR = path.join(import.meta.dir, '..', 'src', 'themes');
const at = (width: number, height: number): FaceSize => FACE_SIZES.find((f) => f.width === width && f.height === height)!;
const named = (s: { width: number; height: number }): string => `${s.width}x${s.height}`;
const isTestTheme = (id: string): boolean => id.startsWith('test-');

/** The test theme as a catalogue would hold it, at the one size the builds below need. */
const gearLeft: ThemeEntry = { id: GEAR_LEFT_THEME_ID, name: 'Gear left', cars: [], iracingCarPaths: [], sizes: [at(1280, 480)], bandPages: [] };
const defaultAt1280: ThemeEntry = { ...themeEntry(DEFAULT_THEME_ID)!, sizes: [at(1280, 480)] };
const REQUEST = { version: '0.0.0-test', simHubVersion: '9.12.6' };

THEMES[GEAR_LEFT_THEME_ID] = gearLeftTheme;
THEMES[GREEN_GEAR_LEFT_THEME_ID] = greenGearLeftTheme;
afterAll(() => {
  delete THEMES[GEAR_LEFT_THEME_ID];
  delete THEMES[GREEN_GEAR_LEFT_THEME_ID];
});

describe('the theme catalogue', () => {
  test('opens on the default theme, which claims every face under the names its layouts give', () => {
    expect(THEME_CATALOGUE[0]!.id).toBe(DEFAULT_THEME_ID);
    expect(THEME_CATALOGUE[0]!.sizes).toEqual(FACE_SIZES);
  });

  test('holds each theme once, by a lower-case id and a name a folder can carry', () => {
    const ids = THEME_CATALOGUE.map((t) => t.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(new Set(THEME_CATALOGUE.map((t) => t.name.toLowerCase())).size).toBe(ids.length);
    for (const theme of THEME_CATALOGUE) {
      expect({ id: theme.id, ok: /^[a-z][a-z0-9-]*$/.test(theme.id) }).toEqual({ id: theme.id, ok: true });
      // Letters, digits and single spaces: a folder on Windows, and nothing PackageCatalogue.Classify
      // reads as another kind of screen.
      expect({ id: theme.id, ok: /^[A-Za-z0-9]+( [A-Za-z0-9]+)*$/.test(theme.name) && !/companion|pit wall|slots|round/i.test(theme.name) }).toEqual({ id: theme.id, ok: true });
      expect({ id: theme.id, isTest: isTestTheme(theme.id) }).toEqual({ id: theme.id, isTest: false });
    }
  });

  test('claims only faces that ship, each once', () => {
    for (const theme of THEME_CATALOGUE) {
      expect(theme.sizes.length).toBeGreaterThan(0);
      for (const size of theme.sizes) expect(FACE_SIZES).toContain(size);
      expect(new Set(theme.sizes).size).toBe(theme.sizes.length);
    }
  });

  test('catalogues every theme there is code for, and each one with code lives in its own directory', () => {
    for (const id of Object.keys(THEMES).filter((id) => !isTestTheme(id))) expect({ id, catalogued: themeEntry(id) !== undefined }).toEqual({ id, catalogued: true });
    for (const theme of THEME_CATALOGUE.filter((t) => Object.hasOwn(THEMES, t.id))) {
      expect({ id: theme.id, directory: existsSync(join(THEMES_DIR, theme.id)) }).toEqual({ id: theme.id, directory: true });
    }
    // And no directory is a theme the catalogue does not know.
    for (const entry of readdirSync(THEMES_DIR, { withFileTypes: true }).filter((e) => e.isDirectory())) {
      expect({ directory: entry.name, catalogued: themeEntry(entry.name) !== undefined }).toEqual({ directory: entry.name, catalogued: true });
    }
  });

  test('claims, for every theme with code, only sizes its anatomy draws', () => {
    for (const theme of THEME_CATALOGUE.filter((t) => Object.hasOwn(THEMES, t.id))) {
      const drawn = THEMES[theme.id]!.anatomy.sizes.map(named);
      for (const size of theme.sizes) expect({ theme: theme.id, size: named(size), drawn: drawn.includes(named(size)) }).toEqual({ theme: theme.id, size: named(size), drawn: true });
    }
  });

  test('names a themed package after the theme and always after its size, and the default after nothing', () => {
    const porsche: ThemeEntry = { id: 'porsche', name: 'Porsche', cars: [], iracingCarPaths: [], sizes: [], bandPages: [] };
    expect(themedFolder(porsche, { width: 1280, height: 480 })).toBe('OpenDash Porsche 1280x480');
    // At 1920 x 480 as well, where the default's package is the bare `OpenDash` (ADR 0016).
    expect(themedFolder(porsche, { width: 1920, height: 480 })).toBe('OpenDash Porsche 1920x480');
    expect(() => themedFolder(themeEntry(DEFAULT_THEME_ID)!, { width: 1280, height: 480 })).toThrow(/default theme keeps/);
  });
});

describe('what the build is asked to build', () => {
  test('is the default alone with no arguments, and more only when asked', () => {
    expect(themesToBuild(parseArgs([], {})).map((t) => t.id)).toEqual([DEFAULT_THEME_ID]);
    const lines: string[] = [];
    const all = themesToBuild(parseArgs(['--all-themes'], {}), (line) => lines.push(line));
    expect(all[0]!.id).toBe(DEFAULT_THEME_ID);
    // Every catalogued theme with code, and a line for each one without.
    expect(all.map((t) => t.id)).toEqual(THEME_CATALOGUE.filter((t) => Object.hasOwn(THEMES, t.id)).map((t) => t.id));
    for (const theme of THEME_CATALOGUE.filter((t) => !Object.hasOwn(THEMES, t.id))) {
      expect(lines).toContain(`skipped the ${theme.id} theme: it is in the catalogue and has no code under packages/dash/src/themes/${theme.id}/ yet`);
    }
  });

  test('--touched-themes adds the themes the branch touches, by the harness rule, and says why', () => {
    const lines: string[] = [];
    const args = parseArgs(['--touched-themes'], {});
    expect(args.touchedThemes).toBe(true);
    const second = THEME_CATALOGUE[1]!;
    const picked = themesToBuild(args, (line) => lines.push(line), () => ({ ids: [DEFAULT_THEME_ID, second.id], why: 'the reason' }));
    expect(picked.map((t) => t.id)).toEqual([DEFAULT_THEME_ID, second.id]);
    expect(lines).toEqual([`touched themes: ${DEFAULT_THEME_ID}, ${second.id}; the reason`]);
    const untouched = themesToBuild(args, () => {}, () => ({ ids: [DEFAULT_THEME_ID], why: 'none' }));
    expect(untouched.map((t) => t.id)).toEqual([DEFAULT_THEME_ID]);
    // The selection the build takes by default is the harness's own.
    expect(themesToCheck({}).ids).toEqual(touchedThemes().ids);
  });

  test('--theme names a catalogued theme, and an unknown one is refused before anything is built', () => {
    const second = THEME_CATALOGUE[1]!;
    expect(parseArgs(['--theme', second.id, `--theme=${second.id}`], {}).themes).toEqual([second.id]);
    expect(themesToBuild(parseArgs(['--theme', second.id], {})).map((t) => t.id)).toEqual([DEFAULT_THEME_ID, second.id]);
    expect(() => parseArgs(['--theme', 'nope'], {})).toThrow(/unknown theme "nope"/);
  });

  test('a theme the catalogue holds and no code draws fails the build, and says where the code goes', () => {
    const missing: ThemeEntry = { id: 'test-uncoded', name: 'Uncoded', cars: [], iracingCarPaths: [], sizes: [at(1280, 480)], bandPages: [] };
    expect(() => composeTheme({ ...REQUEST, theme: missing })).toThrow('the test-uncoded theme is in the catalogue and has no code under packages/dash/src/themes/test-uncoded/ yet');
  });

  /** The answer to #202's acceptance: a size claimed and not drawn is a build failure, not an absence. */
  test('a size the catalogue claims and the anatomy does not draw fails the build', () => {
    const overclaimed: ThemeEntry = { ...gearLeft, sizes: [at(1280, 480), at(850, 480)] };
    expect(() => composeTheme({ ...REQUEST, theme: overclaimed })).toThrow(
      'the test-gear-left theme claims 850x480 in the theme catalogue, and its anatomy does not draw it; it draws 1280x480, 1920x480',
    );
  });
});

describe('a themed build', () => {
  const root = mkdtempSync(join(tmpdir(), 'opendash-themes-'));
  const only = { layouts: [], screens: [], stripShapes: [], log: (): void => {}, ...REQUEST };
  let first: BuildResult;
  let again: BuildResult;
  beforeAll(() => {
    first = build({ ...only, out: join(root, 'first'), themes: [defaultAt1280, gearLeft] });
    again = build({ ...only, out: join(root, 'again'), themes: [defaultAt1280, gearLeft] });
  }, 60_000);
  afterAll(() => rmSync(root, { recursive: true, force: true }));

  test('writes the themed package beside the default one, and the manifest says which theme it is', () => {
    expect(first.manifest.packages.map((p) => p.folder)).toEqual(['OpenDash 1280x480', 'OpenDash Gear left 1280x480']);
    expect(first.manifest.packages[0]).not.toHaveProperty('theme');
    expect(first.manifest.packages[1]).toMatchObject({ kind: 'dash', theme: GEAR_LEFT_THEME_ID, width: 1280, height: 480, slots: 0, file: 'OpenDash Gear left 1280x480.simhubdash' });
    expect(existsSync(join(root, 'first', 'OpenDash Gear left 1280x480.simhubdash'))).toBe(true);
    const metadata = readFileSync(join(root, 'first', 'OpenDash Gear left 1280x480', 'OpenDash Gear left 1280x480.djson.metadata'), 'utf8');
    expect(metadata).toContain('"Title": "OpenDash Gear left 1280x480"');
  });

  test('reads the stock namespace of its size, as the default package of that size does', () => {
    const text = readFileSync(join(root, 'first', 'OpenDash Gear left 1280x480', 'OpenDash Gear left 1280x480.djson'), 'utf8');
    expect(text).toContain('OpenDash.Face1280x480ZoneA');
    expect(text).not.toMatch(/OpenDash\.Face(?!1280x480)\d+x\d+/);
  });

  /** #201's property: a theme is a dimension of the hashed path, or two themes collide. */
  test('gives every item an identifier stable across rebuilds and unique across themes', () => {
    const ids = (result: BuildResult, folder: string): string[] => {
      const pkg = result.packages.find((p) => p.pkg.folderName === folder)!;
      return pkg.pkg.dashboards.flatMap((d) => [...serializeDashboard(d, { packageName: folder }).matchAll(/"(?:Id|ScreenId)": ?"([0-9a-f-]{36})"/g)].map((m) => m[1]!));
    };
    const house = ids(first, 'OpenDash 1280x480');
    const themed = ids(first, 'OpenDash Gear left 1280x480');
    expect(themed.length).toBeGreaterThan(1000);
    expect(new Set(themed).size).toBe(themed.length);
    expect(themed.filter((id) => house.includes(id))).toEqual([]);
    expect(ids(again, 'OpenDash Gear left 1280x480')).toEqual(themed);
    expect(ids(again, 'OpenDash 1280x480')).toEqual(house);
  });

  test('composes a theme with colours of its own in a process of its own, and writes what it hands back unchanged', () => {
    // Under the same name as the test theme, so that the folders and the identifiers are the same
    // and the colour is the only thing that can differ.
    const green: ThemeEntry = { ...gearLeft, id: GREEN_GEAR_LEFT_THEME_ID };
    const fixture = path.join(import.meta.dir, 'fixtures', 'themeProcess.ts');
    const [child] = composeTheme({ ...REQUEST, theme: green }, fixture);
    const [here] = composeThemeHere({ ...REQUEST, theme: gearLeft });
    expect(child!.pkg.folderName).toBe('OpenDash Gear left 1280x480');
    const oldGreen = (JSON.parse(readFileSync(path.join(import.meta.dir, '..', '..', '..', 'design', 'tokens.json'), 'utf8')) as { palette: { green: { '200': { value: string } } } }).palette.green['200'].value.slice(1);
    const newGreen = greenTheme.palette.green['200'].value.slice(1);
    let reached = 0;
    expect(child!.pkg.dashboards.map((d) => d.name)).toEqual(here!.pkg.dashboards.map((d) => d.name));
    here!.pkg.dashboards.forEach((dashboard, i) => {
      const base = serializeDashboard(dashboard, { packageName: here!.pkg.folderName });
      const drawn = serializeDashboard(child!.pkg.dashboards[i]!, { packageName: child!.pkg.folderName });
      if (base.includes(oldGreen)) reached++;
      expect({ dashboard: dashboard.name, same: drawn === base.replaceAll(oldGreen, newGreen) }).toEqual({ dashboard: dashboard.name, same: true });
    });
    expect(reached).toBeGreaterThan(0);
  });
});
