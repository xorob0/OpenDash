/**
 * Ctrl-C on `bun run dev`, `shots`, `record` or `modules` (#625). Bun dies of SIGINT without
 * running a `finally`, so the emulator, the claim and the trace recorder were left on the VM. The
 * package scripts go through `scripts/interruptible.sh`, whose trap runs `scripts/interrupted.ts`.
 *
 * The wrapper is run for real, against stand-in scripts in a temporary directory, and interrupted
 * the way a terminal does it: SIGINT to the whole process group. The putting back runs against the
 * fake host.
 */
import { afterAll, beforeAll, describe, expect, test } from 'bun:test';
import { spawn, spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { withFakeHost, type Answer } from './fakeHost.ts';
import { INTERRUPTIBLE, interrupted } from './interrupted.ts';

const repoRoot = path.resolve(import.meta.dir, '..');

describe('the package scripts', () => {
  const scripts = (JSON.parse(readFileSync(path.join(repoRoot, 'package.json'), 'utf8')) as { scripts: Record<string, string> }).scripts;

  test.each([...INTERRUPTIBLE])('%s goes through the wrapper that cleans up after Ctrl-C', (name) => {
    expect(scripts[name]).toBe(`bash scripts/interruptible.sh ${name}`);
  });
});

// ------------------------------------------------------------------------------- the wrapper

/** A long run for the wrapper to interrupt, blocked in spawnSync as the real scripts are. */
const SLOW = `import { spawnSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';
writeFileSync(process.env.MARKS + '/started', process.argv.slice(2).join(' '));
for (let i = 0; i < 3; i++) spawnSync('sleep', [process.env.NAP!]);
writeFileSync(process.env.MARKS + '/carried-on', '');
process.exit(Number(process.env.CODE ?? 0));
`;
/** Stands in for scripts/interrupted.ts and says what it was asked to put back. */
const CLEANUP = `import { writeFileSync } from 'node:fs';
writeFileSync(process.env.MARKS + '/cleaned', process.argv.slice(2).join(' '));
`;

interface Run {
  code: number | null;
  marks: (name: string) => string | null;
  /** Whether a sleep the stand-in started is still running. */
  orphaned: boolean;
}

/** Runs `bash interruptible.sh slow ...args`, sending its group SIGINT once it has started when `interrupt`. */
async function runWrapper(args: string[], opts: { interrupt: boolean; code?: number }): Promise<Run> {
  const dir = mkdtempSync(path.join(os.tmpdir(), 'opendash-interruptible-'));
  try {
    copyFileSync(path.join(import.meta.dir, 'interruptible.sh'), path.join(dir, 'interruptible.sh'));
    writeFileSync(path.join(dir, 'slow.ts'), SLOW);
    writeFileSync(path.join(dir, 'interrupted.ts'), CLEANUP);
    // A nap no other process on the machine is taking, so that it can be looked for afterwards.
    const nap = `${opts.interrupt ? 30 : 0}.${process.pid}${Date.now() % 1000}`;
    const child = spawn('bash', [path.join(dir, 'interruptible.sh'), 'slow', ...args], {
      detached: true,
      stdio: 'ignore',
      env: { ...process.env, MARKS: dir, NAP: nap, CODE: String(opts.code ?? 0) },
    });
    const exited = new Promise<number | null>((resolve) => child.on('exit', (code) => resolve(code)));
    if (opts.interrupt) {
      const deadline = Date.now() + 15_000;
      while (!existsSync(path.join(dir, 'started')) && Date.now() < deadline) await Bun.sleep(50);
      await Bun.sleep(300);
      process.kill(-child.pid!, 'SIGINT');
    }
    const code = await exited;
    const orphaned = spawnSync('pgrep', ['-f', `sleep ${nap}`]).status === 0;
    spawnSync('pkill', ['-f', `sleep ${nap}`]);
    const marks = (name: string) => (existsSync(path.join(dir, name)) ? readFileSync(path.join(dir, name), 'utf8') : null);
    const read = { started: marks('started'), cleaned: marks('cleaned'), 'carried-on': marks('carried-on') };
    return { code, marks: (name) => read[name as keyof typeof read] ?? null, orphaned };
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

describe('scripts/interruptible.sh', () => {
  test('Ctrl-C stops the script where it is and puts the VM back for it', async () => {
    const r = await runWrapper(['OpenDash', '--no-build'], { interrupt: true });
    expect(r.marks('started')).toBe('OpenDash --no-build');
    expect(r.marks('cleaned')).toBe('slow');
    expect(r.marks('carried-on')).toBeNull();
    expect(r.orphaned).toBe(false);
    expect(r.code).toBe(130);
  }, 30_000);

  test('--keep is honoured on Ctrl-C too: nothing is put back', async () => {
    const r = await runWrapper(['--keep'], { interrupt: true });
    expect(r.marks('started')).toBe('--keep');
    expect(r.marks('cleaned')).toBeNull();
    expect(r.code).toBe(130);
  }, 30_000);

  test('a run that ends on its own is left to its own finally, and its exit status is kept', async () => {
    const r = await runWrapper(['x'], { interrupt: false, code: 3 });
    expect(r.marks('carried-on')).toBe('');
    expect(r.marks('cleaned')).toBeNull();
    expect(r.code).toBe(3);
  }, 30_000);
});

// ------------------------------------------------------------------------------- putting back

const ME = 'tester@host:~/OpenDash';
const isEmulatorQuery = (script: string) => script.startsWith('$p = Get-Process IrsdkEmulator');
const isEmulatorStop = (script: string) => script.includes('Get-Process IrsdkEmulator') && script.includes('Stop-Process');
const isRecorderRemoval = (script: string) => script.includes('Remove-Item') && script.includes('OpenDashTraceRecorder.dll');
const RECORDER_ENTRY = { ClassName: 'OpenDashTraceRecorder.TraceRecorderPlugin', IsEnabled: true, ShowInMainMenu: false, ShowInMainMenuPosition: 0 };
const OPENDASH_ENTRY = { ClassName: 'OpenDash.OpenDashPlugin', IsEnabled: true, ShowInMainMenu: true, ShowInMainMenuPosition: 0 };

/** A guest with the emulator running and the recorder activated, as an interrupted `record` leaves it. */
function interruptedGuest(activationWritten: string[]): (script: string, share: string) => Answer {
  return (script, share) => {
    if (isEmulatorQuery(script)) return { stdout: '4242' };
    if (isEmulatorStop(script)) return { stdout: 'stopped cleanly' };
    if (script.startsWith('$p = Get-Process SimHubWPF')) return { stdout: 'stopped' };
    if (script.includes('Start-ScheduledTask')) return { stdout: 'started (pid 7)' };
    if (script.includes("'copied' } else { 'missing' }")) {
      writeFileSync(path.join(share, 'PluginsActivation.json'), JSON.stringify([OPENDASH_ENTRY, RECORDER_ENTRY]));
      return { stdout: 'copied' };
    }
    if (script.includes('PluginsActivation.json') && script.includes('-Destination')) {
      activationWritten.push(readFileSync(path.join(share, 'PluginsActivation.json'), 'utf8'));
      return { stdout: 'forgot' };
    }
    return { stdout: 'removed' };
  };
}

function lockedBy(share: string, who: string, minutesAgo = 5): void {
  const since = new Date(Date.now() - minutesAgo * 60_000).toISOString();
  writeFileSync(path.join(share, 'vm.lock'), `${JSON.stringify({ who, since, note: 'dev OpenDash' })}\n`);
}

const quietly = <T>(body: () => T): T => {
  const log = console.log;
  console.log = () => {};
  try {
    return body();
  } finally {
    console.log = log;
  }
};

describe('scripts/interrupted.ts', () => {
  // Who this session is, which is what the claim is compared with by the release too.
  const was = process.env.OPENDASH_VM_WHO;
  beforeAll(() => {
    process.env.OPENDASH_VM_WHO = ME;
  });
  afterAll(() => {
    if (was === undefined) delete process.env.OPENDASH_VM_WHO;
    else process.env.OPENDASH_VM_WHO = was;
  });

  test('a run that still holds the VM has its emulator stopped and its claim given back', () => {
    const r = withFakeHost(interruptedGuest([]), (host, calls, share) => {
      lockedBy(share, ME);
      const code = quietly(() => interrupted(host, 'dev'));
      return { code, scripts: calls.flatMap((c) => (c.script ? [c.script] : [])), locked: existsSync(path.join(share, 'vm.lock')) };
    });
    expect(r.code).toBe(0);
    expect(r.scripts.some(isEmulatorStop)).toBe(true);
    expect(r.scripts.some(isRecorderRemoval)).toBe(false);
    expect(r.locked).toBe(false);
  });

  test('record also has the recorder deleted and forgotten, and SimHub is left running', () => {
    const written: string[] = [];
    const r = withFakeHost(interruptedGuest(written), (host, calls, share) => {
      lockedBy(share, ME);
      const code = quietly(() => interrupted(host, 'record'));
      return { code, scripts: calls.flatMap((c) => (c.script ? [c.script] : [])), locked: existsSync(path.join(share, 'vm.lock')) };
    });
    expect(r.code).toBe(0);
    expect(r.scripts.some(isEmulatorStop)).toBe(true);
    expect(r.scripts.some(isRecorderRemoval)).toBe(true);
    expect(written).toHaveLength(1);
    expect((JSON.parse(written[0]!) as { ClassName: string }[]).map((e) => e.ClassName)).toEqual([OPENDASH_ENTRY.ClassName]);
    expect(r.scripts.at(-1)).toContain('Start-ScheduledTask');
    expect(r.locked).toBe(false);
  });

  test('a VM another session has claimed since is left exactly as it is', () => {
    const r = withFakeHost(interruptedGuest([]), (host, calls, share) => {
      lockedBy(share, 'tim@macbook:~/OpenDash');
      const before = readFileSync(path.join(share, 'vm.lock'), 'utf8');
      const code = quietly(() => interrupted(host, 'record'));
      return { code, guest: calls.filter((c) => c.script !== null).length, unchanged: readFileSync(path.join(share, 'vm.lock'), 'utf8') === before };
    });
    expect(r.code).toBe(0);
    expect(r.guest).toBe(0);
    expect(r.unchanged).toBe(true);
  });

  test('a claim of ours that went stale is not somebody else\'s: the emulator is still stopped', () => {
    const r = withFakeHost(interruptedGuest([]), (host, calls, share) => {
      lockedBy(share, ME, 120);
      const code = quietly(() => interrupted(host, 'shots'));
      return { code, scripts: calls.flatMap((c) => (c.script ? [c.script] : [])) };
    });
    expect(r.code).toBe(0);
    expect(r.scripts.some(isEmulatorStop)).toBe(true);
  });

  test('a step that fails is said and fails the cleanup, and the rest still runs', () => {
    const guest = interruptedGuest([]);
    const r = withFakeHost(
      (script, share) => (isEmulatorQuery(script) ? { status: 255, stderr: 'ssh: connect to host 127.0.0.1 port 2222: Connection timed out' } : guest(script, share)),
      (host, _calls, share) => {
        lockedBy(share, ME);
        const code = quietly(() => interrupted(host, 'modules'));
        return { code, locked: existsSync(path.join(share, 'vm.lock')) };
      },
    );
    expect(r.code).toBe(1);
    expect(r.locked).toBe(false);
  });
});
