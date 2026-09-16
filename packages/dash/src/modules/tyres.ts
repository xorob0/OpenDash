/**
 * Module 7, Tyres: the four corners in car orientation, each with its temperature, its pressure,
 * the tread left behind the numbers and the compound.
 *
 * iRacing's pressures are the ones the car left the box with, so the footer says so rather than
 * letting a stale number read as live.
 */
import { rect } from '../design/geometry.ts';
import { label } from '../elements/label.ts';
import { densityOf } from '../second/density.ts';
import { ROW_TAIL, stack } from '../second/layout.ts';
import { wheel } from '../second/wheel.ts';
import { CORNERS } from '../second/values.ts';
import { blockRow, defineModule, pageKeeps } from './module.ts';

/** Gap between the wheels: tighter between the rows than between the sides of the car. */
const GAP = { x: 24, y: 8 } as const;

export const FOOTER = 'TEMPERATURE · PRESSURE FROM THE LAST STOP · TREAD BEHIND';

export const tyres = defineModule('tyres', (ctx) => {
  const d = densityOf(ctx.density);
  const footer = pageKeeps('footer', ctx);
  const footerHeight = footer ? d.labelSm : 0;
  // Less the tail at each end, which is the room `stack` really has: a grid sized to the whole
  // frame made the two rows one pixel too tall for it, and `rowsThatFit` answered by dropping the
  // caption at every size the build produces rather than at the one shape the catalogue drops it.
  const gridHeight = Math.max(0, ctx.frame.height - 2 * ROW_TAIL - (footer ? footerHeight + d.gapY : 0));
  const cell = {
    width: Math.floor((ctx.frame.width - GAP.x) / 2),
    height: Math.floor((gridHeight - GAP.y) / 2),
  };
  return stack(
    ctx.frame,
    [
      blockRow(gridHeight, (bottom) => {
        const top = bottom - gridHeight;
        return CORNERS.flatMap((corner, i) => {
          const box = rect(ctx.frame.left + (i % 2) * (cell.width + GAP.x), top + Math.floor(i / 2) * (cell.height + GAP.y), cell.width, cell.height);
          return wheel(`${ctx.prefix}${corner}`, box, corner, ctx.density);
        });
      }),
      ...(footer
        ? [
            blockRow(footerHeight, (bottom) => [
              label(`${ctx.prefix}footer`, FOOTER, ctx.frame.left, bottom - footerHeight, ctx.frame.width, { size: d.labelSm }),
            ]),
          ]
        : []),
    ],
    ctx.density,
  );
});
