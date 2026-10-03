/**
 * What `bun run modules` does when a step of its setup fails (#624). The run is a sequence of steps
 * on the VM, so each is answered here in place of the guest, through `steps`; what is pinned is what
 * the run makes of the answers. An emulator that did not build, upload or start leaves SimHub idle,
 * and a run that carried on would photograph idle screens and write a run.json naming the scenario.
 */
import { afterEach, beforeEach, describe, expect, spyOn, test } from 'bun:test';
import { existsSync, mkdtempSync, readdirSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { MODULE_CATALOGUE } from '../packages/dash/src/contract.ts';
import { RUN_FILE } from './shotsRun.ts';
import { packageNameFor, parseArgs, run, steps, type Options } from './modules.ts';
import type { Host, RunResult } from './vm.ts';

const host: Host = { local: true, name: 'fake' };
const ok = (stdout = ''): RunResult => ({ ok: true, code: 0, stdout, stderr: '' });
const failed = (stderr: string, stdout = ''): RunResult => ({ ok: false, code: 1, stdout, stderr });

type Steps = typeof steps;
const real: Steps = { ...steps };

/** Every step answering as a guest with the emulator running does, recording which were taken. */
function answering(overrides: Partial<Steps>): string[] {
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
    uploadEmulator: () => ok('uploaded: 12 scenarios'),
    startEmulator: () => ok('running green (pid 1)'),
    stopEmulator: () => ok('stopped cleanly'),
    waitForLaps: () => true,
    lapsCompleted: () => 2,
    closeDashboards: () => [],
    openDashboard: () => ok(),
    placeDashboards: () => ok(),
    captureDashboard: () => ok(),
    sleep: () => {},
  };
  const answers = { ...healthy, ...overrides } as Record<string, (...args: never[]) => unknown>;
  for (const name of Object.keys(healthy) as (keyof Steps)[]) {
    (steps as Record<string, unknown>)[name] = (...args: never[]) => {
      taken.push(name);
      return answers[name]!(...args);
    };
  }
  return taken;
}

let dir: string;
let opts: Options;
let errors: string[];

beforeEach(() => {
  dir = mkdtempSync(path.join(os.tmpdir(), 'opendash-modules-'));
  const parsed = parseArgs(['--modules', 'fuel,tyres', '--scenario', 'green', '--out', path.join(dir, 'out')]);
  if ('help' in parsed) throw new Error('the arguments read as --help');
  opts = { ...parsed, buildDir: path.join(dir, 'build') };
  errors = [];
  spyOn(console, 'log').mockImplementation(() => {});
  spyOn(console, 'error').mockImplementation((...args: unknown[]) => void errors.push(args.join(' ')));
});

afterEach(() => {
  Object.assign(steps, real);
  (console.log as unknown as { mockRestore(): void }).mockRestore();
  (console.error as unknown as { mockRestore(): void }).mockRestore();
  rmSync(dir, { recursive: true, force: true });
});

/** Nothing a capture leaves behind: no picture and no run.json. */
const nothingWritten = (): boolean => !existsSync(opts.outDir) || readdirSync(opts.outDir).length === 0;

describe('a step that fails before anything is photographed', () => {
  test.each([
    ['the emulator does not build', { buildEmulator: () => failed('', 'error CS1002: ; expected') }, 'error CS1002: ; expected', 'buildEmulator'],
    ['the emulator does not reach the guest', { uploadEmulator: () => failed('could not copy to the share: Permission denied') }, 'could not copy to the share: Permission denied', 'uploadEmulator'],
    ['the emulator does not start', { startEmulator: () => failed('unknown scenario "nope"; the repository ships green, race') }, 'unknown scenario "nope"', 'startEmulator'],
  ] as const)('%s: the run ends 1 and says why, before any lap wait or capture', async (_case, overrides, said, last) => {
    const taken = answering(overrides);
    expect(await run(host, opts)).toBe(1);
    expect(errors.join('\n')).toContain(said);

    const setup = ['buildEmulator', 'uploadEmulator', 'startEmulator'];
    expect(taken.filter((s) => setup.includes(s))).toEqual(setup.slice(0, setup.indexOf(last) + 1));
    for (const step of ['waitForLaps', 'openDashboard', 'captureDashboard']) expect(taken).not.toContain(step);
    expect(nothingWritten()).toBe(true);
  });

  test('the VM is still put back: the emulator stopped and the claim given back', async () => {
    const taken = answering({ startEmulator: () => failed('the emulator did not appear within 30s') });
    expect(await run(host, opts)).toBe(1);
    expect(taken).toContain('stopEmulator');
    expect(taken.at(-1)).toBe('release');
  });

  test('the packages it wrote for the install are gone either way', async () => {
    answering({ uploadEmulator: () => failed('no emulator at tools/irsdk-emulator/bin; build it first') });
    await run(host, opts);
    expect(readdirSync(opts.buildDir)).toEqual([]);
  });
});

describe('a run whose setup went through', () => {
  test('photographs every module, writes run.json and ends 0', async () => {
    const opened: string[] = [];
    const taken = answering({ openDashboard: (_h, o) => (opened.push(o.name), ok()) });
    expect(await run(host, opts)).toBe(0);
    const wanted = MODULE_CATALOGUE.filter((m) => m.id === 'fuel' || m.id === 'tyres');
    expect(opened).toEqual(wanted.map((m) => packageNameFor(m.number, m.id)));
    expect(taken.filter((s) => s === 'captureDashboard')).toHaveLength(2);
    expect(readdirSync(opts.outDir)).toEqual([RUN_FILE]);
  });

  test('laps that do not come are said, and the modules are still photographed', async () => {
    answering({ waitForLaps: () => false });
    expect(await run(host, opts)).toBe(0);
    expect(errors.join('\n')).toContain('the laps did not come');
    expect(existsSync(path.join(opts.outDir, RUN_FILE))).toBe(true);
  });
});
