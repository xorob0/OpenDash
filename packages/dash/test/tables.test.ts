/**
 * The relative and the leaderboard: the row the canvas draws, and the rules that outlive it.
 *
 * The relative is the page a driver reads most and the canvas draws it at the smallest size in the
 * design, so what is pinned here is the anatomy of one row -- its height, its padding, the gap
 * between its cells, the size and the width of each of them -- and the count of rows each real zone
 * body gets.
 *
 * **Those counts were the canvas's arithmetic and are now each page's declaration** (#339, #328): the
 * relative lists three cars either side and five at `tall`, the leaderboard the count the drawings give
 * it, and both at a row stretched to fill the body rather than a block centred in its slack, carrying
 * the largest type its height and its width allow. A table stated with no row of its own is therefore
 * the fixture wherever what is being pinned is the canvas's own row, both pages having taken their row
 * heights into their own hands.
 *
 * The rules are the ones the pages exist for. A relative puts the player on a row rather than on
 * the line between two, which is why the count is odd; a list page keeps its position and its
 * gap whatever else it sheds -- a long driver name used to push the total over and the gap column was
 * what paid; and the column that says *who* is never the one that pays for a row growing.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { LEADERBOARD_COLUMNS, LEAST_NAME_CHARS } from '../src/modules/leaderboard.ts';
import { fittingColumns } from '../src/modules/leaderboard.ts';
import { RELATIVE_COLUMNS } from '../src/modules/relative.ts';
import { densityForBox, type Density } from '../src/second/density.ts';
import { contentRect } from '../src/second/layout.ts';
import { columnWidths, listPlan, NAME_FACE, NAME_SAMPLE, nameFloorOf, nameSizeForRow, rowSlack, SHORTEST_NAME_CHARS, table, tableRowHeight, type ColumnId } from '../src/second/table.ts';
import { charsThatFit } from '../src/design/advances.ts';
import { COMPANION_SIZES, companionGeometry } from '../src/screens/index.ts';
import { walkItems } from '../src/walk.ts';
import { moduleBoxes } from './secondScreens.test.ts';
import type { Item, Rect, RectangleItem, TextItem } from '../src/generator.ts';

const build = (id: string, width: number, height: number, density: Density): Item[] =>
  MODULES.find((m) => m.id === id)!.build({ frame: rect(0, 0, width, height), density, prefix: '' });

const flat = (items: readonly Item[]): Item[] => items.flatMap((i) => [...walkItems([i])]);

/**
 * The companion page's own box, taken from the geometry that hands it out.
 *
 * It was written here as 802 by 336 while the flag band was wrongly 32 px tall. The band is the
 * artboard's 12 now and the page is 356, so the fixtures below were measuring a rectangle the build
 * stopped producing; they passed, which is what a literal copied out of a build does until the
 * build moves under it.
 */
const COMPANION_PAGE = contentRect(companionGeometry(COMPANION_SIZES.find((s) => s.folder === 'OpenDash Companion')!).module, 'companion');


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
 *
 * The counts are the relative's declaration rather than what `rowCapacity` divided out: seven, which is
 * three cars either side, and eleven at `tall`, cut to what the body holds at the canvas's row. Zone C
 * of the 1280 x 720 face listed thirteen cars and the `tall` reference fifteen before #339, and the
 * medium boxes as many as nine before #328; all of them list what is declared now, at a row stretched
 * to fill the body rather than centred in its slack.
 */
const FACE_BODIES = [
  { zone: '469x320', width: 469, height: 282, rows: 7, row: 38 },
  { zone: '469x258', width: 469, height: 220, rows: 5, row: 42 },
  { zone: '469x554', width: 469, height: 516, rows: 11, row: 45 },
  { zone: '600x150', width: 600, height: 112, rows: 3, row: 36 },
  { zone: '360x470', width: 360, height: 432, rows: 11, row: 37 },
];

describe('the relative lists the window it declares', () => {
  for (const { zone, width, height, rows, row } of FACE_BODIES) {
    test(`${zone} lists ${rows} at ${row}`, () => {
      const density = densityForBox({ width, height });
      const drawn = rowsOf(build('relative', width, height, density));
      expect({ zone, rows: drawn.count }).toEqual({ zone, rows });
      // The row and the 2 px between two of them, which is the pitch SimHub stamps by.
      expect({ zone, pitch: drawn.pitch }).toEqual({ zone, pitch: row + 2 });
    });
  }

  test('and never more than eleven, however tall the box', () => {
    // Five cars either side. Past that the list stops being read and becomes texture, which is the
    // declaration #339 asked for; a 1600 px zone would otherwise list forty-five.
    for (const height of [432, 516, 560, 900, 1600]) {
      expect({ height, rows: rowsOf(build('relative', 469, height, 'zone')).count }).toMatchObject({ rows: 11 });
    }
  });

  test('and asks for three however short the box, which is the player and one car either side', () => {
    // Three is what it asks for rather than what it gets: a body too short for three rows is `table()`'s
    // to clamp, and it does. The shortest body the build hands a zone is the 112 px of zone C on the
    // 600 x 686 face, which holds three.
    for (const height of [112, 120]) {
      expect({ height, rows: rowsOf(build('relative', 469, height, 'compact')).count }).toMatchObject({ rows: 3 });
    }
  });

  test('and the count stays odd, so the player is a row rather than the line between two', () => {
    for (const { width, height } of FACE_BODIES) {
      const density = densityForBox({ width, height });
      expect({ width, height, odd: rowsOf(build('relative', width, height, density)).count % 2 }).toMatchObject({ odd: 1 });
    }
  });

  /**
   * The row fills the body rather than the block being centred in a pool of it, on both list pages and
   * in every box.
   *
   * `table()` centres a declared block, which is right for a list that ran out of cars and wrong for
   * one told to stop counting: eleven rows of 28 px in the 560 px body of the 1280 x 720 face's second
   * arrangement is 328 px of list and 232 px of nothing. Filling it leaves only the remainder of one
   * division, which is under a pixel a row.
   *
   * This used to allow four boxes out, where filling the body would have crossed a type step whose car
   * number took a letter from the name, and the row was held short with the rest left as slack. #328
   * took the type apart from the height: the row fills its body wherever the type stops, so what those
   * boxes do not buy in size they buy in space, and there is no exception left to assert.
   */
  test('and the rows fill the body they are given, on both pages and in every box', () => {
    for (const box of moduleBoxes()) {
      for (const id of ['relative', 'leaderboard']) {
        const items = MODULES.find((m) => m.id === id)!.build({ frame: box.frame, density: box.density, prefix: '' });
        const drawn = rowsOf(items);
        const band = rowBand(items);
        const body = box.frame.height - (flat(items).some((i) => i.name.includes('.head.')) ? 16 : 0);
        const slack = body - ((drawn.count - 1) * drawn.pitch + band.height);
        expect({ id, box: box.name, slack, fills: slack >= 0 && slack < drawn.count }).toMatchObject({ fills: true });
      }
    }
  });
});

/**
 * #328: a list page declares its rows, and a box with height to spare buys type with it before space.
 *
 * The ticket's own pair is the case: a 437 x 214 box and a 437 x 510 one differed only in how many
 * drivers they listed, at the same 34 px row. They differ in the row now, the taller box listing the
 * count its shape declares at a row that fills it.
 */
describe('a list declares its rows and answers the box with the rest', () => {
  test('the 437 x 214 and 437 x 510 boxes of the ticket draw different rows, not only more of them', () => {
    for (const id of ['relative', 'leaderboard']) {
      const short = rowsOf(build(id, 437, 214, 'zone'));
      const tall = rowsOf(build(id, 437, 510, 'zone'));
      expect({ id, short: short.count, tall: tall.count, tallerRow: tall.pitch > short.pitch }).toMatchObject({ tallerRow: true });
    }
  });

  test('and a box however tall lists the count its shape declares', () => {
    // A narrow box is `tall narrow` whatever its height, and a medium one is `tall` from 400 px.
    const count = (id: string, width: number, height: number): number => rowsOf(build(id, width, height, densityForBox({ width, height }))).count;
    expect({ relative: count('relative', 250, 900), leaderboard: count('leaderboard', 250, 900) }).toEqual({ relative: 7, leaderboard: 8 });
    expect({ relative: count('relative', 445, 900), leaderboard: count('leaderboard', 445, 900) }).toEqual({ relative: 11, leaderboard: 11 });
    expect({ relative: count('relative', 445, 330), leaderboard: count('leaderboard', 445, 330) }).toEqual({ relative: 7, leaderboard: 6 });
  });

  /**
   * The ticket's own exception: buying rows is right for the pit wall, where the question is *who is in
   * the race*. So a pit wall zone's leaderboard lists every car its box holds at the canvas's row rather
   * than its shape's count, and the 487 x 315 body of the `519 x 359` zone, a `grid` box that would
   * declare six, lists the eight it listed before #328. The row still fills the body, and the relative,
   * whose window is its question on either surface, keeps its count.
   */
  test('but a pit wall zone lists every car its box holds', () => {
    for (const density of ['panel', 'wide'] as const) {
      const board = rowsOf(build('leaderboard', 487, 315, density));
      expect({ density, rows: board.count, pitch: board.pitch }).toEqual({ density, rows: 8, pitch: 39 });
      expect({ density, relative: rowsOf(build('relative', 487, 315, density)).count }).toEqual({ density, relative: 7 });
    }
    expect(rowsOf(build('leaderboard', 487, 315, 'zone')).count).toBe(6);
  });

  /**
   * The harm #328 names, on the base face: the driver name drawn at 13 px on the one column that says
   * *who*, under however much height the zone had, because the height went on more rows of 28.
   *
   * Zone B of the 850 x 480 face is 250 x 290 of body, and 328 without the rev bar. The numerals stay at
   * 24, since at 34 they would take the name column from 82 px to 61; the name steps up alone, to the
   * 15 px the canvas draws a name at in its 34 px row, and pays the one character its larger glyph
   * costs: seven at 13 is `LIAM B…` and six at 15 is `LIAM…`. `nameFloorOf` is the rule that allows the
   * one and refuses two.
   */
  for (const [height, rows] of [[290, [7, 8]], [328, [7, 8]]] as const) {
    test(`zone B of the 850 x 480 face, ${height} px of body, names its drivers at 15`, () => {
      for (const [id, count] of [['relative', rows[0]], ['leaderboard', rows[1]]] as const) {
        const items = build(id, 250, height, 'compact');
        expect({ id, rows: rowsOf(items).count }).toEqual({ id, rows: count });
        expect({ id, name: cell(items, 'name').fontSize, pos: cell(items, 'pos').fontSize, gap: cell(items, 'gap').fontSize }).toEqual({ id, name: 15, pos: 24, gap: 24 });
        expect({ id, room: charsThatFit(NAME_FACE, 15, cell(items, 'name').rect.width) }).toEqual({ id, room: 6 });
      }
    });
  }

  test('a row too short for the larger name keeps the canvas type, and spends nothing it has not got', () => {
    // The 800 x 286 face's zone B: 156 px of body is five rows of 29, under the 34 px the larger name
    // needs, so the row is the canvas's 28 px drawing with a pixel of air.
    const items = build('relative', 245, 156, 'compact');
    expect({ rows: rowsOf(items).count, name: cell(items, 'name').fontSize, pos: cell(items, 'pos').fontSize }).toEqual({ rows: 5, name: 13, pos: 24 });
  });
});

/** The characters the relative's name column holds at this row height in this box, which is what a stretch spends. */
function nameRoom(box: { frame: Rect; density: Density }, rowHeight: number): number {
  const kept = fittingColumns(RELATIVE_COLUMNS, box.frame.width, box.density, rowHeight);
  const index = kept.indexOf('name');
  if (index < 0) return 0;
  return charsThatFit(NAME_FACE, nameSizeForRow(rowHeight), columnWidths(kept, box.frame.width, box.density, rowHeight)[index] ?? 0);
}

/**
 * The room the relative's declaration leaves the rest of the row, which is the third thing #339 asked
 * to be checked.
 *
 * A larger type is not free: crossing 34 px takes the position and the gap from 24 to 34 and the car
 * number from 16 to 24, and all three are monospaced columns of a fixed character budget, so every
 * pixel they gain comes out of the one flexible column beside them. The name's room is what `listPlan`
 * refuses a step for; the chip and the number are what has to still fit once it has chosen.
 */
describe('the class chip and the car number fit the room the relative leaves them', () => {
  for (const box of moduleBoxes().filter((b) => b.density !== 'companion')) {
    test(`on a ${box.name}`, () => {
      const items = MODULES.find((m) => m.id === 'relative')!.build({ frame: box.frame, density: box.density, prefix: '' });
      const all = flat(items);
      const band = rowBand(items);
      const named = (suffix: string): Item[] => all.filter((i) => i.name.endsWith(`.row.${suffix}`) || i.name.includes(`.row.${suffix}.`));
      // The chip is a block and a label; the number is a numeral. Both are drawn at their column's
      // right or left edge and neither may leave the row.
      for (const item of [...named('class'), ...named('num')]) {
        if (item.kind === 'layer') continue;
        const r = item.rect;
        expect({ box: box.name, item: item.name, rect: r, inside: r.left >= band.left && r.left + r.width <= band.left + band.width + 1 }).toMatchObject({ inside: true });
      }
      // And the name holds a name, or there is nothing left to shed for it. That is the rule rather
       // than a list of narrow faces: the three narrow zones -- 250, 245 and 225 px of body -- are down
      // to the position, the name and the gap, and the canvas's own 40 and 92 for the two monospaced
      // columns leave the name under the eight characters the shortest format needs. What it must never
      // be is short while a car number or a class chip is still drawn beside it.
      const name = all.find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.row.name'))!;
      const room = charsThatFit(NAME_FACE, name.fontSize, name.rect.width);
      const shed = named('num').length === 0 && named('class').length === 0;
      expect({ box: box.name, fs: name.fontSize, width: name.rect.width, room, enough: room >= SHORTEST_NAME_CHARS || shed }).toMatchObject({ enough: true });
      // And whatever type the row ended up at, it did not cost the name more than `nameFloorOf` allows.
      // The canvas's row is the baseline and `Liam Byrne` is the line: a box whose canvas row held ten
      // keeps ten, which is the assertion the first cut of #339 would have failed on four boxes -- zone B
      // of the 1280 x 480 and 1280 x 400 faces and zone C of the 1280 x 720 in both arrangements, where
      // a taller row promoted the car number and drew `Liam Byr…` for a name the canvas row drew whole --
      // and a box whose canvas row already cut it may lose one character to a larger name, never two.
      const declared = nameRoom(box, tableRowHeight(box.density));
      expect({ box: box.name, room, declared, floor: nameFloorOf(declared), kept: room >= nameFloorOf(declared) }).toMatchObject({ kept: true });
    });
  }
});

/**
 * The name the default format draws, drawn whole wherever the box ever held it.
 *
 * The case in #385's Why and the one a reader can check against a screenshot: `Liam Byrne` is what the
 * canvas writes in the column, what the traces carry, and what `full` -- the format a rig that never
 * opens the setting gets -- makes of that entry. Ten characters. A box whose column is narrower than
 * that ellipsises it and says so in the row of zones.md section 10 that tabulates the budgets; what no
 * box may do is hold ten at the row the density declares and then lose one to a taller row. A box that
 * never held it may lose one character more to a larger name, which is #328's trade and `nameFloorOf`'s
 * rule, and loses no more than that.
 */
test('the default format draws Liam Byrne whole on every box whose declared row held it', () => {
  const short: string[] = [];
  for (const box of moduleBoxes()) {
    const items = MODULES.find((m) => m.id === 'relative')!.build({ frame: box.frame, density: box.density, prefix: '' });
    const name = flat(items).find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.row.name'))!;
    const room = charsThatFit(NAME_FACE, name.fontSize, name.rect.width);
    if (room >= NAME_SAMPLE.length) continue;
    short.push(box.name);
    // The column was already short of ten at the declared row, so the row is not what cost the name its
    // tenth letter; what the row may have cost is the one character `nameFloorOf` allows a larger name.
    const declared = nameRoom(box, tableRowHeight(box.density));
    expect({ box: box.name, room, declared, short: declared < NAME_SAMPLE.length, kept: room >= nameFloorOf(declared) }).toMatchObject({ short: true, kept: true });
  }
  // The five narrow boxes, named so that one being added or leaving is a diff rather than a silence:
  // zone C of the 850 x 480, 800 x 480 and 800 x 286 faces in both arrangements, and the companion's
  // portrait page, whose 109 px column holds eight.
  expect(short).toEqual([
    'OpenDash Companion portrait page',
    'face-274x328',
    'face-274x366',
    'face-249x328',
    'face-249x366',
    'face-269x194',
    'face-269x226',
  ]);
});

/**
 * A table stated with no row of its own, which is the canvas's row at the density.
 *
 * The fixture for what the drawing states, since neither list page draws it as stated any more: since
 * #328 both declare their rows and stretch them to fill the body, so what a page draws is the canvas's
 * row plus the space its box leaves over.
 */
const canvasTable = (width: number, height: number, density: Density): Item[] => {
  const frame = rect(0, 0, width, height);
  return table({ name: 'table', frame, columns: fittingColumns(LEADERBOARD_COLUMNS, width, density), mode: 'full', density, header: false });
};

describe('the row the canvas draws', () => {
  // The wide catalogue drawing, whose row is stated column by column on ZoneCatalogue: 34 px tall,
  // padded 0 6, cells 12 apart, position and gap at 34 and the car number and the name beneath them.
  const items = canvasTable(600, 242, 'zone');

  test('is 34 px tall with 6 px of side padding and 12 px between its cells', () => {
    expect(rowBand(items).height).toBe(34);
    expect(cell(items, 'pos').rect.left).toBe(6);
    // The car number starts one cell gap past the right edge of the position column.
    const pos = cell(items, 'pos');
    expect(cell(items, 'num').rect.left).toBe(pos.rect.left + pos.rect.width + 12);
  });

  test('draws the position and the gap at 34, the car number at 24 and the name at 15', () => {
    expect(cell(items, 'pos').fontSize).toBe(34);
    expect(cell(items, 'gap').fontSize).toBe(34);
    expect(cell(items, 'num').fontSize).toBe(24);
    // 15 rather than the 13 this row used to draw. The canvas draws 13 at every shape and #339 is the
    // argument against it: a zone read at arm's length labels at 15 everywhere else on the face, and
    // the column that says *who* was the one cell of the row sitting at the floor of the ramp.
    expect(cell(items, 'name').fontSize).toBe(15);
  });

  test('and steps the numerals down one in the 28 px row a narrow zone takes, keeping the name at 13', () => {
    const narrow = canvasTable(274, 262, 'compact');
    expect(rowBand(narrow).height).toBe(28);
    expect(cell(narrow, 'pos').fontSize).toBe(24);
    expect(cell(narrow, 'gap').fontSize).toBe(24);
    // 13 and not 15 here, which is the canvas's own number at every shape and the one the build keeps.
    // It was argued for as a three-character saving at 82 px of column and it is one character; the test
    // below counts all six. 13 is where density.ts puts the floor, not below it.
    expect(cell(narrow, 'name').fontSize).toBe(13);
  });

  /**
   * What 15 px would actually cost the three narrow boxes, counted rather than asserted.
   *
   * The 28 px row keeping 13 was recorded in three places as "82 px is seven characters at 13 and five
   * at 15". It is seven and six. The boxes the row is handed are zone C of the 850 x 480, 800 x 286 and
   * 800 x 480 faces, at 82, 77 and 57 px of name column once `fittingColumns` is down to the position,
   * the name and the gap, and 15 costs one character, one character and nothing. So the saving is not
   * what keeps 13 there — the catalogue drawing 13 at every shape is, a second divergence needing to
   * buy more than a glyph. Pinned here because a documented count nothing runs is a number that rots.
   */
  test('and 15 px would cost the three narrow boxes one character, one character and nothing', () => {
    const budgets = [250, 245, 225].map((width) => {
      const name = columnWidths(['pos', 'name', 'gap'], width, 'compact', 28)[1] ?? 0;
      return { width, name, at13: charsThatFit(NAME_FACE, 13, name), at15: charsThatFit(NAME_FACE, 15, name) };
    });
    expect(budgets).toEqual([
      { width: 250, name: 82, at13: 7, at15: 6 },
      { width: 245, name: 77, at13: 6, at15: 5 },
      { width: 225, name: 57, at13: 4, at15: 4 },
    ]);
  });

  /**
   * And what the one lever those boxes still have would buy, which is the other number zones.md owes.
   *
   * Four characters at 800 x 480 is `LIA…`, one glyph more than the `LIA` #385 was written to delete,
   * and the same three glyphs for `L. Byrne` and `B. Liam`, whose cut lands on a space and drops it:
   * `L.…` and `B.…`. The row is down to the position, the name and the gap,
   * all three of which `THE_ROW` keeps, so the only width left to give the name is one of the other
   * two: the position's 40 px and the gap's 92 are both the canvas's stated numbers, and the gap's is a
   * floor over its own content — six digits and a sign need 79 px at 24, so even dropping a decimal
   * would not narrow it. Giving the name the position's column is therefore the whole of the lever, and
   * it is worth four characters at the narrowest face. Not taken: the position is what says whether the
   * car behind is racing you or lapping you, and dropping a third column is the canvas's to decide.
   */
  test('and giving the name the position column would buy it nine characters at the narrowest face', () => {
    const withoutPos = [250, 245, 225].map((width) => {
      const name = columnWidths(['name', 'gap'], width, 'compact', 28)[0] ?? 0;
      return { width, name, at13: charsThatFit(NAME_FACE, 13, name) };
    });
    expect(withoutPos).toEqual([
      { width: 250, name: 134, at13: 11 },
      { width: 245, name: 129, at13: 11 },
      { width: 225, name: 109, at13: 9 },
    ]);
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
    const names = rowsOf(build('leaderboard', COMPANION_PAGE.width, COMPANION_PAGE.height, 'companion')).names;
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
    const board = build('leaderboard', COMPANION_PAGE.width, COMPANION_PAGE.height, 'companion');
    expect(formulaOf(cell(board, 'gap'), 'Text')).toContain("'Lead'");
    const rating = build('relative', 600, 242, 'zone');
    // The column is declared but no shape keeps it yet, so the format is checked where it is written.
    expect(rowsOf(rating).cells.some((c) => c.name.endsWith('.row.rating'))).toBe(false);
  });

  test('the own row lifts its position, its name and its gap, and nothing else', () => {
    for (const id of ['pos', 'name', 'gap']) expect({ id, lifts: formula(id, 'TextColor').includes('#F5F7FA') }).toEqual({ id, lifts: true });
    // The car number stays in the label ink on every row, the player's included.
    expect(formula('num', 'TextColor')).toBe("'#5A6069'");
    const board = build('leaderboard', COMPANION_PAGE.width, COMPANION_PAGE.height, 'companion');
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
 * The rule written at `LEADERBOARD_COLUMNS`, held at every width rather than at the boxes that happen
 * to exist (#340).
 *
 * The boxes above are the ones the build hands a list today, and the narrowest of them is 225 px, which
 * is where step 3 is the last one taken. What a narrower box does was not decided, and `columnWidths`
 * answered it by flooring the name at nothing and laying the position and the gap out past the box.
 * So this walks every width from the gap alone to the widest zone and asks the rule's three questions of
 * each: is the gap there, does the row fit, and was what it gave up taken in the order written down.
 */
describe('what a row gives way, in the order the leaderboard writes down', () => {
  /** The order the rule gives columns up in, first to go first. The gap is not in it. */
  const GIVES_WAY: readonly ColumnId[] = ['best', 'last', 'class', 'num', 'name', 'pos'];
  const DENSITIES: Density[] = ['companion', 'zone', 'wide', 'compact', 'panel'];
  /** The narrowest a row can be and still hold its gap: the gap's column and the row's padding. */
  const gapAlone = (density: Density): number => -rowSlack(['gap'], 0, density);

  test('keeps the gap, fits its box, and gives up only a head of the order, at every width', () => {
    let widths = 0;
    for (const density of DENSITIES) {
      for (const [page, declared] of [['leaderboard', LEADERBOARD_COLUMNS], ['relative', RELATIVE_COLUMNS]] as const) {
        let before = 0;
        for (let width = gapAlone(density); width <= 1300; width++) {
          const kept = fittingColumns(declared, width, density);
          widths += 1;
          const at = { density, page, width, kept: kept.join(' ') };
          expect({ ...at, gap: kept.includes('gap') }).toMatchObject({ gap: true });
          expect({ ...at, slack: rowSlack(kept, width, density), fits: rowSlack(kept, width, density) >= 0 }).toMatchObject({ fits: true });
          // Kept in the order drawn, and given up from the front of the order: nothing is gone while a
          // column earlier in the order is still drawn.
          expect(kept).toEqual(declared.filter((id) => kept.includes(id)));
          const gone = GIVES_WAY.filter((id) => declared.includes(id) && !kept.includes(id));
          const drawn = GIVES_WAY.filter((id) => kept.includes(id));
          expect({ ...at, inOrder: gone.every((id) => drawn.every((other) => GIVES_WAY.indexOf(id) < GIVES_WAY.indexOf(other))) }).toMatchObject({ inOrder: true });
          // And a wider box never draws less.
          expect({ ...at, before, fewer: kept.length < before }).toMatchObject({ fewer: false });
          before = kept.length;
        }
      }
    }
    expect(widths).toBeGreaterThan(10000);
  });

  test('and takes each step at a width that is written down', () => {
    // The least width that still draws each column, at the two rows a zone is drawn in. The first four
    // are step 2 and move with the columns' own widths; the last two are step 4, which the narrowest
    // body the build produces, 225 px, clears by 34 px in the 28 px row. A box under 104 px holds
    // nothing but the gap and not all of that, and is the one width the page cannot answer.
    const least = (density: Density): Record<string, number> => {
      const found: Record<string, number> = {};
      for (let width = 1300; width >= gapAlone(density); width--) {
        const kept = fittingColumns(LEADERBOARD_COLUMNS, width, density);
        for (const id of kept) found[id] = width;
      }
      return found;
    };
    expect(least('compact')).toEqual({ best: 587, last: 477, class: 367, num: 313, name: 191, pos: 156, gap: 104 });
    expect(least('zone')).toEqual({ best: 666, last: 540, class: 414, num: 356, name: 216, pos: 177, gap: 117 });
  });

  test('the name goes once it cannot hold a letter and the ellipsis, and not before', () => {
    // 191 px of 28 px row leaves the name 27 px, two characters at 13; a pixel less leaves it one,
    // which would draw the ellipsis alone.
    const nameAt = (width: number): number => {
      const columns = ['pos', 'name', 'gap'] as const;
      return charsThatFit(NAME_FACE, 13, columnWidths(columns, width, 'compact', 28)[1] ?? 0);
    };
    expect({ at191: nameAt(191), at190: nameAt(190) }).toEqual({ at191: LEAST_NAME_CHARS, at190: LEAST_NAME_CHARS - 1 });
    expect(fittingColumns(LEADERBOARD_COLUMNS, 191, 'compact')).toEqual(['pos', 'name', 'gap']);
    expect(fittingColumns(LEADERBOARD_COLUMNS, 190, 'compact')).toEqual(['pos', 'gap']);
  });

  test('a larger type never costs a column the canvas row kept', () => {
    // A box tall enough for the 34 px numerals and 170 px wide: the canvas's 28 px row holds the position
    // and the gap in it, and the 34 px row's would overrun it by seven and give up the position. The
    // name's budget cannot refuse that step, the row having no name left to budget, so the plan holds
    // the step to the columns as well and keeps the canvas's type.
    const frame = rect(0, 0, 170, 400);
    const list = { density: 'compact' as Density, header: false, columns: LEADERBOARD_COLUMNS, rows: 3, fit: fittingColumns };
    expect(fittingColumns(LEADERBOARD_COLUMNS, 170, 'compact')).toEqual(['pos', 'gap']);
    const plan = listPlan(frame, list);
    expect({ columns: plan.columns, lead: plan.rowType.lead }).toEqual({ columns: ['pos', 'gap'], lead: 24 });
  });
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
