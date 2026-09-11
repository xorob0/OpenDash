#!/usr/bin/env bun
/**
 * shots: photograph every package on one claim of the VM.
 *
 * `bun run dev` already does the hard part — it claims the VM, builds, installs, restarts SimHub,
 * starts the emulator on a pinned scenario, opens the dash and captures the window with
 * `PrintWindow`, which is the native renderer at the dashboard's own size rather than a desktop
 * grab. What it does not do is repeat, and a pull request that wants to show four faces should not
 * claim the VM four times and restart SimHub four times to get them.
 *
 * So this is the loop around it. The expensive steps are hoisted out: every package is installed in
 * one go, which restarts SimHub once, and the emulator is restarted once per scenario rather than
 * once per capture. What is left in the inner loop is opening a dashboard and photographing it.
 *
 * The order is scenario first, package second. The other way round would restart the emulator once
 * per capture, and an emulator restart costs a SimHub reconnection.
 */
import { mkdirSync } from 'node:fs';
import path from 'node:path';
import { build as buildEmulator, start as startEmulator, stop as stopEmulator, upload as uploadEmulator, scenarios } from './emulator.ts';
import { captureDashboard, closeDashboards, guiAvailable, openDashboard, placeDashboards } from './gui.ts';
import { claim, install, readClaim, release, resolveHost, sleep, status, up, waitReady, whoAmI, type Host } from './vm.ts';

const repoRoot = path.resolve(import.meta.dir, '..');

// The package list and the size a window is given both live in dev.ts, which is the one place they
// are written; a second copy would drift the first time a package is added.
import { LIST_ORDER, packageSize } from './dev.ts';

/** A package name the opener knows how to click. */
export type PackageName = (typeof LIST_ORDER)[number];

/** The dash faces: everything in the list that is not a companion or a pit wall. */
export const FACES: readonly PackageName[] = LIST_ORDER.filter((n) => !n.includes('Companion') && !n.includes('Pit wall'));

export interface ShotsOptions {
  packages: readonly string[];
  scenarios: readonly string[];
  /** Where the PNGs go; inside build/, which is already gitignored. */
  outDir: string;
  noBuild: boolean;
  keep: boolean;
}

const list = (value: string | undefined): string[] | undefined =>
  value === undefined ? undefined : value.split(',').map((s) => s.trim()).filter((s) => s.length > 0);

export function parseArgs(argv: readonly string[]): ShotsOptions | { help: true } {
  if (argv.includes('--help') || argv.includes('-h')) return { help: true };
  const flagValue = (name: string): string | undefined => {
    const index = argv.indexOf(`--${name}`);
    if (index >= 0 && argv[index + 1] && !argv[index + 1]!.startsWith('--')) return argv[index + 1];
    return argv.find((a) => a.startsWith(`--${name}=`))?.split('=').slice(1).join('=');
  };
  return {
    packages: list(flagValue('packages')) ?? FACES,
    scenarios: list(flagValue('scenarios')) ?? ['green'],
    outDir: flagValue('out') ?? path.join(repoRoot, 'build/shots'),
    noBuild: argv.includes('--no-build'),
    keep: argv.includes('--keep'),
  };
}

/**
 * What a capture is called.
 *
 * Numbered so that a directory listing is in the order the loop ran, which is the order a contact
 * sheet should read in; the package and the scenario so that a file dropped into a pull request
 * says what it is without a caption.
 */
export function shotName(index: number, packageName: string, scenario: string): string {
  const slug = (s: string): string => s.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  return `${String(index).padStart(2, '0')}-${slug(packageName)}-${slug(scenario)}.png`;
}

const USAGE = `shots: photograph every package on one claim of the VM.

  bun run shots [--packages a,b] [--scenarios green,yellow] [--out dir] [--no-build] [--keep]

  --packages   comma separated; default the ten faces
               ${LIST_ORDER.join(', ')}
  --scenarios  comma separated; default "green"
               ${scenarios().join(', ') || '(none built)'}
  --out        where the PNGs go; default build/shots
  --no-build   skip the dashboard build, when only the scenario changed
  --keep       leave the emulator running and the VM claimed when this returns

It claims the VM once for the whole batch, installs every package in one pass so SimHub restarts
once, and restarts the emulator once per scenario. A reviewed capture belongs in media/<issue>/;
build/shots is scratch.
`;

export interface Shot {
  packageName: string;
  scenario: string;
  file: string;
  ok: boolean;
  why?: string;
}

export async function shots(host: Host, opts: ShotsOptions): Promise<number> {
  const unknown = opts.packages.filter((p) => !LIST_ORDER.includes(p as (typeof LIST_ORDER)[number]));
  if (unknown.length > 0) {
    console.error(`unknown package${unknown.length > 1 ? 's' : ''}: ${unknown.join(', ')}`);
    console.error(`one of ${LIST_ORDER.join(', ')}`);
    return 1;
  }
  const built = scenarios();
  const missing = built.length > 0 ? opts.scenarios.filter((s) => !built.includes(s)) : [];
  if (missing.length > 0) {
    console.error(`unknown scenario${missing.length > 1 ? 's' : ''}: ${missing.join(', ')}`);
    console.error(`one of ${built.join(', ')}`);
    return 1;
  }

  const held = readClaim(host);
  if (held && held.who !== whoAmI()) {
    console.error(`the VM is claimed by ${held.who} since ${held.since}${held.note ? ` (${held.note})` : ''}`);
    console.error('There is one VM. Wait, or ask them to run `bun run vm release`.');
    return 1;
  }
  const claimed = claim(host, `shots ${opts.packages.length}x${opts.scenarios.length}`);
  if (!claimed.ok) {
    console.error(claimed.stderr);
    return 1;
  }

  const taken: Shot[] = [];
  try {
    if (!status(host).stdout.includes('guest-ssh: up')) {
      console.log('starting the VM');
      up(host);
      if (!waitReady(host, 300)) {
        console.error('the VM did not answer within five minutes; try `bun run vm status`');
        return 1;
      }
    }

    if (!opts.noBuild) {
      console.log('building the packages');
      const b = Bun.spawnSync(['bun', 'run', 'build'], { cwd: repoRoot, stdout: 'pipe', stderr: 'pipe' });
      if (b.exitCode !== 0) {
        console.error(new TextDecoder().decode(b.stderr) || new TextDecoder().decode(b.stdout));
        return 1;
      }
    }

    // One install for the whole batch. SimHub reads its template list once at startup, so this is
    // also the one SimHub restart: installing per package would restart it once per package.
    console.log(`installing ${opts.packages.length} package${opts.packages.length > 1 ? 's' : ''} and restarting SimHub`);
    const installed = install(host, opts.packages);
    if (!installed.ok) {
      console.error(installed.stderr || installed.stdout);
      return 1;
    }

    if (!guiAvailable(host)) {
      console.error('the VNC tooling is not on the VM host, so a dash cannot be opened from here');
      return 1;
    }

    const emulator = buildEmulator();
    if (!emulator.ok) {
      console.error(emulator.stderr || emulator.stdout);
      return 1;
    }
    const uploaded = uploadEmulator(host);
    if (!uploaded.ok) {
      console.error(uploaded.stderr);
      return 1;
    }

    mkdirSync(opts.outDir, { recursive: true });
    let index = 1;
    const total = opts.packages.length * opts.scenarios.length;

    for (const scenario of opts.scenarios) {
      console.log(`\nscenario ${scenario}`);
      const running = startEmulator(host, { scenario, replace: true });
      if (!running.ok) {
        console.error(running.stderr);
        return 1;
      }
      // SimHub takes a moment to notice the reconnection; a dash opened before it does draws its
      // defaults, which is a photograph of nothing.
      sleep(6);

      for (const packageName of opts.packages) {
        const file = path.join(opts.outDir, shotName(index, packageName, scenario));
        process.stdout.write(`  [${index}/${total}] ${packageName} `);

        const opened = openDashboard(host, { name: packageName, index: LIST_ORDER.indexOf(packageName as (typeof LIST_ORDER)[number]) });
        if (!opened.ok) {
          console.log('could not be opened');
          taken.push({ packageName, scenario, file, ok: false, why: opened.stderr.split('\n')[0] });
          index += 1;
          continue;
        }

        sleep(5);
        const size = packageSize(packageName);
        placeDashboards(host, 0, 0, size ? { name: packageName, ...size } : undefined);
        sleep(2);

        const captured = captureDashboard(host, packageName, file);
        taken.push({ packageName, scenario, file, ok: captured.ok, why: captured.ok ? undefined : captured.stderr.split('\n')[0] });
        console.log(captured.ok ? 'photographed' : `capture failed (${captured.stderr.split('\n')[0]})`);

        // Closed before the next one opens. Ten dash windows rendering at once on two cores is
        // what makes a later capture come back half-drawn.
        closeDashboards(host);
        index += 1;
      }
    }

    const good = taken.filter((s) => s.ok);
    console.log('');
    console.log(`  ${good.length} of ${taken.length} photographed into ${path.relative(repoRoot, opts.outDir)}/`);
    for (const s of taken.filter((x) => !x.ok)) console.log(`  missing  ${s.packageName} on ${s.scenario}: ${s.why ?? 'unknown'}`);
    console.log('');
    console.log('  a capture worth showing belongs in media/<issue>/; this directory is scratch');
    return good.length === taken.length ? 0 : 1;
  } finally {
    if (!opts.keep) {
      closeDashboards(host);
      stopEmulator(host);
      release(host);
    }
  }
}

if (import.meta.main) {
  const opts = parseArgs(process.argv.slice(2));
  if ('help' in opts) {
    console.log(USAGE);
  } else {
    process.exit(await shots(resolveHost(), opts));
  }
}
