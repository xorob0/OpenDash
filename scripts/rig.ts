#!/usr/bin/env bun
/**
 * rig: put a set of screens on the VM's plugin, so the captures show more than one layout.
 *
 * Every face draws the same four default pages, which is right for a first run and wrong for a
 * wall of pictures: ten photographs of lap times, the gear and the relative say less about the
 * catalogue's pages than eight photographs of eight different ones. The plugin already decides what
 * a zone shows, and it keeps that decision in its settings file, so this writes a rig there rather
 * than clicking through the panel forty times.
 *
 * A screen whose namespace is the stock one for its size takes the stock folder and the stock
 * package byte for byte (ADR 0017), so a seeded rig changes what the packages read, never what
 * they are. Its name is the package's own name, because SimHub lists a dashboard by its title and
 * `bun run shots` finds it by that.
 *
 * Every write deletes SimHub's copies of the settings file as well. `ReadCommonSettings` falls back
 * to `PluginsData\Common\_Backups\OpenDash.GeneralSettings_b1.json` and on through `_b5` whenever the
 * file itself cannot be read (docs/research/simhub-plugin-sdk.md, Settings), so a copy left behind is
 * an older rig waiting to come back the first time anything goes wrong with the one written here.
 *
 *   bun scripts/rig.ts show       # what is on the rig now
 *   bun scripts/rig.ts gallery    # the rig the website's captures are taken on
 *   bun scripts/rig.ts panel      # the rig the settings panel's captures are taken on
 *   bun scripts/rig.ts clear      # back to no screens, keeping every other setting
 *   bun scripts/rig.ts empty      # a first run: no settings at all and no OpenDash dashboards
 */
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { BAND_D_PAGES, defaultZoneMask, MODULE_CATALOGUE, pagesForZone, THEME_SETTINGS, themedFolder, themeEntry, themeSettingName, ZONE_A_PAGES } from '../packages/dash/src/contract.ts';
import { GRID_SHAPES, shapeById, stripLength } from '../packages/dash/src/leds/strip.ts';
import { fromShare, powershell, psq, resolveHost, simhubStart, simhubStop, sleep, toShare, withClaim, type Host, type RunResult } from './vm.ts';

const repoRoot = path.resolve(import.meta.dir, '..');
const SIMHUB_DIR = 'C:\\Program Files (x86)\\SimHub';
const COMMON = `${SIMHUB_DIR}\\PluginsData\\Common`;
const SETTINGS = `${COMMON}\\OpenDash.GeneralSettings.json`;
/** Where SimHub keeps the rolling copies it restores the settings from: `_b1` to `_b10`. */
const BACKUPS = `${COMMON}\\_Backups`;
const BACKUP_FILTER = 'OpenDash.GeneralSettings_b*.json';
const DASH_TEMPLATES = `${SIMHUB_DIR}\\DashTemplates`;
const SHARE_UNC = '\\\\host.lan\\Data';

/** A zone's page, by the id the catalogue gives it, so a table of names cannot drift into numbers. */
const zoneA = (id: string): number => index(ZONE_A_PAGES, id, 'zone A');
/** A band page, on a face of the theme given: a Porsche's band carries `porscheFoot` after the house's eight (#718). */
const band = (id: string, theme?: string): number => index(pagesForZone('D', theme ? themeEntry(theme) : undefined), id, 'band D');
const page = (id: string): number => index(MODULE_CATALOGUE.map((m) => ({ id: m.id, number: m.number - 1 })), id, 'the catalogue');

function index(list: readonly { id: string; number: number }[], id: string, what: string): number {
  const found = list.find((p) => p.id === id);
  if (!found) throw new RangeError(`${what} has no page ${JSON.stringify(id)}`);
  return found.number;
}

/**
 * What each face shows in the pictures.
 *
 * Chosen so that a reader scanning the wall sees the catalogue rather than one page eight times,
 * and so that each page lands on a face whose shape suits it: the leaderboard and the relative on
 * the widest, the lap history where zone B is tall, the radar where the zones are square, the
 * track map in the portrait face's column.
 */
export const GALLERY: Record<string, { zones: [string, string, string, string]; name?: string }> = {
  OpenDash: { zones: ['gearSpeedRevs', 'leaderboard', 'relative', 'tyres'] },
  'OpenDash 1280x720': { zones: ['gearSpeedRevs', 'lapHistory', 'relative', 'fuel'] },
  'OpenDash 1280x480': { zones: ['gearSpeedRevs', 'lapTimes', 'opponents', 'sectors'] },
  // Weather rather than the stint on this band: SimHub counts a stint from a pit exit it saw, and
  // the emulator's scripted one does not always take, which leaves a row of zeros in a photograph.
  'OpenDash 1280x400': { zones: ['speed', 'tyres', 'leaderboard', 'weather'] },
  'OpenDash 850x480': { zones: ['gearSpeedRevs', 'lapTimes', 'relative', 'fuel'] },
  'OpenDash 800x480': { zones: ['gearAlone', 'delta', 'radar', 'fuel'] },
  'OpenDash 800x286': { zones: ['gearSpeedRevs', 'fuel', 'sectors', 'fuel'] },
  'OpenDash 600x686': { zones: ['track', 'pitView', 'stint', 'relative'] },
};

export interface ManifestPackage {
  folder: string;
  kind: string;
  width: number;
  height: number;
  file: string;
  /** The theme a face is drawn in, by its catalogue id; absent on everything the default theme builds. */
  theme?: string;
}

/** Every mask bit set: the button cycles the whole catalogue, which is what a fresh screen does. */
const FULL_MASKS = [(1 << ZONE_A_PAGES.length) - 1, (1 << MODULE_CATALOGUE.length) - 1, (1 << MODULE_CATALOGUE.length) - 1, (1 << BAND_D_PAGES.length) - 1];

/** The bits of a zone B or C mask that leave exactly these catalogue pages in the cycle. */
export function catalogueMask(ids: readonly string[]): number {
  return ids.reduce((mask, id) => mask | (1 << page(id)), 0);
}

/** The resource name MSBuild gives an embedded package (OpenDash.csproj). */
const packageResource = (pkg: ManifestPackage): string => `OpenDashPlugin.Resources.${pkg.file}`;

/**
 * One screen of the gallery rig, in the shape ScreenInstance serialises to.
 *
 * A themed package's screen carries its theme, which is what gives band D the theme's pages: on a
 * Porsche, `porscheFoot` is band page 8 and the whole-catalogue mask is nine bits (#718).
 */
export function screenFor(pkg: ManifestPackage, zones: [string, string, string, string], name?: string, masks: readonly number[] = FULL_MASKS): Record<string, unknown> {
  const pages = [zoneA(zones[0]), page(zones[1]), page(zones[2]), band(zones[3], pkg.theme)];
  const theme = pkg.theme ? themeEntry(pkg.theme) : undefined;
  const bandMask = masks === FULL_MASKS ? defaultZoneMask('D', theme) : masks[3]!;
  return {
    Namespace: `Face${pkg.width}x${pkg.height}`,
    Name: name ?? pkg.folder,
    Kind: 'face',
    Width: pkg.width,
    Height: pkg.height,
    Folder: pkg.folder,
    Package: `OpenDashPlugin.Resources.${pkg.folder}.simhubdash`,
    ...(pkg.theme ? { Theme: pkg.theme } : {}),
    Face: {
      // Starts as well as Zones: the plugin opens every zone on its start page, so a zone set
      // without one comes back to the default the moment SimHub restarts.
      Zones: pages,
      Masks: [masks[0]!, masks[1]!, masks[2]!, bandMask],
      Starts: pages,
      ClassOnly: [false, false, false, false],
      BarFields: [0, 1, 5, 6],
      QuickGlance: 212,
    },
    FlagFormat: 'band',
    LapReview: 'off',
    RevBar: null,
  };
}

export function galleryRig(manifest: { packages: ManifestPackage[] }): Record<string, unknown>[] {
  const rig: Record<string, unknown>[] = [];
  for (const [folder, spec] of Object.entries(GALLERY)) {
    rig.push(screenFor(packageNamed(manifest, folder), spec.zones, spec.name));
  }
  return rig;
}

function packageNamed(manifest: { packages: ManifestPackage[] }, folder: string): ManifestPackage {
  const pkg = manifest.packages.find((p) => p.folder.toLowerCase() === folder.toLowerCase());
  if (!pkg) throw new RangeError(`build/manifest.json has no package ${JSON.stringify(folder)}; run bun run build`);
  return pkg;
}

// --------------------------------------------------------------------------------- the panel rig

/**
 * The second 1280 by 480 screen's folder. It is not the stock one, since Main dash holds that, so
 * the plugin gives it `OpenDash` and its name (PackageCatalogue.UniqueFolder), and the preset
 * deletes it once SimHub is up, so the Screens page has a screen whose dashboard is missing.
 */
export const RIM_FOLDER = 'OpenDash Rim';

/** Rim's zone B cycle: six pages, lap times among them and first. */
export const RIM_ZONE_B = ['lapTimes', 'delta', 'sectors', 'fuel', 'tyres', 'lapHistory'] as const;
/** Rim's zone C cycle: four pages, the relative among them and first. */
export const RIM_ZONE_C = ['relative', 'leaderboard', 'opponents', 'radar'] as const;

/** The wheel's own strip: the Fanatec wiring of a 3/9/3. */
export const RIM_SHAPE = '3-9-3-fanatec';

/**
 * The brow's shape: a plain 0/15/0, or the plain 15-LED shape nearest to it when the census stops
 * offering that one. "Nearest" is the fewest side LEDs, since the brow has no ends to speak of.
 */
export function browShape(): string {
  if (shapeById('0-15-0')) return '0-15-0';
  const fifteen = GRID_SHAPES.filter((s) => stripLength(s) === 15 && !s.positions).sort((a, b) => a.left + a.right - (b.left + b.right));
  if (fifteen.length === 0) throw new RangeError('the LED census offers no plain 15-LED shape for the brow');
  return fifteen[0]!.id;
}

/** SimHub's Arduino RGB LEDs device, as LedBar.ArduinoDevice spells it. */
const ARDUINO = 'arduino';

/**
 * The rig the settings panel is photographed on: every kind of screen, two strips and two matrix
 * panels, so that no page is photographed empty.
 *
 * Five screens. Main dash is the stock 1280 by 480 and so takes its namespace and folder. Rim is a
 * second screen of that size, which is what ADR 0017 exists for, with a zone B of six pages and a zone
 * C of four so the Screens page shows cycles that are not the whole catalogue; its folder is the one
 * deleted afterwards. A pit wall, a phone in portrait and the 480 round face make up the kinds.
 *
 * Two strips, both on the Arduino device, as a wheel and a brow: a Fanatec 3/9/3, which cannot be
 * reversed, and a plain 15. Two matrix panels, one in each state a panel's switches can leave it in:
 * the flag box showing both sides and the gear at rest, and a pillar showing its left side and dark.
 * Slots 3 and 4 are empty, as RemoveMatrixPanel leaves one.
 *
 * Every field is one ScreenInstance, LedBar, FaceSettings or OpenDashSettings reads, which
 * rig.test.ts checks against the C# itself, and every value is one Normalise keeps.
 */
export function panelRig(manifest: { packages: ManifestPackage[] }): Record<string, unknown> {
  const main = packageNamed(manifest, 'OpenDash 1280x480');
  const pitWall = packageNamed(manifest, 'OpenDash Pit wall');
  const phone = packageNamed(manifest, 'OpenDash Companion portrait');
  const round = packageNamed(manifest, 'OpenDash 480 round');

  // Chosen, as a screen added from the Screens page is: an unclaimed screen puts a note over the
  // cards that belongs to an upgrade and not to this rig.
  const owned = { Unclaimed: false };
  const mainDash = { ...screenFor(main, GALLERY['OpenDash 1280x480']!.zones, 'Main dash'), ...owned };
  const rim = {
    ...screenFor(main, ['gearSpeedRevs', RIM_ZONE_B[0], RIM_ZONE_C[0], 'fuel'], 'Rim', [FULL_MASKS[0]!, catalogueMask(RIM_ZONE_B), catalogueMask(RIM_ZONE_C), FULL_MASKS[3]!]),
    Namespace: 'Rim',
    Folder: RIM_FOLDER,
    Package: packageResource(main),
    ...owned,
  };
  const other = (pkg: ManifestPackage, name: string, kind: string, namespace: string): Record<string, unknown> => ({
    Namespace: namespace,
    Name: name,
    Kind: kind,
    Width: pkg.width,
    Height: pkg.height,
    Folder: pkg.folder,
    Package: packageResource(pkg),
    ...owned,
  });

  const bar = (name: string, namespace: string, shape: string): Record<string, unknown> => ({
    Namespace: namespace,
    Name: name,
    Shape: shape,
    Device: ARDUINO,
    Reversed: false,
    Brightness: null,
    EffectsOff: [],
  });

  return {
    Rig: [
      mainDash,
      rim,
      other(pitWall, 'Pit wall', 'pitwall', 'PitWall'),
      other(phone, 'Phone', 'companion', 'Companion'),
      other(round, 'Round', 'slots', `Slots${round.width}x${round.height}`),
    ],
    LedBars: [bar('Wheel rim', 'LedWheelRim', RIM_SHAPE), bar('Dash brow', 'LedDashBrow', browShape())],
    FlagBoxMatrixName: ['Flag box', 'Left pillar', null, null],
    FlagBoxSide: ['both', 'left', 'both', 'both'],
    FlagBoxRest: ['gear', 'dark', 'dark', 'dark'],
    FlagBoxFlags: [true, true, false, false],
    FlagBoxPit: [true, true, false, false],
    FlagBoxSpotter: [true, true, false, false],
    FlagBoxWarnings: [true, true, false, false],
  };
}

// --------------------------------------------------------------------------------- the guest

/** The settings file on the guest, read through the share, or an empty object when there is none. */
function readSettings(host: Host): Record<string, unknown> {
  const copied = powershell(
    host,
    `if (Test-Path -LiteralPath ${psq(SETTINGS)}) { Copy-Item -LiteralPath ${psq(SETTINGS)} -Destination ${psq(`${SHARE_UNC}\\opendash-settings.json`)} -Force; 'copied' } else { 'missing' }`,
    90,
  );
  if (!copied.ok) throw new Error(copied.stderr || 'could not read the settings file');
  if (copied.stdout.trim() === 'missing') return {};
  const local = path.join(repoRoot, 'build', 'vm-settings.json');
  mkdirSync(path.dirname(local), { recursive: true });
  const fetched = fromShare(host, 'opendash-settings.json', local);
  if (!fetched.ok) throw new Error(fetched.stderr);
  return JSON.parse(readFileSync(local, 'utf8').replace(/^\uFEFF/, '')) as Record<string, unknown>;
}

/** Deletes SimHub's copies of the settings file; PowerShell, to be run with SimHub stopped. */
const DELETE_BACKUPS = `Get-ChildItem -LiteralPath ${psq(BACKUPS)} -Filter ${psq(BACKUP_FILTER)} -ErrorAction SilentlyContinue | Remove-Item -Force`;

/**
 * Writes the settings file with SimHub stopped, deletes the copies SimHub would restore an older rig
 * from, then starts it again so the plugin reads it.
 */
function writeSettings(host: Host, settings: Record<string, unknown>, whileStopped = ''): RunResult {
  const local = path.join(repoRoot, 'build', 'vm-settings.json');
  mkdirSync(path.dirname(local), { recursive: true });
  writeFileSync(local, JSON.stringify(settings));
  const stopped = simhubStop(host);
  if (!stopped.ok) return stopped;
  const sent = toShare(host, local, 'opendash-settings.json');
  if (!sent.ok) return sent;
  const copied = powershell(
    host,
    `$ErrorActionPreference = 'Stop'
Copy-Item ${psq(`${SHARE_UNC}\\opendash-settings.json`)} -Destination ${psq(SETTINGS)} -Force
${DELETE_BACKUPS}
${whileStopped}
'written'`,
    120,
  );
  if (!copied.ok) return copied;
  return simhubStart(host);
}

/**
 * A first run: SimHub stopped, the settings file and every copy of it deleted, and every OpenDash
 * dashboard folder taken out of DashTemplates, so the plugin starts with no rig and installs nothing.
 * Deleting the file alone brings the previous rig back from `_b1`, which is why the copies go too.
 */
function resetSettings(host: Host): RunResult {
  const stopped = simhubStop(host);
  if (!stopped.ok) return stopped;
  const cleared = powershell(
    host,
    `$ErrorActionPreference = 'Stop'
Remove-Item -LiteralPath ${psq(SETTINGS)} -Force -ErrorAction SilentlyContinue
${DELETE_BACKUPS}
$gone = @(Get-ChildItem -LiteralPath ${psq(DASH_TEMPLATES)} -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'OpenDash*' })
$gone | Remove-Item -Recurse -Force
"removed the settings, their copies and $($gone.Count) OpenDash dashboard folder(s)"`,
    180,
  );
  if (!cleared.ok) return cleared;
  const started = simhubStart(host);
  return started.ok ? cleared : started;
}

/**
 * Deletes a screen's folder once the plugin has written it, so the Screens page reads that screen's
 * dashboard as missing.
 *
 * It has to wait. SimHub was just started, and the plugin's Init writes every folder on the rig
 * (DashboardInstaller.EnsureInstalled), so a folder deleted before Init has run is simply written
 * again, and one deleted before SimHub starts is written at its start. The folder appearing is the
 * sign Init has reached it; a few seconds more let it finish the rest of the rig.
 */
function dropFolderOnceWritten(host: Host, folder: string, waitSeconds = 240): RunResult {
  return powershell(
    host,
    `$target = Join-Path ${psq(DASH_TEMPLATES)} ${psq(folder)}
$deadline = (Get-Date).AddSeconds(${Math.trunc(waitSeconds)})
while (-not (Test-Path -LiteralPath $target) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 2 }
if (-not (Test-Path -LiteralPath $target)) { "the plugin never wrote $target, so there is nothing to delete; is it installed?"; exit 1 }
Start-Sleep -Seconds 8
Remove-Item -LiteralPath $target -Recurse -Force
"deleted $target, so its screen reads as missing"`,
    waitSeconds + 90,
  );
}

const describe = (settings: Record<string, unknown>): string => {
  const rig = (settings.Rig as { Name?: string; Namespace?: string; Kind?: string; Theme?: string; Face?: { Zones?: number[] } }[] | undefined) ?? [];
  if (rig.length === 0) return '  (no screens)';
  return rig
    .map((s) => {
      const z = s.Face?.Zones;
      const pages = z ? `A ${ZONE_A_PAGES[z[0]!]?.name}, B ${MODULE_CATALOGUE[z[1]!]?.name}, C ${MODULE_CATALOGUE[z[2]!]?.name}, D ${pagesForZone('D', s.Theme ? themeEntry(s.Theme) : undefined)[z[3]!]?.name}` : (s.Kind ?? 'no zones');
      return `  ${(s.Name ?? s.Namespace ?? '?').padEnd(26)} ${pages}`;
    })
    .join('\n');
};

/** The presets a caller may ask for by name. */
export const PRESETS = ['gallery', 'panel', 'clear', 'empty'] as const;
export type Preset = (typeof PRESETS)[number];

const readManifest = (): { packages: ManifestPackage[] } | string => {
  const manifestPath = path.join(repoRoot, 'build', 'manifest.json');
  if (!existsSync(manifestPath)) return 'build/manifest.json is missing; run bun run build';
  return JSON.parse(readFileSync(manifestPath, 'utf8')) as { packages: ManifestPackage[] };
};

/** What a preset does to the settings it finds, or null for `empty`, which deletes them. */
type Edit = ((settings: Record<string, unknown>) => void) | null;

/**
 * Decides what a preset writes, from build/ alone, so that a preset that cannot be put on (no
 * manifest, a face the build did not produce) says so before the VM is claimed or SimHub stopped.
 */
function planPreset(preset: Preset): Edit | RunResult {
  if (preset === 'empty') return null;
  if (preset === 'clear') return (settings) => void (settings.Rig = []);
  const manifest = readManifest();
  if (typeof manifest === 'string') return { ok: false, code: 1, stdout: '', stderr: manifest };
  try {
    if (preset === 'gallery') {
      const rig = galleryRig(manifest);
      return (settings) => void (settings.Rig = rig);
    }
    const panel = panelRig(manifest);
    return (settings) => void Object.assign(settings, panel);
  } catch (e) {
    return { ok: false, code: 1, stdout: '', stderr: e instanceof Error ? e.message : String(e) };
  }
}

const isPlan = (plan: Edit | RunResult): plan is Edit => plan === null || typeof plan === 'function';

/**
 * Puts a preset on the VM and leaves SimHub running on it. The caller holds the VM's claim: this
 * stops and starts SimHub, and `panel` waits for the plugin before it deletes a folder.
 */
export function applyPreset(host: Host, preset: Preset): RunResult {
  const plan = planPreset(preset);
  return isPlan(plan) ? putPreset(host, preset, plan) : plan;
}

function putPreset(host: Host, preset: Preset, edit: Edit): RunResult {
  if (edit === null) return resetSettings(host);
  const settings = readSettings(host);
  edit(settings);
  // The panel rig's missing folder is taken out while SimHub is stopped too, so that it appearing
  // again is Init's doing. A folder left over from an earlier start was already there when the wait
  // below began, so the wait passed at once, the delete beat Init, and Init wrote it straight back.
  const dropRim = `Remove-Item -LiteralPath ${psq(`${DASH_TEMPLATES}\\${RIM_FOLDER}`)} -Recurse -Force -ErrorAction SilentlyContinue`;
  const written = writeSettings(host, settings, preset === 'panel' ? dropRim : '');
  if (!written.ok) return written;
  if (preset !== 'panel') return { ...written, stdout: describe(settings) };
  // SimHub's process is up a second after it is started and the plugin's Init some while later; the
  // wait is on the folder, so this pause only spares the guest a few polls it would answer "no" to.
  sleep(5);
  const dropped = dropFolderOnceWritten(host, RIM_FOLDER);
  return dropped.ok ? { ...dropped, stdout: `${describe(settings)}\n  ${dropped.stdout}` } : dropped;
}

/**
 * What `theme` writes: a 1280 by 480 screen of the theme added to the rig unless one of the theme is on
 * it already, so that the Screens page draws the theme's settings, and the choices given, under the
 * names the plugin publishes them by (#715). Every other setting and every other screen is kept.
 */
export function themedEdit(manifest: { packages: ManifestPackage[] }, themeId: string, choices: readonly string[]): (settings: Record<string, unknown>) => void {
  const entry = themeEntry(themeId);
  if (!entry || entry.id === 'default') throw new RangeError(`${JSON.stringify(themeId)} is not a car theme`);
  const named = choices.map((choice) => {
    const [setting, value] = choice.split('=');
    const declared = THEME_SETTINGS[themeId]?.find((s) => s.id === setting);
    if (!declared || !declared.choices.some((c) => c.id === value)) throw new RangeError(`${JSON.stringify(choice)}: the ${themeId} theme offers ${(THEME_SETTINGS[themeId] ?? []).map((s) => `${s.id}=${s.choices.map((c) => c.id).join('|')}`).join(', ') || 'no settings'}`);
    return [themeSettingName(themeId, declared.id), value!] as const;
  });
  const screen = { ...screenFor(packageNamed(manifest, themedFolder(entry, { width: 1280, height: 480 })), ['gearSpeedRevs', 'lapTimes', 'relative', 'fuel']), Namespace: `${entry.name}1280x480`, Unclaimed: false };
  return (settings) => {
    const rig = (settings.Rig as { Theme?: string }[] | undefined) ?? [];
    if (!rig.some((s) => s.Theme === themeId)) settings.Rig = [...rig, screen];
    settings.ThemeSettings = { ...((settings.ThemeSettings as Record<string, string> | undefined) ?? {}), ...Object.fromEntries(named) };
  };
}

const USAGE = `rig: put a set of screens on the VM's plugin, so the captures show more than one layout.

  bun scripts/rig.ts show       what is on the rig now
  bun scripts/rig.ts gallery    the rig the website's captures are taken on
  bun scripts/rig.ts panel      the rig the settings panel's captures are taken on: Main dash and Rim
                                (1280x480 twice), a pit wall, a phone and the 480 round; a Fanatec
                                3/9/3 wheel rim and a 15-LED brow; a flag box and a left pillar.
                                Rim's dashboard folder is deleted once SimHub is up, so it reads
                                as missing
  bun scripts/rig.ts clear      no screens, every other setting kept
  bun scripts/rig.ts empty      a genuine first run: the settings and SimHub's copies of them
                                deleted, and every OpenDash folder taken out of DashTemplates
  bun scripts/rig.ts theme <id> [<setting>=<choice> ...]
                                a 1280x480 screen of the theme added unless the rig has one, and
                                the theme's own settings set: theme aim backlight=inverted

SimHub is stopped and started again, because the plugin reads its settings once at startup, so a
preset claims the VM for as long as it takes, and is refused while another session holds it. Run
inside a claim of your own (bun run vm claim "..." && bun scripts/rig.ts panel), it leaves that
claim held.
`;

export async function main(argv: readonly string[], host: Host = resolveHost()): Promise<number> {
  const command = argv[0] ?? 'show';
  if (command === '--help' || command === '-h' || command === 'help') {
    console.log(USAGE);
    return 0;
  }
  if (command !== 'show' && command !== 'theme' && !(PRESETS as readonly string[]).includes(command)) {
    console.error(USAGE);
    return 2;
  }
  if (command === 'show') {
    console.log(describe(readSettings(host)));
    return 0;
  }
  if (command === 'theme') {
    const manifest = readManifest();
    if (typeof manifest === 'string') {
      console.error(manifest);
      return 1;
    }
    let edit: (settings: Record<string, unknown>) => void;
    try {
      edit = themedEdit(manifest, argv[1] ?? '', argv.slice(2));
    } catch (e) {
      console.error(e instanceof Error ? e.message : String(e));
      return 2;
    }
    const applied = withClaim(host, `rig theme ${argv.slice(1).join(' ')}`, () => {
      const settings = readSettings(host);
      edit(settings);
      const written = writeSettings(host, settings);
      return written.ok ? { ...written, stdout: `${describe(settings)}\n  ${JSON.stringify(settings.ThemeSettings)}` } : written;
    });
    if (applied.stdout) console.log(applied.stdout);
    if (!applied.ok) console.error(applied.stderr || 'the theme did not take');
    return applied.ok ? 0 : 1;
  }
  const preset = command as Preset;
  const plan = planPreset(preset);
  if (!isPlan(plan)) {
    console.error(plan.stderr);
    return 1;
  }
  const applied = withClaim(host, `rig ${preset}`, () => putPreset(host, preset, plan));
  if (applied.stdout) console.log(applied.stdout);
  if (!applied.ok) console.error(applied.stderr || 'the preset did not take');
  return applied.ok ? 0 : 1;
}

if (import.meta.main) process.exit(await main(process.argv.slice(2)));
