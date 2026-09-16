/**
 * Module 7, Tyres: the four corners, each a tyre drawing beside its temperature, its pressure and
 * the tread it has left, with the compound named once over the grid.
 *
 * The corners are laid out as the car is seen from above, which is why the cells are mirrored and
 * why the compound sits on the axle line between the rows. A box that is wide and short has no
 * second row to give them, so the four go in one line instead.
 *
 * iRacing's pressures are the ones the car left the box with rather than live figures, which is
 * worth knowing and is no longer said on the page: the captions under the grid are the canvas's
 * two, about the tread and about the tick.
 */
import { rect } from '../design/geometry.ts';
import { measureText } from '../design/advances.ts';
import { label } from '../elements/label.ts';
import { chip } from '../second/chip.ts';
import { densityOf } from '../second/density.ts';
import { ROW_TAIL, stack } from '../second/layout.ts';
import { wheel } from '../second/wheel.ts';
import { CORNERS, carCompound, player } from '../second/values.ts';
import { ncalc } from '../generator.ts';
import { blockRow, defineModule, drawnAt, pageKeeps, shapeIn } from './module.ts';

/**
 * The gap between the wheels, per the drawing the box takes: tighter between the rows than between
 * the sides of the car at every shape, and tightest of all where the catalogue draws the narrow
 * cell. The companion page is the same picture at a companion's size, so it keeps the widest
 * column gap and the row gap the artboard gives it.
 */
const GAPS = {
  wide: { x: 28, y: 7 },
  grid: { x: 18, y: 7 },
  tallNarrow: { x: 10, y: 4 },
  tall: { x: 18, y: 12 },
} as const;

const COMPANION_GAP = { x: 28, y: 12 } as const;

/** The compound chip: a badge rather than a label, so it keeps its own size at every density. */
const CHIP = { height: 28, size: 15, padding: 13, widest: 'MEDIUM' } as const;

/** Gap between the two captions when they share a line. */
const CAPTION_GAP = 20;

export const CAPTIONS = ['Tread left fills the tyre, inner to outer', 'A tick marks a wheel changed at the next stop'] as const;

export const tyres = defineModule('tyres', (ctx) => {
  const d = densityOf(ctx.density);
  const shape = shapeIn(ctx);
  // A wide short box is one row of four: two rows of cells in 158 px of pit wall zone leaves each
  // corner 75 px, which is one reading, where one row leaves it all three.
  const columns = shape.width === 'wide' && shape.height === 'short' ? 4 : 2;
  // A four-column row is only ever cut from a wide box, so it keeps the `wide` drawing's gaps
  // rather than the narrower ones its own height would otherwise hand it.
  const gap = ctx.density === 'companion' ? COMPANION_GAP : GAPS[columns === 4 ? 'wide' : drawnAt(ctx)];
  const rows = CORNERS.length / columns;
  const footer = pageKeeps('footer', ctx);
  const footerHeight = footer ? d.label : 0;
  // Less the tail at each end, which is the room `stack` really has: a grid sized to the whole
  // frame made the two rows one pixel too tall for it, and `rowsThatFit` answered by dropping the
  // caption at every size the build produces rather than at the one shape the catalogue drops it.
  const gridHeight = Math.max(0, ctx.frame.height - 2 * ROW_TAIL - (footer ? footerHeight + d.gapY : 0));
  const cell = {
    width: Math.floor((ctx.frame.width - gap.x * (columns - 1)) / columns),
    height: Math.floor((gridHeight - gap.y * (rows - 1)) / rows),
  };
  // The chip is centred over the grid, which is the axle line of a two-row car. A single row has no
  // axle line to put it on, so the four-column arrangement does not draw one.
  const compound = columns === 2 && pageKeeps('compound', ctx);
  const chipWidth = Math.ceil(2 * CHIP.padding + measureText('BarlowMedium', CHIP.widest, CHIP.size));
  return stack(
    ctx.frame,
    [
      blockRow(gridHeight, (bottom) => {
        const top = bottom - gridHeight;
        const cells = CORNERS.flatMap((corner, i) => {
          const box = rect(
            ctx.frame.left + (i % columns) * (cell.width + gap.x),
            top + Math.floor(i / columns) * (cell.height + gap.y),
            cell.width,
            cell.height,
          );
          // Mirrored so the readings sit outboard and the drawing inboard; a single row puts the
          // drawing first in every cell, as the pit wall's does.
          const numbers = columns === 2 && (corner === 'FrontLeft' || corner === 'RearLeft') ? 'left' : 'right';
          return wheel(`${ctx.prefix}${corner}`, box, corner, ctx.density, { numbers, gap: columns === 4 ? 7 : undefined });
        });
        if (!compound) return cells;
        return [
          ...cells,
          ...chip(`${ctx.prefix}compound`, 'Dry 2', Math.round(ctx.frame.left + (ctx.frame.width - chipWidth) / 2), Math.round(top + (gridHeight - CHIP.height) / 2), ctx.density, {
            inverted: true,
            height: CHIP.height,
            size: CHIP.size,
            width: chipWidth,
            bind: ncalc.ucase(carCompound(player())),
          }),
        ];
      }),
      ...(footer
        ? [
            blockRow(footerHeight, (bottom) => {
              // Both sentences where the line holds them, the tread one alone where it does not:
              // a caption cut in half by the renderer says less than the half that fits.
              const widths = CAPTIONS.map((text) => Math.ceil(measureText('BarlowMedium', text.toUpperCase(), d.label)) + 1);
              const both = widths[0]! + CAPTION_GAP + widths[1]! <= ctx.frame.width;
              const y = bottom - footerHeight;
              const captions = [
                label(`${ctx.prefix}footer.tread`, CAPTIONS[0], ctx.frame.left, y, widths[0]!, { size: d.label }),
                ...(both ? [label(`${ctx.prefix}footer.tick`, CAPTIONS[1], ctx.frame.left + widths[0]! + CAPTION_GAP, y, widths[1]!, { size: d.label })] : []),
              ];
              return captions;
            }),
          ]
        : []),
    ],
    ctx.density,
  );
});
