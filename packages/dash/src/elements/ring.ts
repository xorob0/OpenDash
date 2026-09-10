/**
 * ring: a stroked circle with a transparent fill, the flag indicator of a round face. SimHub's
 * EllipseItem is a WPF Ellipse, which draws its whole stroke inside its layout rect (the shape
 * geometry is deflated by half the stroke thickness), not centred on the rect's edge. A ring of
 * thickness t on the rim of a face is therefore the face's bounding square itself with an
 * EllipseThickness of t: the stroke then lies between the radii R - t and R, the band the
 * chequered ring's checks sit in, and an odd thickness needs no half-pixel rect.
 */
import type { EllipseItem, Hex } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { squareOf, type Circle } from '../design/geometry.ts';
import { TRANSPARENT } from '../tokens.ts';

export interface RingOptions {
  visibleBind?: Expr;
  /** EllipseColor binding. */
  colorBind?: Expr;
}

export function ring(name: string, face: Circle, thickness: number, color: Hex, opts: RingOptions = {}): EllipseItem {
  return {
    kind: 'ellipse',
    name,
    rect: squareOf(face),
    fillColor: TRANSPARENT,
    strokeColor: color,
    strokeThickness: thickness,
    backgroundColor: TRANSPARENT,
    ...withBindings({ Visible: opts.visibleBind, EllipseColor: opts.colorBind }),
  };
}
