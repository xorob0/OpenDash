#!/usr/bin/env bun
/**
 * record: turn an emulator scenario into a committed telemetry trace.
 *
 * The values a dashboard reads are `DataCorePlugin.GameData.*` and everything SimHub computes on
 * top of it, which is SimHub's normalised view of the sim and not the irsdk variables the emulator
 * writes. The mapping between the two is SimHub's own code, so the only truthful way to obtain one
 * is to ask a running SimHub. That is what this does: it puts a recorder plugin on the Windows VM,
 * runs the scenario past it once, and brings back what SimHub saw.
 *
 * The VM is therefore the origin of a trace and not a dependency of it. A recording happens when a
 * scenario changes or when a package starts reading a property that was not recorded; everything
 * afterwards replays the committed file and needs no Windows at all.
 *
 * Beside the properties it records calls (#257). `drivername(3)` and the rest of the opponent calls
 * answer from SimHub's in-memory leaderboard, which no property carries, so the recorder evaluates
 * every call `callsRead()` lists with SimHub's own NCalc engine on every frame and keeps the answer
 * under the call's text; a few probes ride along, literal formulas whose answers settle what NCalc
 * does where the browser evaluator could only read the source.
 *
 * Frames are taken on the emulator's tick rather than on the wall clock, so two recordings of an
 * unchanged scenario cover the same span of it. What still moves between them is what SimHub keeps
 * its own history of, the lap deltas and the fuel averages, and the wall clock it publishes.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { callsRead, propertiesRead } from '../packages/dash/src/properties.ts';
import { MAX_CARS } from '../packages/generator/src/opponentCalls.ts';
import {
  build as buildEmulator,
  runningPid,
  start as startEmulator,
  stop as stopEmulator,
  tail as emulatorLog,
  upload as uploadEmulator,
  scenarios,
  tracedScenarioNames,
  UNTRACED_SCENARIOS,
} from './emulator.ts';
import {
  TRACE_DIR,
  TRACE_VERSION,
  formatTrace,
  traceFile,
  type Trace,
  type TraceColumn,
  type TraceValue,
  type TraceValueType,
} from './trace.ts';
import {
  activatePlugin,
  claim,
  forgetPlugin,
  fromShare,
  installPlugin,
  powershell,
  psq,
  readClaim,
  release,
  resolveHost,
  simhubLogs,
  simhubStart,
  simhubStop,
  sleep,
  status,
  toShare,
  up,
  waitReady,
  whoAmI,
  type Host,
  type RunResult,
} from './vm.ts';

const repoRoot = path.resolve(import.meta.dir, '..');
const RECORDER_DIR = path.join(repoRoot, 'tools/trace-recorder');
const RECORDER_DLL = path.join(RECORDER_DIR, 'bin/Release/net48/OpenDashTraceRecorder.dll');
const RECORDER_CLASS = 'OpenDashTraceRecorder.TraceRecorderPlugin';
const RECORDER_DLL_NAME = 'OpenDashTraceRecorder.dll';
const REQUEST_NAME = 'opendash-trace-request.json';

const SIMHUB_DIR = 'C:\\Program Files (x86)\\SimHub';
const SHARE_UNC = '\\\\host.lan\\Data';
/** Where the recorder writes on the guest's own disk; the share is read, not written. */
const GUEST_OUT = 'C:\\Temp\\opendash-trace\\trace.ndjson';

/** The emulator's tick rate. Every scenario shipped runs at 60 Hz; the header records what was used. */
export const TICK_RATE = 60;
/** Frames per second of recorded telemetry: smooth enough for a video, and a sixth of what the sim publishes. */
export const DEFAULT_HZ = 10;
/** Twenty seconds, which is longer than any per-pull-request video and short enough to commit. */
export const DEFAULT_FRAMES = 200;
/**
 * Seconds of telemetry to let pass before the first frame is taken.
 *
 * A few would be enough for SimHub to connect and settle. Two minutes is here for a different
 * reason: the fuel figures under `DataCorePlugin.Computed.*` are averages SimHub builds by watching
 * laps complete, and they read zero until it has seen one. The scenarios lap in ninety-eight
 * seconds, so a trace taken before that carries a fuel card showing nothing, and a video made from
 * it would look like a dash whose fuel calculation is broken rather than one recorded too early.
 *
 * Counted from the moment SimHub first reports the game running, not from an absolute tick. The
 * emulator's `SessionTick` starts at the scenario's own session time, which is seventy thousand in
 * a race half run and forty thousand in a timed one, so no fixed tick could serve every scenario.
 */
export const DEFAULT_WARM_UP_SECONDS = 120;

/**
 * Recorded beyond what the packages read, so that a trace says which tick each of its frames came
 * from. It costs one column and it is the only thing that makes a re-recording comparable.
 */
export const PROVENANCE_PROPERTIES: readonly string[] = ['DataCorePlugin.GameRawData.Telemetry.SessionTick'];

/** Emulator ticks between frames. The rate has to divide the tick rate, which `parseArgs` checks. */
export const stepFor = (hz: number): number => TICK_RATE / hz;

/** Every property a recording asks SimHub for: what the packages read, plus the provenance column. */
export const recordedProperties = (): string[] => [...new Set([...propertiesRead(), ...PROVENANCE_PROPERTIES])].sort();

/**
 * A formula recorded for what SimHub's NCalc answers to it rather than for anything a dashboard
 * draws. The browser evaluator (#395) was written from NCalc 1.3.8's source and SimHub's decompiled
 * function table, without a SimHub to ask, and where the two could not settle a reading it says
 * **Unverified** (the list is in `packages/generator/src/ncalc/functions.ts` and PR #726). A probe
 * asks SimHub the question once per frame, like any call, and `scripts/trace.test.ts` holds the
 * evaluator to every answer once a version 2 trace is committed.
 */
export interface Probe {
  /** The formula, written as the evaluator parses it; it is also the column's name. */
  text: string;
  /** What its answer settles. */
  settles: string;
}

/**
 * A property nobody publishes, which SimHub reads as null: the only way to put a null into a
 * formula, since NCalc 1.3.8 has no null literal.
 */
export const PROBE_NULL = '[OpenDashTraceProbe.Unpublished]';

/**
 * The probes. Each is chosen so that the readings in question give different answers: `max(0, 2.6)`
 * is 3 if `max` takes its left operand's type and 2.6 if it does not, and `abs(7) / 2` is 3.5 for a
 * decimal and 3 for an integer.
 */
export const PROBE_CALLS: readonly Probe[] = [
  { text: 'max(0, 2.6)', settles: 'max takes the left operand\'s type, Int32 here: 3 rather than 2.6' },
  { text: 'min(5, 2.6)', settles: 'min takes the left operand\'s type: 3 rather than 2.6' },
  { text: 'max(0, 2.6) / 2', settles: 'the Int32 max divides as an integer: 1 rather than 1.3' },
  { text: 'max(2.6, 0)', settles: 'a double on the left stays a double: 2.6' },
  { text: `max(${PROBE_NULL}, 2)`, settles: 'a null side of max returns the other side' },
  { text: 'abs(7) / 2', settles: 'abs returns a decimal: 3.5 rather than the integer 3' },
  { text: 'abs(-2.5) * 1.5', settles: 'a decimal times a double throws, so the dash draws nothing' },
  { text: 'abs(-2.5) > 2.4', settles: 'a decimal compares with a double' },
  { text: 'round(2.675, 2)', settles: 'round on the scaled double, as .NET Framework does it' },
  { text: 'round(1.005, 2)', settles: 'round on a value just under its midpoint in binary' },
  { text: 'round(2.5, 0)', settles: 'round goes to even at a midpoint: 2 rather than 3' },
  { text: 'round(0.125, 2)', settles: 'round goes to even at an exact binary midpoint: 0.12' },
  { text: `round(${PROBE_NULL}, 1)`, settles: 'round reads a null as 0 through Convert.ToDouble' },
  { text: `truncate(${PROBE_NULL})`, settles: 'truncate reads a null as 0' },
  { text: "format(2.675, '0.00')", settles: 'format keeps 15 significant digits, then rounds half away from zero: 2.68' },
  { text: "format(0.125, '0.00')", settles: 'format rounds an exact midpoint away from zero: 0.13' },
  { text: "format(1.005, '0.00')", settles: 'format rounds the 15-digit 1.005 up, where round does not' },
  { text: "format(0, '0.0', true)", settles: 'format with addSign puts + on zero' },
  { text: "format(-0.04, '0.0', true)", settles: 'the sign format gives a negative that rounds to zero' },
  { text: "format(-0.04, '0.0')", settles: 'whether .NET Framework writes -0.0 or 0.0 for a negative that rounds to zero' },
  { text: "format(0.04, '0.0', true)", settles: 'a positive that rounds to zero keeps its +' },
  { text: `format(${PROBE_NULL}, '0.0')`, settles: 'format of a null is null' },
  { text: "format('text', '0.0')", settles: 'format returns text it cannot format unchanged' },
  { text: "'3' = 3", settles: 'a string against a number compares as strings: true' },
  { text: "'03' = 3", settles: 'and so 03 is not 3: false' },
  { text: "1 + '2'", settles: 'a number plus a numeric string adds, as a decimal' },
  { text: "'1' + 2", settles: 'a string on the left concatenates' },
  { text: "1 + 'a'", settles: 'a number plus text throws, so the dash draws nothing' },
  { text: `${PROBE_NULL} = ${PROBE_NULL}`, settles: 'a comparison with null throws, null = null included' },
  { text: `if(${PROBE_NULL}, 1, 2)`, settles: 'if reads a null as false' },
  { text: `isnull(${PROBE_NULL}, 'x')`, settles: 'isnull returns the fallback for a null' },
  { text: 'secondstotimespan(5.2)', settles: 'FromSeconds rounds to the millisecond' },
  { text: `secondstotimespan(${PROBE_NULL})`, settles: 'secondstotimespan of a null is null' },
  { text: 'toshorttime(secondstotimespan(5.2), 1, false, false)', settles: 'toshorttime drops the minutes under a minute: 5.2' },
  { text: 'toshorttime(secondstotimespan(5.2), 1, true, false)', settles: 'toshorttime puts + on request: +5.2' },
  { text: 'toshorttime(secondstotimespan(5.2), 1, false, true)', settles: 'forceMinutes keeps the minutes: 0:05.2' },
  { text: 'toshorttime(secondstotimespan(65.27), 3, false, false)', settles: 'toshorttime over a minute, to three decimals' },
  { text: 'toshorttime(secondstotimespan(-1.5), 1, true, false)', settles: 'toshorttime of a negative span' },
  { text: 'toshorttime(secondstotimespan(5.29), 1, false, false)', settles: 'toshorttime truncates the fraction rather than rounding it: 5.2' },
  { text: `toshorttime(${PROBE_NULL}, 1, false, false)`, settles: 'toshorttime of a null is null' },
  { text: `ucase(${PROBE_NULL})`, settles: 'ucase reads a null as the empty string' },
  { text: "left('abc', 0, 5)", settles: 'left clamps a length past the end of the text' },
  { text: "right('abcdef', 0, 2)", settles: 'what right takes its three arguments to mean' },
];

/**
 * Every call a recording asks SimHub to evaluate: each opponent call any package can make, in the
 * canonical spelling the evaluator looks it up by, and the probes. Sorted and deduplicated, since it
 * is written into the request as it is and the trace keeps calls sorted by text.
 */
export const recordedCalls = (): string[] => [...new Set([...callsRead(), ...PROBE_CALLS.map((probe) => probe.text)])].sort();

// --------------------------------------------------------------- the recorder's own file format

/**
 * The version of the file the recorder plugin writes. 2 added a frame's `c`, the calls, and the
 * header's `cars`; the plugin is built from this tree for every recording, so only the current
 * version is ever read.
 */
export const RECORDER_VERSION = 2;

export interface RecordingHeader {
  recorder: number;
  scenario: string;
  hz: number;
  frames: number;
  warmUpTicks: number;
  step: number;
  /** The field size the calls were enumerated over, as the request gave it. */
  cars: number;
  started: string;
}

export interface RecordingFrame {
  tick: number;
  /** The properties, by name. */
  v: Record<string, TraceValue>;
  /** The calls, by text; empty when the request named none. */
  c: Record<string, TraceValue>;
}

export interface Recording {
  header: RecordingHeader;
  frames: RecordingFrame[];
  /** Only the properties and calls whose .NET type a JSON scalar would not carry. */
  types: Record<string, TraceValueType>;
}

export class RecordingError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'RecordingError';
  }
}

/**
 * Reads what the recorder plugin left: a header, one line per frame, and a trailer naming the
 * properties whose type a JSON scalar would lose. A file with no trailer was interrupted, which is
 * worth saying rather than committing the short recording it holds.
 */
export function parseRecording(text: string): Recording {
  const lines = text.split('\n').filter((l) => l.trim() !== '');
  const first = lines[0];
  if (first === undefined) throw new RecordingError('the recorder wrote nothing; SimHub never loaded it, or it never saw the game running');
  const header = JSON.parse(first) as RecordingHeader;
  if (header.recorder !== RECORDER_VERSION) {
    throw new RecordingError(`recorder format ${JSON.stringify(header.recorder)}; this reader understands ${RECORDER_VERSION}, so rebuild the recorder from this tree`);
  }

  const frames: RecordingFrame[] = [];
  let types: Record<string, TraceValueType> | undefined;
  for (let i = 1; i < lines.length; i++) {
    const parsed = JSON.parse(lines[i] as string) as Record<string, unknown>;
    if (parsed.types !== undefined) {
      types = parsed.types as Record<string, TraceValueType>;
      continue;
    }
    frames.push({ tick: parsed.tick as number, v: parsed.v as Record<string, TraceValue>, c: (parsed.c ?? {}) as Record<string, TraceValue> });
  }
  if (types === undefined) throw new RecordingError(`the recording stops after ${frames.length} of ${header.frames} frames; SimHub closed or the emulator stopped before it was done`);
  if (frames.length !== header.frames) throw new RecordingError(`the recording holds ${frames.length} frames and claims ${header.frames}`);
  return { header, frames, types };
}

/**
 * Turns a recording into the committed trace: one column per property and one per call, a scalar
 * when it never moved. The transposition is the whole reason a trace is small, since most of what
 * SimHub publishes holds still for the length of a twenty second scenario, and most of the calls are
 * positions past the end of the field, which are null on every frame.
 */
export function toTrace(recording: Recording, recorded: string, simHub: string): Trace {
  const { header, frames, types } = recording;
  const columnsOf = (pick: (f: RecordingFrame) => Record<string, TraceValue>, call: boolean): TraceColumn[] =>
    [...new Set(frames.flatMap((f) => Object.keys(pick(f))))].sort().map((name) => {
      const values = frames.map((f) => (pick(f)[name] === undefined ? null : (pick(f)[name] as TraceValue)));
      const first = values[0] as TraceValue;
      const constant = values.every((v) => v === first);
      const type = types[name];
      return { p: name, ...(call ? { k: 'call' as const } : {}), ...(type === undefined ? {} : { t: type }), v: constant ? first : values };
    });
  return {
    header: {
      trace: TRACE_VERSION,
      scenario: header.scenario,
      frames: frames.length,
      hz: header.hz,
      ticks: [frames[0]?.tick ?? 0, header.step],
      recorded,
      simHub,
      cars: header.cars,
    },
    columns: [...columnsOf((f) => f.v, false), ...columnsOf((f) => f.c, true)],
  };
}

// -------------------------------------------------------------- what the VM says it is running

/**
 * The SimHub version, from the line it writes when it starts. Neither the executable's file version
 * nor its assembly version carries it: both read 1.0.0.0, and the only place 9.12.6 appears is that
 * log line. A trace is only as truthful as the SimHub that produced it, so it is worth the parse.
 */
export function parseSimHubVersion(log: string): string {
  return /Starting SimHub v([0-9][0-9A-Za-z.-]*)/.exec(log)?.[1] ?? 'unknown';
}

/**
 * The scenario the emulator on the VM is running, from the path it logs when it loads one. The last
 * match wins, since the log is appended to across runs.
 */
export function parseEmulatorScenario(log: string): string | undefined {
  const matches = [...log.matchAll(/scenarios[\\/]([A-Za-z0-9_.-]+)\.json/g)];
  return matches.length === 0 ? undefined : (matches[matches.length - 1]?.[1] as string);
}

// ------------------------------------------------------------------------------ the recording

export interface RecordOptions {
  scenarios: readonly string[];
  hz: number;
  frames: number;
  warmUpSeconds: number;
  /** Skip building and installing the OpenDash plugin, when the VM already carries a current one. */
  noBuild: boolean;
  /** Leave the recorder installed and the claim held, for recording again without the setup. */
  keep: boolean;
  outDir: string;
}

const fail = (message: string): RunResult => ({ ok: false, code: 1, stdout: '', stderr: message });

/** Builds the recorder plugin. It cross-builds from any platform, like the emulator. */
export function buildRecorder(): RunResult {
  const r = spawnSync('dotnet', ['build', 'tools/trace-recorder', '-c', 'Release', '--nologo', '-v', 'q'], {
    cwd: repoRoot,
    encoding: 'utf8',
    timeout: 600_000,
  });
  return { ok: r.status === 0, code: r.status ?? -1, stdout: (r.stdout ?? '').trim(), stderr: (r.stderr ?? '').trim() };
}

/** Puts the recorder DLL beside SimHub and activates it. SimHub must be stopped. */
function installRecorder(host: Host): RunResult {
  if (!existsSync(RECORDER_DLL)) return fail(`no recorder at ${RECORDER_DLL}; build it first`);
  const sent = toShare(host, RECORDER_DLL, RECORDER_DLL_NAME);
  if (!sent.ok) return sent;
  const copied = powershell(
    host,
    `$ErrorActionPreference = 'Stop'
$dest = Join-Path ${psq(SIMHUB_DIR)} ${psq(RECORDER_DLL_NAME)}
Copy-Item ${psq(`${SHARE_UNC}\\${RECORDER_DLL_NAME}`)} $dest -Force
# Windows marks a file that arrived over a network share, and .NET refuses to load it silently.
Unblock-File -LiteralPath $dest -ErrorAction SilentlyContinue
"installed $((Get-Item $dest).Length) bytes"`,
    180,
  );
  if (!copied.ok) return copied;
  return activatePlugin(host, RECORDER_CLASS);
}

/** Takes the recorder off the VM again, so the next session's SimHub is the one a user would have. */
function removeRecorder(host: Host): RunResult {
  const removed = powershell(
    host,
    `Remove-Item (Join-Path ${psq(SIMHUB_DIR)} ${psq(RECORDER_DLL_NAME)}) -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path ${psq(SIMHUB_DIR)} ${psq(REQUEST_NAME)}) -Force -ErrorAction SilentlyContinue
'removed'`,
    180,
  );
  if (!removed.ok) return removed;
  return forgetPlugin(host, RECORDER_CLASS);
}

/**
 * Takes the recorder out of SimHub and leaves SimHub running, which is how every other command on
 * this VM expects to find it. SimHub has to be down to let go of the DLL. Run when a recording
 * ends, and by `scripts/interrupted.ts` when one is interrupted. Best effort, since the run is over
 * either way, but said out loud: a recorder left in, or a SimHub left down, is what the next
 * command on this VM would otherwise trip over.
 */
export function putBack(host: Host): RunResult {
  const problems: string[] = [];
  const stopped = simhubStop(host);
  if (!stopped.ok) problems.push(`${stopped.stderr}; the recorder may still be installed`);
  const removed = removeRecorder(host);
  if (!removed.ok) problems.push(`the recorder may still be installed: ${removed.stderr || removed.stdout}`);
  const started = simhubStart(host);
  if (!started.ok) problems.push(started.stderr);
  if (problems.length > 0) return fail(problems.join('\n'));
  return { ok: true, code: 0, stdout: `took the recorder out; SimHub ${started.stdout}`, stderr: '' };
}

/** What is recorded: the properties read by name, and the calls evaluated by SimHub's NCalc. */
export interface Asked {
  properties: readonly string[];
  calls: readonly string[];
}

/** The request the recorder reads at startup, as JSON. */
export function request(scenario: string, opts: Pick<RecordOptions, 'hz' | 'frames' | 'warmUpSeconds'>, asked: Asked) {
  return {
    scenario,
    hz: opts.hz,
    frames: opts.frames,
    step: stepFor(opts.hz),
    warmUpTicks: opts.warmUpSeconds * TICK_RATE,
    cars: MAX_CARS,
    out: GUEST_OUT,
    properties: asked.properties,
    calls: asked.calls,
  };
}

/** Writes the request the recorder reads at startup. SimHub must be restarted afterwards to see it. */
function writeRequest(host: Host, scenario: string, opts: RecordOptions, asked: Asked): RunResult {
  const local = path.join(repoRoot, 'build', REQUEST_NAME);
  mkdirSync(path.dirname(local), { recursive: true });
  writeFileSync(local, `${JSON.stringify(request(scenario, opts, asked), null, 2)}\n`, 'utf8');
  const sent = toShare(host, local, REQUEST_NAME);
  if (!sent.ok) return sent;
  return powershell(
    host,
    `$ErrorActionPreference = 'Stop'
Copy-Item ${psq(`${SHARE_UNC}\\${REQUEST_NAME}`)} (Join-Path ${psq(SIMHUB_DIR)} ${psq(REQUEST_NAME)}) -Force
Remove-Item ${psq(`${GUEST_OUT}.done`)} -Force -ErrorAction SilentlyContinue
"asked for ${opts.frames} frames of ${asked.properties.length} properties and ${asked.calls.length} calls"`,
    180,
  );
}

/** Waits for the recorder's done marker, which it writes once the last frame is on disk. */
function waitForRecording(host: Host, timeoutSeconds: number): RunResult {
  const deadline = Date.now() + timeoutSeconds * 1000;
  while (Date.now() < deadline) {
    const r = powershell(host, `if (Test-Path ${psq(`${GUEST_OUT}.done`)}) { Get-Content ${psq(`${GUEST_OUT}.done`)} -Raw } else { '' }`, 60);
    if (r.ok && r.stdout.trim() !== '') return { ok: true, code: 0, stdout: `${r.stdout.trim()} frames`, stderr: '' };
    sleep(3);
  }
  return fail(`no recording after ${timeoutSeconds}s. Check SimHub's log for "OpenDash trace recorder"; a game SimHub does not think is running is the usual reason.`);
}

/** Brings the recorded file back and writes the committed trace. */
function collect(host: Host, scenario: string, outDir: string, simHub: string): RunResult {
  const copied = powershell(host, `$ErrorActionPreference = 'Stop'
Copy-Item ${psq(GUEST_OUT)} ${psq(`${SHARE_UNC}\\opendash-trace.ndjson`)} -Force
'copied'`, 180);
  if (!copied.ok) return copied;
  const local = path.join(repoRoot, 'build', `${scenario}.raw.ndjson`);
  const back = fromShare(host, 'opendash-trace.ndjson', local);
  if (!back.ok) return back;

  let trace: Trace;
  try {
    trace = toTrace(parseRecording(readFileSync(local, 'utf8')), new Date().toISOString().slice(0, 10), simHub);
  } catch (e) {
    return fail(`${local}: ${e instanceof Error ? e.message : String(e)}`);
  }
  // Every scenario records to the same file on the guest, so a recording that never happened would
  // otherwise be committed under the next scenario's name.
  if (trace.header.scenario !== scenario) return fail(`the recording on the VM is of ${JSON.stringify(trace.header.scenario)}, not ${JSON.stringify(scenario)}`);
  mkdirSync(outDir, { recursive: true });
  const file = traceFile(scenario, outDir);
  writeFileSync(file, formatTrace(trace), 'utf8');
  const moving = trace.columns.filter((c) => Array.isArray(c.v)).length;
  const calls = trace.columns.filter((c) => c.k === 'call').length;
  const bytes = readFileSync(file).byteLength;
  return {
    ok: true,
    code: 0,
    stdout: `${path.relative(repoRoot, file)}: ${trace.columns.length - calls} properties, ${calls} calls, ${moving} moving, ${trace.header.frames} frames, ${(bytes / 1024).toFixed(0)} KB`,
    stderr: '',
  };
}

/** One scenario, from a stopped SimHub to a written trace. */
function recordOne(host: Host, scenario: string, opts: RecordOptions, asked: Asked): RunResult {
  console.log(`\n${scenario}: asking SimHub for ${opts.frames} frames at ${opts.hz} Hz`);
  stopEmulator(host);
  const stopped = simhubStop(host);
  if (!stopped.ok) return stopped;
  const requested = writeRequest(host, scenario, opts, asked);
  if (!requested.ok) return requested;
  // SimHub reads the request once, in Init, so it has to start after the request is in place.
  const started = simhubStart(host);
  if (!started.ok) return started;
  const simHub = parseSimHubVersion(simhubLogs(host, 400).stdout);

  const emulator = startEmulator(host, { scenario, replace: true });
  if (!emulator.ok) return emulator;

  // There is one VM and two sessions will fight over it, which is what the claim is for; but the
  // winvm tools, and anything else that never claims, drive the guest whoever holds it (#218). What
  // that looks like is a trace labelled with the
  // scenario that was asked for and holding the telemetry of the one somebody else started, and
  // nothing downstream could ever tell. So the emulator is asked what it is running, and the
  // process it answered as is checked again once the recording is done.
  const running = parseEmulatorScenario(emulatorLog(host, 400).stdout);
  if (running !== scenario) {
    return fail(`the emulator on the VM is running ${running === undefined ? 'a scenario it does not name' : JSON.stringify(running)}, not ${JSON.stringify(scenario)}; something else is driving the VM, see \`bun run vm who\``);
  }
  const pid = runningPid(host);
  if (!pid.ok) return pid;

  // The warm-up plus the recorded span, and two minutes more. The VM has two virtual processors and
  // SimHub logs "IRacing missing sample" on it under load, so the telemetry does not always arrive
  // at the sixty hertz the emulator writes it at, and a budget with no room in it fails a recording
  // that was only slow.
  const seconds = opts.warmUpSeconds + Math.ceil((opts.frames * stepFor(opts.hz)) / TICK_RATE) + 120;
  const waited = waitForRecording(host, seconds);
  if (!waited.ok) return waited;
  console.log(`  recorded ${waited.stdout}`);

  const after = runningPid(host);
  if (!after.ok) return fail(`could not tell whether the emulator was replaced while the recording ran, so the trace may be of somebody else's scenario: ${after.stderr}`);
  if (after.pid !== pid.pid) return fail('the emulator was replaced while the recording ran, so the trace is of somebody else\'s scenario; see `bun run vm who`');
  stopEmulator(host);
  return collect(host, scenario, opts.outDir, simHub);
}

export async function record(host: Host, opts: RecordOptions): Promise<number> {
  const known = scenarios();
  const unknown = opts.scenarios.filter((s) => !known.includes(s));
  if (unknown.length > 0) {
    console.error(`unknown scenario${unknown.length > 1 ? 's' : ''}: ${unknown.join(', ')}`);
    console.error(`one of ${known.join(', ')}`);
    return 1;
  }

  const held = readClaim(host);
  if (held && held.who !== whoAmI()) {
    console.error(`the VM is claimed by ${held.who} since ${held.since}${held.note ? ` (${held.note})` : ''}`);
    console.error('There is one VM. Wait, or ask them to run `bun run vm release`.');
    return 1;
  }
  const claimed = claim(host, `record ${opts.scenarios.join(' ')}`);
  if (!claimed.ok) {
    console.error(claimed.stderr);
    return 1;
  }

  let code = 0;
  try {
    if (!status(host).stdout.includes('guest-ssh: up')) {
      console.log('starting the VM');
      up(host);
      if (!waitReady(host, 300)) {
        console.error('the VM did not answer within five minutes; try `bun run vm status`');
        return 1;
      }
    }

    // The OpenDash plugin publishes every [OpenDash.*] property a face reads, so the trace is only
    // complete with a current one installed. Building it also builds the packages the list is
    // derived from, which is why it comes before the properties are computed.
    if (!opts.noBuild) {
      console.log('installing the OpenDash plugin, which is where the [OpenDash.*] defaults come from');
      const plugin = installPlugin(host);
      if (!plugin.ok) {
        console.error(plugin.stderr || plugin.stdout);
        return 1;
      }
    }
    const asked: Asked = { properties: recordedProperties(), calls: recordedCalls() };
    console.log(`recording ${asked.properties.length} properties and ${asked.calls.length} calls`);

    const recorder = buildRecorder();
    if (!recorder.ok) {
      console.error(recorder.stderr || recorder.stdout);
      return 1;
    }
    const stopped = simhubStop(host);
    if (!stopped.ok) {
      console.error(stopped.stderr);
      return 1;
    }
    const installed = installRecorder(host);
    if (!installed.ok) {
      console.error(installed.stderr || installed.stdout);
      return 1;
    }

    const emulator = buildEmulator();
    if (!emulator.ok) {
      console.error(emulator.stderr || emulator.stdout);
      return 1;
    }
    const uploaded = uploadEmulator(host);
    if (!uploaded.ok) {
      console.error(uploaded.stderr || uploaded.stdout);
      return 1;
    }

    for (const scenario of opts.scenarios) {
      const one = recordOne(host, scenario, opts, asked);
      if (!one.ok) {
        console.error(`${scenario}: ${one.stderr || one.stdout}`);
        code = 1;
        continue;
      }
      console.log(`  ${one.stdout}`);
    }
  } finally {
    stopEmulator(host);
    if (!opts.keep) {
      const back = putBack(host);
      if (!back.ok) console.error(back.stderr);
      release(host);
    } else {
      console.log('\nthe recorder is still installed and the VM is still claimed (--keep)');
    }
  }
  return code;
}

const USAGE = `record: turn an emulator scenario into a committed telemetry trace.

  bun run record [scenario ...] [--hz 10] [--frames 200] [--warm-up 120] [--no-build] [--keep]

  scenario      one or more; default every scenario a committed trace is expected for
                ${tracedScenarioNames().join(', ') || '(none)'}
                ${UNTRACED_SCENARIOS.join(', ')} are recorded only when named,
                and nothing replays one until it comes off UNTRACED_SCENARIOS in scripts/emulator.ts
  --hz          frames per second of recorded telemetry; default ${DEFAULT_HZ}
  --frames      how many frames; default ${DEFAULT_FRAMES}, which is ${DEFAULT_FRAMES / DEFAULT_HZ} seconds
  --warm-up     seconds of telemetry to let pass before the first frame; default ${DEFAULT_WARM_UP_SECONDS},
                which is what SimHub's computed fuel averages need before they stop reading zero
  --no-build    do not rebuild and reinstall the OpenDash plugin
  --keep        leave the recorder installed and the VM claimed

It claims the VM, installs a recorder plugin into SimHub, runs each scenario past it once and
writes traces/<scenario>.ndjson. Read the diff before committing: a trace is a reviewed artefact,
not a build output.
`;

/**
 * What to say about a scenario named on the command line that is meant to have no trace. It is
 * recorded all the same, since naming one is how it leaves UNTRACED_SCENARIOS, but until it does
 * nothing replays the file and `trace.test.ts` fails on it.
 */
export function untracedWarnings(names: readonly string[]): string[] {
  return names
    .filter((name) => UNTRACED_SCENARIOS.includes(name))
    .map((name) => `${name} is in UNTRACED_SCENARIOS: nothing replays its trace, and trace.test.ts fails on it until the name comes off that list in scripts/emulator.ts`);
}

export function parseArgs(argv: readonly string[]): RecordOptions | { help: true } {
  if (argv.includes('--help') || argv.includes('-h')) return { help: true };
  const flagValue = (name: string): string | undefined => {
    const index = argv.indexOf(`--${name}`);
    if (index >= 0 && argv[index + 1] && !argv[index + 1]!.startsWith('--')) return argv[index + 1];
    return argv.find((a) => a.startsWith(`--${name}=`))?.split('=').slice(1).join('=');
  };
  const whole = (name: string, fallback: number): number => {
    const raw = flagValue(name);
    if (raw === undefined) return fallback;
    const value = Number.parseInt(raw, 10);
    if (!Number.isInteger(value) || value < 1) throw new RecordingError(`--${name} needs a whole number, got ${JSON.stringify(raw)}`);
    return value;
  };
  const flagNames = new Set(['hz', 'frames', 'warm-up']);
  const positional: string[] = [];
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i] as string;
    if (arg.startsWith('--')) {
      const name = arg.slice(2).split('=')[0] as string;
      if (flagNames.has(name) && !arg.includes('=')) i++;
      continue;
    }
    positional.push(arg);
  }
  const hz = whole('hz', DEFAULT_HZ);
  if (TICK_RATE % hz !== 0) throw new RecordingError(`--hz must divide the emulator's ${TICK_RATE} Hz tick, and ${hz} does not`);
  return {
    // Only the scenarios a trace is expected for: the others would cost the VM two and a half
    // minutes each and write files trace.test.ts refuses (#519).
    scenarios: positional.length > 0 ? positional : tracedScenarioNames(),
    hz,
    frames: whole('frames', DEFAULT_FRAMES),
    warmUpSeconds: whole('warm-up', DEFAULT_WARM_UP_SECONDS),
    noBuild: argv.includes('--no-build'),
    keep: argv.includes('--keep'),
    outDir: TRACE_DIR,
  };
}

export async function main(argv: readonly string[]): Promise<number> {
  let opts: RecordOptions | { help: true };
  try {
    opts = parseArgs(argv);
  } catch (e) {
    console.error(e instanceof Error ? e.message : String(e));
    return 2;
  }
  if ('help' in opts) {
    console.log(USAGE);
    return 0;
  }
  if (opts.scenarios.length === 0) {
    console.error('there is no scenario to record');
    return 1;
  }
  for (const warning of untracedWarnings(opts.scenarios)) console.warn(warning);
  return record(resolveHost(), opts);
}

if (import.meta.main) process.exit(await main(process.argv.slice(2)));
