/**
 * Module 15, Relative: the cars around you on track, you in the middle. The gap is the on-track
 * gap in seconds, signed: negative ahead, positive behind. It carries no state colour, because
 * being behind is not a fault.
 *
 * The row count is made odd so that "the middle" is a row and not a line between two.
 *
 * A zone may ask for the player's own class, which on a relative is the cars a driver is actually
 * racing rather than the ones they are about to be lapped by. The companion and the pit wall ask
 * for nobody in particular and get the whole track, as they always have.
 */
import { densityOf } from '../second/density.ts';
import { rowCapacity, table, type ColumnId } from '../second/table.ts';
import { fittingColumns } from './leaderboard.ts';
import { defineModule, pageColumns } from './module.ts';

export const RELATIVE_COLUMNS: readonly ColumnId[] = ['pos', 'num', 'name', 'class', 'gap'];

export const relative = defineModule('relative', (ctx) => {
  const capacity = rowCapacity(ctx.frame, ctx.density, true, densityOf(ctx.density).rowHeight);
  const rows = Math.max(3, capacity % 2 === 0 ? capacity - 1 : capacity);
  return table({
    name: `${ctx.prefix}table`,
    frame: ctx.frame,
    columns: fittingColumns(pageColumns(RELATIVE_COLUMNS, ctx), ctx.frame.width, ctx.density),
    mode: 'relative',
    density: ctx.density,
    rows,
    classOnly: ctx.classOnly,
  });
});
