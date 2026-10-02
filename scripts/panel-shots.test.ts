/**
 * What `scripts/panel-shots.ts` decides without a VM: that its mirror of the panel's sidebar is the
 * panel's, where each page is clicked at each width, what the pictures are called, and how the
 * command line and the guest's measurements are read.
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
  BORDER_WEIGHT,
  buttonAt,
  clickFor,
  COLUMN,
  GAP_AFTER,
  ITEM_TOP_TERMS,
  itemCentre,
  itemCentreX,
  layoutFor,
  mainColumn,
  notchesPerViewport,
  PAGES,
  parseArgs,
  parseMeasure,
  partsFor,
  SHELL,
  shotFile,
  sidebarWidthFor,
  updatesCentre,
  USAGE,
  type PanelShotsOptions,
} from './panel-shots.ts';

const plugin = path.resolve(import.meta.dir, '..', 'plugin', 'OpenDash');
const read = (file: string): string => readFileSync(path.join(plugin, file), 'utf8');
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
  // A control as UI Automation would report it: SimHub's own menu takes the left of its window, so
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

describe("the guest's measurement", () => {
  // What measurePanel's script prints, byte order mark and all, for SimHub maximised on the VM.
  const report = [
    '﻿window -8 -8 3856 2136',
    'client 0 0 3840 2120',
    'dpi 96',
    'zoomed 1',
    'control 250 64 3590 2056',
    'scroller 254 64 211 1980 0 100 -1',
    'scroller 466 64 3374 2056 1 48.5 0',
    'scroller 900 400 300 200 1 20 0',
    'button 262 278 191 40 Home',
    'button 262 2066 191 40 Updates',
  ].join('\r\n');

  test('reads the rectangles, the scale and what can scroll', () => {
    const m = parseMeasure(report)!;
    expect(m.window).toEqual({ left: -8, top: -8, width: 3856, height: 2136 });
    expect(m.client).toEqual({ left: 0, top: 0, width: 3840, height: 2120 });
    expect([m.scale, m.zoomed]).toEqual([1, true]);
    expect(m.control).toEqual({ left: 250, top: 64, width: 3590, height: 2056 });
    expect(m.scrollers).toHaveLength(3);
    expect(m.buttons.map((b) => b.name)).toEqual(['Home', 'Updates']);
  });

  test('says there is no control when SimHub is showing another page', () => {
    expect(parseMeasure('window 0 0 700 2000\nclient 8 30 684 1962\ndpi 144\nzoomed 0\ncontrol none')).toMatchObject({ control: null, scale: 1.5, zoomed: false });
  });

  test('says there is nothing to measure when SimHub has no window', () => {
    expect(parseMeasure('SimHub has no main window')).toBeNull();
  });

  test("finds the page's own scroll viewer: right of the sidebar, and the largest there", () => {
    expect(mainColumn(parseMeasure(report)!)?.rect).toEqual({ left: 466, top: 64, width: 3374, height: 2056 });
  });

  test('takes as many pictures as the page is viewports tall', () => {
    const column = mainColumn(parseMeasure(report)!);
    expect(partsFor(column)).toBe(3);
    expect(partsFor({ ...column!, viewSize: 50 })).toBe(2);
    expect(partsFor({ ...column!, viewSize: 100 })).toBe(1);
    expect(partsFor({ ...column!, scrollable: false, viewSize: 40 })).toBe(1);
    expect(partsFor(null)).toBe(1);
  });

  test('scrolls a viewport a time, a notch short so the parts overlap', () => {
    expect(notchesPerViewport(2056)).toBe(41);
    expect(notchesPerViewport(40)).toBe(1);
  });

  test('knows which sidebar button a click lands on', () => {
    const m = parseMeasure(report)!;
    expect(buttonAt(clickFor('home', m.control!), m.buttons)).toBe('Home');
    expect(buttonAt(clickFor('updates', m.control!), m.buttons)).toBe('Updates');
    expect(buttonAt({ x: 10, y: 10 }, m.buttons)).toBeNull();
  });
});
