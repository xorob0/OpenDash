/**
 * The zone face: that it is the five parts, that every rectangle is the artboard's, that every
 * zone cycles the catalogue it should, and that nothing it draws leaves its box.
 *
 * The geometry assertions are literal on purpose. These numbers are read off
 * `design/canvas/Dash.dc.html` and tabulated in `docs/design/zones.md`; deriving them here would
 * be a second opinion about the design rather than a check on the code.
 */
import { describe, expect, test } from 'bun:test';
import { BAND_D_PAGES, FACE_SIZES, FACE_ZONE_LETTERS, MODULE_COUNT, ZONE_A_PAGES, bodyOrder, facePrefix, pagesForZone, zoneProperties } from '../src/contract.ts';
import { validatePackage, type TextItem, type WidgetItem } from '../src/generator.ts';
import { PROPERTY_PREFIX, declaredProperties } from '../src/contract.ts';
import { LINE_SPACING } from '../src/design/metrics.ts';
import { measureText } from '../src/design/advances.ts';
import { fontsForPackage } from '../src/dashboard.ts';
import { itemsOf, propertiesIn, walkItems } from '../src/walk.ts';
import { MODULES } from '../src/modules/index.ts';
import { rect } from '../src/design/geometry.ts';
import { ZONE_FACES, bandCorners, bandPageItems, bar, buildZoneFace, sizeOf, faceItems, kindOf, rectOf, zoneDashboardName, zoneFace1920x480 } from '../src/zones/index.ts';
import { cellOverruns, faceOf } from './monoGlyphs.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const BUILT = ZONE_FACES.map((face) => ({ face, built: buildZoneFace(face, OPTS) }));
// Looked up by identity rather than by folder name, which moved to plain "openDash" in XOR-118.
const reference = BUILT.find((b) => b.face === zoneFace1920x480)!;

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
  const widgets = itemsOf(main).filter((i): i is WidgetItem => i.kind === 'widget');

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
    // Which is also why a face carries three zone dashboards rather than four.
    expect(reference.built.zones).toHaveLength(3);
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
    // speed sat hard against the left edge of a 380 px column.
    const a = rectOf(zoneFace1920x480, 'A');
    const shared = reference.built.zones.find((d) => d.name === zoneDashboardName('zoneA', { width: a.width, height: a.height }))!;
    const page = shared.screens.find((s) => s.name === 'gearSpeedRevs')!;
    const speed = [...walkItems(page.items)].find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('speed'))!;
    expect(speed.hAlign).toBe('center');
    expect(speed.rect.left + speed.rect.width / 2).toBeCloseTo(a.width / 2, 0);
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
    const items = textsIn(bar({ left: 0, top: 48, width: 1920, height: 56 }, 'bar.', { fieldsPerEnd: 2, face: sizeOf(zoneFace1920x480) })).filter((i) => i.name.startsWith('bar.strip.'));
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
    const items = textsIn(bar({ left: 0, top: 48, width: 1920, height: 56 }, 'bar.', { fieldsPerEnd: 2, face: sizeOf(zoneFace1920x480) }));
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
