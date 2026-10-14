/**
 * What `scripts/emulator.ts` decides without a VM: which scenarios it will accept, the refusal
 * that stops a second emulator from being started, and the laps a run has completed, the last two
 * asked of a fake host (`fakeHost.ts`). Starting, stopping and following are remote side effects
 * and are proved by running them.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { filesToUpload, lapsCompleted, scenarios, start, stop, withoutSim } from './emulator.ts';
import { withFakeHost, type Answer } from './fakeHost.ts';
import { ASSETTO_CORSA, IRACING, SIMS, simFromArgs, simNamed } from './sims.ts';

const SCENARIO_DIR = path.resolve(import.meta.dir, '../tools/irsdk-emulator/scenarios');

describe('the scenarios a run may name', () => {
  test('they are the JSON files beside the emulator, without their extension', () => {
    const onDisk = readdirSync(SCENARIO_DIR)
      .filter((f) => f.endsWith('.json'))
      .map((f) => f.replace(/\.json$/, ''))
      .sort();
    expect(scenarios()).toEqual(onDisk);
  });

  test('the default scenario is one of them', () => {
    expect(scenarios()).toContain('race');
  });

  test('each one is a readable JSON document', () => {
    for (const name of scenarios()) {
      const file = path.join(SCENARIO_DIR, `${name}.json`);
      expect(existsSync(file)).toBe(true);
      // The emulator's own reader tolerates // comments, which JSON.parse does not, so they are
      // stripped here rather than the scenarios being written without them.
      const text = Bun.file(file).text();
      expect(text).resolves.toContain('{');
    }
  });

  test('a scenario that extends another names one that exists', async () => {
    for (const name of scenarios()) {
      const text = await Bun.file(path.join(SCENARIO_DIR, `${name}.json`)).text();
      const extend = /"extends"\s*:\s*"([^"]+)"/.exec(text)?.[1];
      if (!extend) continue;
      expect({ scenario: name, extends: extend, exists: existsSync(path.join(SCENARIO_DIR, extend)) }).toMatchObject({ exists: true });
    }
  });
});

describe('the sims', () => {
  test('--sim names one by its id or its SimHub game, and naming none is iRacing', () => {
    expect(simFromArgs([])).toBe(IRACING);
    expect(simFromArgs(['--sim', 'ac'])).toBe(ASSETTO_CORSA);
    expect(simFromArgs(['--sim=AssettoCorsa'])).toBe(ASSETTO_CORSA);
    expect(simNamed('IRACING')).toBe(IRACING);
    expect(() => simFromArgs(['--sim', 'lmu'])).toThrow();
    expect(() => simFromArgs(['--sim'])).toThrow();
  });

  test('the sim is taken off the command line before a scenario is looked for in it', () => {
    expect(withoutSim(['start', '--sim', 'ac', 'green', '--follow'])).toEqual(['start', 'green', '--follow']);
    expect(withoutSim(['start', 'green', '--sim=ac'])).toEqual(['start', 'green']);
  });

  test('each has its own emulator, process, guest folder, tick and traces', () => {
    for (const key of ['sourceDir', 'process', 'guestDir', 'shareDir', 'tickProperty', 'traceSubdir', 'banner', 'game'] as const) {
      const values = SIMS.map((s) => s[key]);
      expect({ key, distinct: new Set(values).size }).toEqual({ key, distinct: SIMS.length });
    }
  });

  test("Assetto Corsa's emulator is acs.exe, the process SimHub's AC reader looks for", () => {
    // ACHelper.acProcessNames in SimHub 9.12.6: acs, acs_x86, acs_pro, acs_dmm.
    expect(ASSETTO_CORSA.exe).toBe('acs.exe');
    expect(['acs', 'acs_x86', 'acs_pro', 'acs_dmm']).toContain(ASSETTO_CORSA.process);
  });

  test("Assetto Corsa's emulator travels with the fake install SimHub reads the car and the track from", () => {
    const files = filesToUpload(ASSETTO_CORSA);
    expect(files).toContain('content/tracks/spa/ui/ui_track.json');
    expect(files).toContain('content/cars/ks_porsche_911_gt3_r_2016/data/drivetrain.ini');
    expect(files.filter((f) => f.startsWith('scenarios/') && f.endsWith('.json')).map((f) => f.slice('scenarios/'.length).replace(/\.json$/, '')).sort()).toEqual(scenarios(ASSETTO_CORSA));
    expect(filesToUpload(IRACING).every((f) => f.startsWith('scenarios/'))).toBe(true);
  });

  test("Assetto Corsa's scenarios mirror iRacing's where AC publishes the state", () => {
    // The three the ticket's acceptance names, and the race they extend.
    for (const name of ['race', 'green', 'yellow', 'pit']) expect(scenarios(ASSETTO_CORSA)).toContain(name);
    // Two have no twin, being states iRacing has no word for: an install without SimHub's app, and a
    // time attack.
    for (const name of scenarios(ASSETTO_CORSA).filter((n) => n !== 'noapp' && n !== 'timeattack')) expect({ name, inIracing: scenarios(IRACING).includes(name) }).toEqual({ name, inIracing: true });
  });

  test("every name on a sim's untraced list is a scenario it ships", () => {
    for (const sim of SIMS) for (const name of sim.untraced) expect({ sim: sim.id, name, ships: scenarios(sim).includes(name) }).toEqual({ sim: sim.id, name, ships: true });
  });

  test('a scenario of each sim that extends another names one beside it', async () => {
    for (const sim of SIMS) {
      const dir = path.resolve(import.meta.dir, '..', sim.sourceDir, 'scenarios');
      for (const name of scenarios(sim)) {
        const text = await Bun.file(path.join(dir, `${name}.json`)).text();
        const extend = /"extends"\s*:\s*"([^"]+)"/.exec(text)?.[1];
        if (extend) expect({ sim: sim.id, scenario: name, extends: extend, exists: existsSync(path.join(dir, extend)) }).toMatchObject({ exists: true });
      }
    }
  });
});

describe('an Assetto Corsa emulator', () => {
  const isAcQuery = (script: string) => script.startsWith('$p = Get-Process acs ');
  const isLaunch = (script: string) => script.includes('New-ScheduledTaskAction');

  test('is asked after, launched and stopped as acs, not as iRacing\'s', () => {
    const { r, scripts } = withFakeHost(
      (script) => (isAcQuery(script) ? { stdout: '' } : { stdout: 'launched' }),
      (host, calls) => ({ r: start(host, { scenario: 'green', sim: ASSETTO_CORSA, waitSeconds: 0 }), scripts: calls.flatMap((c) => (c.script ? [c.script] : [])) }),
    );
    // Nothing answered "running" within no seconds, so the start reports it did not appear.
    expect(r.ok).toBe(false);
    expect(scripts.some(isAcQuery)).toBe(true);
    expect(scripts.some((s) => s.includes('IrsdkEmulator'))).toBe(false);
    const launch = scripts.find(isLaunch) ?? '';
    expect(launch).toContain('C:\\Temp\\acsm-emulator\\acs.exe');
    expect(launch).toContain('green --log C:\\Temp\\acsm-emulator\\emulator.log');
  });

  test('refuses a scenario only iRacing has', () => {
    const r = withFakeHost(() => ({}), (host) => start(host, { scenario: 'flagbox', sim: ASSETTO_CORSA, waitSeconds: 0 }));
    expect(r.ok).toBe(false);
    expect(r.stderr).toContain('unknown Assetto Corsa scenario "flagbox"');
  });

  test('is stopped through its own stop file', () => {
    const scripts = withFakeHost(
      (script) => (isAcQuery(script) ? { stdout: '5151' } : { stdout: 'stopped cleanly' }),
      (host, calls) => {
        stop(host, ASSETTO_CORSA);
        return calls.flatMap((c) => (c.script ? [c.script] : []));
      },
    );
    expect(scripts.some((s) => s.includes('C:\\Temp\\acsm-emulator\\stop.txt') && s.includes('Get-Process acs '))).toBe(true);
  });
});

describe('an emulator that cannot be asked whether it is running', () => {
  const isQuery = (script: string) => script.startsWith('$p = Get-Process IrsdkEmulator');
  const isLaunch = (script: string) => script.includes('New-ScheduledTaskAction');
  /** The guest's SSH failing on the one question, as it does under load, and answering the rest. */
  const hiccup = (script: string): Answer =>
    isQuery(script) ? { status: 255, stderr: 'ssh: connect to host 127.0.0.1 port 2222: Connection timed out' } : { stdout: 'launched' };

  test('is not started a second time over', () => {
    const { r, launched } = withFakeHost(hiccup, (host, calls) => ({
      r: start(host, { scenario: 'race', waitSeconds: 0 }),
      launched: calls.some((c) => c.script !== null && isLaunch(c.script)),
    }));
    expect(launched).toBe(false);
    expect(r.ok).toBe(false);
    expect(r.stderr).toContain('could not tell whether an emulator is running');
  });

  test('is not reported as stopped', () => {
    const r = withFakeHost(hiccup, (host) => stop(host));
    expect(r.ok).toBe(false);
    expect(r.stderr).toContain('could not tell whether an emulator is running');
  });

  test('one that answers is still told apart: none running is stopped, one running is refused', () => {
    expect(withFakeHost((script) => (isQuery(script) ? { stdout: '' } : {}), (host) => stop(host))).toMatchObject({ ok: true, stdout: 'not running' });
    const refused = withFakeHost(
      (script) => (isQuery(script) ? { stdout: '4242' } : { stdout: 'launched' }),
      (host) => start(host, { scenario: 'race', waitSeconds: 0 }),
    );
    expect(refused.ok).toBe(false);
    expect(refused.stderr).toContain('already running (pid 4242)');
  });
});

describe('the laps the running scenario has completed', () => {
  /**
   * The log as the emulator appends it: a banner per run, its status line every second, and a lap
   * line each time the player crosses the line, every `lapSeconds`.
   */
  function emulatorLog(runs: readonly { laps: number }[], lapSeconds = 98): string[] {
    const lines: string[] = [];
    runs.forEach((run, i) => {
      lines.push(`IrsdkEmulator - scenario 'race' (C:\\Temp\\irsdk-emulator\\scenarios\\race.json) run ${i}`);
      for (let second = 1; second <= run.laps * lapSeconds + 30; second++) {
        lines.push(`t=${second}s  speed 212 km/h  gear 5  rpm 9120`);
        if (second % lapSeconds === 0) lines.push(`lap ${second / lapSeconds} completed in ${lapSeconds}.000 s (best ${lapSeconds}.000)`);
      }
    });
    return lines;
  }

  /** A guest holding that log, answering the two ways PowerShell reads one: its tail, and a search. */
  const holding =
    (log: readonly string[]) =>
    (script: string): Answer => {
      if (!script.includes('emulator.log')) return {};
      const pattern = /Select-String[^\n]*-Pattern '([^']+)'/.exec(script)?.[1];
      if (pattern) return { stdout: log.filter((l) => new RegExp(pattern).test(l)).join('\n') };
      const tail = /-Tail (\d+)/.exec(script)?.[1];
      if (tail) return { stdout: log.slice(-Number(tail)).join('\n') };
      return {};
    };

  test('are counted from the banner, however long ago it scrolled by', () => {
    // Twelve laps of 98 s is twenty minutes of status lines, three times the 400 a tail reads.
    const log = emulatorLog([{ laps: 12 }]);
    expect(log.length).toBeGreaterThan(1200);
    expect(withFakeHost(holding(log), (host) => lapsCompleted(host))).toBe(12);
  });

  test('a previous run on the same log does not count', () => {
    expect(withFakeHost(holding(emulatorLog([{ laps: 5 }, { laps: 3 }])), (host) => lapsCompleted(host))).toBe(3);
  });

  test('a run that has completed none has none', () => {
    expect(withFakeHost(holding(emulatorLog([{ laps: 5 }, { laps: 0 }])), (host) => lapsCompleted(host))).toBe(0);
  });

  test("Assetto Corsa's are counted from its own banner, in its own log", () => {
    const log = emulatorLog([{ laps: 4 }]).map((l) => l.replace("IrsdkEmulator - scenario 'race' (C:\\Temp\\irsdk-emulator", "AcsmEmulator - scenario 'race' (C:\\Temp\\acsm-emulator"));
    const guest = (script: string): Answer => (script.includes('acsm-emulator\\emulator.log') ? holding(log)(script) : {});
    expect(withFakeHost(guest, (host) => lapsCompleted(host, ASSETTO_CORSA))).toBe(4);
    // iRacing's log is another file, which this guest does not have.
    expect(withFakeHost(guest, (host) => lapsCompleted(host))).toBe(0);
  });
});
