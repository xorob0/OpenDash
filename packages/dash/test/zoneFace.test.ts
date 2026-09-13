/**
 * The zone face: that it is the five parts, that every rectangle is the artboard's, that every
 * zone cycles the catalogue it should, and that nothing it draws leaves its box.
 *
 * The geometry assertions are literal on purpose. These numbers are read off
 * `design/canvas/Dash.dc.html` and tabulated in `docs/design/zones.md`; deriving them here would
 * be a second opinion about the design rather than a check on the code.
 */
import { describe, expect, test } from 'bun:test';
import { BAND_D_PAGES, FACE_ZONE_LETTERS, MODULE_CATALOGUE, MODULE_COUNT, ZONE_A_PAGES, pagesForZone, zoneCounterReadings, zoneProperties } from '../src/contract.ts';
import { validatePackage, type TextItem, type WidgetItem } from '../src/generator.ts';
import { PROPERTY_PREFIX, declaredProperties } from '../src/contract.ts';
import { LINE_SPACING } from '../src/design/metrics.ts';
import { measureText } from '../src/design/advances.ts';
import { fontsForPackage } from '../src/dashboard.ts';
import { itemsOf, propertiesIn, walkItems } from '../src/walk.ts';
import { rect } from '../src/design/geometry.ts';
import { shapeOf } from '../src/second/shape.ts';
import { zoneFrame } from '../src/second/header.ts';
import { ZONE_FACES, buildZoneFace, faceItems, kindOf, rectOf, zoneDashboardName, zoneFace1920x480 } from '../src/zones/index.ts';
import { cellOverruns, faceOf } from './monoGlyphs.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const BUILT = ZONE_FACES.map((face) => ({ face, built: buildZoneFace(face, OPTS) }));
const reference = BUILT.find((b) => b.face.folder === 'openDash zones 1920x480')!;

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
      expect(String(formula)).toContain(`${PROPERTY_PREFIX}.Zone${zone}`);
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
    for (const zone of FACE_ZONE_LETTERS) expect(used).toContain(`${PROPERTY_PREFIX}.Zone${zone}`);
    for (const p of zoneProperties().filter((n) => n.includes('.Bar'))) expect(used).toContain(p);
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
    .replace(new RegExp(`isnull\\(\\[OpenDash\\.Zone${zone}Pages\\], \\d+\\)`, 'g'), String(mask))
    .replace(new RegExp(`isnull\\(\\[OpenDash\\.Zone${zone}\\], \\d+\\)`, 'g'), String(page))
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
      expect(expression).toContain(`OpenDash.Zone${zone}Pages`);
      expect(expression).toContain(`OpenDash.Zone${zone}`);
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
    for (const { face, built } of BUILT) {
      const items = itemsOf(built.main).filter((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.counter'));
      expect(items.length).toBe(2);
      for (const item of items) {
        const zone = item.name.startsWith('zoneB') ? 'B' : 'C';
        for (const reading of zoneCounterReadings(zone)) {
          const width = measureText('BarlowMedium', reading, item.fontSize);
          expect({ face: face.folder, item: item.name, reading, fits: width <= item.rect.width }).toMatchObject({ fits: true });
        }
      }
    }
  });
});
