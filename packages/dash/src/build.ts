/**
 * The build: `bun run build [--out <dir>] [--strategy widget|inline]`.
 *
 * For every layout in src/layouts it composes the package, validates it against the settings
 * contract (every `[OpenDash.X]` read must be a declared property, and one this package's own
 * screen owns or every screen shares, plus the generator's own checks), writes `<out>/<folder>/` (the .djson files, their .metadata sidecars and _SHFonts/),
 * zips that folder into `<out>/<folder>.simhubdash` and records `{ folder, width, height,
 * slots, rung, file }` in `<out>/manifest.json`, which every release publishes beside the packages
 * and which therefore carries a `schemaVersion`. Folder names may contain spaces. Validation errors fail the build before anything is written; warnings
 * are printed. Importing this module runs nothing: only `bun src/build.ts` calls main().
 */
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { COMPANION_PREFIX, declaredProperties, facePrefix, foreignProperties, PIT_WALL_PREFIX, PROPERTY_PREFIX } from './contract.ts';
import { buildPackage, DEFAULT_AUTHOR, DEFAULT_SIMHUB_VERSION } from './dashboard.ts';
import { buildZoneFace, sizeOf, ZONE_FACES, type ZoneLayout } from './zones/index.ts';
import { fontsForPackage } from './dashboard.ts';
import { fontsForPanel } from './design/fontFiles.ts';
import { noticesForPackage, PANEL_NOTICES } from './design/notices.ts';
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
import { buildFlagBoxProfile, contactSheet, FLAG_BOX_PROFILE_NAME } from './leds/index.ts';
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
 * The shape of {@link Manifest}, carried in the file itself. The manifest is published with every
 * release rather than kept as build output, so a reader out in the world meets manifests this
 * build never saw: one that knows only version 1 can refuse a 2 it cannot read, instead of
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
/** Environment fallback for `--strategy`, as the spec's `SLOT_STRATEGY=inline` build flag. */
export const STRATEGY_ENV = 'SLOT_STRATEGY';

export const STRATEGIES: readonly SlotStrategy[] = ['widget', 'inline'];

export const USAGE = [
  'usage: bun run build [--out <dir>] [--strategy widget|inline]',
  '  --out <dir>         output directory; default <repo>/build',
  `  --strategy <name>   how slots show cards: widget (default) or inline; ${STRATEGY_ENV} is the fallback`,
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
  const args: BuildArgs = { out: DEFAULT_OUT_DIR, strategy: parseStrategy(envStrategy) ?? DEFAULT_STRATEGY, help: false };
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

const VERSION_PATTERN = /^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$/;

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
 * Validates against the contract's declared properties; throws a BuildError on errors, returns the
 * warnings.
 *
 * `screen` is the prefix of the screen this package is, and what it may read follows from it: its
 * own group and the settings every screen shares, never another screen's. Every group is declared,
 * so being declared proves nothing here; without this rule a package could read the screen beside
 * it and only a rig with two screens would ever show it. A card face passes nothing, because it
 * owns no group and reads the modes and the slots alone.
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
  return result.warnings;
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
  /** Package folder and main dashboard name; may contain spaces ("openDash 850x480"). */
  folder: string;
  kind: PackageKind;
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
  /** Default: every zone face in src/zones. Pass an empty list to build the card faces alone. */
  zoneFaces?: readonly ZoneLayout[];
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
  /** The layout a zone face was built from; absent for anything else. */
  zoneFace?: ZoneLayout;
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
  const zoneFaces = opts.zoneFaces ?? ZONE_FACES;
  const screens = opts.screens ?? SCREEN_PACKAGES;
  const log = opts.log ?? ((line: string): void => console.log(line));
  // Composing nothing is legitimate now that a build can be lights alone; the guard that catches
  // "you asked for nothing at all" lives in build(), which is the only place that can see the
  // profiles as well as the packages.
  if (layouts.length === 0 && screens.length === 0 && zoneFaces.length === 0 && !allowEmpty) {
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
    const warnings = validateOrThrow(pkg);
    for (const w of warnings) log(`warning ${w.code} ${w.path}: ${w.message}`);
    staged.push({ layout, kind: 'dash', pkg, warnings });
  }
  for (const face of zoneFaces) {
    claim(face.folder);
    const built = buildZoneFace(face, { version, simHubVersion, author: DEFAULT_AUTHOR });
    const pkg: DashPackage = { folderName: face.folder, dashboards: [built.main, ...built.zones], fonts: fontsForPackage() };
    const warnings = validateOrThrow(pkg, facePrefix(sizeOf(face)));
    for (const w of warnings) log(`warning ${w.code} ${w.path}: ${w.message}`);
    staged.push({ zoneFace: face, kind: 'dash', pkg, warnings });
  }
  for (const screen of screens) {
    claim(screen.folder);
    const pkg = buildScreenPackage(screen, { version, simHubVersion });
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
    const profile = rpmStripProfile(shape, stableGuid(`openDash/leds/${shape.id}`));
    const warnings = validateStripProfileOrThrow(profile, deviceLength(shape));
    for (const w of warnings) log(`warning ${w.code} ${w.path}: ${w.message}`);
    stagedProfiles.push({ shape, fileName: rpmStripFileName(shape), profile, warnings });
  }

  mkdirSync(out, { recursive: true });
  const packages: BuiltPackage[] = [];
  const stripProfiles: BuiltProfile[] = [];
  const manifest: Manifest = { schemaVersion: MANIFEST_SCHEMA_VERSION, version, simHubVersion, packages: [], ledProfiles: [] };
  for (const { layout, zoneFace, screen, kind, pkg, warnings } of staged) {
    // Derived here rather than by each builder, so that a package cannot be assembled anywhere in
    // this file without the licences for what it carries.
    pkg.notices = noticesForPackage(pkg);
    const written = writePackage(pkg, out);
    for (const file of written.files) log(`wrote ${relative(file)}`);
    const zipped = zipPackage(out, pkg.folderName);
    log(`wrote ${relative(zipped.path)} (${zipped.entries.length} entries, ${zipped.bytes.byteLength} bytes)`);
    packages.push({ layout, zoneFace, screen, kind, pkg, warnings, written, zipped });
    manifest.packages.push({
      folder: pkg.folderName,
      kind,
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

  // Not zipped and not in `packages`: a profile is a single file the user imports by hand, and
  // wrapping it in an archive would only add a step. ADR 0013.
  const ledProfilePath = path.join(out, FLAG_BOX_FILE);
  writeFileSync(ledProfilePath, serializeProfile(ledProfile), 'utf8');
  log(`wrote ${relative(ledProfilePath)}`);
  manifest.ledProfiles.push(FLAG_BOX_FILE);

  const sheetPath = path.join(out, FLAG_BOX_SHEET_FILE);
  writeFileSync(sheetPath, contactSheet(), 'utf8');
  log(`wrote ${relative(sheetPath)}`);

  const manifestPath = path.join(out, MANIFEST_FILE);
  writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
  log(`wrote ${relative(manifestPath)}`);
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
    const result = build({ out: args.out, strategy: args.strategy });
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
