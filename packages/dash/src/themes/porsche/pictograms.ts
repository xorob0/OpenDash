/**
 * The car's pictograms, drawn from rectangles and ellipses in the canvas's own geometry, since an
 * image carries no tint and the repository holds no artwork for them: the headlight of the top
 * strip, and down the telltale column the lights, the warning, the hazard triangle and the tyre.
 *
 * Each is drawn in one ink, the panel grey until it is lit and its colour while it is, by binding
 * every stroke to the same expression, so that a pictogram is lit or dim as a whole. Every
 * coordinate below is the canvas's, in the pictogram's own viewBox, scaled to the box it is drawn in.
 */
import type { Border, EllipseItem, Hex, Item, Rect, RectangleItem } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { formula, withMoreBindings, type Expr } from '../../bind.ts';
import { roundRect } from '../../design/geometry.ts';
import { TRANSPARENT } from '../../tokens.ts';
import { carColour } from './register.ts';

const { iff, str } = ncalc;

/** One lit state: when it is lit, and in what. Absent, the pictogram is drawn dim and bound to nothing. */
export interface Lit {
  on: Expr;
  colour: Hex;
}

/** What every stroke of a pictogram is drawn in: the panel grey, or its colour while it is lit. */
const inkOf = (lit: Lit | undefined): Expr | undefined => (lit ? iff(lit.on, str(lit.colour), str(carColour('panel'))) : undefined);

/** A viewBox scaled into a box: a point of the drawing to a point of the face. */
interface Scale {
  at(x: number, y: number, w: number, h: number): Rect;
  stroke: number;
}

const scaleInto = (box: Rect, viewWidth: number, viewHeight: number, stroke: number): Scale => {
  const k = Math.min(box.width / viewWidth, box.height / viewHeight);
  const left = box.left + (box.width - viewWidth * k) / 2;
  const top = box.top + (box.height - viewHeight * k) / 2;
  return { at: (x, y, w, h) => roundRect({ left: left + x * k, top: top + y * k, width: Math.max(1, w * k), height: Math.max(1, h * k) }), stroke: Math.max(1, Math.round(stroke * k)) };
};

/** A filled bar: a straight stroke of the drawing, optionally turned about its centre. */
function bar(name: string, r: Rect, lit: Lit | undefined, rotation = 0): RectangleItem {
  return withMoreBindings({ kind: 'rect', name, rect: r, backgroundColor: carColour('panel'), ...(rotation ? { rotation } : {}) } satisfies RectangleItem, { BackgroundColor: inkOf(lit) });
}

/** An outlined ellipse, its stroke centred on the edge as SimHub draws one. */
function ring(name: string, r: Rect, stroke: number, lit: Lit | undefined): EllipseItem {
  return withMoreBindings({ kind: 'ellipse', name, rect: r, fillColor: TRANSPARENT, strokeColor: carColour('panel'), strokeThickness: stroke } satisfies EllipseItem, { EllipseColor: inkOf(lit) });
}

/** A rounded outline with some sides left open, which is how the headlight's half disc is drawn. */
function outline(name: string, r: Rect, sides: { top: number; bottom: number; left: number; right: number }, radius: Border['radius'], lit: Lit | undefined): RectangleItem {
  const ink = inkOf(lit);
  return {
    kind: 'rect',
    name,
    rect: r,
    backgroundColor: TRANSPARENT,
    border: { color: carColour('panel'), ...sides, radius, ...(ink ? { colorBinding: formula(ink) } : {}) },
  };
}

/** The headlight of the strip: a half disc opening right, and three beams. viewBox 50 x 32, stroke 2.6. */
export function headlight(name: string, box: Rect, lit?: Lit): Item[] {
  const s = scaleInto(box, 50, 32, 2.6);
  const half = s.at(7, 3, 13, 26);
  return [
    outline(`${name}.lamp`, half, { top: s.stroke, bottom: s.stroke, left: s.stroke, right: 0 }, { topLeft: half.width, topRight: 0, bottomLeft: half.width, bottomRight: 0 }, lit),
    ...[8, 16, 24].map((y, i) => bar(`${name}.beam${i + 1}`, s.at(24, y - 1.3, 22, 2.6), lit)),
  ];
}

/** The lights pictogram at the head of the column: a lens between two bars. viewBox 30 x 26, stroke 2. */
export function lights(name: string, box: Rect, lit?: Lit): Item[] {
  const s = scaleInto(box, 30, 26, 2);
  return [ring(`${name}.lens`, s.at(3, 7, 24, 12), s.stroke, lit), bar(`${name}.top`, s.at(6, 4, 18, 2), lit), bar(`${name}.foot`, s.at(6, 20, 18, 2), lit)];
}

/** The warning: a ring with an exclamation mark in it. viewBox 28 x 28, stroke 2. */
export function warning(name: string, box: Rect, lit?: Lit): Item[] {
  const s = scaleInto(box, 28, 28, 2);
  return [ring(`${name}.ring`, s.at(3, 3, 22, 22), s.stroke, lit), bar(`${name}.stem`, s.at(13, 8, 2, 7), lit), bar(`${name}.dot`, s.at(13, 18, 2, 2), lit)];
}

/** The hazard: a triangle with an exclamation mark in it, its two sides turned sixty degrees. viewBox 34 x 30, stroke 2. */
export function hazard(name: string, box: Rect, lit?: Lit): Item[] {
  const s = scaleInto(box, 34, 30, 2);
  // Each side runs from the apex at (17, 2) to a foot at (2, 28) or (32, 28): thirty units long,
  // centred half way along it, and turned about that centre.
  const side = (cx: number): Rect => s.at(cx - 15, 15 - 1, 30, 2);
  return [
    bar(`${name}.left`, side(9.5), lit, -60),
    bar(`${name}.right`, side(24.5), lit, 60),
    bar(`${name}.base`, s.at(2, 27, 30, 2), lit),
    bar(`${name}.stem`, s.at(16, 11, 2, 8), lit),
    bar(`${name}.dot`, s.at(16, 22, 2, 2), lit),
  ];
}

/** The tyre: two rings, one inside the other. viewBox 28 x 28, stroke 2. */
export function tyre(name: string, box: Rect, lit?: Lit): Item[] {
  const s = scaleInto(box, 28, 28, 2);
  return [ring(`${name}.outer`, s.at(3, 3, 22, 22), s.stroke, lit), ring(`${name}.inner`, s.at(9, 9, 10, 10), s.stroke, lit)];
}
