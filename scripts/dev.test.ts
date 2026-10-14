/**
 * What `bun run dev` decides before it touches the VM: how its arguments are read, and that the
 * package names it offers are the ones the build actually produces. The rest is a sequence of
 * remote steps, answered here through `steps` in place of the guest where the order of them is the
 * point, and otherwise proved by running it.
 */
import { afterEach, beforeEach, describe, expect, spyOn, test } from 'bun:test';
import { BASE_FACE, LARGE_FACE } from '../packages/dash/src/zones/index.ts';
import { built, readBuiltManifest, withBuild } from './buildOutput.ts';
import { dev, LIST_ORDER, packageSize, parseArgs, steps, type DevOptions } from './dev.ts';
import { ASSETTO_CORSA, IRACING } from './sims.ts';
import type { Host, RunResult } from './vm.ts';

const manifest = readBuiltManifest<{ version: string; packages: { folder: string; width: number; height: number }[] }>();

describe('reading the arguments', () => {
  test('nothing means the base face and a race', () => {
    expect(parseArgs([])).toMatchObject({ packageName: 'OpenDash 850x480', scenario: 'race', noBuild: false, keep: false });
  });

  test('a package name is positional', () => {
    expect(parseArgs(['OpenDash Pit wall'])).toMatchObject({ packageName: 'OpenDash Pit wall', scenario: 'race' });
  });

  test('a scenario is a flag, and does not become the package', () => {
    expect(parseArgs(['--scenario', 'notc'])).toMatchObject({ packageName: 'OpenDash 850x480', scenario: 'notc' });
  });

  test('the two together', () => {
    expect(parseArgs(['OpenDash 850x480', '--scenario', 'notc'])).toMatchObject({ packageName: 'OpenDash 850x480', scenario: 'notc' });
  });

  test('a scenario may be written with an equals sign', () => {
    expect(parseArgs(['--scenario=notc'])).toMatchObject({ scenario: 'notc' });
  });

  test('the switches are read', () => {
    expect(parseArgs(['--no-build', '--keep'])).toMatchObject({ noBuild: true, keep: true });
  });

  test('nothing names iRacing, as every run did before there was a second sim', () => {
    expect(parseArgs([])).toMatchObject({ sim: IRACING });
  });

  test('--sim ac plays Assetto Corsa, and its value is not taken for a package', () => {
    // A flag's value used to be dropped only when it was the scenario, so `--sim ac` would have
    // opened a package called "ac".
    expect(parseArgs(['--sim', 'ac'])).toMatchObject({ sim: ASSETTO_CORSA, packageName: 'OpenDash 850x480', scenario: 'race' });
    expect(parseArgs(['OpenDash Pit wall', '--sim', 'ac', '--scenario', 'pit'])).toMatchObject({ sim: ASSETTO_CORSA, packageName: 'OpenDash Pit wall', scenario: 'pit' });
    expect(parseArgs(['--sim=ac', '--scenario=yellow'])).toMatchObject({ sim: ASSETTO_CORSA, scenario: 'yellow' });
  });

  test('a scenario named like its package is still the scenario', () => {
    expect(parseArgs(['--scenario', 'green', 'OpenDash'])).toMatchObject({ packageName: 'OpenDash', scenario: 'green' });
  });

  test('a sim with no emulator is refused rather than played as iRacing', () => {
    expect(() => parseArgs(['--sim', 'acc'])).toThrow(/iracing, ac/);
  });

  test('help wins over everything else', () => {
    expect(parseArgs(['OpenDash', '--help'])).toEqual({ help: true });
  });
});

describe('the base face and the large one', () => {
  // The default has to be a package Dash Studio lists, or `bun run dev` with no arguments filters
  // the list by a name nothing matches and reports that it could not open an installed dashboard.
  test('both are packages this script can open', () => {
    const listed: readonly string[] = LIST_ORDER;
    expect(listed).toContain(BASE_FACE.folder);
    expect(listed).toContain(LARGE_FACE.folder);
  });

  test('the default with no arguments is the base face', () => {
    expect(parseArgs([])).toMatchObject({ packageName: BASE_FACE.folder });
  });
});

describe('the size a dash window is given', () => {
  // Skipped, not passed, when this tree has not built; on CI, which builds first, run either way (#909).
  test.if(withBuild(manifest !== null))('it comes from the manifest the build writes', () => {
    for (const pkg of built(manifest).packages) {
      expect({ folder: pkg.folder, size: packageSize(pkg.folder) }).toMatchObject({
        size: { width: pkg.width, height: pkg.height },
      });
    }
  });

  test('a package that does not exist has no size', () => {
    expect(packageSize('OpenDash 1x1')).toBeUndefined();
  });
});

describe('the emulators a run finds on the VM', () => {
  const host: Host = { local: true, name: 'fake' };
  const ok = (stdout = ''): RunResult => ({ ok: true, code: 0, stdout, stderr: '' });
  type Steps = typeof steps;
  const real: Steps = { ...steps };

  /** Every step answering as a healthy guest does, recording which were taken and in what order. */
  function answering(): string[] {
    const taken: string[] = [];
    const healthy: Steps = {
      readClaim: () => null,
      claim: () => ok('claimed'),
      release: () => ok('released'),
      status: () => ok('guest-ssh: up'),
      up: () => ok(),
      waitReady: () => true,
      guiProblem: () => null,
      install: () => ok('installed'),
      buildEmulator: () => ok(),
      uploadEmulator: () => ok('uploaded'),
      startEmulator: () => ok('running race (pid 1)'),
      stopEmulator: () => ok('stopped cleanly'),
      stopEmulators: () => ok('iRacing not running, Assetto Corsa stopped cleanly'),
      runningPid: () => ({ ...ok('1'), pid: 1 }),
      openDashboard: () => ok('opened'),
      placeDashboards: () => ok(),
      captureDashboard: () => ok(),
      screenshot: () => ok(),
      sleep: () => {},
      packageSize: () => ({ width: 850, height: 480 }),
    };
    for (const name of Object.keys(healthy) as (keyof Steps)[]) {
      (steps as Record<string, unknown>)[name] = (...args: never[]) => {
        taken.push(name);
        return (healthy[name] as (...a: never[]) => unknown)(...args);
      };
    }
    return taken;
  }

  beforeEach(() => {
    spyOn(console, 'log').mockImplementation(() => {});
    spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    Object.assign(steps, real);
    (console.log as unknown as { mockRestore(): void }).mockRestore();
    (console.error as unknown as { mockRestore(): void }).mockRestore();
  });

  test.each([
    ['iRacing', IRACING],
    ['Assetto Corsa', ASSETTO_CORSA],
  ] as const)('a run on %s stops every sim\'s before SimHub restarts', async (_, sim) => {
    // A licensed SimHub switches to whichever game it finds running, so an acs.exe left by an AC run
    // could take an iRacing run's SimHub onto AC as it starts, and the other way round (#1138).
    const taken = answering();
    const opts: DevOptions = { packageName: 'OpenDash 850x480', sim, scenario: 'race', noBuild: true, keep: false };
    expect(await dev(host, opts)).toBe(0);
    expect(taken).toContain('stopEmulators');
    expect(taken.indexOf('stopEmulators')).toBeLessThan(taken.indexOf('install'));
    expect(taken.indexOf('install')).toBeLessThan(taken.indexOf('startEmulator'));
  });
});
