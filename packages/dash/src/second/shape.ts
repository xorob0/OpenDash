/**
 * The shape of the box a page is drawn in.
 *
 * Rule 17: *a page answers to the shape of its zone, not to its width alone. A long shallow zone
 * takes one rank, a tall narrow one stacks its values and sizes them to the height, and a page
 * sheds its secondary rows before it shrinks its numerals. Nothing is ever scaled down.*
 *
 * Shape is **a pair of bands, width and height**, not one ratio. A single ratio cannot express the
 * drawings: the catalogue's `tall narrow` 274 × 300 is 0.91 and its `tall` 360 × 470 is 0.77, so
 * any one threshold collapses them into the same shape — and lap times shows two fields at one and
 * six at the other. Width picks the column set and the rank width; height picks the row count and
 * whether the lead values are promoted.
 *
 * The four archetypes below are the four the catalogue draws, and they are the test fixtures. A
 * fifth band, `short`, exists because the pit wall hands a module 607 × 158 and 1007 × 211, which
 * the canvas does not draw and the build really produces.
 */
import type { Rect } from '../design/geometry.ts';

export type WidthBand = 'narrow' | 'medium' | 'wide';
export type HeightBand = 'short' | 'medium' | 'tall';

export interface Shape {
  width: WidthBand;
  height: HeightBand;
}

/**
 * Where the bands begin.
 *
 * Chosen so that the four shapes the catalogue draws land on four distinct pairs, and so that the
 * boxes the build actually produces land somewhere sensible. They are thresholds rather than
 * measurements: a box one pixel either side of one is the same page with one column more or less,
 * which is a decision a person would make the same way.
 */
export const WIDTH_BANDS = { narrow: 0, medium: 320, wide: 520 } as const;
export const HEIGHT_BANDS = { short: 0, medium: 200, tall: 400 } as const;

export const widthBandOf = (width: number): WidthBand => (width >= WIDTH_BANDS.wide ? 'wide' : width >= WIDTH_BANDS.medium ? 'medium' : 'narrow');

export const heightBandOf = (height: number): HeightBand => (height >= HEIGHT_BANDS.tall ? 'tall' : height >= HEIGHT_BANDS.medium ? 'medium' : 'short');

/** The shape of a box. */
export const shapeOf = (box: { width: number; height: number }): Shape => ({ width: widthBandOf(box.width), height: heightBandOf(box.height) });

/**
 * The four shapes the catalogue draws, by the sizes it draws them at. `ZoneCatalogue.dc.html`
 * labels each drawing with its size, and these are those labels.
 */
export const SHAPE_ARCHETYPES = {
  /** `wide · 600 by 280`. One rank. The fullest form of a page. */
  wide: { width: 600, height: 280 },
  /** `grid · 430 by 300`. Two ranks; the least important column goes. */
  grid: { width: 430, height: 300 },
  /** `tall narrow · 274 by 300`. One column, stacked and centred; only the reading it exists for. */
  tallNarrow: { width: 274, height: 300 },
  /** `tall · 360 by 470`. Stacked and expanded: the sectors come back, a list grows rows. */
  tall: { width: 360, height: 470 },
} as const;

export type Archetype = keyof typeof SHAPE_ARCHETYPES;

export const ARCHETYPES: readonly Archetype[] = ['wide', 'grid', 'tallNarrow', 'tall'];

/**
 * How many values a rank may hold side by side at this shape.
 *
 * A narrow zone is one column whatever its height: that is what "stacks its values" means. The
 * question a page asks is how many of its fields go on a line, and the answer is a property of the
 * width alone.
 */
export function columnsAt(shape: Shape): number {
  if (shape.width === 'narrow') return 1;
  if (shape.width === 'medium') return 2;
  return 3;
}

// What a short box does is not a second rule here. It is `archetypeOf` in `modules/shedding.ts`,
// which hands a short box the drawing of the next shape down -- `grid` to a wide one, `tall narrow`
// to the rest -- because what a short box has is room for less, and the declaration is where "less"
// is written down page by page. A predicate here saying a short box keeps one rank said the same
// thing a second time and in other words, and nothing read it.

/** Whether the lead value of a page is promoted a size, which a tall box can afford. */
export const promotesLead = (shape: Shape): boolean => shape.height === 'tall';

/** `wide 600x280`-style, for a test name or a failure message. */
export const describeShape = (shape: Shape): string => `${shape.width}/${shape.height}`;

/** The shape of a rect, for a caller that already has one. */
export const shapeOfRect = (rect: Rect): Shape => shapeOf(rect);
