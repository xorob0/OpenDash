/**
 * Module 14, Leaderboard: the order of the race, overall or in the player's class. The row set fits
 * the box, so the same module is seven rows on a companion page and four in a short zone.
 *
 * Two different settings say "class" here and they are not the same question. `PositionMode` is
 * which number a position column shows; a zone's `classOnly` is who is in the list at all. A zone
 * set to one class and counting overall positions is a legitimate thing to want on a multi-class
 * grid, which is why neither implies the other.
 *
 * Columns drop as the box narrows, in the order `shedding.ts` declares: the lap times go first,
 * then the chip and the number. The gap outlives both, because a leaderboard without a gap is a
 * list of names.
 */
import { columnWidths, table, tableRowHeight, type ColumnId } from '../second/table.ts';
import { defineModule, pageColumns } from './module.ts';
import type { Density } from '../second/density.ts';

/** Every column this page has, in drawing order. Which of them a shape keeps is `shedding.ts`. */
export const LEADERBOARD_COLUMNS: readonly ColumnId[] = ['pos', 'num', 'name', 'class', 'gap', 'best', 'last'];

/**
 * Whether a table draws its header row.
 *
 * The companion shows one page at a time with room for a legend; a zone of a face has a title
 * already, and every list on the catalogue and the face artboards is drawn without a header. It is
 * the density that says which of the two a module is on.
 */
export const drawsHeader = (density: Density): boolean => density === 'companion';

/**
 * The name column is the one that flexes, and the gap column is the one the page exists for.
 *
 * `fittingColumns` used to pop from the end until a full name fitted, and with the gap drawn last
 * that is the column that paid: at 229 x 292 the leaderboard lost the gap and became a list of
 * names, which is readability-pass.md §11. A page keeps its position and its gap whatever else it
 * has to give up; the name that no longer fits becomes the three-letter code `table.ts` draws when
 * its column is too narrow for a name, which costs a column nothing.
 */
const NEVER_DROPPED: readonly ColumnId[] = ['pos', 'gap'];

/** The columns that fit `width`: drops the last droppable one until the row is no wider than its box. */
export function fittingColumns(columns: readonly ColumnId[], width: number, density: Density, rowHeight?: number): ColumnId[] {
  const kept = [...columns];
  const h = rowHeight ?? tableRowHeight(density);
  // A row fits when every fixed column plus a column wide enough for a driver code fits.
  const minimumName = 60;
  for (;;) {
    const widths = columnWidths(kept, width, density, h);
    const nameIndex = kept.indexOf('name');
    const name = nameIndex < 0 ? minimumName : (widths[nameIndex] ?? 0);
    if (name >= minimumName) break;
    const droppable = kept.map((id, i) => ({ id, i })).filter(({ id }) => !NEVER_DROPPED.includes(id));
    const last = droppable[droppable.length - 1];
    if (!last) break;
    kept.splice(last.i, 1);
  }
  return kept;
}

export const leaderboard = defineModule('leaderboard', (ctx) => {
  const header = drawsHeader(ctx.density);
  const rowHeight = tableRowHeight(ctx.density);
  return table({
    name: `${ctx.prefix}table`,
    frame: ctx.frame,
    columns: fittingColumns(pageColumns(LEADERBOARD_COLUMNS, ctx), ctx.frame.width, ctx.density, rowHeight),
    mode: 'full',
    density: ctx.density,
    rowHeight,
    header,
    classOnly: ctx.classOnly,
  });
});
