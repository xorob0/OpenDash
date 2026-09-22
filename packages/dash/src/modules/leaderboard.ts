/**
 * Module 14, Leaderboard: the order of the race, overall or in the player's class. The row set fits
 * the box, so the same module is seven rows on a companion page and four in a short zone.
 *
 * Two settings say "class" here and since #212 they meet, in one direction. `PositionMode` is the
 * rig's own and it filters the rows it numbers, a column of class positions drawn over the whole
 * field having numbered an order it did not sort. A zone's `classOnly` is that zone asking for the
 * filter on its own, which a rig counting overall still allows: one class listed by its overall
 * places is a legitimate thing to want on a multi-class grid. A list is therefore filtered when
 * either of the two says so, and `rowsInClass` in `second/values.ts` is where they are joined.
 *
 * Columns drop as the box narrows, in the order `shedding.ts` declares: the lap times go first,
 * then the chip and the number. The gap outlives both, because a leaderboard without a gap is a
 * list of names.
 */
import { columnWidths, table, tableRowHeight, type ColumnId } from '../second/table.ts';
import { defineModule, pageColumns } from './module.ts';
import type { Density } from '../second/density.ts';

/**
 * Every column this page has, in drawing order. Which of them a shape keeps is `shedding.ts`.
 *
 * The gap is drawn last, where the canvas draws it, after the two lap times. It used to sit before
 * them so that `fittingColumns` popping the tail would drop the times first; the order is the
 * canvas's now and the rule that the gap survives is written down in {@link NEVER_DROPPED} instead,
 * which is the honest place for it.
 */
export const LEADERBOARD_COLUMNS: readonly ColumnId[] = ['pos', 'num', 'name', 'class', 'last', 'best', 'gap'];

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
 * names, which is readability-pass.md §11. The name is here for the same reason from the other
 * side: once the zone frame took the artboards' 12 px of padding the body came down to 225, the
 * name was the last droppable column left, and the page became a list of gaps belonging to nobody.
 * A page keeps its position, its name and its gap whatever else it has to give up; a name whose
 * column is too narrow for a name becomes the three-letter code `table.ts` draws, which costs a
 * column nothing, and the loop stops when the three are all that is left.
 */
const NEVER_DROPPED: readonly ColumnId[] = ['pos', 'name', 'gap'];

/** The floor the canvas puts under the flexible driver column: below it the row sheds a column. */
const MINIMUM_NAME = 60;

/** The columns that fit `width`: drops the last droppable one until the row is no wider than its box. */
export function fittingColumns(columns: readonly ColumnId[], width: number, density: Density, rowHeight?: number): ColumnId[] {
  const kept = [...columns];
  const h = rowHeight ?? tableRowHeight(density);
  for (;;) {
    const widths = columnWidths(kept, width, density, h);
    const nameIndex = kept.indexOf('name');
    // A row with no name column has nothing left to flex, so it fits by construction.
    const name = nameIndex < 0 ? MINIMUM_NAME : (widths[nameIndex] ?? 0);
    if (name >= MINIMUM_NAME) break;
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
