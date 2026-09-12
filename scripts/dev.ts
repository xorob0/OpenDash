#!/usr/bin/env bun
/**
 * dev: from a clean machine to a dash rendering live telemetry, in one command.
 *
 * Everything here is orchestration. The VM and SimHub are `scripts/vm.ts`, the telemetry is
 * `scripts/emulator.ts`, and the clicking is `scripts/gui.ts`; this file decides the order, holds
 * the claim on the VM while it works, and says at the end what it did.
 *
 * The order matters in two places. SimHub reads its template list once at startup, so a package
 * has to be installed before SimHub is started rather than after. And the emulator has to be
 * running before the dash is opened, because a dash opened against a disconnected SimHub draws its
 * defaults and looks broken.
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { build as buildEmulator, runningPid, start as startEmulator, stop as stopEmulator, upload as uploadEmulator, scenarios } from './emulator.ts';
import { captureDashboard, guiAvailable, openDashboard, placeDashboards } from './gui.ts';
import { claim, install, readClaim, release, resolveHost, screenshot, simhubStop, sleep, status, up, waitReady, whoAmI, type Host } from './vm.ts';

const repoRoot = path.resolve(import.meta.dir, '..');

/**
 * The faces, in the order Dash Studio lists them when filtered by "openDash".
 *
 * `openDashboard` clicks a row by its index in that list, so the order is not cosmetic. `shots.ts`
 * reads the same list rather than keeping a second copy.
 *
 * Since XOR-118 the zone faces carry the shipped names and the card faces are the ones saying
 * "slots". That makes the bare `openDash` a prefix of every other entry, so filtering the list by it
 * matches all of them and the opener takes the first row; it is first here because it is first
 * there, and that is checked on the VM rather than assumed, since this order is read off Dash
 * Studio and is not a sort this repository performs.
 */
export const LIST_ORDER = [
  'openDash',
  'openDash 1280x400',
  'openDash 1280x480',
  'openDash 1280x720',
  'openDash 480 round',
  'openDash 600x686',
  'openDash 800 round',
  'openDash 800x286',
  'openDash 800x480',
  'openDash 850x480',
  // The card faces, built beside the zone faces for comparison until XOR-95 deletes them.
  'openDash slots 1280x400',
  'openDash slots 1280x480',
  'openDash slots 1280x720',
  'openDash slots 1920x480',
  'openDash slots 600x686',
  'openDash slots 800x286',
  'openDash slots 800x480',
  'openDash slots 850x480',
  'openDash Companion',
  'openDash Companion portrait',
  'openDash Pit wall',
  'openDash Pit wall portrait',
] as const;

export interface DevOptions {
  /** Which package to install and open. */
  packageName: string;
  scenario: string;
  /** Skip the dashboard build, for when only the scenario changed. */
  noBuild: boolean;
  /** Leave the emulator running and the claim held; for handing the VM to a longer session. */
  keep: boolean;
}

export function parseArgs(argv: readonly string[]): DevOptions | { help: true } {
  if (argv.includes('--help') || argv.includes('-h')) return { help: true };
  const positional = argv.filter((a) => !a.startsWith('--'));
  const flagValue = (name: string): string | undefined => {
    const index = argv.indexOf(`--${name}`);
    if (index >= 0 && argv[index + 1] && !argv[index + 1]!.startsWith('--')) return argv[index + 1];
    return argv.find((a) => a.startsWith(`--${name}=`))?.split('=').slice(1).join('=');
  };
  const scenario = flagValue('scenario') ?? 'race';
  // The scenario is also positional-looking once `--scenario race` has been read, so it is removed.
  const named = positional.filter((a) => a !== scenario);
  return {
    packageName: named[0] ?? 'openDash',
    scenario,
    noBuild: argv.includes('--no-build'),
    keep: argv.includes('--keep'),
  };
}

const USAGE = `dev: bring the whole rig up and leave a dash rendering live telemetry.

  bun run dev [package] [--scenario <name>] [--no-build] [--keep]

  package     which one to install and open; default "openDash"
              ${LIST_ORDER.join(', ')}
  --scenario  which telemetry to replay; default "race"
              ${scenarios().join(', ') || '(none built)'}
  --no-build  skip the dashboard build, when only the scenario changed
  --keep      leave the emulator running and the VM claimed when this returns

It claims the VM first and refuses if somebody else holds it, starts the VM if it is down, builds,
installs, starts SimHub, starts the emulator, opens the dash and screenshots it.
`;

/** A package's own pixel size, from the manifest the build writes. */
export function packageSize(name: string): { width: number; height: number } | undefined {
  const manifest = path.join(repoRoot, 'build/manifest.json');
  if (!existsSync(manifest)) return undefined;
  const packages = (JSON.parse(readFileSync(manifest, 'utf8')) as { packages: { folder: string; width: number; height: number }[] }).packages;
  const found = packages.find((p) => p.folder === name);
  return found ? { width: found.width, height: found.height } : undefined;
}

function step(n: number, of: number, what: string): void {
  console.log(`[${n}/${of}] ${what}`);
}

export async function dev(host: Host, opts: DevOptions): Promise<number> {
  const steps = 8;
  const shot = path.join(repoRoot, 'build/dev.png');

  step(1, steps, 'claiming the VM');
  const held = readClaim(host);
  if (held && held.who !== whoAmI()) {
    console.error(`the VM is claimed by ${held.who} since ${held.since}${held.note ? ` (${held.note})` : ''}`);
    console.error('There is one VM. Wait, or ask them to run `bun run vm release`.');
    return 1;
  }
  const claimed = claim(host, `dev ${opts.packageName}`);
  if (!claimed.ok) {
    console.error(claimed.stderr);
    return 1;
  }

  try {
    step(2, steps, 'starting the VM if it is down');
    if (!status(host).stdout.includes('guest-ssh: up')) {
      up(host);
      if (!waitReady(host, 300)) {
        console.error('the VM did not answer within five minutes; try `bun run vm status` and `bun run vm shot`');
        return 1;
      }
    }

    step(3, steps, opts.noBuild ? 'skipping the build' : 'building the packages');
    if (!opts.noBuild) {
      const built = Bun.spawnSync(['bun', 'run', 'build'], { cwd: repoRoot, stdout: 'pipe', stderr: 'pipe' });
      if (built.exitCode !== 0) {
        console.error(new TextDecoder().decode(built.stderr) || new TextDecoder().decode(built.stdout));
        return 1;
      }
    }

    step(4, steps, `installing ${opts.packageName} and restarting SimHub`);
    simhubStop(host);
    const installed = install(host, [opts.packageName]);
    if (!installed.ok) {
      console.error(installed.stderr || installed.stdout);
      return 1;
    }
    console.log(`      ${installed.stdout.split('\n').join('\n      ')}`);

    step(5, steps, `starting the emulator on "${opts.scenario}"`);
    const emulatorBuilt = buildEmulator();
    if (!emulatorBuilt.ok) {
      console.error(emulatorBuilt.stderr || emulatorBuilt.stdout);
      return 1;
    }
    const uploaded = uploadEmulator(host);
    if (!uploaded.ok) {
      console.error(uploaded.stderr);
      return 1;
    }
    const running = startEmulator(host, { scenario: opts.scenario, replace: true });
    if (!running.ok) {
      console.error(running.stderr);
      return 1;
    }
    console.log(`      ${running.stdout.split('\n').join('\n      ')}`);

    step(6, steps, `opening ${opts.packageName}`);
    if (!guiAvailable(host)) {
      console.error('the VNC tooling is not on the VM host, so the dash cannot be opened from here');
      return 1;
    }
    if (!LIST_ORDER.includes(opts.packageName as (typeof LIST_ORDER)[number])) {
      console.error(`unknown package "${opts.packageName}"; one of ${LIST_ORDER.join(', ')}`);
      return 1;
    }
    const opened = openDashboard(host, { name: opts.packageName });
    if (!opened.ok) {
      console.error(opened.stderr);
      return 1;
    }
    console.log(`      ${opened.stdout}`);

    step(7, steps, 'letting the dash settle, then sizing it to the dashboard it holds');
    sleep(5);
    // Fully on screen, because the capture below asks the window to draw itself and an off-screen
    // edge comes back cut. Sized too, because SimHub reopens a windowed dash at whatever size it
    // last used and letterboxes the dashboard inside it.
    const size = packageSize(opts.packageName);
    placeDashboards(host, 0, 0, size ? { name: opts.packageName, ...size } : undefined);
    sleep(2);

    step(8, steps, 'photographing the dash');
    let captured = captureDashboard(host, opts.packageName, shot);
    if (!captured.ok) {
      console.error(`      the window capture failed (${captured.stderr}); falling back to the whole display`);
      captured = screenshot(host, shot, 1600);
    }
    if (!captured.ok) console.error(`      the screenshot failed too: ${captured.stderr}`);

    console.log('');
    console.log(`  package   ${opts.packageName}, installed and open`);
    console.log(`  telemetry ${opts.scenario}, emulator pid ${runningPid(host) ?? 'unknown'}`);
    console.log(`  screen    ${captured.ok ? shot : '(not captured)'}`);
    console.log(`  logs      bun run vm logs 80        SimHub`);
    console.log(`            bun run emulator tail 20  the telemetry being replayed`);
    console.log('');
    console.log(opts.keep ? '  the emulator is still running and the VM is still claimed' : '  stopping the emulator and releasing the VM');
    return 0;
  } finally {
    if (!opts.keep) {
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
    process.exit(await dev(resolveHost(), opts));
  }
}
