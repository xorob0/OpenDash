/**
 * The car seen from above: one drawing read twice. The pit order colours its four corner blocks by
 * the wheels the crew is down to change, and the damage page colours its panels by what is hurt.
 * iRacing reports no damage at all, so that second reading is a colour binding waiting for a caller
 * rather than anything drawn today; the drawing is the same either way, which is the whole reason
 * it is one file and not two.
 *
 * The canvas draws it at 142, 129, 106 and 79 px wide and every one of those is the same picture in
 * the same 86 by 150 viewBox, so the numbers below are the drawing and the size is rule 18's
 * business: a third of the frame's width, capped by what the frame's height allows at the picture's
 * own ratio, and dropped rather than shrunk past the point where four corner blocks stop reading as
 * wheels.
 *
 * The bumpers are rectangles. The canvas draws the nose and the tail as quadratic paths, and the
 * format has no path item: SimHub's drawables are a rectangle, an ellipse, an image and text.
 * Of the two ways round that -- one picture of the whole body, or the paths reduced to the
 * rectangles they are mostly made of -- this file takes the rectangles, because an image carries
 * its colours in its pixels and cannot be tinted (`design/assets.ts` records why), and a per-panel
 * colour is exactly what the damage reading needs. What a rectangle has in place of the taper is a
 * corner radius.
 */
import type { EllipseItem, Hex, Item, Rect } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { WHEEL_CHANGE_TICK, assetBox, imageOf } from '../design/assets.ts';
import { rect, type Size } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { TRANSPARENT, ds } from '../tokens.ts';
import { CORNERS, type Corner } from './values.ts';

/** The canvas's own viewBox. Every car on every artboard is this box scaled. */
const VIEW: Size = { width: 86, height: 150 };

/** The picture's ratio, which the box it is cut to keeps. */
export const CAR_ASPECT = VIEW.width / VIEW.height;

/**
 * Rule 18's cap for this drawing: a car is at most a third of the frame's width however tall the
 * box is. The catalogue draws it a shade under that ceiling at every shape -- 129 of a 406 px
 * frame, 106 of 336, 79 of 250 -- and never above it.
 */
export const CAR_SHARE = 1 / 3;

/**
 * Below this the corner blocks are six pixels across and the cockpit is a dot, which is a smudge
 * where a car was rather than a smaller car. The catalogue's own narrowest drawing is 79.
 */
export const CAR_MIN_WIDTH = 44;

/** A rectangle of the drawing, in the canvas's units; `radius` is what it has where the path curved. */
interface Part {
  left: number;
  top: number;
  width: number;
  height: number;
  radius?: number;
}

/** The tub, which is the straight run of the body path between the two tapers. */
const TUB: Part = { left: 18, top: 18, width: 50, height: 110, radius: 6 };
/** The front and rear bumpers, at the bounding boxes of the paths the canvas curves them along. */
const NOSE: Part = { left: 28, top: 3, width: 30, height: 17, radius: 3 };
const TAIL: Part = { left: 22, top: 120, width: 42, height: 20, radius: 3 };
const LEFT_SILL: Part = { left: 14, top: 26, width: 6, height: 96 };
const RIGHT_SILL: Part = { left: 66, top: 26, width: 6, height: 96 };
const WING: Part = { left: 12, top: 136, width: 62, height: 7 };
const COCKPIT: Part = { left: 30, top: 52, width: 26, height: 40 };

/** The four wheels, which is where the car is read from on the pit page. */
const BLOCKS: Record<Corner, Part> = {
  FrontLeft: { left: 0, top: 26, width: 14, height: 26, radius: 3 },
  FrontRight: { left: 72, top: 26, width: 14, height: 26, radius: 3 },
  RearLeft: { left: 0, top: 100, width: 14, height: 26, radius: 3 },
  RearRight: { left: 72, top: 100, width: 14, height: 26, radius: 3 },
};

/** The body's outline and the cockpit's, both 1.5 in the canvas's units. */
const STROKE = 1.5;

/** The panels a sim's damage lands on, which is what the canvas's damaged drawing paints. */
export type CarPanel = 'nose' | 'leftSill' | 'rightSill' | 'tail';

/** What a part is painted: the colour it rests at, and the expression that overrides it live. */
export interface CarPaint {
  /** The colour at rest, which is also what the editor shows. */
  color?: Hex;
  /** `BackgroundColor`, for a part whose colour is a reading rather than a constant. */
  bind?: Expr;
}

export interface CarTopViewOptions {
  /** The block at a corner, and the tick over it when the crew is down to change that wheel. */
  wheel(corner: Corner): CarPaint & { tick?: Expr };
  /** The panels the page has a state for. The rest stay the structure colour. */
  panels?: Partial<Record<CarPanel, CarPaint>>;
}

/**
 * A part on the box the drawing was cut to, both edges snapped, so that the rightmost block ends
 * exactly on the box's right edge rather than a rounding either side of it.
 */
function partRect(box: Rect, part: Part): Rect {
  const x = (u: number): number => Math.round(box.left + (u * box.width) / VIEW.width);
  const y = (u: number): number => Math.round(box.top + (u * box.height) / VIEW.height);
  const left = x(part.left);
  const top = y(part.top);
  return rect(left, top, x(part.left + part.width) - left, y(part.top + part.height) - top);
}

const radiusOf = (part: Part, scale: number): { radius: number } | undefined => (part.radius === undefined ? undefined : { radius: part.radius * scale });

/**
 * The size a car takes in a frame: a third of its width, cut to the picture's ratio by its height,
 * and no wider than `spare` -- what the rank beside the drawing does not need, which is what a
 * drawing is cut from. Nothing at all where that leaves less than a car.
 */
export function carTopViewSize(frame: Size, spare: number = frame.width): Size {
  const width = Math.floor(Math.min(CAR_SHARE * frame.width, CAR_ASPECT * frame.height, spare));
  if (width < CAR_MIN_WIDTH) return { width: 0, height: 0 };
  return { width, height: Math.floor(width / CAR_ASPECT) };
}

/** The car drawn to fill `box`, which a caller has already cut with {@link carTopViewSize}. */
export function carTopView(name: string, box: Rect, opts: CarTopViewOptions): Item[] {
  const scale = box.width / VIEW.width;
  const stroke = Math.max(1, Math.round(STROKE * scale));
  const structure = ds.purpose.illustration.dim;
  const painted = (id: string, part: Part, paint: CarPaint | undefined): Item => ({
    ...band(`${name}.${id}`, partRect(box, part), paint?.color ?? structure, { ...radiusOf(part, scale) }),
    ...withBindings({ BackgroundColor: paint?.bind }),
  });
  const cockpit: EllipseItem = {
    kind: 'ellipse',
    name: `${name}.cockpit`,
    rect: partRect(box, COCKPIT),
    fillColor: TRANSPARENT,
    strokeColor: ds.purpose.illustration.outline,
    strokeThickness: stroke,
    backgroundColor: TRANSPARENT,
  };
  const items: Item[] = [
    band(`${name}.body`, partRect(box, TUB), ds.purpose.block.well, { border: { color: structure, width: stroke }, ...radiusOf(TUB, scale) }),
    painted('leftSill', LEFT_SILL, opts.panels?.leftSill),
    painted('rightSill', RIGHT_SILL, opts.panels?.rightSill),
    painted('nose', NOSE, opts.panels?.nose),
    painted('tail', TAIL, opts.panels?.tail),
    painted('wing', WING, undefined),
    cockpit,
  ];
  for (const corner of CORNERS) {
    const part = BLOCKS[corner];
    const block = partRect(box, part);
    const wheel = opts.wheel(corner);
    items.push({
      ...band(`${name}.${corner}`, block, wheel.color ?? ds.purpose.illustration.outline, { ...radiusOf(part, scale) }),
      ...withBindings({ BackgroundColor: wheel.bind }),
    });
    // The tick is the one part of the picture no rect can draw and no font can measure, so it is
    // the badge image the tyre corners already ship, cut square inside the block it marks.
    if (wheel.tick !== undefined) {
      items.push({
        kind: 'image',
        name: `${name}.${corner}.tick`,
        image: WHEEL_CHANGE_TICK.name,
        rect: assetBox(block, imageOf(WHEEL_CHANGE_TICK)),
        ...withBindings({ Visible: wheel.tick }),
      });
    }
  }
  return items;
}
