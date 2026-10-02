/**
 * The strip shapes as the Lights page lays them out.
 *
 * The build generates a grid, sides of 0 to 4 around a centre of 4 to 12, then the long bare runs
 * for brows, then the five legacy shapes that shipped before the grid. The page draws them in
 * exactly those three groups, and every one of them is read from the generated list rather than
 * typed, so a shape the build adds appears on the page without anyone touching it.
 */
import { STRIP_SHAPES } from './content.generated';
import type { SiteStripShape } from '../scripts/content';
import { stripGrid } from './stripGrid';

export const SHAPES: readonly SiteStripShape[] = STRIP_SHAPES;

const GRID = stripGrid(SHAPES);

/** The sided grid: every shape with something at its ends, legacy ones excluded. */
export const SIDED: readonly SiteStripShape[] = GRID.sided;

/** Bare runs, brows included: nothing at the ends. */
export const BARE: readonly SiteStripShape[] = GRID.bare;

export const LEGACY: readonly SiteStripShape[] = GRID.legacy;

/** The side lengths the grid has, ascending, with the bare runs as the row of none. */
export const SIDES: readonly number[] = GRID.sides;

/** The centre lengths the sided grid has, ascending. */
export const CENTRES: readonly number[] = GRID.centres;

/** The shape with these sides and this centre, if the grid has it. Legacy shapes fill the one hole. */
export const shapeAt = (side: number, centre: number): SiteStripShape | undefined =>
  SHAPES.find((s) => s.left === side && s.right === side && s.centre === centre && !s.id.includes('reversed') && !s.id.includes('fanatec'));

/** How many LEDs a shape has in its main run. */
export const length = (s: SiteStripShape): number => s.left + s.centre + s.right;
