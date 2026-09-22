/**
 * The comparison is complete and honest by construction: every row has a cell for every product,
 * a coming feature names its issue, a refusal has its reason, openDash's own column is never
 * "not checked", and the competitor columns say when they were read.
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

  test('a refusal has a reason', () => {
    for (const row of ROWS) {
      const c = row.cells.opendash;
      if (c.mark === 'notBuilt') expect(c.text.length).toBeGreaterThan(10);
    }
  });

  test('openDash never marks itself not checked', () => {
    expect(ROWS.filter((r) => r.cells.opendash.mark === 'unchecked').map((r) => r.id)).toEqual([]);
  });

  test('the competitor columns are dated and openDash is not', () => {
    for (const p of PRODUCTS) {
      if (p.id === 'opendash') expect(p.asOf).toBeUndefined();
      else expect(p.asOf).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    }
  });

  test('the price row makes the promise', () => {
    expect(ROWS.find((r) => r.id === 'price')?.cells.opendash.text).toContain(FREE_FOREVER);
  });

  test('row ids are unique', () => {
    expect(new Set(ROWS.map((r) => r.id)).size).toBe(ROWS.length);
  });
});
