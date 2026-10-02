/**
 * What `scripts/panel-shots.ts` decides without a VM: that its mirror of the panel's sidebar is the
 * panel's, where each page is clicked at each width, what the pictures are called, and how the
 * command line and the guest's measurements are read: the panel found by its colours in a picture
 * of SimHub's window, held here to rows read on the VM.
 *
 * The mirror is held to the C# the way ThemeTests holds Theme.cs to tokens.json: PanelShell.cs,
 * PanelNav.cs and PanelMetrics.cs are read as text, every constant the mirror carries is evaluated
 * out of them, and the bodies of the functions it mirrors are compared with the ones it was written
 * from. A change to any of the three fails here, and says which number or which function moved.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import {
  activeProbe,
  BORDER_WEIGHT,
  clickFor,
  COLUMN,
  drawnLayout,
  findPanel,
  GAP_AFTER,
  ITEM_TOP_TERMS,
  itemCentre,
  itemCentreX,
  layoutFor,
  mainColumn,
  MAX_PARTS,
  MIN_SIDEBAR_RUN,
  notchesPerViewport,
  PAGES,
  PANEL_COLOURS,
  parseArgs,
  parseMeasure,
  parseSpans,
  SHELL,
  shotFile,
  sidebarWidthFor,
  SIMHUB_PAGE,
  updatesCentre,
  USAGE,
  type PanelShotsOptions,
} from './panel-shots.ts';
import { hex } from './gui.ts';

const plugin = path.resolve(import.meta.dir, '..', 'plugin', 'OpenDash');
/**
 * A C# source as code alone, as RepoPaths.Code reads it for the plugin's tests: its // and /* *\/ comments
 * dropped, its string and character literals kept whole, so a constant named in a comment never satisfies a
 * pin on the code.
 */
function code(source: string): string {
  let out = '';
  let i = 0;
  while (i < source.length) {
    const c = source[i]!;
    const next = source[i + 1];
    if (c === '/' && next === '/') {
      while (i < source.length && source[i] !== '\n') i++;
      continue;
    }
    if (c === '/' && next === '*') {
      const end = source.indexOf('*/', i + 2);
      i = end < 0 ? source.length : end + 2;
      out += ' ';
      continue;
    }
    if (c === '"' || c === "'") {
      const start = i++;
      const verbatim = c === '"' && source[start - 1] === '@';
      while (i < source.length && source[i] !== c && (verbatim || source[i] !== '\n')) i += !verbatim && source[i] === '\\' ? 2 : 1;
      i = Math.min(source.length, i + 1);
      out += source.slice(start, i);
      continue;
    }
    out += c;
    i++;
  }
  return out;
}

const read = (file: string): string => code(readFileSync(path.join(plugin, file), 'utf8'));
const shell = read('PanelShell.cs');
const nav = read('PanelNav.cs');
const metrics = read('PanelMetrics.cs');

/** Every `public const double` of a C# file, with its expression. */
const constants = (text: string): Map<string, string> => new Map([...text.matchAll(/public const double (\w+) = ([^;]+);/g)].map((m) => [m[1]!, m[2]!.trim()]));

/**
 * The value of a constant, evaluating its expression: numbers, arithmetic, the file's own constants
 * and PanelMetrics's by their qualified name. Anything else is refused rather than guessed at.
 */
function evaluate(name: string, own: Map<string, string>, depth = 0): number {
  if (depth > 10) throw new Error(`${name} refers to itself`);
  const expression = own.get(name);
  if (expression === undefined) throw new Error(`no public const double ${name}`);
  const substituted = expression.replace(/\b(PanelMetrics\.)?([A-Za-z_]\w*)\b/g, (_, qualifier: string | undefined, id: string) =>
    String(qualifier ? evaluate(id, constants(metrics), depth + 1) : evaluate(id, own, depth + 1)),
  );
  if (!/^[\d\s.+\-*/()]+$/.test(substituted)) throw new Error(`${name} = ${expression} is not arithmetic this test can read`);
  return Function(`"use strict"; return (${substituted});`)() as number;
}

/** A method's body, braces excluded, with comments dropped and whitespace collapsed. */
function body(text: string, signature: RegExp): string {
  const at = text.search(signature);
  if (at < 0) throw new Error(`no method matching ${signature}`);
  const open = text.indexOf('{', at);
  let depth = 0;
  for (let i = open; i < text.length; i++) {
    if (text[i] === '{') depth++;
    else if (text[i] === '}' && --depth === 0) {
      return text
        .slice(open + 1, i)
        .replace(/\/\/[^\n]*/g, '')
        .replace(/\s+/g, ' ')
        .trim();
    }
  }
  throw new Error(`unbalanced braces after ${signature}`);
}

describe('the mirror of the sidebar', () => {
  test('reads the C# as code alone, so a comment never stands in for a constant', () => {
    expect([...constants(code('// public const double Gone = 1;\n/* public const double Also = 2; */ public const double Kept = 3;')).keys()]).toEqual(['Kept']);
    expect(code('var url = "https://example.org/a"; // note')).toBe('var url = "https://example.org/a"; ');
    expect(code("var slash = '/'; var at = @\"C:\\x\"; // note")).toBe("var slash = '/'; var at = @\"C:\\x\"; ");
    expect(constants(code('/// public const double Doc = 4;\n'))).toEqual(new Map());
  });

  test('carries the numbers PanelShell.cs has', () => {
    const own = constants(shell);
    for (const [name, value] of Object.entries(SHELL)) expect({ name, value: evaluate(name, own) }).toEqual({ name, value });
  });

  test("carries PanelMetrics.BorderWeight, which the rail's inner width and every item's x subtract", () => {
    expect(evaluate('BorderWeight', constants(metrics))).toBe(BORDER_WEIGHT);
  });

  test('lists the pages in the order the enum declares them', () => {
    const declared = /public enum PanelPage\s*\{([^}]*)\}/.exec(shell)![1]!.split(',').map((s) => s.trim().toLowerCase()).filter((s) => s.length > 0);
    expect(declared).toEqual([...PAGES]);
  });

  test("puts the column's items in PanelNav.Pages' order", () => {
    const pages = /public static readonly PanelPage\[\] Pages =\s*\{([^}]*)\}/.exec(nav)![1]!;
    expect([...pages.matchAll(/PanelPage\.(\w+)/g)].map((m) => m[1]!.toLowerCase())).toEqual([...COLUMN]);
  });

  test('puts a rule after the pages PanelNav.GapAfter names', () => {
    const gap = body(nav, /public static bool GapAfter\(/);
    expect(new Set([...gap.matchAll(/PanelPage\.(\w+)/g)].map((m) => m[1]!.toLowerCase()))).toEqual(new Set(GAP_AFTER));
  });

  test('adds up the same terms above the first item as ItemCentre does, full and rail', () => {
    const y = /var y = ([^;]+);/.exec(body(shell, /public static double ItemCentre\(/))![1]!;
    const choice = /\(layout == PanelLayout\.Full \? (\w+) : (\w+)\)/.exec(y)!;
    const terms = (pick: string) => y.replace(choice[0], pick).split('+').map((t) => t.trim());
    expect(terms(choice[1]!)).toEqual([...ITEM_TOP_TERMS.full]);
    expect(terms(choice[2]!)).toEqual([...ITEM_TOP_TERMS.rail]);
  });

  // The functions themselves, as the mirror was written from them. A change to one is a change to
  // where a page is clicked: move the mirror in panel-shots.ts with it, then this text.
  test.each([
    [/public static PanelLayout Layout\(/, 'if (controlWidth >= FullFrom) return PanelLayout.Full; return controlWidth >= RailFrom ? PanelLayout.Rail : PanelLayout.Compact;'],
    [/public static double SidebarWidthFor\(/, 'return layout == PanelLayout.Full ? SidebarWidth : RailWidth;'],
    [/public static double ItemCentreX\(/, 'return (SidebarWidthFor(layout) - PanelMetrics.BorderWeight) / 2;'],
    [
      /public static double ItemCentre\(/,
      'if (index < 0 || index >= PanelNav.Pages.Length) throw new ArgumentOutOfRangeException("index", index, "not an item the sidebar lists"); ' +
        'var y = SidebarPaddingTop + MarkRowHeight + MarkRowGap + SearchHeight + SearchGap + (layout == PanelLayout.Full ? LiveCardHeight : RailLiveHeight) + LiveCardGap; ' +
        'for (var i = 0; i < index; i++) { y += NavItemHeight + NavItemGap; if (PanelNav.GapAfter(PanelNav.Pages[i])) y += NavDividerHeight + NavItemGap; } return y + NavItemHeight / 2;',
    ],
    [/public static double UpdatesCentre\(/, 'return sidebarHeight - SidebarPaddingBottom - NavItemHeight / 2;'],
  ])('mirrors %s as it stands', (signature, expected) => {
    expect(body(shell, signature)).toBe(expected);
  });
});

describe('the layout a control width draws', () => {
  test.each([
    [700, 'Compact'],
    [759, 'Compact'],
    [760, 'Rail'],
    [999, 'Rail'],
    [1000, 'Full'],
    [1600, 'Full'],
    [3840, 'Full'],
  ] as const)('%d px is %s', (width, layout) => {
    expect(layoutFor(width)).toBe(layout);
  });

  test('the sidebar is 216 wide in full and a 56 rail otherwise', () => {
    expect([sidebarWidthFor('Full'), sidebarWidthFor('Rail'), sidebarWidthFor('Compact')]).toEqual([216, 56, 56]);
  });
});

describe('where each item is', () => {
  // Worked by hand from the artboard's numbers: 22 + 21 + 20 + 34 + 14 + 85 + 18 above the first
  // item, 42 an item, and 19 more after Rig and after Matrix for the rule and its gap.
  const FULL = [234, 276, 337, 379, 421, 482, 524];

  test('down the full sidebar', () => {
    expect(COLUMN.map((_, i) => itemCentre(i, 'Full'))).toEqual(FULL);
  });

  test('down the rail, 63 px higher, since the live card shrinks to its dot', () => {
    expect(COLUMN.map((_, i) => itemCentre(i, 'Rail'))).toEqual(FULL.map((y) => y - 63));
    expect(COLUMN.map((_, i) => itemCentre(i, 'Compact'))).toEqual(FULL.map((y) => y - 63));
  });

  test('across: 107.5 on the full sidebar, 27.5 on the rail', () => {
    expect([itemCentreX('Full'), itemCentreX('Rail'), itemCentreX('Compact')]).toEqual([107.5, 27.5, 27.5]);
  });

  test('Updates, measured up from the foot', () => {
    expect(updatesCentre(2096)).toBe(2062);
    expect(updatesCentre(645)).toBe(611);
  });

  test('an index the column does not have is refused', () => {
    expect(() => itemCentre(7, 'Full')).toThrow(/not an item/);
    expect(() => itemCentre(-1, 'Full')).toThrow(/not an item/);
  });
});

describe('where a page is clicked', () => {
  // A control as the measurement would report it: SimHub's own menu takes the left of its window, so
  // the control starts right of it and is narrower than SimHub by that much.
  const control = (left: number, width: number, height = 2000, top = 60) => ({ left, top, width, height });

  test.each([
    // SimHub asked for 700 and 1000 px: the panel is under 1000, and draws the rail.
    [700, control(250, 450), 'Compact', 250 + 27.5, 60 + 171],
    [1000, control(250, 750), 'Compact', 250 + 27.5, 60 + 171],
    [1000, control(200, 800), 'Rail', 200 + 27.5, 60 + 171],
    [1600, control(250, 1350), 'Full', 250 + 107.5, 60 + 234],
    [3840, control(250, 3590), 'Full', 250 + 107.5, 60 + 234],
  ] as const)('SimHub at %d px', (_, rect, layout, x, y) => {
    expect(clickFor('home', rect)).toEqual({ x, y, layout });
  });

  test('Settings is the last item in the column and Updates is at the foot', () => {
    expect(clickFor('settings', control(250, 1350))).toEqual({ x: 357.5, y: 60 + 524, layout: 'Full' });
    expect(clickFor('updates', control(250, 1350, 2000))).toEqual({ x: 357.5, y: 60 + 2000 - 34, layout: 'Full' });
    expect(clickFor('updates', control(250, 450, 700))).toEqual({ x: 277.5, y: 60 + 700 - 34, layout: 'Compact' });
  });

  test('at a scale other than 1 the layout is read in device-independent pixels and the offsets scaled back', () => {
    // 1350 screen pixels at 150 % is a 900 px control, which draws the rail.
    expect(clickFor('rig', control(300, 1350, 3000), 1.5)).toEqual({ x: 300 + 27.5 * 1.5, y: 60 + 213 * 1.5, layout: 'Rail' });
    expect(clickFor('updates', control(300, 3000, 3000), 1.5)).toEqual({ x: 300 + 107.5 * 1.5, y: 60 + (2000 - 34) * 1.5, layout: 'Full' });
  });
});

describe('the command line', () => {
  const ok = (argv: string[]): PanelShotsOptions => {
    const parsed = parseArgs(argv);
    if ('help' in parsed || 'error' in parsed) throw new Error(JSON.stringify(parsed));
    return parsed;
  };

  test('refuses to run without the menu entry measured, and says why', () => {
    const parsed = parseArgs([]);
    expect(parsed).toEqual({ error: expect.stringMatching(/--menu-y is required.*will not guess/) });
    expect(USAGE).toMatch(/--menu-y\s+REQUIRED/);
    expect(USAGE).toMatch(/There is no default because it moves with every plugin/);
  });

  test('defaults to every page at the four widths, into build/panel, with no rig and no emulator', () => {
    const opts = ok(['--menu-y', '431']);
    expect(opts.pages).toEqual([...PAGES]);
    expect(opts.widths).toEqual([700, 1000, 1600, 'max']);
    expect([opts.menuY, opts.menuX, opts.rig, opts.scenario, opts.noBuild, opts.keep]).toEqual([431, 100, null, null, false, false]);
    expect(opts.outDir.endsWith(path.join('build', 'panel'))).toBe(true);
  });

  test('reads every option, in either spelling', () => {
    const opts = ok(['--menu-y=431', '--pages', 'screens,leds', '--widths=1000,max', '--rig', 'panel', '--scenario', 'race', '--out', 'build/p2', '--menu-x', '90', '--no-build', '--keep']);
    expect(opts.pages).toEqual(['screens', 'leds']);
    expect(opts.widths).toEqual([1000, 'max']);
    expect([opts.rig, opts.scenario, opts.menuX, opts.noBuild, opts.keep]).toEqual(['panel', 'race', 90, true, true]);
    expect(opts.outDir.endsWith(path.join('build', 'p2'))).toBe(true);
  });

  test.each([
    [['--menu-y', 'top'], /not a pixel offset/],
    [['--menu-y', '431', '--pages', 'home,lights'], /unknown page lights/],
    [['--menu-y', '431', '--widths', '700,wide'], /neither max nor a width/],
    [['--menu-y', '431', '--rig', 'gallery'], /--rig gallery: one of panel, clear, empty/],
    [['--menu-y', '431', '--pages'], /--pages needs a value/],
    [['--menu-y', '431', '--page', 'home'], /unknown argument/],
  ])('refuses %p', (argv, message) => {
    expect(parseArgs(argv)).toEqual({ error: expect.stringMatching(message) });
  });

  test('asks for help', () => {
    expect(parseArgs(['--help'])).toEqual({ help: true });
  });
});

describe('what a picture is called', () => {
  test('a page and the width asked for', () => {
    expect(shotFile('home', 700)).toBe('home-700.png');
    expect(shotFile('updates', 'max')).toBe('updates-max.png');
  });

  test('a page taller than the panel, in parts', () => {
    expect([1, 2].map((part) => shotFile('screens', 1600, part, 2))).toEqual(['screens-1600-1of2.png', 'screens-1600-2of2.png']);
  });

  test('every page at every width is a different file', () => {
    const names = PAGES.flatMap((p) => [700, 1000, 1600, 'max' as const].map((w) => shotFile(p, w)));
    expect(new Set(names).size).toBe(PAGES.length * 4);
  });
});

describe('the colours the panel is found by', () => {
  const theme = code(readFileSync(path.join(plugin, 'Theme.cs'), 'utf8'));

  test.each(Object.entries(PANEL_COLOURS))("carries Theme.%s as Theme.cs has it", (name, rgb) => {
    const declared = new RegExp(`public const string ${name} = "(#[0-9A-Fa-f]{6})";`).exec(theme)?.[1];
    expect({ name, hex: declared?.toLowerCase() }).toEqual({ name, hex: hex(rgb) });
  });

  test("none of them is SimHub's page, which is what every gap around the panel is drawn in", () => {
    expect(hex(SIMHUB_PAGE)).toBe('#252525');
    for (const rgb of Object.values(PANEL_COLOURS)) expect(hex(rgb)).not.toBe(hex(SIMHUB_PAGE));
  });

  test("a sidebar run is shorter than the rail's and longer than any stray pixel", () => {
    expect(MIN_SIDEBAR_RUN).toBeLessThan(SHELL.RailWidth - BORDER_WEIGHT);
    expect(MIN_SIDEBAR_RUN).toBeGreaterThan(10);
  });
});

describe("the guest's measurement", () => {
  // What measurePanel's script printed on the VM (SimHub 9.12.6, 3840 x 2160 at 100 %) on 2026-10-02,
  // with the Screens page open. The rows are whole; each column is cut where it leaves the panel, since
  // below it is SimHub's licence offer, which this does not read.
  const head = (window: string, client: string, zoomed: 0 | 1) => [`﻿window ${window}`, `client ${client}`, 'dpi 96', `zoomed ${zoomed}`];
  const at700 = [
    ...head('0 0 700 2120', '0 0 700 2120', 0),
    'row 1060 [[0,0,[128,128,128]],[1,213,[37,37,37]],[214,214,[22,22,22]],[215,268,[6,7,8]],[269,269,[28,31,36]],[270,682,[10,11,13]],[683,692,[27,28,30]],[693,694,[10,11,13]],[695,698,[37,37,37]],[699,699,[128,128,128]]]',
    'col 217 [[0,0,[128,128,128]],[1,78,[37,37,37]],[79,1867,[6,7,8]],[1868,1868,[14,15,15]],[1869,1871,[37,37,37]]]',
  ].join('\r\n');
  const at1600 = [
    ...head('0 0 1600 2120', '0 0 1600 2120', 0),
    'row 1060 [[0,0,[128,128,128]],[1,214,[37,37,37]],[215,429,[6,7,8]],[430,430,[28,31,36]],[431,1121,[10,11,13]],[1122,1122,[51,56,63]],[1123,1163,[28,31,36]],[1164,1164,[51,56,63]],[1165,1208,[10,11,13]],[1209,1209,[51,56,63]],[1210,1233,[10,11,13]],[1234,1234,[28,31,36]],[1235,1548,[20,22,26]],[1549,1549,[28,31,36]],[1550,1593,[10,11,13]],[1594,1598,[37,37,37]],[1599,1599,[128,128,128]]]',
    'col 217 [[0,0,[128,128,128]],[1,78,[37,37,37]],[79,1868,[6,7,8]],[1869,1871,[37,37,37]]]',
  ].join('\r\n');
  const maximised = [
    ...head('-8 -8 3856 2136', '0 0 3840 2120', 1),
    'row 1060 [[-8,-1,[0,0,0]],[0,1324,[37,37,37]],[1325,1539,[6,7,8]],[1540,1540,[28,31,36]],[1541,2252,[10,11,13]],[2253,2253,[51,56,63]],[2254,2294,[28,31,36]],[2295,2295,[51,56,63]],[2296,2339,[10,11,13]],[2340,2340,[51,56,63]],[2341,2364,[10,11,13]],[2365,2365,[28,31,36]],[2366,2679,[20,22,26]],[2680,2680,[28,31,36]],[2681,2724,[10,11,13]],[2725,3839,[37,37,37]],[3840,3847,[0,0,0]]]',
    'col 1327 [[-8,-1,[0,0,0]],[0,78,[37,37,37]],[79,1868,[6,7,8]],[1869,1871,[37,37,37]]]',
    'signature BB329C1A',
    'probe 1433 432 [20,22,26]',
  ].join('\r\n');

  test('reads the rectangles, the scale, the signature and the probes', () => {
    const m = parseMeasure(maximised)!;
    expect(m.window).toEqual({ left: -8, top: -8, width: 3856, height: 2136 });
    expect(m.client).toEqual({ left: 0, top: 0, width: 3840, height: 2120 });
    expect([m.scale, m.zoomed]).toEqual([1, true]);
    expect(m.signature).toBe('bb329c1a');
    expect(m.probes).toEqual([{ x: 1433, y: 432, colour: [20, 22, 26] }]);
  });

  test("maximised, the panel is SimHub's centred column, 1400 wide, not the client area less the menu", () => {
    const m = parseMeasure(maximised)!;
    expect(m.control).toEqual({ left: 1325, top: 79, width: 1400, height: 1790 });
    expect(m.sidebar).toBe(216);
    expect(drawnLayout(m.sidebar, m.control!.width)).toBe(layoutFor(m.control!.width));
  });

  test('at 1600 px it fills the client area right of the menu, short of a four-pixel margin', () => {
    const m = parseMeasure(at1600)!;
    expect(m.control).toEqual({ left: 215, top: 79, width: 1379, height: 1790 });
    expect([m.sidebar, drawnLayout(m.sidebar, m.control!.width), layoutFor(m.control!.width)]).toEqual([216, 'Full', 'Full']);
  });

  test('at 700 px it draws the rail, its blended edges counted in and its scroll bar inside the crop', () => {
    const m = parseMeasure(at700)!;
    expect(m.control).toEqual({ left: 214, top: 79, width: 481, height: 1790 });
    expect([m.sidebar, drawnLayout(m.sidebar, m.control!.width), layoutFor(m.control!.width)]).toEqual([56, 'Compact', 'Compact']);
  });

  test('says there is no panel when no run of the sidebar colour is long enough to be one', () => {
    const other = [...head('0 0 700 2120', '8 30 684 1962', 0).map((l) => l.replace('dpi 96', 'dpi 144')), 'row 1000 [[0,699,[37,37,37]]]'].join('\n');
    expect(parseMeasure(other)).toMatchObject({ control: null, sidebar: 0, scale: 1.5, zoomed: false });
    const stray = [...head('0 0 700 2120', '0 0 700 2120', 0), 'row 1000 [[0,299,[37,37,37]],[300,320,[6,7,8]],[321,699,[37,37,37]]]', 'col 302 [[0,1999,[6,7,8]]]'].join('\n');
    expect(parseMeasure(stray)?.control).toBeNull();
  });

  test('says there is no panel when the column the guest read is not the sidebar where the row crosses it', () => {
    const row = parseSpans(at700, 'row')!;
    expect(findPanel(row.spans, row.at, null)).toBeNull();
    expect(findPanel(row.spans, row.at, [{ from: 0, to: 2119, colour: SIMHUB_PAGE }])).toBeNull();
  });

  test('says there is nothing to measure when SimHub has no window', () => {
    expect(parseMeasure('SimHub has no main window')).toBeNull();
  });

  test('refuses a line of spans it cannot read', () => {
    expect(parseSpans('row 10 [[0,1,[1,2]]]', 'row')).toBeNull();
    expect(parseSpans('row 10 not json', 'row')).toBeNull();
    expect(parseSpans('col 5 [[0,1,[1,2,3]]]', 'row')).toBeNull();
    expect(parseSpans('col 5 [[0,1,[1,2,3]]]', 'col')).toEqual({ at: 5, spans: [{ from: 0, to: 1, colour: [1, 2, 3] }] });
  });

  test("the page's own column is the panel right of its sidebar", () => {
    expect(mainColumn(parseMeasure(maximised)!)).toEqual({ left: 1541, top: 79, width: 1184, height: 1790 });
    expect(mainColumn(parseMeasure(at700)!)).toEqual({ left: 270, top: 79, width: 425, height: 1790 });
    expect(mainColumn(parseMeasure('window 0 0 1 1\nclient 0 0 1 1')!)).toBeNull();
  });

  test('a rail read at a scale is still a rail', () => {
    expect(drawnLayout(84, 1350, 1.5)).toBe('Rail');
    expect(drawnLayout(324, 1350, 1.5)).toBe('Full');
    expect(drawnLayout(56, 700)).toBe('Compact');
  });

  test('the clicks the mirror makes land on the sidebar the measurement found, and read the open item low in it', () => {
    for (const report of [at700, at1600, maximised]) {
      const m = parseMeasure(report)!;
      for (const page of PAGES) {
        const at = clickFor(page, m.control!);
        expect(at.x).toBeGreaterThan(m.control!.left);
        expect(at.x).toBeLessThan(m.control!.left + m.sidebar);
        expect(at.y).toBeLessThan(m.control!.top + m.control!.height);
        const probe = activeProbe(at);
        expect(probe.y - at.y).toBe(16);
        expect(probe.y).toBeLessThan(at.y + SHELL.NavItemHeight / 2);
      }
    }
    // The probe of the maximised report is where Screens is clicked, read 16 px lower: the open item's colour.
    const m = parseMeasure(maximised)!;
    expect(activeProbe(clickFor('screens', m.control!))).toEqual({ x: 1432.5, y: 432 });
    expect(m.probes[0]!.colour).toEqual([...PANEL_COLOURS.SurfaceZone]);
  });

  test('scrolls a viewport a time, a notch short so the parts overlap, and stops at a bound', () => {
    expect(notchesPerViewport(1790)).toBe(36);
    expect(notchesPerViewport(40)).toBe(1);
    expect(MAX_PARTS).toBeGreaterThanOrEqual(4);
  });
});
