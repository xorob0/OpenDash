#!/usr/bin/env bun
/**
 * demo-data: what the in-browser demo (#395) draws and replays, copied out of the repository.
 *
 * The demo runs the dashboards from the same `.djson` files the plugin installs, so this does not
 * describe them, it copies them. It reads
 *
 *   build/manifest.json                which packages the build wrote, and their sizes and themes
 *   build/<folder>/*.djson             each package's dashboards, the widgets included
 *   build/<folder>/*.djson.ressources  the pictures those dashboards reference, a zip per dashboard
 *   traces/*.ndjson                    the recorded scenarios the demo replays
 *   packages/dash/src/contract.ts      the panel: catalogues, value sets, defaults, property names
 *
 * and writes
 *
 *   public/demo/<slug>/<file>.djson.json       minified, otherwise as built
 *   public/demo/<slug>/images/<file>/<name>    each picture out of its sidecar
 *   public/demo/traces/<scenario>.ndjson.txt   as committed
 *   lib/demo.generated.ts                      what was copied, and the panel's catalogue
 *
 * all of them gitignored. Every face of the default theme, every face of each car theme the build
 * wrote (`bun run build --all-themes`, which the site's image runs through `bun run package`), the
 * two companions and the two pit walls. With no build the list comes back empty and the page says
 * so, as the Downloads page does.
 */
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { inflateRawSync } from 'node:zlib';
import {
  BAR_FIELDS,
  BAR_SLOTS,
  BLUE_FLAG_DETAILS,
  BLUE_FLAG_DETAIL_SETTING,
  CARD_CATALOGUE,
  CLOCK_FORMATS,
  CLOCK_FORMAT_SETTING,
  COMPANION_FLAG_FORMAT_SETTING,
  COMPANION_FLAG_FORMATS,
  COMPANION_OPEN_ON_BACK,
  COMPANION_OPEN_ON_SETTING,
  COMPANION_PAGE_SETTING,
  DEFAULT_COMPANION_FLAG_FORMAT,
  DEFAULT_COMPANION_OPEN_ON,
  DEFAULT_PIT_WALL_CLASS_ONLY,
  DEFAULT_PIT_WALL_FLAG_FORMAT,
  DEFAULT_PIT_WALL_PAGE,
  DEFAULT_WEB_VIEW_URL,
  DEFAULT_BAR_FIELDS,
  DEFAULT_FLAG_FORMAT,
  DEFAULT_LAP_REVIEW,
  DEFAULT_QUICK_GLANCE,
  DEFAULT_SLOT_CARDS,
  DEFAULTS,
  DELTA_PRECISIONS,
  DELTA_PRECISION_SETTING,
  DELTA_REFERENCES,
  DRIVER_NAME_FORMATS,
  DRIVER_NAME_FORMAT_SETTING,
  DRIVER_NAME_TEAM_SETTING,
  FACE_SIZES,
  FACE_ZONE_LETTERS,
  FLAGS_IN_PIT_LANE_SETTING,
  FLAG_FORMATS,
  LAP_REVIEW_MODES,
  MODULE_CATALOGUE,
  moduleSettingName,
  PIT_WALL_CLASS_ONLY_SETTING,
  PIT_WALL_FLAG_FORMAT_SETTING,
  PIT_WALL_PAGE_SETTING,
  PIT_WALL_PAGES,
  PIT_WALL_WIDE_ZONE_PAGES,
  PIT_WALL_ZONE_PAGES,
  pitWallZoneSettingName,
  POSITION_MODES,
  PROPERTY_PREFIX,
  REV_BAR_MODES,
  REV_BAR_SETTING,
  SESSION_PROGRESS_MODES,
  SLOT_MAX,
  THEME_CATALOGUE,
  themeEntry,
  WEB_VIEW_SETTING,
  barFieldSettingName,
  defaultZoneMask,
  defaultZonePage,
  facePrefix,
  flagFormatSettingName,
  lapReviewSettingName,
  pagesForZone,
  quickGlanceSettingName,
  revBarSettingName,
  slotSettingName,
  zoneClassOnlySettingName,
  zoneMaskSettingName,
  zonePageSettingName,
  zonePositionSettingName,
  zoneStartSettingName,
} from '../../packages/dash/src/contract.ts';
import { parseHeader } from '../../scripts/traceFormat.ts';
import type { PanelCatalogue, PanelChoice, PanelFace, PanelZone } from '../lib/demo/panel.ts';
import type { DemoFace, DemoFile, DemoGroup, DemoImage, DemoTrace } from '../lib/demo/types.ts';
import { slug } from '../lib/packages.ts';
import { SUPERSEDED, type Manifest, type ManifestEntry } from './content.ts';

const repoRoot = path.resolve(import.meta.dir, '..', '..');
const siteRoot = path.resolve(import.meta.dir, '..');
const outDir = path.join(siteRoot, 'public', 'demo');
const outPath = path.join(siteRoot, 'lib', 'demo.generated.ts');

/** The trace the demo opens on: a race lap, the state a face spends its life in. */
export const DEMO_SCENARIO = 'race';

/**
 * How long the plugin forces a companion's start module after it opens, and the module it was on
 * after a glance is released: `Contract.CompanionOpenOnWindow` and `Contract.CompanionBackWindow`,
 * with the glance and the start module a fresh install has, `DefaultCompanionQuickGlance` and
 * `DefaultCompanionStart`. They live in the plugin alone; `demo-panel.test.ts` reads them back
 * out of `plugin/OpenDash/Contract.cs`.
 */
export const COMPANION_TIMING: { readonly openOnWindowMs: number; readonly backWindowMs: number; readonly defaultStart: number; readonly defaultGlance: number } = {
  openOnWindowMs: 6000,
  backWindowMs: 1000,
  defaultStart: 0,
  defaultGlance: 12,
};

/**
 * What the copies are served as. `next start` compresses a response only when its type is one it
 * knows to be compressible, and a `.djson` or an `.ndjson` is served as `application/octet-stream`,
 * which it is not: a companion went over the wire as 4.6 MB rather than 330 KB. A `.djson` is JSON
 * and a trace is lines of text, so they are served as what they are.
 */
const SERVED_AS_JSON = '.json';
const SERVED_AS_TEXT = '.txt';

// ------------------------------------------------------------------------------------- faces

type Entry = ManifestEntry & { slots?: number };

/** Which row of the size switcher a package sits in, or null when the demo does not draw it. */
export function demoGroup(entry: Entry): DemoGroup | null {
  if (SUPERSEDED.test(entry.folder)) return null;
  if (entry.kind === 'companion') return 'companion';
  if (entry.kind === 'pitwall') return 'pitwall';
  if (entry.kind !== 'dash') return null;
  if (entry.theme === undefined) return 'face';
  return themeEntry(entry.theme) ? 'theme' : null;
}

const GROUP_ORDER: readonly DemoGroup[] = ['face', 'theme', 'companion', 'pitwall'];

/**
 * The packages the demo draws: the default theme's faces as the site lists them, then each car
 * theme's faces, then the companion and the pit wall, each in the manifest's order within its row.
 */
export const demoEntries = (manifest: { packages: Entry[] }): Entry[] =>
  GROUP_ORDER.flatMap((group) => manifest.packages.filter((p) => demoGroup(p) === group));

// --------------------------------------------------------------------------------- the panel

const zonesOfTheme = (theme?: (typeof THEME_CATALOGUE)[number]): PanelZone[] =>
  FACE_ZONE_LETTERS.map((letter) => ({
    letter,
    pages: pagesForZone(letter, theme).map(({ number, id, name }) => ({ number, id, name })),
    defaultPage: defaultZonePage(letter, theme),
    defaultMask: defaultZoneMask(letter, theme),
  }));

const rigChoice = (setting: string, label: string, values: readonly (string | boolean)[], fallback: string | boolean): PanelChoice => ({ setting, label, values: [...values], default: fallback });

/** The panel's whole catalogue, from the contract and nothing else. */
export function panelCatalogue(): PanelCatalogue {
  const faces: PanelFace[] = FACE_SIZES.map((face) => {
    const perZone = (f: (face: (typeof FACE_SIZES)[number], z: (typeof FACE_ZONE_LETTERS)[number]) => string) =>
      Object.fromEntries(FACE_ZONE_LETTERS.map((z) => [z, f(face, z)]));
    return {
      width: face.width,
      height: face.height,
      prefix: facePrefix(face),
      hasBar: face.hasBar,
      barFieldsPerEnd: face.barFieldsPerEnd,
      names: {
        zonePage: perZone(zonePageSettingName),
        zoneMask: perZone(zoneMaskSettingName),
        zoneStart: perZone(zoneStartSettingName),
        zoneClassOnly: perZone(zoneClassOnlySettingName),
        zonePosition: perZone(zonePositionSettingName),
        barField: Object.fromEntries(BAR_SLOTS.map((s) => [s, barFieldSettingName(face, s)])),
        quickGlance: quickGlanceSettingName(face),
        flagFormat: flagFormatSettingName(face),
        lapReview: lapReviewSettingName(face),
        revBar: revBarSettingName(face),
      },
    };
  });
  return {
    propertyPrefix: PROPERTY_PREFIX,
    zones: zonesOfTheme(),
    themes: THEME_CATALOGUE.map((theme) => ({ id: theme.id, name: theme.name, zones: zonesOfTheme(theme) })),
    barFields: BAR_FIELDS.map(({ number, id, name }) => ({ number, id, name })),
    barSlots: [...BAR_SLOTS],
    defaultBarFields: { ...DEFAULT_BAR_FIELDS },
    defaultQuickGlance: DEFAULT_QUICK_GLANCE,
    flagFormats: [...FLAG_FORMATS],
    defaultFlagFormat: DEFAULT_FLAG_FORMAT,
    lapReviewModes: [...LAP_REVIEW_MODES],
    defaultLapReview: DEFAULT_LAP_REVIEW,
    revBarModes: [...REV_BAR_MODES],
    defaultRevBar: DEFAULTS.RevBar,
    rig: [
      rigChoice('PositionMode', 'Position', POSITION_MODES, DEFAULTS.PositionMode),
      rigChoice('DeltaReference', 'Delta against', DELTA_REFERENCES, DEFAULTS.DeltaReference),
      rigChoice(DELTA_PRECISION_SETTING, 'Delta precision', DELTA_PRECISIONS, DEFAULTS.DeltaPrecision),
      rigChoice('SessionProgress', 'Session progress', SESSION_PROGRESS_MODES, DEFAULTS.SessionProgress),
      rigChoice(CLOCK_FORMAT_SETTING, 'Clock', CLOCK_FORMATS, DEFAULTS.ClockFormat),
      rigChoice(DRIVER_NAME_FORMAT_SETTING, 'Driver names', DRIVER_NAME_FORMATS, DEFAULTS.DriverNameFormat),
      rigChoice(DRIVER_NAME_TEAM_SETTING, 'Team names in lists', [false, true], DEFAULTS.DriverNameTeam),
      rigChoice(BLUE_FLAG_DETAIL_SETTING, 'Blue flag detail', BLUE_FLAG_DETAILS, DEFAULTS.BlueFlagDetail),
      rigChoice(FLAGS_IN_PIT_LANE_SETTING, 'Flags in the pit lane', [true, false], DEFAULTS.FlagsInPitLane),
      rigChoice(REV_BAR_SETTING, 'Rev bar, every screen', REV_BAR_MODES, DEFAULTS.RevBar),
    ],
    cards: CARD_CATALOGUE.map(({ number, id, displayName }) => ({ number, id, name: displayName })),
    defaultSlotCards: [...DEFAULT_SLOT_CARDS],
    slotNames: Array.from({ length: SLOT_MAX }, (_, i) => slotSettingName(i + 1)),
    faces,
    companion: {
      modules: MODULE_CATALOGUE.map(({ number, id, name, enabled }) => ({ number, id, name, enabled })),
      moduleNames: MODULE_CATALOGUE.map((m) => moduleSettingName(m.number)),
      pageName: COMPANION_PAGE_SETTING,
      flagFormatName: COMPANION_FLAG_FORMAT_SETTING,
      openOnName: COMPANION_OPEN_ON_SETTING,
      flagFormats: [...COMPANION_FLAG_FORMATS],
      defaultFlagFormat: DEFAULT_COMPANION_FLAG_FORMAT,
      defaultStart: COMPANION_TIMING.defaultStart,
      defaultGlance: COMPANION_TIMING.defaultGlance,
      openOnNone: DEFAULT_COMPANION_OPEN_ON,
      openOnBack: COMPANION_OPEN_ON_BACK,
      openOnWindowMs: COMPANION_TIMING.openOnWindowMs,
      backWindowMs: COMPANION_TIMING.backWindowMs,
    },
    pitWall: {
      pages: PIT_WALL_PAGES.map((page) => ({
        id: page.id,
        name: page.name,
        landscape: page.landscape,
        zones: page.zones.map((z) => ({ slot: z.slot, kind: z.kind, fallback: z.fallback, setting: pitWallZoneSettingName(page.id, z.slot) })),
      })),
      standardPages: PIT_WALL_ZONE_PAGES.map(({ number, id, name }) => ({ number, id, name })),
      widePages: PIT_WALL_WIDE_ZONE_PAGES.map(({ number, id, name }) => ({ number, id, name })),
      pageName: PIT_WALL_PAGE_SETTING,
      defaultPage: DEFAULT_PIT_WALL_PAGE,
      classOnlyName: PIT_WALL_CLASS_ONLY_SETTING,
      defaultClassOnly: DEFAULT_PIT_WALL_CLASS_ONLY,
      flagFormatName: PIT_WALL_FLAG_FORMAT_SETTING,
      flagFormats: [...COMPANION_FLAG_FORMATS],
      defaultFlagFormat: DEFAULT_PIT_WALL_FLAG_FORMAT,
      webViewUrlName: WEB_VIEW_SETTING,
      defaultWebViewUrl: DEFAULT_WEB_VIEW_URL,
    },
  };
}

// ------------------------------------------------------------------------------- the pictures

/**
 * The entries of a zip, as a `.djson.ressources` sidecar is: stored or deflated, named at its root.
 * Read from the central directory, so a local header's data descriptor does not matter.
 */
export function unzip(buffer: Buffer): { name: string; data: Buffer }[] {
  const eocd = buffer.lastIndexOf(Buffer.from([0x50, 0x4b, 0x05, 0x06]));
  if (eocd < 0) throw new Error('not a zip: no end of central directory');
  const count = buffer.readUInt16LE(eocd + 10);
  let at = buffer.readUInt32LE(eocd + 16);
  const out: { name: string; data: Buffer }[] = [];
  for (let i = 0; i < count; i++) {
    if (buffer.readUInt32LE(at) !== 0x02014b50) throw new Error('not a zip: a central directory entry is malformed');
    const method = buffer.readUInt16LE(at + 10);
    const compressed = buffer.readUInt32LE(at + 20);
    const nameLength = buffer.readUInt16LE(at + 28);
    const extraLength = buffer.readUInt16LE(at + 30);
    const commentLength = buffer.readUInt16LE(at + 32);
    const local = buffer.readUInt32LE(at + 42);
    const name = buffer.toString('utf8', at + 46, at + 46 + nameLength);
    const start = local + 30 + buffer.readUInt16LE(local + 26) + buffer.readUInt16LE(local + 28);
    const raw = buffer.subarray(start, start + compressed);
    if (method !== 0 && method !== 8) throw new Error(`${name} is compressed with method ${method}, which this reader does not know`);
    out.push({ name, data: method === 8 ? inflateRawSync(raw) : Buffer.from(raw) });
    at += 46 + nameLength + extraLength + commentLength;
  }
  return out;
}

// ------------------------------------------------------------------------------------ copying

/**
 * The files of a built package: its `.djson` documents and their `.ressources` sidecars. Read from
 * `build/<folder>/` when the build left the folder there, and otherwise out of the `.simhubdash`
 * the build also writes, which is the same folder zipped with the folder's name as its root. CI's
 * Website job has only the zips: it downloads the dash job's artifact, which carries
 * `build/*.simhubdash` and the manifest and not the unpacked folders.
 */
export function packageFiles(entry: ManifestEntry, buildDir = path.join(repoRoot, 'build')): Map<string, Buffer> {
  const out = new Map<string, Buffer>();
  const folder = path.join(buildDir, entry.folder);
  if (existsSync(folder)) {
    for (const file of readdirSync(folder)) if (/\.djson(\.ressources)?$/.test(file)) out.set(file, readFileSync(path.join(folder, file)));
    return out;
  }
  const zip = path.join(buildDir, entry.file);
  if (!existsSync(zip)) throw new Error(`neither build/${entry.folder}/ nor build/${entry.file} exists; run bun run build at the repository root`);
  const prefix = `${entry.folder}/`;
  for (const { name, data } of unzip(readFileSync(zip))) {
    if (!name.startsWith(prefix)) continue;
    const file = name.slice(prefix.length);
    if (/\.djson(\.ressources)?$/.test(file) && !file.includes('/')) out.set(file, data);
  }
  return out;
}

function copyFace(entry: Entry, prefixes: ReadonlyMap<string, string>): DemoFace {
  const group = demoGroup(entry);
  if (group === null) throw new Error(`${entry.folder} is not a package the demo draws`);
  const sources = packageFiles(entry);
  const s = slug(entry.folder);
  const target = path.join(outDir, s);
  mkdirSync(target, { recursive: true });
  const files: DemoFile[] = [];
  const images: DemoImage[] = [];
  for (const file of [...sources.keys()].filter((f) => f.endsWith('.djson')).sort()) {
    const built = sources.get(file)!;
    const document = JSON.parse(built.toString('utf8').replace(/^\uFEFF/, '')) as { Images?: { Name?: string; Extension?: string }[] };
    const text = JSON.stringify(document);
    writeFileSync(path.join(target, `${file}${SERVED_AS_JSON}`), text);
    files.push({ file, src: `/demo/${s}/${encodeURIComponent(file)}${SERVED_AS_JSON}`, bytes: Buffer.byteLength(text), builtBytes: built.length });
    const sidecar = sources.get(`${file}.ressources`);
    if (!sidecar || !document.Images?.length) continue;
    const entries = unzip(sidecar);
    const dir = path.join(target, 'images', file);
    mkdirSync(dir, { recursive: true });
    for (const image of document.Images) {
      const entryName = `${image.Name}${image.Extension}`;
      const found = entries.find((e) => e.name === entryName);
      if (!found || !image.Name) {
        console.warn(`  ${entry.folder}/${file} names ${entryName}, which its sidecar does not carry`);
        continue;
      }
      writeFileSync(path.join(dir, entryName), found.data);
      images.push({ dashboard: file, name: image.Name, src: `/demo/${s}/images/${encodeURIComponent(file)}/${encodeURIComponent(entryName)}` });
    }
  }
  const main = `${entry.folder}.djson`;
  if (!files.some((f) => f.file === main)) throw new Error(`build/${entry.folder} has no ${main}`);
  return {
    slug: s,
    folder: entry.folder,
    group,
    theme: entry.theme ?? 'default',
    width: entry.width,
    height: entry.height,
    round: /round/.test(entry.folder),
    main,
    base: `/demo/${s}/`,
    files,
    images,
    prefix: group === 'face' || group === 'theme' ? (prefixes.get(`${entry.width}x${entry.height}`) ?? null) : null,
    slots: /round/.test(entry.folder) ? (entry.slots ?? 0) : 0,
  };
}

/** Every committed trace, the one the demo opens on first and the rest by name. */
export function traceFiles(): string[] {
  const dir = path.join(repoRoot, 'traces');
  if (!existsSync(dir)) return [];
  const files = readdirSync(dir)
    .filter((f) => f.endsWith('.ndjson'))
    .sort();
  return [...files.filter((f) => f === `${DEMO_SCENARIO}.ndjson`), ...files.filter((f) => f !== `${DEMO_SCENARIO}.ndjson`)];
}

function copyTrace(file: string): DemoTrace {
  const from = path.join(repoRoot, 'traces', file);
  const dir = path.join(outDir, 'traces');
  mkdirSync(dir, { recursive: true });
  copyFileSync(from, path.join(dir, `${file}${SERVED_AS_TEXT}`));
  const text = readFileSync(from, 'utf8');
  const header = parseHeader(text.slice(0, text.indexOf('\n')));
  return {
    src: `/demo/traces/${encodeURIComponent(file)}${SERVED_AS_TEXT}`,
    scenario: header.scenario,
    frames: header.frames,
    hz: header.hz,
    recorded: header.recorded,
    simHub: header.simHub,
    bytes: statSync(from).size,
  };
}

if (import.meta.main) {
  const manifestPath = path.join(repoRoot, 'build', 'manifest.json');
  const manifest = existsSync(manifestPath) ? (JSON.parse(readFileSync(manifestPath, 'utf8')) as Manifest & { packages: Entry[] }) : null;
  rmSync(outDir, { recursive: true, force: true });
  mkdirSync(outDir, { recursive: true });
  const catalogue = panelCatalogue();
  const prefixes = new Map(catalogue.faces.map((f) => [`${f.width}x${f.height}`, f.prefix]));
  const faces = manifest ? demoEntries(manifest).map((e) => copyFace(e, prefixes)) : [];
  const traces = traceFiles().map(copyTrace);
  const json = (value: unknown): string => JSON.stringify(value, null, 2);
  const body = `/*
 * Generated by site/scripts/demo-data.ts. Do not edit.
 * The faces and the trace the demo serves from public/demo/, and the panel's catalogue from the contract.
 */
import type { PanelCatalogue } from './demo/panel';
import type { DemoFace, DemoTrace } from './demo/types';

/** The version of the build the faces were copied from, or null when there was none. */
export const DEMO_BUILD_VERSION: string | null = ${JSON.stringify(manifest?.version ?? null)};

/**
 * Every package the demo draws: the default theme's faces, each car theme's, the companions and the
 * pit walls. Empty when the repository has not been built.
 */
export const DEMO_FACES: readonly DemoFace[] = ${json(faces)};

/** Every committed trace, the one the demo opens on first. Empty when traces/ is missing. */
export const DEMO_TRACES: readonly DemoTrace[] = ${json(traces)};

/** Everything the fake panel offers, read from packages/dash/src/contract.ts. */
export const PANEL_CATALOGUE: PanelCatalogue = ${json(catalogue)};
`;
  writeFileSync(outPath, body);
  const weight = faces.map((f) => `${f.slug} ${Math.round(f.files.reduce((n, x) => n + x.bytes, 0) / 1024)} KB`).join(', ');
  console.log(
    `wrote lib/demo.generated.ts and public/demo/ (${faces.length} packages${weight ? `: ${weight}` : ''}; ${faces.reduce((n, f) => n + f.images.length, 0)} pictures; traces ${traces.map((t) => t.scenario).join(', ') || 'missing'})`,
  );
}
