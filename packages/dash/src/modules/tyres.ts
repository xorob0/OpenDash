/**
 * Module 7, Tyres: the four corners, each a tyre drawing beside its temperature, its pressure and
 * the tread it has left, with the compound named once over the grid.
 *
 * The corners are laid out as the car is seen from above, which is why the cells are mirrored and
 * why the compound sits on the axle line between the rows. A box that is wide and short has no
 * second row to give them, so the four go in one line instead.
 *
 * iRacing's pressures are the ones the car left the box with rather than live figures, which is
 * worth knowing and is no longer said on the page: the captions under the grid are the canvas's two,
 * about the tread and about the tick, and #384's third, about the compound.
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

/**
 * The compound chip: a badge rather than a label, so it keeps its own size at every density.
 *
 * No word sits beside it on the axle line, which #384 asked for and the drawing has no room for: the
 * chip is centred there, the only band free of the two inboard drawings is the column gap of 18 to
 * 28 px, and the chip's own 78 px already overlap each front tyre by about thirty. A `Compound`
 * beside it measures 62 px more and would cover a third of each of them. What names it instead is a
 * caption under the grid, where the page already explains the tick, drawn wherever the footer line
 * has the room for a second sentence; the room for a label on the axle line is the canvas's to give,
 * and the disagreement is recorded in `docs/design/zones.md` §10 rather than settled here.
 */
const CHIP = { height: 28, size: 15, padding: 13, widest: 'Medium' } as const;

/** Gap between two captions sharing a line. */
const CAPTION_GAP = 20;

/** One caption under the grid: the mark it names, and the sentence naming it. */
interface Caption {
  id: string;
  text: string;
}

/**
 * The captions, in the order the canvas draws them: what fills the tyre, what the tick on it means,
 * and, #384's, what the word between the axles is.
 */
export const CAPTIONS: readonly Caption[] = [
  { id: 'tread', text: 'Tread left fills the tyre, inner to outer' },
  { id: 'tick', text: 'A tick marks a wheel changed at the next stop' },
  { id: 'compound', text: 'The word between the axles is the compound' },
];

/**
 * Which caption a line too short for all of them gives up first.
 *
 * This used to be the array order, which is the canvas's drawing order and not an order of
 * importance: the tread's sentence came first and so survived, and the tick's went, leaving the tick
 * itself drawn -- it is drawn at every shape -- with nothing anywhere on the page saying what it is.
 *
 * The order here is what #384 measures a page by, which is whether each quantity carries something
 * naming it. The tick and the compound carry nothing else; the tread's columns stand beside a figure
 * that already carries its own per cent, and what its sentence adds is which column is the inner
 * shoulder. So the tread's is the first to go and the tick's the last, and the two mid-sized boxes
 * that once drew tread and tick now draw tick and compound. What they are drawn in is still the
 * canvas's order, which the trade is recorded against in `docs/design/zones.md` §10.
 */
const SHED_FIRST: readonly string[] = ['tread', 'compound', 'tick'];

/** A caption's box: the sentence as it is drawn, and the pixel of slack WPF needs not to clip it. */
const captionWidth = (caption: Caption, fs: number): number => Math.ceil(measureText('BarlowMedium', caption.text, fs)) + 1;

/**
 * The captions the line holds, in the order they are drawn: as many as fit, shed by
 * {@link SHED_FIRST}. None, where it does not hold even the last of them, a caption cut in half by
 * the renderer saying less than the half that fits.
 */
function captionsThatFit(candidates: readonly Caption[], width: number, fs: number): Caption[] {
  const total = (kept: readonly Caption[]): number => kept.reduce((sum, caption) => sum + captionWidth(caption, fs), 0) + CAPTION_GAP * Math.max(0, kept.length - 1);
  let kept = [...candidates];
  for (const id of SHED_FIRST) {
    if (total(kept) <= width) break;
    kept = kept.filter((caption) => caption.id !== id);
  }
  return kept;
}

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
            bind: carCompound(player()),
          }),
        ];
      }),
      ...(footer
        ? [
            blockRow(footerHeight, (bottom) => {
              // The compound's sentence is offered only where the chip it names is drawn; a
              // four-column row has no axle line to put one on and so nothing to explain.
              const candidates = CAPTIONS.filter((caption) => caption.id !== 'compound' || compound);
              const y = bottom - footerHeight;
              let x = ctx.frame.left;
              return captionsThatFit(candidates, ctx.frame.width, d.label).map((caption) => {
                const width = captionWidth(caption, d.label);
                const item = label(`${ctx.prefix}footer.${caption.id}`, caption.text, x, y, width, { size: d.label });
                x += width + CAPTION_GAP;
                return item;
              });
            }),
          ]
        : []),
    ],
    ctx.density,
  );
});
