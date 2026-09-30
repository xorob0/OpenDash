#!/usr/bin/env bun
/**
 * panel-shots: photograph every page of the settings panel, at every width it reads at, on one claim
 * of the VM.
 *
 * The panel is WPF inside SimHub's own window, so none of the Dash Studio dance `bun run shots` needs
 * applies: install the plugin with its left-menu entry, maximise SimHub, click the entry, and from
 * then on every page is one click on the sidebar. What makes that a script rather than a paragraph is
 * knowing where the click goes without reading the tree, and that is PanelShell's: ItemCentreX and
 * ItemCentre put each sidebar item at a function of the layout, and Layout puts the layout at a
 * function of the control's width. This file carries a mirror of those functions, and
 * panel-shots.test.ts reads PanelShell.cs, PanelNav.cs and PanelMetrics.cs as text and holds the
 * mirror to all three, the way ThemeTests holds Theme.cs to tokens.json.
 *
 * **The width is the control's, never SimHub's window's.** SimHub draws its own left menu beside the
 * plugin, so a SimHub window 1000 pixels wide gives the panel well under 1000, and the panel draws
 * its rail there: every item 63 px higher than on the full sidebar and at 27.5 across instead of
 * 107.5. So the control's rectangle is measured on the guest, by UI Automation, as the element whose
 * class is SettingsControl, and everything is clicked and cropped from that. The display's scale is
 * measured too (GetDpiForWindow) and every panel number is multiplied by it, although the VM runs
 * at 100 % and the factor is 1 there.
 *
 * The one thing it will not measure is where OpenDash sits in SimHub's left menu, which moves with
 * every plugin SimHub lists above it. `--menu-y` is that, measured by the caller from a screenshot,
 * and there is no default: a guessed y clicks another plugin's page and photographs it eight times.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { build as buildEmulator, start as startEmulator, stop as stopEmulator, upload as uploadEmulator, scenarios } from './emulator.ts';
import { captureWindow, click, guiProblem, inDesktopScript, maximiseSimHub, parseClientArea, wheel, WINDOW_HELPER, type ClientArea, type ScreenRect } from './gui.ts';
import { applyPreset, type Preset } from './rig.ts';
import { provenance, writeRun, type RunCapture } from './shotsRun.ts';
import { claim, claimLost, installPlugin, readClaim, release, resolveHost, sleep, status, up, waitReady, whoAmI, type Host, type RunResult } from './vm.ts';

const repoRoot = path.resolve(import.meta.dir, '..');

// --------------------------------------------------------------------------------- the mirror

/**
 * The numbers of PanelShell.cs and PanelMetrics.cs the sidebar's click targets are made of, under
 * their C# names. panel-shots.test.ts reads each one out of the C# and fails when they disagree.
 */
export const SHELL = {
  FullFrom: 1000,
  RailFrom: 760,
  SidebarWidth: 216,
  RailWidth: 56,
  SidebarPaddingTop: 22,
  SidebarPaddingBottom: 14,
  MarkRowHeight: 21,
  MarkRowGap: 20,
  SearchHeight: 34,
  SearchGap: 14,
  LiveCardHeight: 85,
  RailLiveHeight: 22,
  LiveCardGap: 18,
  NavItemHeight: 40,
  NavItemGap: 2,
  NavDividerHeight: 17,
} as const;

/** PanelMetrics.BorderWeight: the rule down the sidebar's right edge, which the items stop short of. */
export const BORDER_WEIGHT = 1;

/**
 * What ItemCentre adds up before the first item, in its order, full and rail: the C# expression's
 * terms, which the test reads out of the method body and compares with these.
 */
export const ITEM_TOP_TERMS = {
  full: ['SidebarPaddingTop', 'MarkRowHeight', 'MarkRowGap', 'SearchHeight', 'SearchGap', 'LiveCardHeight', 'LiveCardGap'],
  rail: ['SidebarPaddingTop', 'MarkRowHeight', 'MarkRowGap', 'SearchHeight', 'SearchGap', 'RailLiveHeight', 'LiveCardGap'],
} as const;

/** PanelPage, in the order the enum declares it: the pages as this script names them. */
export const PAGES = ['home', 'rig', 'screens', 'leds', 'matrix', 'shortcuts', 'settings', 'updates'] as const;
export type Page = (typeof PAGES)[number];

/** PanelNav.Pages: the column of items, in order. Updates is pinned to the foot and is not in it. */
export const COLUMN: readonly Page[] = ['home', 'rig', 'screens', 'leds', 'matrix', 'shortcuts', 'settings'];

/** PanelNav.GapAfter: the pages a rule follows. */
export const GAP_AFTER: ReadonlySet<Page> = new Set<Page>(['rig', 'matrix']);

/** PanelLayout. */
export type Layout = 'Full' | 'Rail' | 'Compact';

/** PanelShell.Layout: the layout a control of that width draws. */
export function layoutFor(controlWidth: number): Layout {
  if (controlWidth >= SHELL.FullFrom) return 'Full';
  return controlWidth >= SHELL.RailFrom ? 'Rail' : 'Compact';
}

/** PanelShell.SidebarWidthFor. */
export const sidebarWidthFor = (layout: Layout): number => (layout === 'Full' ? SHELL.SidebarWidth : SHELL.RailWidth);

/** PanelShell.ItemCentreX: where an item's centre sits across, from the sidebar's left edge. */
export const itemCentreX = (layout: Layout): number => (sidebarWidthFor(layout) - BORDER_WEIGHT) / 2;

/** PanelShell.ItemCentre: where the centre of the item at that place in the column sits, from the top. */
export function itemCentre(index: number, layout: Layout): number {
  if (!Number.isInteger(index) || index < 0 || index >= COLUMN.length) throw new RangeError(`${index} is not an item the sidebar lists`);
  const terms = layout === 'Full' ? ITEM_TOP_TERMS.full : ITEM_TOP_TERMS.rail;
  let y = terms.reduce((sum, term) => sum + SHELL[term], 0);
  for (let i = 0; i < index; i++) {
    y += SHELL.NavItemHeight + SHELL.NavItemGap;
    if (GAP_AFTER.has(COLUMN[i]!)) y += SHELL.NavDividerHeight + SHELL.NavItemGap;
  }
  return y + SHELL.NavItemHeight / 2;
}

/** PanelShell.UpdatesCentre: Updates is pinned to the foot, so it is measured up from the bottom. */
export const updatesCentre = (sidebarHeight: number): number => sidebarHeight - SHELL.SidebarPaddingBottom - SHELL.NavItemHeight / 2;

/**
 * Where to click for a page, in screen pixels, given the control's rectangle on the screen and the
 * display's scale. The panel's numbers are device-independent pixels, so the rectangle is divided by
 * the scale to find the layout and the height, and the offsets multiplied by it to reach the screen.
 */
export function clickFor(page: Page, control: ScreenRect, scale = 1): { x: number; y: number; layout: Layout } {
  const layout = layoutFor(control.width / scale);
  // The sidebar is the control's whole height, so the control's height is the sidebar's.
  const y = page === 'updates' ? updatesCentre(control.height / scale) : itemCentre(COLUMN.indexOf(page), layout);
  return { x: control.left + itemCentreX(layout) * scale, y: control.top + y * scale, layout };
}

// --------------------------------------------------------------------------------- the options

/** A width to photograph at: SimHub's client area this many pixels wide, or maximised. */
export type Width = number | 'max';

export const DEFAULT_WIDTHS: readonly Width[] = [700, 1000, 1600, 'max'];
export const RIG_PRESETS = ['panel', 'clear', 'empty'] as const satisfies readonly Preset[];

export interface PanelShotsOptions {
  pages: readonly Page[];
  widths: readonly Width[];
  /** Where OpenDash's entry sits in SimHub's left menu, from the top of its client area, maximised. */
  menuY: number;
  /** And across; SimHub's menu entries all sit at gui.ts's MENU.x. */
  menuX: number;
  rig: (typeof RIG_PRESETS)[number] | null;
  scenario: string | null;
  outDir: string;
  noBuild: boolean;
  keep: boolean;
}

export const USAGE = `panel-shots: photograph every page of the settings panel at every width, on one claim of the VM.

  bun run panel-shots --menu-y N [--pages a,b] [--widths 700,1000,1600,max] [--rig panel|clear|empty]
                      [--scenario race] [--out build/panel] [--menu-x 100] [--no-build] [--keep]

  --menu-y     REQUIRED. Where OpenDash's entry sits in SimHub's left menu, in pixels from the top of
               SimHub's client area with SimHub maximised, which is the screen's own y then. Take
               \`bun run vm shot\` with the plugin installed (\`bun run vm plugin --menu\`) and read it
               off, times 2.4, since the shot is scaled from 3840 to 1600 wide; it was about 431 with
               the plugins the VM has had. There is no default because it moves with every plugin
               SimHub lists above OpenDash, and a guess clicks another plugin's page and photographs that.
  --pages      comma separated; default all of ${PAGES.join(', ')}
  --widths     SimHub's client width in pixels, or max; default ${DEFAULT_WIDTHS.join(',')}. The panel's own
               width is what is measured and recorded: SimHub's menu takes its share, so 700 and 1000
               both draw the rail
  --rig        put a rig on first (bun scripts/rig.ts): panel, clear or empty
  --scenario   start the emulator on this scenario, so the live card has a session; default none
               ${scenarios().join(', ') || '(none built)'}
  --out        where the PNGs go; default build/panel
  --menu-x     across, for a SimHub whose menu is not where gui.ts measured it; default 100
  --no-build   install the plugin DLL already built rather than packaging again
  --keep       leave the VM claimed and the emulator running when this returns

It claims the VM as OPENDASH_VM_WHO, and refuses on the VM host when that is not set: every session
there is root@cumulus otherwise, and two of them would both think the claim was theirs. Every step is
gated on the claim still being this run's. A page taller than the panel is photographed in parts,
<page>-<width>-1of2.png and -2of2.png, scrolled with the mouse wheel. run.json and panel.json are
written beside the pictures: provenance, and the width SimHub and the panel actually reached.
`;

const list = (value: string): string[] => value.split(',').map((s) => s.trim()).filter((s) => s.length > 0);

/** The options, or why they cannot be run. */
export function parseArgs(argv: readonly string[]): PanelShotsOptions | { help: true } | { error: string } {
  if (argv.includes('--help') || argv.includes('-h')) return { help: true };
  const known = new Set(['--pages', '--widths', '--menu-y', '--menu-x', '--rig', '--scenario', '--out', '--no-build', '--keep']);
  const values = new Map<string, string>();
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i]!;
    const eq = arg.indexOf('=');
    const name = eq > 0 ? arg.slice(0, eq) : arg;
    if (!known.has(name)) return { error: `unknown argument: ${arg}` };
    if (name === '--no-build' || name === '--keep') {
      values.set(name, 'true');
      continue;
    }
    const value = eq > 0 ? arg.slice(eq + 1) : argv[++i];
    if (value === undefined || value.startsWith('--')) return { error: `${name} needs a value` };
    values.set(name, value);
  }

  const menuYText = values.get('--menu-y');
  if (menuYText === undefined) {
    return { error: "--menu-y is required: where OpenDash sits in SimHub's left menu moves with every plugin above it, and this will not guess it" };
  }
  const menuY = Number(menuYText);
  if (!Number.isFinite(menuY) || menuY <= 0) return { error: `--menu-y ${menuYText} is not a pixel offset` };
  const menuX = Number(values.get('--menu-x') ?? 100);
  if (!Number.isFinite(menuX) || menuX <= 0) return { error: `--menu-x ${values.get('--menu-x')} is not a pixel offset` };

  const pageNames = values.has('--pages') ? list(values.get('--pages')!) : [...PAGES];
  const unknownPages = pageNames.filter((p) => !(PAGES as readonly string[]).includes(p));
  if (unknownPages.length > 0) return { error: `unknown page${unknownPages.length > 1 ? 's' : ''} ${unknownPages.join(', ')}; one of ${PAGES.join(', ')}` };
  if (pageNames.length === 0) return { error: '--pages names no page' };

  const widths: Width[] = [];
  for (const w of values.has('--widths') ? list(values.get('--widths')!) : DEFAULT_WIDTHS.map(String)) {
    if (w === 'max') {
      widths.push('max');
      continue;
    }
    const n = Number(w);
    if (!Number.isInteger(n) || n < 400 || n > 8000) return { error: `--widths: ${w} is neither max nor a width in pixels` };
    widths.push(n);
  }
  if (widths.length === 0) return { error: '--widths names no width' };

  const rig = values.get('--rig') ?? null;
  if (rig !== null && !(RIG_PRESETS as readonly string[]).includes(rig)) return { error: `--rig ${rig}: one of ${RIG_PRESETS.join(', ')}` };

  return {
    pages: pageNames as Page[],
    widths,
    menuY,
    menuX,
    rig: rig as PanelShotsOptions['rig'],
    scenario: values.get('--scenario') ?? null,
    outDir: path.resolve(repoRoot, values.get('--out') ?? 'build/panel'),
    noBuild: values.has('--no-build'),
    keep: values.has('--keep'),
  };
}

/** What a width is called in a file name: the pixels asked for, or max. */
export const widthLabel = (width: Width): string => String(width);

/**
 * What a picture is called: the page, then the width asked for, then which part of a page taller than
 * the panel it is. The width asked for rather than the one reached, so a run repeated at the same
 * widths overwrites its own files; the one reached is in panel.json and run.json.
 */
export function shotFile(page: Page, width: Width, part = 1, parts = 1): string {
  return `${page}-${widthLabel(width)}${parts > 1 ? `-${part}of${parts}` : ''}.png`;
}

// --------------------------------------------------------------------------------- measuring

/** A scroll viewer in the panel, as UI Automation reports it. */
export interface Scroller {
  rect: ScreenRect;
  scrollable: boolean;
  /** How much of the content is in view, in percent. */
  viewSize: number;
  /** How far down it is scrolled, in percent, or -1 when it cannot scroll. */
  percent: number;
}

/** What one look at SimHub's window found. */
export interface PanelMeasure {
  window: ScreenRect;
  client: ScreenRect;
  /** Device-independent pixels to screen pixels: SimHub's DPI over 96. */
  scale: number;
  zoomed: boolean;
  /** The OpenDash control, or null when SimHub is not showing it. */
  control: ScreenRect | null;
  scrollers: Scroller[];
  /** The buttons in the sidebar's strip, by the name UI Automation gives them. */
  buttons: { rect: ScreenRect; name: string }[];
}

const rectOf = (parts: readonly string[]): ScreenRect | null => {
  const [left, top, width, height] = parts.map(Number);
  return [left, top, width, height].every((n) => Number.isFinite(n)) ? { left: left!, top: top!, width: width!, height: height! } : null;
};

/** Reads the measuring script's report, or null when it says there is no SimHub window to measure. */
export function parseMeasure(stdout: string): PanelMeasure | null {
  let window: ScreenRect | null = null;
  let client: ScreenRect | null = null;
  let dpi = 96;
  let zoomed = false;
  let control: ScreenRect | null = null;
  const scrollers: Scroller[] = [];
  const buttons: { rect: ScreenRect; name: string }[] = [];
  for (const raw of stdout.split('\n')) {
    const line = raw.replace(/^﻿/, '').trim();
    const [kind, ...rest] = line.split(/\s+/);
    if (kind === 'window') window = rectOf(rest);
    else if (kind === 'client') client = rectOf(rest);
    else if (kind === 'dpi' && Number(rest[0]) > 0) dpi = Number(rest[0]);
    else if (kind === 'zoomed') zoomed = rest[0] === '1';
    else if (kind === 'control' && rest[0] !== 'none') control = rectOf(rest);
    else if (kind === 'scroller') {
      const rect = rectOf(rest.slice(0, 4));
      const [scrollable, viewSize, percent] = rest.slice(4, 7).map(Number);
      if (rect && [scrollable, viewSize, percent].every((n) => Number.isFinite(n))) scrollers.push({ rect, scrollable: scrollable === 1, viewSize: viewSize!, percent: percent! });
    } else if (kind === 'button') {
      const rect = rectOf(rest.slice(0, 4));
      if (rect) buttons.push({ rect, name: rest.slice(4).join(' ') });
    }
  }
  if (!window || !client) return null;
  return { window, client, scale: dpi / 96, zoomed, control, scrollers, buttons };
}

/**
 * The page's own scroll viewer: the largest one right of the sidebar. The sidebar has one of its own,
 * with its bar hidden, and a page may hold small ones inside it; the page's is the one that is the
 * whole main column.
 */
export function mainColumn(measure: PanelMeasure): Scroller | null {
  const control = measure.control;
  if (!control) return null;
  const sidebarRight = control.left + sidebarWidthFor(layoutFor(control.width / measure.scale)) * measure.scale;
  const candidates = measure.scrollers.filter((s) => s.rect.left >= sidebarRight - 2 && s.rect.width > 0 && s.rect.height > 0);
  candidates.sort((a, b) => b.rect.width * b.rect.height - a.rect.width * a.rect.height);
  return candidates[0] ?? null;
}

/** How many pictures a page takes: one, or as many viewports as its content is tall. */
export function partsFor(column: Scroller | null): number {
  if (!column || !column.scrollable || column.viewSize <= 0 || column.viewSize >= 99.5) return 1;
  return Math.ceil(100 / column.viewSize - 0.005);
}

/** WPF's ScrollViewer: three lines of sixteen device-independent pixels a wheel notch. */
export const WHEEL_NOTCH = 48;

/** The notches that move a viewport of that height by a viewport, less one notch of overlap. */
export const notchesPerViewport = (viewportDip: number): number => Math.max(1, Math.floor(viewportDip / WHEEL_NOTCH) - 1);

/** The sidebar button a click lands on, or null when it lands on none. */
export function buttonAt(point: { x: number; y: number }, buttons: PanelMeasure['buttons']): string | null {
  const hit = buttons.find((b) => point.x >= b.rect.left && point.x < b.rect.left + b.rect.width && point.y >= b.rect.top && point.y < b.rect.top + b.rect.height);
  return hit ? hit.name || '(unnamed)' : null;
}

/**
 * Looks at SimHub's main window: its rectangle, its client area, its DPI, and by UI Automation the
 * OpenDash control, the scroll viewers in it and the buttons down its sidebar.
 *
 * The search is in the raw view, which holds every element with an automation peer; the control view
 * the default search walks may leave a UserControl out. It runs in the desktop session, as every
 * window call must, and it is made DPI aware before it measures, so the rectangles are the pixels a
 * VNC click lands on.
 */
export function measurePanel(host: Host): RunResult & { measure?: PanelMeasure } {
  const r = inDesktopScript(
    host,
    `${WINDOW_HELPER}
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public class OpenDashDpi {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
}
'@
[OpenDashDpi]::SetProcessDPIAware() | Out-Null
$inv = [Globalization.CultureInfo]::InvariantCulture
$proc = Get-Process SimHubWPF -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc -or $proc.MainWindowHandle -eq [IntPtr]::Zero) { 'SimHub has no main window'; exit }
$h = $proc.MainWindowHandle
"window {0} {1} {2} {3}" -f [OpenDashWindows]::Rect($h)
"client {0} {1} {2} {3}" -f [OpenDashWindows]::Client($h)
$dpi = 96
try { $d = [OpenDashDpi]::GetDpiForWindow($h); if ($d -gt 0) { $dpi = $d } } catch { }
"dpi $dpi"
"zoomed $([int][OpenDashWindows]::IsZoomed($h))"
$A = [System.Windows.Automation.AutomationElement]
$cache = New-Object System.Windows.Automation.CacheRequest
$cache.TreeFilter = [System.Windows.Automation.Automation]::RawViewCondition
$cache.AutomationElementMode = [System.Windows.Automation.AutomationElementMode]::Full
$cache.Add($A::ClassNameProperty)
$cache.Push()
try {
  function Box($e) { $b = $e.Current.BoundingRectangle; '{0} {1} {2} {3}' -f [Math]::Round($b.X), [Math]::Round($b.Y), [Math]::Round($b.Width), [Math]::Round($b.Height) }
  $root = $A::FromHandle($h)
  $all = [System.Windows.Automation.TreeScope]::Descendants
  $ctl = $root.FindFirst($all, (New-Object System.Windows.Automation.PropertyCondition($A::ClassNameProperty, 'SettingsControl')))
  if (-not $ctl) { 'control none'; exit }
  "control $(Box $ctl)"
  $cb = $ctl.Current.BoundingRectangle
  foreach ($sv in $ctl.FindAll($all, (New-Object System.Windows.Automation.PropertyCondition($A::ClassNameProperty, 'ScrollViewer')))) {
    $p = $null
    if ($sv.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$p)) {
      $s = $p.Current
      "scroller $(Box $sv) $([int]$s.VerticallyScrollable) $($s.VerticalViewSize.ToString($inv)) $($s.VerticalScrollPercent.ToString($inv))"
    }
  }
  $strip = $cb.X + ${SHELL.SidebarWidth} * $dpi / 96
  foreach ($b in $ctl.FindAll($all, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))) {
    $bb = $b.Current.BoundingRectangle
    if ($bb.Width -gt 0 -and $bb.X -lt $strip) { "button $(Box $b) $($b.Current.Name)" }
  }
} finally { $cache.Pop() }`,
    240,
  );
  if (!r.ok) return r;
  const measure = parseMeasure(r.stdout);
  if (!measure) return { ok: false, code: 1, stdout: '', stderr: r.stdout.trim() || r.stderr || 'the measurement produced nothing' };
  return { ...r, measure };
}

/**
 * Sizes SimHub's main window so its client area is that wide and as tall as the working area, at
 * the working area's top-left, or maximises it; and says the client area it got. SimHub may refuse a
 * width below its own minimum, which is why the width reached is read back rather than assumed.
 */
export function sizeSimHub(host: Host, width: Width): RunResult & { client?: ClientArea } {
  const fit =
    width === 'max'
      ? '[OpenDashWindows]::ShowWindow($h, 3) | Out-Null   # SW_MAXIMIZE'
      : `[OpenDashWindows]::ShowWindow($h, 9) | Out-Null   # SW_RESTORE
Start-Sleep -Milliseconds 700
$w = [OpenDashWindows]::Rect($h); $c = [OpenDashWindows]::Client($h)
$chromeY = $w[3] - $c[3]
[OpenDashWindows]::Fit($h, $work.X, $work.Y, ${Math.trunc(width)}, $work.Height - $chromeY)`;
  const r = inDesktopScript(
    host,
    `${WINDOW_HELPER}
Add-Type -AssemblyName System.Windows.Forms
$work = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$proc = Get-Process SimHubWPF -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc -or $proc.MainWindowHandle -eq [IntPtr]::Zero) { 'SimHub has no main window'; exit }
$h = $proc.MainWindowHandle
${fit}
[OpenDashWindows]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 900
$c = [OpenDashWindows]::Client($h)
"client {0},{1} {2}x{3}" -f $c`,
    120,
  );
  if (!r.ok) return r;
  const client = parseClientArea(r.stdout);
  return client ? { ...r, client } : { ok: false, code: 1, stdout: '', stderr: r.stdout.trim() || 'SimHub could not be sized' };
}

// --------------------------------------------------------------------------------- the run

/** What panel.json records for each width: what was asked for and what SimHub and the panel reached. */
export interface WidthRecord {
  asked: Width;
  simHubWidth: number;
  controlWidth: number;
  controlHeight: number;
  scale: number;
  layout: Layout;
}

interface Shot {
  file: string;
  ok: boolean;
  why?: string;
}

const firstLine = (r: RunResult): string => (r.stderr || r.stdout || 'no output').split('\n')[0]!.trim();

export async function panelShots(host: Host, opts: PanelShotsOptions): Promise<number> {
  if (host.local && !process.env.OPENDASH_VM_WHO) {
    console.error('Set OPENDASH_VM_WHO to a name of your own first, e.g. OPENDASH_VM_WHO=root@cumulus-503.');
    console.error('Every session on this host claims as root@cumulus otherwise, and two of them would each take the other\'s claim for their own.');
    return 1;
  }
  if (opts.scenario !== null) {
    const known = scenarios();
    if (known.length > 0 && !known.includes(opts.scenario)) {
      console.error(`unknown scenario ${opts.scenario}; one of ${known.join(', ')}`);
      return 1;
    }
  }

  const held = readClaim(host);
  if (held && held.who !== whoAmI()) {
    console.error(`the VM is claimed by ${held.who} since ${held.since}${held.note ? ` (${held.note})` : ''}`);
    console.error('There is one VM. Wait, or ask them to run `bun run vm release`.');
    return 1;
  }
  const since = new Date();
  const claimed = claim(host, `panel-shots ${opts.pages.length}x${opts.widths.length}`, since);
  if (!claimed.ok) {
    console.error(claimed.stderr);
    return 1;
  }

  // The && of a shell chain, for a run that is a sequence of remote steps: nothing runs once the
  // claim is not this run's any more, and nothing runs after a step that failed.
  let lost: string | null = null;
  const stillOurs = (): boolean => {
    lost = claimLost(readClaim(host), since.toISOString());
    return lost === null;
  };
  const step = <T extends RunResult>(what: string, run: () => T): T | null => {
    if (!stillOurs()) {
      console.error(`stopped before ${what}: ${lost}`);
      return null;
    }
    console.log(what);
    const r = run();
    if (!r.ok) {
      console.error(`  ${what} failed: ${r.stderr || r.stdout}`);
      return null;
    }
    return r;
  };

  let emulating = false;
  const shots: Shot[] = [];
  const widths: WidthRecord[] = [];
  try {
    if (!step('checking the VM is up', () => {
      if (status(host).stdout.includes('guest-ssh: up')) return { ok: true, code: 0, stdout: '', stderr: '' };
      up(host);
      return waitReady(host, 300) ? { ok: true, code: 0, stdout: '', stderr: '' } : { ok: false, code: 1, stdout: '', stderr: 'the VM did not answer within five minutes; try `bun run vm status`' };
    })) return 1;

    if (!step('checking the display can be clicked', () => {
      const problem = guiProblem(host);
      return problem ? { ok: false, code: 1, stdout: '', stderr: problem } : { ok: true, code: 0, stdout: '', stderr: '' };
    })) return 1;

    if (!step(opts.noBuild ? 'installing the plugin, with its menu entry' : 'packaging and installing the plugin, with its menu entry', () => installPlugin(host, !opts.noBuild, true))) return 1;

    // After the install, not before: installing restarts SimHub, the plugin's Init writes every folder
    // on the rig at its start, and the panel rig deletes one of them once SimHub is up.
    if (opts.rig !== null) {
      const preset = opts.rig;
      if (!step(`putting the ${preset} rig on`, () => applyPreset(host, preset))) return 1;
    }

    if (opts.scenario !== null) {
      const scenario = opts.scenario;
      if (!step(`starting the emulator on ${scenario}`, () => {
        const built = buildEmulator();
        if (!built.ok) return built;
        const uploaded = uploadEmulator(host);
        if (!uploaded.ok) return uploaded;
        const started = startEmulator(host, { scenario, replace: true });
        emulating = started.ok;
        return started;
      })) return 1;
      // SimHub takes a moment to notice the game; the live card is what this is for.
      sleep(8);
    }

    const maximised = step("waiting for SimHub's window and maximising it", () => maximiseSimHub(host));
    if (!maximised) return 1;
    const client = parseClientArea(maximised.stdout);
    if (!client) {
      console.error(`  ${maximised.stdout}`);
      return 1;
    }

    const menu = { x: client.left + opts.menuX, y: client.top + opts.menuY };
    if (!step(`opening OpenDash from the left menu at ${menu.x},${menu.y}`, () => click(host, menu.x, menu.y, 1))) return 1;
    sleep(3);

    mkdirSync(opts.outDir, { recursive: true });
    const run = { ...provenance(opts.scenario), captures: {} as Record<string, RunCapture> };

    for (const width of opts.widths) {
      const sized = step(`sizing SimHub to ${width === 'max' ? 'maximised' : `${width} px`}`, () => sizeSimHub(host, width));
      if (!sized) return 1;
      // The panel rebuilds a page after a resize settles (PanelShell.ResizeSettleMs); a second is
      // plenty for that and for SimHub to lay its own menu out again.
      sleep(2);
      const looked = step('measuring the panel', () => measurePanel(host));
      if (!looked?.measure) return 1;
      const measure = looked.measure;
      if (!measure.control) {
        console.error(`  SimHub is not showing OpenDash: the click at y ${opts.menuY} opened something else, or nothing. Take \`bun run vm shot\` and measure the entry again.`);
        return 1;
      }
      const control = measure.control;
      const layout = layoutFor(control.width / measure.scale);
      const record: WidthRecord = {
        asked: width,
        simHubWidth: sized.client?.width ?? measure.client.width,
        controlWidth: Math.round(control.width / measure.scale),
        controlHeight: Math.round(control.height / measure.scale),
        scale: measure.scale,
        layout,
      };
      widths.push(record);
      console.log(`  SimHub ${record.simHubWidth} px, the panel ${record.controlWidth} x ${record.controlHeight}, ${layout}${measure.scale !== 1 ? ` at ${measure.scale}x` : ''}`);

      for (const pageName of opts.pages) {
        const at = clickFor(pageName, control, measure.scale);
        const landsOn = buttonAt(at, measure.buttons);
        if (measure.buttons.length > 0 && landsOn === null) console.log(`  ${pageName}: the click at ${Math.round(at.x)},${Math.round(at.y)} is on no sidebar button UI Automation reports; the mirror may be off`);
        if (!step(`  ${pageName}${landsOn ? ` (${landsOn})` : ''}`, () => click(host, at.x, at.y))) {
          if (lost) return 1;
          shots.push({ file: shotFile(pageName, width), ok: false, why: 'the click failed' });
          continue;
        }
        sleep(1.5);

        const after = measurePanel(host);
        const column = after.measure ? mainColumn(after.measure) : null;
        const parts = partsFor(column);
        const crop = after.measure?.control ?? control;
        for (let part = 1; part <= parts; part++) {
          if (!stillOurs()) {
            console.error(`  stopped: ${lost}`);
            return 1;
          }
          if (part > 1 && column) {
            const viewport = column.rect.height / measure.scale;
            // The last part is scrolled to the very bottom, so it ends where the page ends.
            const notches = part === parts ? notchesPerViewport(viewport) * parts + 10 : notchesPerViewport(viewport);
            wheel(host, column.rect.left + 10 * measure.scale, column.rect.top + column.rect.height / 2, notches);
            sleep(1);
          }
          const file = shotFile(pageName, width, part, parts);
          const captured = captureWindow(host, { mainWindowOf: 'SimHubWPF' }, path.join(opts.outDir, file), crop);
          shots.push({ file, ok: captured.ok, why: captured.ok ? undefined : firstLine(captured) });
          console.log(`    ${file}: ${captured.ok ? captured.stdout.split(' to ')[0] : `failed (${firstLine(captured)})`}`);
          if (captured.ok) run.captures[file] = { kind: 'panel', panel: pageName, width: Math.round(crop.width), height: Math.round(crop.height), lapsSeen: null };
        }
        // Back to the top, so the next page is not opened scrolled and a re-run starts where this one did.
        if (parts > 1 && column) wheel(host, column.rect.left + 10 * measure.scale, column.rect.top + column.rect.height / 2, -(notchesPerViewport(column.rect.height / measure.scale) * parts + 10));
      }
      // Written after every width, so a run that stops short still says what it photographed.
      writeRun(opts.outDir, run);
      writeFileSync(path.join(opts.outDir, 'panel.json'), `${JSON.stringify({ menuY: opts.menuY, widths }, null, 2)}\n`);
    }

    const good = shots.filter((s) => s.ok);
    console.log('');
    console.log(`  ${good.length} of ${shots.length} photographed into ${path.relative(repoRoot, opts.outDir)}/`);
    for (const s of shots.filter((x) => !x.ok)) console.log(`  missing  ${s.file}: ${s.why ?? 'unknown'}`);
    for (const w of widths) console.log(`  asked ${String(w.asked).padEnd(5)} SimHub ${w.simHubWidth} px, panel ${w.controlWidth} px, ${w.layout}`);
    console.log('  a capture worth showing belongs in media/<issue>/; this directory is scratch');
    return good.length === shots.length ? 0 : 1;
  } finally {
    // Nothing is clicked or restarted in a guest that now belongs to somebody else.
    if (!lost) {
      const restored = maximiseSimHub(host);
      if (!parseClientArea(restored.stdout)) console.error(`  could not maximise SimHub again: ${firstLine(restored)}`);
      if (emulating && !opts.keep) stopEmulator(host);
    }
    if (!opts.keep) release(host);
  }
}

if (import.meta.main) {
  const opts = parseArgs(process.argv.slice(2));
  if ('help' in opts) {
    console.log(USAGE);
  } else if ('error' in opts) {
    console.error(`${opts.error}\n\n${USAGE}`);
    process.exit(2);
  } else {
    process.exit(await panelShots(resolveHost(), opts));
  }
}
