#!/usr/bin/env bun
/**
 * gui: the part of the loop that has to be clicked.
 *
 * SimHub opens a dashboard in a window from its Dash Studio page and offers no other way to do it:
 * no command line, no setting, and `SaveAndRestoreOppenedDashboards` does not bring a windowed dash
 * back after a restart, which was measured rather than assumed. So `bun run dev` drives the mouse,
 * over the same QEMU VNC that takes the screenshots.
 *
 * Coordinates are the fragile part and are treated as such. Everything anchored to the window's
 * top-left, which is the left menu, is a fixed pixel offset and holds at any resolution while the
 * guest stays at 100% DPI; everything in the centred content column is a fraction of the screen
 * width. Both were measured on the 3840 by 2160 guest. Nothing is trusted: `openDashboard` asks
 * Windows which dash windows exist afterwards, retries once, and fails with what to do by hand
 * rather than leaving the caller to wonder.
 */
import { existsSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { onHost, powershell, psq, sleep, type Host, type RunResult } from './vm.ts';

const WINVM_DIR = '/opt/winvm';
const VENV_PYTHON = `${WINVM_DIR}/mcp/.venv/bin/python`;

/** Where the left menu's entries sit, measured from the top-left of a maximised SimHub. */
const MENU = { x: 100, dashStudio: 248 } as const;
/** Where things in the centred content column sit, as a fraction of the screen width. */
const CONTENT = { searchX: 0.522, rowX: 0.383 } as const;
/**
 * The first dashboard row, and the step between rows, in pixels of a 100% DPI guest.
 *
 * `lastUsedBand` is the second first row. Dash Studio draws a "Last used" strip above the list
 * holding the dashboards recently opened, and it appears only when one of them matches what is in
 * the search box, so the list starts 221 px lower in some searches and not others. It went
 * unnoticed for as long as every search was a package's full name, which no other dashboard
 * matches; the face is now called plainly "openDash", every other package name begins with it, and
 * the band turned up.
 */
const LIST = { firstRow: 336, lastUsedBand: 221, rowHeight: 84, quickRunOffset: 86 } as const;
/**
 * SimHub 9.12.6 offers prebuilt track layouts at the top of the Dash Studio page, and the offer
 * pushes the dashboard list sixty-one pixels down. It is dismissed before anything is measured from
 * the list; clicking where "No thanks" would be costs nothing when the offer is not there, because
 * what is underneath it is the page's own background.
 */
const TRACK_LAYOUT_OFFER = { x: 0.695, y: 182 } as const;
/** How long a row is hovered before it is clicked: its Start button appears on hover, not on click. */
const HOVER_SECONDS = 2;

/** Runs a snippet against the guest's VNC through the Python environment the host already has. */
function vnc(host: Host, body: string, timeoutMs = 90_000): RunResult {
  return onHost(
    host,
    `${VENV_PYTHON} - <<'PY'
import logging, time
from vncdotool import api
logging.getLogger('vncdotool').setLevel(logging.ERROR)
client = api.connect('127.0.0.1::5900', password=None, timeout=15)
try:
${body
  .split('\n')
  .map((line) => `    ${line}`)
  .join('\n')}
finally:
    client.disconnect()
PY`,
    timeoutMs,
  );
}

/**
 * Moves the pointer and presses. `dwellSeconds` keeps the pointer there first, for the controls
 * that reveal themselves on hover -- a dashboard row's Start button, a menu item's highlight -- and
 * it has to happen inside this one VNC session: a move in one connection and a press in the next
 * arrives with the hover already forgotten.
 */
export const click = (host: Host, x: number, y: number, dwellSeconds = 0): RunResult =>
  vnc(host, `client.mouseMove(${Math.round(x)}, ${Math.round(y)})\ntime.sleep(${dwellSeconds})\nclient.mousePress(1)`);

/**
 * Types a string a key at a time. vncdotool's proxy has no `type`, and its key names are words for
 * anything that is not a bare character.
 */
export function type(host: Host, text: string): RunResult {
  const special: Record<string, string> = { ' ': 'space', '-': 'minus', '.': 'period', '_': 'underscore', '/': 'fslash' };
  const keys = [...text].map((c) => special[c] ?? c);
  return vnc(host, keys.map((k) => `client.keyPress(${JSON.stringify(k)})\ntime.sleep(0.02)`).join('\n'));
}

export const press = (host: Host, ...keys: string[]): RunResult =>
  vnc(host, keys.map((k) => `client.keyPress(${JSON.stringify(k)})\ntime.sleep(0.05)`).join('\n'));

/** The guest's display size, which the content-column coordinates are a fraction of. */
export function screenSize(host: Host): { width: number; height: number } | null {
  const r = onHost(
    host,
    `${VENV_PYTHON} - <<'PY'
import tempfile, os
from vncdotool import api
from PIL import Image
client = api.connect('127.0.0.1::5900', password=None, timeout=15)
try:
    tmp = tempfile.NamedTemporaryFile(suffix='.png', delete=False).name
    client.captureScreen(tmp)
finally:
    client.disconnect()
im = Image.open(tmp); print(im.width, im.height); os.unlink(tmp)
PY`,
    90_000,
  );
  const [w, h] = r.stdout.trim().split(/\s+/).map(Number);
  return Number.isFinite(w) && Number.isFinite(h) && w && h ? { width: w, height: h } : null;
}

let desktopCall = 0;

/**
 * Runs PowerShell in the interactive desktop and brings its output back. It has to be the desktop:
 * a session 0 process enumerates session 0's windows, which are none of these.
 */
function inDesktopScript(host: Host, script: string, timeoutSeconds = 120): RunResult {
  // A fresh name per call. Sharing one meant a second call could read the first call's output file
  // before its own task had written anything, which is how `openDashboards` once reported that the
  // open dashboard was called "maximised".
  const tag = `opendash_gui_${process.pid}_${++desktopCall}`;
  const share = `${WINVM_DIR}/shared/${tag}.ps1`;
  const out = `${WINVM_DIR}/shared/${tag}.out`;
  const done = `${WINVM_DIR}/shared/${tag}.done`;
  const written = onHost(host, `cat > ${share} <<'SCRIPT'\n${script}\nSCRIPT\nrm -f ${out} ${done}`);
  if (!written.ok) return written;
  const launched = powershell(
    host,
    `$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument ${psq(
      // The marker is written after the output file is closed, and it is the marker the host waits
      // for. Waiting for the output file itself does not work: Out-File creates it empty and fills
      // it afterwards, so a wait on existence returns nothing at all.
      `-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command "& '\\\\host.lan\\Data\\${tag}.ps1' *>&1 | Out-File -Encoding utf8 '\\\\host.lan\\Data\\${tag}.out'; 'done' | Out-File -Encoding utf8 '\\\\host.lan\\Data\\${tag}.done'"`,
    )}
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances Parallel
Register-ScheduledTask -TaskName '${tag}' -Action $action -Principal $principal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName '${tag}'
'launched'`,
    90,
  );
  if (!launched.ok) return launched;
  // The wait happens on the host, in the same command as the read. Polling from here cost one
  // SSH round trip per second, which made a handful of these calls slower than the clicking they
  // were there to verify.
  // OPENDASH_GUI_DEBUG keeps the script and its output on the share, and names them, which is the
  // only way to see why a step that runs on the far side of a scheduled task produced nothing.
  const keep = Boolean(process.env.OPENDASH_GUI_DEBUG);
  const read = onHost(
    host,
    `for i in $(seq 1 ${Math.max(1, Math.trunc(timeoutSeconds))}); do [ -f ${done} ] && break; sleep 1; done
cat ${out} 2>/dev/null | tr -d '\\000'
${keep ? `echo "[debug] kept ${share}, ${out} and ${done}" >&2` : `rm -f ${share} ${out} ${done}`}`,
    (timeoutSeconds + 30) * 1000,
  );
  if (!keep) powershell(host, `Unregister-ScheduledTask -TaskName '${tag}' -Confirm:$false -ErrorAction SilentlyContinue`, 60);
  return read;
}

/**
 * Holds a key down, screenshots the whole display while it is held, and releases it.
 *
 * All of it in one VNC session, which is the point. **A VNC server releases every held key when the
 * client disconnects**, so holding a key in one call and photographing in the next photographs a
 * key that is no longer down -- which is exactly what made the quick glance look broken when it was
 * working. The capture is the display rather than one window, because `captureDashboard` schedules
 * a task on the guest and could not run inside this session anyway.
 *
 * SimHub's keyboard reader uses RawInput, so this reaches it the way a wheel button would.
 */
export function captureWhileHeld(host: Host, key: string, localPath: string, seconds = 2.5): RunResult {
  const guestPng = `${WINVM_DIR}/shared/held_${Math.round(Date.now())}.png`;
  const held = vnc(
    host,
    `client.keyDown(${JSON.stringify(key)})
time.sleep(${seconds})
client.captureScreen(${JSON.stringify(guestPng)})
time.sleep(0.5)
client.keyUp(${JSON.stringify(key)})`,
    Math.round((seconds + 60) * 1000),
  );
  if (!held.ok) return held;
  const fetched = fetchFromShare(host, guestPng, localPath);
  return fetched.ok ? { ...fetched, stdout: `captured the display while ${key} was held` } : fetched;
}

/** A window enumerator, shared by the calls below so the P/Invoke block is written once. */
const WINDOW_HELPER = `
Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public class OpenDashWindows {
  public delegate bool Enum(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Enum cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  /// <summary>WM_CLOSE, which a WPF window handles as a click on its close box.</summary>
  public static void Close(IntPtr h) { PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero); }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  public static int[] Rect(IntPtr h) { RECT r; GetWindowRect(h, out r); return new int[] { r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top }; }
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  public static string Title(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
  public static string Cls(IntPtr h) { var sb = new StringBuilder(512); GetClassName(h, sb, 512); return sb.ToString(); }
  public static List<IntPtr> Visible() {
    var list = new List<IntPtr>();
    EnumWindows(delegate(IntPtr h, IntPtr l) { if (IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
    return list;
  }
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  public static void Move(IntPtr h, int x, int y) { SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); }
  /// <summary>Places a window so that its CLIENT area is exactly cx by cy at (x, y).</summary>
  public static void Fit(IntPtr h, int x, int y, int cx, int cy) {
    RECT w, c;
    GetWindowRect(h, out w); GetClientRect(h, out c);
    int chromeX = (w.Right - w.Left) - (c.Right - c.Left);
    int chromeY = (w.Bottom - w.Top) - (c.Bottom - c.Top);
    SetWindowPos(h, IntPtr.Zero, x, y, cx + chromeX, cy + chromeY, 0x0004 | 0x0010);
  }
}
'@
`;

/** The dashboards SimHub currently has open in a window, by name. */
export function openDashboards(host: Host): string[] {
  const r = inDesktopScript(
    host,
    `${WINDOW_HELPER}
foreach ($h in [OpenDashWindows]::Visible()) {
  $t = [OpenDashWindows]::Title($h)
  if ($t -match ' \\(WPF Renderer\\)$') { $t -replace ' \\(WPF Renderer\\)$', '' }
}`,
  );
  return r.stdout
    .split('\n')
    .map((l) => l.replace(/^﻿/, '').trim())
    .filter((l) => l.length > 0);
}

/**
 * Closes every open dash window, and returns the names it closed.
 *
 * `bun run shots` walks ten packages on a two-core VM, and a dash window left open keeps rendering
 * at sixty frames a second. Ten of them open at once is not a tidiness problem, it is why the
 * eighth capture comes back half-drawn. WM_CLOSE rather than a killed process, because SimHub owns
 * these windows and would notice.
 */
export function closeDashboards(host: Host): string[] {
  const r = inDesktopScript(
    host,
    `${WINDOW_HELPER}
foreach ($h in [OpenDashWindows]::Visible()) {
  $t = [OpenDashWindows]::Title($h)
  if ($t -notmatch ' \\(WPF Renderer\\)$') { continue }
  [OpenDashWindows]::Close($h)
  $t -replace ' \\(WPF Renderer\\)$', ''
}
Start-Sleep -Milliseconds 900`,
  );
  return r.stdout
    .split('\n')
    .map((l) => l.replace(/^﻿/, '').trim())
    .filter((l) => l.length > 0);
}

/**
 * Puts SimHub's main window where the coordinates below expect it: top-left, filling the screen.
 *
 * `ShowWindow(SW_MAXIMIZE)` is asked for first and then checked, because it does not always take:
 * a window that WPF has not finished laying out stays where it was, and every click after that
 * lands on the desktop. When the rectangle is not the whole screen, `SetWindowPos` puts it there,
 * which no window state can decline. The returned rectangle is what the caller should trust.
 */
export function maximiseSimHub(host: Host): RunResult {
  return inDesktopScript(
    host,
    `${WINDOW_HELPER}
Add-Type -AssemblyName System.Windows.Forms
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
# The process's own MainWindowHandle, not a title match. SimHub has three visible windows called
# "SimHub", one of which is a full-screen overlay, and matching on the title maximised that one
# while every click went to the real window still sitting at 78,0.
$proc = Get-Process SimHubWPF -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc -or $proc.MainWindowHandle -eq [IntPtr]::Zero) { 'SimHub main window not found'; exit }
$found = $proc.MainWindowHandle
# Anything else on the desktop takes the clicks meant for SimHub: an Explorer window left open on
# the share was doing exactly that. Only the two kinds a developer leaves lying about are
# minimised, chosen by window class rather than by title, so no shell window is touched.
foreach ($h in [OpenDashWindows]::Visible()) {
  if ($h -eq $found) { continue }
  $c = [OpenDashWindows]::Cls($h)
  if ($c -eq 'CabinetWClass' -or $c -eq 'ExploreWClass' -or $c -eq 'ConsoleWindowClass') {
    [OpenDashWindows]::ShowWindow($h, 6) | Out-Null   # SW_MINIMIZE
  }
}
[OpenDashWindows]::ShowWindow($found, 3) | Out-Null    # SW_MAXIMIZE
[OpenDashWindows]::SetForegroundWindow($found) | Out-Null
Start-Sleep -Milliseconds 700
$r = [OpenDashWindows]::Rect($found)
# A maximised window reports -8,-8 and eight pixels larger than the screen on each axis: the
# invisible resize border sits off-screen and the client area still starts at 0,0. So the test is
# whether the rectangle covers the screen, not whether it equals it. Testing for equality sent
# SetWindowPos at an already correct window, which a maximised window ignores anyway.
if ($r[0] -gt 0 -or $r[1] -gt 0 -or ($r[0] + $r[2]) -lt $screen.Width -or ($r[1] + $r[3]) -lt $screen.Height) {
  # SWP_NOZORDER | SWP_NOACTIVATE
  [OpenDashWindows]::SetWindowPos($found, [IntPtr]::Zero, 0, 0, $screen.Width, $screen.Height, 0x0004 -bor 0x0010) | Out-Null
  Start-Sleep -Milliseconds 700
  $r = [OpenDashWindows]::Rect($found)
}
"rect {0},{1} {2}x{3}" -f $r[0], $r[1], $r[2], $r[3]`,
  );
}

/**
 * Moves every open dash window, and optionally sizes one of them to the dashboard it holds.
 *
 * The size matters more than it sounds. SimHub opens a windowed dash at whatever size it last used
 * and letterboxes the dashboard inside, so a 1920 by 480 face was arriving in an 800 by 200 window
 * and would have been photographed at that size. `client` is the dashboard's own size; the window
 * is grown by its chrome so that the client area matches exactly.
 */
export function placeDashboards(host: Host, x: number, y: number, client?: { name: string; width: number; height: number }): RunResult {
  const sizing = client
    ? `
  if ($t -eq ${psq(`${client.name} (WPF Renderer)`)}) {
    [OpenDashWindows]::Fit($h, ${Math.round(x)}, ${Math.round(y)}, ${Math.round(client.width)}, ${Math.round(client.height)})
    "fitted $t to ${Math.round(client.width)}x${Math.round(client.height)}"
    continue
  }`
    : '';
  return inDesktopScript(
    host,
    `${WINDOW_HELPER}
foreach ($h in [OpenDashWindows]::Visible()) {
  $t = [OpenDashWindows]::Title($h)
  if ($t -notmatch ' \\(WPF Renderer\\)$') { continue }${sizing}
  [OpenDashWindows]::Move($h, ${Math.round(x)}, ${Math.round(y)})
  "moved $t"
}`,
  );
}

export interface OpenOptions {
  /** The package's folder name, as it appears in Dash Studio's list. */
  name: string;
  /**
   * Row of the package in the list filtered by `filter`, counting from zero. Typing a package's
   * full name narrows the list to it, so this is 0 for every package but the one called plainly
   * "openDash", whose name is a prefix of every other and which SimHub lists first anyway.
   */
  index?: number;
  /** What to type into the search box; defaults to the name. */
  filter?: string;
}

/**
 * Opens a dashboard in a window: Dash Studio, dismiss the track-layout offer, filter the list,
 * hover the row so its Start button appears, click it, then Windowed from the Quick run menu.
 *
 * Both clicks need a dwell in front of them. The Start button is revealed by the hover and not by
 * the click, and the Quick run menu highlights its item on hover before it will take a press; a
 * click sent to a coordinate the pointer has only just reached lands on neither. That is SimHub's
 * behaviour rather than a race, and it is why this hovers for two seconds twice.
 */
export function openDashboard(host: Host, opts: OpenOptions): RunResult {
  const already = openDashboards(host);
  if (already.includes(opts.name)) return { ok: true, code: 0, stdout: `${opts.name} is already open`, stderr: '' };

  const size = screenSize(host);
  if (!size) return { ok: false, code: 1, stdout: '', stderr: 'could not read the guest display size over VNC' };

  const searchX = Math.round(size.width * CONTENT.searchX);
  const rowX = Math.round(size.width * CONTENT.rowX);
  const row = opts.index ?? 0;

  // Whether the "Last used" band is there cannot be read off the screen from here, so both places
  // the first row can be are tried. The plain one first: it is the one that is right when the
  // search names a single package, which is every call but the face's.
  const offsets = [0, LIST.lastUsedBand];
  for (const [attempt, bandOffset] of offsets.map((o, i) => [i + 1, o] as const)) {
    const rowY = LIST.firstRow + bandOffset + row * LIST.rowHeight;
    maximiseSimHub(host);
    sleep(2);
    click(host, MENU.x, MENU.dashStudio);
    sleep(4);
    click(host, Math.round(size.width * TRACK_LAYOUT_OFFER.x), TRACK_LAYOUT_OFFER.y);
    sleep(2);
    click(host, searchX, LIST.firstRow - 123);
    sleep(1);
    press(host, 'ctrl-a');
    sleep(1);
    type(host, opts.filter ?? opts.name);
    sleep(3);
    // Hover, then press: the Start button is inside the row and appears only under the pointer.
    click(host, rowX, rowY, HOVER_SECONDS);
    sleep(3);
    click(host, rowX + 49, rowY + LIST.quickRunOffset, HOVER_SECONDS);
    sleep(14);
    const open = openDashboards(host);
    if (open.includes(opts.name)) {
      return { ok: true, code: 0, stdout: `opened ${opts.name}${attempt > 1 ? ` (on attempt ${attempt})` : ''}`, stderr: '' };
    }
    // A guess at the wrong offset lands on another row and opens the wrong dashboard. Close what
    // this opened before guessing again, so a failure leaves the rig as it found it.
    const strays = open.filter((n) => !already.includes(n));
    if (strays.length > 0) {
      closeDashboards(host);
      sleep(2);
    }
  }

  return {
    ok: false,
    code: 1,
    stdout: '',
    stderr:
      `could not open ${opts.name} after two attempts.\n` +
      `Opening a dashboard is the one step SimHub offers no way to script, so this clicks Dash Studio, ` +
      `filters the list and presses Start, and the coordinates it uses were measured on a 3840x2160 guest at 100% DPI. ` +
      `Open it by hand once (Dash Studio, find ${opts.name}, Start, Windowed) and run this again; everything else will be in place.`,
  };
}

/**
 * Photographs one dash window, at its own size, into a local PNG.
 *
 * `PrintWindow` with `PW_RENDERFULLCONTENT` asks the window to draw itself, so the result is the
 * dashboard rather than whatever happens to be on top of it. It only works while the window is
 * fully on screen: a window hanging off the bottom comes back with the off-screen part cut, which
 * looks exactly like a clipped glyph and wasted an afternoon. `placeDashboards` first.
 */
export function captureDashboard(host: Host, name: string, localPath: string): RunResult {
  const guestPng = `${WINVM_DIR}/shared/capture_${Math.round(Date.now())}.png`;
  const guestUnc = `\\\\host.lan\\Data\\${path.basename(guestPng)}`;
  const captured = inDesktopScript(
    host,
    `${WINDOW_HELPER}
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public class OpenDashShot {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  public static Bitmap Shoot(IntPtr h, int w, int hgt) {
    var bmp = new Bitmap(w, hgt, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp)) {
      IntPtr hdc = g.GetHdc();
      PrintWindow(h, hdc, 2);
      g.ReleaseHdc(hdc);
    }
    return bmp;
  }
}
'@ -ReferencedAssemblies System.Drawing
$target = ${psq(`${name} (WPF Renderer)`)}
foreach ($h in [OpenDashWindows]::Visible()) {
  if ([OpenDashWindows]::Title($h) -ne $target) { continue }
  $r = [OpenDashWindows]::Rect($h)
  $bmp = [OpenDashShot]::Shoot($h, $r[2], $r[3])
  # Saved to local disk and then copied. GDI+ reports success saving straight to the UNC share and
  # leaves nothing there, which is a silent way to lose every screenshot.
  $local = Join-Path $env:TEMP ${psq(path.basename(guestPng))}
  $bmp.Save($local, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Copy-Item $local ${psq(guestUnc)} -Force
  Remove-Item $local -Force -ErrorAction SilentlyContinue
  "captured {0}x{1}" -f $r[2], $r[3]
  exit
}
"no window titled $target"`,
  );
  if (!captured.ok || !captured.stdout.startsWith('captured')) {
    return { ok: false, code: 1, stdout: '', stderr: captured.stdout || captured.stderr || 'the capture produced nothing' };
  }
  const fetched = fetchFromShare(host, guestPng, localPath);
  onHost(host, `rm -f ${guestPng}`, 30_000);
  return fetched.ok ? { ...fetched, stdout: `${captured.stdout} to ${localPath}` } : fetched;
}

/** Brings a file out of the share to a local path, directly or over SSH. */
function fetchFromShare(host: Host, remotePath: string, localPath: string): RunResult {
  mkdirSync(path.dirname(path.resolve(localPath)), { recursive: true });
  if (host.local) return onHost(host, `cp '${remotePath}' '${localPath}'`);
  const r = Bun.spawnSync(['scp', '-q', '-o', 'BatchMode=yes', `${host.name}:${remotePath}`, localPath]);
  return { ok: r.exitCode === 0, code: r.exitCode, stdout: localPath, stderr: new TextDecoder().decode(r.stderr).trim() };
}

/** True when the host has the VNC tooling this module needs, which only the container host has. */
export const guiAvailable = (host: Host): boolean => (host.local ? existsSync(VENV_PYTHON) : onHost(host, `test -x ${VENV_PYTHON}`, 30_000).ok);
