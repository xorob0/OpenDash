#!/usr/bin/env bun
/**
 * emulator: runs a synthetic telemetry feed on the test VM, in the one way that works.
 *
 * There is one emulator per sim (`scripts/sims.ts`): `IrsdkEmulator.exe` writes iRacing's shared
 * memory and `acs.exe` Assetto Corsa's, and every function here takes the sim it is about, iRacing
 * when none is named. Two mistakes are easy to make with either and both fail silently, which is why
 * this exists rather than a paragraph in a README.
 *
 * The first is the logon session. The emulators create named kernel objects under `Local\`
 * (`Local\IRSDKMemMapFileName`, `Local\acpmf_physics` and the rest), and `Local\` names are per
 * logon session, so an emulator started over SSH lands in session 0 where SimHub, which runs on the
 * desktop, cannot see it. Nothing reports an error: SimHub simply never connects and the dash shows
 * its defaults. Every start therefore goes through the interactive desktop.
 *
 * The second is a second copy. Two processes writing the same shared memory make SimHub drop the
 * connection, and the symptom is a disconnection with no cause in any log. Starting refuses while
 * one is running unless it is asked to replace it. The other sim's emulator counts as well: it
 * writes other memory, but it is a game SimHub can switch to, so one emulator runs at a time.
 *
 * Stopping uses the stop file rather than killing the process, because the emulator says the
 * session is over on the way out (iRacing's header status 0, Assetto Corsa's AC_OFF) and SimHub
 * then sees a clean disconnect instead of waiting out its timeout.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { IRACING, SIMS, simFromArgs, sourceDir, type Sim } from './sims.ts';
import { inDesktop, onHost, powershell, psq, resolveHost, shq, sleep, toShare, type Host, type RunResult } from './vm.ts';

const buildDir = (sim: Sim): string => path.join(sourceDir(sim), 'bin/Release/net48');
const guestExe = (sim: Sim): string => `${sim.guestDir}\\${sim.exe}`;
const guestLog = (sim: Sim): string => `${sim.guestDir}\\emulator.log`;
const guestStop = (sim: Sim): string => `${sim.guestDir}\\stop.txt`;
/** The share as an SSH session sees it; `Z:` exists only in the interactive one. */
const SHARE_UNC = '\\\\host.lan\\Data';

/**
 * iRacing's scenarios with no committed trace: the ones that will never have one, and the ones that
 * do not have one yet. The list and each reason live on the sim in `scripts/sims.ts`, beside Assetto
 * Corsa's.
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
export const UNTRACED_SCENARIOS: readonly string[] = IRACING.untraced;

/** Every scenario a run of that sim may name: the JSON files beside its emulator. */
export function scenarios(sim: Sim = IRACING): string[] {
  const dir = path.join(sourceDir(sim), 'scenarios');
  if (!existsSync(dir)) return [];
  return readdirSync(dir)
    .filter((f) => f.endsWith('.json'))
    .map((f) => f.replace(/\.json$/, ''))
    .sort();
}

/** The scenarios of that sim a committed trace is expected for. */
export function tracedScenarioNames(sim: Sim = IRACING): string[] {
  return scenarios(sim).filter((name) => !sim.untraced.includes(name));
}

/** Whether the emulator is running: its process id, or null when none is. */
export type PidResult = RunResult & { pid: number | null };

/**
 * The emulator's process id on the VM, null when none is running, or a failure when the guest could
 * not be asked. The failure is kept apart from "none" because a caller that reads "none" goes on to
 * start one, and starting one beside another is the two-writer fault this file exists to prevent: an
 * SSH call that failed is not grounds for that.
 */
export function runningPid(host: Host, sim: Sim = IRACING): PidResult {
  const r = powershell(host, `$p = Get-Process ${sim.process} -ErrorAction SilentlyContinue; if ($p) { $p.Id } else { '' }`, 60);
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

/** Builds the sim's emulator. It cross-builds from any platform. */
export function build(sim: Sim = IRACING): RunResult {
  const r = spawnSync('dotnet', ['build', sim.sourceDir, '-c', 'Release', '--nologo', '-v', 'q'], {
    cwd: path.resolve(import.meta.dir, '..'),
    encoding: 'utf8',
    timeout: 600_000,
  });
  return { ok: r.status === 0, code: r.status ?? -1, stdout: (r.stdout ?? '').trim(), stderr: (r.stderr ?? '').trim() };
}

/** Every file under the sim's folders, relative to its source folder and `/`-separated, sorted. */
export function filesToUpload(sim: Sim): string[] {
  const files: string[] = [];
  const walk = (relative: string): void => {
    for (const entry of readdirSync(path.join(sourceDir(sim), relative), { withFileTypes: true })) {
      const child = `${relative}/${entry.name}`;
      if (entry.isDirectory()) walk(child);
      else files.push(child);
    }
  };
  for (const folder of sim.folders) if (existsSync(path.join(sourceDir(sim), folder))) walk(folder);
  return files.sort();
}

/**
 * Puts the exe and its folders on the VM together. They travel as a set because the emulator
 * resolves a scenario name against its own directory, so an exe updated without its scenarios
 * fails on a name that plainly exists in the repository; and Assetto Corsa's carries `content\`,
 * the fake install SimHub reads the car and the track from.
 */
export function upload(host: Host, sim: Sim = IRACING): RunResult {
  const exe = path.join(buildDir(sim), sim.exe);
  if (!existsSync(exe)) return { ok: false, code: 1, stdout: '', stderr: `no emulator at ${exe}; build it first` };

  const files = filesToUpload(sim);
  const dirs = [...new Set(files.map((f) => path.posix.dirname(f)))];
  onHost(host, `mkdir -p ${dirs.map((d) => shq(`/opt/winvm/shared/${sim.shareDir}/${d}`)).join(' ')}`);
  const sent = toShare(host, exe, `${sim.shareDir}/${sim.exe}`);
  if (!sent.ok) return sent;
  for (const file of files) {
    const one = toShare(host, path.join(sourceDir(sim), file), `${sim.shareDir}/${file}`);
    if (!one.ok) return one;
  }

  // Each folder is replaced rather than merged, so that a scenario deleted from the repository is
  // gone from the VM as well and cannot be started under a name that no longer exists.
  const folders = sim.folders.map(
    (folder) => `Remove-Item (Join-Path $dir ${psq(folder)}) -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item ${psq(`${SHARE_UNC}\\${sim.shareDir}\\${folder}`)} $dir -Recurse -Force`,
  );
  return powershell(
    host,
    `$dir = ${psq(sim.guestDir)}
New-Item -ItemType Directory -Force -Path $dir | Out-Null
Copy-Item ${psq(`${SHARE_UNC}\\${sim.shareDir}\\${sim.exe}`)} $dir -Force
${folders.join('\n')}
"uploaded: $((Get-ChildItem (Join-Path $dir 'scenarios')).Count) scenarios"`,
    180,
  );
}

/**
 * Stops the emulator through its stop file, so it says the session is over on the way out and
 * SimHub registers a disconnect rather than timing out. Falls back to killing it only when the
 * process ignores the file, which means it was started without one.
 */
export function stop(host: Host, sim: Sim = IRACING, timeoutSeconds = 20): RunResult {
  const running = runningPid(host, sim);
  if (!running.ok) return running;
  if (running.pid === null) return { ok: true, code: 0, stdout: 'not running', stderr: '' };
  return powershell(
    host,
    `New-Item -ItemType File -Force -Path ${psq(guestStop(sim))} | Out-Null
$deadline = (Get-Date).AddSeconds(${Math.trunc(timeoutSeconds)})
while ((Get-Date) -lt $deadline) {
  if (-not (Get-Process ${sim.process} -ErrorAction SilentlyContinue)) {
    Remove-Item ${psq(guestStop(sim))} -Force -ErrorAction SilentlyContinue
    'stopped cleanly'; exit 0
  }
  Start-Sleep -Milliseconds 500
}
Get-Process ${sim.process} -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item ${psq(guestStop(sim))} -Force -ErrorAction SilentlyContinue
'killed after ${Math.trunc(timeoutSeconds)}s; SimHub will time out rather than see a disconnect'`,
    timeoutSeconds + 60,
  );
}

/**
 * Stops every sim's emulator, for a run that was interrupted and cannot say which one it started.
 * Each is asked in turn, and the first that cannot be asked is the answer.
 */
export function stopAll(host: Host): RunResult {
  const said: string[] = [];
  for (const sim of SIMS) {
    const r = stop(host, sim);
    if (!r.ok) return r;
    said.push(`${sim.label} ${r.stdout.trim()}`);
  }
  return { ok: true, code: 0, stdout: said.join(', '), stderr: '' };
}

export interface StartOptions {
  scenario: string;
  /** Which sim's emulator; iRacing's when absent. */
  sim?: Sim;
  /** Stop an emulator that is already running instead of refusing. */
  replace?: boolean;
  /** How long to wait for the process and its first log line. */
  waitSeconds?: number;
}

/**
 * Stops every emulator but the sim's own, or with `replace` unset refuses when one is running.
 *
 * Another sim's emulator does not share this one's memory, but it is a game SimHub can see: with a
 * licence SimHub switches to whichever game it finds running, so a stray `acs.exe` left by an
 * Assetto Corsa run can take an iRacing run's SimHub onto AC halfway through a check (#1138).
 */
export function stopOthers(host: Host, sim: Sim, replace = true): RunResult {
  const said: string[] = [];
  for (const other of SIMS) {
    if (other === sim) continue;
    const running = runningPid(host, other);
    if (!running.ok) return running;
    if (running.pid === null) continue;
    if (!replace) {
      return {
        ok: false,
        code: 1,
        stdout: '',
        stderr: `the ${other.label} emulator is running (pid ${running.pid}), and SimHub can switch to the game it finds running.\nStop it with \`bun run emulator stop --sim ${other.id}\`, or start with --replace.`,
      };
    }
    const stopped = stop(host, other);
    if (!stopped.ok) return stopped;
    said.push(`${other.label} ${stopped.stdout.trim()}`);
  }
  return { ok: true, code: 0, stdout: said.join(', ') || 'no other emulator running', stderr: '' };
}

/**
 * Starts the sim's emulator in the interactive desktop on the named scenario, alone: every other
 * sim's emulator is stopped first, or with `replace` unset is a refusal like a second copy of its own.
 */
export function start(host: Host, opts: StartOptions): RunResult {
  const sim = opts.sim ?? IRACING;
  const known = scenarios(sim);
  if (known.length > 0 && !known.includes(opts.scenario)) {
    return { ok: false, code: 1, stdout: '', stderr: `unknown ${sim.label} scenario "${opts.scenario}"; the repository ships ${known.join(', ')}` };
  }

  const others = stopOthers(host, sim, opts.replace ?? false);
  if (!others.ok) return others;

  const running = runningPid(host, sim);
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
    const stopped = stop(host, sim);
    if (!stopped.ok) return stopped;
  }

  // A previous run may have left the stop file behind, which would end this one immediately.
  powershell(host, `Remove-Item ${psq(guestStop(sim))} -Force -ErrorAction SilentlyContinue`, 60);

  const launched = inDesktop(host, guestExe(sim), `${opts.scenario} --log ${guestLog(sim)} --stop-file ${guestStop(sim)}`, sim.guestDir);
  if (!launched.ok) return launched;

  const waitSeconds = opts.waitSeconds ?? 30;
  const deadline = Date.now() + waitSeconds * 1000;
  while (Date.now() < deadline) {
    const now = runningPid(host, sim);
    if (now.ok && now.pid !== null) {
      sleep(3);
      const line = tail(host, 1, sim);
      return { ok: true, code: 0, stdout: `running ${opts.scenario} (pid ${now.pid})\n${line.stdout}`, stderr: '' };
    }
    sleep(2);
  }
  return {
    ok: false,
    code: 1,
    stdout: '',
    stderr: `the emulator did not appear within ${waitSeconds}s. It is launched into the interactive desktop through a scheduled task, so check that somebody is logged in: \`bun run vm shot\`. ${sim.label}'s emulator writes why it stopped to ${guestLog(sim)}: \`bun run emulator tail --sim ${sim.id}\`.`,
  };
}

/** The last lines of the emulator's log, which is its per-second status line. */
export function tail(host: Host, lines: number, sim: Sim = IRACING): RunResult {
  return powershell(
    host,
    `if (-not (Test-Path ${psq(guestLog(sim))})) { 'no emulator log yet'; exit 0 }
Get-Content ${psq(guestLog(sim))} -Tail ${Math.max(1, Math.trunc(lines))}`,
    90,
  );
}

/** The lines of the log that count laps: a run's banner, and a completed lap, which both emulators write alike. */
const lapLines = (sim: Sim): string => `${sim.banner}|lap \\d+ completed in`;

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
export function lapsCompleted(host: Host, sim: Sim = IRACING): number {
  const found = powershell(
    host,
    `if (-not (Test-Path ${psq(guestLog(sim))})) { exit 0 }
Select-String -LiteralPath ${psq(guestLog(sim))} -Pattern '${lapLines(sim)}' | ForEach-Object { $_.Line }`,
    90,
  );
  const lines = found.stdout.split('\n');
  const banner = lines.map((l, i) => (l.includes(sim.banner) ? i : -1)).filter((i) => i >= 0).pop() ?? -1;
  return lines.slice(banner + 1).filter((l) => /lap \d+ completed in/.test(l)).length;
}

export function waitForLaps(host: Host, n: number, timeoutSeconds = 420, sim: Sim = IRACING): boolean {
  const deadline = Date.now() + timeoutSeconds * 1000;
  let seen = -1;
  while (Date.now() < deadline) {
    const laps = lapsCompleted(host, sim);
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
export async function follow(host: Host, sim: Sim = IRACING): Promise<void> {
  let seen = '';
  for (;;) {
    const line = tail(host, 1, sim).stdout.trim();
    if (line && line !== seen) {
      seen = line;
      console.log(line);
    }
    // Only a guest that answered "none" has stopped; one that could not be asked is asked again.
    const running = runningPid(host, sim);
    if (running.ok && running.pid === null) {
      console.log('the emulator has stopped');
      return;
    }
    await Bun.sleep(1000);
  }
}

const USAGE = `emulator: run a synthetic telemetry feed on the test VM.

  bun run emulator start <scenario> [--sim ac] [--replace] [--follow]
  bun run emulator stop [--sim ac]
  bun run emulator status [--sim ac]
  bun run emulator tail [n] [--sim ac]
  bun run emulator upload [--sim ac]   build and copy the exe and its folders to the VM

--sim picks the emulator: iracing, the default, or ac for Assetto Corsa's.
iRacing's scenarios are the files in ${IRACING.sourceDir}/scenarios: ${scenarios(IRACING).join(', ') || '(none)'}.
${SIMS.filter((s) => s !== IRACING)
  .map((s) => `${s.label}'s are in ${s.sourceDir}/scenarios: ${scenarios(s).join(', ') || '(none)'}.`)
  .join('\n')}

Starting refuses while one is already running: two writers to the shared memory make SimHub drop
the connection. It refuses too while the other sim's emulator runs, which SimHub could switch to;
--replace stops either. --follow prints the per-second status line here; Ctrl-C stops the emulator on the
VM and returns, which is handled by scripts/emulator.sh rather than here.

An emulator plays to SimHub only while SimHub reads its game, and SimHub reads one game at a time.
\`bun run dev --sim ac\` selects Assetto Corsa before it starts SimHub; starting the emulator alone
does not.
`;

function report(r: RunResult): never {
  if (r.stdout) console.log(r.stdout);
  if (r.stderr) console.error(r.stderr);
  process.exit(r.ok ? 0 : 1);
}

/** The arguments with `--sim` and its value taken out, so that the sim is never read as a scenario. */
export function withoutSim(argv: readonly string[]): string[] {
  return argv.filter((a, i) => a !== '--sim' && !a.startsWith('--sim=') && argv[i - 1] !== '--sim');
}

export async function main(argv: readonly string[]): Promise<void> {
  const [command, ...args] = argv;
  if (!command || command === 'help' || command === '--help') {
    console.log(USAGE);
    return;
  }
  let sim: Sim;
  try {
    sim = simFromArgs(args);
  } catch (e) {
    console.error(e instanceof Error ? e.message : String(e));
    process.exit(2);
  }
  const rest = withoutSim(args);
  const host = resolveHost();

  switch (command) {
    case 'upload': {
      const built = build(sim);
      if (!built.ok) return report(built);
      return report(upload(host, sim));
    }
    case 'start': {
      const scenario = rest.find((a) => !a.startsWith('--')) ?? sim.defaultScenario;
      const built = build(sim);
      if (!built.ok) return report(built);
      const sent = upload(host, sim);
      if (!sent.ok) return report(sent);
      const started = start(host, { scenario, sim, replace: rest.includes('--replace') });
      if (!started.ok || !rest.includes('--follow')) return report(started);
      console.log(started.stdout);
      await follow(host, sim);
      return;
    }
    case 'stop':
      return report(stop(host, sim));
    case 'status': {
      const running = runningPid(host, sim);
      if (!running.ok) return report(running);
      return report({ ok: true, code: 0, stdout: running.pid === null ? 'not running' : `running (pid ${running.pid})`, stderr: '' });
    }
    case 'tail':
      return report(tail(host, Number(rest[0] ?? 20), sim));
    default:
      console.error(`unknown command: ${command}\n\n${USAGE}`);
      process.exit(2);
  }
}

if (import.meta.main) await main(process.argv.slice(2));
