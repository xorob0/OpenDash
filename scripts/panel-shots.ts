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
 * 107.5. Maximised, SimHub also centres the page in a column of its own, about 1400 px wide. So the
 * control's rectangle is measured on the guest, by its colours in a picture of SimHub's window (see
 * "measuring" below: SimHub exposes no UI Automation tree to ask), and everything is clicked and
 * cropped from that. The display's scale is
 * measured too (GetDpiForWindow) and every panel number is multiplied by it, although the VM runs
 * at 100 % and the factor is 1 there.
 *
 * The one thing it will not measure is where OpenDash sits in SimHub's left menu, which moves with
 * every plugin SimHub lists above it. `--menu-y` is that, measured by the caller from a screenshot,
 * and there is no default: a guessed y clicks another plugin's page and photographs it eight times.
 */
import { mkdirSync, renameSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { build as buildEmulator, start as startEmulator, stop as stopEmulator, upload as uploadEmulator, scenarios } from './emulator.ts';
import {
  captureWindow,
  click,
  DASH_STUDIO,
  guiProblem,
  hex,
  inDesktopScript,
  maximiseSimHub,
  movePointer,
  parseClientArea,
  sameColour,
  wheel,
  WINDOW_HELPER,
  type ClientArea,
  type Rgb,
  type ScreenRect,
} from './gui.ts';
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
//
// SimHub 9.12.6's main window exposes no UI Automation children at all: FromHandle has no
// descendants and FromPoint inside the panel returns the Window (#640). And the panel is not simply
// the client area less SimHub's menu: maximised, SimHub centres a plugin's page in a column about
// 1400 px wide with its licence offer under it. So the panel is found by its own colours, along one
// row and one column of a picture of SimHub's window. Its sidebar is painted Theme.SurfaceInset from
// top to bottom, a colour nothing of SimHub's draws, and its main column Theme.SurfaceBase; SimHub's
// page around it is #252525. The guest reports the runs of colour, and everything made of them is
// decided here, where panel-shots.test.ts can hold it to real rows read on the VM.

/** Theme.cs's surfaces the panel is found by. panel-shots.test.ts reads them out of Theme.cs. */
export const PANEL_COLOURS = {
  /** Theme.SurfaceInset: the sidebar, from the panel's top to its bottom. */
  SurfaceInset: [0x06, 0x07, 0x08],
  /** Theme.SurfaceBase: the main column behind every page. */
  SurfaceBase: [0x0a, 0x0b, 0x0d],
  /** Theme.SurfaceZone: the active sidebar item. */
  SurfaceZone: [0x14, 0x16, 0x1a],
  /** Theme.Rule: the sidebar's right-hand border. */
  Rule: [0x1c, 0x1f, 0x24],
} as const satisfies Record<string, Rgb>;

/** SimHub's page behind a plugin's, the colour every gap around the panel is. */
export const SIMHUB_PAGE: Rgb = DASH_STUDIO.page;

/**
 * The shortest run of the sidebar's colour that is the sidebar: the rail is 55 px of it at 100 %, and
 * nothing SimHub draws is that colour at all, so this only keeps a stray pixel from counting.
 */
export const MIN_SIDEBAR_RUN = 40;

/** A stretch of one colour along the row or the column the guest read, first and last inclusive, in screen pixels. */
export interface Span {
  from: number;
  to: number;
  colour: Rgb;
}

/** What one look at SimHub's window found. */
export interface PanelMeasure {
  window: ScreenRect;
  client: ScreenRect;
  /** Device-independent pixels to screen pixels: SimHub's DPI over 96. */
  scale: number;
  zoomed: boolean;
  /** The OpenDash panel, or null when SimHub is not showing it. */
  control: ScreenRect | null;
  /** How wide the panel's sidebar is drawn, its border included, in screen pixels; 0 with no panel. */
  sidebar: number;
  /** A hash of the main column's pixels, when the look was asked for one: equal hashes, the same picture. */
  signature: string | null;
  /** The colours at the points the look was asked to read, in the order asked. */
  probes: { x: number; y: number; colour: Rgb }[];
}

const isRgb = (v: unknown): v is Rgb => Array.isArray(v) && v.length === 3 && v.every((c) => Number.isInteger(c) && c >= 0 && c <= 255);

/** The spans of a `row` or `col` line, or null when the line is missing or is not a list of spans. */
export function parseSpans(stdout: string, kind: 'row' | 'col'): { at: number; spans: Span[] } | null {
  const line = stdout
    .split('\n')
    .map((l) => l.replace(/^﻿/, '').trim())
    .find((l) => l.startsWith(`${kind} `));
  if (!line) return null;
  const m = /^\w+ (-?\d+) (.*)$/.exec(line);
  if (!m) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(m[2]!);
  } catch {
    return null;
  }
  if (!Array.isArray(parsed)) return null;
  const spans: Span[] = [];
  for (const s of parsed) {
    if (!Array.isArray(s) || s.length !== 3 || !Number.isInteger(s[0]) || !Number.isInteger(s[1]) || !isRgb(s[2])) return null;
    spans.push({ from: s[0], to: s[1], colour: s[2] });
  }
  return { at: Number(m[1]), spans };
}

/**
 * The panel's rectangle and its sidebar's width, from the spans across SimHub's client area at `row`
 * and down the column the guest chose inside the sidebar; or null when no run of the sidebar's colour
 * is long enough to be one, which is SimHub showing another page.
 *
 * Left is where the sidebar's colour starts, top and bottom where it stops down the column. Right is
 * the end of the last run of the main column's colour, so a scroll bar's thumb inside the panel is
 * inside the crop. SimHub places the panel at fractional pixels, so an edge can be one pixel blended
 * half-way to SimHub's page; such a pixel is the panel's, and is counted in.
 */
export function findPanel(row: readonly Span[], rowY: number, column: readonly Span[] | null): { control: ScreenRect; sidebar: number } | null {
  const isPage = (s: Span | undefined): boolean => s === undefined || sameColour(s.colour, SIMHUB_PAGE);
  const blended = (s: Span | undefined): boolean => s !== undefined && s.from === s.to && !isPage(s);

  const at = row.findIndex((s) => sameColour(s.colour, PANEL_COLOURS.SurfaceInset) && s.to - s.from + 1 >= MIN_SIDEBAR_RUN);
  if (at < 0) return null;
  const left = blended(row[at - 1]) ? row[at]!.from - 1 : row[at]!.from;

  // The sidebar ends where the main column starts: past its border, whatever colour that is drawn.
  let after = at + 1;
  while (after < row.length && sameColour(row[after]!.colour, PANEL_COLOURS.Rule)) after++;
  const sidebar = (row[after]?.from ?? row[at]!.to + 1) - left;

  let last = -1;
  for (let i = row.length - 1; i > at; i--) {
    if (sameColour(row[i]!.colour, PANEL_COLOURS.SurfaceBase)) {
      last = i;
      break;
    }
  }
  if (last < 0) return null;
  const right = blended(row[last + 1]) ? row[last]!.to + 1 : row[last]!.to;

  if (!column) return null;
  const down = column.findIndex((s) => s.from <= rowY && rowY <= s.to);
  if (down < 0 || !sameColour(column[down]!.colour, PANEL_COLOURS.SurfaceInset)) return null;
  const top = blended(column[down - 1]) ? column[down]!.from - 1 : column[down]!.from;
  const bottom = blended(column[down + 1]) ? column[down]!.to + 1 : column[down]!.to;

  return { control: { left, top, width: right - left + 1, height: bottom - top + 1 }, sidebar };
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
  let signature: string | null = null;
  const probes: PanelMeasure['probes'] = [];
  for (const raw of stdout.split('\n')) {
    const line = raw.replace(/^﻿/, '').trim();
    const [kind, ...rest] = line.split(/\s+/);
    if (kind === 'window') window = rectOf(rest);
    else if (kind === 'client') client = rectOf(rest);
    else if (kind === 'dpi' && Number(rest[0]) > 0) dpi = Number(rest[0]);
    else if (kind === 'zoomed') zoomed = rest[0] === '1';
    else if (kind === 'signature' && /^[0-9a-f]+$/i.test(rest[0] ?? '')) signature = rest[0]!.toLowerCase();
    else if (kind === 'probe') {
      const [x, y] = rest.slice(0, 2).map(Number);
      let colour: unknown = null;
      try {
        colour = JSON.parse(rest.slice(2).join(''));
      } catch {
        colour = null;
      }
      if (Number.isFinite(x) && Number.isFinite(y) && isRgb(colour)) probes.push({ x: x!, y: y!, colour });
    }
  }
  if (!window || !client) return null;
  const row = parseSpans(stdout, 'row');
  const found = row ? findPanel(row.spans, row.at, parseSpans(stdout, 'col')?.spans ?? null) : null;
  return { window, client, scale: dpi / 96, zoomed, control: found?.control ?? null, sidebar: found?.sidebar ?? 0, signature, probes };
}

/**
 * The layout the sidebar is drawn in, read off its width: the full sidebar is 216 wide and the rail
 * 56, so anything nearer the full width is Full. Rail and Compact draw the same rail, so a rail is
 * called by the control's width. panel-shots compares this with layoutFor(control width), which is the
 * mirror's answer, and says so when they disagree.
 */
export function drawnLayout(sidebar: number, controlWidth: number, scale = 1): Layout {
  const dip = sidebar / scale;
  if (dip >= (SHELL.SidebarWidth + SHELL.RailWidth) / 2) return 'Full';
  return controlWidth / scale >= SHELL.RailFrom ? 'Rail' : 'Compact';
}

/** The page's own column: the panel right of its sidebar, which is what scrolls. */
export function mainColumn(measure: PanelMeasure): ScreenRect | null {
  const control = measure.control;
  if (!control || measure.sidebar <= 0 || measure.sidebar >= control.width) return null;
  return { left: control.left + measure.sidebar, top: control.top, width: control.width - measure.sidebar, height: control.height };
}

/**
 * The most pictures one page is taken in. A page is scrolled a viewport at a time until its column
 * stops changing, which is its foot; this is only the stop for a column that never does.
 */
export const MAX_PARTS = 8;

/** WPF's ScrollViewer: three lines of sixteen device-independent pixels a wheel notch. */
export const WHEEL_NOTCH = 48;

/** The notches that move a viewport of that height by a viewport, less one notch of overlap. */
export const notchesPerViewport = (viewportDip: number): number => Math.max(1, Math.floor(viewportDip / WHEEL_NOTCH) - 1);

/** Where to read whether a click opened its page: low in the item, below its icon and its words, on the active item's colour. */
export const activeProbe = (at: { x: number; y: number }, scale = 1): { x: number; y: number } => ({ x: at.x, y: at.y + (SHELL.NavItemHeight / 2 - 4) * scale });

/**
 * Looks at SimHub's main window: its rectangle, its client area and its DPI, and a picture of it,
 * read along the row half-way down the client area and down a column two pixels into the first run
 * of the sidebar's colour on that row. With `region`, a hash of the pixels inside it too, and with
 * `probes`, the colour at each of those points.
 *
 * It runs in the desktop session, as every window call must, and it is made DPI aware before it
 * measures, so the coordinates are the pixels a VNC click lands on. The picture is PrintWindow's, as
 * captureWindow takes it, so what is measured is what is photographed.
 */
export function measurePanel(host: Host, opts: { region?: ScreenRect | null; probes?: readonly { x: number; y: number }[] } = {}): RunResult & { measure?: PanelMeasure } {
  const region = opts.region ? [opts.region.left, opts.region.top, opts.region.width, opts.region.height].map(Math.round).join(', ') : null;
  const probes = (opts.probes ?? []).map((p) => `[OpenDashLook]::Probe($px, $w, $hgt, $r[0], $r[1], ${Math.round(p.x)}, ${Math.round(p.y)})`).join('\n');
  const [ir, ig, ib] = PANEL_COLOURS.SurfaceInset;
  const r = inDesktopScript(
    host,
    `${WINDOW_HELPER}
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices; using System.Text;
public class OpenDashLook {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  public static int[] Pixels(IntPtr h, int w, int hgt) {
    using (var bmp = new Bitmap(w, hgt, PixelFormat.Format32bppArgb)) {
      using (var g = Graphics.FromImage(bmp)) { IntPtr hdc = g.GetHdc(); PrintWindow(h, hdc, 2); g.ReleaseHdc(hdc); }
      var data = bmp.LockBits(new Rectangle(0, 0, w, hgt), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
      var px = new int[w * hgt];
      for (int y = 0; y < hgt; y++) Marshal.Copy(data.Scan0 + y * data.Stride, px, y * w, w);
      bmp.UnlockBits(data);
      return px;
    }
  }
  static string Rgb(int c) { return string.Format("[{0},{1},{2}]", (c >> 16) & 255, (c >> 8) & 255, c & 255); }
  // Runs of one colour along a row (or down a column) of the picture, in screen pixels.
  public static string Runs(int[] px, int w, int hgt, bool row, int at, int ox, int oy) {
    var sb = new StringBuilder("["); int n = row ? w : hgt; int start = 0; int last = 0; bool first = true;
    for (int i = 0; i <= n; i++) {
      int c = i < n ? (row ? px[at * w + i] : px[i * w + at]) & 0xFFFFFF : -1;
      if (i == 0) { last = c; continue; }
      if (c != last) {
        int o = row ? ox : oy;
        sb.AppendFormat("{0}[{1},{2},{3}]", first ? "" : ",", start + o, i - 1 + o, Rgb(last));
        first = false; start = i; last = c;
      }
    }
    return sb.Append("]").ToString();
  }
  static bool Near(int c, int r, int g, int b) { return Math.Abs(((c >> 16) & 255) - r) <= 2 && Math.Abs(((c >> 8) & 255) - g) <= 2 && Math.Abs((c & 255) - b) <= 2; }
  // The first x on the row where the sidebar's colour runs for at least the length asked, or -1.
  public static int FirstRun(int[] px, int w, int y, int r, int g, int b, int length) {
    int run = 0;
    for (int x = 0; x < w; x++) { if (Near(px[y * w + x], r, g, b)) { if (++run >= length) return x - length + 1; } else run = 0; }
    return -1;
  }
  // FNV-1a over every pixel of a rectangle of the screen, clipped to the picture.
  public static string Hash(int[] px, int w, int hgt, int ox, int oy, int l, int t, int rw, int rh) {
    uint h = 2166136261;
    for (int y = Math.Max(0, t - oy); y < Math.Min(hgt, t - oy + rh); y++)
      for (int x = Math.Max(0, l - ox); x < Math.Min(w, l - ox + rw); x++) { h ^= (uint)(px[y * w + x] & 0xFFFFFF); h *= 16777619; }
    return h.ToString("x8");
  }
  public static string Probe(int[] px, int w, int hgt, int ox, int oy, int x, int y) {
    int bx = x - ox, by = y - oy;
    if (bx < 0 || by < 0 || bx >= w || by >= hgt) return "";
    return string.Format("probe {0} {1} {2}", x, y, Rgb(px[by * w + bx]));
  }
}
'@ -ReferencedAssemblies System.Drawing
[OpenDashLook]::SetProcessDPIAware() | Out-Null
$proc = Get-Process SimHubWPF -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc -or $proc.MainWindowHandle -eq [IntPtr]::Zero) { 'SimHub has no main window'; exit }
$h = $proc.MainWindowHandle
$r = [OpenDashWindows]::Rect($h); "window {0} {1} {2} {3}" -f $r[0], $r[1], $r[2], $r[3]
$c = [OpenDashWindows]::Client($h); "client {0} {1} {2} {3}" -f $c[0], $c[1], $c[2], $c[3]
$dpi = 96
try { $d = [OpenDashLook]::GetDpiForWindow($h); if ($d -gt 0) { $dpi = $d } } catch { }
"dpi $dpi"
"zoomed $([int][OpenDashWindows]::IsZoomed($h))"
$w = $r[2]; $hgt = $r[3]
$px = [OpenDashLook]::Pixels($h, $w, $hgt)
# Half-way down the client area: below every sidebar item at every width, above its foot.
$y = [int]($c[1] + $c[3] / 2)
"row $y $([OpenDashLook]::Runs($px, $w, $hgt, $true, $y - $r[1], $r[0], $r[1]))"
$x = [OpenDashLook]::FirstRun($px, $w, $y - $r[1], ${ir}, ${ig}, ${ib}, ${MIN_SIDEBAR_RUN})
# Two pixels in, clear of a blended edge and left of every item's background.
if ($x -ge 0) { "col $($x + 2 + $r[0]) $([OpenDashLook]::Runs($px, $w, $hgt, $false, $x + 2, $r[0], $r[1]))" }
${region ? `"signature $([OpenDashLook]::Hash($px, $w, $hgt, $r[0], $r[1], ${region}))"` : ''}
${probes}`,
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
"client {0},{1} {2}x{3}" -f $c[0], $c[1], $c[2], $c[3]`,
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
    // Off every control, onto the taskbar's empty middle: a pointer left on the menu entry or a
    // sidebar button opens its tooltip, which SimHub's MainWindowHandle then reports as the window,
    // and every measurement and capture after it would be of a 70 x 24 popup.
    const park = (): void => void movePointer(host, client.left + client.width / 2, client.top + client.height + 20);
    park();
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
        console.error(`  SimHub is not showing OpenDash: no run of the sidebar's colour crosses its window, so the click at y ${opts.menuY} opened something else, or nothing. Take \`bun run vm shot\` and measure the entry again.`);
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
      console.log(`  SimHub ${record.simHubWidth} px, the panel ${record.controlWidth} x ${record.controlHeight} at ${control.left},${control.top}, ${layout}${measure.scale !== 1 ? ` at ${measure.scale}x` : ''}`);
      const drawn = drawnLayout(measure.sidebar, control.width, measure.scale);
      if (drawn !== layout) console.log(`  the sidebar is drawn ${measure.sidebar} px wide, which is ${drawn}, where the mirror says ${layout}; the mirror may be off`);
      const column = mainColumn(measure);
      const viewport = notchesPerViewport((column?.height ?? control.height) / measure.scale);

      for (const pageName of opts.pages) {
        const at = clickFor(pageName, control, measure.scale);
        if (!step(`  ${pageName}`, () => click(host, at.x, at.y))) {
          if (lost) return 1;
          shots.push({ file: shotFile(pageName, width), ok: false, why: 'the click failed' });
          continue;
        }
        park();
        sleep(1.5);

        // The item clicked is the open page's when it is drawn in the active item's colour.
        const probe = activeProbe(at, measure.scale);
        let look = measurePanel(host, { region: column, probes: [probe] });
        const seen = look.measure?.probes[0];
        if (seen && !sameColour(seen.colour, PANEL_COLOURS.SurfaceZone)) {
          console.log(`  ${pageName}: the item clicked at ${Math.round(at.x)},${Math.round(at.y)} is not drawn as the open one (${hex(seen.colour)} at ${seen.x},${seen.y}); the mirror may be off`);
        }

        // A viewport a picture, until the column stops changing: that is the page's foot, and the
        // picture that did not change is not taken twice.
        let signature = look.measure?.signature ?? null;
        let scrolled = 0;
        const taken: string[] = [];
        let failed: string | null = null;
        for (let part = 1; part <= MAX_PARTS; part++) {
          if (!stillOurs()) {
            console.error(`  stopped: ${lost}`);
            return 1;
          }
          if (part > 1) {
            if (!column || signature === null) break;
            wheel(host, column.left + 10 * measure.scale, column.top + column.height / 2, viewport);
            scrolled += viewport;
            park();
            sleep(1);
            look = measurePanel(host, { region: column });
            const next = look.measure?.signature ?? null;
            if (next === null || next === signature) break;
            signature = next;
          }
          const temp = `.${pageName}-${widthLabel(width)}-part${part}.png`;
          const captured = captureWindow(host, { mainWindowOf: 'SimHubWPF' }, path.join(opts.outDir, temp), control);
          if (!captured.ok) {
            failed = firstLine(captured);
            break;
          }
          taken.push(temp);
          if (part === MAX_PARTS) console.log(`    ${pageName} was still scrolling after ${MAX_PARTS} pictures; the rest of it is not photographed`);
        }
        for (const [i, temp] of taken.entries()) {
          const file = shotFile(pageName, width, i + 1, taken.length);
          renameSync(path.join(opts.outDir, temp), path.join(opts.outDir, file));
          shots.push({ file, ok: true });
          run.captures[file] = { kind: 'panel', panel: pageName, width: Math.round(control.width), height: Math.round(control.height), lapsSeen: null };
          console.log(`    ${file}: ${Math.round(control.width)}x${Math.round(control.height)}`);
        }
        if (failed !== null) {
          shots.push({ file: shotFile(pageName, width, taken.length + 1, taken.length + 1), ok: false, why: failed });
          console.log(`    ${pageName}: failed (${failed})`);
        }
        // Back to the top, so the next page is not opened scrolled and a re-run starts where this one did.
        if (scrolled > 0 && column) wheel(host, column.left + 10 * measure.scale, column.top + column.height / 2, -(scrolled + 10));
        park();
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
