/**
 * The relative and the leaderboard: the row the canvas draws, and the two rules that outlive it.
 *
 * The relative is the page a driver reads most and the canvas draws it at the smallest size in the
 * design, so what is pinned here is the anatomy of one row -- its height, its padding, the gap
 * between its cells, the size and the width of each of them -- and the count of rows each real zone
 * body gets. Those counts are the canvas's own, read off the face artboards rather than derived, so
 * a change to the row geometry that quietly costs a face a row fails here.
 *
 * The two rules are the ones the pages exist for. A relative puts the player on a row rather than on
 * the line between two, which is why the count is odd; and a list page keeps its position and its
 * gap whatever else it sheds, which is docs/design/readability-pass.md section 11 -- a long driver
 * name used to push the total over and the gap column was what paid.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { LEADERBOARD_COLUMNS } from '../src/modules/leaderboard.ts';
import { RELATIVE_COLUMNS } from '../src/modules/relative.ts';
import { densityForBox, type Density } from '../src/second/density.ts';
import { columnWidths } from '../src/second/table.ts';
import { walkItems } from '../src/walk.ts';
import { moduleBoxes } from './secondScreens.test.ts';
import type { Item, Rect, RectangleItem, TextItem } from '../src/generator.ts';

const build = (id: string, width: number, height: number, density: Density): Item[] =>
  MODULES.find((m) => m.id === id)!.build({ frame: rect(0, 0, width, height), density, prefix: '' });

const flat = (items: readonly Item[]): Item[] => items.flatMap((i) => [...walkItems([i])]);

/** The one stamped row definition, and how many copies of it the table asks SimHub for. */
function rowsOf(items: readonly Item[]): { count: number; pitch: number; cells: TextItem[]; names: string[] } {
  const all = flat(items);
  const stamped = all.find((i) => i.kind === 'layer' && i.name.endsWith('.rows'));
  if (!stamped || stamped.kind !== 'layer') throw new Error('no stamped rows layer');
  const cells = all.filter((i): i is TextItem => i.kind === 'text' && i.name.includes('.row.'));
  return { count: (stamped.repetitions ?? 0) + 1, pitch: stamped.repeatTopOffset ?? 0, cells, names: all.map((i) => i.name) };
}

const cell = (items: readonly Item[], id: string): TextItem => rowsOf(items).cells.find((c) => c.name.endsWith(`.row.${id}`))!;

/** The background band of the stamped row, which is the row's own rect. */
const rowBand = (items: readonly Item[]): Rect => flat(items).find((i): i is RectangleItem => i.kind === 'rect' && i.name.endsWith('.row.background'))!.rect;

/** What a binding evaluates to, as written. A gradient binding carries no formula and is not one here. */
const formulaOf = (item: TextItem, target: 'Text' | 'TextColor'): string => {
  const binding = item.bindings?.[target];
  return binding && typeof binding === 'object' && 'formula' in binding ? String(binding.formula) : '';
};

/**
 * The bodies the face artboards give the relative, which is the zone's rect less its chrome: a
 * 22 px title, 4 px under it and 6 px of padding top and bottom.
 */
const FACE_BODIES = [
  { zone: '469x320', width: 469, height: 282, rows: 7 },
  { zone: '469x258', width: 469, height: 220, rows: 5 },
  { zone: '469x554', width: 469, height: 516, rows: 13 },
  { zone: '600x150', width: 600, height: 112, rows: 3 },
  { zone: '360x470', width: 360, height: 432, rows: 11 },
];

describe('the relative lists what the canvas draws at each face', () => {
  for (const { zone, width, height, rows } of FACE_BODIES) {
    test(`${zone} lists ${rows}`, () => {
      const density = densityForBox({ width, height });
      const drawn = rowsOf(build('relative', width, height, density));
      expect({ zone, rows: drawn.count }).toEqual({ zone, rows });
      // The row and the 2 px between two of them, which is the pitch SimHub stamps by.
      expect({ zone, pitch: drawn.pitch }).toEqual({ zone, pitch: density === 'compact' ? 30 : 36 });
    });
  }

  test('and the count stays odd, so the player is a row rather than the line between two', () => {
    for (const { width, height } of FACE_BODIES) {
      const density = densityForBox({ width, height });
      expect({ width, height, odd: rowsOf(build('relative', width, height, density)).count % 2 }).toMatchObject({ odd: 1 });
    }
  });
});

describe('the row the canvas draws', () => {
  // The wide catalogue drawing, whose row is stated column by column on ZoneCatalogue: 34 px tall,
  // padded 0 6, cells 12 apart, position and gap at 34 and the car number and the name beneath them.
  const items = build('relative', 600, 242, 'zone');

  test('is 34 px tall with 6 px of side padding and 12 px between its cells', () => {
    expect(rowBand(items).height).toBe(34);
    expect(cell(items, 'pos').rect.left).toBe(6);
    // The car number starts one cell gap past the right edge of the position column.
    const pos = cell(items, 'pos');
    expect(cell(items, 'num').rect.left).toBe(pos.rect.left + pos.rect.width + 12);
  });

  test('draws the position and the gap at 34, the car number at 24 and the name at 13', () => {
    expect(cell(items, 'pos').fontSize).toBe(34);
    expect(cell(items, 'gap').fontSize).toBe(34);
    expect(cell(items, 'num').fontSize).toBe(24);
    expect(cell(items, 'name').fontSize).toBe(13);
  });

  test('and steps all three down one in the 28 px row a narrow zone takes', () => {
    const narrow = build('relative', 274, 262, 'compact');
    expect(rowBand(narrow).height).toBe(28);
    expect(cell(narrow, 'pos').fontSize).toBe(24);
    expect(cell(narrow, 'gap').fontSize).toBe(24);
    // The name stops at 13 rather than following the compact ramp to 12, which density.ts calls the
    // floor below which a label stops being readable at arm's length; ticket 10 is about this cell.
    expect(cell(narrow, 'name').fontSize).toBe(13);
  });

  test('and the canvas widths are the floor of every monospaced column', () => {
    // 40 for the position and 92 for the gap, which is what the canvas states at every shape that
    // draws them. They are a floor and not a number: at the 34 px row the position has to hold
    // "P24" and the gap "−12.345", which are a cell wider each, and WPF clips in silence.
    expect(columnWidths(['pos', 'name', 'gap'], 274, 'compact', 28)).toEqual([40, 106, 92]);
    const wide = columnWidths(['pos', 'name', 'gap'], 600, 'zone', 34);
    expect({ pos: wide[0], gap: wide[2] }).toEqual({ pos: 48, gap: 105 });
  });
});

/**
 * The padding is only there if every cell honours it.
 *
 * `table` insets the row by six and lays its columns out from that edge, which says nothing about
 * what the cells then draw: a box is given a width and WPF clips whatever overruns it, so a cell
 * that sizes itself -- the chips, and a numeral whose box is its own glyphs plus slack -- can end
 * outside the row it belongs to without any of the counts or the widths changing. The background
 * and the rule are the two items that do span the frame, being the row's furniture rather than
 * cells of it.
 *
 * The vertical slack is the fit walk's: a WPF line box is taller than its ink at both ends and
 * both tails are transparent.
 */
describe('a row draws its cells inside the padding', () => {
  const PAD_X = 6;

  for (const box of moduleBoxes()) {
    test(`on a ${box.name}`, () => {
      for (const id of ['relative', 'leaderboard']) {
        const items = MODULES.find((m) => m.id === id)!.build({ frame: box.frame, density: box.density, prefix: '' });
        const band = rowBand(items);
        for (const item of flat(items)) {
          if (item.kind === 'layer' || !item.name.includes('.row.')) continue;
          if (item.name.endsWith('.row.background') || item.name.endsWith('.row.rule')) continue;
          const above = item.kind === 'text' ? Math.ceil(0.1 * item.fontSize) + 2 : 1;
          const below = item.kind === 'text' ? Math.ceil(0.25 * item.fontSize) + 2 : 1;
          const r = item.rect;
          const inside =
            r.left >= band.left + PAD_X &&
            r.left + r.width <= band.left + band.width - PAD_X + 1 &&
            r.top >= band.top - above &&
            r.top + r.height <= band.top + band.height + below;
          expect({ id, box: box.name, item: item.name, rect: r, inside }).toMatchObject({ inside: true });
        }
      }
    });
  }
});

describe('a zone draws no header and the companion draws one', () => {
  test('no drawing on the catalogue or on a face artboard has a header row', () => {
    for (const density of ['zone', 'wide', 'compact'] as const) {
      const names = rowsOf(build('leaderboard', 600, 242, density)).names;
      expect({ density, head: names.filter((n) => n.includes('.head.')) }).toEqual({ density, head: [] });
    }
  });

  test('the companion page keeps its legend', () => {
    const names = rowsOf(build('leaderboard', 802, 336, 'companion')).names;
    expect(names.some((n) => n.includes('.head.pos'))).toBe(true);
  });
});

describe('the values a row writes', () => {
  const items = build('relative', 600, 242, 'zone');
  const formula = (id: string, binding: 'Text' | 'TextColor'): string => formulaOf(cell(items, id), binding);

  test('the position carries the P the canvas prefixes it with', () => {
    expect(cell(items, 'pos').text).toBe('P4');
    expect(formula('pos', 'Text')).toContain("('P') + (");
  });

  test('the own row reads YOU and a gap of nought, which is its own reference', () => {
    expect(formula('name', 'Text')).toContain("'YOU'");
    expect(formula('gap', 'Text')).toContain("'0.000'");
  });

  test('the leader reads Lead and the iRating reads thousands to one decimal', () => {
    const board = build('leaderboard', 802, 336, 'companion');
    expect(formulaOf(cell(board, 'gap'), 'Text')).toContain("'Lead'");
    const rating = build('relative', 600, 242, 'zone');
    // The column is declared but no shape keeps it yet, so the format is checked where it is written.
    expect(rowsOf(rating).cells.some((c) => c.name.endsWith('.row.rating'))).toBe(false);
  });

  test('the own row lifts its position, its name and its gap, and nothing else', () => {
    for (const id of ['pos', 'name', 'gap']) expect({ id, lifts: formula(id, 'TextColor').includes('#F5F7FA') }).toEqual({ id, lifts: true });
    // The car number stays in the label ink on every row, the player's included.
    expect(formula('num', 'TextColor')).toBe("'#5A6069'");
    const board = build('leaderboard', 802, 336, 'companion');
    for (const id of ['last', 'best']) {
      expect({ id, lifts: formulaOf(cell(board, id), 'TextColor').includes('#F5F7FA') }).toEqual({ id, lifts: false });
    }
  });
});

describe('a long name cannot cost a page its gap column', () => {
  // Readability-pass section 11. `fittingColumns` used to pop from the end until a full name fitted,
  // and with the gap drawn last that is the column that paid.
  for (const box of moduleBoxes()) {
    test(`on a ${box.name}`, () => {
      for (const id of ['relative', 'leaderboard']) {
        const names = rowsOf(MODULES.find((m) => m.id === id)!.build({ frame: box.frame, density: box.density, prefix: '' })).names;
        expect({ id, box: box.name, pos: names.some((n) => n.endsWith('.row.pos')), gap: names.some((n) => n.endsWith('.row.gap')) }).toMatchObject({ pos: true, gap: true });
      }
    });
  }
});

/**
 * Where the list sits inside the body the chrome leaves it.
 *
 * Every one of the seventeen zone bodies on `PitWallZones.dc.html` carries `justify-content:
 * center`, and so does every list body of the catalogue, whereas a table page used to stamp its
 * rows from the top of that rect and pile the remainder under them. The two are the same drawing
 * only when the rows happen to fill the body exactly, which is one box in twenty; in the 639 x 202
 * pit wall zone they were eight pixels apart. Nothing checked it, so it is checked here, at every
 * rectangle the build really hands a zone rather than at one of them.
 */
describe('a zone centres its list in the body it is given', () => {
  for (const box of moduleBoxes().filter((b) => b.density !== 'companion')) {
    test(`on a ${box.name}`, () => {
      for (const id of ['relative', 'leaderboard']) {
        const items = MODULES.find((m) => m.id === id)!.build({ frame: box.frame, density: box.density, prefix: '' });
        const { count, pitch } = rowsOf(items);
        const band = rowBand(items);
        // The pitch is the row and the gap under it, so the block is one row shorter than a count
        // of pitches: the last row is closed by the body's edge rather than by a gap of its own.
        const extent = (count - 1) * pitch + band.height;
        const above = band.top - box.frame.top;
        const below = box.frame.top + box.frame.height - (band.top + extent);
        // Within a pixel, because an odd remainder cannot be halved and `table` rounds up.
        expect({ id, box: box.name, above, below, centred: Math.abs(above - below) <= 1 }).toMatchObject({ id, box: box.name, centred: true });
        expect({ id, box: box.name, overflows: below < 0 }).toMatchObject({ overflows: false });
      }
    });
  }
});

describe('the column lists are the canvas order', () => {
  test('the leaderboard draws its two lap times before the gap', () => {
    expect(LEADERBOARD_COLUMNS.indexOf('gap')).toBeGreaterThan(LEADERBOARD_COLUMNS.indexOf('best'));
    expect(LEADERBOARD_COLUMNS.indexOf('best')).toBeGreaterThan(LEADERBOARD_COLUMNS.indexOf('last'));
  });

  test('and the relative ends on the gap to you', () => {
    expect(RELATIVE_COLUMNS[RELATIVE_COLUMNS.length - 1]).toBe('gap');
  });
});
