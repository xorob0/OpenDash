/**
 * The companion and the pit wall: that they are built, that they fit, and that they agree with the
 * settings contract. The text checks are the same ones the dash face gets, because SimHub hands
 * every box to WPF as a hard clip whatever screen it is on.
 */
import { describe, expect, test } from 'bun:test';
import { measureText, type MeasuredFace } from '../src/design/advances.ts';
import { LINE_SPACING, cells, monoWidth } from '../src/design/metrics.ts';
import {
  MODULE_CATALOGUE,
  MODULE_COUNT,
  PIT_WALL_WIDE_ZONE_PAGES,
  PIT_WALL_ZONE_PAGES,
  dashProperties,
  declaredProperties,
  moduleSettingName,
  secondScreen,
  secondScreenProperties,
} from '../src/contract.ts';
import { validatePackage, type ChartItem, type Dashboard, type Item, type RadarItem, type RectangleItem, type StaticMapItem, type TextItem, type WidgetItem } from '../src/generator.ts';
import { PROPERTY_PREFIX } from '../src/contract.ts';
import { packImages } from '../src/build.ts';
import { MODULES, pageBuilder } from '../src/modules/index.ts';
import { COMPANION_SIZES, SCREEN_PACKAGES, buildScreenPackage, companionGeometry, zoneDashboardName } from '../src/screens/index.ts';
import { ZONE_FACES, layoutWithoutRevBar, zonesOf } from '../src/zones/index.ts';
import { densityForBox } from '../src/second/density.ts';
import { DENOMINATOR_GAP, UNIT_GAP, field, type Follower } from '../src/second/field.ts';
import { zoneFrame } from '../src/second/header.ts';
import { contentRect } from '../src/second/layout.ts';
import { contains, rect } from '../src/design/geometry.ts';
import type { Density } from '../src/second/density.ts';
import type { Rect } from '../src/design/geometry.ts';
import { itemsOf, propertiesIn, walkItems } from '../src/walk.ts';
import { cellOverruns, drawableGlyphs } from './monoGlyphs.ts';
import { ds } from '../src/tokens.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
// Composed the way `build.ts` composes them, the image step included: a package whose items draw a
// picture declares it before it is validated, or the validator reads the drawing as a hole.
const PACKAGES = SCREEN_PACKAGES.map((def) => {
  const pkg = buildScreenPackage(def, OPTS);
  packImages(pkg);
  return { def, pkg };
});
const BRAND = /#00E5FF/i;

/** Which measured face an item draws in: the family it names, at the weight it asks for. */
const faceOf = (item: TextItem): MeasuredFace => {
  if (item.font === 'Barlow') return item.fontWeight === 'Bold' ? 'BarlowBold' : 'BarlowMedium';
  if (item.fontWeight === 'Bold') return 'BarlowCondensedBold';
  if (item.fontWeight === 'Light') return 'BarlowCondensedLight';
  return 'BarlowCondensedSemiBold';
};

/**
 * What an item will draw. A bound item draws its binding rather than its sample, so where one
 * declares `widest` that is what has to fit the box.
 */
const drawnText = (item: TextItem): string => item.widest ?? item.text;

/** Width of what an item draws: its cells when monospaced, the measured advances otherwise. */
function drawnWidth(item: TextItem): number {
  const text = drawnText(item);
  const mono = item.monospace;
  if (!mono) return measureText(faceOf(item), text, item.fontSize);
  const specials = [...text].filter((c) => mono.specialChars?.includes(c) ?? false).length;
  return (text.length - specials) * mono.charWidth + specials * mono.specialCharsWidth;
}

const textsOf = (dashboard: Dashboard): TextItem[] => itemsOf(dashboard).filter((i): i is TextItem => i.kind === 'text');

describe('the packages are built and valid', () => {
  test('four packages: two companions and two pit walls', () => {
    expect(SCREEN_PACKAGES.map((d) => d.folder)).toEqual(['openDash Companion', 'openDash Companion portrait', 'openDash Pit wall', 'openDash Pit wall portrait']);
    expect(SCREEN_PACKAGES.filter((d) => d.kind === 'companion')).toHaveLength(2);
    expect(SCREEN_PACKAGES.filter((d) => d.kind === 'pitwall')).toHaveLength(2);
  });

  for (const { def, pkg } of PACKAGES) {
    test(`${def.folder} validates with no error or warning`, () => {
      const result = validatePackage(pkg, { declaredProperties: declaredProperties(), propertyPrefix: PROPERTY_PREFIX });
      expect(result.errors).toEqual([]);
      expect(result.warnings).toEqual([]);
    });

    test(`${def.folder} draws no brand colour`, () => {
      expect(JSON.stringify(pkg.dashboards)).not.toMatch(BRAND);
    });

    test(`${def.folder} fits every text in its box`, () => {
      for (const dashboard of pkg.dashboards) {
        for (const item of textsOf(dashboard)) {
          const width = drawnWidth(item);
          expect({ dashboard: dashboard.name, item: item.name, text: drawnText(item), width, box: item.rect.width, fits: width <= item.rect.width }).toMatchObject({ fits: true });
          const line = LINE_SPACING * item.fontSize;
          expect({ item: item.name, line, box: item.rect.height, fits: line <= item.rect.height }).toMatchObject({ fits: true });
        }
      }
    });

    test(`${def.folder} keeps every item on its canvas`, () => {
      for (const dashboard of pkg.dashboards) {
        for (const item of itemsOf(dashboard)) {
          if (item.kind === 'layer') continue;
          const r = item.rect;
          const inside = r.left >= 0 && r.top >= 0 && r.left + r.width <= dashboard.width && r.top + r.height <= dashboard.height;
          expect({ dashboard: dashboard.name, item: item.name, rect: r, inside }).toMatchObject({ inside: true });
        }
      }
    });

    test(`${def.folder} names every item once per screen`, () => {
      for (const dashboard of pkg.dashboards) {
        for (const screen of dashboard.screens) {
          const names = itemsOf({ ...dashboard, screens: [screen] }).map((i) => i.name);
          expect({ screen: screen.name, duplicates: names.filter((n, i) => names.indexOf(n) !== i) }).toMatchObject({ duplicates: [] });
        }
      }
    });
  }
});

describe('the companion', () => {
  const companion = PACKAGES.find((p) => p.def.folder === 'openDash Companion')!;
  const main = companion.pkg.dashboards[0]!;
  const bothSizes = PACKAGES.filter((p) => p.def.folder.startsWith('openDash Companion'));

  test('has one screen per module, in catalogue order', () => {
    expect(main.screens).toHaveLength(MODULE_COUNT);
    expect(main.screens.map((s) => s.name)).toEqual(MODULE_CATALOGUE.map((m) => m.id));
    expect(MODULES.map((m) => m.id)).toEqual(MODULE_CATALOGUE.map((m) => m.id));
  });

  test('lap times draws ranks of three equal columns, spread over the body', () => {
    // The artboard's grid: three `minmax(0, 1fr)` columns 24 px apart, which is a 275 px pitch in
    // an 802 px body. Its point is the alignment down the page: the estimate under the session
    // best, the position under the estimate. Content widths put each field wherever its own digits
    // ended, and the four ranks then lined up with nothing.
    const screen = main.screens.find((s) => s.name === 'lapTimes')!;
    const labels = [...walkItems(screen.items)].filter((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.label') && !i.name.includes('.header.'));
    const rows = [...new Set(labels.map((l) => l.rect.top))].sort((a, b) => a - b);
    // Four ranks, as the artboard draws. The sector rank needs 348 px of content and used to be
    // shed, because the flag band above took the token's 32 px rather than the artboard's 12 and
    // left the page 336 tall; at the artboard's 356 it fits.
    expect(rows).toHaveLength(4);
    const pitches = new Set<number>();
    for (const top of rows) {
      const lefts = labels
        .filter((l) => l.rect.top === top)
        .map((l) => l.rect.left)
        .sort((a, b) => a - b);
      expect(lefts).toHaveLength(3);
      expect(lefts[1]! - lefts[0]!).toBe(lefts[2]! - lefts[1]!);
      pitches.add(lefts[1]! - lefts[0]!);
    }
    // The same pitch in every rank, which is what makes the page a grid rather than three rows.
    expect(pitches.size).toBe(1);
    // Spread rather than centred: every rank opens on the body's own left edge.
    expect(new Set(rows.map((top) => Math.min(...labels.filter((l) => l.rect.top === top).map((l) => l.rect.left)))).size).toBe(1);
  });

  test('switches each screen on its own plugin setting', () => {
    main.screens.forEach((screen, i) => {
      expect(screen.enabledExpression).toBe(secondScreen.moduleEnabled(i + 1));
      expect(screen.enabledExpression).toContain(moduleSettingName(i + 1));
    });
    // Energy, damage and track rivals default to off, which is a 0 in the expression.
    expect(main.screens[5]!.enabledExpression).toContain(', 0)');
    expect(main.screens[0]!.enabledExpression).toContain(', 1)');
  });

  test('gives every screen the same roles, so the page ring works in and out of a session', () => {
    // SimHub only filters screens by role when the roles differ between them; identical roles keep
    // every enabled screen in the Next/Previous ring whatever the game is doing.
    const roles = new Set(main.screens.map((s) => `${s.inGame};${s.idle};${s.pit}`));
    expect([...roles]).toEqual(['true;true;false']);
  });

  test('stacks the four bands the artboard draws, which fill the screen exactly', () => {
    // The flag band is the artboard's 12 px strip, as on the nano face, rather than the 32 px
    // heightSm token it used to read, which took twenty pixels off the body of all forty-two
    // screens. Pinned on both sizes so the bands cannot drift again.
    const bandsOf = (folder: string): number[] => {
      const g = companionGeometry(COMPANION_SIZES.find((s) => s.folder === folder)!);
      return [g.header.height, g.module.height, g.dots.height, g.flags.height];
    };
    expect(bandsOf('openDash Companion')).toEqual([56, 388, 24, 12]);
    expect(bandsOf('openDash Companion portrait')).toEqual([56, 758, 24, 12]);
    for (const size of COMPANION_SIZES) {
      expect(bandsOf(size.folder).reduce((a, b) => a + b, 0)).toBe(size.height);
      // What the band arithmetic is for: the box the module is actually handed.
      const box = contentRect(companionGeometry(size).module, 'companion');
      expect([box.width, box.height]).toEqual(size.width === 850 ? [802, 356] : [432, 726]);
    }
  });

  test('draws the header, the dots and the flag band on every screen', () => {
    for (const { pkg } of bothSizes) {
      const dashboard = pkg.dashboards[0]!;
      for (const screen of dashboard.screens) {
        const items = itemsOf({ ...dashboard, screens: [screen] });
        const names = items.map((i) => i.name);
        expect(names.some((n) => n.includes('header.module'))).toBe(true);
        expect(names.filter((n) => n.includes('.dots.dot'))).toHaveLength(MODULE_COUNT);
        // The rectangle rather than the presence: the body is measured against what is left under
        // it, so a band that grew back would be a silently shorter page rather than a failure.
        const band = items.find((i) => i.name.endsWith('.flag.yellow.band')) as RectangleItem;
        expect(band.rect).toEqual({ left: 0, top: dashboard.height - 12, width: dashboard.width, height: 12 });
        expect(band.backgroundColor).toBe(ds.purpose.flag.yellow);
      }
    }
  });

  test('the portrait package draws the same modules in a taller box', () => {
    const portrait = PACKAGES.find((p) => p.def.folder === 'openDash Companion portrait')!.pkg.dashboards[0]!;
    expect(portrait.width).toBe(480);
    expect(portrait.height).toBe(850);
    expect(portrait.screens.map((s) => s.name)).toEqual(main.screens.map((s) => s.name));
  });
});

describe('the pit wall', () => {
  const landscape = PACKAGES.find((p) => p.def.folder === 'openDash Pit wall')!;
  const main = landscape.pkg.dashboards[0]!;

  test('has three pages', () => {
    expect(main.screens.map((s) => s.name)).toEqual(['race', 'tower', 'telemetry']);
  });

  test('carries a zone dashboard for every rectangle its pages embed', () => {
    const widgets = itemsOf(main).filter((i) => i.kind === 'widget');
    expect(widgets.length).toBeGreaterThan(0);
    const files = new Set(widgets.map((w) => (w.kind === 'widget' ? w.fileName : '')));
    const built = new Set(landscape.pkg.dashboards.slice(1).map((d) => `${d.name}.djson`));
    expect([...files].sort()).toEqual([...built].sort());
    for (const widget of widgets) {
      if (widget.kind !== 'widget') continue;
      const kind = widget.fileName.startsWith('zones-wide') ? 'wide' : 'standard';
      expect(widget.fileName).toBe(`${zoneDashboardName(kind, { width: widget.rect.width, height: widget.rect.height })}.djson`);
    }
  });

  test('binds every zone to its own plugin setting', () => {
    const bound = itemsOf(main)
      .filter((i) => i.kind === 'widget')
      .map((i) => i.bindings?.InitialScreenIndex?.formula)
      .filter((f): f is string => typeof f === 'string');
    expect(bound).toContain(secondScreen.zonePage('A'));
    expect(bound).toContain(secondScreen.zonePage('B'));
    expect(bound).toContain(secondScreen.zonePage('C'));
    expect(bound).toContain(secondScreen.zonePage('D'));
    expect(bound).toContain(secondScreen.wideZonePage());
  });

  test('a zone dashboard holds every page of its kind, in the order the plugin lists them', () => {
    for (const dashboard of landscape.pkg.dashboards.slice(1)) {
      const pages = dashboard.name.startsWith('zones-wide') ? PIT_WALL_WIDE_ZONE_PAGES : PIT_WALL_ZONE_PAGES;
      expect(dashboard.screens.map((s) => s.name)).toEqual(pages.map((p) => p.id));
    }
  });

  test('a wide zone says so in its title, and a narrow one does not', () => {
    // The same page is offered in both kinds and a pit wall carries both at once, so the title is
    // where the two are told apart. The catalogue's own name stays clean, because it is also the
    // label the plugin puts in its wide-zone dropdown.
    const titleOf = (dashboard: Dashboard, screen: string): string => {
      const item = itemsOf(dashboard).find((i): i is TextItem => i.kind === 'text' && i.name === `${screen}.title`);
      if (!item) throw new Error(`no title on ${dashboard.name}/${screen}`);
      return item.text;
    };
    const wide = landscape.pkg.dashboards.find((d) => d.name.startsWith('zones-wide'))!;
    const narrow = landscape.pkg.dashboards.find((d) => d.name.startsWith('zones-') && !d.name.startsWith('zones-wide'))!;
    for (const page of PIT_WALL_WIDE_ZONE_PAGES) expect(titleOf(wide, page.id)).toBe(`${page.name.toUpperCase()} · WIDE`);
    for (const page of PIT_WALL_ZONE_PAGES) expect(titleOf(narrow, page.id)).toBe(page.name.toUpperCase());
  });

  test('the portrait page is one screen with four zones', () => {
    const portrait = PACKAGES.find((p) => p.def.folder === 'openDash Pit wall portrait')!.pkg.dashboards[0]!;
    expect(portrait.screens.map((s) => s.name)).toEqual(['portrait']);
    expect(itemsOf(portrait).filter((i) => i.kind === 'widget')).toHaveLength(4);
  });
});

describe('the two chromes', () => {
  test('a panel title is the plain label grey and a zone title the brighter secondary', () => {
    // The sheets differ by one inline override: every pit wall zone title carries `color: #8A9099`
    // and the counter beside it does not, while a panel title carries nothing and takes the small
    // label's own #5A6069. Reading the two as one colour is what drew every panel title too bright.
    const titlesOf = (isZone: boolean): TextItem[] =>
      PACKAGES.flatMap((p) =>
        p.pkg.dashboards
          .filter((d) => d.name.startsWith('zones') === isZone)
          .flatMap((d) => d.screens.flatMap((s) => [...walkItems(s.items)])),
      ).filter((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.title'));
    const panels = titlesOf(false);
    const zones = titlesOf(true);
    expect(panels.length).toBeGreaterThan(0);
    expect(zones.length).toBeGreaterThan(0);
    expect([...new Set(panels.map((i) => i.textColor))]).toEqual([ds.color.text.label]);
    expect([...new Set(zones.map((i) => i.textColor))]).toEqual([ds.color.text.secondary]);
  });
});

describe('the tables', () => {
  const all: Item[] = PACKAGES.flatMap((p) => p.pkg.dashboards.flatMap((d) => itemsOf(d)));
  const repeated = all.filter((i) => i.kind === 'layer' && (i.repetitions ?? 0) > 0);

  test('stamp their rows from one row definition', () => {
    expect(repeated.length).toBeGreaterThan(0);
    for (const layer of repeated) {
      if (layer.kind !== 'layer') continue;
      expect({ name: layer.name, pitch: layer.repeatTopOffset }).toMatchObject({ pitch: expect.any(Number) });
      expect(layer.repeatTopOffset).toBeGreaterThan(0);
      expect(layer.repeatLeftOffset).toBe(0);
      // SimHub drops widgets from the copies it stamps, so a repeated row must not hold one.
      expect(itemsOf({ screens: [{ name: 'x', items: layer.children }] } as Dashboard).some((i) => i.kind === 'widget')).toBe(false);
    }
  });

  test('hide a row that has no car, from inside the repeat context', () => {
    for (const layer of repeated) {
      if (layer.kind !== 'layer') continue;
      // The repeated layer itself is evaluated once, so the per-row test lives on its child.
      expect(layer.bindings?.Visible).toBeUndefined();
      const child = layer.children[0];
      expect(child?.kind).toBe('layer');
      expect(typeof child?.bindings?.Visible?.formula).toBe('string');
    }
  });
});

describe('the contract', () => {
  test('every property the second screens read is declared', () => {
    const declared = new Set(declaredProperties());
    for (const { pkg } of PACKAGES) {
      for (const dashboard of pkg.dashboards) {
        for (const p of propertiesIn(dashboard).filter((n) => n.startsWith('OpenDash.'))) {
          expect({ p, declared: declared.has(p) }).toEqual({ p, declared: true });
        }
      }
    }
  });

  test('the second screens read every second-screen property, and the face reads none of them', () => {
    const used = new Set<string>();
    for (const { pkg } of PACKAGES) {
      for (const dashboard of pkg.dashboards) {
        for (const screen of dashboard.screens) if (screen.enabledExpression) used.add(screen.enabledExpression);
        for (const p of propertiesIn(dashboard)) used.add(p);
      }
    }
    const text = [...used].join(' ');
    for (const p of secondScreenProperties()) expect({ p, read: text.includes(p) }).toEqual({ p, read: true });
    // The dash settings are shared: the second screens read the modes too, but never a slot.
    expect(text).toContain('OpenDash.PositionMode');
    for (const p of dashProperties().filter((n) => n.includes('.Slot'))) expect(text).not.toContain(p);
  });
});

describe('a monospaced value only draws glyphs that fit its cell', () => {
  // Rule 19. `metrics.ts` has said since the cells were cut that "the cell holds every glyph a
  // value can draw, not only the digits", and nothing enforced it over these packages: the glyph
  // check in textFit.test.ts covers the faces only, and against a fixed glyph set that `#` was
  // never in. Thirty-one items drew one anyway, in cells cut for digits, and WPF clipped every
  // one. The set here is the item's own text, so a value that gains a character nobody measured
  // fails here rather than on somebody's screen.
  for (const { def, pkg } of PACKAGES) {
    test(def.folder, () => {
      let monospaced = 0;
      for (const dashboard of pkg.dashboards) {
        for (const item of textsOf(dashboard)) {
          if (!item.monospace) continue;
          monospaced += 1;
          expect({ item: item.name, overruns: cellOverruns(item) }).toMatchObject({ overruns: [] });
          // The same rule said in the design's own words rather than in advances. `numeral` refuses
          // the set at the point a value is built, which covers everything drawn through it; this
          // covers what these packages actually emit, a monospaced item assembled some other way
          // included, and it names the token so a failure reads as the ban it is.
          const banned = [...drawableGlyphs(item)].filter((glyph) => ds.font.cell.excluded.has(glyph)).sort();
          expect({ item: item.name, banned }).toMatchObject({ banned: [] });
        }
      }
      expect(monospaced).toBeGreaterThan(0);
    });
  }

  test('the banned set is banned because it does not fit, not because somebody typed it', () => {
    // Without this the token and the guards above are two opinions that happen to agree:
    // `cellOverruns` measures and `font.cell.excluded` declares, and a character only one of them
    // rejects is a place where the design and the code have quietly parted. Measured at 1000 px so
    // the comparison is the em fraction the token is written in, free of any rounding a size adds.
    const faces = [
      ['BarlowCondensedSemiBold', ds.font.cell.semiBold.digit],
      ['BarlowCondensedBold', ds.font.cell.bold.digit],
    ] as const;
    expect(ds.font.cell.excluded.size).toBeGreaterThan(0);
    for (const [face, cell] of faces) {
      for (const glyph of ds.font.cell.excluded) {
        const em = measureText(face, glyph, 1000) / 1000;
        expect({ face, glyph, em, cell, overruns: em > cell }).toMatchObject({ overruns: true });
      }
    }
  });
});

/**
 * The boxes a module is actually given, derived from the geometry that gives them.
 *
 * They used to be written down here as literals, and every one of them had drifted: the companion
 * page was tested at 802 by 356 and built at 802 by 336, the race zone at 607 by 174 against 607
 * by 158, the telemetry zone at 607 by 310 against 608 by 294. Every literal was *taller* than the
 * real box, so the guarantee read stronger than it was — a module could pass and still overflow
 * what the build hands it. Nothing overflowed, which is why nobody noticed, and the shape model is
 * about to ask modules to fill their height.
 *
 * The companion's 356 has since become the built height too, the flag band having come down from
 * the 32 px token to the artboard's 12, but that is a coincidence rather than a reason to write
 * any of these down again.
 */
export function moduleBoxes(): { name: string; frame: Rect; density: Density }[] {
  const boxes: { name: string; frame: Rect; density: Density }[] = [];
  for (const size of COMPANION_SIZES) {
    boxes.push({ name: `${size.folder} page`, frame: contentRect(companionGeometry(size).module, 'companion'), density: 'companion' });
  }
  // One box per distinct zone rectangle the pit wall packages embed, taken from the widgets they
  // actually place and put through the same frame the zone screen draws.
  const seen = new Set<string>();
  for (const { def, pkg } of PACKAGES) {
    if (def.kind !== 'pitwall') continue;
    for (const widget of itemsOf(pkg.dashboards[0]!).filter((i): i is WidgetItem => i.kind === 'widget')) {
      const size = { width: widget.rect.width, height: widget.rect.height };
      const wide = widget.fileName.startsWith('zones-wide');
      const key = `${wide ? 'wide' : 'zone'}-${size.width}x${size.height}`;
      if (seen.has(key)) continue;
      seen.add(key);
      const { body } = zoneFrame('probe', { frame: rect(0, 0, size.width, size.height), title: 'PROBE', counter: { kind: 'static', page: 1, pages: 9 } });
      boxes.push({ name: `${def.folder} ${key}`, frame: body, density: wide ? 'wide' : 'zone' });
    }
  }
  // And one box per distinct rectangle a *face* zone is drawn at, both arrangements of all eight
  // faces, through the face's own frame rather than the pit wall's. The modules are one catalogue
  // and the faces are where the smallest boxes of it are: 245 by 156 on the nano and 576 by 112 in
  // zone C of the portrait face are narrower and shorter than anything the pit wall places, so a
  // module that overflows one of them would otherwise overflow it unmeasured.
  for (const layout of ZONE_FACES) {
    for (const arrangement of [layout, layoutWithoutRevBar(layout)]) {
      for (const { zone, size } of zonesOf(arrangement)) {
        if (zone === 'A' || zone === 'D') continue;
        const key = `face-${size.width}x${size.height}`;
        if (seen.has(key)) continue;
        seen.add(key);
        const density = densityForBox(size);
        const { body } = zoneFrame('probe', { frame: rect(0, 0, size.width, size.height), title: 'PROBE', counter: { kind: 'reserved', widest: '21 / 21' } }, density, 'face');
        boxes.push({ name: key, frame: body, density });
      }
    }
  }
  return boxes;
}

describe('every module fits the box it is given', () => {
  const BOXES = moduleBoxes();

  test('the boxes come from the geometry, not from a list somebody kept up to date', () => {
    // Two companion pages, the distinct zone rectangles the pit walls use, and the distinct ones
    // the eight faces draw in both of their arrangements.
    expect(BOXES.length).toBeGreaterThanOrEqual(6);
    expect(BOXES.map((b) => b.name)).toContain('face-269x194');
    expect(BOXES.map((b) => b.name)).toContain('face-600x150');
    for (const box of BOXES) {
      expect({ name: box.name, w: box.frame.width > 0, h: box.frame.height > 0 }).toMatchObject({ w: true, h: true });
    }
  });

  for (const box of BOXES) {
    test(`on a ${box.name}`, () => {
      for (const module of MODULES) {
        const items = module.build({ frame: box.frame, density: box.density, prefix: `${module.id}.` });
        expect({ module: module.id, drew: items.length > 0 }).toMatchObject({ drew: true });
        for (const item of items.flatMap((i) => [...walkItems([i])])) {
          if (item.kind === 'layer') continue;
          const r = item.rect;
          // A text box is a WPF line box and it is taller than its ink at both ends. `textBox`
          // puts its top a tenth of the font size above the line it is given, so the baseline
          // lands where the layout asked for it, and the box runs about a fifth of the size below
          // that baseline. Both tails are transparent, so both are slack -- and granting it only
          // at the bottom is why these boxes were quietly written 16 px taller than the real ones
          // instead of being derived.
          const below = item.kind === 'text' ? Math.ceil(0.25 * item.fontSize) + 2 : 1;
          const above = item.kind === 'text' ? Math.ceil(0.1 * item.fontSize) + 2 : 1;
          const inside =
            r.left >= box.frame.left - 1 &&
            r.top >= box.frame.top - above &&
            r.left + r.width <= box.frame.left + box.frame.width + 1 &&
            r.top + r.height <= box.frame.top + box.frame.height + below;
          expect({ module: module.id, item: item.name, rect: r, inside }).toMatchObject({ inside: true });
        }
      }
    });
  }
});

/**
 * The one page whose rank is two of the same thing, and the one way it can fail that no fit check
 * catches: a page about the car ahead and the car behind that draws only the car ahead fits its box
 * perfectly and has lost its subject.
 *
 * Three boxes used to draw one block — the nano's 245 by 156 and both of the 600 by 686 face's
 * short zones — because each block was a fixed-height row and the stack dropped the second one
 * whole rather than shedding a line from each.
 */
describe('the opponents page keeps both cars', () => {
  const opponents = MODULES.find((m) => m.id === 'opponents')!;

  for (const box of moduleBoxes()) {
    test(`on a ${box.name}`, () => {
      const names = opponents.build({ frame: box.frame, density: box.density, prefix: '' }).flatMap((i) => [...walkItems([i])]).map((i) => i.name);
      const drawn = (side: string): string[] => names.filter((name) => name.startsWith(`${side}.`)).map((name) => name.slice(side.length + 1).split('.')[0]!);
      expect({ box: box.name, ahead: [...new Set(drawn('ahead'))] }).not.toEqual({ box: box.name, ahead: [] });
      // The same pieces on both, never one car's gap without the other's: what a short box sheds,
      // it sheds from the pair.
      expect({ box: box.name, behind: [...new Set(drawn('behind'))] }).toEqual({ box: box.name, behind: [...new Set(drawn('ahead'))] });
    });
  }
});

describe('a bar drawn under a value', () => {
  const gaugeOf = (density: Density) => {
    const module = MODULES.find((m) => m.id === 'fuel')!;
    const items = module.build({ frame: rect(0, 0, 802, 336), density, prefix: 'fuel.' });
    const drawn = items.find((i) => i.kind === 'linearGauge');
    if (drawn?.kind !== 'linearGauge') throw new Error('no gauge drawn');
    return drawn;
  };

  test('is four pixels tall on the companion as in a zone, with the canvas track and fill', () => {
    for (const density of ['companion', 'zone'] as const) {
      const drawn = gaugeOf(density);
      expect({ density, height: drawn.rect.height, track: drawn.backgroundColor, fill: drawn.gaugeColor }).toEqual({
        density,
        height: 4,
        track: ds.color.surface.raised,
        fill: ds.color.text.primary,
      });
    }
  });
});

describe('the small text that follows a value', () => {
  const followerOf = (follower: Follower, fs: number): TextItem => {
    const spec = { name: 'f', label: 'Fuel', value: { sample: '38.4', chars: { digits: 3, specials: 1 }, fs, follower } };
    const drawn = field(spec, 0, 200, 'companion').find((i) => i.name.endsWith('.unit') || i.name.endsWith('.denominator'));
    if (drawn?.kind !== 'text') throw new Error('no follower drawn');
    return drawn;
  };

  // The colour was lost by a spread: `field` builds its options with `color: follower.color`, so a
  // follower with no colour of its own passed the key as `undefined` and the unit element's default
  // was overwritten with it, leaving every unit in text.label #5A6069.
  test('a unit with no colour of its own is text.secondary, at the density small size', () => {
    const drawn = followerOf({ text: 'L' }, 64);
    expect({ color: drawn.textColor, size: drawn.fontSize, font: drawn.font }).toEqual({
      color: ds.color.text.secondary,
      size: ds.size.labelSm,
      font: ds.font.label,
    });
  });

  test('a unit keeps a colour it does declare', () => {
    expect(followerOf({ text: 'L', color: ds.color.text.primary }, 64).textColor).toBe(ds.color.text.primary);
  });

  test('a unit sits six pixels after the value and a denominator eight', () => {
    const valueEnd = monoWidth(cells('SemiBold', 64), { digits: 3, specials: 1 });
    expect(followerOf({ text: 'L' }, 64).rect.left).toBe(Math.round(valueEnd + UNIT_GAP));
    expect(followerOf({ kind: 'denominator', text: '/ 24' }, 64).rect.left).toBe(Math.round(valueEnd + DENOMINATOR_GAP));
  });

  test('a denominator is a numeral at 0.7 of the value it follows', () => {
    for (const [valueFs, size] of [
      [46, 32],
      [64, 44],
    ] as const) {
      const drawn = followerOf({ kind: 'denominator', text: '/ 24' }, valueFs);
      expect({ valueFs, size: drawn.fontSize, font: drawn.font, color: drawn.textColor }).toEqual({
        valueFs,
        size,
        font: ds.font.data,
        color: ds.color.text.secondary,
      });
    }
  });
});

describe('the track module has a titled and a titleless form', () => {
  const frame = rect(10, 20, 300, 200);
  const build = (title?: boolean) => MODULES.find((m) => m.id === 'track')!.build({ frame, density: 'zone', prefix: 'track.', title });
  const mapIn = (items: Item[]): StaticMapItem => items.find((i): i is StaticMapItem => i.kind === 'staticMap')!;

  test('names the track above its map by default, which is what the companion and the pit wall draw', () => {
    const items = build();
    expect(items.map((i) => i.name)).toEqual(['track.title', 'track.state', 'track.map']);
    expect(mapIn(items).rect.top).toBeGreaterThan(frame.top);
  });

  test('puts the surface state at the right of that header, measured for its longest reading', () => {
    const [title, state] = build() as [TextItem, TextItem];
    expect({ hAlign: state.hAlign, widest: state.widest, text: state.text }).toEqual({ hAlign: 'right', widest: 'MODERATE', text: 'DRY' });
    expect(state.bindings?.Text?.formula).toContain('TrackGripStatus');
    // The name gives up the state's width rather than the two sharing the line: WPF clips, it does
    // not reflow, so a long track name would otherwise be drawn straight through "MODERATE".
    expect(state.rect.left + state.rect.width).toBe(frame.left + frame.width);
    expect(title.rect.left + title.rect.width).toBeLessThanOrEqual(state.rect.left);
  });

  test('gives the map the whole frame when the caller asks for no title', () => {
    const items = build(false);
    expect(items.map((i) => i.name)).toEqual(['track.map']);
    expect(mapIn(items).rect).toMatchObject({ top: frame.top, height: frame.height });
  });

  /**
   * The style the canvas draws the cars and the circuit in, asserted because nothing else would
   * catch it going back.
   *
   * SimHub's defaults are a 12 px dot per car and a bordered track, which is the map the design
   * spent a package replacing: 4 px for the player against 3 for everyone else, one border on the
   * player alone so that a car in traffic is still findable, and a single dim stroke with no
   * border under it. Class colours stay off, as `track.ts` explains.
   */
  test('draws the cars and the outline in the style the canvas asks for', () => {
    const map = mapIn(build(false));
    expect({
      player: { radius: map.playerStyle?.dotRadius, border: map.playerStyle?.dotBorderThickness },
      opponent: { radius: map.opponentStyle?.dotRadius, border: map.opponentStyle?.dotBorderThickness },
      trackColor: map.trackColor,
      trackBorderWidth: map.trackBorderWidth,
      classColors: map.overrideColorsWithCarClassColors,
      startLine: map.startLine?.enabled,
    }).toEqual({
      player: { radius: 4, border: 1 },
      opponent: { radius: 3, border: 0 },
      trackColor: ds.color.text.dim,
      trackBorderWidth: 0,
      classColors: false,
      startLine: true,
    });
  });

  test('strokes the circuit at a weight cut from the map rather than the same line in every box', () => {
    const at = (w: number, h: number): number => mapIn(MODULES.find((m) => m.id === 'track')!.build({ frame: rect(0, 0, w, h), density: 'zone', prefix: 'track.', title: false })).trackWidth!;
    // The catalogue's own 566 by 220 map is where the 2.5 came from; a pit wall zone and a tall
    // face zone are the two that were drawing it at the same weight.
    expect(at(566, 220)).toBe(2.5);
    expect(at(607, 158)).toBeLessThan(at(445, 516));
  });
});

describe('the inputs page', () => {
  const build = (w: number, h: number, density: Density = 'zone'): Item[] => MODULES.find((m) => m.id === 'inputs')!.build({ frame: rect(0, 0, w, h), density, prefix: 'inputs.' });
  const named = (items: Item[], name: string): Item => items.find((i) => i.name === name)!;

  test('gives each pedal its own reading, drawn at the density small size', () => {
    const at = (w: number, h: number, density: Density): { text: string; fontSize: number }[] =>
      build(w, h, density)
        .filter((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.value'))
        .map((i) => ({ text: i.text, fontSize: i.fontSize }));
    // Three different numbers: Dash Studio draws the samples, and three identical ones said
    // nothing about which bar belongs to which pedal.
    expect(at(802, 336, 'companion')).toEqual([
      { text: '76', fontSize: 34 },
      { text: '12', fontSize: 34 },
      { text: '0', fontSize: 34 },
    ]);
    expect(at(607, 158, 'zone').map((v) => v.fontSize)).toEqual([24, 24, 24]);
    // d.tiny is 14 at compact, which density.ts's own comment puts below the readable floor.
    expect(at(245, 156, 'compact').map((v) => v.fontSize)).toEqual([18, 18, 18]);
  });

  test('draws the bars at the width the catalogue gives them and no legend under the plot', () => {
    const barsOf = (w: number, h: number, density: Density): number[] => build(w, h, density).filter((i) => i.name.endsWith('.bar')).map((i) => (i as { rect: Rect }).rect.width);
    // 20 at the fullest drawing and 16 at the other three, which follows the catalogue rather than
    // the density: the 1920 x 480 face's wide zone is drawn at 20 and its 469 px grid zone at 16.
    expect(barsOf(802, 336, 'companion')).toEqual([20, 20, 20]);
    expect(barsOf(737, 270, 'zone')).toEqual([20, 20, 20]);
    expect(barsOf(607, 158, 'zone')).toEqual([16, 16, 16]);
    expect(barsOf(437, 510, 'zone')).toEqual([16, 16, 16]);
    // Every line has its own bar and its own number beside it in the same colour, so a legend row
    // would repeat the labelling and cost the plot 18 px of height. The three quarter hairlines
    // are the whole of the grid the catalogue draws: the rule along the bottom belongs to the pit
    // wall's telemetry panels, which are plots under a title rather than bars standing on a line.
    const chrome = build(802, 336, 'companion').filter((i) => /\.(legend|swatch|baseline)$/.test(i.name));
    expect(chrome).toEqual([]);
    expect(build(802, 336, 'companion').filter((i) => /\.grid\d$/.test(i.name))).toHaveLength(3);
  });

  test('takes its sample count from the plot it is given rather than from the density', () => {
    const pointsOf = (w: number, h: number, density: Density): number[] => build(w, h, density).filter((i) => i.kind === 'chart').map((i) => (i as { pointsCount?: number }).pointsCount!);
    // The canvas draws 101 points across a 566 px plot, which is a sample every six pixels; the
    // companion's plot is what the bars, the numbers and the steering leave it.
    expect(pointsOf(802, 336, 'companion')).toEqual([85, 85, 85]);
    // And a zone that is not wide enough to be finer keeps the floor rather than a shorter window.
    expect(pointsOf(245, 156, 'compact')).toEqual([60, 60, 60]);
  });

  test('ends with the steering, which is a declared part the narrow shapes drop', () => {
    const steerIn = (items: Item[]): string[] => items.filter((i) => i.name.startsWith('inputs.steer')).map((i) => i.name);
    expect(steerIn(build(600, 280))).toEqual(['inputs.steer.rim', 'inputs.steer.mark', 'inputs.steer.label']);
    expect(steerIn(build(430, 300))).toHaveLength(3);
    expect(steerIn(build(274, 300))).toEqual([]);
    expect(steerIn(build(360, 470))).toEqual([]);
  });

  test('and moves the mark round the rim rather than turning the rim, since SimHub binds no rotation', () => {
    const items = build(600, 280);
    const mark = named(items, 'inputs.steer.mark');
    const left = mark.bindings?.Left?.formula ?? '';
    const top = mark.bindings?.Top?.formula ?? '';
    expect(left).toContain('SteeringWheelAngle');
    expect(left).toContain('sin(');
    expect(top).toContain('cos(');
    // Clamped to the lock the pit wall's own steering trace is drawn at, so full lock is full lock
    // and not a mark that has come round past the top again.
    expect(left).toContain('min(max(');
    expect(left).toContain('3.5');
    // Where the two formulas put the mark on a wheel that is straight is expressions.test.ts.
    expect((named(items, 'inputs.steer.label') as TextItem).text).toBe('STEER');
  });
});

describe('the radar is cut from its box', () => {
  const build = (w: number, h: number, density: Density = 'zone'): Item[] => MODULES.find((m) => m.id === 'radar')!.build({ frame: rect(0, 0, w, h), density, prefix: 'radar.' });
  const radarIn = (items: Item[]): RadarItem => items.find((i): i is RadarItem => i.kind === 'radar')!;

  test('the scale follows the plot rather than the density, which is what rule 18 means here', () => {
    // The two boxes readability-pass.md §16 puts side by side: a nano zone and a tall face zone
    // were drawing the same twenty metres of track at the same scale.
    expect(radarIn(build(249, 158)).scale).toBeLessThan(radarIn(build(437, 510)).scale!);
    // And the companion page keeps the 1.25 the canvas was measured at.
    expect(radarIn(build(802, 336, 'companion')).scale!).toBeCloseTo(1.25, 1);
  });

  test('the grid the canvas draws under the cars is four rects behind the plot', () => {
    const items = build(802, 336, 'companion');
    const plot = radarIn(items).rect;
    const grid = items.filter((i): i is RectangleItem => i.kind === 'rect' && /\.(grid\d|centre)$/.test(i.name));
    expect(grid.map((i) => i.name)).toEqual(['radar.grid0', 'radar.grid1', 'radar.grid2', 'radar.centre']);
    for (const line of grid) {
      expect({ name: line.name, colour: line.backgroundColor }).toMatchObject({ colour: ds.color.surface.raised });
      expect(line.rect.left).toBeGreaterThanOrEqual(plot.left);
      expect(line.rect.left + line.rect.width).toBeLessThanOrEqual(plot.left + plot.width);
    }
    // Behind, not over: SimHub paints the items in order and the radar's own background is clear.
    expect(items.findIndex((i) => i.name === 'radar.centre')).toBeLessThan(items.findIndex((i) => i.kind === 'radar'));
  });

  test('the player is the canvas block over the plot centre rather than a dot of the item', () => {
    const items = build(802, 336, 'companion');
    const radar = radarIn(items);
    const you = items.find((i): i is RectangleItem => i.name === 'radar.you')!;
    expect({ colour: you.backgroundColor, width: you.rect.width, height: you.rect.height }).toEqual({ colour: ds.color.text.primary, width: 24, height: 44 });
    // The item draws the player at its own centre, so the block is only true where it sits on it.
    expect(you.rect.left + you.rect.width / 2).toBeCloseTo(radar.rect.left + radar.rect.width / 2, 0);
    expect(you.rect.top + you.rect.height / 2).toBeCloseTo(radar.rect.top + radar.rect.height / 2, 0);
    // Over, not behind, and with nothing of the item's own left under it.
    expect(items.findIndex((i) => i.name === 'radar.you')).toBeGreaterThan(items.findIndex((i) => i.kind === 'radar'));
    expect(radar.playerStyle?.dotRadius).toBe(0);
  });

  test('the other cars wear the canvas fill and stroke as a dot border', () => {
    const opponents = radarIn(build(802, 336, 'companion')).opponentStyle;
    expect(opponents).toMatchObject({ dotColor: ds.color.surface.raised, dotBorderColor: ds.color.text.dim, dotBorderThickness: 1, dotRadius: 12 });
  });

  for (const box of moduleBoxes()) {
    test(`the grid and the player's block stay in the plot on a ${box.name}`, () => {
      const items = MODULES.find((m) => m.id === 'radar')!.build({ frame: box.frame, density: box.density, prefix: 'radar.' });
      const plot = radarIn(items).rect;
      const drawn = items.filter((i): i is RectangleItem => i.kind === 'rect' && /\.(grid\d|centre|you)$/.test(i.name));
      expect(drawn.map((i) => i.name).sort()).toEqual(['radar.centre', 'radar.grid0', 'radar.grid1', 'radar.grid2', 'radar.you']);
      for (const mark of drawn) expect({ box: box.name, mark: mark.name, rect: mark.rect, inside: contains(plot, mark.rect) }).toMatchObject({ inside: true });
    });
  }

  test('the spotter flanks are a proportion of the width and turn red on the side being called', () => {
    const flanksOf = (w: number, h: number): RectangleItem[] => build(w, h).filter((i): i is RectangleItem => i.name === 'radar.left' || i.name === 'radar.right');
    expect(flanksOf(802, 336).map((i) => i.rect.width)).toEqual([64, 64]);
    expect(flanksOf(607, 158).map((i) => i.rect.width)).toEqual([51, 51]);
    expect(flanksOf(249, 158).map((i) => i.rect.width)).toEqual([21, 21]);
    for (const flank of flanksOf(607, 158)) {
      const formula = flank.bindings?.BackgroundColor?.formula ?? '';
      expect(formula).toContain(flank.name.endsWith('left') ? 'SpotterCarLeft' : 'SpotterCarRight');
      expect(formula).toContain(ds.purpose.delta.slower);
    }
  });
});

describe('what iRacing cannot answer', () => {
  test('the three unavailable modules say so instead of drawing zeros', () => {
    const companion = PACKAGES[0]!.pkg.dashboards[0]!;
    for (const id of ['energy', 'damage', 'trackRivals']) {
      const screen = companion.screens.find((s) => s.name === id)!;
      const texts = itemsOf({ ...companion, screens: [screen] }).filter((i): i is TextItem => i.kind === 'text');
      const placeholder = texts.find((t) => t.name.endsWith('.placeholder'));
      expect({ id, text: placeholder?.text }).toMatchObject({ text: expect.stringContaining('NOT A') });
      expect(placeholder?.textColor).toBe(ds.color.text.dim);
    }
  });
});

/**
 * The wide car-telemetry zone, against `pitwalltower1920x1080` and the wide zone sheet.
 *
 * The row is drawn twice on the canvas, at the 1279 by 240 reference frame and at the 1039 by 255
 * the tower page carries, and the two agree on three things a single drawing would have left
 * looking like one author's arithmetic: the settings column is 380 px at both, the row keeps a page
 * padding of slack at its right edge at both, and the plot is 28 px shorter than the content box at
 * both. The numbers below are the 1039 px frame's half of that, so a change to the split fails here
 * rather than by looking plausible on a screen nobody has open.
 */
describe('the wide car-telemetry page', () => {
  const { body } = zoneFrame('probe', { frame: rect(0, 0, 1039, 255), title: 'CAR TELEMETRY · WIDE', counter: { kind: 'static', page: 6, pages: 6 } });
  const items = pageBuilder('carTelemetry')({ frame: body, density: 'wide', prefix: 'carTelemetry.' }).flatMap((i) => [...walkItems([i])]);
  const named = (suffix: string): Exclude<Item, { kind: 'layer' }>[] => items.filter((i): i is Exclude<Item, { kind: 'layer' }> => i.kind !== 'layer' && i.name.endsWith(suffix));
  const charts = items.filter((i): i is ChartItem => i.kind === 'chart');
  const plot = charts[0]!.rect;

  test('splits the content box the way the sheet does', () => {
    expect({ width: body.width, height: body.height }).toEqual({ width: 1007, height: 211 });
    // Both traces share the plot, and the plot is the canvas's 587 by 183.
    expect([...new Set(charts.map((c) => `${c.rect.width}x${c.rect.height}`))]).toEqual(['587x183']);
    // The row is the plot, the canvas's 24 px gap, the 380 px settings column, and the page padding
    // of slack the sheet leaves at the right edge.
    expect(plot.width + 24 + 380 + 16).toBe(body.width);
    const labels = named('.label').map((i) => i.rect);
    expect(labels[0]!.left - (plot.left + plot.width)).toBe(24);
    // Equal cells three across a 380 px column, which is the shape of the sheet's nine-cell grid.
    expect([...new Set(labels.map((r) => r.width))]).toEqual([Math.floor((380 - 2 * 18) / 3)]);
    expect([...new Set(labels.map((r) => r.left))]).toHaveLength(3);
  });

  test('stands the settings grid on the plot rather than in the middle of the box', () => {
    // `align-items: flex-end` on the sheet's row: the two halves share a bottom edge. The block of
    // cells is short, so bottom-aligned and centred are far apart and easy to tell apart.
    const cells = [...named('.label'), ...named('.value')].map((i) => i.rect);
    expect(Math.min(...cells.map((r) => r.top))).toBeGreaterThan(plot.top + plot.height / 2);
    const foot = Math.max(...cells.map((r) => r.top + r.height));
    // The last line is placed on the plot's bottom edge; what hangs below it is the line box's own
    // tail, which is transparent and is what `every module fits the box it is given` allows for.
    expect(foot - (plot.top + plot.height)).toBeLessThanOrEqual(Math.ceil(0.25 * 16) + 2);
    expect(foot).toBeGreaterThan(plot.top + plot.height - LINE_SPACING * 16);
  });

  test('draws the midline alone, where the telemetry panels draw their quarters', () => {
    const grid = items.filter((i): i is RectangleItem => i.kind === 'rect' && /\.grid\d$/.test(i.name));
    expect(grid).toHaveLength(1);
    expect(grid[0]!.backgroundColor).toBe(ds.color.surface.raised);
    expect(grid[0]!.rect.top).toBe(Math.round(plot.top + plot.height / 2));
    // And no rule under the plot: the sheet closes a panel drawn under a title, not this row.
    expect(named('.baseline')).toEqual([]);
  });

  test('keeps the legend outside the plot, in the band under the row', () => {
    const legend = named('.legend') as TextItem[];
    expect(legend.map((i) => i.text)).toEqual(['THROTTLE', 'BRAKE']);
    // The swatches are drawn boxes and land squarely in the band; a label is a WPF line box and
    // starts a tenth of its size above the line it was given, so it is measured by its foot.
    for (const swatch of named('.swatch')) expect(swatch.rect.top).toBeGreaterThanOrEqual(plot.top + plot.height);
    for (const entry of legend) expect(entry.rect.top + entry.rect.height).toBeGreaterThan(plot.top + plot.height);
  });

  test('lists the cells the catalogue names, in its order and at the size it sets them', () => {
    expect((named('.label') as TextItem[]).map((i) => i.text)).toEqual(['TC', 'BB', 'MAP', 'ABS']);
    // Pinned at the sheet's 16 px rather than grown by rule 20, which drew them at 29 and the car
    // number at 41: the grid is a reference beside a trace, and the trace is the reading.
    expect([...new Set((named('.value') as TextItem[]).map((i) => i.fontSize))]).toEqual([16]);
    // The car line and the anti-roll bars belong to the car settings page and not to this one.
    expect(items.filter((i) => /\.(car|arbFront|arbRear)\./.test(i.name))).toEqual([]);
  });

  test('and leaves the telemetry column its four lines a plot', () => {
    const pitwall = PACKAGES.find((p) => p.def.folder === 'openDash Pit wall')!.pkg.dashboards[0]!;
    const screen = pitwall.screens.find((s) => s.name === 'telemetry')!;
    const lines = new Map<string, number>();
    for (const item of itemsOf({ ...pitwall, screens: [screen] })) {
      const match = /^(.*)\.(?:grid\d|baseline)$/.exec(item.name);
      if (match) lines.set(match[1]!, (lines.get(match[1]!) ?? 0) + 1);
    }
    // The five panels the column stacks, each with its quarters and the rule that closes it.
    expect(lines.size).toBeGreaterThanOrEqual(5);
    expect([...new Set(lines.values())]).toEqual([4]);
  });
});
