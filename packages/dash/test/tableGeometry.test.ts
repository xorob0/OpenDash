/**
 * The board: the table the three pit wall pages draw, which is not the table a zone draws.
 *
 * `ZoneCatalogue.dc.html` and the pit wall artboards disagree about every number in a row, and the
 * disagreement is deliberate on both sides: a zone's list is read at arm's length from a driver's
 * seat and a board is read across a garage. What is pinned here is the board half -- the 32 px
 * column-label row, the 16 px inset, the rule that closes every row, the 15 px name under 24 px
 * numerals and the 16 px the tower brings them down to -- together with the two cases that must
 * not move with it: a zone's list, and the companion's leaderboard, which draws a legend over the
 * catalogue's row rather than a board's.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { PORTRAIT_COLUMNS, RACE_COLUMNS, TOWER_COLUMNS, portraitPage, racePage, towerPage } from '../src/screens/pitwall.ts';
import { PIT_WALL_HEADER } from '../src/screens/pitwallHeader.ts';
import { contentRect } from '../src/second/layout.ts';
import { columnWidths, type ColumnId } from '../src/second/table.ts';
import { COMPANION_SIZES, companionGeometry } from '../src/screens/index.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import type { Item, RectangleItem, TextItem } from '../src/generator.ts';

/**
 * The gap between two cells of a board's row, which is none: `.trow` declares no `gap` on any of
 * the four artboards and its cells are `flex: none`, so two columns meet and what separates their
 * values is the slack inside the wider of them. A list spaces its cells 12 apart instead, which
 * `tables.test.ts` is where.
 */
const CELL_GAP = 0;

/**
 * The companion page's own box, taken from the geometry that hands it out.
 *
 * It was written here as 802 by 336 while the flag band was wrongly 32 px tall. The band is the
 * artboard's 12 now and the page is 356, so the fixtures below were measuring a rectangle the build
 * stopped producing; they passed, which is what a literal copied out of a build does until the
 * build moves under it.
 */
const COMPANION_PAGE = contentRect(companionGeometry(COMPANION_SIZES.find((s) => s.folder === 'openDash Companion')!).module, 'companion');


interface Board {
  page: string;
  items: Item[];
  columns: readonly ColumnId[];
  /** Left edge and width of the board's frame, read off the row band rather than restated. */
  left: number;
  width: number;
  rowHeight: number;
}

function board(page: string, screen: { items: Item[] }, columns: readonly ColumnId[]): Board {
  const items = [...walkItems(screen.items)].filter((i) => i.name.includes(`${page}.board.`));
  const band = items.find((i): i is RectangleItem => i.name.endsWith('.row.background'))!;
  return { page, items, columns, left: band.rect.left, width: band.rect.width, rowHeight: band.rect.height };
}

const BOARDS = [
  { ...board('race', racePage(1920, 1080), RACE_COLUMNS), header: 32 },
  { ...board('tower', towerPage(1920, 1080), TOWER_COLUMNS), header: 28 },
  { ...board('portrait', portraitPage(1080, 1920), PORTRAIT_COLUMNS), header: 32 },
];

const text = (b: Board, id: string): TextItem => b.items.find((i): i is TextItem => i.kind === 'text' && i.name.endsWith(`.row.${id}`))!;
const box = (b: Board, name: string): RectangleItem => b.items.find((i): i is RectangleItem => i.kind === 'rect' && i.name.endsWith(name))!;

/** Where each column of a board begins and ends, laid out as `table()` lays them. */
function columnEdges(b: Board): Map<ColumnId, { left: number; right: number }> {
  const widths = columnWidths(b.columns, b.width, 'zone', b.rowHeight, true);
  const out = new Map<ColumnId, { left: number; right: number }>();
  let x = b.left + 16;
  b.columns.forEach((id, i) => {
    const width = widths[i] ?? 0;
    out.set(id, { left: x, right: x + width });
    x += width + CELL_GAP;
  });
  return out;
}

describe('a board is padded and ruled the way every pit wall artboard draws it', () => {
  for (const b of BOARDS) {
    test(`${b.page}: the column labels sit in a ${b.header} px row closed by a rule`, () => {
      const rule = box(b, '.head.rule');
      expect({ page: b.page, top: rule.rect.top, height: rule.rect.height, colour: rule.backgroundColor }).toEqual({
        page: b.page,
        top: PIT_WALL_HEADER.height + b.header - 1,
        height: 1,
        colour: ds.color.surface.raised,
      });
      // The rule runs the frame, under the padding the labels are inset by.
      expect({ page: b.page, left: rule.rect.left, width: rule.rect.width }).toEqual({ page: b.page, left: b.left, width: b.width });
    });

    test(`${b.page}: every row is inset 16 px and closed by its own rule`, () => {
      const first = b.columns[0]!;
      const last = b.columns[b.columns.length - 1]!;
      const edges = columnEdges(b);
      expect({ page: b.page, left: edges.get(first)!.left, right: edges.get(last)!.right }).toEqual({ page: b.page, left: b.left + 16, right: b.left + b.width - 16 });
      const rule = box(b, '.row.rule');
      expect({ page: b.page, height: rule.rect.height, width: rule.rect.width, colour: rule.backgroundColor }).toEqual({ page: b.page, height: 1, width: b.width, colour: ds.color.surface.raised });
      // The rule closes the row, so it is the row's last pixel and two rows are flush.
      const band = box(b, '.row.background');
      expect({ page: b.page, bottom: rule.rect.top + 1 }).toEqual({ page: b.page, bottom: band.rect.top + b.rowHeight });
      const stamped = b.items.find((i) => i.kind === 'layer' && i.name.endsWith('.board.rows'));
      expect({ page: b.page, pitch: stamped?.kind === 'layer' ? stamped.repeatTopOffset : 0 }).toEqual({ page: b.page, pitch: b.rowHeight });
    });

    test(`${b.page}: the rows open against the header rule rather than being centred under it`, () => {
      // The board's column carries no `justify-content` on any of the four artboards, so a field
      // shorter than the board leaves its empty rows at the foot. A list is the opposite case and
      // `tables.test.ts` is where that one is pinned.
      const band = box(b, '.row.background');
      expect({ page: b.page, top: band.rect.top }).toEqual({ page: b.page, top: PIT_WALL_HEADER.height + b.header });
    });

    test(`${b.page}: the name is Barlow 500 15 under numerals the row's height decides`, () => {
      const lead = b.rowHeight >= 32 ? 24 : 16;
      expect({ page: b.page, name: text(b, 'name').fontSize, font: text(b, 'name').font, weight: text(b, 'name').fontWeight }).toEqual({ page: b.page, name: 15, font: ds.font.label, weight: 'Medium' });
      for (const id of ['pos', 'gap', 'last', 'best']) expect({ page: b.page, id, fs: text(b, id).fontSize }).toEqual({ page: b.page, id, fs: lead });
      // The car number and the rank count are the canvas's 16 whatever the numerals beside them do.
      expect({ page: b.page, num: text(b, 'num').fontSize }).toEqual({ page: b.page, num: 16 });
    });

    test(`${b.page}: a right-aligned value ends on its column's edge`, () => {
      const edges = columnEdges(b);
      for (const id of ['pos', 'gap', 'last', 'best'] as const) {
        const cell = text(b, id);
        expect({ page: b.page, id, align: cell.hAlign, right: cell.rect.left + cell.rect.width }).toEqual({ page: b.page, id, align: 'right', right: edges.get(id)!.right });
      }
    });

    test(`${b.page}: the position reads P4 rather than 4`, () => {
      const pos = text(b, 'pos');
      expect({ page: b.page, sample: pos.text }).toEqual({ page: b.page, sample: 'P4' });
      const binding = pos.bindings?.Text;
      expect(binding && typeof binding === 'object' && 'formula' in binding ? String(binding.formula) : '').toContain("('P') + (");
    });
  }
});

describe('a chip on a board is 20 tall with 6 either side of its text', () => {
  const race = BOARDS[0]!;

  test('the class chip', () => {
    const block = box(race, '.row.class.block');
    const label = race.items.find((i): i is TextItem => i.name.endsWith('.row.class.text'))!;
    expect({ height: block.rect.height, padLeft: label.rect.left - block.rect.left, width: label.rect.width }).toEqual({ height: 20, padLeft: 6, width: block.rect.width - 12 });
  });

  test('and the tyre chip is drawn at the right edge of a right-aligned column', () => {
    const block = box(race, '.row.tyre.block');
    expect({ height: block.rect.height, right: block.rect.left + block.rect.width }).toEqual({ height: 20, right: columnEdges(race).get('tyre')!.right });
  });

  test('and the PIT chip ends where the count it replaces ends', () => {
    const block = box(race, '.row.pitChip.block');
    const count = text(race, 'pit');
    expect({ height: block.rect.height, right: block.rect.left + block.rect.width }).toEqual({ height: 20, right: count.rect.left + count.rect.width });
  });
});

describe('the other two tables keep the row they already drew', () => {
  const build = (id: string, width: number, height: number, density: 'zone' | 'companion'): Item[] =>
    [...walkItems(MODULES.find((m) => m.id === id)!.build({ frame: rect(0, 0, width, height), density, prefix: '' }))];

  test('a zone list is padded 6, spaced 2 and ruled nowhere', () => {
    const items = build('relative', 600, 242, 'zone');
    expect(items.find((i) => i.name.endsWith('.row.rule'))).toBeUndefined();
    expect(items.find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.row.pos'))!.rect.left).toBe(6);
    const stamped = items.find((i) => i.kind === 'layer' && i.name.endsWith('.rows'));
    expect(stamped?.kind === 'layer' ? stamped.repeatTopOffset : 0).toBe(36);
  });

  test('and the companion draws a legend over that row rather than a board', () => {
    // The header is what tells a board from a list, and only on a pit wall page: the companion
    // heads its leaderboard and still draws the catalogue's row underneath.
    const items = build('leaderboard', COMPANION_PAGE.width, COMPANION_PAGE.height, 'companion');
    expect(items.some((i) => i.name.includes('.head.pos'))).toBe(true);
    expect(items.find((i) => i.name.endsWith('.row.rule'))).toBeUndefined();
    expect(items.find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.row.pos'))!.rect.left).toBe(6);
  });
});
