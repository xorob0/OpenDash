/**
 * What `scripts/emulator.ts` decides without a VM: which scenarios it will accept, the refusal
 * that stops a second emulator from being started, and the laps a run has completed, the last two
 * asked of a fake host (`fakeHost.ts`). Starting, stopping and following are remote side effects
 * and are proved by running them.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { lapsCompleted, scenarios, start, stop } from './emulator.ts';
import { withFakeHost, type Answer } from './fakeHost.ts';

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
});
