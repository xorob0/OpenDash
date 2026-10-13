/**
 * The steering dial: a racing wheel that turns as the driver's does. A thick rim with a padded grip
 * at either hand, three broad spokes, a centre pad, and the white stripe across the top of the rim
 * that a racing wheel carries to say where straight ahead is.
 *
 * The catalogue draws an open arc turned by the wheel angle with the mark fixed at top dead centre.
 * The author asked for a real wheel instead, keeping the white part at the top (#1057, on the pull
 * request), so the build departs from the canvas here and docs/design/zones.md says so.
 *
 * It turns because `Rotation` binds. `DrawableItem.Rotation` is a plain `double` with change
 * notification, `ApplyBindings` sets any `double` property a binding names, and the renderer's
 * `ControlHost` turns a non-zero value into a `RotateTransform`, live, as
 * docs/research/simhub-dash-format.md records. An earlier cut of this file took the opposite for
 * granted and moved a mark round a ring that stood still. A rotation turns an item about its own
 * centre, so each part is placed to suit: the crossbar and the centre pad are centred on the hub and
 * need only their angle, while the lower spoke, the grips and the stripe sit off the hub and have
 * their centres carried round it by `sin` and `cos`, as the mark's was before, and are turned by the
 * same angle so they stay square to the rim. The rim is a circle, which a turn leaves as it is.
 */
import type { Hex, Item, RectangleItem } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect, type Rect } from '../design/geometry.ts';
import { textBox } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { ring } from '../elements/ring.ts';
import { ds } from '../tokens.ts';
import { STEERING_RANGE, steering } from './values.ts';

const { add, max, min, mul, num, real, sub } = ncalc;

/**
 * The wheel at the catalogue's 96 px. The rim is 7 px thick on a circle of radius 41, so it fills the
 * catalogue's ring to its outer edge; a grip is 22 px along the rim and 12 across it; the crossbar is
 * 8 px deep and the lower spoke 10 wide; the centre pad is 30 by 24; the stripe is 8 px along the rim
 * and 14 across it; and the word sits six pixels under the box. Every one of them is a ratio of the
 * box, which is how the dial is re-cut for a shorter column.
 */
export const STEERING_DIAL = {
  size: 96,
  radius: 41,
  stroke: 7,
  grip: { along: 22, across: 12, corner: 4 },
  crossbar: 8,
  spoke: 10,
  pad: { width: 30, height: 24, corner: 7 },
  stripe: { along: 8, across: 14 },
  labelGap: 6,
} as const;

/**
 * NCalc's trigonometry, which `ncalc.ts` carries no helper for. The angle is in radians, which is
 * what `sin` and `cos` read and what iRacing publishes: a conversion either way would be a constant
 * in every formula and a chance to get the direction of it wrong.
 */
const sin = (a: Expr): Expr => `sin(${a})`;
const cos = (a: Expr): Expr => `cos(${a})`;

/** Radians to the degrees `Rotation` takes, clockwise in both. */
const degrees = (a: Expr): Expr => mul(a, num(180 / Math.PI));

/**
 * A part of `size` whose centre sits `distance` from the hub along one of the wheel's two axes on a
 * wheel that is straight, `vertical` (positive up) or `horizontal` (positive right), carried round
 * the hub and turned with it by `angle` radians clockwise. It is drawn where a zero angle puts it,
 * which is what Dash Studio shows against a sim that is not running. A part centred on the hub is a
 * distance of 0 and binds its angle alone.
 *
 * Clockwise is the driver's, and it is the opposite of iRacing's: `SteeringWheelAngle` is positive
 * to the left, and `steering()` turns it round before it gets here (#1057).
 */
function onWheel(
  name: string,
  hub: { cx: number; cy: number },
  at: { axis: 'vertical' | 'horizontal'; distance: number },
  size: { width: number; height: number },
  color: Hex,
  angle: Expr,
  corner?: number,
): RectangleItem {
  const d = at.distance;
  const x = at.axis === 'vertical' ? 0 : d;
  const y = at.axis === 'vertical' ? d : 0;
  const part = band(name, rect(hub.cx + x - size.width / 2, hub.cy - y - size.height / 2, size.width, size.height), color, corner === undefined ? {} : { radius: corner });
  if (d === 0) return withMoreBindings(part, { Rotation: degrees(angle) });
  // The centre at angle a: up the wheel is (sin a, -cos a) on the screen, and right is (cos a, sin a).
  const [dx, dy] = at.axis === 'vertical' ? [sin(angle), cos(angle)] : [cos(angle), sin(angle)];
  return withMoreBindings(part, {
    Left: add(num(hub.cx - size.width / 2), mul(num(d), dx)),
    Top: at.axis === 'vertical' ? sub(num(hub.cy - size.height / 2), mul(num(d), dy)) : add(num(hub.cy - size.height / 2), mul(num(d), dy)),
    Rotation: degrees(angle),
  });
}

/**
 * The dial filling `frame`: the rim, the mark on it and the word beneath, centred in whatever the
 * column leaves once the word has taken its line.
 *
 * The angle is clamped to the lock the pit wall's own steering trace is drawn at, because a wheel
 * turned further would carry the mark past the top again and read as a smaller angle than it is.
 * The bounds are on the left of each clamp and written as doubles: NCalc's `max` and `min` answer in
 * their left operand's type, and the reading on the left was `isnull(angle, 0)`, an Int32 on a frame
 * with no angle (#1046).
 */
export function steeringDial(name: string, frame: Rect, labelSize: number): Item[] {
  // A pixel over the advance: `Steer` is a whole 36 px at 15, and a box cut to the exact advance loses
  // the last glyph's final column to WPF.
  const labelWidth = Math.ceil(measureText('BarlowMedium', 'Steer', labelSize)) + 1;
  // The word is shed rather than shrunk in a column too narrow for it: a dial is legible without
  // being named and a clipped "Stee" names nothing. Its line box is taller than its size, and a row
  // measured from the size instead puts the last of the word outside the frame.
  const named = labelWidth <= frame.width;
  const box = textBox(0, labelSize);
  const labelRow = named ? box.height + STEERING_DIAL.labelGap : 0;
  // Even, so that the centre of a column of whole pixels is itself whole and the formulas resolve to
  // the integers the drawing was laid out on.
  const side = 2 * Math.floor(Math.max(0, Math.min(frame.width, frame.height - labelRow)) / 2);
  const scale = side / STEERING_DIAL.size;
  // Even sides, so that a part centred on a whole pixel has whole edges.
  const even = (n: number): number => 2 * Math.max(1, Math.round((n * scale) / 2));
  const px = (n: number): number => Math.max(1, Math.round(n * scale));
  const D = STEERING_DIAL;
  const stroke = px(D.stroke);
  const radius = Math.round(D.radius * scale);
  const top = frame.top + Math.round((frame.height - side - labelRow) / 2);
  const cx = Math.round(frame.left + frame.width / 2);
  const cy = top + side / 2;
  const hub = { cx, cy };
  const clamped = max(real(-STEERING_RANGE), min(real(STEERING_RANGE), steering()));
  const grip = { width: even(D.grip.across), height: even(D.grip.along) };
  // The stripe always stands proud of the rim on both sides, however thin the rim is drawn.
  const stripe = { width: even(D.stripe.along), height: Math.max(even(D.stripe.across), 2 * Math.ceil((stroke + 2) / 2)) };
  const spokeColour = ds.color.text.label;
  return [
    // The spokes run to the rim's centre line and are drawn first, so the rim covers their ends.
    onWheel(`${name}.spokes`, hub, { axis: 'vertical', distance: 0 }, { width: 2 * radius, height: even(D.crossbar) }, spokeColour, clamped),
    onWheel(`${name}.spoke`, hub, { axis: 'vertical', distance: -radius / 2 }, { width: even(D.spoke), height: radius }, spokeColour, clamped),
    // The stroke lies inside the ellipse's own rect, so the rim's square is the outer diameter and
    // the stroke is centred on the circle of radius `radius`.
    ring(`${name}.rim`, { cx, cy, r: radius + stroke / 2 }, stroke, spokeColour),
    // The grips are a shade lighter than the rim and stand proud of it, as the padding a driver holds.
    onWheel(`${name}.gripLeft`, hub, { axis: 'horizontal', distance: -radius }, grip, ds.color.text.secondary, clamped, px(D.grip.corner)),
    onWheel(`${name}.gripRight`, hub, { axis: 'horizontal', distance: radius }, grip, ds.color.text.secondary, clamped, px(D.grip.corner)),
    onWheel(`${name}.hub`, hub, { axis: 'vertical', distance: 0 }, { width: even(D.pad.width), height: even(D.pad.height) }, ds.color.text.secondary, clamped, px(D.pad.corner)),
    onWheel(`${name}.mark`, hub, { axis: 'vertical', distance: radius }, stripe, ds.color.text.primary, clamped),
    ...(named ? [label(`${name}.label`, 'Steer', cx - Math.round(labelWidth / 2), top + side + STEERING_DIAL.labelGap - box.top, labelWidth, { size: labelSize })] : []),
  ];
}
