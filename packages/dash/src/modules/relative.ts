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
import { columnWidths, nameFloorForRow, ROW_TYPE_STEPS, rowHeightThatFills, rowsThatFit, table, tableRowHeight, type ColumnId } from '../second/table.ts';
import type { Density } from '../second/density.ts';
import type { Rect } from '../design/geometry.ts';
import { drawsHeader, fittingColumns } from './leaderboard.ts';
import { defineModule, pageColumns } from './module.ts';

export const RELATIVE_COLUMNS: readonly ColumnId[] = ['pos', 'num', 'name', 'class', 'gap'];

/**
 * The most cars the relative lists either side of the player, and the least.
 *
 * #339 asked for the row count to be a declaration with an argument behind it rather than whatever
 * `rowCapacity` divided out, and this is it. **Five either side.** The question the page answers is
 * *is the car behind me going to be there at the next corner*, and that is the two either side; the
 * third, fourth and fifth are the traffic a driver is about to be in, which is worth having on a tall
 * zone. Past the fifth the list stops being read and becomes texture: zone C of the 1280 x 720 face
 * was listing fifteen cars, and nobody reads the seventh row down from their own while braking.
 *
 * **One either side is the floor**, because a relative is the player's row and the cars around it; a
 * two-row relative would be the player and one neighbour, and the neighbour would be whichever side
 * the arithmetic rounded to.
 *
 * The count is odd both ways, so the player's own row is a row and not the line between two.
 */
const MOST_EITHER_SIDE = 5;
const FEWEST_EITHER_SIDE = 1;

/**
 * The rows the relative lists in a body of this height, and the row it draws them at.
 *
 * Two decisions, and the order is the point. The count comes first, from what the box holds at the
 * row the density declares, clamped to the window above; the row then **stretches to fill the height
 * those rows leave**, rather than the block being centred in a pool of slack. A zone C of 560 px
 * listing eleven cars at 28 px is 328 px of list and 232 px of nothing, which reads as a page that
 * failed to draw; the same eleven at 49 px is the same list with air between the rows, and air between
 * the rows of a relative is what separates the car ahead from the car behind at a glance.
 *
 * Nothing about the type runs away with the row. `rowTypeOf` stops at the 38 px row, which is the
 * tallest the canvas draws a table at, so a row stretched past it is spacing and not size — which is
 * rule 20's third edge read for a table: the height of the box, the width of the box, and the next
 * size up the ramp.
 */
function relativeRowPlan(frame: Rect, density: Density, header: boolean, columns: readonly ColumnId[]): { rows: number; rowHeight: number } {
  const declared = tableRowHeight(density);
  const fit = { density, header, rowHeight: declared };
  const fits = rowsThatFit(frame, fit);
  const odd = fits % 2 === 0 ? fits - 1 : fits;
  const rows = Math.max(2 * FEWEST_EITHER_SIDE + 1, Math.min(2 * MOST_EITHER_SIDE + 1, odd));
  const stretched = rowHeightThatFills(frame, fit, rows);
  // The heights to try, tallest first: the stretch itself, then the top of each type step under it.
  // A stretch that crosses a step takes the position and the gap up with it, and both are monospaced
  // columns of a fixed budget, so the width comes out of the name. At 245 px the 800 x 286 face's
  // second arrangement stretched from 29 to 36, promoted its numerals and cut the name from six
  // characters to four -- a page spending the room it gained on the column it exists to be read by.
  const steps = ROW_TYPE_STEPS.map((step) => step - 1).filter((h) => h >= declared && h < stretched);
  const tried = [stretched, ...steps].sort((a, b) => b - a);
  for (const rowHeight of tried) {
    const kept = fittingColumns(columns, frame.width, density, rowHeight);
    const name = columnWidths(kept, frame.width, density, rowHeight)[kept.indexOf('name')] ?? 0;
    if (name >= nameFloorForRow(rowHeight)) return { rows, rowHeight };
  }
  // Nothing holds a name: take the shortest row tried, which is the one that leaves the column widest.
  return { rows, rowHeight: tried[tried.length - 1] ?? declared };
}

export const relative = defineModule('relative', (ctx) => {
  const header = drawsHeader(ctx.density);
  // The shape's own declared set, so that the row plan measures the name against the columns the page
  // is actually going to draw rather than against the five it carries at its fullest.
  const declared = pageColumns(RELATIVE_COLUMNS, ctx);
  const { rows, rowHeight } = relativeRowPlan(ctx.frame, ctx.density, header, declared);
  return table({
    name: `${ctx.prefix}table`,
    frame: ctx.frame,
    columns: fittingColumns(declared, ctx.frame.width, ctx.density, rowHeight),
    mode: 'relative',
    density: ctx.density,
    rows,
    rowHeight,
    header,
    classOnly: ctx.classOnly,
  });
});
