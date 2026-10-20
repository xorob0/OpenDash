#!/usr/bin/env bun
/**
 * dev: from a clean machine to a dash rendering live telemetry, in one command.
 *
 * Everything here is orchestration. The VM and SimHub are `scripts/vm.ts`, the telemetry is
 * `scripts/emulator.ts`, and the clicking is `scripts/gui.ts`; this file decides the order, holds
 * the claim on the VM while it works, and says at the end what it did.
 *
 * It plays one sim, iRacing unless `--sim ac` asks for Assetto Corsa (`scripts/sims.ts`). The sim
 * decides three things and nothing else: which emulator runs, which of its scenarios may be named,
 * and which game SimHub is told to open on when the install restarts it, since SimHub reads one
 * game at a time and the emulators are not processes it would switch to by itself.
 *
 * The order matters in two places. SimHub reads its fonts once at startup, so a package that brings
 * a face it does not have has to be installed before SimHub is started rather than after (its
 * dashboard list it watches, #664). And the emulator has to be
 * running before the dash is opened, because a dash opened against a disconnected SimHub draws its
 * defaults and looks broken.
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { build as buildEmulator, runningPid, start as startEmulator, stop as stopEmulator, stopAll as stopEmulators, upload as uploadEmulator, scenarios } from './emulator.ts';
import { IRACING, SIMS, simFromArgs, type Sim } from './sims.ts';
import { captureDashboard, guiProblem, openDashboard, placeDashboards } from './gui.ts';
import { BASE_FACE } from '../packages/dash/src/zones/index.ts';
import { claim, claimLost, install, readClaim, release, resolveHost, screenshot, sleep, status, up, waitReady, whoAmI, type Host } from './vm.ts';

const repoRoot = path.resolve(import.meta.dir, '..');

/**
 * The faces, in the order Dash Studio lists them when filtered by "OpenDash".
 *
 * `openDashboard` clicks a row by its index in that list, so the order is not cosmetic. `shots.ts`
 * reads the same list rather than keeping a second copy.
 *
 * Since #169 the zone faces carry the shipped names and the card faces are the ones saying
 * "slots". That makes the bare `OpenDash` a prefix of every other entry, so filtering the list by it
 * matches all of them and the opener takes the first row; it is first here because it is first
 * there, and that is checked on the VM rather than assumed, since this order is read off Dash
 * Studio and is not a sort this repository performs.
 */
export const LIST_ORDER = [
  'OpenDash',
  'OpenDash 1280x400',
  'OpenDash 1280x480',
  'OpenDash 1280x720',
  'OpenDash 480 round',
  'OpenDash 600x686',
  'OpenDash 800 round',
  'OpenDash 800x286',
  'OpenDash 800x480',
  'OpenDash 850x480',
  // The card faces, built beside the zone faces for comparison until #146 deletes them.
  'OpenDash slots 1280x400',
  'OpenDash slots 1280x480',
  'OpenDash slots 1280x720',
  'OpenDash slots 1920x480',
  'OpenDash slots 600x686',
  'OpenDash slots 800x286',
  'OpenDash slots 800x480',
  'OpenDash slots 850x480',
  'OpenDash Companion',
  'OpenDash Companion portrait',
  'OpenDash Pit wall',
  'OpenDash Pit wall portrait',
  // The themed packages, built by `--all-themes` and installed on the VM like any other (#205, #713, #204),
  // in the order Dash Studio sorts the house's own sizes.
  'OpenDash Porsche 1280x400',
  'OpenDash Porsche 1280x480',
  'OpenDash Porsche 1280x720',
  'OpenDash Porsche 1920x480',
  'OpenDash Porsche 600x686',
  'OpenDash Porsche 800x286',
  'OpenDash Porsche 800x480',
  'OpenDash Porsche 850x480',
  'OpenDash AiM 1280x400',
  'OpenDash AiM 1280x480',
  'OpenDash AiM 1280x720',
  'OpenDash AiM 1920x480',
  'OpenDash AiM 600x686',
  'OpenDash AiM 800x286',
  'OpenDash AiM 800x480',
  'OpenDash AiM 850x480',
  'OpenDash BMW 1280x400',
  'OpenDash BMW 1280x480',
  'OpenDash BMW 1280x720',
  'OpenDash BMW 1920x480',
  'OpenDash BMW 600x686',
  'OpenDash BMW 800x286',
  'OpenDash BMW 800x480',
  'OpenDash BMW 850x480',
] as const;

export interface DevOptions {
  /** Which package to install and open. */
  packageName: string;
  /** Which sim's emulator plays, and which game SimHub opens on. */
  sim: Sim;
  scenario: string;
  /** Skip the dashboard build, for when only the scenario changed. */
  noBuild: boolean;
  /** Leave the emulator running and the claim held; for handing the VM to a longer session. */
  keep: boolean;
}

/** The flags that take a value, whose value is therefore not a package name. */
const VALUED_FLAGS = ['--scenario', '--sim'];

/** Reads the command line. An unknown `--sim` throws, since playing iRacing in its place would look like a pass. */
export function parseArgs(argv: readonly string[]): DevOptions | { help: true } {
  if (argv.includes('--help') || argv.includes('-h')) return { help: true };
  // A flag's value is positional-looking, so it is taken out by where it stands rather than by what
  // it says: `--scenario race` once made a package named race, and `--sim ac` would make one named ac.
  const positional = argv.filter((a, i) => !a.startsWith('--') && !VALUED_FLAGS.includes(argv[i - 1] ?? ''));
  const flagValue = (name: string): string | undefined => {
    const index = argv.indexOf(`--${name}`);
    if (index >= 0 && argv[index + 1] && !argv[index + 1]!.startsWith('--')) return argv[index + 1];
    return argv.find((a) => a.startsWith(`--${name}=`))?.split('=').slice(1).join('=');
  };
  const sim = simFromArgs(argv);
  const scenario = flagValue('scenario') ?? sim.defaultScenario;
  return {
    packageName: positional[0] ?? BASE_FACE.folder,
    sim,
    scenario,
    noBuild: argv.includes('--no-build'),
    keep: argv.includes('--keep'),
  };
}

const USAGE = `dev: bring the whole rig up and leave a dash rendering live telemetry.

  bun run dev [package] [--sim ac] [--scenario <name>] [--no-build] [--keep]

  package     which one to install and open; default "${BASE_FACE.folder}", the base size
              (docs/scope.md: 850 x 480 is the base, 1280 x 480 the large one)
              ${LIST_ORDER.join(', ')}
  --sim       which sim to play: iracing, the default, or ac for Assetto Corsa. SimHub is
              restarted on that game, and the sim's own emulator plays
  --scenario  which telemetry to replay; default "race"
${SIMS.map((s) => `              ${s.id === IRACING.id ? 'iRacing' : `with --sim ${s.id}`}: ${scenarios(s).join(', ') || '(none)'}`).join('\n')}
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

/**
 * The remote steps a run takes, in one object so that a test can answer them in place of the guest,
 * as `steps` in shots.ts and modules.ts do.
 */
export const steps = {
  readClaim,
  claim,
  release,
  status,
  up,
  waitReady,
  guiProblem,
  install,
  buildEmulator,
  uploadEmulator,
  startEmulator,
  stopEmulator,
  stopEmulators,
  runningPid,
  openDashboard,
  placeDashboards,
  captureDashboard,
  screenshot,
  sleep,
  /** Read from build/manifest.json, which a test has no reason to have built. */
  packageSize,
};

function step(n: number, of: number, what: string): void {
  console.log(`[${n}/${of}] ${what}`);
}

export async function dev(host: Host, opts: DevOptions): Promise<number> {
  const { readClaim, claim, release, status, up, waitReady, guiProblem, install } = steps;
  const { buildEmulator, uploadEmulator, startEmulator, stopEmulator, stopEmulators, runningPid } = steps;
  const { openDashboard, placeDashboards, captureDashboard, screenshot, sleep, packageSize } = steps;
  const count = 8;
  const shot = path.join(repoRoot, 'build/dev.png');
  // Asked before the VM is claimed: a scenario the sim does not have is a typo, and costs nothing to refuse.
  if (!scenarios(opts.sim).includes(opts.scenario)) {
    console.error(`${opts.sim.label} has no scenario "${opts.scenario}"; one of ${scenarios(opts.sim).join(', ')}`);
    return 1;
  }

  step(1, count, 'claiming the VM');
  const held = readClaim(host);
  if (held && held.who !== whoAmI()) {
    console.error(`the VM is claimed by ${held.who} since ${held.since}${held.note ? ` (${held.note})` : ''}`);
    console.error('There is one VM. Wait, or ask them to run `bun run vm release`.');
    return 1;
  }
  const mine = new Date();
  const claimed = claim(host, `dev ${opts.packageName}${opts.sim === IRACING ? '' : ` --sim ${opts.sim.id}`}`, mine);
  if (!claimed.ok) {
    console.error(claimed.stderr);
    return 1;
  }

  try {
    step(2, count, 'starting the VM if it is down, and reading the display it will be clicked on');
    if (!status(host).stdout.includes('guest-ssh: up')) {
      up(host);
      if (!waitReady(host, 300)) {
        console.error('the VM did not answer within five minutes; try `bun run vm status` and `bun run vm shot`');
        return 1;
      }
    }
    // Asked here rather than at step 6, where the click is: the guest is up, which is all this needs,
    // and the four minutes between the two are the build, the install and SimHub's restart. Refusing
    // after them is refusing after spending them.
    const cannotClick = guiProblem(host);
    if (cannotClick) {
      console.error(cannotClick);
      return 1;
    }
    if (!LIST_ORDER.includes(opts.packageName as (typeof LIST_ORDER)[number])) {
      console.error(`unknown package "${opts.packageName}"; one of ${LIST_ORDER.join(', ')}`);
      return 1;
    }

    step(3, count, opts.noBuild ? 'skipping the build' : 'building the packages');
    if (!opts.noBuild) {
      const built = Bun.spawnSync(['bun', 'run', 'build', '--all-themes'], { cwd: repoRoot, stdout: 'pipe', stderr: 'pipe' });
      if (built.exitCode !== 0) {
        console.error(new TextDecoder().decode(built.stderr) || new TextDecoder().decode(built.stdout));
        return 1;
      }
    }

    step(4, count, `stopping every emulator, installing ${opts.packageName} and restarting SimHub on ${opts.sim.label}`);
    // Every sim's, not only this one's: SimHub with a licence switches to whichever game it finds
    // running, so an emulator left by a run on the other sim could take this one's SimHub onto that
    // game as it starts (#1138). Its own is stopped too, since SimHub is about to restart under it.
    const stopped = stopEmulators(host);
    if (!stopped.ok) {
      console.error(stopped.stderr);
      return 1;
    }
    console.log(`      ${stopped.stdout}`);
    const installed = install(host, [opts.packageName], undefined, opts.sim.game);
    if (!installed.ok) {
      console.error(installed.stderr || installed.stdout);
      return 1;
    }
    console.log(`      ${installed.stdout.split('\n').join('\n      ')}`);

    step(5, count, `starting the ${opts.sim.label} emulator on "${opts.scenario}"`);
    const emulatorBuilt = buildEmulator(opts.sim);
    if (!emulatorBuilt.ok) {
      console.error(emulatorBuilt.stderr || emulatorBuilt.stdout);
      return 1;
    }
    const uploaded = uploadEmulator(host, opts.sim);
    if (!uploaded.ok) {
      console.error(uploaded.stderr);
      return 1;
    }
    const running = startEmulator(host, { scenario: opts.scenario, sim: opts.sim, replace: true });
    if (!running.ok) {
      console.error(running.stderr);
      return 1;
    }
    console.log(`      ${running.stdout.split('\n').join('\n      ')}`);

    step(6, count, `opening ${opts.packageName}`);
    const opened = openDashboard(host, { name: opts.packageName });
    if (!opened.ok) {
      console.error(opened.stderr);
      // The clicking is driven over VNC, so a session that takes the claim mid-run lands its own
      // clicks in this one's sequence. The message above then sends the reader after coordinates
      // that were never the problem, which is why the lock is read again before giving up.
      const lost = claimLost(readClaim(host), mine.toISOString());
      if (lost) console.error(`${lost}; that, rather than the coordinates, is what to look at.`);
      return 1;
    }
    console.log(`      ${opened.stdout}`);

    step(7, count, 'letting the dash settle, then sizing it to the dashboard it holds');
    sleep(5);
    // Fully on screen, because the capture below asks the window to draw itself and an off-screen
    // edge comes back cut. Sized too, because SimHub reopens a windowed dash at whatever size it
    // last used and letterboxes the dashboard inside it.
    const size = packageSize(opts.packageName);
    placeDashboards(host, 0, 0, size ? { name: opts.packageName, ...size } : undefined);
    sleep(2);

    step(8, count, 'photographing the dash');
    let captured = captureDashboard(host, opts.packageName, shot);
    if (!captured.ok) {
      console.error(`      the window capture failed (${captured.stderr}); falling back to the whole display`);
      captured = screenshot(host, shot, 1600);
    }
    if (!captured.ok) console.error(`      the screenshot failed too: ${captured.stderr}`);

    console.log('');
    console.log(`  package   ${opts.packageName}, installed and open`);
    console.log(`  telemetry ${opts.sim.label}, ${opts.scenario}, emulator pid ${runningPid(host, opts.sim).pid ?? 'unknown'}`);
    console.log(`  screen    ${captured.ok ? shot : '(not captured)'}`);
    console.log(`  logs      bun run vm logs 80        SimHub`);
    console.log(`            bun run emulator tail 20${opts.sim === IRACING ? '' : ` --sim ${opts.sim.id}`}  the telemetry being replayed`);
    console.log('');
    console.log(opts.keep ? '  the emulator is still running and the VM is still claimed' : '  stopping the emulator and releasing the VM');
    return 0;
  } finally {
    if (!opts.keep) {
      stopEmulator(host, opts.sim);
      release(host);
    }
  }
}

if (import.meta.main) {
  let opts: DevOptions | { help: true };
  try {
    opts = parseArgs(process.argv.slice(2));
  } catch (e) {
    console.error(e instanceof Error ? e.message : String(e));
    process.exit(2);
  }
  if ('help' in opts) {
    console.log(USAGE);
  } else {
    process.exit(await dev(resolveHost(), opts));
  }
}
