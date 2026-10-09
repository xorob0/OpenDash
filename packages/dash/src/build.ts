/**
 * The build: `bun run build [--out <dir>] [--strategy widget|inline] [--theme <id>] [--touched-themes] [--all-themes]`.
 *
 * For every layout in src/layouts, every size the theme catalogue claims for each theme it is asked
 * for (the default alone unless told otherwise) and every second screen, it composes the package, validates it against the settings
 * contract (every `[OpenDash.X]` read must be a declared property, and one this package's own
 * screen owns or every screen shares, plus the generator's own checks), writes `<out>/<folder>/` (the .djson files, their .metadata sidecars and _SHFonts/),
 * zips that folder into `<out>/<folder>.simhubdash` and records `{ folder, width, height,
 * slots, rung, file }` in `<out>/manifest.json`, which releases published beside the packages until
 * #438 and which therefore carries a `schemaVersion`. Folder names may contain spaces. Validation errors fail the build before anything is written; warnings
 * are printed. Importing this module runs nothing: only `bun src/build.ts` calls main().
 */
import { copyFileSync, mkdirSync, readdirSync, readFileSync, realpathSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import {
  COMPANION_PREFIX,
  DEFAULT_THEME_ID,
  declaredProperties,
  facePrefix,
  foreignProperties,
  PIT_WALL_PREFIX,
  PROPERTY_PREFIX,
  THEME_CATALOGUE,
  themeEntry,
  themedFolder,
  type ThemeEntry,
} from './contract.ts';
import { buildPackage, DEFAULT_AUTHOR, DEFAULT_SIMHUB_VERSION } from './dashboard.ts';
import { buildThemeFace, drawsInThisProcess } from './themes/faces.ts';
import { THEME_ENV, THEMES } from './themes/index.ts';
import { TOUCHED_BASE, touchedThemes, type Selection } from './themes/touched.ts';
import { ZONE_FACES, type ZoneLayout } from './zones/index.ts';
import { fontsForPackage } from './dashboard.ts';
import { assetNamed, imageOf } from './design/assets.ts';
import { fontsForPanel } from './design/fontFiles.ts';
import { noticesForPackage, PANEL_NOTICES } from './design/notices.ts';
import { previewFileName, previewFor } from './previews.ts';
import { itemsOf } from './walk.ts';
import type { Rung } from './design/rung.ts';
import {
  formatIssues,
  PACKAGE_EXTENSION,
  PROFILE_EXTENSION,
  serializeProfile,
  validatePackage,
  validateProfile,
  writePackage,
  zipPackage,
  type DashPackage,
  type MatrixProfile,
  type ValidationIssue,
  type WrittenPackage,
  type ZippedPackage,
  leds,
  stableGuid,
} from './generator.ts';
import { LAYOUTS, rungOf, type Layout } from './layouts/index.ts';
import { buildFlagBoxProfile, contactSheet, FLAG_BOX_PROFILE_NAME, glyphSheetJson } from './leds/index.ts';
import { SCREEN_PACKAGES, buildScreenPackage, type ScreenPackageDef } from './screens/index.ts';
import { ALL_SHAPES, deviceLength, type StripShape } from './leds/strip.ts';
import { rpmStripFileName, rpmStripProfile, rpmStripProfileName } from './leds/rpmStrip.ts';
import { validateShiftTable } from './leds/shiftPoints.ts';
import { DEFAULT_STRATEGY, type SlotStrategy } from './slots.ts';

/** The repository root: this file lives in packages/dash/src. */
export const REPO_ROOT = path.resolve(import.meta.dir, '..', '..', '..');
/** Where `bun run build` writes unless `--out` says otherwise. */
export const DEFAULT_OUT_DIR = path.join(REPO_ROOT, 'build');
/** The one version string of the project. */
export const VERSION_FILE = path.join(REPO_ROOT, 'VERSION');
export const MANIFEST_FILE = 'manifest.json';
/**
 * The shape of {@link Manifest}, carried in the file itself. The manifest was published with every
 * release until #438 made the plugin zip the only release file, so a reader out in the world may
 * still meet manifests this build never saw: one that knows only version 1 can refuse a 2 it cannot read, instead of
 * guessing at fields that moved. Raise it when an existing field changes meaning or leaves, never
 * for a field that is merely added.
 */
export const MANIFEST_SCHEMA_VERSION = 1;
/** Where the build leaves the fonts the plugin embeds, relative to the output directory. */
export const PANEL_FONTS_DIR = 'fonts';
/** The flag box profile, relative to the output directory. Not a package: see ADR 0013. */
export const FLAG_BOX_FILE = `${FLAG_BOX_PROFILE_NAME}${PROFILE_EXTENSION}`;
/**
 * Every glyph the flag box draws, as one SVG. A pull request that changes the chequered flag shows
 * the chequered flag; nothing else in a generated profile is reviewable by looking at it.
 */
export const FLAG_BOX_SHEET_FILE = 'flag-box.svg';
/**
 * The same glyphs as data, which the plugin embeds to draw the box's preview and the rig page's
 * tiles from the pictures the profile is built from (#503). Beside the SVG and, like it, not in the
 * manifest: it is not something SimHub installs, and scripts/package.sh copies it by name.
 */
export const FLAG_BOX_GLYPHS_FILE = 'flag-box-glyphs.json';
/** Environment fallback for `--strategy`, as the spec's `SLOT_STRATEGY=inline` build flag. */
export const STRATEGY_ENV = 'SLOT_STRATEGY';

export const STRATEGIES: readonly SlotStrategy[] = ['widget', 'inline'];

/**
 * What a theme with colours of its own is composed in. Its colours are fixed when `ds` is built,
 * once per process, so a build of several themes is one process per such theme; see
 * {@link composeTheme}.
 */
export const THEME_PROCESS = path.join(import.meta.dir, 'buildTheme.ts');

export const USAGE = [
  'usage: bun run build [--out <dir>] [--strategy widget|inline] [--theme <id>] [--touched-themes] [--all-themes]',
  '  --out <dir>         output directory; default <repo>/build',
  `  --strategy <name>   how slots show cards: widget (default) or inline; ${STRATEGY_ENV} is the fallback`,
  '  --theme <id>        build this theme as well as the default; may be given more than once',
  `  --touched-themes    build the themes the branch touches against ${TOUCHED_BASE} as well, as CI does`,
  '  --all-themes        build every theme in the catalogue, as `bun run package` and a release do',
  '  --help              print this text',
].join('\n');

/** A build failure. `issues` carries the validator errors when validation is what failed. */
export class BuildError extends Error {
  constructor(
    message: string,
    readonly issues: readonly ValidationIssue[] = [],
  ) {
    super(message);
    this.name = 'BuildError';
  }
}

export interface BuildArgs {
  /** Absolute output directory. */
  out: string;
  strategy: SlotStrategy;
  /** The themes asked for by name, beside the default, which is always built. */
  themes: string[];
  /** The themes the branch touches, by the rule the conformance harness checks them by. */
  touchedThemes: boolean;
  allThemes: boolean;
  help: boolean;
}

const parseStrategy = (value: string | undefined): SlotStrategy | undefined =>
  STRATEGIES.find((s) => s === value?.trim().toLowerCase());

/** Command line parsing. `--flag value` and `--flag=value` are both accepted; the flag wins over the environment. */
export function parseArgs(argv: readonly string[], env: Record<string, string | undefined> = process.env, cwd: string = process.cwd()): BuildArgs {
  const envStrategy = env[STRATEGY_ENV];
  if (envStrategy !== undefined && envStrategy !== '' && parseStrategy(envStrategy) === undefined) {
    throw new BuildError(`${STRATEGY_ENV}=${JSON.stringify(envStrategy)} is not a strategy; expected ${STRATEGIES.join(' or ')}`);
  }
  const args: BuildArgs = { out: DEFAULT_OUT_DIR, strategy: parseStrategy(envStrategy) ?? DEFAULT_STRATEGY, themes: [], touchedThemes: false, allThemes: false, help: false };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i] ?? '';
    const eq = arg.startsWith('--') ? arg.indexOf('=') : -1;
    const flag = eq >= 0 ? arg.slice(0, eq) : arg;
    const inlineValue = eq >= 0 ? arg.slice(eq + 1) : undefined;
    const value = (): string => {
      if (inlineValue !== undefined) return inlineValue;
      const next = argv[++i];
      if (next === undefined) throw new BuildError(`${flag} needs a value\n${USAGE}`);
      return next;
    };
    switch (flag) {
      case '--out':
      case '-o':
        args.out = path.resolve(cwd, value());
        break;
      case '--strategy':
      case '-s': {
        const raw = value();
        const strategy = parseStrategy(raw);
        if (strategy === undefined) throw new BuildError(`unknown strategy ${JSON.stringify(raw)}; expected ${STRATEGIES.join(' or ')}`);
        args.strategy = strategy;
        break;
      }
      case '--theme': {
        const id = value();
        if (!themeEntry(id)) throw new BuildError(`unknown theme ${JSON.stringify(id)}; the catalogue holds ${THEME_CATALOGUE.map((t) => t.id).join(', ')}`);
        if (!args.themes.includes(id)) args.themes.push(id);
        break;
      }
      case '--touched-themes':
        args.touchedThemes = true;
        break;
      case '--all-themes':
        args.allThemes = true;
        break;
      case '--help':
      case '-h':
        args.help = true;
        break;
      default:
        throw new BuildError(`unknown argument ${JSON.stringify(arg)}\n${USAGE}`);
    }
  }
  return args;
}

/** What VERSION may hold. `scripts/version.ts` refuses to write anything else, so the build never meets it. */
export const VERSION_PATTERN = /^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$/;

/** The trimmed contents of VERSION, which must look like `major.minor.patch[-prerelease]`. */
export function readVersion(file: string = VERSION_FILE): string {
  let text: string;
  try {
    text = readFileSync(file, 'utf8');
  } catch (e) {
    throw new BuildError(`cannot read ${file}: ${e instanceof Error ? e.message : String(e)}`);
  }
  const version = text.trim();
  if (!VERSION_PATTERN.test(version)) throw new BuildError(`${file} must hold a version like 0.1.0, got ${JSON.stringify(version)}`);
  return version;
}

/**
 * The two font warnings this build refuses to print and carry on from. A run drawn in a family or
 * a weight the package does not bundle is resolved by WPF to whatever it can find, so the advances
 * in design/advances.ts, and the fit textFit.test.ts proved with them, describe a face that never
 * shipped; that is the failure the family renaming in design/fontFiles.ts was introduced to end.
 * The generator can only warn, because a package it validates need not carry its own fonts at all,
 * and one built here always does.
 */
const FONT_ERROR_CODES: readonly string[] = ['font/missing', 'font/weight-missing'];

/**
 * Validates against the contract's declared properties; throws a BuildError on errors, returns the
 * warnings.
 *
 * `screen` is the prefix of the screen this package is, and what it may read follows from it: its
 * own group and the settings every screen shares, never another screen's. Every group is declared,
 * so being declared proves nothing here; without this rule a package could read the screen beside
 * it and only a rig with two screens would ever show it. A card face passes nothing, because it
 * owns no group and reads the modes and the slots alone.
 *
 * {@link FONT_ERROR_CODES} is why a package that draws a weight it does not ship fails here rather
 * than logging a warning: what a package ships is the set of faces it draws in, and a weight added
 * to an item has to be added to FACE_FONT_FILES or SCREEN_FONT_FILES, and measured into
 * design/advances.ts, before anything can be drawn in it.
 */
export function validateOrThrow(pkg: DashPackage, screen?: string): ValidationIssue[] {
  const result = validatePackage(pkg, {
    declaredProperties: declaredProperties(),
    propertyPrefix: PROPERTY_PREFIX,
    foreignProperties: foreignProperties(screen),
  });
  if (!result.ok) {
    const n = result.errors.length;
    throw new BuildError(`package ${pkg.folderName} has ${n} validation error${n === 1 ? '' : 's'}:\n${formatIssues(result.errors)}`, result.errors);
  }
  const faces = result.warnings.filter((w) => FONT_ERROR_CODES.includes(w.code));
  if (faces.length > 0) {
    const n = faces.length;
    throw new BuildError(`package ${pkg.folderName} draws in ${n} face${n === 1 ? '' : 's'} it does not ship:\n${formatIssues(faces)}`, faces);
  }
  return result.warnings;
}

/**
 * Declares on each dashboard the images its own items draw, which is what puts the files into the
 * `.ressources` sidecar of the `.simhubdash` beside `_SHFonts/`.
 *
 * A module returns items and never sees the dashboard it lands on, so no drawing can declare its
 * own artwork; the declaration is derived here instead, from what is actually drawn. A package
 * therefore carries the assets it uses and no others, which is what keeps a package a package
 * SimHub can read at startup rather than a folder of every picture OpenDash owns.
 *
 * Run before validation, so that an item whose image nothing declares is the validator's
 * `image/missing` rather than a dashboard that loads and draws a hole.
 */
export function packImages(pkg: DashPackage): void {
  for (const dashboard of pkg.dashboards) {
    const drawn = [...new Set(itemsOf(dashboard).flatMap((item) => (item.kind === 'image' ? [item.image] : [])))].sort();
    if (drawn.length === 0) continue;
    dashboard.images = drawn.map((name) => {
      const asset = assetNamed(name);
      if (asset === undefined) {
        throw new BuildError(`${pkg.folderName}/${dashboard.name} draws the image ${JSON.stringify(name)}, which is not an asset of packages/dash/src/design/assets.ts`);
      }
      return imageOf(asset);
    });
  }
}

/**
 * Validates a generated strip profile the way {@link validateOrThrow} validates a package: errors
 * block the whole build before anything is written, warnings are returned to be logged. The strip
 * length is passed so that the fit rule has something to measure against — an effect running off
 * the end of a strip is silent on the device, exactly as clipped text is silent on a screen.
 *
 * Named apart from {@link validateProfileOrThrow} because the two artefacts are two object models:
 * a strip is a run addressed by index, a matrix is a grid, and they do not share a validator.
 */
export function validateStripProfileOrThrow(profile: leds.LedProfile, ledCount: number): ValidationIssue[] {
  const result = leds.validateProfile(profile, { declaredProperties: declaredProperties(), propertyPrefix: PROPERTY_PREFIX, ledCount });
  if (!result.ok) {
    const n = result.errors.length;
    throw new BuildError(`profile ${profile.name} has ${n} validation error${n === 1 ? '' : 's'}:\n${formatIssues(result.errors)}`, result.errors);
  }
  return result.warnings;
}

/** Validates the flag box profile against the same contract; throws a BuildError on errors. */
export function validateProfileOrThrow(profile: MatrixProfile): ValidationIssue[] {
  const result = validateProfile(profile, { declaredProperties: declaredProperties(), propertyPrefix: PROPERTY_PREFIX });
  if (!result.ok) {
    const n = result.errors.length;
    throw new BuildError(`profile ${profile.name} has ${n} validation error${n === 1 ? '' : 's'}:\n${formatIssues(result.errors)}`, result.errors);
  }
  return result.warnings;
}

/** What a package is: the dash face, or one of the two second screens. */
export type PackageKind = 'dash' | 'companion' | 'pitwall';

export interface ManifestEntry {
  /** Package folder and main dashboard name; may contain spaces ("OpenDash 850x480"). */
  folder: string;
  kind: PackageKind;
  /**
   * The theme a face is drawn in, by its catalogue id. Absent on everything the default theme builds,
   * and on the card faces and the second screens, which no theme draws, so that the manifest of a
   * build without themes is the one it was before they existed.
   */
  theme?: string;
  width: number;
  height: number;
  /**
   * Slots the face has. A second screen has none, and neither does a zone face: a zone is not a
   * slot, and reporting one as twelve would tell the plugin to draw twelve dropdowns for it.
   */
  slots: number;
  /** The card rung of the layout's slots; absent on anything without cards. */
  rung?: Rung;
  /** The .simhubdash, relative to the output directory. */
  file: string;
}

export interface Manifest {
  /** {@link MANIFEST_SCHEMA_VERSION}, so that a reader can tell this shape from a later one. */
  schemaVersion: number;
  version: string;
  simHubVersion: string;
  packages: ManifestEntry[];
  /**
   * Every LED profile the build wrote, relative to the output directory: the flag box and one per
   * strip shape. Listed apart from `packages` because none of them is one — the plugin extracts
   * them and the user imports them, rather than the plugin installing them. ADR 0013 is why.
   */
  ledProfiles: string[];
}

export interface BuildOptions {
  /** Output directory; default {@link DEFAULT_OUT_DIR}. */
  out?: string;
  strategy?: SlotStrategy;
  /** Default: the VERSION file. */
  version?: string;
  simHubVersion?: string;
  /** Default: every layout in src/layouts. */
  layouts?: readonly Layout[];
  /**
   * The themes whose faces are built, each at the sizes its entry claims. Default: the default theme
   * alone, at every size. Pass an empty list to build no zone face.
   */
  themes?: readonly ThemeEntry[];
  /** Default: {@link THEME_PROCESS}. A test points it at a fixture that registers a theme first. */
  themeProcess?: string;
  /** Default: every second screen in src/screens. Pass an empty list to build the faces alone. */
  screens?: readonly ScreenPackageDef[];
  /** Default: the flag box profile in src/leds. */
  ledProfile?: MatrixProfile;
  /** Default: every strip shape in src/leds/strip. Pass an empty list to build the packages alone. */
  stripShapes?: readonly StripShape[];
  /** Progress and warnings, one line at a time. Default: console.log. */
  log?: (line: string) => void;
}

export interface ComposedPackage {
  /** The layout a card face was built from; absent for anything else. */
  layout?: Layout;
  /** The layout a zone face was built from, under the package's folder; absent for anything else. */
  zoneFace?: ZoneLayout;
  /** The theme a themed face is drawn in; absent for everything the default theme builds. */
  theme?: string;
  /** The definition a second screen was built from; absent for a face. */
  screen?: ScreenPackageDef;
  kind: PackageKind;
  pkg: DashPackage;
  warnings: ValidationIssue[];
}

export interface BuiltPackage extends ComposedPackage {
  written: WrittenPackage;
  zipped: ZippedPackage;
}

/** A profile as it was written. */
export interface BuiltProfile {
  /** The strip this was generated for. */
  shape: StripShape;
  profile: leds.LedProfile;
  warnings: ValidationIssue[];
  path: string;
}

export interface BuildResult {
  out: string;
  strategy: SlotStrategy;
  version: string;
  simHubVersion: string;
  packages: BuiltPackage[];
  /** The flag box profile and where it was written. */
  ledProfile: { profile: MatrixProfile; warnings: ValidationIssue[]; path: string };
  /** One per strip shape, and where each was written. */
  stripProfiles: BuiltProfile[];
  manifest: Manifest;
  manifestPath: string;
}

/** A path as the log shows it: relative to the working directory when it lies below it, else absolute. */
const relative = (file: string): string => {
  const rel = path.relative(process.cwd(), file);
  return rel === '' ? '.' : rel.startsWith('..') ? file : rel;
};

/** One face of a theme as composed, which is also what a theme's own process hands back. */
export interface ThemePackage {
  width: number;
  height: number;
  pkg: DashPackage;
  warnings: ValidationIssue[];
}

export interface ThemeRequest {
  theme: ThemeEntry;
  version: string;
  simHubVersion: string;
}

const named = (size: { width: number; height: number }): string => `${size.width}x${size.height}`;

/** A theme's faces at the sizes its entry claims, composed and validated in this process. */
export function composeThemeHere({ theme, version, simHubVersion }: ThemeRequest): ThemePackage[] {
  return theme.sizes.map((size) => {
    const folder = theme.id === DEFAULT_THEME_ID ? undefined : themedFolder(theme, size);
    const { layout, built } = buildThemeFace(theme.id, size, { version, simHubVersion, author: DEFAULT_AUTHOR }, folder);
    const pkg: DashPackage = { folderName: layout.folder, dashboards: [built.main, ...built.zones], fonts: fontsForPackage(theme.id) };
    packImages(pkg);
    // The stock namespace of its size, for a themed face as for the default one: a theme changes the
    // register and never the contract (ADR 0016).
    return { width: size.width, height: size.height, pkg, warnings: validateOrThrow(pkg, facePrefix(size)) };
  });
}

/**
 * A theme's faces, refusing an entry the build cannot honour: a theme with no code, or a size the
 * catalogue claims and the anatomy does not draw. Both are failures rather than skips, since a
 * catalogue that claims a package the build does not make is a picker offering nothing.
 *
 * A theme drawn in this process's colours is composed here. One with colours of its own is composed
 * in `themeProcess`, started with {@link THEME_ENV} naming it, and handed back as JSON, so that every
 * package is still validated before anything is written and written by the one loop in `build()`.
 */
export function composeTheme(request: ThemeRequest, themeProcess: string = THEME_PROCESS): ThemePackage[] {
  const { theme } = request;
  const code = Object.hasOwn(THEMES, theme.id) ? THEMES[theme.id] : undefined;
  if (!code) throw new BuildError(`the ${theme.id} theme is in the catalogue and has no code under packages/dash/src/themes/${theme.id}/ yet, so there is nothing to build`);
  const undrawn = theme.sizes.filter((size) => !code.anatomy.sizes.some((s) => s.width === size.width && s.height === size.height));
  if (undrawn.length > 0) {
    throw new BuildError(
      `the ${theme.id} theme claims ${undrawn.map(named).join(', ')} in the theme catalogue, and its anatomy does not draw ${undrawn.length === 1 ? 'it' : 'them'}; it draws ${code.anatomy.sizes.map(named).join(', ')}`,
    );
  }
  if (drawsInThisProcess(theme.id)) return composeThemeHere(request);
  const run = Bun.spawnSync([process.execPath, themeProcess, JSON.stringify(request)], { env: { ...process.env, [THEME_ENV]: theme.id }, stdout: 'pipe', stderr: 'pipe' });
  if (run.exitCode !== 0) throw new BuildError(`the ${theme.id} theme could not be composed in a process of its own: ${run.stderr.toString().trim()}`);
  return JSON.parse(run.stdout.toString()) as ThemePackage[];
}

/**
 * The themes a command line asks for: the default always, then every other theme `--all-themes`,
 * `--touched-themes` (which says why it took those, so that a job log answers what it built) or
 * `--theme` names, in catalogue order. A theme with no code yet is skipped, and said to be, when it
 * is swept in by `--all-themes`; named by `--theme`, it is kept, so that {@link composeTheme} refuses it.
 */
export function themesToBuild(
  args: Pick<BuildArgs, 'themes' | 'allThemes' | 'touchedThemes'>,
  log: (line: string) => void = (line) => console.log(line),
  touched: () => Selection = touchedThemes,
): ThemeEntry[] {
  const selection = args.touchedThemes && !args.allThemes ? touched() : undefined;
  if (selection) log(`touched themes: ${selection.ids.join(', ')}; ${selection.why}`);
  return THEME_CATALOGUE.filter((theme) => {
    if (theme.id === DEFAULT_THEME_ID || args.themes.includes(theme.id) || selection?.ids.includes(theme.id)) return true;
    if (!args.allThemes) return false;
    if (Object.hasOwn(THEMES, theme.id)) return true;
    log(`skipped the ${theme.id} theme: it is in the catalogue and has no code under packages/dash/src/themes/${theme.id}/ yet`);
    return false;
  });
}

/**
 * Every package, composed and validated, with nothing written. This is the whole of the build that
 * is a pure function of the sources, which is what lets a test ask what the packages read without
 * putting twenty zip files on disk to find out.
 */
export function composePackages(opts: BuildOptions = {}, allowEmpty = false): ComposedPackage[] {
  const version = opts.version ?? readVersion();
  const simHubVersion = opts.simHubVersion ?? DEFAULT_SIMHUB_VERSION;
  const strategy = opts.strategy ?? DEFAULT_STRATEGY;
  const layouts = opts.layouts ?? LAYOUTS;
  const themes = opts.themes ?? [themeEntry(DEFAULT_THEME_ID)!];
  const screens = opts.screens ?? SCREEN_PACKAGES;
  const log = opts.log ?? ((line: string): void => console.log(line));
  // Composing nothing is legitimate now that a build can be lights alone; the guard that catches
  // "you asked for nothing at all" lives in build(), which is the only place that can see the
  // profiles as well as the packages.
  if (layouts.length === 0 && screens.length === 0 && themes.length === 0 && !allowEmpty) {
    throw new BuildError('there is no layout to build');
  }

  const folders = new Set<string>();
  const staged: ComposedPackage[] = [];
  const claim = (folder: string): void => {
    if (folders.has(folder)) throw new BuildError(`two packages use the folder ${JSON.stringify(folder)}`);
    folders.add(folder);
  };
  for (const layout of layouts) {
    claim(layout.folder);
    const pkg = buildPackage(layout, { version, simHubVersion, strategy });
    packImages(pkg);
    const warnings = validateOrThrow(pkg);
    for (const w of warnings) log(`warning ${w.code} ${w.path}: ${w.message}`);
    staged.push({ layout, kind: 'dash', pkg, warnings });
  }
  for (const theme of themes) {
    for (const { width, height, pkg, warnings } of composeTheme({ theme, version, simHubVersion }, opts.themeProcess)) {
      claim(pkg.folderName);
      for (const w of warnings) log(`warning ${w.code} ${w.path}: ${w.message}`);
      const house = ZONE_FACES.find((f) => f.width === width && f.height === height)!;
      const themed = theme.id !== DEFAULT_THEME_ID;
      staged.push({ zoneFace: themed ? { ...house, folder: pkg.folderName } : house, ...(themed ? { theme: theme.id } : {}), kind: 'dash', pkg, warnings });
    }
  }
  for (const screen of screens) {
    claim(screen.folder);
    const pkg = buildScreenPackage(screen, { version, simHubVersion });
    packImages(pkg);
    const warnings = validateOrThrow(pkg, screen.kind === 'pitwall' ? PIT_WALL_PREFIX : COMPANION_PREFIX);
    for (const w of warnings) log(`warning ${w.code} ${w.path}: ${w.message}`);
    staged.push({ screen, kind: screen.kind, pkg, warnings });
  }
  return staged;
}

/**
 * Builds every layout. Composes and validates all packages first, so that an error in any of
 * them leaves the output directory untouched; then writes, zips and records the manifest.
 */
export function build(opts: BuildOptions = {}): BuildResult {
  const out = path.resolve(opts.out ?? DEFAULT_OUT_DIR);
  const strategy = opts.strategy ?? DEFAULT_STRATEGY;
  const version = opts.version ?? readVersion();
  const simHubVersion = opts.simHubVersion ?? DEFAULT_SIMHUB_VERSION;
  const log = opts.log ?? ((line: string): void => console.log(line));
  // A build of lights alone is legitimate: the flag box and the strips are outputs in their own
  // right. What is not legitimate is asking for nothing at all, which is checked here rather than
  // in composePackages because only this function can see the profiles.
  const staged = composePackages({ ...opts, version, simHubVersion, strategy, log }, true);

  const ledProfile = opts.ledProfile ?? buildFlagBoxProfile(version);
  const stripShapes = opts.stripShapes ?? ALL_SHAPES;
  if (staged.length === 0 && stripShapes.length === 0) throw new BuildError('there is nothing to build');
  const ledWarnings = validateProfileOrThrow(ledProfile);
  for (const w of ledWarnings) log(`warning ${w.code} ${w.path}: ${w.message}`);

  // Staged with the packages and before anything is written, so that a profile that would not
  // light leaves the output directory untouched exactly as a bad package does.
  // A contributed shift point that is not traceable or not ordered fails here, before anything is
  // written, rather than putting a shift light in the wrong place on somebody's rig.
  const tableProblems = validateShiftTable();
  if (tableProblems.length > 0) throw new BuildError(`data/shift-points.json has ${tableProblems.length} problem(s):\n${tableProblems.join('\n')}`);

  const stagedProfiles: { shape: StripShape; fileName: string; profile: leds.LedProfile; warnings: ValidationIssue[] }[] = [];
  for (const shape of stripShapes) {
    const profile = rpmStripProfile(shape, stableGuid(`OpenDash/leds/${shape.id}`), version);
    const warnings = validateStripProfileOrThrow(profile, deviceLength(shape));
    for (const w of warnings) log(`warning ${w.code} ${w.path}: ${w.message}`);
    stagedProfiles.push({ shape, fileName: rpmStripFileName(shape), profile, warnings });
  }

  mkdirSync(out, { recursive: true });
  const packages: BuiltPackage[] = [];
  const stripProfiles: BuiltProfile[] = [];
  const manifest: Manifest = { schemaVersion: MANIFEST_SCHEMA_VERSION, version, simHubVersion, packages: [], ledProfiles: [] };
  for (const { layout, zoneFace, theme, screen, kind, pkg, warnings } of staged) {
    // Derived here rather than by each builder, so that a package cannot be assembled anywhere in
    // this file without the licences for what it carries.
    pkg.notices = noticesForPackage(pkg);
    // The gallery thumbnail, which is a photograph and therefore cannot be produced by this build.
    // A missing one costs a grey box in SimHub's list and nothing else, so it is said out loud and
    // the build carries on: the machine that can take the picture is the Windows VM, and ADR 0008
    // is the record of why that is not something every contributor is asked for.
    pkg.preview = previewFor(pkg.folderName);
    if (pkg.preview === undefined) {
      log(`warning preview/missing ${pkg.folderName}: no packages/dash/previews/${previewFileName(pkg.folderName)}; SimHub will list it without a thumbnail (bun run previews)`);
    }
    const written = writePackage(pkg, out);
    for (const file of written.files) log(`wrote ${relative(file)}`);
    const zipped = zipPackage(out, pkg.folderName);
    log(`wrote ${relative(zipped.path)} (${zipped.entries.length} entries, ${zipped.bytes.byteLength} bytes)`);
    packages.push({ layout, zoneFace, ...(theme ? { theme } : {}), screen, kind, pkg, warnings, written, zipped });
    manifest.packages.push({
      folder: pkg.folderName,
      kind,
      ...(theme ? { theme } : {}),
      width: layout?.width ?? zoneFace?.width ?? screen?.width ?? 0,
      height: layout?.height ?? zoneFace?.height ?? screen?.height ?? 0,
      slots: layout ? layout.slots.length : 0,
      ...(layout ? { rung: rungOf(layout) } : {}),
      file: `${pkg.folderName}${PACKAGE_EXTENSION}`,
    });
  }
  for (const { shape, fileName, profile, warnings } of stagedProfiles) {
    const file = leds.writeLedsProfile(profile, out, fileName);
    log(`wrote ${relative(file)}`);
    stripProfiles.push({ shape, profile, warnings, path: file });
    manifest.ledProfiles.push(`${fileName}${leds.LEDS_PROFILE_EXTENSION}`);
  }

  // The plugin's settings panel draws in the same renamed family and embeds its own copies, and
  // its build runs on a machine that has never run this one. So the fonts a release needs are left
  // beside the packages, where the plugin build (and CI, which hands one job's output to the next)
  // picks them up; see plugin/OpenDash/OpenDash.csproj.
  const fontsOut = path.join(out, PANEL_FONTS_DIR);
  mkdirSync(fontsOut, { recursive: true });
  for (const font of fontsForPanel()) {
    const target = path.join(fontsOut, path.basename(font));
    copyFileSync(font, target);
    log(`wrote ${relative(target)}`);
  }
  for (const notice of PANEL_NOTICES) {
    const target = path.join(fontsOut, notice.name);
    copyFileSync(notice.path, target);
    log(`wrote ${relative(target)}`);
  }

  // Not zipped and not in `packages`: a profile is a single file SimHub takes whole, which the
  // plugin embeds and hands to SimHub from its Lights page. ADR 0013.
  const ledProfilePath = path.join(out, FLAG_BOX_FILE);
  writeFileSync(ledProfilePath, serializeProfile(ledProfile), 'utf8');
  log(`wrote ${relative(ledProfilePath)}`);
  manifest.ledProfiles.push(FLAG_BOX_FILE);

  const sheetPath = path.join(out, FLAG_BOX_SHEET_FILE);
  writeFileSync(sheetPath, contactSheet(), 'utf8');
  log(`wrote ${relative(sheetPath)}`);

  const glyphsPath = path.join(out, FLAG_BOX_GLYPHS_FILE);
  writeFileSync(glyphsPath, glyphSheetJson(), 'utf8');
  log(`wrote ${relative(glyphsPath)}`);

  const manifestPath = path.join(out, MANIFEST_FILE);
  writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
  log(`wrote ${relative(manifestPath)}`);

  for (const stale of sweep(out, manifest)) log(`removed ${relative(stale)} (nothing builds it any more)`);
  return {
    out,
    strategy,
    version,
    simHubVersion,
    packages,
    ledProfile: { profile: ledProfile, warnings: ledWarnings, path: ledProfilePath },
    stripProfiles,
    manifest,
    manifestPath,
  };
}

/**
 * Deletes the artefacts of an earlier build that this one did not write.
 *
 * The output directory is not emptied first, because a build that began by deleting everything would
 * leave nothing at all behind when it threw half way through, and a failed build should cost the last
 * good one. So the sweep runs at the end, when the manifest is the settled list of what this build
 * produces, and removes only the two kinds of file the manifest names: a package and a light profile.
 * Anything else in the directory -- `fonts/`, an unpacked package folder, the sheet and the glyphs
 * the build writes beside the profile, whatever somebody left there -- is left where it is.
 *
 * The reason this exists is a shape that was renamed. `OpenDash 3/9/3 Fanalab` became
 * `OpenDash 3/9/3 Fanatec`, the build wrote the new file, and the old one sat in `build/` until
 * scripts/package.sh copied it into the plugin's resources: the release then carried a profile no
 * source builds, offering a wiring order the panel no longer captions. A removed screen size would do
 * the same, and would be installed by anyone who pressed the button beside it.
 */
export function sweep(out: string, manifest: Manifest): string[] {
  /** Canonical name by its lower-cased spelling, so an entry can be matched however it is spelled. */
  const wanted = new Map<string, string>();
  for (const name of [...manifest.packages.map((p) => p.file), ...manifest.ledProfiles]) wanted.set(name.toLowerCase(), name);

  const removed: string[] = [];
  for (const entry of readdirSync(out, { withFileTypes: true })) {
    if (!entry.isFile()) continue;
    const isArtefact = entry.name.endsWith(PACKAGE_EXTENSION) || entry.name.endsWith(leds.LEDS_PROFILE_EXTENSION);
    if (!isArtefact) continue;
    const canonical = wanted.get(entry.name.toLowerCase());
    if (canonical === entry.name) continue;

    const here = path.join(out, entry.name);
    // A name the manifest wants, spelled differently on the disk. On macOS and Windows that is the
    // *same file*: a write replaces the contents and the filesystem keeps the entry's original
    // spelling, so after `openDash` became `OpenDash` every freshly written artefact was still
    // listed under the old casing, missed this set, and was deleted by the sweep it had just been
    // handed. Fifty files on the disk against sixty-three in the manifest, the flag box among the
    // missing, and only on the machines that build: CI runs on Linux, where the two are genuinely
    // two files and the old one really is stale. `realpath` tells the cases apart, because it
    // answers with the spelling the filesystem holds rather than the one it was asked for.
    if (canonical !== undefined && sameEntry(out, canonical, entry.name)) {
      renameSync(here, path.join(out, canonical));
      continue;
    }
    rmSync(here, { force: true });
    removed.push(here);
  }
  return removed;
}

/** Whether `name` is the directory entry that `canonical` resolves to, rather than a second file. */
function sameEntry(out: string, canonical: string, name: string): boolean {
  try {
    return path.basename(realpathSync.native(path.join(out, canonical))) === name;
  } catch {
    return false;
  }
}

const describe = (e: unknown): string => (e instanceof Error ? e.message : String(e));

/** The command line entry: returns the process exit code (0 ok, 1 build failed, 2 bad arguments). */
export function main(argv: readonly string[] = process.argv.slice(2)): number {
  let args: BuildArgs;
  try {
    args = parseArgs(argv);
  } catch (e) {
    console.error(describe(e));
    return 2;
  }
  if (args.help) {
    console.log(USAGE);
    return 0;
  }
  try {
    const themes = themesToBuild(args);
    console.log(`themes: ${themes.map((t) => `${t.id} at ${t.sizes.length} size${t.sizes.length === 1 ? '' : 's'}`).join(', ')}`);
    const result = build({ out: args.out, strategy: args.strategy, themes });
    const n = result.packages.length;
    const p = result.manifest.ledProfiles.length;
    const warnings =
      result.packages.reduce((sum, q) => sum + q.warnings.length, 0) +
      result.stripProfiles.reduce((sum, q) => sum + q.warnings.length, 0) +
      result.ledProfile.warnings.length;
    const lights = p === 0 ? '' : ` and ${p} LED profile${p === 1 ? '' : 's'}`;
    console.log(`built ${n} package${n === 1 ? '' : 's'}${lights} (version ${result.version}, ${result.strategy} slots, ${warnings} warning${warnings === 1 ? '' : 's'}) into ${relative(result.out)}`);
    return 0;
  } catch (e) {
    console.error(`build failed: ${describe(e)}`);
    return 1;
  }
}

if (import.meta.main) process.exit(main());
