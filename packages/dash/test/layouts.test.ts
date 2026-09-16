/**
 * Every layout, generically: slots inside the canvas and apart, one widget per slot reading its
 * own setting, every card fitting the slot at the layout's rung, every item inside the canvas
 * (rotation included), the contract, no brand colour. Then the exact geometry the sizes spec
 * gives for every rectangular size and every round face (the spec tables, row by row), the
 * nano's small hero, thin strip and card padding, the DisplayDash's hero band over its grid, the
 * 480 round's stacked hero and the 800 round's row hero and card padding, the round-face rules
 * (rev arc segments centred on their circle and clear of the slots, rings on the face's
 * bounding square, nothing outside the disc), the grid cards' cells fitting their columns on
 * every face, and the tyre pressure precision each face can afford.
 */
import { describe, expect, test } from 'bun:test';
import { CARDS } from '../src/cards/index.ts';
import { PRESSURE_TIERS, pressureTier, pressureTierFor } from '../src/cards/tyrePressures.ts';
import { CHEQUER_COUNT, CHEQUER_SIZE, CHEQUER_STEP } from '../src/components/flagRing.ts';
import { FLAG_STRIP_STYLES } from '../src/components/flagStrip.ts';
import { GEAR_SIZES, gear } from '../src/components/gear.ts';
import { gridColumnWidth } from '../src/components/grid2x2.ts';
import { revArcAngle } from '../src/components/revArc.ts';
import { declaredProperties, defaultCardForSlot, slotSettingName } from '../src/contract.ts';
import { buildLayout, buildPackage } from '../src/dashboard.ts';
import { centre, contains, distance, overlaps, rect, type Rect } from '../src/design/geometry.ts';
import { FONT_METRICS, WPF_BASELINE } from '../src/design/metrics.ts';
import { rungForSlot, rungSpec, type Rung } from '../src/design/rung.ts';
import { itemBounds, type Dashboard, type DrawableItem, type EllipseItem, type Item, type LayerItem } from '../src/generator.ts';
import { hero, type HeroGeometry } from '../src/hero/hero.ts';
import {
  cardRung,
  LAYOUTS,
  layout1280x400,
  layout1280x480,
  layout1920x480,
  layout1280x720,
  layout480round,
  layout600x686,
  layout800round,
  layout800x286,
  layout800x480,
  layout850x480,
  rungOf,
  type Layout,
} from '../src/layouts/index.ts';
import { validateOrThrow } from '../src/build.ts';
import { itemsOf, propertiesIn, walkItems } from '../src/walk.ts';

const opts = { version: '0.0.0-test' };
const BRAND = /#(33D9F2|5CE1F5|22909F)/i;
/** Floating point slack on the rotated footprint of an item, in px. */
const EPSILON = 1e-6;

const hasRect = (item: Item): item is DrawableItem => 'rect' in item;

const insideWithin = (outer: Rect, inner: Rect, tolerance: number): boolean =>
  contains({ left: outer.left - tolerance, top: outer.top - tolerance, width: outer.width + 2 * tolerance, height: outer.height + 2 * tolerance }, inner);

const uniqueNamesPerScreen = (d: Dashboard): void => {
  for (const screen of d.screens) {
    const names = [...walkItems(screen.items)].map((i) => i.name);
    expect(new Set(names).size).toBe(names.length);
  }
};

const pad2 = (n: number): string => String(n).padStart(2, '0');

type Point = { x: number; y: number };

/** Corners of a rect rotated by `rotation` degrees clockwise about its centre, y down: SimHub's DrawableItem.Rotation. */
const rotatedCorners = (r: Rect, rotation: number): Point[] => {
  const a = (rotation * Math.PI) / 180;
  const c = centre(r);
  const offsets: [number, number][] = [[-r.width / 2, -r.height / 2], [r.width / 2, -r.height / 2], [r.width / 2, r.height / 2], [-r.width / 2, r.height / 2]];
  return offsets.map(([dx, dy]) => ({ x: c.x + dx * Math.cos(a) - dy * Math.sin(a), y: c.y + dx * Math.sin(a) + dy * Math.cos(a) }));
};

/**
 * True when a rotated item and an axis-aligned rect share any area: the separating axis theorem
 * on the edge normals of both rects. Exact where `itemBounds` is only a bounding box, which
 * matters for the chequered checks, whose boxes reach into the slot corners while the checks
 * themselves stay on the rim.
 */
const rotatedOverlaps = (item: DrawableItem, r: Rect): boolean => {
  const a = rotatedCorners(item.rect, item.rotation ?? 0);
  const b = rotatedCorners(r, 0);
  const normal = (poly: Point[], i: number): Point => ({ x: -(poly[i + 1]!.y - poly[i]!.y), y: poly[i + 1]!.x - poly[i]!.x });
  const axes = [normal(a, 0), normal(a, 1), normal(b, 0), normal(b, 1)];
  const span = (poly: Point[], axis: Point): [number, number] => {
    const d = poly.map((q) => q.x * axis.x + q.y * axis.y);
    return [Math.min(...d), Math.max(...d)];
  };
  return axes.every((axis) => {
    const [aMin, aMax] = span(a, axis);
    const [bMin, bMax] = span(b, axis);
    return aMin < bMax - EPSILON && bMin < aMax - EPSILON;
  });
};

describe('rotatedOverlaps', () => {
  const item = (r: Rect, rotation?: number): DrawableItem => ({ kind: 'rect', name: 'x', rect: r, backgroundColor: '#000000', ...(rotation ? { rotation } : {}) });

  test('agrees with overlaps for an unrotated item, touching edges not counting', () => {
    expect(rotatedOverlaps(item(rect(0, 0, 20, 12)), rect(10, 6, 5, 5))).toBe(true);
    expect(rotatedOverlaps(item(rect(0, 0, 20, 12)), rect(20, 0, 5, 5))).toBe(false);
    expect(rotatedOverlaps(item(rect(0, 0, 20, 12)), rect(25, 25, 5, 5))).toBe(false);
  });

  test('a 20 x 12 check rotated 90 degrees stands 12 wide and 20 tall around its centre', () => {
    const check = item(rect(0, 0, 20, 12), 90);
    const b = itemBounds(check);
    expect([b.left, b.top, b.width, b.height].map((v) => Math.round(v * 1e6) / 1e6)).toEqual([4, -4, 12, 20]);
    expect(rotatedOverlaps(check, rect(0, 0, 3, 3))).toBe(false);
    expect(rotatedOverlaps(check, rect(0, 0, 5, 5))).toBe(true);
  });

  test('a square rotated 45 degrees is a diamond: its bounding box reaches a corner rect that the diamond misses', () => {
    const diamond = item(rect(0, 0, 20, 20), 45);
    expect(overlaps(itemBounds(diamond), rect(0, 0, 2, 2))).toBe(true);
    expect(rotatedOverlaps(diamond, rect(0, 0, 2, 2))).toBe(false);
    expect(rotatedOverlaps(diamond, rect(0, 9, 2, 2))).toBe(true);
  });
});

describe('every layout', () => {
  for (const layout of LAYOUTS) {
    describe(layout.folder, () => {
      const canvas = rect(0, 0, layout.width, layout.height);
      const { main, cards } = buildLayout(layout, opts);
      const inline = buildLayout(layout, { ...opts, strategy: 'inline' }).main;

      test('slots are the slot size, inside the canvas, apart from each other and from the rules', () => {
        expect(layout.slots.length).toBeGreaterThan(0);
        for (const s of layout.slots) {
          expect({ width: s.width, height: s.height }).toEqual(layout.slotSize);
          expect(contains(canvas, s)).toBe(true);
          for (const v of Object.values(s)) expect(Number.isInteger(v)).toBe(true);
        }
        for (let i = 0; i < layout.slots.length; i++) {
          for (let j = i + 1; j < layout.slots.length; j++) expect(overlaps(layout.slots[i]!, layout.slots[j]!)).toBe(false);
        }
        for (const r of layout.rules) for (const s of layout.slots) expect(overlaps(r.rect, s)).toBe(false);
        expect(rungOf(layout)).toBe(rungForSlot(layout.slots[0]!).rung);
        expect(layout.description).toBe(layout.shape === 'round' ? `${layout.slots.length} slots, round` : `${layout.width} x ${layout.height}, ${layout.slots.length} slots`);
      });

      test('the main dashboard has one widget per slot reading that slot setting, and the package is named after the folder', () => {
        expect(main.name).toBe(layout.folder);
        expect(main.metadata.title).toBe(layout.folder);
        expect(main.metadata.description).toBe(layout.description);
        const widgets = main.screens[0]!.items.filter((i) => i.kind === 'widget');
        expect(widgets).toHaveLength(layout.slots.length);
        widgets.forEach((w, i) => {
          if (w.kind !== 'widget') return;
          expect(w.name).toBe(slotSettingName(i + 1));
          expect(w.rect).toEqual(layout.slots[i]!);
          expect(w.initialScreenIndex).toBe(defaultCardForSlot(i + 1));
          expect(w.bindings?.InitialScreenIndex).toEqual({ mode: 'formula', formula: `isnull([OpenDash.Slot${pad2(i + 1)}], ${defaultCardForSlot(i + 1)})` });
        });
        const pkg = buildPackage(layout, opts);
        expect(pkg.folderName).toBe(layout.folder);
        expect(pkg.dashboards.map((d) => d.name)).toEqual([layout.folder, 'cards']);
      });

      test('every card fits the slot at the rung of this layout', () => {
        const origin = rect(0, 0, layout.slotSize.width, layout.slotSize.height);
        const rung = cardRung(layout);
        expect(rung.rung).toBe(rungOf(layout));
        expect(cards.width).toBe(layout.slotSize.width);
        expect(cards.height).toBe(layout.slotSize.height);
        for (const card of CARDS) {
          for (const item of walkItems(card.build(origin, `${card.id}.`, rung))) {
            if (!hasRect(item)) continue;
            expect({ card: card.id, item: item.name, rect: item.rect, inside: contains(origin, item.rect) }).toMatchObject({ inside: true });
            for (const v of Object.values(item.rect)) expect(Number.isInteger(v)).toBe(true);
          }
        }
        for (const s of cards.screens) for (const item of walkItems(s.items)) if (hasRect(item)) expect(contains(origin, item.rect)).toBe(true);
        uniqueNamesPerScreen(cards);
      });

      test('the grid cards keep every value in its column: text inside the column, boxes inside the slot, no overdraw', () => {
        const origin = rect(0, 0, layout.slotSize.width, layout.slotSize.height);
        const rung = cardRung(layout);
        const colWidth = gridColumnWidth(origin, rung);
        /** Width of the text an item actually draws, in its own monospace cells. */
        const textWidth = (it: DrawableItem): number => {
          if (it.kind !== 'text' || !it.monospace) throw new Error(`${it.name} is not a monospaced value`);
          const specials = [...it.text].filter((c) => it.monospace!.specialChars?.includes(c) ?? false).length;
          return (it.text.length - specials) * it.monospace.charWidth + specials * it.monospace.specialCharsWidth;
        };
        for (const id of ['tyreTemps', 'tyrePressures']) {
          const screen = cards.screens.find((s) => s.name === id);
          if (!screen) throw new Error(`${id} has no screen`);
          const corners = ['fl', 'fr', 'rl', 'rr'];
          const drawn = screen.items.filter((i): i is DrawableItem => hasRect(i) && i.kind === 'text' && !i.name.endsWith('.label'));
          const cells = drawn.filter((c) => !c.name.includes('.sub'));
          expect(cells.map((c) => c.name)).toEqual(corners.map((corner) => `${id}.${corner}`));
          // The tread left under a tyre temperature is a value in the same column and is measured
          // with the rest; the canvas draws it at rung L only. Its per-cent sign is a label rather
          // than a cell, so that one is checked by its box.
          const subs = drawn.filter((c) => c.name.endsWith('.sub'));
          expect(subs.map((c) => c.name)).toEqual(id === 'tyreTemps' && rung.rung === 'L' ? corners.map((corner) => `${id}.${corner}.sub`) : []);
          for (const u of drawn.filter((c) => c.name.endsWith('.subunit'))) {
            expect({ card: id, unit: u.name, right: u.rect.left + u.rect.width, inside: u.rect.left + u.rect.width <= layout.slotSize.width }).toMatchObject({ inside: true });
          }
          for (const c of [...cells, ...subs]) {
            // The glyphs fit the column; the box may take the gap and the padding, since WPF clips to it.
            expect({ card: id, cell: c.name, text: textWidth(c), colWidth, fits: textWidth(c) <= colWidth }).toMatchObject({ fits: true });
            expect({ card: id, cell: c.name, right: c.rect.left + c.rect.width, inside: c.rect.left + c.rect.width <= layout.slotSize.width }).toMatchObject({ inside: true });
            // Room to spare in the box, so the last glyph is never clipped.
            expect({ card: id, cell: c.name, slack: c.rect.width - textWidth(c), hasSlack: c.rect.width > textWidth(c) }).toMatchObject({ hasSlack: true });
          }
          // A row's left value never reaches its right neighbour.
          const rows: [DrawableItem, DrawableItem][] = [[cells[0]!, cells[1]!], [cells[2]!, cells[3]!]];
          for (const [l, r] of rows) {
            const gap = r.rect.left - (l.rect.left + textWidth(l));
            expect({ card: id, left: l.name, right: r.name, gap, apart: gap >= 0 }).toMatchObject({ apart: true });
          }
        }
      });

      test('every item of the main dashboard, rotated or not, lies inside the canvas; names are unique', () => {
        for (const item of itemsOf(main)) {
          if (!hasRect(item)) continue;
          expect({ name: item.name, bounds: itemBounds(item), inside: insideWithin(canvas, itemBounds(item), EPSILON) }).toMatchObject({ inside: true });
        }
        uniqueNamesPerScreen(main);
        uniqueNamesPerScreen(inline);
      });

      test('reads exactly the slot settings of its slots plus the fixed settings, all declared; no brand colour', () => {
        const declared = new Set(declaredProperties());
        for (const d of [main, cards, inline]) {
          for (const p of propertiesIn(d).filter((p) => p.startsWith('OpenDash.'))) expect({ p, declared: declared.has(p) }).toEqual({ p, declared: true });
          expect(JSON.stringify(d)).not.toMatch(BRAND);
        }
        const slotReads = propertiesIn(main).filter((p) => /^OpenDash\.Slot\d\d$/.test(p)).sort();
        expect(slotReads).toEqual(layout.slots.map((_, i) => `OpenDash.${slotSettingName(i + 1)}`));
      });

      test('validates without warnings under both strategies', () => {
        expect(validateOrThrow(buildPackage(layout, opts))).toEqual([]);
        expect(validateOrThrow(buildPackage(layout, { ...opts, strategy: 'inline' }))).toEqual([]);
      });
    });
  }
});

/** The rules of a layout as name: [left, top, width, height]. */
const rulesOf = (layout: Layout): Record<string, number[]> =>
  Object.fromEntries(layout.rules.map((r) => [r.name, [r.rect.left, r.rect.top, r.rect.width, r.rect.height]]));

/** Slot origins in slot order. */
const originsOf = (layout: Layout): number[][] => layout.slots.map((s) => [s.left, s.top]);

const layerNamed = (items: readonly Item[], name: string): LayerItem => {
  const layer = items.find((i) => i.name === name);
  if (layer?.kind !== 'layer') throw new Error(`${name} is not a layer`);
  return layer;
};

/** The fifteen rects of the shift lights layer of a rev bar. */
const revBarRects = (layout: Layout): Rect[] => {
  const { main } = buildLayout(layout, opts);
  return layerNamed(main.screens[0]!.items, 'revBar.shiftLights').children.map((c) => {
    if (!hasRect(c)) throw new Error('segment');
    return c.rect;
  });
};

/** A row of the sizes spec's rectangular table. */
interface RectRow {
  layout: Layout;
  folder: string;
  rung: Rung;
  slot: [number, number];
  origins: number[][];
  /** The lowest slot edge: the body bottom, or one pixel above it when the grid leaves a pixel of background. */
  slotsBottom: number;
  hero: HeroGeometry;
  rules: Record<string, number[]>;
}

const strip = (top: number, width: number, height: number, style?: typeof FLAG_STRIP_STYLES.nano) =>
  ({ kind: 'flagStrip', rect: rect(0, top, width, height), ...(style ? { style } : {}) }) as const;

const RECT_ROWS: RectRow[] = [
  {
    layout: layout1280x480,
    folder: 'openDash slots 1280x480',
    rung: 'M',
    slot: [223, 187],
    origins: [[1, 65], [225, 65], [1, 253], [225, 253], [832, 65], [1056, 65], [832, 253], [1056, 253]],
    slotsBottom: 440,
    hero: {
      rev: { kind: 'revBar', left: 16, top: 12, width: 1248, height: 40, gap: 6 },
      gear: { rect: rect(449, 65, 382, 375) },
      pitLimiter: rect(504, 75, 272, 36),
      flags: strip(440, 1280, 40),
    },
    rules: {
      'rule.header': [0, 64, 1280, 1],
      'rule.left.01': [224, 65, 1, 375],
      'rule.left.02': [1, 252, 447, 1],
      'rule.hero.left': [448, 65, 1, 375],
      'rule.hero.right': [831, 65, 1, 375],
      'rule.right.01': [1055, 65, 1, 375],
      'rule.right.02': [832, 252, 447, 1],
    },
  },
  {
    layout: layout1280x400,
    folder: 'openDash slots 1280x400',
    rung: 'M',
    slot: [223, 156],
    origins: [[1, 55], [225, 55], [1, 212], [225, 212], [832, 55], [1056, 55], [832, 212], [1056, 212]],
    slotsBottom: 368,
    hero: {
      rev: { kind: 'revBar', left: 16, top: 10, width: 1248, height: 32, gap: 6 },
      gear: { rect: rect(449, 55, 382, 313) },
      pitLimiter: rect(504, 65, 272, 28),
      flags: strip(368, 1280, 32),
    },
    rules: {
      'rule.header': [0, 54, 1280, 1],
      'rule.left.01': [224, 55, 1, 313],
      'rule.left.02': [1, 211, 447, 1],
      'rule.hero.left': [448, 55, 1, 313],
      'rule.hero.right': [831, 55, 1, 313],
      'rule.right.01': [1055, 55, 1, 313],
      'rule.right.02': [832, 211, 447, 1],
    },
  },
  {
    layout: layout850x480,
    folder: 'openDash slots 850x480',
    rung: 'M',
    slot: [233, 124],
    origins: [[0, 65], [0, 190], [0, 315], [617, 65], [617, 190], [617, 315]],
    // 3 x 124 + 2 rules = 374 inside the 375 body: the last pixel above the flag strip stays background.
    slotsBottom: 439,
    hero: {
      rev: { kind: 'revBar', left: 16, top: 12, width: 818, height: 40, gap: 4 },
      gear: { rect: rect(234, 65, 382, 375) },
      pitLimiter: rect(289, 75, 272, 36),
      flags: strip(440, 850, 40),
    },
    rules: {
      'rule.header': [0, 64, 850, 1],
      'rule.left.01': [0, 189, 233, 1],
      'rule.left.02': [0, 314, 233, 1],
      'rule.hero.left': [233, 65, 1, 375],
      'rule.hero.right': [616, 65, 1, 375],
      'rule.right.01': [617, 189, 233, 1],
      'rule.right.02': [617, 314, 233, 1],
    },
  },
  {
    layout: layout800x480,
    folder: 'openDash slots 800x480',
    rung: 'M',
    slot: [208, 124],
    origins: [[0, 65], [0, 190], [0, 315], [592, 65], [592, 190], [592, 315]],
    slotsBottom: 439,
    hero: {
      rev: { kind: 'revBar', left: 16, top: 12, width: 768, height: 40, gap: 4 },
      gear: { rect: rect(209, 65, 382, 375) },
      pitLimiter: rect(264, 75, 272, 36),
      flags: strip(440, 800, 40),
    },
    rules: {
      'rule.header': [0, 64, 800, 1],
      'rule.left.01': [0, 189, 208, 1],
      'rule.left.02': [0, 314, 208, 1],
      'rule.hero.left': [208, 65, 1, 375],
      'rule.hero.right': [591, 65, 1, 375],
      'rule.right.01': [592, 189, 208, 1],
      'rule.right.02': [592, 314, 208, 1],
    },
  },
  {
    layout: layout1280x720,
    folder: 'openDash slots 1280x720',
    rung: 'M',
    slot: [223, 204],
    origins: [[1, 65], [225, 65], [1, 270], [225, 270], [1, 475], [225, 475], [832, 65], [1056, 65], [832, 270], [1056, 270], [832, 475], [1056, 475]],
    // 3 x 204 + 2 rules = 614 inside the 615 body.
    slotsBottom: 679,
    hero: {
      rev: { kind: 'revBar', left: 24, top: 12, width: 1232, height: 40, gap: 6 },
      gear: { rect: rect(449, 65, 382, 615) },
      pitLimiter: rect(504, 75, 272, 36),
      flags: strip(680, 1280, 40),
    },
    rules: {
      'rule.header': [0, 64, 1280, 1],
      'rule.left.01': [224, 65, 1, 614],
      'rule.left.02': [1, 269, 447, 1],
      'rule.left.03': [1, 474, 447, 1],
      'rule.hero.left': [448, 65, 1, 615],
      'rule.hero.right': [831, 65, 1, 615],
      'rule.right.01': [1055, 65, 1, 614],
      'rule.right.02': [832, 269, 447, 1],
      'rule.right.03': [832, 474, 447, 1],
    },
  },
  {
    layout: layout800x286,
    folder: 'openDash slots 800x286',
    rung: 'M',
    slot: [250, 109],
    origins: [[0, 55], [0, 165], [550, 55], [550, 165]],
    slotsBottom: 274,
    hero: {
      rev: { kind: 'revBar', left: 16, top: 10, width: 768, height: 32, gap: 4 },
      gear: { rect: rect(251, 55, 298, 219), size: 180 },
      pitLimiter: rect(274, 65, 252, 28),
      flags: strip(274, 800, 12, FLAG_STRIP_STYLES.nano),
    },
    rules: {
      'rule.header': [0, 54, 800, 1],
      'rule.left.01': [0, 164, 250, 1],
      'rule.hero.left': [250, 55, 1, 219],
      'rule.hero.right': [549, 55, 1, 219],
      'rule.right.01': [550, 164, 250, 1],
    },
  },
  {
    layout: layout600x686,
    folder: 'openDash slots 600x686',
    rung: 'M',
    slot: [199, 139],
    origins: [[0, 366], [200, 366], [400, 366], [0, 506], [200, 506], [400, 506]],
    // 2 x 139 + 1 rule = 279 in the 280 px area above the flag strip.
    slotsBottom: 645,
    hero: {
      rev: { kind: 'revBar', left: 16, top: 12, width: 568, height: 40, gap: 4 },
      gear: { rect: rect(0, 65, 600, 300) },
      pitLimiter: rect(164, 73, 272, 36),
      flags: strip(646, 600, 40),
    },
    rules: {
      'rule.header': [0, 64, 600, 1],
      'rule.hero': [0, 365, 600, 1],
      'rule.grid.01': [199, 366, 1, 279],
      'rule.grid.02': [399, 366, 1, 279],
      'rule.grid.03': [0, 505, 599, 1],
    },
  },
];

describe('the rectangular sizes, row by row of the spec table', () => {
  test('the table covers every rectangular layout but the MVP, each once', () => {
    const rows = RECT_ROWS.map((r) => r.layout);
    expect(new Set(rows).size).toBe(rows.length);
    // The reference face is the MVP row and has its own describe above; excluded by identity rather
    // than by name, because the name moved to the zone face in XOR-118.
    expect(LAYOUTS.filter((l) => l.shape === 'rect' && l !== layout1920x480)).toEqual(rows);
  });

  for (const row of RECT_ROWS) {
    describe(row.folder, () => {
      const { layout } = row;
      const [width, height] = row.slot;

      test(`${row.origins.length} slots of ${width} x ${height} at rung ${row.rung}, in spec order`, () => {
        expect(layout.folder).toBe(row.folder);
        expect(layout.description).toBe(`${layout.width} x ${layout.height}, ${row.origins.length} slots`);
        expect(layout.shape).toBe('rect');
        expect(rungOf(layout)).toBe(row.rung);
        expect(layout.slotSize).toEqual({ width, height });
        expect(originsOf(layout)).toEqual(row.origins);
        expect(Math.max(...layout.slots.map((s) => s.top + s.height))).toBe(row.slotsBottom);
        expect(layout.background).toBe('#0A0B0D');
      });

      test('hero geometry: rev bar, gear frame, pit limiter, flag strip', () => {
        expect(layout.hero).toEqual(row.hero);
      });

      test('rules: the header rule, the grid separators and the hero separators', () => {
        expect(rulesOf(layout)).toEqual(row.rules);
      });

      test('the rev bar has 15 segments with exact gaps spanning its frame', () => {
        const { rev } = row.hero;
        if (rev.kind !== 'revBar') throw new Error('rev bar');
        const rects = revBarRects(layout);
        expect(rects).toHaveLength(15);
        expect(rects[0]!.left).toBe(rev.left);
        expect(rects[14]!.left + rects[14]!.width).toBe(rev.left + rev.width);
        for (const r of rects) expect([r.top, r.height]).toEqual([rev.top, rev.height]);
        for (let k = 1; k < 15; k++) expect(rects[k]!.left - (rects[k - 1]!.left + rects[k - 1]!.width)).toBe(rev.gap);
      });

      test('the flag strip and the slots meet edge to edge or leave one pixel of background', () => {
        const flags = layout.hero.flags;
        if (flags.kind !== 'flagStrip') throw new Error('flag strip');
        expect(flags.rect.top + flags.rect.height).toBe(layout.height);
        expect(flags.rect.top - row.slotsBottom).toBeLessThanOrEqual(1);
        expect(flags.rect.top - row.slotsBottom).toBeGreaterThanOrEqual(0);
      });
    });
  }
});

/** The label of the first card of a slot under the inline strategy, which draws the cards inside the slots. */
const inlineSlotLabel = (layout: Layout, slot: number): DrawableItem => {
  const inline = buildLayout(layout, { ...opts, strategy: 'inline' }).main;
  const layer = layerNamed(inline.screens[0]!.items, slotSettingName(slot));
  const first = layerNamed(layer.children, `${slotSettingName(slot)}.currentLap`).children[0];
  if (first?.kind !== 'text') throw new Error('label');
  return first;
};

/** A rung M layout with the S padding: every card's label starts 12 px in and every grid value stays inside the slot height. */
const expectSPaddingAtRungM = (layout: Layout): void => {
  expect(layout.cardPadding).toEqual({ y: 8, x: 12 });
  expect(cardRung(layout)).toEqual({ ...rungSpec('M'), padding: { y: 8, x: 12 } });
  const { cards } = buildLayout(layout, opts);
  for (const screen of cards.screens) {
    const lbl = screen.items.find((i) => i.name.endsWith('.label'));
    if (lbl?.kind !== 'text') throw new Error(`${screen.name} has no label`);
    expect([lbl.rect.left, lbl.rect.width]).toEqual([12, layout.slotSize.width - 24]);
  }
  for (const id of ['tyreTemps', 'tyrePressures']) {
    const grid = cards.screens.find((s) => s.name === id)!;
    const values = grid.items.filter((i): i is DrawableItem => hasRect(i) && i.kind === 'text' && !i.name.endsWith('.label'));
    expect(values).toHaveLength(4);
    for (const v of values) {
      expect(v.rect.top).toBeGreaterThanOrEqual(0);
      expect(v.rect.top + v.rect.height).toBeLessThanOrEqual(layout.slotSize.height);
    }
  }
};

describe('card padding per layout', () => {
  /** Rung M faces whose cells are too low for the M padding: they take the S padding. */
  const overridden: readonly Layout[] = [layout800x286, layout800round];

  test("every layout but the nano and the 800 round builds its cards with the rung's own padding", () => {
    for (const layout of LAYOUTS) {
      if (overridden.includes(layout)) continue;
      expect(layout.cardPadding).toBeUndefined();
      expect(cardRung(layout)).toEqual(rungSpec(rungOf(layout)));
    }
  });

  test('the nano is rung M with the S padding, so its 109 px cells hold the grid cards and its labels start 12 px in', () => {
    expectSPaddingAtRungM(layout800x286);
    // The inline strategy draws the same cards inside the slots: the label of slot 3 starts at 550 + 12.
    expect(inlineSlotLabel(layout800x286, 3).rect.left).toBe(562);
  });

  test('the 800 round is rung M with the S padding, so its 110 px cells hold the grid cards and its labels start 12 px in', () => {
    expectSPaddingAtRungM(layout800round);
    // Slot 5 is the left one of the bottom row, at x 210.
    expect(inlineSlotLabel(layout800round, 5).rect.left).toBe(222);
  });
});

describe('800 x 286 nano', () => {
  const layout = layout800x286;
  const { main } = buildLayout(layout, opts);
  const items = main.screens[0]!.items;

  test('the hero is the small gear alone: 180, centred in the 298 column', () => {
    expect(layout.hero.gear).toEqual({ rect: rect(251, 55, 298, 219), size: GEAR_SIZES.nano });
    const gearItem = items.find((i) => i.name === 'hero.gear');
    if (gearItem?.kind !== 'text') throw new Error('hero text');
    expect([gearItem.fontSize, gearItem.fontWeight]).toEqual([180, 'Bold']);
    // Gear cell 94 centred in 298: 102 px either side.
    expect(gearItem.rect).toEqual({ left: 353, top: 57, width: 98, height: 217 });
    expect(Math.abs(gearItem.rect.left + (gearItem.monospace?.charWidth ?? 0) / 2 - (251 + 298 / 2))).toBeLessThanOrEqual(0.5);
    // Everything of the hero stays between the header rule and the flag strip.
    expect(contains(rect(0, 55, 800, 219), gearItem.rect)).toBe(true);
    expect(items.map((i) => i.name).filter((n) => n.startsWith('hero.'))).toEqual(['hero.gear']);
  });

  test('the 12 px flag strip has no labels, a 2 px black outline and 6 px checks', () => {
    const flags = items.filter((i): i is LayerItem => i.kind === 'layer' && i.name.startsWith('flag.'));
    expect(flags.map((f) => f.name)).toEqual(['flag.black', 'flag.chequered', 'flag.yellow', 'flag.blue', 'flag.white', 'flag.green']);
    for (const f of flags) {
      for (const child of walkItems(f.children)) {
        expect(child.kind).toBe('rect');
        expect(child.name.endsWith('.label')).toBe(false);
        if (hasRect(child)) expect(contains(rect(0, 274, 800, 12), child.rect)).toBe(true);
      }
    }
    for (const id of ['blue', 'white', 'green']) expect(layerNamed(items, `flag.${id}`).children).toHaveLength(1);
    expect(layerNamed(items, 'flag.yellow').children.map((c) => c.name)).toEqual(['flag.yellow.band', 'flag.yellow.flash']);
    const black = layerNamed(items, 'flag.black').children[0];
    if (black?.kind !== 'rect') throw new Error('black band');
    expect(black.border).toEqual({ color: '#F5F7FA', top: 2, bottom: 2, left: 2, right: 2 });
    // Opaque, and the darkest ground there is: a flag takes the strip over, and the black flag was
    // the one that did not, leaving whatever it covered readable underneath it.
    expect(black.backgroundColor).toBe('#0A0B0D');
    // 800 / 6 = 133.3 columns, so the last check (column 133, row 1) is clipped to 2 px.
    const checks = layerNamed(items, 'flag.chequered').children.slice(1).filter(hasRect);
    expect(checks).toHaveLength(2 * Math.ceil(800 / 6 / 2));
    for (const c of checks) expect(c.rect.height).toBe(6);
    expect(checks.map((c) => c.rect.width).filter((w) => w !== 6)).toEqual([2]);
    // The flash is a band over the fill and not the layer: a blinking layer draws nothing for half
    // of every cycle, and what a flag covers has to stay covered.
    expect(layerNamed(items, 'flag.yellow').blink).toBeUndefined();
    expect(layerNamed(items, 'flag.yellow').children[1]?.blink).toEqual({ enabled: true, delayMs: 250 });
  });

  test('the standard strip of the other faces keeps its labels and 3 px outline', () => {
    const standard = buildLayout(layout1280x480, opts).main.screens[0]!.items;
    const yellow = layerNamed(standard, 'flag.yellow');
    expect(yellow.children.map((c) => c.name)).toEqual(['flag.yellow.band', 'flag.yellow.label', 'flag.yellow.flash']);
    const black = layerNamed(standard, 'flag.black').children[0];
    if (black?.kind !== 'rect') throw new Error('black band');
    expect(black.border?.top).toBe(3);
  });
});

describe('600 x 686 DisplayDash', () => {
  const layout = layout600x686;
  const { main } = buildLayout(layout, opts);
  const items = main.screens[0]!.items;

  test('the hero is the gear alone in a full-width 300 px band above the grid', () => {
    expect(layout.hero.gear).toEqual({ rect: rect(0, 65, 600, 300) });
    const [gearItem] = gear(rect(0, 65, 600, 300));
    if (gearItem?.kind !== 'text') throw new Error('gear returns one text item');
    // 136 centred in 600: 232 px either side.
    expect(gearItem.rect).toEqual({ left: 232, top: 59, width: 140, height: 313 });
    // The gear's 1.2 em box overhangs the band by 6 px each side; its cap top and baseline stay inside it.
    const baseline = gearItem.rect.top + WPF_BASELINE * gearItem.fontSize;
    const capTop = baseline - (FONT_METRICS.capHeight / FONT_METRICS.unitsPerEm) * gearItem.fontSize;
    expect(capTop).toBeGreaterThanOrEqual(65);
    expect(baseline).toBeLessThanOrEqual(365);
    expect(items.map((i) => i.name).filter((n) => n.startsWith('hero.'))).toEqual(['hero.gear']);
  });

  test('the pit limiter is centred in the band, 8 px below its top, over the gear', () => {
    expect(layout.hero.pitLimiter).toEqual({ left: 164, top: 73, width: 272, height: 36 });
    expect(164 + 272 / 2).toBe(300);
  });

  test('the grid is 3 x 2 row-major with two vertical and one horizontal rule, below the hero rule', () => {
    expect(layout.slots).toHaveLength(6);
    expect(layout.slots[1]!.left - layout.slots[0]!.left).toBe(200);
    expect(layout.slots[3]!.top - layout.slots[0]!.top).toBe(140);
    const gridRules = layout.rules.filter((r) => r.name.startsWith('rule.grid.'));
    expect(gridRules.map((r) => r.rect)).toEqual([rect(199, 366, 1, 279), rect(399, 366, 1, 279), rect(0, 505, 599, 1)]);
    for (const r of gridRules) for (const s of layout.slots) expect(overlaps(r.rect, s)).toBe(false);
    expect(layout.rules.find((r) => r.name === 'rule.hero')!.rect).toEqual(rect(0, 365, 600, 1));
    expect(Math.min(...layout.slots.map((s) => s.top))).toBe(366);
  });
});

/** A row of the sizes spec's round-face table. */
interface RoundRow {
  layout: Layout;
  folder: string;
  rung: Rung;
  slot: [number, number];
  origins: number[][];
  face: { cx: number; cy: number; r: number };
  hero: HeroGeometry;
  cardPadding?: { y: number; x: number };
}

const FACE_480 = { cx: 240, cy: 240, r: 240 };
const FACE_800 = { cx: 400, cy: 400, r: 400 };

const ROUND_ROWS: RoundRow[] = [
  {
    layout: layout480round,
    folder: 'openDash 480 round',
    rung: 'S',
    slot: [140, 108],
    origins: [[20, 186], [320, 186]],
    face: FACE_480,
    hero: {
      rev: { kind: 'revArc', circle: { cx: 240, cy: 240, r: 206 }, segment: { width: 22, height: 14 } },
      // The gap between the two slots, which takes the spec's 260 gear without a size of its own.
      gear: { rect: rect(160, 108, 160, 340) },
      pitLimiter: rect(165, 112, 150, 28),
      flags: { kind: 'flagRing', face: FACE_480 },
    },
  },
  {
    layout: layout800round,
    folder: 'openDash 800 round',
    rung: 'M',
    slot: [180, 110],
    // Left column, right column, then the bottom row.
    origins: [[44, 290], [44, 404], [576, 290], [576, 404], [210, 562], [410, 562]],
    face: FACE_800,
    hero: {
      rev: { kind: 'revArc', circle: { cx: 400, cy: 400, r: 352 }, segment: { width: 30, height: 18 } },
      gear: { rect: rect(240, 260, 320, 280) },
      pitLimiter: rect(300, 176, 200, 36),
      flags: { kind: 'flagRing', face: FACE_800 },
    },
    cardPadding: { y: 8, x: 12 },
  },
];

/** The items of a round face's hero, in the order the main screen draws them (the ring last, so it is the outermost element). */
const ROUND_HERO_NAMES = ['revArc.shiftLights', 'revArc.shiftLightsSimHub', 'revArc.rpmBar', 'hero.gear', 'pitLimiter', 'flag.black', 'flag.chequered', 'flag.yellow', 'flag.blue', 'flag.white', 'flag.green'];

describe('the round faces, row by row of the spec table', () => {
  test('the table covers every round layout, each once', () => {
    const rows = ROUND_ROWS.map((r) => r.layout);
    expect(new Set(rows).size).toBe(rows.length);
    expect(LAYOUTS.filter((l) => l.shape === 'round')).toEqual(rows);
  });

  for (const row of ROUND_ROWS) {
    describe(row.folder, () => {
      const { layout, face } = row;
      const centrePoint = { x: face.cx, y: face.cy };
      const { main } = buildLayout(layout, opts);
      const items = main.screens[0]!.items;
      const [width, height] = row.slot;

      test(`${row.origins.length} slots of ${width} x ${height} at rung ${row.rung}, in spec order; the hero variants; the ring on the face; no rules`, () => {
        expect(layout.folder).toBe(row.folder);
        expect(layout.description).toBe(`${row.origins.length} slots, round`);
        expect(layout.shape).toBe('round');
        expect([layout.width, layout.height]).toEqual([2 * face.r, 2 * face.r]);
        expect(rungOf(layout)).toBe(row.rung);
        expect(layout.slotSize).toEqual({ width, height });
        expect(originsOf(layout)).toEqual(row.origins);
        expect(layout.hero).toEqual(row.hero);
        expect(layout.rules).toEqual([]);
        expect(layout.cardPadding).toEqual(row.cardPadding);
        expect(layout.background).toBe('#0A0B0D');
      });

      test('carries the round hero: rev arc layers, the gear, the pit limiter and six flag rings, then the slots', () => {
        expect(items.map((i) => i.name)).toEqual([...ROUND_HERO_NAMES, ...layout.slots.map((_, i) => slotSettingName(i + 1))]);
        expect(hero(layout.hero).map((i) => i.name)).toEqual(ROUND_HERO_NAMES);
      });

      test('every rev arc segment centre lies on its circle within 1 px and is rotated by its angle', () => {
        const { rev } = row.hero;
        if (rev.kind !== 'revArc') throw new Error('rev arc');
        for (const layer of ['revArc.shiftLights', 'revArc.shiftLightsSimHub', 'revArc.rpmBar']) {
          const segments = layerNamed(items, layer).children;
          expect(segments).toHaveLength(15);
          segments.forEach((s, k) => {
            if (!hasRect(s)) throw new Error('segment');
            const angle = revArcAngle(k, 15);
            expect(angle).toBe(-63 + 9 * k);
            expect(s.rotation ?? 0).toBe(angle);
            expect({ width: s.rect.width, height: s.rect.height }).toEqual(rev.segment);
            expect(Math.abs(distance(centre(s.rect), centrePoint) - rev.circle.r)).toBeLessThanOrEqual(1);
            for (const v of Object.values(s.rect)) expect(Number.isInteger(v)).toBe(true);
          });
        }
        // The extremes: -63 degrees sits left of centre in the upper half, +63 mirrors it on the right, 0 straight up.
        const shift = layerNamed(items, 'revArc.shiftLights').children as DrawableItem[];
        expect(centre(shift[7]!.rect)).toEqual({ x: face.cx, y: face.cy - rev.circle.r });
        expect(centre(shift[0]!.rect).x).toBeLessThan(face.cx);
        expect(centre(shift[14]!.rect).x).toBeGreaterThan(face.cx);
        expect(centre(shift[0]!.rect).y).toBe(centre(shift[14]!.rect).y);
        expect(shift[7]!.rotation).toBeUndefined();
      });

      test('flag rings are ellipses on the face square, so their stroke lies on the rim: 12 px, black 3 px, yellow blinking', () => {
        const rings: Record<string, EllipseItem> = {};
        for (const id of ['black', 'yellow', 'blue', 'white', 'green']) {
          const layer = layerNamed(items, `flag.${id}`);
          expect(layer.children).toHaveLength(1);
          const item = layer.children[0]!;
          if (item.kind !== 'ellipse') throw new Error(`flag.${id} holds no ellipse`);
          rings[id] = item;
          // WPF draws an Ellipse's stroke inside its rect: the face square puts a stroke of t between the radii r - t and r.
          expect(item.rect).toEqual({ left: face.cx - face.r, top: face.cy - face.r, width: 2 * face.r, height: 2 * face.r });
          expect(item.strokeThickness).toBeLessThanOrEqual(12);
          expect(item.fillColor).toBe('#00FFFFFF');
          expect(item.name).toBe(`flag.${id}.ring`);
          expect(layer.bindings?.Visible?.formula).toContain(`[DataCorePlugin.GameData.Flag_${id[0]!.toUpperCase()}${id.slice(1)}]`);
        }
        expect(rings.black!.strokeThickness).toBe(3);
        expect(rings.black!.strokeColor).toBe('#F5F7FA');
        for (const id of ['yellow', 'blue', 'white', 'green']) expect(rings[id]!.strokeThickness).toBe(12);
        expect(rings.yellow!.strokeColor).toBe('#FFD400');
        expect(layerNamed(items, 'flag.yellow').blink).toEqual({ enabled: true, delayMs: 250 });
        expect(layerNamed(items, 'flag.blue').blink).toBeUndefined();
        expect(items.filter((i) => i.kind === 'layer' && i.name.startsWith('flag.')).map((i) => i.name)).toEqual(['flag.black', 'flag.chequered', 'flag.yellow', 'flag.blue', 'flag.white', 'flag.green']);
      });

      test('the chequered ring is 24 white 20 x 12 checks at 15 degree steps offset half a step, centred 6 px inside the rim, and nothing else', () => {
        const checks = layerNamed(items, 'flag.chequered').children;
        expect(checks).toHaveLength(CHEQUER_COUNT);
        expect(CHEQUER_COUNT).toBe(24);
        expect(CHEQUER_STEP).toBe(15);
        expect(CHEQUER_SIZE).toEqual({ width: 20, height: 12 });
        checks.forEach((c, k) => {
          if (c.kind !== 'rect') throw new Error('check');
          expect(c.name).toBe(`flag.chequered.c${pad2(k)}`);
          expect(c.backgroundColor).toBe('#F5F7FA');
          expect(c.rotation ?? 0).toBe((k + 0.5) * 15);
          expect({ width: c.rect.width, height: c.rect.height }).toEqual({ width: 20, height: 12 });
          expect(Math.abs(distance(centre(c.rect), centrePoint) - (face.r - 6))).toBeLessThanOrEqual(1);
          expect(c.border?.radius).toBeUndefined();
        });
      });

      test('the hero text boxes clear the slots; slots and rev segments stay inside the inner disc; every item stays inside the face square', () => {
        const inner = face.r - 12;
        const corners = (r: Rect) => [
          { x: r.left, y: r.top },
          { x: r.left + r.width, y: r.top },
          { x: r.left, y: r.top + r.height },
          { x: r.left + r.width, y: r.top + r.height },
        ];
        for (const s of layout.slots) for (const c of corners(s)) expect(distance(c, centrePoint)).toBeLessThanOrEqual(inner);
        for (const s of layerNamed(items, 'revArc.shiftLights').children) {
          if (!hasRect(s)) continue;
          const halfDiagonal = Math.hypot(s.rect.width, s.rect.height) / 2;
          expect(distance(centre(s.rect), centrePoint) + halfDiagonal).toBeLessThanOrEqual(inner);
        }
        for (const name of ['hero.gear', 'pitLimiter.band']) {
          const item = [...walkItems(items)].find((i) => i.name === name);
          if (!item || !hasRect(item)) throw new Error(`${name} has no rect`);
          for (const c of corners(item.rect)) expect({ name, corner: c, inside: distance(c, centrePoint) <= inner }).toMatchObject({ inside: true });
          for (const s of layout.slots) expect({ name, slot: s, clear: !overlaps(item.rect, s) }).toMatchObject({ clear: true });
        }
        const square = rect(0, 0, 2 * face.r, 2 * face.r);
        for (const item of itemsOf(main)) if (hasRect(item)) expect(insideWithin(square, itemBounds(item), EPSILON)).toBe(true);
      });

      test('the rev arc ends above the slots and, with the chequered checks, stays clear of the slots, the pit limiter and the hero text (rotated footprints)', () => {
        const segments = [...layerNamed(items, 'revArc.shiftLights').children, ...layerNamed(items, 'revArc.shiftLightsSimHub').children, ...layerNamed(items, 'revArc.rpmBar').children].filter(hasRect);
        const checks = layerNamed(items, 'flag.chequered').children.filter(hasRect);
        expect(segments).toHaveLength(45);
        expect(checks).toHaveLength(CHEQUER_COUNT);
        const heroBoxes = ['hero.gear', 'pitLimiter.band'].map((name) => {
          const item = [...walkItems(items)].find((i) => i.name === name);
          if (!item || !hasRect(item)) throw new Error(`${name} has no rect`);
          return { name, rect: item.rect };
        });
        const obstacles = [...layout.slots.map((s, i) => ({ name: slotSettingName(i + 1), rect: s })), ...heroBoxes];
        for (const item of [...segments, ...checks]) {
          for (const o of obstacles) expect({ item: item.name, against: o.name, rect: item.rect, rotation: item.rotation, clear: !rotatedOverlaps(item, o.rect) }).toMatchObject({ clear: true });
        }
        // The rev segments clear the slots by their bounding boxes too; the checks only by their exact rects.
        for (const item of segments) for (const s of layout.slots) expect(overlaps(itemBounds(item), s)).toBe(false);
        // A cap over the top, not a wrap around the sides: every segment footprint ends above the highest slot.
        const arcBottom = Math.max(...segments.map((s) => itemBounds(s).top + itemBounds(s).height));
        expect(arcBottom).toBeLessThanOrEqual(Math.min(...layout.slots.map((s) => s.top)));
      });
    });
  }
});

describe('480 round', () => {
  const layout = layout480round;

  // The full 260 of the spec, in the 160 px between the two slots: a 260 gear needs 140 px of box
  // and fits. It was cut to 228 while the cell had to hold whichever Barlow WPF resolved (XOR-84).
  test('the gear alone, 260, centred on the face', () => {
    const [gearItem] = gear(layout.hero.gear.rect, layout.hero.gear.size);
    if (gearItem?.kind !== 'text') throw new Error('gear returns one text item');
    expect(gearItem.fontSize).toBe(260);
    expect(gearItem.fontWeight).toBe('Bold');
    expect(gearItem.rect).toEqual({ left: 172, top: 122, width: 140, height: 313 });
    // Centred on the 480 face in both axes, to the half pixel rounding allows.
    expect(Math.abs(gearItem.rect.left + (gearItem.monospace?.charWidth ?? 0) / 2 - 240)).toBeLessThanOrEqual(0.5);
    expect(Math.abs(gearItem.rect.top + gearItem.rect.height / 2 - 278)).toBeLessThanOrEqual(0.5);
  });

  /**
   * At 260 the gear's box overlaps the pit limiter band by 18 px, where at 228 it cleared it. That
   * is allowed and it is not an accident: a text box has no background, and what must not touch the
   * band is the ink. So the invariant is about the cap top rather than about the boxes, and it is
   * written down here because the round-face table checks each hero box against the slots and never
   * against the other hero boxes, which is why nothing noticed the overlap appear.
   */
  test('the gear glyph clears the pit limiter band, though its box does not', () => {
    const [gearItem] = gear(layout.hero.gear.rect, layout.hero.gear.size);
    if (gearItem?.kind !== 'text') throw new Error('gear returns one text item');
    const band = layout.hero.pitLimiter;
    expect(overlaps(gearItem.rect, band)).toBe(true);
    const baseline = gearItem.rect.top + WPF_BASELINE * gearItem.fontSize;
    const capTop = baseline - (FONT_METRICS.capHeight / FONT_METRICS.unitsPerEm) * gearItem.fontSize;
    expect(capTop).toBeGreaterThan(band.top + band.height);
  });
});

describe('800 round', () => {
  const layout = layout800round;

  test('the hero is the gear alone, centred in the 320 x 280 column, its glyphs inside the column', () => {
    expect(layout.hero.gear).toEqual({ rect: rect(240, 260, 320, 280) });
    const [gearItem] = gear(rect(240, 260, 320, 280));
    if (gearItem?.kind !== 'text') throw new Error('gear returns one text item');
    expect([gearItem.fontSize, gearItem.fontWeight]).toEqual([260, 'Bold']);
    expect(gearItem.rect).toEqual({ left: 332, top: 244, width: 140, height: 313 });
    // The cell, not the box, is centred: 136 in 320 leaves 92 either side.
    const cell = gearItem.monospace?.charWidth ?? 0;
    expect(Math.abs(gearItem.rect.left - 240 - (560 - (gearItem.rect.left + cell)))).toBeLessThanOrEqual(1);
  });

  test('the pit limiter is the standard 36 px block, 200 wide, centred over the gear and 48 px above the column', () => {
    expect(layout.hero.pitLimiter).toEqual({ left: 300, top: 176, width: 200, height: 36 });
    expect(300 + 200 / 2).toBe(400);
    expect(176 + 36).toBe(260 - 48);
  });

  test('the six slots sit two per side and two below, 4 px apart on a side and 20 px apart below, all clear of the hero column', () => {
    const [l1, l2, r1, r2, b1, b2] = layout.slots as [Rect, Rect, Rect, Rect, Rect, Rect];
    expect(l2.top - (l1.top + l1.height)).toBe(4);
    expect(r2.top - (r1.top + r1.height)).toBe(4);
    expect([l1.left, l2.left]).toEqual([44, 44]);
    expect([r1.left, r2.left]).toEqual([576, 576]);
    expect(b2.left - (b1.left + b1.width)).toBe(20);
    expect([b1.top, b2.top]).toEqual([562, 562]);
    // Left and right columns are mirror images about the face centre, and so is the bottom row.
    expect(800 - (r1.left + r1.width)).toBe(l1.left);
    expect(800 - (b2.left + b2.width)).toBe(b1.left);
    const column = layout.hero.gear.rect;
    for (const s of layout.slots) expect(overlaps(column, s)).toBe(false);
  });
});

describe('tyre pressure precision per face', () => {
  const origin = (layout: Layout): Rect => rect(0, 0, layout.slotSize.width, layout.slotSize.height);
  const tierOf = (layout: Layout) => pressureTierFor(origin(layout), cardRung(layout));
  const P = 'isnull([DataCorePlugin.GameData.TyrePressureFrontLeft], 0)';
  const frontLeft = (layout: Layout): DrawableItem => {
    const { cards } = buildLayout(layout, opts);
    const item = cards.screens.find((s) => s.name === 'tyrePressures')?.items.find((i) => i.name === 'tyrePressures.fl');
    if (item?.kind !== 'text') throw new Error('tyrePressures.fl');
    return item;
  };

  test('pressureTier picks the most precise budget that fits the column: 96 / 75 / 63 px at 46, 73 / 57 / 48 at 34', () => {
    expect(PRESSURE_TIERS.map((t) => t.integerFrom)).toEqual([Number.POSITIVE_INFINITY, 100, 10]);
    expect(pressureTier(103.5, 46)).toBe(PRESSURE_TIERS[0]!);
    expect(pressureTier(95, 46)).toBe(PRESSURE_TIERS[1]!);
    expect(pressureTier(73, 34)).toBe(PRESSURE_TIERS[0]!);
    expect(pressureTier(70, 34)).toBe(PRESSURE_TIERS[1]!);
    expect(pressureTier(50, 34)).toBe(PRESSURE_TIERS[2]!);
    // Nothing fits: the narrowest tier, which the grid fit test above then reports.
    expect(pressureTier(10, 34)).toBe(PRESSURE_TIERS[2]!);
  });

  test('every rectangular face shows one decimal in every unit', () => {
    for (const layout of LAYOUTS.filter((l) => l.shape === 'rect')) {
      expect({ folder: layout.folder, tier: tierOf(layout) }).toEqual({ folder: layout.folder, tier: PRESSURE_TIERS[0]! });
      const fl = frontLeft(layout);
      expect(fl.bindings?.Text?.formula).toBe(`if((${P}) = (0), '--', format(${P}, '0.0'))`);
      if (fl.kind === 'text') expect(fl.text).toBe('27.8');
    }
  });

  test('the 800 round, 70 px columns at 34, shows kPa as integers and keeps the decimal below 100', () => {
    expect(tierOf(layout800round)).toEqual({ integerFrom: 100, chars: { digits: 3, specials: 1 } });
    const fl = frontLeft(layout800round);
    expect(fl.bindings?.Text?.formula).toBe(`if((${P}) = (0), '--', if((${P}) < (100), format(${P}, '0.0'), format(${P}, '0')))`);
    if (fl.kind !== 'text') throw new Error('tyrePressures.fl is a text item');
    expect(fl.text).toBe('27.8');
    // Three digit cells and a point of budget, in a box with slack so the last glyph is not clipped.
    expect(fl.monospace).toMatchObject({ charWidth: 16, specialCharsWidth: 9 });
    expect(fl.rect.width).toBeGreaterThan(3 * 16 + 9);
    expect(3 * 16 + 9).toBeLessThanOrEqual(gridColumnWidth(origin(layout800round), cardRung(layout800round)));
  });

  test('the 480 round, 50 px columns at 34, shows psi and kPa as integers and bar with its decimal', () => {
    expect(tierOf(layout480round)).toEqual({ integerFrom: 10, chars: { digits: 3, specials: 0 } });
    const fl = frontLeft(layout480round);
    expect(fl.bindings?.Text?.formula).toBe(`if((${P}) = (0), '--', if((${P}) < (10), format(${P}, '0.0'), format(${P}, '0')))`);
    if (fl.kind !== 'text') throw new Error('tyrePressures.fl is a text item');
    expect(fl.text).toBe('28');
    expect(fl.monospace).toMatchObject({ charWidth: 16 });
    expect(fl.rect.width).toBeGreaterThan(3 * 16);
    expect(3 * 16).toBeLessThanOrEqual(gridColumnWidth(origin(layout480round), cardRung(layout480round)));
  });
});
