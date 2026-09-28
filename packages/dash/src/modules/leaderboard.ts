/**
 * Module 14, Leaderboard: the order of the race, overall or in the player's class. The page declares
 * how many rows it lists at each shape and a box too short for them lists fewer, so the same module is
 * seven rows on a companion page and four in a short zone. A pit wall's zone is the exception and
 * lists every car it has room for; see {@link rowsIn}.
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
import { columnWidths, listPlan, nameFloorForRow, table, tableRowHeight, type ColumnId, type ListPlan, type RowSize } from '../second/table.ts';
import { defineModule, drawnAt, pageColumns, type ModuleContext } from './module.ts';
import { isPitWall, type Density } from '../second/density.ts';
import type { Archetype } from './shedding.ts';

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
 * A page keeps its position, its name and its gap whatever else it has to give up, and the loop stops
 * when the three are all that is left. A name whose column is still too narrow for one used to become
 * a three-letter code, which cost a column nothing and said nothing; it is ellipsised now, which is
 * the same trade made where a reader can see it being made.
 */
const NEVER_DROPPED: readonly ColumnId[] = ['pos', 'name', 'gap'];

/**
 * The columns that fit `width`: drops the last droppable one until the row is no wider than its box.
 *
 * The floor under the flexible driver column was the canvas's 60 px, which at 13 px is five characters
 * of the budget the name is cut to — a column that fits `L. By…`. It is the room the shortest of the
 * four name formats needs now, so a row sheds the column after the name before it shortens the name,
 * which is what #385 asked for; a row with nothing left to shed draws the name ellipsised to the
 * column it has.
 */
export function fittingColumns(columns: readonly ColumnId[], width: number, density: Density, rowHeight?: RowSize): ColumnId[] {
  const kept = [...columns];
  const h = rowHeight ?? tableRowHeight(density);
  const floor = nameFloorForRow(h);
  for (;;) {
    const widths = columnWidths(kept, width, density, h);
    const nameIndex = kept.indexOf('name');
    // A row with no name column has nothing left to flex, so it fits by construction.
    const name = nameIndex < 0 ? floor : (widths[nameIndex] ?? 0);
    if (name >= floor) break;
    const droppable = kept.map((id, i) => ({ id, i })).filter(({ id }) => !NEVER_DROPPED.includes(id));
    const last = droppable[droppable.length - 1];
    if (!last) break;
    kept.splice(last.i, 1);
  }
  return kept;
}

/**
 * How many rows the leaderboard lists at each shape, which is the count the drawings give it.
 *
 * `ZoneCatalogue.dc.html` draws six rows at `wide`, at `grid` and at `tall`, and eight at `tall
 * narrow`; the companion artboard draws seven. Three of the four are taken as drawn, and two are not:
 *
 * - **`wide` is seven**, the companion's count rather than the zone sheet's six, because the `wide` row
 *   of zones.md section 5 is the page's fullest form and carries what the companion artboard gives it.
 *   The companion's own page is a `wide` box, so this is also the count that keeps it drawing what its
 *   artboard draws, where it used to draw eight.
 * - **`tall` is eleven**, where the catalogue draws six. `SHAPE_ARCHETYPES` defines the shape as the one
 *   where *a list grows rows*, and the catalogue's own relative does, to eleven, in the same 360 x 470
 *   box; the leaderboard's six there leave 250 px of that box empty under a `justify-content: center`.
 *   Eleven is the count the other list takes at the shape, so the two list pages of a tall zone are one
 *   length. The canvas owes the redraw.
 *
 * Which of the order a zone should show — the head of the field, or the cars around the player the way a
 * split board does — is #340's question, and the count is the half of it that belongs to #328.
 */
const ROWS: Record<Archetype, number> = { wide: 7, grid: 6, tallNarrow: 8, tall: 11 };

/**
 * How many rows the leaderboard lists in this context: the count above on a face and on the companion,
 * and on the pit wall every car the box holds at the canvas's row.
 *
 * #328 is a face's question. A zone of a face asks *who is near me and by how much*, the answer is a
 * few rows, and height the count does not need is better spent on type and space. A pit wall zone asks
 * *who is in the race*, and there buying rows is right, which the ticket says in as many words; its
 * `519 x 359` zone listed eight before #328 and would have listed six under the `grid` count, two cars
 * fewer under 18 px of air a row. So the pit wall keeps listing the field, at a row stretched to fill
 * the box and carrying the largest type the plan allows, which is the same answer to height it gets
 * everywhere else.
 */
const rowsIn = (ctx: ModuleContext): number => (isPitWall(ctx.density) ? Number.POSITIVE_INFINITY : ROWS[drawnAt(ctx)]);

/** The leaderboard's plan for this box: its declared rows, the type they carry and the columns kept. */
export const leaderboardPlan = (ctx: ModuleContext): ListPlan =>
  listPlan(ctx.frame, {
    density: ctx.density,
    header: drawsHeader(ctx.density),
    columns: pageColumns(LEADERBOARD_COLUMNS, ctx),
    rows: rowsIn(ctx),
    fit: fittingColumns,
  });

export const leaderboard = defineModule('leaderboard', (ctx) => {
  const plan = leaderboardPlan(ctx);
  return table({
    name: `${ctx.prefix}table`,
    frame: ctx.frame,
    columns: plan.columns,
    mode: 'full',
    density: ctx.density,
    rows: plan.rows,
    rowHeight: plan.rowHeight,
    rowType: plan.rowType,
    header: drawsHeader(ctx.density),
    classOnly: ctx.classOnly,
  });
});
