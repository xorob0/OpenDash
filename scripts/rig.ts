#!/usr/bin/env bun
/**
 * rig: put a set of screens on the VM's plugin, so the captures show more than one layout.
 *
 * Every face draws the same four default pages, which is right for a first run and wrong for a
 * wall of pictures: ten photographs of lap times, the gear and the relative say less about
 * twenty-one pages than eight photographs of eight different ones. The plugin already decides what
 * a zone shows, and it keeps that decision in its settings file, so this writes a rig there rather
 * than clicking through the panel forty times.
 *
 * A screen whose namespace is the stock one for its size takes the stock folder and the stock
 * package byte for byte (ADR 0017), so a seeded rig changes what the packages read, never what
 * they are. Its name is the package's own name, because SimHub lists a dashboard by its title and
 * `bun run shots` finds it by that.
 *
 *   bun scripts/rig.ts show       # what is on the rig now
 *   bun scripts/rig.ts gallery    # the rig the website's captures are taken on
 *   bun scripts/rig.ts clear      # back to one screen, as a first run leaves it
 */
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { BAND_D_PAGES, MODULE_CATALOGUE, ZONE_A_PAGES } from '../packages/dash/src/contract.ts';
import { fromShare, powershell, psq, resolveHost, simhubStart, simhubStop, toShare, type Host, type RunResult } from './vm.ts';

const repoRoot = path.resolve(import.meta.dir, '..');
const SETTINGS = 'C:\\Program Files (x86)\\SimHub\\PluginsData\\Common\\OpenDash.GeneralSettings.json';
const SHARE_UNC = '\\\\host.lan\\Data';

/** A zone's page, by the id the catalogue gives it, so a table of names cannot drift into numbers. */
const zoneA = (id: string): number => index(ZONE_A_PAGES, id, 'zone A');
const band = (id: string): number => index(BAND_D_PAGES, id, 'band D');
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
}

/** Every mask bit set: the button cycles the whole catalogue, which is what a fresh screen does. */
const FULL_MASKS = [(1 << ZONE_A_PAGES.length) - 1, (1 << MODULE_CATALOGUE.length) - 1, (1 << MODULE_CATALOGUE.length) - 1, (1 << BAND_D_PAGES.length) - 1];

/** One screen of the gallery rig, in the shape ScreenInstance serialises to. */
export function screenFor(pkg: ManifestPackage, zones: [string, string, string, string], name?: string): Record<string, unknown> {
  const pages = [zoneA(zones[0]), page(zones[1]), page(zones[2]), band(zones[3])];
  return {
    Namespace: `Face${pkg.width}x${pkg.height}`,
    Name: name ?? pkg.folder,
    Kind: 'face',
    Width: pkg.width,
    Height: pkg.height,
    Folder: pkg.folder,
    Package: `OpenDashPlugin.Resources.${pkg.folder}.simhubdash`,
    Face: {
      // Starts as well as Zones: the plugin opens every zone on its start page, so a zone set
      // without one comes back to the default the moment SimHub restarts.
      Zones: pages,
      Masks: FULL_MASKS,
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
    const pkg = manifest.packages.find((p) => p.folder.toLowerCase() === folder.toLowerCase());
    if (!pkg) throw new RangeError(`build/manifest.json has no package ${JSON.stringify(folder)}; run bun run build`);
    rig.push(screenFor(pkg, spec.zones, spec.name));
  }
  return rig;
}

/** The settings file on the guest, read through the share. */
function readSettings(host: Host): Record<string, unknown> {
  const copied = powershell(host, `Copy-Item ${psq(SETTINGS)} ${psq(`${SHARE_UNC}\\opendash-settings.json`)} -Force; 'copied'`, 90);
  if (!copied.ok) throw new Error(copied.stderr || 'could not read the settings file');
  const local = path.join(repoRoot, 'build', 'vm-settings.json');
  mkdirSync(path.dirname(local), { recursive: true });
  const fetched = fromShare(host, 'opendash-settings.json', local);
  if (!fetched.ok) throw new Error(fetched.stderr);
  return JSON.parse(readFileSync(local, 'utf8')) as Record<string, unknown>;
}

/** Writes the settings file with SimHub stopped, then starts it again so the plugin reads it. */
function writeSettings(host: Host, settings: Record<string, unknown>): RunResult {
  const local = path.join(repoRoot, 'build', 'vm-settings.json');
  writeFileSync(local, JSON.stringify(settings));
  simhubStop(host);
  const sent = toShare(host, local, 'opendash-settings.json');
  if (!sent.ok) return sent;
  const copied = powershell(host, `Copy-Item ${psq(`${SHARE_UNC}\\opendash-settings.json`)} ${psq(SETTINGS)} -Force; 'written'`, 120);
  if (!copied.ok) return copied;
  return simhubStart(host);
}

const describe = (settings: Record<string, unknown>): string => {
  const rig = (settings.Rig as { Name?: string; Namespace?: string; Face?: { Zones?: number[] } }[] | undefined) ?? [];
  if (rig.length === 0) return '  (no screens)';
  return rig
    .map((s) => {
      const z = s.Face?.Zones;
      const pages = z ? `A ${ZONE_A_PAGES[z[0]!]?.name}, B ${MODULE_CATALOGUE[z[1]!]?.name}, C ${MODULE_CATALOGUE[z[2]!]?.name}, D ${BAND_D_PAGES[z[3]!]?.name}` : 'no zones';
      return `  ${(s.Name ?? s.Namespace ?? '?').padEnd(26)} ${pages}`;
    })
    .join('\n');
};

const USAGE = `rig: put a set of screens on the VM's plugin, so the captures show more than one layout.

  bun scripts/rig.ts show       what is on the rig now
  bun scripts/rig.ts gallery    the rig the website's captures are taken on
  bun scripts/rig.ts clear      back to one screen, as a first run leaves it

SimHub is stopped and started again, because the plugin reads its settings once at startup.
`;

export async function main(argv: readonly string[]): Promise<number> {
  const command = argv[0] ?? 'show';
  if (command === '--help' || command === '-h' || command === 'help') {
    console.log(USAGE);
    return 0;
  }
  const host = resolveHost();
  const settings = readSettings(host);

  if (command === 'show') {
    console.log(describe(settings));
    return 0;
  }
  if (command === 'gallery') {
    const manifestPath = path.join(repoRoot, 'build', 'manifest.json');
    if (!existsSync(manifestPath)) {
      console.error('build/manifest.json is missing; run bun run build');
      return 1;
    }
    const manifest = JSON.parse(readFileSync(manifestPath, 'utf8')) as { packages: ManifestPackage[] };
    settings.Rig = galleryRig(manifest);
    const written = writeSettings(host, settings);
    if (!written.ok) {
      console.error(written.stderr || written.stdout);
      return 1;
    }
    console.log(describe(settings));
    return 0;
  }
  if (command === 'clear') {
    settings.Rig = [];
    const written = writeSettings(host, settings);
    if (!written.ok) {
      console.error(written.stderr || written.stdout);
      return 1;
    }
    console.log('  (no screens)');
    return 0;
  }
  console.error(USAGE);
  return 2;
}

if (import.meta.main) process.exit(await main(process.argv.slice(2)));
