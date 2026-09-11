#!/usr/bin/env bun
/**
 * vm: drives the Windows test VM and the SimHub running on it, so that the loop in
 * docs/testing-vm.md is a command rather than a paragraph of instructions.
 *
 * Where the VM is. It lives in a container on one host, and everything reaches it through that
 * host: `/opt/winvm/ssh/id_ed25519` opens an SSH session into Windows on 127.0.0.1:2222, and
 * 127.0.0.1:5900 is QEMU's own VNC, which is how a screenshot is taken whatever state Windows is
 * in. A session running on that host therefore talks to the VM directly, and a session anywhere
 * else tunnels every command through SSH to it. `resolveHost` picks between the two, so the same
 * script works on the host and on a laptop.
 *
 * Two traps this exists to close, both documented in tools/irsdk-emulator/README.md and both
 * silent when hit. A command sent over SSH lands in session 0, which has no desktop: anything
 * with a window started there is invisible to SimHub and to a screenshot, so `desktop` goes
 * through a scheduled task instead. And SimHub reads the template list once at startup, so a
 * package expanded into DashTemplates is not seen until SimHub is restarted, which `install`
 * does rather than leaving it to be remembered.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync } from 'node:fs';
import path from 'node:path';

/** Where the VM's files live on whichever host runs the container. */
const WINVM_DIR = '/opt/winvm';
/** Guest SSH and QEMU VNC, both published on the container host's loopback only. */
const GUEST_SSH_PORT = 2222;
const SIMHUB_DIR = 'C:\\Program Files (x86)\\SimHub';
const DASH_TEMPLATES = `${SIMHUB_DIR}\\DashTemplates`;
/** The share that is `Z:\` in the interactive session and `\\host.lan\Data` from an SSH one. */
const SHARE_UNC = '\\\\host.lan\\Data';
const LOCK_PATH = `${WINVM_DIR}/shared/vm.lock`;
/** A claim older than this is treated as abandoned: a session that dies never releases its lock. */
const LOCK_STALE_MINUTES = 90;

const repoRoot = path.resolve(import.meta.dir, '..');

// --------------------------------------------------------------------------------- the host

export interface Host {
  /** True when this process is already on the machine that runs the container. */
  local: boolean;
  /** Human-readable name, for messages. */
  name: string;
}

/**
 * The host the VM runs on. `/opt/winvm` present means this process is already there; otherwise
 * OPENDASH_VM_HOST names the SSH host to tunnel through, defaulting to the one the project uses.
 */
export function resolveHost(): Host {
  if (existsSync(WINVM_DIR)) return { local: true, name: 'localhost' };
  const name = process.env.OPENDASH_VM_HOST ?? 'cumulus';
  return { local: false, name: `${process.env.OPENDASH_VM_USER ?? 'root'}@${name}` };
}

export interface RunResult {
  ok: boolean;
  code: number;
  stdout: string;
  stderr: string;
}

/** Runs a shell command on the container host, directly or over SSH. */
export function onHost(host: Host, command: string, timeoutMs = 180_000): RunResult {
  const argv = host.local
    ? ['bash', '-lc', command]
    : ['ssh', '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10', host.name, command];
  const r = spawnSync(argv[0]!, argv.slice(1), { encoding: 'utf8', timeout: timeoutMs, maxBuffer: 64 * 1024 * 1024 });
  return {
    ok: r.status === 0,
    code: r.status ?? -1,
    stdout: (r.stdout ?? '').trim(),
    stderr: (r.stderr ?? '').trim(),
  };
}

/** Copies a local file to the container host's shared folder, which Windows sees as `Z:\`. */
export function toShare(host: Host, localPath: string, name = path.basename(localPath)): RunResult {
  const dest = `${WINVM_DIR}/shared/${name}`;
  if (host.local) return onHost(host, `cp ${shq(localPath)} ${shq(dest)}`);
  const r = spawnSync('scp', ['-q', '-o', 'BatchMode=yes', localPath, `${host.name}:${dest}`], { encoding: 'utf8', timeout: 900_000 });
  return { ok: r.status === 0, code: r.status ?? -1, stdout: '', stderr: (r.stderr ?? '').trim() };
}

/** Copies a file out of the container host's shared folder to a local path. */
export function fromShare(host: Host, name: string, localPath: string): RunResult {
  const src = `${WINVM_DIR}/shared/${name}`;
  mkdirSync(path.dirname(localPath), { recursive: true });
  if (host.local) return onHost(host, `cp ${shq(src)} ${shq(localPath)}`);
  const r = spawnSync('scp', ['-q', '-o', 'BatchMode=yes', `${host.name}:${src}`, localPath], { encoding: 'utf8', timeout: 900_000 });
  return { ok: r.status === 0, code: r.status ?? -1, stdout: '', stderr: (r.stderr ?? '').trim() };
}

/** Single-quotes a string for a POSIX shell. */
export const shq = (s: string): string => `'${s.replaceAll("'", `'\\''`)}'`;

// --------------------------------------------------------------------------------- the guest

/**
 * Runs PowerShell inside Windows over the guest's SSH, base64 encoded so that no quoting survives
 * the three shells the script passes through. Session 0: a window opened here is invisible.
 */
export function powershell(host: Host, script: string, timeoutSeconds = 180): RunResult {
  const encoded = Buffer.from(script, 'utf16le').toString('base64');
  const inner = [
    'ssh',
    '-i', `${WINVM_DIR}/ssh/id_ed25519`,
    '-p', String(GUEST_SSH_PORT),
    '-o', 'StrictHostKeyChecking=no',
    '-o', 'UserKnownHostsFile=/dev/null',
    '-o', 'LogLevel=ERROR',
    '-o', 'BatchMode=yes',
    '-o', 'ConnectTimeout=10',
    // The guest's admin user is whatever the container was provisioned with, so it is read from
    // the compose environment file rather than assumed.
    `"$(grep -m1 '^WIN_USERNAME=' ${WINVM_DIR}/.env | cut -d= -f2)"@127.0.0.1`,
    `powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -OutputFormat Text -EncodedCommand ${encoded}`,
  ].join(' ');
  const r = onHost(host, inner, timeoutSeconds * 1000);
  return { ...r, stderr: cleanClixml(r.stderr) };
}

/**
 * PowerShell over SSH writes CLIXML on stderr, progress records included, so a successful command
 * still produces noise. Only the error records are worth showing.
 */
export function cleanClixml(text: string): string {
  const t = text.trim();
  if (!t.startsWith('#< CLIXML')) return t;
  const errors = [...t.matchAll(/<S S="Error">(.*?)<\/S>/gs)].map((m) =>
    (m[1] ?? '').replaceAll('_x000D__x000A_', '\n').replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&amp;', '&'),
  );
  return errors.join('\n').trim();
}

/** Quotes a string for PowerShell. */
export const psq = (s: string): string => `'${s.replaceAll("'", "''")}'`;

/**
 * Launches a program into the logged-in desktop through a scheduled task, which is the standard
 * way out of session 0. Returns as soon as the task is started, not when the program exits.
 */
export function inDesktop(host: Host, command: string, args = '', workingDir = ''): RunResult {
  const argument = args ? ` -Argument ${psq(args)}` : '';
  const cwd = workingDir ? ` -WorkingDirectory ${psq(workingDir)}` : '';
  return powershell(
    host,
    `
$action = New-ScheduledTaskAction -Execute ${psq(command)}${argument}${cwd}
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances Parallel
Register-ScheduledTask -TaskName 'opendash_desktop' -Action $action -Principal $principal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName 'opendash_desktop'
Start-Sleep -Milliseconds 800
'launched'
`,
    90,
  );
}

// --------------------------------------------------------------------------------- lifecycle

export function status(host: Host): RunResult {
  return onHost(
    host,
    `docker inspect -f '{{.State.Status}} since {{.State.StartedAt}}' winvm 2>/dev/null || echo 'no container';
     (echo > /dev/tcp/127.0.0.1/${GUEST_SSH_PORT}) 2>/dev/null && echo 'guest-ssh: up' || echo 'guest-ssh: down';
     (echo > /dev/tcp/127.0.0.1/5900) 2>/dev/null && echo 'vnc: up' || echo 'vnc: down'`,
    60_000,
  );
}

export const up = (host: Host): RunResult => onHost(host, `cd ${WINVM_DIR} && docker compose up -d`, 300_000);
export const down = (host: Host): RunResult => onHost(host, `cd ${WINVM_DIR} && docker compose stop`, 300_000);

/** Blocks until the guest answers over SSH. A cold start takes about two minutes. */
export function waitReady(host: Host, timeoutSeconds = 300): boolean {
  const deadline = Date.now() + timeoutSeconds * 1000;
  while (Date.now() < deadline) {
    const r = powershell(host, `'ready'`, 30);
    if (r.ok && r.stdout.includes('ready')) return true;
    sleep(5);
  }
  return false;
}

/** Blocking sleep; the script is a sequence of slow remote steps and has nothing else to do. */
export function sleep(seconds: number): void {
  spawnSync('sleep', [String(seconds)]);
}

// --------------------------------------------------------------------------------- SimHub

export const simhubStop = (host: Host): RunResult =>
  powershell(
    host,
    `$p = Get-Process SimHubWPF -ErrorAction SilentlyContinue
if (-not $p) { 'not running'; exit 0 }
$p | Stop-Process -Force
Start-Sleep -Seconds 3
'stopped'`,
    90,
  );

export function simhubStart(host: Host, waitSeconds = 40): RunResult {
  return powershell(
    host,
    `if (Get-Process SimHubWPF -ErrorAction SilentlyContinue) { 'already running'; exit 0 }
Start-ScheduledTask -TaskName 'SimHub'
$deadline = (Get-Date).AddSeconds(${waitSeconds})
while ((Get-Date) -lt $deadline) {
  $p = Get-Process SimHubWPF -ErrorAction SilentlyContinue
  if ($p) { "started (pid $($p.Id))"; exit 0 }
  Start-Sleep 1
}
'SimHub did not appear; check a screenshot'`,
    waitSeconds + 60,
  );
}

/**
 * Tails the log SimHub is writing now.
 *
 * Not the newest by modification time: the guest clock drifts, so a rotated file can carry a
 * later timestamp than the live one, and sorting by `LastWriteTime` picks a log from hours ago.
 * SimHub's own convention settles it instead. `SimHub.txt` is the current log and `SimHub.N.txt`
 * are the rotations, N growing with age, so the current log is `SimHub.txt` when it exists and
 * the lowest N otherwise. Its `Length` reads 0 while the process holds it open, which is why the
 * choice cannot be made on size either.
 */
export const simhubLogs = (host: Host, lines: number): RunResult =>
  powershell(
    host,
    `$dir = Join-Path ${psq(SIMHUB_DIR)} 'Logs'
$log = Get-Item (Join-Path $dir 'SimHub.txt') -ErrorAction SilentlyContinue
if (-not $log) {
  $log = Get-ChildItem $dir -Filter 'SimHub.*.txt' -ErrorAction SilentlyContinue |
    Sort-Object { [int]($_.Name -replace '^SimHub\.(\d+)\.txt$', '$1') } |
    Select-Object -First 1
}
if (-not $log) { 'no SimHub log yet'; exit 0 }
"== $($log.FullName) =="
Get-Content $log.FullName -Tail ${Math.max(1, Math.trunc(lines))}`,
    120,
  );

/**
 * Expands the named packages into DashTemplates and restarts SimHub so the template list is
 * rescanned. Reports what reached the guest, because a silent copy to the wrong place is the
 * failure this command exists to prevent.
 */
export function install(host: Host, packages: readonly string[]): RunResult {
  const buildDir = path.join(repoRoot, 'build');
  if (!existsSync(buildDir)) return { ok: false, code: 1, stdout: '', stderr: 'build/ does not exist; run `bun run build` first' };

  const wanted = packages.length > 0 ? packages : ['*'];
  const localFiles = new Set<string>();
  for (const pattern of wanted) {
    const glob = new Bun.Glob(`${pattern}.simhubdash`);
    for (const file of glob.scanSync({ cwd: buildDir })) localFiles.add(file);
  }
  if (localFiles.size === 0) return { ok: false, code: 1, stdout: '', stderr: `no package in build/ matched ${wanted.join(', ')}` };

  onHost(host, `mkdir -p ${WINVM_DIR}/shared/opendash`);
  for (const file of localFiles) {
    const r = toShare(host, path.join(buildDir, file), `opendash/${file}`);
    if (!r.ok) return r;
  }

  simhubStop(host);
  const names = [...localFiles].map((f) => f.replace(/\.simhubdash$/, ''));
  const expand = powershell(
    host,
    `$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$dt = ${psq(DASH_TEMPLATES)}
$installed = @()
foreach ($name in @(${names.map(psq).join(', ')})) {
  $src = Join-Path ${psq(`${SHARE_UNC}\\opendash`)} "$name.simhubdash"
  if (-not (Test-Path $src)) { throw "missing on the share: $src" }
  $target = Join-Path $dt $name
  if (Test-Path $target) { Remove-Item $target -Recurse -Force }
  # Expanding straight off the share is slow and occasionally faults, so it goes via TEMP.
  $tmp = Join-Path $env:TEMP "$name.simhubdash"
  Copy-Item $src $tmp -Force
  [IO.Compression.ZipFile]::ExtractToDirectory($tmp, $dt)
  Remove-Item $tmp -Force
  if (-not (Test-Path (Join-Path $target "$name.djson"))) { throw "expanded but no .djson under $target" }
  $installed += $name
}
# SimHub resolves a bundled font from DashFonts, not from the package, so they are copied too.
$fonts = Join-Path ${psq(SIMHUB_DIR)} 'DashFonts'
New-Item -ItemType Directory -Force -Path $fonts | Out-Null
Get-ChildItem (Join-Path $dt 'openDash*\\_SHFonts\\*.ttf') -ErrorAction SilentlyContinue | ForEach-Object { Copy-Item $_.FullName $fonts -Force }
"installed: " + ($installed -join ', ')`,
    300,
  );
  if (!expand.ok) return expand;
  const started = simhubStart(host);
  return { ...expand, stdout: `${expand.stdout}\n${started.stdout}` };
}

/**
 * Puts the current plugin on the VM: builds it, copies the DLL in, unblocks it, pre-activates it
 * and restarts SimHub.
 *
 * The build goes through `bun run package` rather than `dotnet build`, because the plugin embeds
 * the packages found in `Resources/` and that folder is gitignored build output. Building the DLL
 * alone would install a plugin carrying whatever packages happened to be lying there, which is the
 * one failure mode nobody notices: the plugin loads, the panel works, and the dashboard it installs
 * is last week's. Pass `--no-build` when the DLL is known to be current.
 */
export function installPlugin(host: Host, build = true): RunResult {
  const dll = path.join(repoRoot, 'plugin/OpenDash/bin/Release/net48/OpenDash.dll');
  if (build || !existsSync(dll)) {
    const packaged = spawnSync('bash', ['scripts/package.sh'], { cwd: repoRoot, encoding: 'utf8', timeout: 900_000 });
    if (packaged.status !== 0) {
      return { ok: false, code: packaged.status ?? -1, stdout: packaged.stdout ?? '', stderr: (packaged.stderr ?? '').trim() || 'packaging failed' };
    }
  }
  if (!existsSync(dll)) return { ok: false, code: 1, stdout: '', stderr: `no plugin at ${dll}` };
  simhubStop(host);
  const sent = toShare(host, dll, 'OpenDash.dll');
  if (!sent.ok) return sent;
  const copy = powershell(
    host,
    `$dest = Join-Path ${psq(SIMHUB_DIR)} 'OpenDash.dll'
Copy-Item ${psq(`${SHARE_UNC}\\OpenDash.dll`)} $dest -Force
# Windows marks a file that arrived over a network share, and .NET refuses to load it silently.
Unblock-File -LiteralPath $dest -ErrorAction SilentlyContinue
# Pre-activating spares the "new plugin found" prompt, which blocks the desktop until clicked.
$activation = Join-Path ${psq(SIMHUB_DIR)} 'PluginsData\\PluginsActivation.json'
if (Test-Path $activation) {
  $json = Get-Content $activation -Raw | ConvertFrom-Json
  if (-not $json.'OpenDash.OpenDashPlugin') {
    $json | Add-Member -NotePropertyName 'OpenDash.OpenDashPlugin' -NotePropertyValue $true -Force
    $json | ConvertTo-Json -Depth 10 | Set-Content $activation -Encoding UTF8
  }
}
"installed $((Get-Item $dest).Length) bytes"`,
    180,
  );
  if (!copy.ok) return copy;
  const started = simhubStart(host);
  return { ...copy, stdout: `${copy.stdout}\n${started.stdout}` };
}

// --------------------------------------------------------------------------------- screenshot

/**
 * A screenshot of the whole VM display, taken through QEMU's VNC so that it works whatever
 * Windows is doing, installation included.
 */
export function screenshot(host: Host, localPath: string, maxWidth = 1600): RunResult {
  const remote = '/tmp/opendash-shot.png';
  const capture = onHost(
    host,
    `${WINVM_DIR}/mcp/.venv/bin/python - <<'PY'
import logging, tempfile, os
from vncdotool import api
from PIL import Image
logging.getLogger('vncdotool').setLevel(logging.ERROR)
client = api.connect('127.0.0.1::5900', password=None, timeout=15)
try:
    tmp = tempfile.NamedTemporaryFile(suffix='.png', delete=False).name
    client.captureScreen(tmp)
finally:
    client.disconnect()
im = Image.open(tmp); im.load(); os.unlink(tmp)
if ${Math.trunc(maxWidth)} and im.width > ${Math.trunc(maxWidth)}:
    im = im.resize((${Math.trunc(maxWidth)}, round(im.height * ${Math.trunc(maxWidth)} / im.width)), Image.LANCZOS)
im.convert('RGB').save('${remote}', format='PNG', optimize=True)
print('${remote}')
PY`,
    120_000,
  );
  if (!capture.ok) return capture;
  mkdirSync(path.dirname(path.resolve(localPath)), { recursive: true });
  if (host.local) return onHost(host, `cp ${shq(remote)} ${shq(localPath)}`);
  const r = spawnSync('scp', ['-q', '-o', 'BatchMode=yes', `${host.name}:${remote}`, localPath], { encoding: 'utf8', timeout: 120_000 });
  return { ok: r.status === 0, code: r.status ?? -1, stdout: localPath, stderr: (r.stderr ?? '').trim() };
}

// --------------------------------------------------------------------------------- the lock

export interface Claim {
  who: string;
  since: string;
  note: string;
}

/** Who is currently using the VM, or null when nobody is. A stale claim counts as nobody. */
export function readClaim(host: Host, now = new Date()): Claim | null {
  const r = onHost(host, `cat ${shq(LOCK_PATH)} 2>/dev/null || true`, 60_000);
  if (!r.stdout) return null;
  let claim: Claim;
  try {
    claim = JSON.parse(r.stdout) as Claim;
  } catch {
    return null;
  }
  const age = (now.getTime() - Date.parse(claim.since)) / 60_000;
  if (!Number.isFinite(age) || age > LOCK_STALE_MINUTES) return null;
  return claim;
}

/** Who this session is, for the lock: whatever identifies the machine and the user running it. */
export const whoAmI = (): string => process.env.OPENDASH_VM_WHO ?? `${process.env.USER ?? 'someone'}@${process.env.HOSTNAME ?? hostname()}`;

function hostname(): string {
  const r = spawnSync('hostname', { encoding: 'utf8' });
  return (r.stdout ?? 'unknown').trim();
}

/**
 * Takes the lock, unless somebody else holds a fresh one. Re-claiming your own is allowed, so a
 * command that claims can be run twice without a release in between.
 */
export function claim(host: Host, note = '', now = new Date()): RunResult {
  const held = readClaim(host, now);
  const me = whoAmI();
  if (held && held.who !== me) {
    return { ok: false, code: 1, stdout: '', stderr: `the VM is claimed by ${held.who} since ${held.since}${held.note ? ` (${held.note})` : ''}` };
  }
  const body = JSON.stringify({ who: me, since: now.toISOString(), note } satisfies Claim);
  const write = onHost(host, `mkdir -p ${WINVM_DIR}/shared && cat > ${shq(LOCK_PATH)} <<'LOCK'\n${body}\nLOCK`, 60_000);
  return write.ok ? { ...write, stdout: `claimed by ${me}` } : write;
}

/** Gives the lock back. Releasing a lock somebody else holds is refused rather than silent. */
export function release(host: Host, now = new Date()): RunResult {
  const held = readClaim(host, now);
  const me = whoAmI();
  if (held && held.who !== me) return { ok: false, code: 1, stdout: '', stderr: `the VM is claimed by ${held.who}, not by you` };
  const r = onHost(host, `rm -f ${shq(LOCK_PATH)}`, 60_000);
  return r.ok ? { ...r, stdout: 'released' } : r;
}

// --------------------------------------------------------------------------------- the CLI

const USAGE = `vm: drive the Windows test VM and its SimHub.

  bun run vm status                 container, guest SSH and VNC
  bun run vm up                     start the container
  bun run vm down                   shut Windows down and free the host's RAM
  bun run vm wait [seconds]         block until the guest answers (a cold start is ~2 min)

  bun run vm install [package...]   expand built packages into DashTemplates, restart SimHub
                                    names are globs over build/, e.g. 'openDash', 'openDash 8*'
  bun run vm plugin [--no-build]    package, install OpenDash.dll, restart SimHub
                                    packaging is what embeds the current dashboards in the plugin

  bun run vm logs [n]               tail SimHub's log
  bun run vm shot [file]            screenshot the VM display (default: build/vm.png)

  bun run vm claim [note]           take the VM (there is one, and two sessions will fight)
  bun run vm release                give it back
  bun run vm who                    who holds it

The host is found automatically: this machine when /opt/winvm exists, otherwise the SSH host in
OPENDASH_VM_HOST (default "cumulus").
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
    case 'status':
      return report(status(host));
    case 'up':
      return report(up(host));
    case 'down':
      return report(down(host));
    case 'wait': {
      const seconds = Number(rest[0] ?? 300);
      const ready = waitReady(host, seconds);
      return report({ ok: ready, code: ready ? 0 : 1, stdout: ready ? 'ready' : '', stderr: ready ? '' : `not ready after ${seconds}s` });
    }
    case 'install':
      return report(install(host, rest));
    case 'plugin':
      return report(installPlugin(host, !rest.includes('--no-build')));
    case 'logs':
      return report(simhubLogs(host, Number(rest[0] ?? 80)));
    case 'shot': {
      const out = rest[0] ?? path.join(repoRoot, 'build/vm.png');
      return report(screenshot(host, out));
    }
    case 'claim':
      return report(claim(host, rest.join(' ')));
    case 'release':
      return report(release(host));
    case 'who': {
      const held = readClaim(host);
      return report({ ok: true, code: 0, stdout: held ? `${held.who} since ${held.since}${held.note ? ` (${held.note})` : ''}` : 'nobody', stderr: '' });
    }
    default:
      console.error(`unknown command: ${command}\n\n${USAGE}`);
      process.exit(2);
  }
}

if (import.meta.main) await main(process.argv.slice(2));
