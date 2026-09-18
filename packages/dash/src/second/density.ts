/**
 * How large a second-screen module draws. The same module code makes a companion page (a whole
 * 850 x 480 screen) and a pit wall zone (a panel as small as 639 x 202), so every size a module
 * uses comes from this table rather than from a literal.
 *
 * The numbers are the design canvas's two ramps: the companion's 116 / 64 / 46 / 34 / 24 and the
 * pit wall zone's 64 / 46 / 34 / 24 / 16. Each step is read from `ds` rather than written here, so
 * a size that moves in `design/tokens.json` reaches the drawing; the two ramps overlap by four
 * steps, which is why the same token is named `big` on one and `hero` on the other. The lower two
 * steps are the `ui` scale, whose scope line covers the pit wall tables at 96 dpi, and the upper
 * three are the card ramp the face already draws. Both ramps label at 15 over a 13 px small label, which is
 * the pair every artboard draws and the one distinction a page cannot lose: a label and a unit that
 * are the same size read as one run of text. `wide` is a zone that spans a column: the same type,
 * more room across.
 *
 * It is the zone ramp itself, and stays a density of its own because a caller says which kind of
 * zone it is drawing rather than how wide it is. The one number it used to change, a trace's
 * sample count, is cut from the plot's own width now.
 */
import { ds } from '../tokens.ts';

export type Density = 'companion' | 'zone' | 'compact' | 'wide' | 'panel';

/**
 * Gap between a field's label row and its value row. The canvas draws it as `gap: 5px` on the
 * companion artboards and on the zone pages alike; 5 is not on the `space` scale, which stops at 4
 * and then goes to 8, so the literal stays here with the canvas as its citation.
 */
const FIELD_GAP = 5;

export interface DensitySpec {
  /** The one big number of a module: speed, fuel, the delta. */
  hero: number;
  big: number;
  mid: number;
  small: number;
  tiny: number;
  /** Field labels and zone titles. */
  label: number;
  /**
   * The row a label is centred in, which is not the size it is set in. Every sheet in `design/`
   * draws a label as a 15 px run inside a `height: 13px` row and then leaves `fieldGap` before the
   * value, so the ramp's step from 13 to 15 is bought in width and not in height: a field grows no
   * taller and the panels whose height the sheets fix to the pixel keep fitting. `bandPages.ts`
   * writes the same number as `LABEL_ROW`.
   */
  labelRow: number;
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
}

const COMPANION: DensitySpec = {
  hero: ds.size.hero,
  big: ds.size.lapTime,
  mid: ds.size.value,
  small: ds.size.valueSm,
  tiny: ds.ui.numeralLg,
  label: ds.size.label,
  labelRow: ds.size.labelSm,
  labelSm: ds.size.labelSm,
  name: 15,
  gapX: ds.space[5],
  gapY: ds.space[4],
  fieldGap: FIELD_GAP,
  rowHeight: 38,
  headerHeight: 24,
  cellGap: 12,
  bar: 6,
  chipHeight: 20,
  chipPadding: 6,
  padX: 24,
  padY: 16,
};

const ZONE: DensitySpec = {
  hero: ds.size.lapTime,
  big: ds.size.value,
  mid: ds.size.valueSm,
  small: ds.ui.numeralLg,
  tiny: ds.ui.numeral,
  label: ds.size.label,
  labelRow: ds.size.labelSm,
  labelSm: ds.size.labelSm,
  name: 13,
  gapX: ds.space[5],
  gapY: 12,
  fieldGap: FIELD_GAP,
  rowHeight: 26,
  headerHeight: 20,
  cellGap: ds.space[3],
  bar: 4,
  chipHeight: 20,
  chipPadding: 6,
  padX: 16,
  padY: 6,
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
 * numbers. That is the floor the ramp is allowed to reach, and it is where the small label sits.
 * The label itself keeps one step above it, because a label and the unit after it being the same
 * size is the distinction the artboards draw and the reason this ramp has two numbers at all; 13
 * over 12 is the narrowest that separation can be written in.
 */
const COMPACT: DensitySpec = {
  ...ZONE,
  hero: ZONE.big,
  big: ZONE.mid,
  mid: ZONE.small,
  // TODO: font.size.ui has no step between numeral (16) and numeralLg (24), and none below 16 that
  // is a numeral rather than a label, so the last two rungs of the step-down have no token to read.
  small: 18,
  tiny: 14,
  label: 13,
  labelRow: 12,
  labelSm: 12,
  name: 12,
  gapX: ds.space[4],
  gapY: 8,
  rowHeight: 20,
  headerHeight: 16,
  cellGap: 6,
  // The canvas draws the chip 20 high with 6 either side, which is what the other two densities
  // give it. A 20 px row cannot hold a 20 px chip, so this ramp keeps its own pair.
  chipHeight: 15,
  chipPadding: 4,
  padX: 10,
  padY: 4,
};

/**
 * The zone ramp on the pit wall, which labels a step lower than the same ramp on a face.
 *
 * The six pit wall sheets and `Panels.dc.html` write every field label as `.lblt`, 13 px, and not
 * one of them uses the `.lbl` at 15 that `ZoneCatalogue.dc.html` draws five hundred times. That is
 * a distance rule rather than an inconsistency: a face is read at arm's length over a wheel and a
 * pit wall across a garage, where the row a reader scans is the value and the label beside it is
 * there to be found once. Only the label moves; the row it sits in does not, so no panel whose
 * height the sheets fix to the pixel changes and nothing is shed for it.
 */
const PANEL: DensitySpec = { ...ZONE, label: ds.size.labelSm };

export const DENSITIES: Record<Density, DensitySpec> = {
  companion: COMPANION,
  zone: ZONE,
  compact: COMPACT,
  wide: ZONE,
  panel: PANEL,
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

/**
 * The named sizes of a density, smallest first. This is the ramp, and it is the only ladder a
 * value is allowed to move along.
 *
 * Rule 20 is written against it: a rank grows until it meets an edge, and one of the three edges
 * is the next size up here. That ceiling is what separates filling a box from scaling into one --
 * a page that has spent its room is the same drawing one size larger, not a page stretched.
 */
export const rampOf = (density: Density): readonly number[] => {
  const d = densityOf(density);
  return [d.tiny, d.small, d.mid, d.big, d.hero];
};

/** The next size up the ramp from `fs`, or `fs` itself once it is at the top. */
export function nextOnRamp(fs: number, density: Density): number {
  for (const size of rampOf(density)) if (size > fs) return size;
  return fs;
}
