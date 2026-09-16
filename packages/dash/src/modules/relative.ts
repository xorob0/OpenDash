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
import { rowsThatFit, table, tableRowHeight, type ColumnId } from '../second/table.ts';
import { drawsHeader, fittingColumns } from './leaderboard.ts';
import { defineModule, pageColumns } from './module.ts';

export const RELATIVE_COLUMNS: readonly ColumnId[] = ['pos', 'num', 'name', 'class', 'gap'];

export const relative = defineModule('relative', (ctx) => {
  const header = drawsHeader(ctx.density);
  const rowHeight = tableRowHeight(ctx.density);
  const fits = rowsThatFit(ctx.frame, { density: ctx.density, header, rowHeight });
  const rows = Math.max(3, fits % 2 === 0 ? fits - 1 : fits);
  return table({
    name: `${ctx.prefix}table`,
    frame: ctx.frame,
    columns: fittingColumns(pageColumns(RELATIVE_COLUMNS, ctx), ctx.frame.width, ctx.density, rowHeight),
    mode: 'relative',
    density: ctx.density,
    rows,
    rowHeight,
    header,
    classOnly: ctx.classOnly,
  });
});
