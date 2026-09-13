/**
 * How large a second-screen module draws. The same module code makes a companion page (a whole
 * 850 x 480 screen) and a pit wall zone (a panel as small as 639 x 202), so every size a module
 * uses comes from this table rather than from a literal.
 *
 * The numbers are the design canvas's two ramps: the companion's 116 / 64 / 46 / 34 / 24 and the
 * pit wall zone's 64 / 46 / 34 / 24 / 16, with 15 px labels on the companion and 13 px on the pit
 * wall. `wide` is a zone that spans a column: the same type, more room across.
 */
import { ds } from '../tokens.ts';

export type Density = 'companion' | 'zone' | 'compact' | 'wide';

export interface DensitySpec {
  /** The one big number of a module: speed, fuel, the delta. */
  hero: number;
  big: number;
  mid: number;
  small: number;
  tiny: number;
  /** Field labels and zone titles. */
  label: number;
  /** Units, denominators and compound letters. */
  labelSm: number;
  /** Driver names, which are Barlow Medium and never monospaced. */
  name: number;
  /** Gap between the fields of a row. */
  gapX: number;
  /** Gap between the rows of a module. */
  gapY: number;
  /** Gap between a field's label and its value. */
  fieldGap: number;
  /** Height of a table row and of a table's header row. */
  rowHeight: number;
  headerHeight: number;
  /** Gap between the cells of a table row. */
  cellGap: number;
  /** Height of a bar gauge's track. */
  bar: number;
  /** Height of a class chip and the padding either side of its text. */
  chipHeight: number;
  chipPadding: number;
  /** Padding between a module's rect and its content. */
  padX: number;
  padY: number;
  /** Samples a trace keeps, which is also its horizontal resolution. */
  tracePoints: number;
}

const COMPANION: DensitySpec = {
  hero: 116,
  big: 64,
  mid: 46,
  small: 34,
  tiny: 24,
  label: ds.size.label,
  labelSm: ds.size.labelSm,
  name: 15,
  gapX: ds.space[6],
  gapY: 20,
  fieldGap: ds.space[1],
  rowHeight: 38,
  headerHeight: 24,
  cellGap: 12,
  bar: 6,
  chipHeight: 20,
  chipPadding: 6,
  padX: 24,
  padY: 16,
  tracePoints: 300,
};

const ZONE: DensitySpec = {
  hero: 64,
  big: 46,
  mid: 34,
  small: 24,
  tiny: 16,
  label: ds.size.labelSm,
  labelSm: ds.size.labelSm,
  name: 13,
  gapX: ds.space[5],
  gapY: 12,
  fieldGap: ds.space[1],
  rowHeight: 26,
  headerHeight: 20,
  cellGap: 8,
  bar: 4,
  chipHeight: 18,
  chipPadding: 5,
  padX: 16,
  padY: 6,
  tracePoints: 600,
};

/**
 * A zone small enough that the zone ramp does not fit it.
 *
 * The nano's zones are 269 by 194 and the 800 by 480's are 249 by 328, which are a different
 * instrument from a 769 by 314 zone rather than the same one squeezed. A 64 px hero in a 144 px
 * body leaves room for nothing under it, so the whole ramp steps down and the page keeps its rows.
 *
 * The labels stop at 12 px rather than scaling with the rest: below that a label stops being
 * readable at arm's length on a DDU, and a page whose label cannot be read is a page of unlabelled
 * numbers. That is the floor the ramp is allowed to reach.
 */
const COMPACT: DensitySpec = {
  ...ZONE,
  hero: 46,
  big: 34,
  mid: 24,
  small: 18,
  tiny: 14,
  label: 12,
  labelSm: 12,
  name: 12,
  gapX: ds.space[4],
  gapY: 8,
  rowHeight: 20,
  headerHeight: 16,
  cellGap: 6,
  chipHeight: 15,
  chipPadding: 4,
  padX: 10,
  padY: 4,
};

export const DENSITIES: Record<Density, DensitySpec> = {
  companion: COMPANION,
  zone: ZONE,
  compact: COMPACT,
  wide: { ...ZONE, tracePoints: 900 },
};

export const densityOf = (density: Density): DensitySpec => DENSITIES[density];

/** True for the small densities, which are the zones and the pit wall's panels. */
export const isZone = (density: Density): boolean => density !== 'companion';

/**
 * The density a box of this size wants. Below the thresholds the zone ramp does not fit, which is
 * a fact about the box rather than about the page in it, so the choice is made once here.
 */
export const densityForBox = (box: { width: number; height: number }): Density =>
  box.width < 320 || box.height < 220 ? 'compact' : 'zone';
