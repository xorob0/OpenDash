/**
 * The tyre as a drawing: a body, three tread columns filling from the bottom, the grooves cut
 * across them and the badge that marks a wheel down for a change.
 *
 * The canvas draws the same picture at eight sizes -- 34 by 62 on the components sheet, 37 by 58 on
 * the pit wall, 42 by 66, 72 by 113, 74 by 117 and 84 by 132 elsewhere -- and every one of them is
 * that picture scaled, so the proportions below are the drawing and the box is rule 18's business.
 * It lives in its own file for the reason `carTopView.ts` does: a drawing answers any rectangle at
 * all, which is a different question from the one the cell around it answers, and it is drawn by a
 * wheel cell today and by whatever asks for a tyre tomorrow.
 *
 * Two things the format cannot do decide how it is built, both recorded in
 * `docs/research/simhub-dash-format.md`. A gauge has no corner radius that clips its fill, so the
 * rounded ends of a tread column are a rounded rect drawn behind the gauge, whose own background is
 * transparent; a full column therefore has the canvas's rounded head and a square foot, which is as
 * near as rectangles get. And no item draws a tick, so the mark on the badge is the one picture
 * this package ships rather than a glyph or a path.
 */
import type { Item, Rect } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { WHEEL_CHANGE_TICK, assetBox, imageOf } from '../design/assets.ts';
import { rect, right, roundRect, type Size } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { TRANSPARENT, ds } from '../tokens.ts';
import { barGauge } from './gauge.ts';
import { tyreChangeScheduled, tyreWear, type Corner } from './values.ts';

/** The drawing, as fractions of its own box; the radii the canvas writes in pixels are noted as such. */
export const GLYPH = {
  /** 72 by 113 on the catalogue's `wide` sheet, and the same ratio at every other size. */
  aspect: 72 / 113,
  /** The body's outline, and the inset the change badge sits at. */
  stroke: 0.0385,
  radius: 0.2,
  /** A tread column and the gap between two of them, across the box. */
  column: 0.2033,
  columnGap: 0.065,
  /**
   * The shoulders are round and the crown is not, which is the tyre seen end-on: the outer columns
   * are rounded at 0.09 of the box at every size the canvas draws, and the middle one carries the
   * 1.2 px the canvas gives every square-ish corner rather than a share of the box.
   */
  columnRadius: 0.09,
  middleRadius: 1.2,
  /** The tread band down the box: where it starts and how tall it is. */
  treadTop: 0.07,
  treadHeight: 0.86,
  /** A groove cut across the tread, and the badge that marks a wheel down for a change. */
  groove: 0.0346,
  badge: 0.34,
  badgeRadius: 1.5,
} as const;

/** Below this the three columns stop reading as tread and the cell keeps its numbers instead. */
export const GLYPH_MIN_WIDTH = 20;

/** The share of a cell the drawing takes, which is what sizes it at every shape but the widest. */
export const GLYPH_SHARE = 0.35;

/** Three grooves where the canvas has the height for them, two where it does not. */
const GROOVES_FROM = 80;

/**
 * The drawing's box: its share of the cell, cut to the picture's ratio by the cell's height and no
 * wider than `spare` -- what the readings beside it do not need. Nothing at all below the width
 * three columns stop being three columns at, because a tyre is not drawn smaller, it is not drawn.
 */
export function tyreGlyphSize(frame: Size, spare: number = frame.width): Size {
  const width = Math.floor(Math.min(GLYPH_SHARE * frame.width, GLYPH.aspect * frame.height, spare));
  if (width < GLYPH_MIN_WIDTH) return { width: 0, height: 0 };
  return { width, height: Math.floor(width / GLYPH.aspect) };
}

export interface TyreGlyphOptions {
  /**
   * What a tread column is filled in. The tyre's colour is a reading of its own -- the temperature
   * band, with the caution amber of a worn tread standing in for the nominal -- and the numerals
   * beside the drawing take the same one, so it is passed in rather than decided twice.
   */
  fillBind: Expr;
  /** Design-time tread left, so the editor shows four wheels rather than the same one four times. */
  sample: number;
  /** The side of the drawing the change badge sits in, which is the cell's outer one. */
  badge: 'left' | 'right';
}

/**
 * The three tread columns, each snapped to the pixel grid it will be drawn on.
 *
 * Snapped here rather than by each item for itself, so that the grooves crossing them can be
 * measured against where the columns really land: a groove cut from the unrounded band was a pixel
 * short of the outer columns at half the sizes the catalogue draws.
 */
const treadColumns = (box: Rect): Rect[] => {
  const width = GLYPH.column * box.width;
  const gap = GLYPH.columnGap * box.width;
  const left = box.left + (box.width - 3 * width - 2 * gap) / 2;
  const top = box.top + GLYPH.treadTop * box.height;
  const height = GLYPH.treadHeight * box.height;
  return [0, 1, 2].map((i) => roundRect(rect(left + i * (width + gap), top, width, height)));
};

/** The tyre drawn to fill `box`, which a caller has already cut with {@link tyreGlyphSize}. */
export function tyreGlyph(name: string, box: Rect, corner: Corner, opts: TyreGlyphOptions): Item[] {
  const stroke = Math.max(1, Math.round(GLYPH.stroke * box.width));
  const items: Item[] = [
    band(`${name}.body`, box, ds.purpose.block.well, {
      border: { color: ds.purpose.illustration.dim, width: stroke },
      radius: GLYPH.radius * box.width,
    }),
  ];
  const columns = treadColumns(box);
  // The canvas fills each column from its own third of the tyre, which `tyreWearMin`'s sections are
  // there for. Nothing reads them yet: the twelve `*wear[LMR]` properties are in no committed trace,
  // and a trace is recorded on the VM rather than written by hand, so the columns share the corner's
  // one figure until `bun run record` has been past them.
  const wear = tyreWear(corner);
  for (const [i, column] of columns.entries()) {
    items.push(
      band(`${name}.track${i + 1}`, column, ds.color.surface.raised, { radius: i === 1 ? GLYPH.middleRadius : GLYPH.columnRadius * box.width }),
      barGauge(`${name}.tread${i + 1}`, column, wear, {
        track: TRANSPARENT,
        fill: ds.color.text.primary,
        fillBind: opts.fillBind,
        max: 100,
        value: opts.sample,
      }),
    );
  }
  // The grooves are the division lines of the tread band, which is why there are three of them in a
  // tall drawing and two in a short one: a groove every quarter of a 113 px tyre is a groove every
  // 28 px, and the same every quarter of a 58 px one is a stripe.
  const grooves = box.height >= GROOVES_FROM ? 3 : 2;
  const thickness = Math.max(1, Math.round(GLYPH.groove * box.width));
  const tread = columns[0]!;
  const span = right(columns[2]!) - tread.left;
  for (let i = 1; i <= grooves; i++) {
    const y = Math.round(tread.top + (i * tread.height) / (grooves + 1) - thickness / 2);
    items.push(band(`${name}.groove${i}`, rect(tread.left, y, span, thickness), ds.purpose.block.well));
  }
  return [...items, ...changeBadge(name, box, corner, opts.badge)];
}

/**
 * The badge at the drawing's outer top corner, marking a wheel the pit box is set to change.
 *
 * The block is a rect so that its colour stays a token; the tick inside it is the part no rect can
 * draw and no font can measure, so it is the one picture this package ships.
 */
function changeBadge(name: string, box: Rect, corner: Corner, outer: 'left' | 'right'): Item[] {
  const size = GLYPH.badge * box.width;
  const inset = GLYPH.stroke * box.width;
  const x = outer === 'left' ? box.left + inset : box.left + box.width - inset - size;
  const badge = rect(x, box.top + inset, size, size);
  const visible = tyreChangeScheduled(corner);
  return [
    band(`${name}.change`, badge, ds.color.text.secondary, { visibleBind: visible, radius: GLYPH.badgeRadius }),
    {
      kind: 'image',
      name: `${name}.change.tick`,
      image: WHEEL_CHANGE_TICK.name,
      rect: assetBox(badge, imageOf(WHEEL_CHANGE_TICK)),
      ...withBindings({ Visible: visible }),
    },
  ];
}
