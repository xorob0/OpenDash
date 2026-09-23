/**
 * The comparison is complete and honest by construction: every row has a cell for every product,
 * a coming feature names its issue, a refusal has its reason, and no competitor cell is left
 * blank. A cell we could not stand behind is cut from the row, never hedged.
 */
import { describe, expect, test } from 'bun:test';
import { PRODUCTS, ROWS } from '../lib/compare.ts';
import { FREE_FOREVER } from '../lib/site.ts';

describe('the comparison', () => {
  test('every row has a cell for every product', () => {
    for (const row of ROWS) for (const p of PRODUCTS) expect({ row: row.id, product: p.id, has: row.cells[p.id] !== undefined }).toEqual({ row: row.id, product: p.id, has: true });
  });

  test('a coming feature names the issue it is scheduled under', () => {
    for (const row of ROWS) {
      const c = row.cells.opendash;
      if (c.mark === 'soon') expect({ row: row.id, issues: c.issues?.length ?? 0 }).not.toEqual({ row: row.id, issues: 0 });
      for (const n of c.issues ?? []) expect(Number.isInteger(n) && n > 0).toBe(true);
    }
  });

  test('a refusal has a reason, and a word that is not the mark repeated', () => {
    for (const row of ROWS) {
      const c = row.cells.opendash;
      if (c.mark === 'notBuilt') expect(c.text.length).toBeGreaterThan(10);
      expect({ row: row.id, repeats: c.text.trim().toLowerCase() === c.word.toLowerCase() + '.' }).toEqual({ row: row.id, repeats: false });
    }
  });

  test('every competitor cell says something', () => {
    const blank = [];
    for (const row of ROWS) for (const p of PRODUCTS) if (p.id !== 'opendash' && row.cells[p.id].text.trim().length < 10) blank.push(`${row.id}/${p.id}`);
    expect(blank).toEqual([]);
  });

  test('every cell carries a word', () => {
    for (const row of ROWS) for (const p of PRODUCTS) expect({ cell: `${row.id}/${p.id}`, word: row.cells[p.id].word.length > 0 }).toEqual({ cell: `${row.id}/${p.id}`, word: true });
  });

  test('the price row makes the promise', () => {
    expect(ROWS.find((r) => r.id === 'price')?.cells.opendash.text).toContain(FREE_FOREVER);
  });

  test('row ids are unique', () => {
    expect(new Set(ROWS.map((r) => r.id)).size).toBe(ROWS.length);
  });
});
