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
  // A driver's name is not a size of the density. It is the list row's, `LIST_ROW_TYPES` in
  // `table.ts`, wherever the name is drawn: the relative, the leaderboard and the opponents page used
  // to answer how large a name is twice, 15 / 13 in a list and 15 / 13 / 12 here, and #341 made it once.
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
  // A wide zone is a pit wall screen too, so it takes the panel's label; it stays a density of its
  // own because a module reads the name to decide what a wide box may draw, not to size its type.
  wide: PANEL,
  panel: PANEL,
};

export const densityOf = (density: Density): DensitySpec => DENSITIES[density];

/** True for the small densities, which are the zones and the pit wall's panels. */
export const isZone = (density: Density): boolean => density !== 'companion';

/**
 * True for the pit wall's densities: its panels, and the zone dashboards of both kinds it embeds.
 *
 * A pit wall is read across a garage by somebody who is not driving, and a list on it answers *who is
 * in the race* rather than *who is near me*, so where a face's list declares the rows it wants a pit
 * wall's takes every row its box holds. `screens/zones.ts` builds its standard zones at `panel` and its
 * wide ones at `wide`, and no face or companion draws at either, which is what makes the density the
 * place to ask.
 */
export const isPitWall = (density: Density): boolean => density === 'panel' || density === 'wide';

/**
 * The density a box of this size wants. Below the thresholds the zone ramp does not fit, which is
 * a fact about the box rather than about the page in it, so the choice is made once here.
 */
export const densityForBox = (box: { width: number; height: number }): Density =>
  box.width < 320 || box.height < 220 ? 'compact' : 'zone';

/**
 * True for the two densities a face's zones are drawn at, which are the two `densityForBox` chooses
 * between. The companion draws at its own and the pit wall at `panel` and `wide`.
 */
export const isFace = (density: Density): boolean => density === 'zone' || density === 'compact';

/**
 * The named sizes of a density, smallest first. This is the ramp: the sizes a page is drawn at
 * before rule 20 grows it, and on the companion and the pit wall the limit of that growth.
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

/**
 * How far rule 20 takes a page on a face past the sizes it is drawn at: the canvas's own figure.
 *
 * Every `FaceVariants` sheet embeds the catalogue's drawing of each page, reflows it to that face's
 * real zone and grows it "as one, hierarchy intact, until it meets the width, the height or a
 * ceiling of ×2.2 (rule 20)", and chips the factor each page reached, from ×1.02 to ×2.2. A ratio
 * rather than a size, so it is cited here rather than defined in the tokens, as pit view's ratio is.
 */
export const FACE_GROWTH = 2.2;

/**
 * The largest size rule 20 may grow a value drawn at `fs` to. **The rule's third edge.**
 *
 * It is two answers, because the canvas draws the two kinds of surface two ways. A face's zone is a
 * rectangle the catalogue never drew -- it draws each page at four archetype sizes -- so the page is
 * the catalogue's drawing taken to a box of another size, and the sheets take it up to ×2.2. The
 * companion and the pit wall are drawn on artboards of their own screens, at the sizes they are
 * built at, so a page there is already the drawing of its box and grows no further than the next
 * name on its ramp: what is left to spend is the difference between the artboard's box and the
 * build's, and not a factor of two.
 *
 * The ramp step was the answer on faces too until #330. It cost a page a factor of about 1.35 in
 * every box, being the smallest step any size on the page takes and 34 to 46 on every page that
 * mixes the two, and a factor of exactly one where the lead was already at the top of its ramp,
 * which is to say on the pages whose one number is the reason for the page. zones.md §2 has the
 * argument.
 */
export const grownAtMost = (fs: number, density: Density): number => (isFace(density) ? fs * FACE_GROWTH : nextOnRamp(fs, density));
