/**
 * The steering dial: the wheel's rim as a ring, and the mark that says where its top is running
 * round it.
 *
 * The catalogue draws the opposite arrangement, an open arc turned by the wheel angle with the mark
 * fixed at top dead centre, and neither half of it is expressible. SimHub has no arc primitive, and
 * `Rotation` is a number written into the package rather than one of the properties a formula can
 * drive, so a rim that turned would have to be a picture and a picture does not turn either. What a
 * formula can drive is a position: `Left` and `Top` are binding targets and `sin` and `cos` are
 * NCalc's own, so the drawing is inverted and the rim stands still while the mark moves. The angle
 * between the two is what the dial is read by, and it is the same angle either way.
 *
 * The rim is a closed ring rather than the catalogue's open arc for the same reason. The gap at the
 * bottom of that arc is what makes its rotation legible; on a rim that does not turn it would be a
 * fixed cue saying the wheel is straight whatever the mark is doing. Cutting it would in any case
 * mean covering the gap with the ground behind the module, and a module does not know its ground:
 * band D draws on `purpose.block.well` and every other zone on `surface.base`.
 */
import type { Hex, Item, RectangleItem } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { onCircle, type Circle, type Rect } from '../design/geometry.ts';
import { textBox } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { ring } from '../elements/ring.ts';
import { ds } from '../tokens.ts';
import { STEERING_RANGE, steering } from './values.ts';

const { add, max, min, mul, num, sub } = ncalc;

/**
 * The catalogue's dial: a 96 px box holding a rim of radius 44 stroked 3 px, a 6 px mark riding on
 * that rim, and the word under it six pixels down. Every one of them is a ratio of the box, which
 * is how the dial is re-cut for a shorter column.
 */
export const STEERING_DIAL = { size: 96, radius: 44, stroke: 3, mark: 6, labelGap: 6 } as const;

/**
 * NCalc's trigonometry, which `ncalc.ts` carries no helper for. The angle is in radians, which is
 * what `sin` and `cos` read and what iRacing publishes: a conversion either way would be a constant
 * in every formula and a chance to get the direction of it wrong.
 */
const sin = (a: Expr): Expr => `sin(${a})`;
const cos = (a: Expr): Expr => `cos(${a})`;

/**
 * A square mark of `size` riding on `face`, its position bound to `angle` radians clockwise from
 * the top. It is drawn at top dead centre, which is where a zero angle puts it and therefore what
 * Dash Studio shows against a sim that is not running.
 */
function markOnCircle(name: string, face: Circle, size: number, color: Hex, angle: Expr): RectangleItem {
  return withMoreBindings(band(name, onCircle(face, 0, { width: size, height: size }), color), {
    Left: add(num(face.cx - size / 2), mul(num(face.r), sin(angle))),
    Top: sub(num(face.cy - size / 2), mul(num(face.r), cos(angle))),
  });
}

/**
 * The dial filling `frame`: the rim, the mark on it and the word beneath, centred in whatever the
 * column leaves once the word has taken its line.
 *
 * The angle is clamped to the lock the pit wall's own steering trace is drawn at, because a wheel
 * turned further would carry the mark past the top again and read as a smaller angle than it is.
 */
export function steeringDial(name: string, frame: Rect, labelSize: number): Item[] {
  const labelWidth = Math.ceil(measureText('BarlowMedium', 'STEER', labelSize));
  // The word is shed rather than shrunk in a column too narrow for it: a dial is legible without
  // being named and a clipped "STEE" names nothing. Its line box is taller than its size, and a row
  // measured from the size instead puts the last of the word outside the frame.
  const named = labelWidth <= frame.width;
  const box = textBox(0, labelSize);
  const labelRow = named ? box.height + STEERING_DIAL.labelGap : 0;
  // Even, so that the centre of a column of whole pixels is itself whole and the mark's two
  // formulas resolve to the integers the drawing was laid out on.
  const side = 2 * Math.floor(Math.max(0, Math.min(frame.width, frame.height - labelRow)) / 2);
  const scale = side / STEERING_DIAL.size;
  const stroke = Math.max(1, Math.round(STEERING_DIAL.stroke * scale));
  const markSize = 2 * Math.max(1, Math.round((STEERING_DIAL.mark * scale) / 2));
  const radius = Math.round(STEERING_DIAL.radius * scale);
  const top = frame.top + Math.round((frame.height - side - labelRow) / 2);
  const cx = Math.round(frame.left + frame.width / 2);
  const cy = top + side / 2;
  const clamped = min(max(steering(), num(-STEERING_RANGE)), num(STEERING_RANGE));
  return [
    // The stroke lies inside the ellipse's own rect, so the rim's square is the outer diameter and
    // the mark rides half a stroke inside it, on the circle the catalogue gives radius 44.
    ring(`${name}.rim`, { cx, cy, r: radius + stroke / 2 }, stroke, ds.color.text.label),
    markOnCircle(`${name}.mark`, { cx, cy, r: radius }, markSize, ds.color.text.primary, clamped),
    ...(named ? [label(`${name}.label`, 'Steer', cx - Math.round(labelWidth / 2), top + side + STEERING_DIAL.labelGap - box.top, labelWidth, { size: labelSize })] : []),
  ];
}
