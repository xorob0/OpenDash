/**
 * Module 14, Leaderboard: the order of the race, overall or in the player's class per the plugin's
 * PositionMode. The row set fits the box, so the same module is seven rows on a companion page and
 * four in a short zone.
 *
 * Columns drop as the box narrows, longest-tail first: the class chip and the last lap go before
 * the gap does, because a leaderboard without a gap is a list of names.
 */
import { densityOf } from '../second/density.ts';
import { columnWidths, table, type ColumnId } from '../second/table.ts';
import { defineModule } from './module.ts';
import type { Density } from '../second/density.ts';

/** Columns in importance order; the table keeps the longest prefix that fits. */
export const LEADERBOARD_COLUMNS: readonly ColumnId[] = ['pos', 'num', 'name', 'class', 'gap', 'best', 'last'];

/** The columns that fit `width`: drops from the end until the row is no wider than its box. */
export function fittingColumns(columns: readonly ColumnId[], width: number, density: Density): ColumnId[] {
  const kept = [...columns];
  // The name column flexes, so a row fits when every fixed column plus a readable name fits.
  const minimumName = densityOf(density).name * 4;
  while (kept.length > 2) {
    const widths = columnWidths(kept, width, density);
    const nameIndex = kept.indexOf('name');
    const name = nameIndex < 0 ? minimumName : (widths[nameIndex] ?? 0);
    if (name >= minimumName) break;
    kept.pop();
  }
  return kept;
}

export const leaderboard = defineModule('leaderboard', (ctx) =>
  table({
    name: `${ctx.prefix}table`,
    frame: ctx.frame,
    columns: fittingColumns(LEADERBOARD_COLUMNS, ctx.frame.width, ctx.density),
    mode: 'full',
    density: ctx.density,
  }),
);
