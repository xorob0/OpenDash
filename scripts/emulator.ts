#!/usr/bin/env bun
/**
 * emulator: runs the synthetic iRacing telemetry feed on the test VM, in the one way that works.
 *
 * Two mistakes are easy to make with `IrsdkEmulator.exe` and both fail silently, which is why
 * this exists rather than a paragraph in a README.
 *
 * The first is the logon session. The emulator creates `Local\IRSDKMemMapFileName` and
 * `Local\IRSDKDataValidEvent`, and `Local\` names are per logon session, so an emulator started
 * over SSH lands in session 0 where SimHub, which runs on the desktop, cannot see it. Nothing
 * reports an error: SimHub simply never connects and the dash shows its defaults. Every start
 * therefore goes through the interactive desktop.
 *
 * The second is a second copy. Two processes writing the same shared memory make SimHub drop the
 * connection, and the symptom is a disconnection with no cause in any log. Starting refuses while
 * one is running unless it is asked to replace it.
 *
 * Stopping uses the stop file rather than killing the process, because the emulator sets the
 * header status to 0 on the way out and SimHub then sees a clean disconnect instead of waiting
 * out its four second timeout.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { inDesktop, onHost, powershell, psq, resolveHost, shq, sleep, toShare, type Host, type RunResult } from './vm.ts';

const repoRoot = path.resolve(import.meta.dir, '..');
const SOURCE_DIR = path.join(repoRoot, 'tools/irsdk-emulator');
const BUILD_DIR = path.join(SOURCE_DIR, 'bin/Release/net48');
/** Where the emulator lives inside Windows. Local disk, not the share: it is read at 60 Hz. */
const GUEST_DIR = 'C:\\Temp\\irsdk-emulator';
const GUEST_EXE = `${GUEST_DIR}\\IrsdkEmulator.exe`;
const GUEST_LOG = `${GUEST_DIR}\\emulator.log`;
const GUEST_STOP = `${GUEST_DIR}\\stop.txt`;
/** The share as an SSH session sees it; `Z:` exists only in the interactive one. */
const SHARE_UNC = '\\\\host.lan\\Data';

/** The scenarios shipped beside the emulator, which is what `--scenario` may name. */
/**
 * Scenarios with no committed trace: the ones that will never have one, and the ones that do not
 * have one yet.
 *
 * A trace exists so that every binding of every package can be replayed without SimHub. The flag
 * box is not a package — it is a `.ledsprofile` the user imports, and no `.djson` binding reads
 * anything it sets — so a trace of `flagbox` would carry properties nothing replays, and would
 * still have to be recorded from a real SimHub. Listed by name rather than skipped by a rule, so
 * that adding a scenario without a trace stays a decision somebody made.
 *
 * The second group is the honest form of a decision somebody made: recording a trace claims the VM,
 * and a scenario can be added by a branch that cannot. `bun run record <scenario>` is what moves a
 * name out of this list, and until then nothing replays that scenario.
 */
export const UNTRACED_SCENARIOS: readonly string[] = [
  // flagbox drives lights, not a dashboard.
  'flagbox',
  // alerts walks the alert catalogue over 220 seconds, one condition every seven, and is watched on
  // the VM rather than replayed: a trace holds twenty seconds, which is three of its states (#109).
  'alerts',
  // gallery exists to be photographed for the website, and clip to be filmed; nothing replays them.
  'gallery',
  'clip',
  // Added for the #406 and #387 VM pass by a branch that could not claim the VM: the game running
  // with no session named, and the lap-counted race that publishes no clock. Both are worth
  // recording — a trace of either would let a state nothing else reaches be replayed headlessly.
  'nosession',
  'untimed',
  // cars changes the player's car for SimHub's per-car playlists (#199), whose switch is a device's and is
  // watched on the VM; a twenty-second trace would hold no car change at all.
  'cars',
];

/** Every scenario a run may name. */
export function scenarios(): string[] {
  const dir = path.join(SOURCE_DIR, 'scenarios');
  if (!existsSync(dir)) return [];
  return readdirSync(dir)
    .filter((f) => f.endsWith('.json'))
    .map((f) => f.replace(/\.json$/, ''))
    .sort();
}

/** The scenarios a committed trace is expected for. */
export function tracedScenarioNames(): string[] {
  return scenarios().filter((name) => !UNTRACED_SCENARIOS.includes(name));
}

/** Whether the emulator is running: its process id, or null when none is. */
export type PidResult = RunResult & { pid: number | null };

/**
 * The emulator's process id on the VM, null when none is running, or a failure when the guest could
 * not be asked. The failure is kept apart from "none" because a caller that reads "none" goes on to
 * start one, and starting one beside another is the two-writer fault this file exists to prevent: an
 * SSH call that failed is not grounds for that.
 */
export function runningPid(host: Host): PidResult {
  const r = powershell(host, `$p = Get-Process IrsdkEmulator -ErrorAction SilentlyContinue; if ($p) { $p.Id } else { '' }`, 60);
  const answer = r.stdout.trim();
  if (r.ok && answer === '') return { ...r, pid: null };
  if (r.ok && /^\d+(\s+\d+)*$/.test(answer)) return { ...r, pid: Number.parseInt(answer, 10) };
  return {
    ok: false,
    code: r.code === 0 ? 1 : r.code,
    stdout: '',
    stderr: `could not tell whether an emulator is running: ${r.stderr.trim() || answer || `the guest answered nothing (exit ${r.code})`}`,
    pid: null,
  };
}

/** Builds the emulator unless the exe is already there. It cross-builds from any platform. */
export function build(): RunResult {
  const r = spawnSync('dotnet', ['build', 'tools/irsdk-emulator', '-c', 'Release', '--nologo', '-v', 'q'], {
    cwd: repoRoot,
    encoding: 'utf8',
    timeout: 600_000,
  });
  return { ok: r.status === 0, code: r.status ?? -1, stdout: (r.stdout ?? '').trim(), stderr: (r.stderr ?? '').trim() };
}

/**
 * Puts the exe and the scenarios on the VM together. They travel as a pair because the emulator
 * resolves a scenario name against its own directory, so an exe updated without its scenarios
 * fails on a name that plainly exists in the repository.
 */
export function upload(host: Host): RunResult {
  const exe = path.join(BUILD_DIR, 'IrsdkEmulator.exe');
  if (!existsSync(exe)) return { ok: false, code: 1, stdout: '', stderr: `no emulator at ${exe}; build it first` };

  onHost(host, `mkdir -p ${shq('/opt/winvm/shared/irsdk-emulator/scenarios')}`);
  const sent = toShare(host, exe, 'irsdk-emulator/IrsdkEmulator.exe');
  if (!sent.ok) return sent;
  for (const file of readdirSync(path.join(SOURCE_DIR, 'scenarios'))) {
    const one = toShare(host, path.join(SOURCE_DIR, 'scenarios', file), `irsdk-emulator/scenarios/${file}`);
    if (!one.ok) return one;
  }

  return powershell(
    host,
    `$dir = ${psq(GUEST_DIR)}
New-Item -ItemType Directory -Force -Path (Join-Path $dir 'scenarios') | Out-Null
Copy-Item ${psq(`${SHARE_UNC}\\irsdk-emulator\\IrsdkEmulator.exe`)} $dir -Force
Copy-Item ${psq(`${SHARE_UNC}\\irsdk-emulator\\scenarios\\*`)} (Join-Path $dir 'scenarios') -Force
"uploaded: $((Get-ChildItem (Join-Path $dir 'scenarios')).Count) scenarios"`,
    180,
  );
}

/**
 * Stops the emulator through its stop file, so it clears the header status on the way out and
 * SimHub registers a disconnect rather than timing out. Falls back to killing it only when the
 * process ignores the file, which means it was started without one.
 */
export function stop(host: Host, timeoutSeconds = 20): RunResult {
  const running = runningPid(host);
  if (!running.ok) return running;
  if (running.pid === null) return { ok: true, code: 0, stdout: 'not running', stderr: '' };
  return powershell(
    host,
    `New-Item -ItemType File -Force -Path ${psq(GUEST_STOP)} | Out-Null
$deadline = (Get-Date).AddSeconds(${Math.trunc(timeoutSeconds)})
while ((Get-Date) -lt $deadline) {
  if (-not (Get-Process IrsdkEmulator -ErrorAction SilentlyContinue)) {
    Remove-Item ${psq(GUEST_STOP)} -Force -ErrorAction SilentlyContinue
    'stopped cleanly'; exit 0
  }
  Start-Sleep -Milliseconds 500
}
Get-Process IrsdkEmulator -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item ${psq(GUEST_STOP)} -Force -ErrorAction SilentlyContinue
'killed after ${Math.trunc(timeoutSeconds)}s; SimHub will time out rather than see a disconnect'`,
    timeoutSeconds + 60,
  );
}

export interface StartOptions {
  scenario: string;
  /** Stop an emulator that is already running instead of refusing. */
  replace?: boolean;
  /** How long to wait for the process and its first log line. */
  waitSeconds?: number;
}

/** Starts the emulator in the interactive desktop on the named scenario. */
export function start(host: Host, opts: StartOptions): RunResult {
  const known = scenarios();
  if (known.length > 0 && !known.includes(opts.scenario)) {
    return { ok: false, code: 1, stdout: '', stderr: `unknown scenario "${opts.scenario}"; the repository ships ${known.join(', ')}` };
  }

  const running = runningPid(host);
  if (!running.ok) {
    return { ...running, stderr: `${running.stderr}\nNot starting one beside it blind: two writers to the shared memory make SimHub drop the connection. Try again.` };
  }
  if (running.pid !== null) {
    if (!opts.replace) {
      return {
        ok: false,
        code: 1,
        stdout: '',
        stderr: `an emulator is already running (pid ${running.pid}). Two writers to the shared memory make SimHub drop the connection.\nStop it with \`bun run emulator stop\`, or start with --replace.`,
      };
    }
    const stopped = stop(host);
    if (!stopped.ok) return stopped;
  }

  // A previous run may have left the stop file behind, which would end this one immediately.
  powershell(host, `Remove-Item ${psq(GUEST_STOP)} -Force -ErrorAction SilentlyContinue`, 60);

  const launched = inDesktop(host, GUEST_EXE, `${opts.scenario} --log ${GUEST_LOG} --stop-file ${GUEST_STOP}`, GUEST_DIR);
  if (!launched.ok) return launched;

  const waitSeconds = opts.waitSeconds ?? 30;
  const deadline = Date.now() + waitSeconds * 1000;
  while (Date.now() < deadline) {
    const now = runningPid(host);
    if (now.ok && now.pid !== null) {
      sleep(3);
      const line = tail(host, 1);
      return { ok: true, code: 0, stdout: `running ${opts.scenario} (pid ${now.pid})\n${line.stdout}`, stderr: '' };
    }
    sleep(2);
  }
  return {
    ok: false,
    code: 1,
    stdout: '',
    stderr: `the emulator did not appear within ${waitSeconds}s. It is launched into the interactive desktop through a scheduled task, so check that somebody is logged in: \`bun run vm shot\`.`,
  };
}

/** The last lines of the emulator's log, which is its per-second status line. */
export function tail(host: Host, lines: number): RunResult {
  return powershell(
    host,
    `if (-not (Test-Path ${psq(GUEST_LOG)})) { 'no emulator log yet'; exit 0 }
Get-Content ${psq(GUEST_LOG)} -Tail ${Math.max(1, Math.trunc(lines))}`,
    90,
  );
}

/** The lines of the log that count laps: a run's banner, and a completed lap. */
const LAP_LINES = 'IrsdkEmulator - scenario|lap \\d+ completed in';

/**
 * The laps the running scenario has completed since it started, read from the log.
 *
 * The log is appended across runs, so the count is of `lap N completed` lines after the most recent
 * scenario banner. What a capture needs is not a settled connection but a settled history: SimHub's
 * last-lap columns, fuel averages, stint counters and recorded track map all fill on a completed
 * lap it has observed, and a photograph taken before the first one carries blanks that look like
 * bugs. `waitForLaps` blocks until `n` have gone by, or gives up after `timeoutSeconds`.
 *
 * The whole log is searched for the two kinds of line rather than its tail read. The emulator
 * writes a status line every second, so a tail of any fixed length loses the banner a few minutes
 * into a run and counts only the laps still in it, which a long batch of captures used to store in
 * `run.json` as the laps it had seen.
 */
export function lapsCompleted(host: Host): number {
  const found = powershell(
    host,
    `if (-not (Test-Path ${psq(GUEST_LOG)})) { exit 0 }
Select-String -LiteralPath ${psq(GUEST_LOG)} -Pattern '${LAP_LINES}' | ForEach-Object { $_.Line }`,
    90,
  );
  const lines = found.stdout.split('\n');
  const banner = lines.map((l, i) => (l.includes('IrsdkEmulator - scenario') ? i : -1)).filter((i) => i >= 0).pop() ?? -1;
  return lines.slice(banner + 1).filter((l) => /lap \d+ completed in/.test(l)).length;
}

export function waitForLaps(host: Host, n: number, timeoutSeconds = 420): boolean {
  const deadline = Date.now() + timeoutSeconds * 1000;
  let seen = -1;
  while (Date.now() < deadline) {
    const laps = lapsCompleted(host);
    if (laps !== seen) {
      seen = laps;
      console.log(`  ${laps} of ${n} laps completed`);
    }
    if (laps >= n) return true;
    sleep(10);
  }
  return false;
}

/**
 * Follows the log in the local terminal, so the per-second line the emulator prints on the VM is
 * visible here. It polls rather than streaming, since the transport is one SSH round trip per read
 * and the line only changes once a second anyway.
 *
 * Nothing here catches a signal, deliberately. Bun calls a SIGINT handler only when its event
 * loop turns, and a script blocked in `spawnSync` carries on past Ctrl-C until it does, so a
 * handler here would not stop the loop when asked. The trap lives in `scripts/emulator.sh`, which
 * is what `bun run emulator` invokes, because a shell trap runs at once.
 */
export async function follow(host: Host): Promise<void> {
  let seen = '';
  for (;;) {
    const line = tail(host, 1).stdout.trim();
    if (line && line !== seen) {
      seen = line;
      console.log(line);
    }
    // Only a guest that answered "none" has stopped; one that could not be asked is asked again.
    const running = runningPid(host);
    if (running.ok && running.pid === null) {
      console.log('the emulator has stopped');
      return;
    }
    await Bun.sleep(1000);
  }
}

const USAGE = `emulator: run the synthetic iRacing telemetry feed on the test VM.

  bun run emulator start <scenario> [--replace] [--follow]
  bun run emulator stop
  bun run emulator status
  bun run emulator tail [n]
  bun run emulator upload            build and copy the exe and its scenarios to the VM

Scenarios are the files in tools/irsdk-emulator/scenarios, currently: ${scenarios().join(', ') || '(none built)'}.

Starting refuses while one is already running: two writers to the shared memory make SimHub drop
the connection. --follow prints the per-second status line here; Ctrl-C stops the emulator on the
VM and returns, which is handled by scripts/emulator.sh rather than here.
`;

function report(r: RunResult): never {
  if (r.stdout) console.log(r.stdout);
  if (r.stderr) console.error(r.stderr);
  process.exit(r.ok ? 0 : 1);
}

export async function main(argv: readonly string[]): Promise<void> {
  const [command, ...rest] = argv;
  if (!command || command === 'help' || command === '--help') {
    console.log(USAGE);
    return;
  }
  const host = resolveHost();

  switch (command) {
    case 'upload': {
      const built = build();
      if (!built.ok) return report(built);
      return report(upload(host));
    }
    case 'start': {
      const scenario = rest.find((a) => !a.startsWith('--')) ?? 'race';
      const built = build();
      if (!built.ok) return report(built);
      const sent = upload(host);
      if (!sent.ok) return report(sent);
      const started = start(host, { scenario, replace: rest.includes('--replace') });
      if (!started.ok || !rest.includes('--follow')) return report(started);
      console.log(started.stdout);
      await follow(host);
      return;
    }
    case 'stop':
      return report(stop(host));
    case 'status': {
      const running = runningPid(host);
      if (!running.ok) return report(running);
      return report({ ok: true, code: 0, stdout: running.pid === null ? 'not running' : `running (pid ${running.pid})`, stderr: '' });
    }
    case 'tail':
      return report(tail(host, Number(rest[0] ?? 20)));
    default:
      console.error(`unknown command: ${command}\n\n${USAGE}`);
      process.exit(2);
  }
}

if (import.meta.main) await main(process.argv.slice(2));
