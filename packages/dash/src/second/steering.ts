/**
 * The steering dial: a three-spoke sport wheel that turns as the driver's does, with the white
 * stripe across the top of the rim that a racing wheel carries to say where straight ahead is.
 *
 * The catalogue draws an open arc turned by the wheel angle with the mark fixed at top dead centre.
 * The author asked for a real wheel instead, keeping the white part at the top, and gave the drawing
 * to follow: a thick rim, two side spokes lifted a little above level and a broader one down, each
 * with a slot along it, and a round boss with a hole in it (#1057, on the pull request). The build
 * departs from the canvas here and docs/design/zones.md says so.
 *
 * It turns because `Rotation` binds. `DrawableItem.Rotation` is a plain `double` with change
 * notification, `ApplyBindings` sets any `double` property a binding names, and the renderer's
 * `ControlHost` turns a non-zero value into a `RotateTransform`, live, as
 * docs/research/simhub-dash-format.md records. An earlier cut of this file took the opposite for
 * granted and moved a mark round a ring that stood still. A rotation turns an item about its own
 * centre, so every part that is not a circle is placed by {@link onWheel}: its centre is carried
 * round the hub by `sin` and `cos`, as the mark's was before, and it is turned by the wheel's angle
 * on top of its own. A spoke tapers, and the format has no polygon, so {@link spoke} builds the taper
 * from three bars. The rim, the boss and its hole are circles on the hub, which a turn leaves as
 * they are.
 */
import type { Hex, Item, RectangleItem } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect, type Rect } from '../design/geometry.ts';
import { boxSlack, textBox } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { ring } from '../elements/ring.ts';
import { ds } from '../tokens.ts';
import { hasSteering, STEERING_RANGE, steering } from './values.ts';

const { add, max, min, mul, num, real, sub } = ncalc;

/**
 * The wheel at the catalogue's 96 px, in the reference drawing's proportions. The rim is 8 px thick
 * on a circle of radius 44, so its outer edge is the box. The side spokes are lifted 6 degrees above
 * level and taper from 11 px at the boss to 6 at the rim; the spoke down tapers from 13 to 7. Each
 * carries a slot 2 px wide along its outer half. The boss has a radius of 11 with a hole of 4. The white stripe is 6 px along the rim and stands 2 px proud of it either side, and the word
 * sits six pixels under the box. Every one of them is a ratio of the box, which is how the dial is
 * re-cut for a shorter column.
 */
export const STEERING_DIAL = {
  size: 96,
  radius: 44,
  stroke: 8,
  spokeLift: 6,
  side: { hub: 11, rim: 6 },
  down: { hub: 13, rim: 7 },
  slot: 2,
  boss: 11,
  hole: 4,
  mark: 6,
  labelGap: 6,
} as const;

/** Below this box the slots are a pixel or two of noise, and the wheel is drawn without them. */
const SLOTS_FROM = 64;

/**
 * NCalc's trigonometry, which `ncalc.ts` carries no helper for. The angle is in radians, which is
 * what `sin` and `cos` read and what iRacing publishes: a conversion either way would be a constant
 * in every formula and a chance to get the direction of it wrong.
 */
const sin = (a: Expr): Expr => `sin(${a})`;
const cos = (a: Expr): Expr => `cos(${a})`;

/** Radians to the degrees `Rotation` takes, clockwise in both. */
const degrees = (a: Expr): Expr => mul(a, num(180 / Math.PI));

/** A point on the screen relative to the hub, in pixels, y down. */
interface Offset {
  x: number;
  y: number;
}

/**
 * A bar of `size` centred at `at` from the hub on a wheel that is straight, standing up and turned
 * `turn` degrees clockwise about its own centre, then carried round the hub and turned further with
 * the wheel by `angle` radians clockwise. It is drawn where a zero angle puts it, which is what Dash
 * Studio shows against a sim that is not running, and its formulas start from the corner the package
 * writes, so a straight wheel binds exactly where it is drawn.
 *
 * Clockwise on the screen, with y down, takes (x, y) to (x cos a - y sin a, x sin a + y cos a).
 * Clockwise is the driver's, and it is the opposite of iRacing's: `SteeringWheelAngle` is positive
 * to the left, and `steering()` turns it round before it gets here (#1057).
 */
function onWheel(name: string, hub: { cx: number; cy: number }, at: Offset, turn: number, size: { width: number; height: number }, color: Hex, angle: Expr): RectangleItem {
  // Placed to the hundredth rather than to the pixel `band` rounds to. A part that turns is drawn
  // antialiased whatever its corner, so the whole pixel buys nothing, and rounding each part on its
  // own sets a spoke's slot and its edges off its axis by up to a pixel in different directions.
  const hundredth = (n: number): number => Math.round(n * 100) / 100;
  const r = rect(hundredth(hub.cx + at.x - size.width / 2), hundredth(hub.cy + at.y - size.height / 2), hundredth(size.width), hundredth(size.height));
  const own = hundredth(turn);
  const part: RectangleItem = { ...band(name, r, color, own === 0 ? {} : { rotation: own }), rect: r };
  const x = part.rect.left + part.rect.width / 2 - hub.cx;
  const y = part.rect.top + part.rect.height / 2 - hub.cy;
  return withMoreBindings(part, {
    Left: add(num(part.rect.left - x), sub(mul(num(x), cos(angle)), mul(num(y), sin(angle)))),
    Top: add(num(part.rect.top - y), add(mul(num(x), sin(angle)), mul(num(y), cos(angle)))),
    Rotation: own === 0 ? degrees(angle) : add(degrees(angle), num(own)),
  });
}

/** The point `along` the direction `bearing` degrees clockwise from up, and `across` to its right. */
const polar = (bearing: number, along: number, across = 0): Offset => {
  const b = (bearing * Math.PI) / 180;
  return { x: along * Math.sin(b) + across * Math.cos(b), y: -along * Math.cos(b) + across * Math.sin(b) };
};

/**
 * A spoke from the hub to the rim's centre line, `widths.hub` wide at one end and `widths.rim` at the
 * other. The format has no polygon, so the taper is three bars: one down the middle, a little narrower
 * than the rim end, and one along either edge, laid on the line from the hub's corner to the rim's.
 *
 * Each bar is placed to the hundredth of a pixel and drawn antialiased, so three bars that only just
 * met would still show a faint seam where their soft edges cross. So the edge bars are two pixels
 * thicker than the gap they fill, the middle bar stays inside the outline, and every bar runs `bury`
 * past both of its
 * ends, under the boss at one and the rim at the other, where nothing it draws is seen.
 */
function spoke(name: string, hub: { cx: number; cy: number }, bearing: number, length: number, widths: { hub: number; rim: number }, bury: number, color: Hex, angle: Expr): RectangleItem[] {
  // Narrower than the spoke anywhere along it, so the outline is always the edge bars' and never the
  // middle bar's straight side showing where an edge bar was rounded inward.
  const core = Math.max(1, widths.rim - 2);
  const middle = onWheel(`${name}`, hub, polar(bearing, length / 2), bearing, { width: core, height: length + 2 * bury }, color, angle);
  const thickness = (widths.hub - core) / 2 + 2;
  const edges = ([-1, 1] as const).map((side) => {
    const from = polar(bearing, 0, (side * widths.hub) / 2);
    const to = polar(bearing, length, (side * widths.rim) / 2);
    const d = { x: to.x - from.x, y: to.y - from.y };
    const run = Math.hypot(d.x, d.y);
    // The unit across the edge, turned to point into the spoke rather than out of it.
    const across = { x: -d.y / run, y: d.x / run };
    const inside = polar(bearing, 0, -side);
    const inward = across.x * inside.x + across.y * inside.y > 0 ? across : { x: -across.x, y: -across.y };
    const centre = { x: (from.x + to.x) / 2 + (inward.x * thickness) / 2, y: (from.y + to.y) / 2 + (inward.y * thickness) / 2 };
    const turn = (Math.atan2(d.x, -d.y) * 180) / Math.PI;
    return onWheel(`${name}.${side < 0 ? 'left' : 'right'}`, hub, centre, turn, { width: thickness, height: run + 2 * bury }, color, angle);
  });
  return [middle, ...edges];
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
 *
 * The whole dial is one group that hides where the sim publishes no angle, which is every sim but
 * iRacing until Assetto Corsa's is measured (#567).
 */
export function steeringDial(name: string, frame: Rect, labelSize: number): Item[] {
  // The box's slack over the advance: `Steer` is a whole 36 px at 15, and a box cut to the exact
  // advance loses the last glyph's final column to WPF.
  const labelWidth = Math.ceil(measureText('BarlowMedium', 'Steer', labelSize)) + boxSlack(labelSize);
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
  const D = STEERING_DIAL;
  const px = (n: number): number => Math.max(1, Math.round(n * scale));
  const stroke = px(D.stroke);
  const radius = Math.round(D.radius * scale);
  const top = frame.top + Math.round((frame.height - side - labelRow) / 2);
  const cx = Math.round(frame.left + frame.width / 2);
  const cy = top + side / 2;
  const hub = { cx, cy };
  const clamped = max(real(-STEERING_RANGE), min(real(STEERING_RANGE), steering()));
  const wheel = ds.color.text.label;
  // A slot is a cut through the spoke, so it is drawn in the darkest ink there is rather than in the
  // ground, which a module does not know: band D draws on one surface and every other zone on another.
  const cut = ds.color.text.dim;
  const width = (w: { hub: number; rim: number }): { hub: number; rim: number } => ({ hub: px(w.hub), rim: px(w.rim) });
  const spokes = [
    { id: 'Left', bearing: -(90 - D.spokeLift), widths: width(D.side) },
    { id: 'Right', bearing: 90 - D.spokeLift, widths: width(D.side) },
    { id: 'Down', bearing: 180, widths: width(D.down) },
  ];
  const slots = side >= SLOTS_FROM;
  // The stripe always stands proud of the rim on both sides, however thin the rim is drawn.
  const mark = { width: even(D.mark), height: 2 * Math.ceil((stroke + 2 * px(2)) / 2) };
  // One group, hidden where the sim publishes no wheel angle (#567): a wheel held straight through every
  // corner is a reading of its own, and a wrong one. A group whose Visible is false leaves its children's
  // bindings unevaluated, so the turning parts cost nothing there either; see `withSessionGate`.
  const wheelItems: Item[] = [
    // Each spoke runs from the hub to the rim's centre line, so the boss and the rim cover its ends.
    ...spokes.flatMap((s) => spoke(`${name}.spoke${s.id}`, hub, s.bearing, radius, s.widths, px(2), wheel, clamped)),
    ...(slots ? spokes.map((s) => onWheel(`${name}.slot${s.id}`, hub, polar(s.bearing, radius * 0.55), s.bearing, { width: px(D.slot), height: Math.round(radius * 0.36) }, cut, clamped)) : []),
    // The stroke lies inside the ellipse's own rect, so the rim's square is the outer diameter and
    // the stroke is centred on the circle of radius `radius`.
    ring(`${name}.rim`, { cx, cy, r: radius + stroke / 2 }, stroke, wheel),
    // A ring stroked as thick as its radius is a disc.
    ring(`${name}.boss`, { cx, cy, r: px(D.boss) }, px(D.boss), wheel),
    ring(`${name}.hole`, { cx, cy, r: px(D.hole) }, px(D.hole), cut),
    onWheel(`${name}.mark`, hub, { x: 0, y: -radius }, 0, mark, ds.color.text.primary, clamped),
    ...(named ? [label(`${name}.label`, 'Steer', cx - Math.round(labelWidth / 2), top + side + STEERING_DIAL.labelGap - box.top, labelWidth, { size: labelSize })] : []),
  ];
  return [withMoreBindings({ kind: 'layer', name: `${name}.dial`, children: wheelItems }, { Visible: hasSteering() })];
}
