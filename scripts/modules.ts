#!/usr/bin/env bun
/**
 * modules: photograph every companion module on its own, one per picture.
 *
 * Why this exists rather than a flag on `shots`. The companion is one dashboard of twenty-one
 * screens, and which screen shows is decided by SimHub's own Next/Previous ring over the screens
 * whose `enabledExpression` is above zero — `isnull([OpenDash.CompanionModule07], 1)`. The plugin
 * reads those settings once, at SimHub start, from SimHub's own settings store, and writes them
 * back at shutdown. So there are only two ways to photograph module seven specifically: drive
 * SimHub's screen navigation from outside, which needs a control binding this repository does not
 * own, or restart SimHub with a different settings file twenty-one times, which is over an hour of
 * restarts on a two-core VM.
 *
 * The third way is to stop asking SimHub to choose. A companion screen is `companionScreen(size,
 * page)` and a dashboard is a list of screens, so a package holding exactly one of them is a few
 * lines. Twenty-one such packages install in one pass, SimHub restarts once, and the existing
 * open-and-capture loop photographs them exactly as it photographs any other package.
 *
 * What is photographed is therefore the real module, built by the real builder at the real
 * density, rendered by SimHub against live telemetry. The only thing that is not the shipped
 * package is that this dashboard has one screen where the shipped one has twenty-one — the header,
 * the page dots and the flag band are all still the companion's own, which is why the dots read
 * "one of twenty-one" in every picture.
 *
 *   bun scripts/modules.ts                       # all 21, on the green scenario
 *   bun scripts/modules.ts --modules fuel,tyres  # by id
 *   bun scripts/modules.ts --scenario race --keep
 *
 * The packages it installs are named `OpenDash module <nn> <id>` so that they sort in catalogue
 * order in Dash Studio's list and never collide with a shipped name. They are scratch: nothing
 * removes them from the VM, and the next `bun run dev` install pass leaves them alone.
 */
import { mkdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { MODULE_CATALOGUE } from '../packages/dash/src/contract.ts';
import { COMPANION_SIZES, companionScreen } from '../packages/dash/src/screens/index.ts';
import { fontsForScreens } from '../packages/dash/src/screens/index.ts';
import { writePackage, zipPackage, type DashPackage, type Dashboard } from '../packages/dash/src/generator.ts';
import { DEFAULT_AUTHOR, DEFAULT_SIMHUB_VERSION } from '../packages/dash/src/dashboard.ts';
import { readVersion } from '../packages/dash/src/build.ts';
import { noticesForPackage } from '../packages/dash/src/design/notices.ts';
import { ds } from '../packages/dash/src/tokens.ts';
import { build as buildEmulator, start as startEmulator, stop as stopEmulator, upload as uploadEmulator } from './emulator.ts';
import { captureDashboard, closeDashboards, guiAvailable, openDashboard, placeDashboards } from './gui.ts';
import { claim, install, readClaim, release, resolveHost, sleep, status, up, waitReady, whoAmI, type Host } from './vm.ts';

const repoRoot = path.resolve(import.meta.dir, '..');

/** The landscape companion. The portrait one is the same modules in a narrower box. */
const SIZE = COMPANION_SIZES[0]!;

/** `OpenDash module 05 fuel`: sorts in catalogue order and cannot collide with a shipped name. */
export const packageNameFor = (number: number, id: string): string =>
  `OpenDash module ${String(number).padStart(2, '0')} ${id}`;

/**
 * One package holding one module.
 *
 * The screen's `enabledExpression` is dropped. Three modules ship switched off, so the expression
 * they carry would evaluate to zero against the plugin the VM has installed and SimHub would show
 * an empty dashboard — a picture of a setting rather than of a module.
 */
export function modulePackage(number: number, id: string, version: string): DashPackage {
  const metadata = {
    title: packageNameFor(number, id),
    author: DEFAULT_AUTHOR,
    description: `${id}, alone, for a capture. Not a shipped package.`,
    version,
    simHubVersion: DEFAULT_SIMHUB_VERSION,
  };
  const { enabledExpression: _dropped, ...screen } = companionScreen(SIZE, number);
  const dashboard: Dashboard = {
    name: packageNameFor(number, id),
    width: SIZE.width,
    height: SIZE.height,
    backgroundColor: screen.backgroundColor ?? ds.color.surface.base,
    screens: [screen],
    metadata,
  };
  const pkg: DashPackage = { folderName: dashboard.name, dashboards: [dashboard], fonts: fontsForScreens() };
  pkg.notices = noticesForPackage(pkg);
  return pkg;
}

export interface Options {
  modules: readonly string[];
  scenario: string;
  outDir: string;
  keep: boolean;
}

const list = (value: string | undefined): string[] | undefined =>
  value === undefined ? undefined : value.split(',').map((s) => s.trim()).filter((s) => s.length > 0);

export function parseArgs(argv: readonly string[]): Options | { help: true } {
  if (argv.includes('--help') || argv.includes('-h')) return { help: true };
  const flagValue = (name: string): string | undefined => {
    const index = argv.indexOf(`--${name}`);
    if (index >= 0 && argv[index + 1] && !argv[index + 1]!.startsWith('--')) return argv[index + 1];
    return argv.find((a) => a.startsWith(`--${name}=`))?.split('=').slice(1).join('=');
  };
  return {
    modules: list(flagValue('modules')) ?? MODULE_CATALOGUE.map((m) => m.id),
    scenario: flagValue('scenario') ?? 'green',
    outDir: flagValue('out') ?? path.join(repoRoot, 'build/shots/modules'),
    keep: argv.includes('--keep'),
  };
}

const USAGE = `modules: photograph every companion module on its own.

  bun scripts/modules.ts [--modules a,b] [--scenario green] [--out dir] [--keep]

  --modules   comma separated module ids; default all ${MODULE_CATALOGUE.length}
              ${MODULE_CATALOGUE.map((m) => m.id).join(', ')}
  --scenario  which telemetry to replay; default "green"
  --out       where the PNGs go; default build/shots/modules
  --keep      leave the emulator running and the VM claimed when this returns

It builds one single-screen companion package per module, installs them all in one pass so that
SimHub restarts once, and then opens and photographs each. See the comment at the top of this file
for why the shipped companion cannot be paged from outside.
`;

export async function run(host: Host, opts: Options): Promise<number> {
  const wanted = MODULE_CATALOGUE.filter((m) => opts.modules.includes(m.id));
  const unknown = opts.modules.filter((id) => !MODULE_CATALOGUE.some((m) => m.id === id));
  if (unknown.length > 0) {
    console.error(`unknown module${unknown.length > 1 ? 's' : ''}: ${unknown.join(', ')}`);
    return 1;
  }

  const held = readClaim(host);
  if (held && held.who !== whoAmI()) {
    console.error(`the VM is claimed by ${held.who} since ${held.since}${held.note ? ` (${held.note})` : ''}`);
    console.error('There is one VM. Wait, or ask them to run `bun run vm release`.');
    return 1;
  }
  const claimed = claim(host, `modules ${wanted.length}`);
  if (!claimed.ok) {
    console.error(claimed.stderr);
    return 1;
  }

  try {
    if (!status(host).stdout.includes('guest-ssh: up')) {
      console.log('starting the VM');
      up(host);
      if (!waitReady(host, 300)) {
        console.error('the VM did not answer within five minutes');
        return 1;
      }
    }

    // `install` globs inside build/, so that is where these have to be written, and build/ is also
    // what `bun run package` copies wholesale into the plugin's resources. A capture package left
    // there would be embedded in the next release, so they are deleted the moment SimHub has them
    // — which is as soon as install returns, since from then on the copies that matter are the
    // ones on the VM.
    const buildDir = path.join(repoRoot, 'build');
    mkdirSync(buildDir, { recursive: true });
    const version = readVersion();
    console.log(`building ${wanted.length} single-module packages`);
    const folders = wanted.map((meta) => {
      const pkg = modulePackage(meta.number, meta.id, version);
      writePackage(pkg, buildDir);
      zipPackage(buildDir, pkg.folderName);
      return pkg.folderName;
    });

    console.log('installing them, which restarts SimHub once');
    const installed = install(host, folders);
    for (const folder of folders) {
      rmSync(path.join(buildDir, folder), { recursive: true, force: true });
      rmSync(path.join(buildDir, `${folder}.simhubdash`), { force: true });
    }
    if (!installed.ok) {
      console.error(installed.stderr);
      return 1;
    }

    if (!guiAvailable(host)) {
      console.error('no desktop session; the VM must be logged in (see docs/testing-vm.md)');
      return 1;
    }

    console.log(`starting the emulator on "${opts.scenario}"`);
    buildEmulator();
    uploadEmulator(host);
    startEmulator(host, { scenario: opts.scenario, replace: true });
    sleep(6);

    mkdirSync(opts.outDir, { recursive: true });
    let taken = 0;
    for (const [index, meta] of wanted.entries()) {
      const name = packageNameFor(meta.number, meta.id);
      closeDashboards(host);
      const opened = openDashboard(host, { name });
      if (!opened.ok) {
        console.error(`  [${index + 1}/${wanted.length}] ${meta.name}: could not open (${opened.stderr.trim()})`);
        continue;
      }
      placeDashboards(host, 0, 0, { name, width: SIZE.width, height: SIZE.height });
      const file = path.join(opts.outDir, `module-${meta.id}.png`);
      const shot = captureDashboard(host, name, file);
      if (shot.ok) {
        taken += 1;
        console.log(`  [${index + 1}/${wanted.length}] ${meta.name} photographed`);
      } else {
        console.error(`  [${index + 1}/${wanted.length}] ${meta.name}: ${shot.stderr.trim()}`);
      }
    }

    console.log(`\n  ${taken} of ${wanted.length} photographed into ${path.relative(repoRoot, opts.outDir)}/\n`);
    return taken === wanted.length ? 0 : 1;
  } finally {
    if (!opts.keep) {
      stopEmulator(host);
      closeDashboards(host);
      release(host);
    }
  }
}

if (import.meta.main) {
  const opts = parseArgs(process.argv.slice(2));
  if ('help' in opts) {
    console.log(USAGE);
    process.exit(0);
  }
  process.exit(await run(resolveHost(), opts));
}
