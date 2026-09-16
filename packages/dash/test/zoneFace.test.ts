/**
 * The zone face: that it is the five parts, that every rectangle is the artboard's, that every
 * zone cycles the catalogue it should, and that nothing it draws leaves its box.
 *
 * The geometry assertions are literal on purpose. These numbers are read off
 * `design/canvas/Dash.dc.html` and tabulated in `docs/design/zones.md`; deriving them here would
 * be a second opinion about the design rather than a check on the code.
 */
import { describe, expect, test } from 'bun:test';
import {
  BAND_D_PAGES,
  FACE_SIZES,
  FACE_ZONE_LETTERS,
  MODULE_CATALOGUE,
  MODULE_COUNT,
  ZONE_A_PAGES,
  bodyOrder,
  facePrefix,
  pagesForZone,
  setting,
  zoneCounterReadings,
  zoneProperties,
} from '../src/contract.ts';
import { validatePackage, type Dashboard, type RectangleItem, type TextItem, type WidgetItem } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';
import { PROPERTY_PREFIX, declaredProperties } from '../src/contract.ts';
import { LINE_SPACING, boxSlack } from '../src/design/metrics.ts';
import { measureText } from '../src/design/advances.ts';
import { fontsForPackage } from '../src/dashboard.ts';
import { itemsOf, propertiesIn, walkItems } from '../src/walk.ts';
import { MODULES } from '../src/modules/index.ts';
import { rect } from '../src/design/geometry.ts';
import { shapeOf } from '../src/second/shape.ts';
import { zoneFrame } from '../src/second/header.ts';
import {
  BAR_FIELD_SPECS,
  BASE_FACE,
  FACE_SCREEN_NAME,
  FACE_SCREEN_NAME_NO_REV_BAR,
  LARGE_FACE,
  ZONE_FACES,
  bandCorners,
  bandPageItems,
  bar,
  buildZoneFace,
  sizeOf,
  faceItems,
  kindOf,
  layoutWithoutRevBar,
  rectOf,
  revBarReclaim,
  zoneDashboardName,
  zoneFace1920x480,
  zoneFace600x686,
  zoneFace800x286,
  zoneFace1280x400,
  zoneFace1280x480,
  zoneFace1280x720,
  zoneFace850x480,
  type ZoneLayout,
} from '../src/zones/index.ts';
import { cellOverruns, faceOf } from './monoGlyphs.ts';
import { SCREEN_PACKAGES, buildScreenPackage } from '../src/screens/index.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const BUILT = ZONE_FACES.map((face) => ({ face, built: buildZoneFace(face, OPTS) }));
/** The companion and the pit walls, to check the face's new options stay off their screens. */
const SECOND_SCREENS = SCREEN_PACKAGES.map((def) => buildScreenPackage(def, OPTS));
// Looked up by identity rather than by folder name, which moved to plain "openDash" in XOR-118.
const reference = BUILT.find((b) => b.face === zoneFace1920x480)!;
/** Every zone property carries its face's prefix, so a test that names one has to say whose. */
const REFERENCE = facePrefix(sizeOf(zoneFace1920x480));

describe('the base face and the large one', () => {
  // Named constants rather than folder strings, because the two sizes are a product decision
  // docs/scope.md holds and README.md photographs, and the scripts read them from here.
  test('the base is 850 by 480 and the large one 1280 by 480', () => {
    expect({ folder: BASE_FACE.folder, width: BASE_FACE.width, height: BASE_FACE.height }).toEqual({ folder: 'openDash 850x480', width: 850, height: 480 });
    expect({ folder: LARGE_FACE.folder, width: LARGE_FACE.width, height: LARGE_FACE.height }).toEqual({ folder: 'openDash 1280x480', width: 1280, height: 480 });
  });

  test('both ship', () => {
    expect(ZONE_FACES).toContain(BASE_FACE);
    expect(ZONE_FACES).toContain(LARGE_FACE);
  });

  // The pair earns its place by being two shapes rather than two sizes of one: the base stacks a
  // page into a tall narrow zone, the large one tabulates it into a wide one.
  test('their zones are different shapes, not the same shape scaled', () => {
    const base = BASE_FACE.zones.zoneB;
    const large = LARGE_FACE.zones.zoneB;
    expect(base.width).toBeLessThan(base.height);
    expect(large.width).toBeGreaterThan(large.height);
  });

  test('neither is the reference face', () => {
    expect(BASE_FACE).not.toBe(zoneFace1920x480);
    expect(LARGE_FACE).not.toBe(zoneFace1920x480);
  });
});

describe('the reference face is the artboard', () => {
  const z = zoneFace1920x480.zones;

  test('1920 by 480, and five parts', () => {
    expect({ width: zoneFace1920x480.width, height: zoneFace1920x480.height }).toEqual({ width: 1920, height: 480 });
    expect(z.revBarWell).toEqual({ left: 18, top: 4, width: 1884, height: 40 });
    expect(z.revBar).toEqual({ left: 24, top: 8, width: 1872, height: 32 });
    expect(z.bar).toEqual({ left: 0, top: 48, width: 1920, height: 56 });
    expect(z.zoneB).toEqual({ left: 0, top: 105, width: 769, height: 314 });
    expect(z.zoneA).toEqual({ left: 770, top: 105, width: 380, height: 314 });
    expect(z.zoneC).toEqual({ left: 1151, top: 105, width: 769, height: 314 });
    expect(z.band).toEqual({ left: 0, top: 420, width: 1920, height: 60 });
  });

  test('the body is B, then A, then C, with a pixel between them', () => {
    // The order matters: zone A is in the middle because it holds the gear, and the gear is the
    // one thing read by reflex. B and C flank it because they hold tables, which are read
    // deliberately.
    expect(z.zoneB.left + z.zoneB.width).toBe(z.zoneA.left - 1);
    expect(z.zoneA.left + z.zoneA.width).toBe(z.zoneC.left - 1);
    expect(z.zoneC.left + z.zoneC.width).toBe(zoneFace1920x480.width);
  });

  test('zone A is a narrow column, not a third of the screen', () => {
    // The whole point of the model. A twelve-slot face gave the hero 382 of 1920; a zone face gives
    // zone A 380 and the rest to two zones that show twenty-one pages each.
    expect(z.zoneA.width).toBeLessThan(z.zoneB.width);
    expect(z.zoneA.height).toBe(z.zoneB.height);
  });

  test('nothing overlaps and nothing is off the canvas', () => {
    const parts = [z.revBarWell, z.bar!, z.zoneB, z.zoneA, z.zoneC, z.band];
    for (const p of parts) {
      expect({ p, inside: p.left >= 0 && p.top >= 0 && p.left + p.width <= 1920 && p.top + p.height <= 480 }).toMatchObject({ inside: true });
    }
    // The rev bar sits inside its well, which is what makes the well read as recessed.
    expect(z.revBar.left).toBeGreaterThanOrEqual(z.revBarWell.left);
    expect(z.revBar.top).toBeGreaterThanOrEqual(z.revBarWell.top);
    expect(z.revBar.left + z.revBar.width).toBeLessThanOrEqual(z.revBarWell.left + z.revBarWell.width);
  });
});

describe('every zone cycles its own catalogue', () => {
  const main = reference.built.main;
  // The face as drawn with the rev bar. The second arrangement carries the same four widgets, so
  // reading both screens would count every zone twice.
  const mainScreen = main.screens.find((s) => s.name === FACE_SCREEN_NAME)!;
  const widgets = itemsOf({ ...main, screens: [mainScreen] }).filter((i): i is WidgetItem => i.kind === 'widget');

  test('four zones, four widgets', () => {
    expect(widgets.map((w) => w.name)).toEqual(['zoneA', 'zoneB', 'zoneC', 'zoneD']);
  });

  test('each widget points at a dashboard of its own catalogue', () => {
    for (const zone of FACE_ZONE_LETTERS) {
      const widget = widgets.find((w) => w.name === `zone${zone}`)!;
      const r = rectOf(zoneFace1920x480, zone);
      expect(widget.fileName).toBe(`${zoneDashboardName(kindOf(zone), { width: r.width, height: r.height })}.djson`);
      expect({ zone, rect: { left: widget.rect.left, top: widget.rect.top, width: widget.rect.width, height: widget.rect.height } }).toMatchObject({ rect: r });
    }
  });

  test('and its screen index is bound to the zone property, which is what a button advances', () => {
    for (const zone of FACE_ZONE_LETTERS) {
      const widget = widgets.find((w) => w.name === `zone${zone}`)!;
      const formula = widget.bindings?.InitialScreenIndex?.formula;
      expect({ zone, bound: formula !== undefined }).toMatchObject({ bound: true });
      expect(String(formula)).toContain(`${PROPERTY_PREFIX}.${facePrefix(sizeOf(reference.face))}Zone${zone}`);
    }
  });

  test('zones B and C share one dashboard, because they are the same box and the same catalogue', () => {
    const b = widgets.find((w) => w.name === 'zoneB')!;
    const c = widgets.find((w) => w.name === 'zoneC')!;
    expect(b.fileName).toBe(c.fileName);
    // Which is also why each arrangement of the face needs three zone dashboards rather than four:
    // three for the face as drawn, and two more for the rectangles the rev-bar-off arrangement
    // grows. Band D is the same rectangle in both, so it is not built twice.
    expect(reference.built.zones).toHaveLength(5);
  });

  test('the catalogues are the sizes the contract declares', () => {
    const byName = new Map(reference.built.zones.map((d) => [d.name, d]));
    expect(byName.get(zoneDashboardName('zoneA', { width: 380, height: 314 }))!.screens).toHaveLength(ZONE_A_PAGES.length);
    expect(byName.get(zoneDashboardName('module', { width: 769, height: 314 }))!.screens).toHaveLength(MODULE_COUNT);
    expect(byName.get(zoneDashboardName('band', { width: 1920, height: 60 }))!.screens).toHaveLength(BAND_D_PAGES.length);
    expect(pagesForZone('A')).toHaveLength(4);
    expect(pagesForZone('D')).toHaveLength(8);
  });
});

/**
 * The contract carries a little of each face's shape, because the plugin draws a plan of the face in
 * its panel and cannot read a layout file. A plan drawn to one face's proportions for every face is
 * how the nano at 800 x 286 came to be offered bar fields for a bar it does not have, so the two
 * descriptions have to agree.
 */
describe('the contract describes the shape each face really has', () => {
  test('every layout is named by FACE_SIZES, and named once', () => {
    expect(FACE_SIZES).toHaveLength(ZONE_FACES.length);
    const named = FACE_SIZES.map((f) => `${f.width}x${f.height}`);
    expect(new Set(named).size).toBe(named.length);
    for (const layout of ZONE_FACES) expect(named).toContain(`${layout.width}x${layout.height}`);
  });

  for (const layout of ZONE_FACES) {
    test(`${layout.folder} is described as it is drawn`, () => {
      const face = sizeOf(layout);
      const z = layout.zones;
      expect({ folder: layout.folder, hasBar: face.hasBar }).toMatchObject({ hasBar: z.bar !== undefined });
      expect({ folder: layout.folder, per: face.barFieldsPerEnd }).toMatchObject({ per: layout.barFieldsPerEnd });

      // A body whose three zones share a left edge is stacked; one that does not is a row.
      const stacked = z.zoneA.left === z.zoneB.left && z.zoneB.left === z.zoneC.left;
      expect({ folder: layout.folder, body: face.body }).toMatchObject({ body: stacked ? 'column' : 'row' });

      // The parts are the sizes along whichever axis the body runs, in drawing order.
      const order = bodyOrder(face).map((letter) => (letter === 'A' ? z.zoneA : letter === 'B' ? z.zoneB : z.zoneC));
      const drawn = order.map((r) => (stacked ? r.height : r.width));
      expect({ folder: layout.folder, parts: [...face.parts] }).toMatchObject({ parts: drawn });

      // And the four rows the panel's plan scales from, which are the face's own and not constants.
      // The body is the whole region the three zones occupy, the one-pixel seams included, so that a
      // stacked body measures the same way as a body laid side by side.
      const rows = { revBar: z.bar ? z.bar.top : order[0]!.top, bar: z.bar ? z.bar.height : 0, body: z.band.top - order[0]!.top - 1, band: z.band.height };
      expect({ folder: layout.folder, rows: { ...face.rows } }).toMatchObject({ rows });
      // And they add up to the face, seam by seam, which is what makes them a plan rather than a list.
      expect({ folder: layout.folder, sum: rows.revBar + rows.bar + rows.body + rows.band }).toMatchObject({ sum: layout.height - (z.bar ? 2 : 1) });
    });
  }

  test('and the drawing order is the one the design settled on', () => {
    // B, A, C across a wide face, because zone A holds the gear and the gear is read by reflex; A
    // over B over C in portrait, for the same reason with the axis turned.
    expect(bodyOrder(sizeOf(zoneFace1920x480))).toEqual(['B', 'A', 'C']);
    const portrait = ZONE_FACES.find((f) => f.width === 600)!;
    expect(bodyOrder(sizeOf(portrait))).toEqual(['A', 'B', 'C']);
  });
});

describe('the face reads what it declares and nothing else', () => {
  test('every package validates with no error and no warning', () => {
    for (const { face, built } of BUILT) {
      const pkg = { folderName: face.folder, dashboards: [built.main, ...built.zones], fonts: fontsForPackage() };
      const result = validatePackage(pkg, { declaredProperties: declaredProperties(), propertyPrefix: PROPERTY_PREFIX });
      expect({ folder: face.folder, errors: result.errors }).toMatchObject({ errors: [] });
      expect({ folder: face.folder, warnings: result.warnings }).toMatchObject({ warnings: [] });
    }
  });

  test('it reads the zone properties, which the card face does not', () => {
    const used = new Set(
      [reference.built.main, ...reference.built.zones].flatMap((d) => propertiesIn(d)).filter((p) => p.startsWith(`${PROPERTY_PREFIX}.`)),
    );
    for (const zone of FACE_ZONE_LETTERS) expect(used).toContain(`${PROPERTY_PREFIX}.${facePrefix(sizeOf(reference.face))}Zone${zone}`);
    for (const p of zoneProperties().filter((n) => n.includes(`${facePrefix(sizeOf(reference.face))}Bar`))) expect(used).toContain(p);
    // And nothing belonging to another face, which is the point of the prefix: this package must
    // not move when somebody configures the 850 beside it.
    const others = zoneProperties().filter((n) => !n.includes(facePrefix(sizeOf(reference.face))));
    expect([...used].filter((p) => others.includes(p))).toEqual([]);
    // And no slot, because a zone is not a slot.
    expect([...used].some((p) => p.includes('.Slot'))).toBe(false);
  });
});

describe('nothing the face draws is clipped', () => {
  for (const { face, built } of BUILT) {
    test(`${face.folder} fits every text in its box`, () => {
      for (const dashboard of [built.main, ...built.zones]) {
        const texts = itemsOf(dashboard).filter((i): i is TextItem => i.kind === 'text');
        expect(texts.length).toBeGreaterThan(0);
        for (const item of texts) {
          const drawn = item.widest ?? item.text;
          const mono = item.monospace;
          const width = mono
            ? (drawn.length - [...drawn].filter((c) => mono.specialChars?.includes(c) ?? false).length) * mono.charWidth +
              [...drawn].filter((c) => mono.specialChars?.includes(c) ?? false).length * mono.specialCharsWidth
            : measureText(faceOf(item), drawn, item.fontSize);
          expect({ dashboard: dashboard.name, item: item.name, drawn, width, box: item.rect.width, fits: width <= item.rect.width }).toMatchObject({ fits: true });
          const line = LINE_SPACING * item.fontSize;
          expect({ item: item.name, line, box: item.rect.height, fits: line <= item.rect.height }).toMatchObject({ fits: true });
        }
      }
    });

    test(`${face.folder} draws no glyph that overruns its cell`, () => {
      for (const dashboard of [built.main, ...built.zones]) {
        for (const item of itemsOf(dashboard)) {
          if (item.kind !== 'text' || !item.monospace) continue;
          expect({ dashboard: dashboard.name, overruns: cellOverruns(item) }).toMatchObject({ overruns: [] });
        }
      }
    });

    test(`${face.folder} keeps every item on its canvas`, () => {
      for (const dashboard of [built.main, ...built.zones]) {
        for (const item of itemsOf(dashboard)) {
          if (item.kind === 'layer') continue;
          const r = item.rect;
          const inside = r.left >= 0 && r.top >= 0 && r.left + r.width <= dashboard.width && r.top + r.height <= dashboard.height;
          expect({ dashboard: dashboard.name, item: item.name, rect: r, inside }).toMatchObject({ inside: true });
        }
      }
    });

    test(`${face.folder} names every item once per screen`, () => {
      for (const dashboard of [built.main, ...built.zones]) {
        for (const screen of dashboard.screens) {
          const names = itemsOf({ ...dashboard, screens: [screen] }).map((i) => i.name);
          expect({ dashboard: dashboard.name, screen: screen.name, duplicates: names.filter((n, i) => names.indexOf(n) !== i) }).toMatchObject({ duplicates: [] });
        }
      }
    });
  }
});

describe('the parts the face draws itself', () => {
  const all = [...walkItems(faceItems(zoneFace1920x480))];
  const names = all.map((i) => i.name);

  test('the rev bar sits in a well, which the first token file named and nothing ever drew', () => {
    expect(names).toContain('well');
    expect(names.filter((n) => n.startsWith('revBar')).length).toBeGreaterThan(0);
  });

  test('the flag takes band D rather than having a strip of its own', () => {
    const band = zoneFace1920x480.zones.band;
    const flag = all.filter((i) => i.name.startsWith('flag'));
    expect(flag.length).toBeGreaterThan(0);
    // Every part of it is inside band D. The slot model drew a strip along the bottom edge and the
    // band is where that sixty pixels went, so a flag that fell outside the band would mean the
    // face had grown a second one.
    for (const item of flag) {
      if (item.kind === 'layer') continue;
      const r = item.rect;
      const inside = r.top >= band.top && r.top + r.height <= band.top + band.height + 8 && r.left >= band.left && r.left + r.width <= band.left + band.width;
      expect({ item: item.name, rect: r, inside }).toMatchObject({ inside: true });
    }
  });

  test('the pit limiter covers zone A rather than taking room of its own', () => {
    const z = zoneFace1920x480.zones;
    expect(z.pitLimiter.left).toBeGreaterThanOrEqual(z.zoneA.left);
    expect(z.pitLimiter.left + z.pitLimiter.width).toBeLessThanOrEqual(z.zoneA.left + z.zoneA.width);
    expect(names).toContain('pitLimiter');
  });

  test('the bar is drawn and is not a zone', () => {
    expect(names.some((n) => n.startsWith('bar.'))).toBe(true);
    // It carries no widget, which is what "does not cycle" means in the scene graph.
    const widgets = faceItems(zoneFace1920x480).filter((i) => i.kind === 'widget');
    expect(widgets.every((w) => !w.name.startsWith('bar'))).toBe(true);
  });
});

/**
 * The face is ruled across as well as down. The build drew the two vertical rules between the zones
 * from the first zone face and neither of the horizontal ones, although every rectangular artboard
 * separates the bar from the body and the body from band D with the same pixel.
 */
describe('the hairlines across the face', () => {
  const hairlines = (layout: ZoneLayout, revBar: boolean): RectangleItem[] =>
    faceItems(layout, { revBar }).filter((i): i is RectangleItem => i.kind === 'rect' && i.name.startsWith('rule.') && i.rect.height === 1);

  test('every face rules the row above each zone row and above band D, in surface.raised', () => {
    for (const face of ZONE_FACES) {
      for (const [layout, on] of [
        [face, true],
        [layoutWithoutRevBar(face), false],
      ] as const) {
        const z = layout.zones;
        // One row per distinct zone top -- one on a face whose body is a row, three on the portrait
        // face that stacks its zones -- and one for the band. The nano with its rev bar off starts
        // its body on row 1, where a rule would be the face's top edge rather than a boundary.
        const wanted = [...new Set([z.zoneA.top, z.zoneB.top, z.zoneC.top, z.band.top])].sort((a, b) => a - b).filter((top) => top > 1);
        const drawn = hairlines(layout, on);
        expect({ face: face.folder, revBar: on, tops: drawn.map((r) => r.rect.top) }).toMatchObject({ tops: wanted.map((top) => top - 1) });
        for (const r of drawn) {
          expect({ face: face.folder, rule: r.name, rect: r.rect, colour: r.backgroundColor }).toMatchObject({
            rect: { left: 0, width: layout.width, height: 1 },
            colour: ds.color.surface.raised,
          });
        }
      }
    }
  });

  test('and they land where the artboards draw them', () => {
    // Read off the artboards, which place each as `left: 0; width: <face>; height: 1px;
    // background: #1C1F24`. Literal here for the same reason every other rect in this file is.
    const drawn = (layout: ZoneLayout): number[] => hairlines(layout, true).map((r) => r.rect.top);
    expect(drawn(zoneFace1920x480)).toEqual([104, 419]);
    expect(drawn(zoneFace1280x480)).toEqual([98, 419]);
    expect(drawn(zoneFace1280x400)).toEqual([86, 345]);
    expect(drawn(zoneFace1280x720)).toEqual([104, 659]);
    expect(drawn(zoneFace850x480)).toEqual([90, 419]);
    expect(drawn(zoneFace800x286)).toEqual([32, 227]);
    expect(drawn(zoneFace600x686)).toEqual([82, 317, 478, 629]);
  });

  test('each sits in an empty row, over nothing and under nothing', () => {
    for (const face of ZONE_FACES) {
      const z = face.zones;
      const parts = [z.revBarWell, ...(z.bar ? [z.bar] : []), z.zoneA, z.zoneB, z.zoneC, z.band];
      for (const r of hairlines(face, true)) {
        const clash = parts.filter((p) => r.rect.top >= p.top && r.rect.top < p.top + p.height);
        expect({ face: face.folder, rule: r.name, row: r.rect.top, clash }).toMatchObject({ clash: [] });
      }
    }
  });
});

/**
 * The three things the first capture of the 1920 face on the VM caught, which no test had an
 * opinion about. Each is the same shape of mistake: something measured or written in one place and
 * drawn in another.
 */
describe('what the first photograph of the face showed', () => {
  const items = faceItems(zoneFace1920x480);
  const texts = items.filter((i): i is TextItem => i.kind === 'text');

  test('the zone letter comes from the face, because B and C share one dashboard', () => {
    const b = texts.find((t) => t.name === 'zoneB.letter')!;
    const c = texts.find((t) => t.name === 'zoneC.letter')!;
    expect(b.text).toBe('B');
    expect(c.text).toBe('C');
    // Zone A and band D carry no header, so they get no letter.
    expect(texts.some((t) => t.name === 'zoneA.letter' || t.name === 'zoneD.letter')).toBe(false);

    // And no page of the shared dashboard carries a letter of its own: one file cannot say both.
    const shared = reference.built.zones.find((d) => d.name === zoneDashboardName('module', { width: 769, height: 314 }))!;
    for (const screen of shared.screens) {
      const title = [...walkItems(screen.items)].find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.zone.title'));
      expect(title).toBeDefined();
      expect(title!.text).not.toMatch(/^[ABCD] /);
    }
  });

  test('each letter sits where its zone will draw its title, not over the page name', () => {
    for (const zone of ['B', 'C'] as const) {
      const letter = texts.find((t) => t.name === `zone${zone}.letter`)!;
      const r = rectOf(zoneFace1920x480, zone);
      expect(letter.rect.left).toBeGreaterThanOrEqual(r.left);
      // Inside the zone's own padding, and clear of the page name that follows it.
      expect(letter.rect.left - r.left).toBeLessThan(24);
      expect(letter.rect.top).toBeGreaterThanOrEqual(r.top);
      expect(letter.rect.top).toBeLessThan(r.top + 28);
    }
  });

  test('a value centred in a zone is centred in the zone, not in its own glyphs', () => {
    // `maxWidth` caps a box; it cannot widen one, so hAlign had nothing to centre within and the
    // speed sat hard against the left edge of a 380 px column. The unit now sits beside the value
    // on its baseline rather than under it, so what the column centres is the pair: centring the
    // value alone would put the group's own centre half a unit's width left of the column's.
    const a = rectOf(zoneFace1920x480, 'A');
    const shared = reference.built.zones.find((d) => d.name === zoneDashboardName('zoneA', { width: a.width, height: a.height }))!;
    const page = shared.screens.find((s) => s.name === 'gearSpeedRevs')!;
    const items = [...walkItems(page.items)];
    const speed = items.find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('speed'))!;
    const unit = items.find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('speed.unit'))!;
    expect(speed.rect.left).toBeGreaterThan(0);
    const centre = (speed.rect.left + unit.rect.left + unit.rect.width) / 2;
    // Within a pixel: the boxes are integer-snapped and the unit's takes a pixel past its advances.
    expect({ centre, column: a.width / 2, centred: Math.abs(centre - a.width / 2) <= 1 }).toMatchObject({ centred: true });
  });

  test('the ghosted gears are mapped from text, because SimHub publishes the gear as a string', () => {
    const a = rectOf(zoneFace1920x480, 'A');
    const shared = reference.built.zones.find((d) => d.name === zoneDashboardName('zoneA', { width: a.width, height: a.height }))!;
    const page = shared.screens.find((s) => s.name === 'gearSpeedRevs')!;
    const above = [...walkItems(page.items)].find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('gear.above'))!;
    const below = [...walkItems(page.items)].find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('gear.below'))!;
    const bind = (item: TextItem): string => String(item.bindings!.Text!.formula);

    // `[Gear] + 1` is string concatenation: in third gear it evaluated to "31", and a cell one
    // character wide drew the 3, so the right-hand ghost showed the gear the car was already in.
    expect(bind(above)).not.toContain('+');
    expect(bind(below)).not.toContain('+');
    // Third gear maps to fourth above and second below.
    expect(bind(above)).toContain("if(([DataCorePlugin.GameData.Gear]) = ('3'), '4'");
    expect(bind(below)).toContain("if(([DataCorePlugin.GameData.Gear]) = ('3'), '2'");
    // Neutral and reverse match nothing and draw nothing, and so does the gear below first.
    expect(bind(below)).not.toContain("= ('1'), '0'");
  });
});

/**
 * A field the game does not publish is removed and the rank closes over the hole; a telltale that
 * is unlit keeps its place and goes dim. Both rules live in `second/rank.ts`, and these are the
 * three places on the face that ask for one of them.
 */
describe('what the face does with a field that is not there', () => {
  const band = { left: 0, top: 420, width: 1920, height: 60 };
  const textsIn = (items: readonly ReturnType<typeof bandPageItems>[number][]): TextItem[] => items.filter((i): i is TextItem => i.kind === 'text');
  const bound = (item: TextItem, target: 'Left' | 'Visible' | 'TextColor' | 'Text'): string | undefined => {
    const b = item.bindings?.[target];
    return b && b.mode === 'formula' && typeof b.formula === 'string' ? b.formula : undefined;
  };

  test("the bar's strip closes over a setting the car does not have", () => {
    const items = textsIn(bar({ left: 0, top: 48, width: 1920, height: 56 }, 'bar.', { fieldsPerEnd: 2, face: sizeOf(zoneFace1920x480), scale: zoneFace1920x480.bar! })).filter((i) => i.name.startsWith('bar.strip.'));
    expect(items.length).toBeGreaterThan(0);
    for (const item of items) {
      // Hidden when the sim has no property for it, and everything moves when one goes.
      expect({ name: item.name, hides: bound(item, 'Visible') !== undefined, moves: bound(item, 'Left') !== undefined }).toEqual({
        name: item.name,
        hides: true,
        moves: true,
      });
    }
    // The cell that goes first is the one a driver would give up first, not the last drawn.
    expect(bound(items.find((i) => i.name === 'bar.strip.diff.value')!, 'Left')).toContain('dcTractionControl2');
  });

  test("band D's car page removes a gauge the sim does not wire, and keeps the temperatures", () => {
    const items = textsIn(bandPageItems('car', band, 'car.'));
    const water = items.find((i) => i.name === 'car.water.value')!;
    const voltage = items.find((i) => i.name === 'car.voltage.value')!;
    expect(bound(voltage, 'Visible')).toContain('Voltage');
    expect(bound(water, 'Visible')).toBeUndefined();
    // The temperature does not vanish, but it does move: the rank recentres on what is left.
    expect(bound(water, 'Left')).toContain('Voltage');
  });

  test("the strip asks the property that says the car has the setting, not the one it reads", () => {
    // SimHub reports TCLevel 0 for a car with no traction control at all, which is what a driver
    // who has turned it off also sees. The raw dc field is absent on the car that has none, and
    // that is the difference between a cell drawn as OFF and a cell that is not there.
    const items = textsIn(bar({ left: 0, top: 48, width: 1920, height: 56 }, 'bar.', { fieldsPerEnd: 2, face: sizeOf(zoneFace1920x480), scale: zoneFace1920x480.bar! }));
    const tc = items.find((i) => i.name === 'bar.strip.tc.value')!;
    expect(bound(tc, 'Visible')).toBe('!(isnull([DataCorePlugin.GameRawData.Telemetry.dcTractionControl]))');
    expect(bound(tc, 'Text')).toContain('[DataCorePlugin.GameData.TCLevel]');
  });

  test('a module rank closes over a setting the car does not have', () => {
    // Zones B and C draw the modules, so the same contract has to hold inside a page: the car
    // settings grid hid a field the sim does not publish and left its gap where it had been.
    const settings = MODULES.find((m) => m.id === 'carSettings')!;
    const items = textsIn(settings.build({ frame: rect(0, 0, 600, 280), density: 'zone', prefix: '' }));
    const abs = items.find((i) => i.name === 'abs.value')!;
    expect(bound(abs, 'Visible')).toBe('!(isnull([DataCorePlugin.GameRawData.Telemetry.dcABS]))');
    expect(bound(abs, 'Left')).toContain('dcTractionControl');
  });

  test('the corner lamps dim in place rather than vanishing', () => {
    const items = textsIn(bandCorners(band, 'corner.'));
    for (const id of ['drs', 'p2p', 'spt']) {
      const lamp = items.find((i) => i.name === `corner.${id}`)!;
      expect({ id, colour: bound(lamp, 'TextColor') !== undefined, hides: bound(lamp, 'Visible') !== undefined, moves: bound(lamp, 'Left') !== undefined }).toEqual({
        id,
        colour: true,
        hides: false,
        moves: false,
      });
    }
  });
});

/**
 * Band D is the same three blocks as the bar, on every face that draws its corners: the left
 * corner, the page rank, the right corner. The bar got this test when BIAS was found sitting on
 * POSITION; the band never did, and page D6 Sectors was six pixels into the DRS lamp at 1280.
 */
describe('band D keeps its rank clear of its corners', () => {
  for (const { face, built } of BUILT) {
    if (!face.bandCorners) continue;
    const band = face.zones.band;
    const dashboard = built.zones.find((d) => d.name === zoneDashboardName('band', { width: band.width, height: band.height }))!;

    for (const screen of dashboard.screens) {
      test(`${face.folder} draws no field of ${screen.name} over a corner block`, () => {
        const items = [...walkItems(screen.items)].filter((i): i is TextItem => i.kind === 'text');
        const extent = (of: (name: string) => boolean): { left: number; right: number } | null => {
          const group = items.filter((i) => of(i.name));
          if (group.length === 0) return null;
          return { left: Math.min(...group.map((i) => i.rect.left)), right: Math.max(...group.map((i) => i.rect.left + i.rect.width)) };
        };
        // A corner item is named "<page>.corner.<field>"; everything else on the screen is the rank.
        const corner = (name: string): boolean => name.includes('.corner.');
        const leftNames = ['incidents', 'trackState'];
        const left = extent((n) => corner(n) && leftNames.some((f) => n.includes(`.corner.${f}`)));
        const right = extent((n) => corner(n) && !leftNames.some((f) => n.includes(`.corner.${f}`)));
        const rank = extent((n) => !corner(n));

        expect({ screen: screen.name, left: left !== null, right: right !== null }).toMatchObject({ left: true, right: true });
        if (rank) {
          expect({ screen: screen.name, clearOfLeft: rank.left >= left!.right }).toMatchObject({ clearOfLeft: true });
          expect({ screen: screen.name, clearOfRight: rank.right <= right!.left }).toMatchObject({ clearOfRight: true });
          expect(rank.left).toBeGreaterThanOrEqual(0);
          expect(rank.right).toBeLessThanOrEqual(band.width);
        }
      });
    }
  }
});

/**
 * The bar is three blocks on one line -- the left end, the car settings strip, the right end -- and
 * every text-fits check in the suite is satisfied by three blocks drawn on top of one another. At
 * 850 by 480 they were: BIAS sat on POSITION and ABS on the slash of "3 / 24".
 */
/**
 * The bar has a scale of its own, read off each artboard rather than taken from the zone density
 * ramp: a 15 px label, a value, a dimmer denominator six pixels after it, and the gap between two
 * readouts. The numbers are the artboards' own, so they are asserted literally.
 */
describe('the bar is drawn at the scale its artboard draws', () => {
  /** The side padding every artboard gives the bar. */
  const PAD_X = 20;
  /** The gap between a value and the dimmer denominator after it. */
  const DENOMINATOR_GAP = 6;

  test('every face that has a bar says what scale to draw it at', () => {
    for (const face of ZONE_FACES) {
      expect({ face: face.folder, scale: face.bar !== undefined }).toMatchObject({ scale: face.zones.bar !== undefined });
    }
  });

  for (const { face } of BUILT) {
    const frame = face.zones.bar;
    const scale = face.bar;
    if (!frame || !scale) continue;
    const items = (): TextItem[] => faceItems(face).filter((i): i is TextItem => i.kind === 'text' && i.name.startsWith('bar.'));

    test(`${face.folder} labels at ${ds.size.label} over values of ${scale.valueSize} and denominators of ${scale.denominatorSize}`, () => {
      const expected = {
        label: { size: ds.size.label, color: ds.color.text.label },
        denominator: { size: scale.denominatorSize, color: ds.color.text.secondary },
        value: { size: scale.valueSize, color: ds.color.text.primary },
      };
      for (const item of items()) {
        const kind = item.name.endsWith('.label') ? 'label' : item.name.endsWith('.denominator') ? 'denominator' : 'value';
        expect({ item: item.name, size: item.fontSize, color: item.textColor }).toMatchObject(expected[kind]);
      }
    });

    test(`${face.folder} spaces its readouts ${scale.gap} apart and ends its right end at the padding`, () => {
      const drawn = items();
      const labelOf = (slot: string): TextItem => drawn.find((i) => i.name.startsWith(`bar.${slot}.`) && i.name.endsWith('.label'))!;
      if (face.barFieldsPerEnd === 2) {
        for (const end of ['Left', 'Right']) {
          const first = labelOf(`${end}1`);
          expect({ end, step: labelOf(`${end}2`).rect.left - first.rect.left }).toMatchObject({ step: first.rect.width + scale.gap });
        }
      }
      // Every field of the outermost right slot ends against the padding, whatever it is made of:
      // a value, or a value and the denominator after it.
      const outer = `bar.Right${face.barFieldsPerEnd}.`;
      const fields = new Set(drawn.filter((i) => i.name.startsWith(outer)).map((i) => i.name.split('.').slice(0, 3).join('.')));
      for (const field of fields) {
        const parts = drawn.filter((i) => i.name.startsWith(`${field}.`));
        expect({ field, right: Math.max(...parts.map((i) => i.rect.left + i.rect.width)) }).toMatchObject({ right: frame.left + frame.width - PAD_X });
        for (const part of parts) expect({ item: part.name, hAlign: part.hAlign }).toMatchObject({ hAlign: 'right' });
      }
    });

    test(`${face.folder} sets a denominator ${DENOMINATOR_GAP} after the value and on its baseline`, () => {
      const drawn = items();
      const lap = (part: string): TextItem => drawn.find((i) => i.name === `bar.Left1.lap.${part}`)!;
      const value = lap('value');
      const cells = value.rect.width - boxSlack(scale.valueSize);
      expect(lap('denominator').rect.left - (value.rect.left + cells)).toBe(DENOMINATOR_GAP);
      // A WPF box puts its baseline one em below its top, so two runs sit on one baseline exactly
      // when their top and their size add up to the same number.
      expect(lap('denominator').rect.top + scale.denominatorSize).toBe(value.rect.top + scale.valueSize);
    });
  }
});

/**
 * The catalogue an end draws from, written as the zone catalogue artboard writes it. Ten fields
 * rather than the artboard's eleven: strength of field is published by SimHub in no form at all
 * and ADR 0009 settled that a field which can never have a value is not offered.
 */
describe('the bar draws the catalogue the artboard writes', () => {
  test('ten fields, each with the label and the reading the artboard gives it', () => {
    expect(BAR_FIELD_SPECS.map((spec) => [spec.label, spec.sample, spec.denominator?.sample ?? ''])).toEqual([
      ['Race', '0:28:14', ''],
      ['Lap', '4', '/ 32'],
      ['Time left', '0:42:15', ''],
      ['Clock', '14:32', ''],
      ['Real time', '19:26', ''],
      ['Position', '3', '/ 22'],
      ['Class', 'GT3 · P4', ''],
      ['Incidents', '3x', ''],
      ['Air', '21.5', '°'],
      ['Track', '27.6', '°'],
    ]);
  });

  test('and shortens the one label the portrait artboard shortens', () => {
    const labelOn = (face: typeof zoneFace600x686, slot: string): string =>
      faceItems(face).find((i): i is TextItem => i.kind === 'text' && i.name === `bar.${slot}.position.label`)!.text;
    expect(labelOn(zoneFace600x686, 'Right1')).toBe('POS');
    expect(labelOn(zoneFace1920x480, 'Right1')).toBe('POSITION');
  });
});

describe('the bar keeps its three blocks apart', () => {
  for (const { face } of BUILT) {
    const bar = face.zones.bar;
    if (!bar) continue;

    test(`${face.folder} draws no cell over a field`, () => {
      const items = faceItems(face).filter((i): i is TextItem => i.kind === 'text' && i.name.startsWith('bar.'));
      const extent = (prefix: string): { left: number; right: number } | null => {
        const group = items.filter((i) => i.name.startsWith(prefix));
        if (group.length === 0) return null;
        return {
          left: Math.min(...group.map((i) => i.rect.left)),
          right: Math.max(...group.map((i) => i.rect.left + i.rect.width)),
        };
      };

      // Every alternative of one slot is drawn at the same place with its Visible bound, so blocks
      // are compared rather than items: only one of each slot's ten is ever on screen.
      const left = extent('bar.Left');
      const right = extent('bar.Right');
      const strip = extent('bar.strip');
      expect(left).not.toBeNull();
      expect(right).not.toBeNull();

      if (strip) {
        expect(strip.left).toBeGreaterThanOrEqual(left!.right);
        expect(strip.right).toBeLessThanOrEqual(right!.left);
      }
      expect(left!.right).toBeLessThanOrEqual(right!.left);
      expect(left!.left).toBeGreaterThanOrEqual(bar.left);
      expect(right!.right).toBeLessThanOrEqual(bar.left + bar.width);
    });
  }
});

describe('the twenty-one pages reach the face', () => {
  const size = { width: 769, height: 314 };
  const shared = reference.built.zones.find((d) => d.name === zoneDashboardName('module', size))!;

  test('zones B and C cycle the module catalogue, and it is the catalogue the companion cycles', () => {
    expect(shared.screens.map((s) => s.name)).toEqual(MODULE_CATALOGUE.map((m) => m.id));
    for (const zone of ['B', 'C'] as const) {
      const r = rectOf(zoneFace1920x480, zone);
      expect({ zone, width: r.width, height: r.height }).toEqual({ zone, ...size });
    }
  });

  test('every page of the catalogue draws, and draws inside a 769 by 314 zone at shape wide', () => {
    // The ticket's own measure. The zone is 769 by 314; what a page is handed is the body under the
    // header, and the shape model is asked what that body is before anything is drawn into it.
    const { body } = zoneFrame('probe', { frame: rect(0, 0, size.width, size.height), title: 'PROBE', counter: { kind: 'static', page: 1, pages: MODULE_COUNT } });
    expect(shapeOf(body).width).toBe('wide');

    for (const screen of shared.screens) {
      const items = [...walkItems(screen.items)].filter((i) => i.kind !== 'layer');
      expect({ page: screen.name, drew: items.length > 0 }).toMatchObject({ drew: true });
      for (const item of items) {
        const r = item.rect;
        const inside = r.left >= 0 && r.top >= -2 && r.left + r.width <= size.width && r.top + r.height <= size.height + 2;
        expect({ page: screen.name, item: item.name, rect: r, inside }).toMatchObject({ inside: true });
      }
    }
  });
});

/**
 * Evaluates the counter's arithmetic by turning it into the JavaScript it already almost is.
 *
 * This is not an NCalc interpreter and is not trying to be one -- that is XOR-20. The counter uses
 * six things (a property, `isnull`, `truncate`, `if`, `format` and arithmetic), every one of which
 * has a JavaScript spelling, so substituting the two properties and renaming three calls is enough
 * to ask the real expression what it answers. The point is that the arithmetic is checked against a
 * plain count rather than against itself.
 */
function readCounter(expression: string, zone: 'B' | 'C', page: number, mask: number): string {
  const js = expression
    .replace(new RegExp(`isnull\\(\\[OpenDash\\.${REFERENCE}Zone${zone}Pages\\], \\d+\\)`, 'g'), String(mask))
    .replace(new RegExp(`isnull\\(\\[OpenDash\\.${REFERENCE}Zone${zone}\\], \\d+\\)`, 'g'), String(page))
    .replace(/\bif\(/g, 'iff(')
    .replace(/\btruncate\(/g, 'Math.trunc(')
    .replace(/\bformat\(/g, 'fmt(');
  expect(js).not.toContain('OpenDash.');
  const iff = (c: boolean, a: unknown, b: unknown): unknown => (c ? a : b);
  const fmt = (v: number): string => String(Math.round(v));
  return new Function('iff', 'fmt', `return ${js};`)(iff, fmt) as string;
}

describe('a zone counts its cycle, not its catalogue', () => {
  const texts = faceItems(zoneFace1920x480).filter((i): i is TextItem => i.kind === 'text');

  test('the counter comes from the face too, because only the face knows whose mask is deciding', () => {
    for (const zone of ['B', 'C'] as const) {
      const counter = texts.find((t) => t.name === `zone${zone}.counter`)!;
      expect(counter).toBeDefined();
      const formula = counter.bindings?.Text;
      expect(formula).toBeDefined();
      const expression = (formula as { formula: string }).formula;
      expect(expression).toContain(`OpenDash.${REFERENCE}Zone${zone}Pages`);
      expect(expression).toContain(`OpenDash.${REFERENCE}Zone${zone}`);
      // Bound text is measured by its widest reading, not by the sample it was written with.
      expect(counter.widest).toBeDefined();
    }
    expect(texts.some((t) => t.name === 'zoneA.counter' || t.name === 'zoneD.counter')).toBe(false);
  });

  test('and no page of the shared dashboard counts for itself, which would count to twenty-one', () => {
    const shared = reference.built.zones.find((d) => d.name === zoneDashboardName('module', { width: 769, height: 314 }))!;
    const counters = itemsOf(shared).filter((i) => i.name.endsWith('.zone.counter'));
    expect(counters).toEqual([]);
  });

  test('a mask of any length reads as that length, and the page reads as its place in it', () => {
    const expression = ((texts.find((t) => t.name === 'zoneB.counter')!.bindings!.Text as { formula: string }).formula);
    const all = (1 << MODULE_COUNT) - 1;

    // Everything on: the counter is the catalogue, which is what it always used to say.
    expect(readCounter(expression, 'B', 0, all)).toBe(`1 / ${MODULE_COUNT}`);
    expect(readCounter(expression, 'B', MODULE_COUNT - 1, all)).toBe(`${MODULE_COUNT} / ${MODULE_COUNT}`);

    // Three pages on -- lap times, fuel and the relative -- is a cycle of three whichever of them
    // is showing, and the twenty-one is nowhere on the screen.
    const three = (1 << 0) | (1 << 4) | (1 << 14);
    expect(readCounter(expression, 'B', 0, three)).toBe('1 / 3');
    expect(readCounter(expression, 'B', 4, three)).toBe('2 / 3');
    expect(readCounter(expression, 'B', 14, three)).toBe('3 / 3');

    // One page on is a cycle of one, which is a zone that does not move when the button is pressed.
    expect(readCounter(expression, 'B', 6, 1 << 6)).toBe('1 / 1');

    // And every mask of every length agrees with a plain count of the bits below the page.
    for (const mask of [all, three, 0b101010101010101010101, 0b11, 1 << 20]) {
      for (let page = 0; page < MODULE_COUNT; page++) {
        if ((mask & (1 << page)) === 0) continue;
        const before = [...Array(page).keys()].filter((i) => (mask & (1 << i)) !== 0).length;
        const length = [...Array(MODULE_COUNT).keys()].filter((i) => (mask & (1 << i)) !== 0).length;
        expect({ mask, page, read: readCounter(expression, 'B', page, mask) }).toMatchObject({ read: `${before + 1} / ${length}` });
      }
    }
  });

  test('every reading of the counter fits the box it is measured for', () => {
    // Per screen, because a face is two arrangements of itself and the rev-bar-off one hands its
    // zones a taller box: two counters on each, and every reading has to fit on both.
    for (const { face, built } of BUILT) {
      for (const screen of built.main.screens) {
        const items = itemsOf({ ...built.main, screens: [screen] }).filter((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.counter'));
        expect({ face: face.folder, screen: screen.name, counters: items.length }).toMatchObject({ counters: 2 });
        for (const item of items) {
          const zone = item.name.startsWith('zoneB') ? 'B' : 'C';
          for (const reading of zoneCounterReadings(zone)) {
            const width = measureText('BarlowMedium', reading, item.fontSize);
            expect({ face: face.folder, screen: screen.name, item: item.name, reading, fits: width <= item.rect.width }).toMatchObject({ fits: true });
          }
        }
      }
    }
  });
});

describe('a zone may list the class a driver is racing in', () => {
  /** The row lookup a table's cells address their car through, which is what the filter changes. */
  const rowLookups = (dashboard: Dashboard, page: string): string[] => {
    const screen = dashboard.screens.find((s) => s.name === page)!;
    return itemsOf({ ...dashboard, screens: [screen] })
      .flatMap((item) => Object.values(item.bindings ?? {}))
      .map((b) => (b as { formula?: string }).formula ?? '')
      .filter((f) => f.includes('repeatindex()'));
  };

  test('the leaderboard and the relative in zone B or C ask whose zone is showing them', () => {
    const shared = reference.built.zones.find((d) => d.name === zoneDashboardName('module', { width: 769, height: 314 }))!;
    for (const page of ['leaderboard', 'relative']) {
      const lookups = rowLookups(shared, page);
      expect({ page, found: lookups.length > 0 }).toMatchObject({ found: true });
      for (const lookup of lookups) {
        // Both zones, because one file serves both and a page in it cannot know which is showing it.
        expect({ page, lookup }).toMatchObject({ lookup: expect.stringContaining(`OpenDash.${REFERENCE}ZoneBClassOnly`) });
        expect({ page, lookup }).toMatchObject({ lookup: expect.stringContaining(`OpenDash.${REFERENCE}ZoneCClassOnly`) });
        // And the class-only twin of the lookup it would otherwise use.
        expect(lookup).toContain('playerclassonly');
      }
    }
  });

  test('a page with nobody to list does not read the setting at all', () => {
    const shared = reference.built.zones.find((d) => d.name === zoneDashboardName('module', { width: 769, height: 314 }))!;
    for (const page of ['fuel', 'tyres', 'sectors', 'gear']) {
      const screen = shared.screens.find((s) => s.name === page)!;
      const used = propertiesIn({ ...shared, screens: [screen] });
      expect({ page, used: used.filter((p) => p.endsWith('ClassOnly')) }).toMatchObject({ used: [] });
    }
  });

  test('a portrait face gives B and C a file each, and neither reads the other zone', () => {
    // 600 x 686 stacks A over B over C, so the two are different rectangles and cannot share.
    const portrait = BUILT.find((b) => b.face === zoneFace600x686)!;
    const prefix = facePrefix(sizeOf(zoneFace600x686));
    const b = rectOf(portrait.face, 'B');
    const c = rectOf(portrait.face, 'C');
    expect({ b: b.height, c: c.height }).not.toMatchObject({ b: c.height });

    for (const [zone, r, other] of [
      ['B', b, 'C'],
      ['C', c, 'B'],
    ] as const) {
      const file = portrait.built.zones.find((d) => d.name === zoneDashboardName('module', { width: r.width, height: r.height }))!;
      const lookups = rowLookups(file, 'leaderboard');
      expect({ zone, found: lookups.length > 0 }).toMatchObject({ found: true });
      for (const lookup of lookups) {
        expect({ zone, lookup }).toMatchObject({ lookup: expect.stringContaining(`OpenDash.${prefix}Zone${zone}ClassOnly`) });
        expect(lookup).not.toContain(`OpenDash.${prefix}Zone${other}ClassOnly`);
      }
    }
  });

  test('the companion and the pit wall list everybody, as they always have', () => {
    for (const pkg of SECOND_SCREENS) {
      for (const dashboard of pkg.dashboards) {
        const used = propertiesIn(dashboard).filter((p) => p.endsWith('ClassOnly'));
        expect({ dashboard: dashboard.name, used }).toMatchObject({ used: [] });
      }
    }
  });
});


/**
 * XOR-138: the rev bar off entirely.
 *
 * Unlike every other geometry assertion in this file, the rectangles here are **derived** rather
 * than read off an artboard -- the canvas has not answered what the top of a face without a rev bar
 * looks like, and `docs/design/zones.md` §10 records that. So these tests check the rule rather than
 * a table: what is given back, what does not move, and that both arrangements still fit.
 */
describe('the rev bar can be off entirely', () => {
  const screensOf = (built: (typeof BUILT)[number]['built']): { on: (typeof BUILT)[number]['built']['main']['screens'][number]; off: (typeof BUILT)[number]['built']['main']['screens'][number] } => ({
    on: built.main.screens.find((s) => s.name === FACE_SCREEN_NAME)!,
    off: built.main.screens.find((s) => s.name === FACE_SCREEN_NAME_NO_REV_BAR)!,
  });

  test('a face is two screens, and exactly one of them is ever enabled', () => {
    for (const { face, built } of BUILT) {
      expect({ face: face.folder, screens: built.main.screens.map((s) => s.name) }).toMatchObject({
        screens: [FACE_SCREEN_NAME, FACE_SCREEN_NAME_NO_REV_BAR],
      });
      const { on, off } = screensOf(built);
      // Complementary, so SimHub's screen-role pass always has exactly one to choose.
      expect(off.enabledExpression).toBe(setting.revBarIs('off'));
      expect(on.enabledExpression).toBe(`!(${setting.revBarIs('off')})`);
    }
  });

  test('the shift lights and the plain RPM bar are untouched, and only reachable on the first screen', () => {
    for (const { face, built } of BUILT) {
      const { on, off } = screensOf(built);
      const names = (screen: typeof on): string[] => [...walkItems(screen.items)].map((i) => i.name);
      // Both layers still there, still one setting apart: ADR 0004's two states are not what changed.
      expect({ face: face.folder, shift: names(on).includes('revBar.shiftLights'), rpm: names(on).includes('revBar.rpmBar') }).toMatchObject({ shift: true, rpm: true });
      // And the well goes with them: hiding the segments and keeping the recess is the hole the
      // ticket is about.
      expect({ face: face.folder, drawn: names(off).filter((n) => n === 'well' || n.startsWith('revBar')) }).toMatchObject({ drawn: [] });
    }
  });

  test('what it gives back is the well and the gap under it, and not the face’s top margin', () => {
    for (const { face } of BUILT) {
      const z = face.zones;
      const firstBelow = z.bar?.top ?? Math.min(z.zoneA.top, z.zoneB.top, z.zoneC.top);
      expect({ face: face.folder, reclaim: revBarReclaim(z) }).toMatchObject({ reclaim: firstBelow - z.revBarWell.top });
      // Which is the whole recess plus its gap, and nothing above it.
      expect(revBarReclaim(z)).toBe(z.revBarWell.height + (firstBelow - (z.revBarWell.top + z.revBarWell.height)));
    }
  });

  test('the body grows by exactly that, and nothing measured from the bottom edge moves', () => {
    for (const { face } of BUILT) {
      const z = face.zones;
      const o = layoutWithoutRevBar(face).zones;
      const reclaim = revBarReclaim(z);
      expect({ face: face.folder, width: o.zoneB.width, band: o.band }).toMatchObject({ band: z.band });
      if (z.bar) expect({ face: face.folder, bar: o.bar }).toMatchObject({ bar: { ...z.bar, top: z.bar.top - reclaim } });

      const bodyTop = Math.min(z.zoneA.top, z.zoneB.top, z.zoneC.top);
      for (const key of ['zoneA', 'zoneB', 'zoneC'] as const) {
        const before = z[key];
        const after = o[key];
        const grew = before.top === bodyTop;
        // A zone that starts the body keeps its bottom edge and gains the room above it; one that
        // does not -- zones B and C in portrait, which sit under zone A -- is untouched.
        expect({ face: face.folder, key, after }).toMatchObject({
          after: grew ? { ...before, top: before.top - reclaim, height: before.height + reclaim } : before,
        });
        expect({ face: face.folder, key, bottom: after.top + after.height }).toMatchObject({ bottom: before.top + before.height });
      }
      // The limiter is drawn over zone A and nowhere else, so it moves with it.
      expect({ face: face.folder, offset: o.pitLimiter.top - o.zoneA.top }).toMatchObject({ offset: z.pitLimiter.top - z.zoneA.top });
    }
  });

  test('the reference face gives back 44 of its 480 rows', () => {
    const o = layoutWithoutRevBar(zoneFace1920x480).zones;
    expect(revBarReclaim(zoneFace1920x480.zones)).toBe(44);
    expect(o.bar).toEqual({ left: 0, top: 4, width: 1920, height: 56 });
    expect(o.zoneB).toEqual({ left: 0, top: 61, width: 769, height: 358 });
    expect(o.zoneA).toEqual({ left: 770, top: 61, width: 380, height: 358 });
    expect(o.zoneC).toEqual({ left: 1151, top: 61, width: 769, height: 358 });
    expect(o.band).toEqual({ left: 0, top: 420, width: 1920, height: 60 });
  });

  test('the nano gets a ninth of its screen back, which is what makes the setting worth having', () => {
    // By identity rather than by folder name, the way `reference` is: the folders were renamed in
    // XOR-118 and a string here would have gone on compiling and stopped finding anything.
    const nano = zoneFace800x286;
    expect(ZONE_FACES).toContain(nano);
    const o = layoutWithoutRevBar(nano).zones;
    // The one face with no bar: the body starts straight under the well, so it is the body that
    // rises to the top margin.
    expect(nano.zones.bar).toBeUndefined();
    expect(revBarReclaim(nano.zones)).toBe(32);
    expect(o.zoneB).toEqual({ left: 0, top: 1, width: 269, height: 226 });
    expect(o.zoneA).toEqual({ left: 270, top: 1, width: 260, height: 226 });
  });

  test('every zone the second arrangement needs has a dashboard drawn for it', () => {
    for (const { face, built } of BUILT) {
      const names = new Set(built.zones.map((d) => d.name));
      for (const layout of [face, layoutWithoutRevBar(face)]) {
        for (const zone of FACE_ZONE_LETTERS) {
          const r = rectOf(layout, zone);
          const name = zoneDashboardName(kindOf(zone), { width: r.width, height: r.height });
          expect({ face: face.folder, zone, name, drawn: names.has(name) }).toMatchObject({ drawn: true });
        }
      }
    }
  });
});
