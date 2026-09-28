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
 * Columns give way as the box narrows, in the order written at {@link LEADERBOARD_COLUMNS}: the lap
 * times first, then the chip and the number, then the name's length. The gap outlives all of them,
 * because a leaderboard without a gap is a list of names.
 */
import { columnWidths, listPlan, NAME_FACE, nameFloorForRow, nameSizeForRow, rowSlack, table, tableRowHeight, type ColumnId, type ListPlan, type RowSize } from '../second/table.ts';
import { charsThatFit } from '../design/advances.ts';
import { defineModule, drawnAt, pageColumns, type ModuleContext } from './module.ts';
import { isPitWall, type Density } from '../second/density.ts';
import type { Archetype } from './shedding.ts';

/**
 * Every column this page has, in drawing order, and the rule for which of them gives way when a box
 * is short of room.
 *
 * The gap is drawn last, where the canvas draws it, after the two lap times. It used to sit before
 * them so that `fittingColumns` popping the tail would drop the times first; the order is the
 * canvas's now, and what survives is written down below rather than implied by where a column sits.
 *
 * **What gives way, in order** (#340). A long name used to push the row over and the gap was what
 * paid, which is the one column the page exists for. The name cannot push anything now -- it is cut in
 * the expression to the characters its column holds, so a twenty-five character name draws in the
 * same column as `Liam Byrne` (#172, #385) -- and what is left to decide is what a box too narrow for
 * the columns it keeps gives up. In this order, each step taken only when the one before it is spent:
 *
 * 1. **What the shape does not declare.** `shedding.ts` keeps all seven at `wide`, `pos · num · name ·
 *    class · gap` at `grid` and `tall`, and `pos · name · gap` at `tall narrow`: the lap times go at
 *    every shape but the fullest, and the chip and the number at the narrowest.
 * 2. **The droppable columns, from the right**, until the name clears its floor, the room the shortest
 *    of the four name formats needs: the best lap, the last lap, the class chip, the car number. A row
 *    sheds a column before it shortens a name (#385). The one box the build produces that takes this
 *    step is the 639 x 338 pit wall zone, whose `wide` declaration draws the last lap and not the best.
 * 3. **The name's length.** With nothing droppable left the name column is what remains between the
 *    position and the gap, and the name is ellipsised to it. The three narrow faces are here: seven,
 *    six and four characters at 850 x 480, 800 x 286 and 800 x 480.
 * 4. **The name**, once its column cannot hold one letter and the ellipsis after it, and then **the
 *    position**, once the position and the gap alone overrun the row. In the 28 px row the name goes
 *    below 191 px and the position below 156; the narrowest body the build produces is 225 px, so no
 *    box takes this step today, and it is written down so that the first one to does not draw its gap
 *    past its edge.
 *
 * **The gap is not on the list.** It is never dropped and never narrowed, its width being the canvas's
 * 92 px over content that needs 79 at 24, so every step above is taken before it moves a pixel. A box
 * narrower than the gap alone, 104 px with the 28 px row's padding, is the one box this page cannot
 * answer. `tables.test.ts` walks every width down to it and pins where each step is taken.
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
 * The three columns a list page is: the position, the name and the gap, which step 2 above never
 * sheds to make room for a name.
 *
 * `fittingColumns` used to pop from the end until a full name fitted, and with the gap drawn last
 * that is the column that paid: at 229 x 292 the leaderboard lost the gap and became a list of
 * names, which is readability-pass.md §11. The name is here for the same reason from the other
 * side: once the zone frame took the artboards' 12 px of padding the body came down to 225, the
 * name was the last droppable column left, and the page became a list of gaps belonging to nobody.
 * A name whose column is still too narrow for one used to become a three-letter code, which cost a
 * column nothing and said nothing; it is ellipsised now, which is the same trade made where a reader
 * can see it being made.
 */
const THE_ROW: readonly ColumnId[] = ['pos', 'name', 'gap'];

/**
 * Step 4: of those three, the ones a row too narrow to hold them gives up, first to last. The gap is
 * the third and is not here.
 */
const LAST_TO_GO: readonly ColumnId[] = ['name', 'pos'];

/**
 * The least a name column may hold and still be drawn: one letter and the ellipsis that says it was
 * cut. A column of one character draws the ellipsis alone and a column of none draws nothing, and
 * either of those is width the position and the gap need more.
 */
export const LEAST_NAME_CHARS = 2;

/**
 * The columns that fit `width`, given up in the order {@link LEADERBOARD_COLUMNS} states.
 *
 * The floor under the flexible driver column was the canvas's 60 px, which at 13 px is five characters
 * of the budget the name is cut to — a column that fits `L. By…`. It is the room the shortest of the
 * four name formats needs now, so a row sheds the column after the name before it shortens the name,
 * which is what #385 asked for; a row with nothing left to shed draws the name ellipsised to the
 * column it has, and a row that cannot hold even that gives up the name and then the position.
 *
 * The relative draws the same row and sheds it the same way, which is why it calls this.
 */
export function fittingColumns(columns: readonly ColumnId[], width: number, density: Density, rowHeight?: RowSize): ColumnId[] {
  const kept = [...columns];
  const h = rowHeight ?? tableRowHeight(density);
  const floor = nameFloorForRow(h);
  const nameWidth = (): number | undefined => {
    const index = kept.indexOf('name');
    return index < 0 ? undefined : (columnWidths(kept, width, density, h)[index] ?? 0);
  };
  // Step 2: shed from the right until the name clears its floor, or, in a row with no name to measure,
  // until the row fits its box.
  for (;;) {
    const name = nameWidth();
    if (name === undefined ? rowSlack(kept, width, density, h) >= 0 : name >= floor) break;
    const droppable = kept.map((id, i) => ({ id, i })).filter(({ id }) => !THE_ROW.includes(id));
    const last = droppable[droppable.length - 1];
    if (!last) break;
    kept.splice(last.i, 1);
  }
  // Step 4, the name and then the position, each only when the row cannot hold it. Step 3 is what
  // happens when neither is taken: the name keeps a column narrower than its floor and is cut to it.
  for (const id of LAST_TO_GO) {
    if (!kept.includes(id)) continue;
    const name = nameWidth();
    const short = id === 'name' ? charsThatFit(NAME_FACE, nameSizeForRow(h), name ?? 0) < LEAST_NAME_CHARS : rowSlack(kept, width, density, h) < 0;
    if (short) kept.splice(kept.indexOf(id), 1);
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
