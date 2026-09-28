/**
 * Module 15, Relative: the cars around you on track, you in the middle. The gap is the on-track
 * gap in seconds, signed: negative ahead, positive behind. It carries no state colour, because
 * being behind is not a fault.
 *
 * The row count is made odd so that "the middle" is a row and not a line between two.
 *
 * A zone may ask for the player's own class, which on a relative is the cars a driver is actually
 * racing rather than the ones they are about to be lapped by. The rig's `PositionMode` asks the
 * same of every relative there is, so a page passing no filter of its own, the companion's and the
 * pit wall's among them, still draws one class wherever the rig counts in class.
 */
import { listPlan, table, type ColumnId, type ListPlan } from '../second/table.ts';
import type { Archetype } from './shedding.ts';
import { drawsHeader, fittingColumns } from './leaderboard.ts';
import { defineModule, drawnAt, pageColumns, type ModuleContext } from './module.ts';

export const RELATIVE_COLUMNS: readonly ColumnId[] = ['pos', 'num', 'name', 'class', 'gap'];

/**
 * How many rows the relative lists at each shape: three cars either side of the player, and five at
 * `tall`.
 *
 * #339 made the count a window rather than a division — at most five either side, as many as the box
 * held at the canvas's row — and #328 makes it a declaration per shape, because a window that grows
 * with the box is still the box deciding. The question the page answers is *is the car behind me going
 * to be there at the next corner*, and that is the two either side; the third is the traffic a driver
 * is about to be in. The fourth and fifth are worth having only where the zone is tall enough that
 * they cost the rows nothing, which is the `tall` shape and nowhere else.
 *
 * **Seven is the catalogue's own count made odd.** It draws six rows at `wide` and at `grid`, eight at
 * `tall narrow` and eleven at `tall`, and the companion artboard draws seven. Seven is the one odd
 * count between six and eight, so it takes the catalogue's length at every shape it can rather than
 * rounding each of the three a different way; eleven is the catalogue's `tall` exactly.
 *
 * What the declaration buys is the height. Zone B of the 850 x 480 face listed nine cars at 30 px, and
 * eleven at 28 in its second arrangement; it lists seven at 39 and at 45 now, and a 39 px row is tall
 * enough for the 15 px name the face used to spend on two more cars at 13. `listPlan` is where the
 * height goes.
 *
 * **One either side is the floor**, because a relative is the player's row and the cars around it; a
 * two-row relative would be the player and one neighbour, and the neighbour would be whichever side
 * the arithmetic rounded to.
 */
const ROWS: Record<Archetype, number> = { wide: 7, grid: 7, tallNarrow: 7, tall: 11 };
const FEWEST_EITHER_SIDE = 1;

/**
 * The relative's plan for this box: its declared rows, the type they carry and the columns kept.
 *
 * Measured against the shape's own declared set, so that the name is measured against the columns the
 * page is actually going to draw rather than against the five it carries at its fullest.
 */
export const relativePlan = (ctx: ModuleContext): ListPlan =>
  listPlan(ctx.frame, {
    density: ctx.density,
    header: drawsHeader(ctx.density),
    columns: pageColumns(RELATIVE_COLUMNS, ctx),
    rows: ROWS[drawnAt(ctx)],
    least: 2 * FEWEST_EITHER_SIDE + 1,
    odd: true,
    fit: fittingColumns,
  });

export const relative = defineModule('relative', (ctx) => {
  const plan = relativePlan(ctx);
  return table({
    name: `${ctx.prefix}table`,
    frame: ctx.frame,
    columns: plan.columns,
    mode: 'relative',
    density: ctx.density,
    rows: plan.rows,
    rowHeight: plan.rowHeight,
    rowType: plan.rowType,
    header: drawsHeader(ctx.density),
    classOnly: ctx.classOnly,
  });
});
