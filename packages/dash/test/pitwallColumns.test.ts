/**
 * Which columns each board draws, and how wide each one is.
 *
 * The numbers here are read off the three artboards' own `.th` rows rather than derived: every cell
 * of `PitWall1920x1080`, `PitWallTower1920x1080` and `PitWall1080x1920` carries a `width` and a
 * `text-align` in its style attribute, and this file is that list. They are a different set from
 * the catalogue's, which `tables.test.ts` holds, and the two must not be reconciled: a board is
 * read across a garage and a list at arm's length.
 *
 * Three of the canvas's columns are not drawn and are pinned here as absent rather than quietly
 * forgotten. Nat is a flag per country, Licence is the iRacing licence class with its safety
 * rating and Inc is an incident count; all three are session-YAML values with no per-leaderboard-
 * row reader, so a board that drew them would be drawing data that is not there. Their widths are
 * kept in {@link BLOCKED} so that each page's total can still be checked against the canvas's.
 */
import { describe, expect, test } from 'bun:test';
import { PORTRAIT_COLUMNS, RACE_COLUMNS, TOWER_COLUMNS, portraitPage, racePage, towerPage } from '../src/screens/pitwall.ts';
import { columnWidths, type ColumnId } from '../src/second/table.ts';
import { walkItems } from '../src/walk.ts';
import type { Item, RectangleItem, TextItem } from '../src/generator.ts';

/** A column of a board's header row: its id here, and the width and alignment its artboard states. */
interface Column {
  id: ColumnId;
  width: number;
  align: 'left' | 'right';
}

const col = (id: ColumnId, width: number, align: 'left' | 'right'): Column => ({ id, width, align });

/**
 * The driver column, which is the one number of a board this code does not take literally.
 *
 * The canvas fixes it at 190 and lets the row stop short of its right edge; with two of the
 * eighteen columns unfillable that would leave a sixth of the race board blank, so the remainder
 * goes to the name instead and 190 is its floor. It is the column WPF punishes for being narrow --
 * `cellName` has no ellipsis to give and falls back to a three-letter code -- so the floor is the
 * half of the statement worth holding.
 */
const DRIVER = 190;

/** The canvas's widths for the three columns no board draws, which its totals still count. */
const BLOCKED = { nat: 28, licence: 86, inc: 40 } as const;

interface Board {
  page: string;
  columns: readonly ColumnId[];
  canvas: readonly Column[];
  /** The canvas's own column total, with the rank at the 36 its cell takes rather than the 34 its label does. */
  total: number;
  /** What the canvas counts in that total but no board draws. */
  blocked: number;
  rowHeight: number;
  items: Item[];
}

/**
 * The race board: eighteen columns in 1280, of which fifteen are drawable.
 *
 * The artboard's `.th` sums to 1216 with the rank label at 34; the rank cell is 36, which is the
 * width the column takes, so the total checked against is two more.
 */
const RACE: readonly Column[] = [
  col('pos', 44, 'right'),
  col('rank', 36, 'right'),
  col('num', 44, 'left'),
  col('name', DRIVER, 'left'),
  col('class', 56, 'left'),
  col('rating', 76, 'right'),
  col('gap', 92, 'right'),
  col('int', 84, 'right'),
  col('last', 92, 'right'),
  col('best', 92, 'right'),
  col('s1', 58, 'right'),
  col('s2', 58, 'right'),
  col('s3', 58, 'right'),
  col('pit', 44, 'right'),
  col('tyre', 40, 'right'),
];

/** The tower, which draws neither the interval nor the iRating and stops at the pit count. */
const TOWER: readonly Column[] = [
  col('pos', 44, 'right'),
  col('rank', 36, 'right'),
  col('num', 44, 'left'),
  col('name', DRIVER, 'left'),
  col('class', 56, 'left'),
  col('gap', 92, 'right'),
  col('last', 92, 'right'),
  col('best', 92, 'right'),
  col('pit', 44, 'right'),
];

/** The portrait board, which spends the three sector columns on the iRating instead. */
const PORTRAIT: readonly Column[] = [
  col('pos', 44, 'right'),
  col('rank', 36, 'right'),
  col('num', 44, 'left'),
  col('name', DRIVER, 'left'),
  col('class', 56, 'left'),
  col('rating', 76, 'right'),
  col('gap', 92, 'right'),
  col('int', 84, 'right'),
  col('last', 92, 'right'),
  col('best', 92, 'right'),
  col('pit', 44, 'right'),
  col('tyre', 40, 'right'),
];

const board = (page: string, screen: { items: Item[] }, rest: Omit<Board, 'page' | 'items'>): Board => ({
  ...rest,
  page,
  items: [...walkItems(screen.items)].filter((i) => i.name.includes(`${page}.board.`)),
});

const BOARDS: Board[] = [
  board('race', racePage(1920, 1080), { columns: RACE_COLUMNS, canvas: RACE, total: 1218, blocked: BLOCKED.nat + BLOCKED.licence + BLOCKED.inc, rowHeight: 34 }),
  board('tower', towerPage(1920, 1080), { columns: TOWER_COLUMNS, canvas: TOWER, total: 804, blocked: BLOCKED.nat + BLOCKED.licence, rowHeight: 28 }),
  board('portrait', portraitPage(1080, 1920), { columns: PORTRAIT_COLUMNS, canvas: PORTRAIT, total: 1004, blocked: BLOCKED.nat + BLOCKED.licence, rowHeight: 32 }),
];

/** The frame a board was given, read off its own row band rather than restated from the page. */
const frameOf = (b: Board): { left: number; width: number } => {
  const band = b.items.find((i): i is RectangleItem => i.kind === 'rect' && i.name.endsWith('.row.background'))!;
  return { left: band.rect.left, width: band.rect.width };
};

const widthsOf = (b: Board): number[] => columnWidths(b.columns, frameOf(b).width, 'zone', b.rowHeight, true);

const cellOf = (b: Board, id: ColumnId): TextItem | undefined => b.items.find((i): i is TextItem => i.kind === 'text' && i.name.endsWith(`.row.${id}`));

const headerOf = (b: Board, id: ColumnId): TextItem | undefined => b.items.find((i): i is TextItem => i.kind === 'text' && i.name.endsWith(`.head.${id}`));

describe('each board draws the columns its artboard heads', () => {
  for (const b of BOARDS) {
    test(`${b.page}: in the canvas's order`, () => {
      expect({ page: b.page, columns: [...b.columns] }).toEqual({ page: b.page, columns: b.canvas.map((c) => c.id) });
    });

    test(`${b.page}: and none of the three the canvas draws from data nobody publishes`, () => {
      expect({ page: b.page, blocked: b.columns.filter((id) => id === 'flag' || id === 'licence') }).toEqual({ page: b.page, blocked: [] });
    });

    test(`${b.page}: at the widths its header row states`, () => {
      const widths = widthsOf(b);
      const drawn = b.canvas.map((c, i) => ({ id: c.id, width: c.id === 'name' ? DRIVER : (widths[i] ?? 0) }));
      expect({ page: b.page, drawn }).toEqual({ page: b.page, drawn: b.canvas.map((c) => ({ id: c.id, width: c.width })) });
    });

    test(`${b.page}: with the driver column no narrower than the ${DRIVER} the canvas fixes it at`, () => {
      const widths = widthsOf(b);
      const name = widths[b.columns.indexOf('name')] ?? 0;
      expect({ page: b.page, atLeast: name >= DRIVER }).toEqual({ page: b.page, atLeast: true });
    });

    test(`${b.page}: summing to the canvas's ${b.total} px less what it cannot fill`, () => {
      const widths = widthsOf(b);
      const drawn = b.canvas.reduce((sum, c, i) => sum + (c.id === 'name' ? DRIVER : (widths[i] ?? 0)), 0);
      expect({ page: b.page, canvas: drawn + b.blocked }).toEqual({ page: b.page, canvas: b.total });
      // And the row itself spans the frame it was given, less the 16 px it is inset either side:
      // what the blocked columns would have taken is the driver column's until they can be filled.
      const laid = widths.reduce((sum, w) => sum + w, 0);
      expect({ page: b.page, laid }).toEqual({ page: b.page, laid: frameOf(b).width - 32 });
    });

    test(`${b.page}: aligned as the canvas aligns them`, () => {
      const drawn = b.canvas.filter((c) => cellOf(b, c.id)).map((c) => ({ id: c.id, align: cellOf(b, c.id)!.hAlign }));
      expect({ page: b.page, drawn }).toEqual({ page: b.page, drawn: b.canvas.filter((c) => cellOf(b, c.id)).map((c) => ({ id: c.id, align: c.align })) });
    });
  }
});

describe('the iRating column', () => {
  for (const b of BOARDS.filter((x) => x.columns.includes('rating'))) {
    test(`${b.page}: is headed by the word rather than the abbreviation`, () => {
      expect({ page: b.page, header: headerOf(b, 'rating')?.text }).toEqual({ page: b.page, header: 'IRATING' });
    });

    test(`${b.page}: and is drawn at 24 px, right aligned, in thousands to one decimal`, () => {
      const cell = cellOf(b, 'rating')!;
      expect({ page: b.page, fs: cell.fontSize, align: cell.hAlign, sample: cell.text }).toEqual({ page: b.page, fs: 24, align: 'right', sample: '4.6k' });
      const binding = cell.bindings?.Text;
      expect(binding && typeof binding === 'object' && 'formula' in binding ? String(binding.formula) : '').toContain("'k'");
    });
  }

  test('and the tower, whose artboard does not head one, draws none', () => {
    expect(TOWER_COLUMNS.includes('rating')).toBe(false);
  });
});

describe('the columns no artboard heads are gone from the boards that drew them', () => {
  test('the stint count, which the race board drew where the canvas draws the iRating', () => {
    expect(RACE_COLUMNS.includes('stint')).toBe(false);
  });

  test('the interval, which the tower drew and its artboard does not', () => {
    expect(TOWER_COLUMNS.includes('int')).toBe(false);
  });

  test('and the three sectors, which the portrait board drew where the canvas draws Nat, Licence and iRating', () => {
    expect(PORTRAIT_COLUMNS.filter((id) => id === 's1' || id === 's2' || id === 's3')).toEqual([]);
  });
});
