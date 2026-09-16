/**
 * Zone A's four pages: that every run is cut from the column rather than drawn at a size of its
 * own, and that what a page draws is centred in the column both ways.
 *
 * The sizes are literal on purpose. They are read off the face variants sheets, which draw each
 * page at the real zone rectangle -- A1's gear at 180 px in a 328 px column, at 173 in a 314 and at
 * 107 in a 194 -- and deriving them here would be a second opinion about the design rather than a
 * check on the code. `packages/dash/test/zoneFace.test.ts` owns the rectangles themselves.
 */
import { describe, expect, test } from 'bun:test';
import { ZONE_A_PAGES } from '../src/contract.ts';
import { GEAR_CHARS } from '../src/components/gear.ts';
import { measureText } from '../src/design/advances.ts';
import { CANVAS_BASELINE, LINE_SPACING, WPF_BASELINE, gearCells, monoWidth } from '../src/design/metrics.ts';
import { rect, type Rect } from '../src/design/geometry.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { faceOf } from './monoGlyphs.ts';
import { ZONE_FACES, gearSizeIn, layoutWithoutRevBar, zoneAPage } from '../src/zones/index.ts';

/** Every zone A rectangle the faces use, in both arrangements, once per distinct shape. */
const COLUMNS = (() => {
  const seen = new Map<string, { frame: Rect; faces: string[] }>();
  for (const face of ZONE_FACES) {
    for (const [layout, on] of [
      [face, true],
      [layoutWithoutRevBar(face), false],
    ] as const) {
      const a = layout.zones.zoneA;
      const key = `${a.width}x${a.height}`;
      const entry = seen.get(key) ?? { frame: rect(0, 0, a.width, a.height), faces: [] };
      entry.faces.push(on ? face.folder : `${face.folder} (rev bar off)`);
      seen.set(key, entry);
    }
  }
  return [...seen].map(([key, entry]) => ({ key, ...entry }));
})();

const page = (id: string, frame: Rect): Item[] => [...walkItems(zoneAPage(id, frame, `${id}.`))];
const texts = (items: readonly Item[]): TextItem[] => items.filter((i): i is TextItem => i.kind === 'text');
const named = (items: readonly Item[], suffix: string): TextItem => texts(items).find((i) => i.name.endsWith(suffix))!;

/**
 * How far a run's drawn box starts below its item rect, as a fraction of the font size.
 *
 * Two leadings. `textBox` puts the SimHub rect a tenth of the size above the canvas's em box so
 * that the baselines agree, and the canvas sets zone A at `line-height: 0.9`, so the box it centres
 * in the column is nine tenths of the size, half-leaded inside that em box. What the column centres
 * is therefore neither the item rect nor the em box, and measuring either would call a centred page
 * off-centre by the difference between the gear's size and the revs'.
 */
const LINE_HEIGHT = 0.9;
const LEADING = LINE_SPACING - WPF_BASELINE - (1 - CANVAS_BASELINE) + (1 - LINE_HEIGHT) / 2;

/** Where WPF puts a top-aligned run's baseline, which a value and the unit beside it must share. */
const baselineOf = (item: TextItem): number => item.rect.top + WPF_BASELINE * item.fontSize;

/**
 * What a run draws across, which is narrower than its box: the box takes a slack past the text so
 * that WPF clips nothing, and it takes it on the right, where a centred page would read as pushed.
 */
function drawnWidth(item: TextItem): number {
  const drawn = item.widest ?? item.text;
  const mono = item.monospace;
  if (!mono) return measureText(faceOf(item), drawn, item.fontSize);
  const specials = [...drawn].filter((c) => mono.specialChars?.includes(c) ?? false).length;
  return (drawn.length - specials) * mono.charWidth + specials * mono.specialCharsWidth;
}

/** The box a page actually draws in: its runs' 0.9 em boxes and, for the track map, its own rect. */
function extentOf(items: readonly Item[]): { top: number; bottom: number; left: number; right: number } {
  // A layer is a grouping and has no rect of its own; what it holds is walked alongside it.
  const boxes = items
    .filter((item) => item.kind !== 'layer')
    .map((item) => {
      if (item.kind !== 'text') return item.rect;
      return { ...item.rect, top: item.rect.top + LEADING * item.fontSize, height: LINE_HEIGHT * item.fontSize, width: drawnWidth(item) };
    });
  return {
    top: Math.min(...boxes.map((b) => b.top)),
    bottom: Math.max(...boxes.map((b) => b.top + b.height)),
    left: Math.min(...boxes.map((b) => b.left)),
    right: Math.max(...boxes.map((b) => b.left + b.width)),
  };
}

/**
 * What each column draws, read off the face variants sheets where they give it and taken from the
 * shares where they draw the page at half scale. A1 is the gear, the speed and the revs; A2 is the
 * gear alone; A3 is the speed and the gear under it.
 */
const DRAWN: Record<string, { a1: [number, number, number]; a2: number; a3: [number, number] }> = {
  '260x194': { a1: [107, 37, 21], a2: 158, a3: [85, 47] },
  '260x226': { a1: [124, 43, 25], a2: 185, a3: [99, 54] },
  '600x234': { a1: [129, 44, 26], a2: 191, a3: [103, 56] },
  '340x258': { a1: [142, 49, 28], a2: 211, a3: [114, 62] },
  '600x268': { a1: [147, 51, 29], a2: 220, a3: [118, 64] },
  '340x292': { a1: [161, 55, 32], a2: 240, a3: [128, 70] },
  '380x314': { a1: [173, 60, 35], a2: 258, a3: [138, 75] },
  '340x320': { a1: [176, 61, 35], a2: 263, a3: [141, 77] },
  '300x328': { a1: [180, 62, 36], a2: 270, a3: [144, 79] },
  '380x358': { a1: [197, 68, 39], a2: 295, a3: [158, 86] },
  '340x361': { a1: [199, 69, 40], a2: 297, a3: [159, 87] },
  '300x366': { a1: [201, 70, 40], a2: 301, a3: [161, 88] },
  // The speed is the one run a column can be too narrow for: at 340 the width stops it before the
  // height does, and the sheets draw 204 there. The code reaches 212, because the speed is a
  // SemiBold numeral again and SemiBold cells are narrower than the Bold ones it used to be
  // measured in, so the same column holds eight pixels more. docs/research/design-audit.md carries
  // the question; the 204 is the only number on these sheets the build no longer reproduces.
  '340x554': { a1: [305, 105, 61], a2: 458, a3: [212, 133] },
  // The one column where the gear is stopped by its neighbours rather than by its own share: a
  // 0.42 ghost either side of a 329 px gear wants 344 px of a 340 px column, so the cluster settles
  // at 320. It is a rev-bar-off arrangement, which no sheet draws, so 329 was a share rather than a
  // reading; the three sheets that do draw ghosts all set them at 0.42 of the gear.
  '340x598': { a1: [320, 114, 66], a2: 495, a3: [212, 144] },
};

describe('zone A shares its column', () => {
  test('every column the faces use has a drawing to check against', () => {
    expect(COLUMNS.map((c) => c.key).sort()).toEqual(Object.keys(DRAWN).sort());
  });

  for (const column of COLUMNS) {
    const drawn = DRAWN[column.key]!;

    test(`A1 at ${column.key} is the gear, the speed and the revs at 55, 19 and 11 per cent`, () => {
      const items = page('gearSpeedRevs', column.frame);
      const sizes = [named(items, 'main.gear'), named(items, '.speed'), named(items, '.revs')].map((i) => i.fontSize);
      expect({ column: column.key, faces: column.faces, sizes }).toMatchObject({ sizes: drawn.a1 });
      // Each is its share of the column, give or take the pixel the width fitter may take back.
      const shares = [0.55, 0.19, 0.11];
      sizes.forEach((fs, i) => expect(fs).toBeLessThanOrEqual(Math.round(column.frame.height * shares[i]!)));
    });

    test(`A2 at ${column.key} fills the column with the gear`, () => {
      const items = page('gearAlone', column.frame);
      const gear = named(items, 'main.gear');
      expect({ column: column.key, size: gear.fontSize }).toMatchObject({ size: drawn.a2 });
      // Cut from the height, not capped: the gear is the whole line box the column can hold, so
      // nothing but the leading is left over, and the 1280x720 column draws far past ds.size.gear.
      expect(gear.fontSize).toBe(gearSizeIn(column.frame));
      expect(gear.rect.height).toBeGreaterThanOrEqual(column.frame.height - Math.ceil(LINE_SPACING) - 1);
      expect(gear.fontSize / column.frame.height).toBeGreaterThan(0.8);
    });

    test(`A3 at ${column.key} leads with the speed and keeps the gear under it in the secondary ink`, () => {
      const items = page('speed', column.frame);
      const speed = named(items, '.speed');
      const gear = named(items, '.gear');
      expect({ column: column.key, sizes: [speed.fontSize, gear.fontSize] }).toMatchObject({ sizes: drawn.a3 });
      expect(speed.fontSize).toBeGreaterThan(gear.fontSize);
      expect(gear.textColor).toBe(ds.color.text.secondary);
      expect(speed.textColor).toBe(ds.color.text.primary);
      // The speed is the page's one big value and is still a numeral: every sheet sets a numeral in
      // 600 and the current gear alone in 700, and zone A's catalogue is on every face, so a Bold
      // speed was a second Bold on every screen the build emits. The size is unchanged, the solver
      // landing on the same share of the column in either face.
      expect(speed.fontWeight).toBe('SemiBold');
      expect(baselineOf(speed)).toBeGreaterThan(0);
    });

    for (const meta of ZONE_A_PAGES) {
      test(`${meta.id} at ${column.key} is centred in the column both ways`, () => {
        const items = page(meta.id, column.frame);
        expect(items.length).toBeGreaterThan(0);
        const e = extentOf(items);
        const above = e.top - column.frame.top;
        const below = column.frame.top + column.frame.height - e.bottom;
        const left = e.left - column.frame.left;
        const right = column.frame.left + column.frame.width - e.right;
        // Within a pixel, which is all an integer-snapped rect can promise.
        const centred = (a: number, b: number): boolean => Math.abs(a - b) <= 1;
        expect({ page: meta.id, column: column.key, above, below, centred: centred(above, below) }).toMatchObject({ centred: true });
        expect({ page: meta.id, column: column.key, left, right, centred: centred(left, right) }).toMatchObject({ centred: true });
        expect(above).toBeGreaterThan(0);
      });
    }
  }
});

describe("what zone A's pages say beside their values", () => {
  const frame = rect(0, 0, 380, 314);

  test('a unit sits beside its value on the same baseline, not under it', () => {
    for (const [id, pairs] of [
      ['gearSpeedRevs', [['.speed', '.speed.unit'] as const, ['.revs', '.revs.unit'] as const]],
      ['speed', [['.speed', '.speed.unit'] as const, ['.gear', '.gear.label'] as const]],
    ] as const) {
      const items = page(id, frame);
      for (const [value, follower] of pairs) {
        const v = named(items, value);
        const f = named(items, follower);
        expect({ id, follower, apart: Math.abs(baselineOf(v) - baselineOf(f)) }).toMatchObject({ apart: expect.closeTo(0, 0) });
        // Beside, which means after the value's cells: a unit under its value would start at the
        // value's own left edge. Measured from the cells rather than the box, which the slack widens.
        expect(f.rect.left).toBeGreaterThanOrEqual(v.rect.left + drawnWidth(v));
        expect(f.fontSize).toBeLessThan(v.fontSize);
      }
    }
  });

  test('only the gear is Bold, and the revs are in the secondary ink', () => {
    const items = page('gearSpeedRevs', frame);
    expect(named(items, '.speed').fontWeight).toBe('SemiBold');
    expect(named(items, '.revs').textColor).toBe(ds.color.text.secondary);
    expect(named(items, 'main.gear').fontWeight).toBe('Bold');
  });
});

describe('the ghosted gears either side of the one a driver is in', () => {
  const frame = rect(0, 0, 380, 314);
  const items = page('gearSpeedRevs', frame);
  const gear = named(items, 'main.gear');
  const below = named(items, 'gear.below');
  const above = named(items, 'gear.above');

  test("they are spaced off the gear's own cell, not off the column edges", () => {
    const cell = monoWidth(gearCells(gear.fontSize), GEAR_CHARS);
    const ghost = monoWidth(gearCells(below.fontSize), GEAR_CHARS);
    const gap = ds.space[3];
    expect(below.rect.left + ghost).toBeCloseTo(gear.rect.left - gap, 0);
    expect(above.rect.left).toBeCloseTo(gear.rect.left + cell + gap, 0);
    // Both ghosts, and the gear, centred on the column as one cluster.
    expect((below.rect.left + above.rect.left + ghost) / 2).toBeCloseTo(frame.width / 2, 0);
  });

  test("they are dim, and the one above is hidden in the car's top gear", () => {
    for (const ghost of [below, above]) expect(ghost.textColor).toBe(ds.color.text.dim);
    expect(above.bindings?.Visible).toBeDefined();
    expect(String(above.bindings!.Visible!.formula)).toContain('DriverCarGearNumForward');
    expect(below.bindings?.Visible).toBeUndefined();
  });

  test('neither end of the box invents a gear the sim never reports', () => {
    const bind = (item: TextItem): string => String(item.bindings!.Text!.formula);
    // Eighth gear is the last SimHub names, so there is no ninth to ghost above it.
    expect(bind(above)).not.toContain("'9'");
    expect(bind(below)).not.toContain("'0'");
    expect(bind(above)).toContain("if(([DataCorePlugin.GameData.Gear]) = ('7'), '8'");
  });
});
