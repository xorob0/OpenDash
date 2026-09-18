/**
 * The split list: the board that keeps the top of the field and lets the rest of its rows follow
 * the player across a limit line.
 *
 * Two things are checked here that nothing else can check. The row indices are arithmetic, so they
 * are evaluated rather than matched: the canvas's own example is a player at P16 with six rows kept
 * and seven cars behind the line, and a formula that agrees with that sentence is one a reader can
 * trust. And the limit line has to sit outside both repeated layers, because SimHub evaluates a
 * repeated layer's own Visible once and a band inside the repeat would be drawn on every row or on
 * none.
 */
import { describe, expect, test } from 'bun:test';
import { measureText } from '../src/design/advances.ts';
import { rect } from '../src/design/geometry.ts';
import { table, type ColumnId, type TableSpec } from '../src/second/table.ts';
import { opponentCount } from '../src/second/values.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import type { Item, LayerItem, RectangleItem, TextItem } from '../src/generator.ts';

const COLUMNS: readonly ColumnId[] = ['pos', 'num', 'name', 'gap', 'last'];

/** The band's height and the clearance either side of its label, as the canvas draws them. */
const BAND = 20;
const CLEARANCE = 12;

const ROW_HEIGHT = 34;
const HEADER = 32;
const ROWS = 11;
const KEPT = 6;
/** The rows left for the window, and where in it the player sits. */
const WINDOW = ROWS - KEPT;
const CENTRE = Math.ceil(WINDOW / 2);

/** A board whose body holds exactly eleven rows and the limit line between them. */
const FRAME = rect(0, 100, 1280, HEADER + ROWS * ROW_HEIGHT + BAND);

const board = (split?: number): TableSpec => ({
  name: 'test.board',
  frame: FRAME,
  columns: COLUMNS,
  mode: 'full',
  density: 'zone',
  rowHeight: ROW_HEIGHT,
  board: true,
  ...(split === undefined ? {} : { split }),
});

const items = (split?: number): Item[] => [...walkItems(table(board(split)))];
const named = (list: Item[], suffix: string): Item | undefined => list.find((i) => i.name.endsWith(suffix));
const layer = (list: Item[], name: string): LayerItem => list.find((i): i is LayerItem => i.kind === 'layer' && i.name === name)!;

/** A field of twenty-four cars, which is the grid the canvas draws its own example against. */
const FIELD = 24;

/**
 * An NCalc expression of the split, evaluated for a player at `position` in a field of `field` cars,
 * on row `k` of a repeated layer. Nothing in the repository evaluates a binding, so what is
 * evaluated here is only what the split emits: two functions over three readings.
 */
const evaluate = (expression: string, position: number, { k = 1, field = FIELD }: { k?: number; field?: number } = {}): number => {
  const js = expression
    .replaceAll(opponentCount(), String(field))
    .replaceAll('getplayerleaderboardposition()', String(position))
    .replaceAll('repeatindex()', String(k))
    .replaceAll('max(', 'Math.max(')
    .replaceAll('min(', 'Math.min(');
  // Whatever is left once the two calls are taken out has to be arithmetic, or the split is emitting
  // something this is quietly reading as a number.
  if (!/^[\d\s().,+\-*/]+$/.test(js.replaceAll('Math.max', '').replaceAll('Math.min', ''))) throw new Error(`the expression holds something this cannot evaluate: ${js}`);
  return Number(new Function(`return ${js};`)());
};

const formula = (item: Item, target: 'Text' | 'Visible'): string => {
  const b = item.bindings?.[target];
  if (!b || typeof b.formula !== 'string') throw new Error(`${item.name} has no ${target} formula`);
  return b.formula;
};

/** The leaderboard index the row template of a block addresses, read off its own Visible test. */
const rowIndexOf = (list: Item[], block: string): string => {
  const visible = formula(layer(list, `test.board.${block}`), 'Visible');
  const inner = visible.match(/driveravailable\((.*)\)\s*,/);
  if (!inner?.[1]) throw new Error(`no row index in ${visible}`);
  return inner[1];
};

/**
 * How many cars the limit line says are behind it, for a player at `position`.
 *
 * Read off the Visible test rather than off the label's Text, which is the same count with the copy
 * concatenated onto it: what shows the line and what it says are one expression or the line lies.
 */
const hiddenCount = (list: Item[], position: number, field = FIELD): number =>
  evaluate(formula(named(list, '.limit.count')!, 'Visible').replace(/ > \(0\)$/, ''), position, { field });

describe('a table splits only when it is asked to', () => {
  test('no split: one repeated layer and no limit line, which is every list the packages draw', () => {
    const list = items();
    expect(list.filter((i) => i.name.includes('.limit.'))).toEqual([]);
    expect(list.filter((i): i is LayerItem => i.kind === 'layer' && i.name.endsWith('Rows'))).toEqual([]);
    expect(layer(list, 'test.board.rows').repetitions).toBe(ROWS - 1);
  });

  test('a split list is the overall leaderboard, so it refuses a class filter', () => {
    expect(() => table({ ...board(KEPT), mode: 'class' })).toThrow(/overall leaderboard/);
    expect(() => table({ ...board(KEPT), classOnly: '[Setting.ClassOnly]' })).toThrow(/overall leaderboard/);
  });
});

describe('the split keeps the top of the field and follows the player', () => {
  const list = items(KEPT);
  const kept = layer(list, 'test.board.rows');
  const window = layer(list, 'test.board.splitRows');

  test('six rows above the line, the rest below it', () => {
    expect(kept.repetitions).toBe(KEPT - 1);
    expect(window.repetitions).toBe(WINDOW - 1);
  });

  test('the rows above the line are the leaderboard from the top, whatever the player does', () => {
    expect(rowIndexOf(list, 'row')).toBe('repeatindex()');
  });

  test('a player inside the budget splits nothing: the window carries on from the kept rows', () => {
    const idx = rowIndexOf(list, 'splitRow');
    // The last position that is still inside the budget: the window has opened under the kept rows
    // and the player has reached its centre row without pushing it down.
    for (const position of [-1, 1, 5, KEPT + CENTRE - 1]) {
      expect({ position, first: evaluate(idx, position) }).toEqual({ position, first: KEPT + 1 });
      expect({ position, hidden: hiddenCount(list, position) }).toEqual({ position, hidden: 0 });
    }
  });

  test('a player at P16 keeps six rows and reads seven cars not shown', () => {
    const idx = rowIndexOf(list, 'splitRow');
    // The canvas's own example: P1 to P6 above the line, P7 to P13 behind it, the window opening at
    // P14 with the player third in it.
    expect(evaluate(idx, 16)).toBe(14);
    expect(evaluate(idx, 16, { k: CENTRE })).toBe(16);
    expect(evaluate(idx, 16, { k: WINDOW })).toBe(18);
    expect(hiddenCount(list, 16)).toBe(7);
    expect(formula(named(list, '.limit.count')!, 'Text')).toContain("' CARS NOT SHOWN'");
  });

  test('a field the rows can all hold is never cut, wherever the player is in it', () => {
    const idx = rowIndexOf(list, 'splitRow');
    // Eleven rows hold eleven cars, so the player can be last of them and the list stays whole. A
    // field shorter than the six kept rows is the same answer twice over: nothing is hidden, and
    // both blocks draw only the rows a car answers for.
    for (const field of [4, ROWS]) {
      expect({ field, hidden: hiddenCount(list, field, field) }).toEqual({ field, hidden: 0 });
      expect({ field, first: evaluate(idx, field, { field }) }).toEqual({ field, first: KEPT + 1 });
    }
    for (const block of ['row', 'splitRow']) expect(formula(layer(list, `test.board.${block}`), 'Visible')).toContain('driveravailable');
  });

  test('the window stops at the last car rather than running off the end of the field', () => {
    const idx = rowIndexOf(list, 'splitRow');
    // A player last of twenty-four: the window ends on P24 and what it could not reach is behind the
    // line, rather than five rows of nothing under a count that is short of the truth.
    expect(evaluate(idx, FIELD, { k: WINDOW })).toBe(FIELD);
    expect(hiddenCount(list, FIELD)).toBe(FIELD - WINDOW - KEPT);
  });

  test('the line and its count come and go together', () => {
    const shown = formula(named(list, '.limit.count')!, 'Visible');
    for (const id of ['.limit.before', '.limit.after']) expect(formula(named(list, id)!, 'Visible')).toBe(shown);
    expect(hiddenCount(list, 17)).toBe(8);
  });
});

describe('the limit line is drawn where the canvas draws it', () => {
  const list = items(KEPT);
  const count = named(list, '.limit.count') as TextItem;
  const before = named(list, '.limit.before') as RectangleItem;
  const after = named(list, '.limit.after') as RectangleItem;
  const top = FRAME.top + HEADER + KEPT * ROW_HEIGHT;

  /** Where a block's row template is drawn, read off the background the own row is lifted with. */
  const blockTop = (name: string): number => (named([...walkItems(layer(list, name).children)], '.background') as RectangleItem).rect.top;

  test('the band stands between the two blocks, a row apart from neither', () => {
    expect(blockTop('test.board.row')).toBe(FRAME.top + HEADER);
    expect(blockTop('test.board.splitRow')).toBe(top + BAND);
  });

  test('two 1 px lines in text.primary, centred in a 20 px band', () => {
    for (const line of [before, after]) {
      expect({ name: line.name, height: line.rect.height, colour: line.backgroundColor }).toEqual({ name: line.name, height: 1, colour: ds.color.text.primary });
      expect(line.rect.top).toBe(top + Math.round((BAND - 1) / 2));
    }
    expect(before.rect.left).toBe(FRAME.left + 16);
    expect(after.rect.left + after.rect.width).toBe(FRAME.left + FRAME.width - 16);
  });

  test('the label is twelve pixels clear of both lines and centred in the row', () => {
    expect(count.rect.left - (before.rect.left + before.rect.width)).toBe(CLEARANCE);
    expect(after.rect.left - (count.rect.left + count.rect.width)).toBe(CLEARANCE);
    expect(count.rect.left + count.rect.width / 2).toBeCloseTo(FRAME.left + FRAME.width / 2, 0);
    expect(count.fontSize).toBe(13);
    expect(count.hAlign).toBe('center');
  });

  test('the bound count declares the widest text it can draw, and the box holds it', () => {
    expect(count.widest).toBeTruthy();
    expect(count.widest).toMatch(/CARS NOT SHOWN$/);
    // Every digit, since the count is a number and the widest is measured rather than guessed.
    for (const digit of '0123456789') {
      const drawn = measureText('BarlowMedium', `${digit}${digit} CARS NOT SHOWN`, count.fontSize);
      expect({ digit, drawn, box: count.rect.width, fits: drawn <= count.rect.width }).toMatchObject({ fits: true });
    }
  });

  test('the band sits outside both repeated layers, which SimHub would evaluate once', () => {
    expect(table(board(KEPT)).filter((i) => i.name.includes('.limit.'))).toHaveLength(3);
    for (const block of ['test.board.rows', 'test.board.splitRows']) {
      expect([...walkItems(layer(list, block).children)].filter((i) => i.name.includes('.limit.'))).toEqual([]);
    }
  });
});
