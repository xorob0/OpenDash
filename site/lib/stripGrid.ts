/**
 * The strip grid the Lights page draws, as a pure function of the shape list.
 *
 * Apart from `strips.ts` because that one reads the generated content, which is gitignored and may
 * not exist when the tests run; this one imports nothing but a type, so `test/` can hold it.
 */
import type { SiteStripShape } from '../scripts/content';

/** The strip shapes laid out as the Lights page draws them. */
export interface SiteStripGrid {
  /** The sided grid: every shape with something at its ends, legacy ones excluded. */
  sided: SiteStripShape[];
  /** Bare runs, brows included: nothing at the ends. */
  bare: SiteStripShape[];
  legacy: SiteStripShape[];
  /** The side lengths the grid has, ascending, with the bare runs as the row of none. */
  sides: number[];
  /** The centre lengths the sided grid has, ascending. */
  centres: number[];
}

/**
 * The grid the Lights page draws, from the site's shape list.
 *
 * A far-end wiring is never a cell: the grid is geometries, and a `-reversed` shape that reached the
 * list anyway is left out here rather than drawn as a row of its own.
 */
export function stripGrid(shapes: readonly SiteStripShape[]): SiteStripGrid {
  const wiredOwnWay = (s: SiteStripShape): boolean => s.id.endsWith('-reversed') || s.id.endsWith('-fanatec');
  const sided = shapes.filter((s) => !s.legacy && !wiredOwnWay(s) && s.left > 0);
  const bare = shapes.filter((s) => !s.legacy && !wiredOwnWay(s) && s.left === 0);
  return {
    sided,
    bare,
    legacy: shapes.filter((s) => s.legacy),
    sides: [0, ...new Set(sided.map((s) => s.left))].sort((a, b) => a - b),
    centres: [...new Set(sided.map((s) => s.centre))].sort((a, b) => a - b),
  };
}
