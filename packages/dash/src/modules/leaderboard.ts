/**
 * Module 14, Leaderboard: the order of the race, overall or in the player's class per the plugin's
 * PositionMode. The row set fits the box, so the same module is seven rows on a companion page and
 * four in a short zone.
 *
 * Columns drop as the box narrows, in the order `shedding.ts` declares: the lap times go first,
 * then the chip and the number. The gap outlives both, because a leaderboard without a gap is a
 * list of names.
 */
import { measureText } from '../design/advances.ts';
import { densityOf } from '../second/density.ts';
import { columnWidths, table, type ColumnId } from '../second/table.ts';
import { defineModule, pageColumns } from './module.ts';
import type { Density } from '../second/density.ts';

/** Every column this page has, in drawing order. Which of them a shape keeps is `shedding.ts`. */
export const LEADERBOARD_COLUMNS: readonly ColumnId[] = ['pos', 'num', 'name', 'class', 'gap', 'best', 'last'];

/**
 * The name a driver column is measured for: a full first and last name at the density's own size.
 *
 * This used to be four characters' worth, which is a column that fits "Toma". WPF clips in silence,
 * so what that produced was not a narrow column but a cut name: at 800 x 480 zone C drew "Tomasz
 * Kowalcz" with the class chip hard against it. A column that cannot hold a name should cost the
 * row its next column instead, which is what this measurement makes it do.
 *
 * It is a bound and not a guarantee. A name of unusually wide letters is half again as wide as this
 * one at the same length, and nothing here truncates; XOR-121 owns what a name does when it is
 * longer than any column will ever be.
 */
const NAME_TO_FIT = 'Tomasz Kowalczyk';

/** The columns that fit `width`: drops from the end until the row is no wider than its box. */
export function fittingColumns(columns: readonly ColumnId[], width: number, density: Density): ColumnId[] {
  const kept = [...columns];
  // The name column flexes, so a row fits when every fixed column plus a readable name fits.
  const minimumName = Math.ceil(measureText('BarlowMedium', NAME_TO_FIT, densityOf(density).name));
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
    columns: fittingColumns(pageColumns(LEADERBOARD_COLUMNS, ctx), ctx.frame.width, ctx.density),
    mode: 'full',
    density: ctx.density,
  }),
);
