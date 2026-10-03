/**
 * What `bun run shots` decides before it touches the VM: how its arguments are read, which
 * packages it walks by default, and what it calls the files it writes. The loop itself is a
 * sequence of remote steps, answered here by `steps` in place of the guest, so that what it writes
 * beside the pictures can be read back.
 */
import { afterEach, beforeEach, describe, expect, spyOn, test } from 'bun:test';
import { mkdtempSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { LIST_ORDER } from './dev.ts';
import { FACES, parseArgs, shotName, shots, steps, type ShotsOptions } from './shots.ts';
import { readRun, upgradeRun } from './shotsRun.ts';
import type { Host, RunResult } from './vm.ts';

describe('reading the arguments', () => {
  test('nothing means every face on green', () => {
    expect(parseArgs([])).toMatchObject({ packages: FACES, scenarios: ['green'], noBuild: false, keep: false });
  });

  test('packages and scenarios are comma separated lists', () => {
    expect(parseArgs(['--packages', 'OpenDash,OpenDash 850x480', '--scenarios', 'green,yellow'])).toMatchObject({
      packages: ['OpenDash', 'OpenDash 850x480'],
      scenarios: ['green', 'yellow'],
    });
  });

  test('either may be written with an equals sign', () => {
    expect(parseArgs(['--scenarios=pit'])).toMatchObject({ scenarios: ['pit'] });
  });

  test('blank entries and stray spaces are dropped', () => {
    expect(parseArgs(['--scenarios', ' green , , yellow '])).toMatchObject({ scenarios: ['green', 'yellow'] });
  });

  test('the switches are read', () => {
    expect(parseArgs(['--no-build', '--keep'])).toMatchObject({ noBuild: true, keep: true });
  });

  test('the output directory defaults inside build/, which is already ignored', () => {
    const opts = parseArgs([]);
    expect('help' in opts ? '' : opts.outDir).toMatch(/build\/shots$/);
  });

  test('help wins over everything else', () => {
    expect(parseArgs(['--packages', 'OpenDash', '--help'])).toEqual({ help: true });
  });
});

describe('which packages a bare run walks', () => {
  test('every face, and neither second screen', () => {
    // Ten card faces and the zone faces built beside them. The count moves as the zone work lands
    // and moves back when the card path is retired, so it is checked against the list rather than
    // written down.
    expect(FACES).toEqual(LIST_ORDER.filter((n) => !n.includes('Companion') && !n.includes('Pit wall')));
    expect(FACES.length).toBeGreaterThanOrEqual(10);
    expect(FACES.some((f) => f.includes('Companion') || f.includes('Pit wall'))).toBe(false);
  });

  test('the zone face is among them, under the name it now ships as', () => {
    // Since #169 the zone face is plain "OpenDash"; the card face it replaced says "slots", and
    // both are captured while the two are being compared.
    expect(FACES).toContain('OpenDash');
    expect(FACES).toContain('OpenDash slots 1920x480');
  });

  test('every one of them is a package the opener knows how to click', () => {
    for (const face of FACES) expect(LIST_ORDER).toContain(face);
  });
});

describe('what a capture is called', () => {
  test('numbered, so a listing reads in the order the loop ran', () => {
    expect(shotName(1, 'OpenDash', 'green')).toBe('01-opendash-green.png');
    expect(shotName(12, 'OpenDash', 'green')).toBe('12-opendash-green.png');
  });

  test('the package and the scenario are in the name, so a file needs no caption', () => {
    expect(shotName(4, 'OpenDash 850x480', 'yellow')).toBe('04-opendash-850x480-yellow.png');
  });

  test('a name with spaces and a round size still makes one path segment', () => {
    expect(shotName(9, 'OpenDash 480 round', 'pit')).toBe('09-opendash-480-round-pit.png');
  });

  test('every face produces a distinct file name in one scenario', () => {
    const names = new Set(FACES.map((f, i) => shotName(i + 1, f, 'green')));
    expect(names.size).toBe(FACES.length);
  });
});

describe('what a run writes beside its pictures', () => {
  const host: Host = { local: true, name: 'fake' };
  const ok = (stdout = ''): RunResult => ({ ok: true, code: 0, stdout, stderr: '' });
  const real = { ...steps };
  let dir: string;

  beforeEach(() => {
    dir = mkdtempSync(path.join(os.tmpdir(), 'opendash-shots-'));
    // Every step answering as a guest with the emulator running does.
    Object.assign(steps, {
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
      startEmulator: () => ok('running'),
      stopEmulator: () => ok('stopped cleanly'),
      waitForLaps: () => true,
      lapsCompleted: () => 2,
      closeDashboards: () => [],
      openDashboard: () => ok(),
      placeDashboards: () => ok(),
      captureDashboard: () => ok(),
      sleep: () => {},
      packageSize: (name: string) => (name === 'OpenDash' ? { width: 1920, height: 480 } : { width: 850, height: 480 }),
    } satisfies Partial<typeof steps>);
    spyOn(console, 'log').mockImplementation(() => {});
    spyOn(process.stdout, 'write').mockImplementation(() => true);
  });

  afterEach(() => {
    Object.assign(steps, real);
    (console.log as unknown as { mockRestore(): void }).mockRestore();
    (process.stdout.write as unknown as { mockRestore(): void }).mockRestore();
    rmSync(dir, { recursive: true, force: true });
  });

  const opts = (): ShotsOptions => ({ packages: ['OpenDash', 'OpenDash 850x480'], scenarios: ['green', 'notc'], outDir: dir, noBuild: true, keep: false, warmLaps: 2 });

  test('a run over two scenarios leaves a record for every picture, each naming its own scenario', async () => {
    expect(await shots(host, opts())).toBe(0);
    const run = readRun(dir);
    expect(Object.fromEntries(Object.entries(run?.captures ?? {}).map(([file, c]) => [file, c.scenario]))).toEqual({
      [shotName(1, 'OpenDash', 'green')]: 'green',
      [shotName(2, 'OpenDash 850x480', 'green')]: 'green',
      [shotName(3, 'OpenDash', 'notc')]: 'notc',
      [shotName(4, 'OpenDash 850x480', 'notc')]: 'notc',
    });
  });
});

describe('a run.json written before #627', () => {
  test('reads as the current schema, every capture taking the one scenario the file named', () => {
    const v1 = { schema: 1 as const, scenario: 'gallery', version: '0.3.0-rc.6', commit: '2b876e2', dirty: false, date: '2026-09-22', simHubVersion: '9.12.6', captures: { '01-opendash-gallery.png': { kind: 'package' as const, package: 'OpenDash', width: 1920, height: 480, lapsSeen: 2 } } };
    expect(upgradeRun(v1)).toEqual({ schema: 2, version: '0.3.0-rc.6', commit: '2b876e2', dirty: false, date: '2026-09-22', simHubVersion: '9.12.6', captures: { '01-opendash-gallery.png': { kind: 'package', package: 'OpenDash', width: 1920, height: 480, lapsSeen: 2, scenario: 'gallery' } } });
  });
});
