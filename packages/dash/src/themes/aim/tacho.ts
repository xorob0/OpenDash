/**
 * The tacho, which is this theme's rev bar (#204): a fine segment per hundred rpm across the well, the
 * lit ones in the ink and the rest ghosts, a 2 px rule under them, a tick and a numeral at every
 * thousand and a shorter tick at every five hundred, and R, P, M stacked at the right end. All of the
 * lit segments flash at the shift point.
 *
 * **The scale.** The tacho runs to the car's redline rounded up to the thousand, read from the sim
 * as the strips read it (`redlineRpm`, ADR 0004 and ADR 0014), so the numerals are 0 to 8 on the
 * MX-5 and 0 to 12 on the Legends. A segment's place therefore depends on the car, and an item's place
 * is fixed when the package is built; the tacho is consequently drawn once per scale from
 * {@link SCALES}, each a layer shown while the car's redline rounds to it. A layer that is hidden has
 * its children's bindings left unevaluated, so the rig pays for the one it shows.
 *
 * **The fill.** Lighting a hundred and thirty segments one binding each is a hundred and thirty
 * expressions a frame, so a layer lights them with two instead. Under everything is the row of ghost
 * segments' extent in the ghost's ink; over it the same extent in full ink, which is every segment lit;
 * over that the unlit part masked back to the ground and its ghost, both moved along by one bound
 * `Left`; and over all of it the gaps between the segments, in the ground, which is what makes a
 * band of ink read as a row of segments. The fill stops on a segment's edge because the mask's left is
 * the hundred the revs have reached.
 *
 * **The flash** is the full-ink extent blinking, under the same condition the house's rev bar flashes
 * on: in the `shift` mode, at the car's own measured over-rev where the rig reads one and otherwise
 * at either ladder's, which is `revSegments.ts`'s model rather than one of this theme's.
 */
import type { Item, LayerItem, Rect, RectangleItem } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { REDLINE_BLINK_MS } from '../../components/revSegments.ts';
import { zone as zoneSetting } from '../../contract.ts';
import { rect, roundRect } from '../../design/geometry.ts';
import { carLadderFlash, carLadderOnScreens, overRevEither, redlineRpm, rpms } from '../../shift.ts';
import { regionRect } from '../anatomy.ts';
import type { FaceContext } from '../drawing.ts';
import { RPM_LETTERS } from './anatomy.ts';
import { GHOST_OPACITY, inkRect, lcdColour, segment, segmentWidth } from './register.ts';

const { add, and, div, eq, iff, le, max, min, mul, num, str, truncate } = ncalc;

/** The scales the tacho is drawn at, in thousands: the three cars' and the room either side of them. */
export const SCALES: readonly number[] = [6, 7, 8, 9, 10, 11, 12, 13];

/** A car whose redline the sim does not publish is drawn on the MX-5's scale. */
const UNKNOWN_SCALE = 8;

/** The canvas's tacho at its 90 px well: segment 6 wide, the rule at 54, ticks from 56, numerals at 70. */
const TACHO = { segment: 6, rule: { top: 54, height: 2 }, tick: { top: 56, major: { width: 3, height: 10 }, minor: { width: 2, height: 6 } }, numerals: { top: 70, size: 20 }, letters: { size: 16, line: 20 } };

/** The scale the car's redline rounds up to, in thousands, kept to {@link SCALES}. */
export const scaleOf = (redline: Expr): Expr => {
  const thousands = truncate(div(add(redline, num(999)), num(1000)));
  const kept = max(num(SCALES[0]!), min(num(SCALES[SCALES.length - 1]!), thousands));
  return iff(le(redline, num(0)), num(UNKNOWN_SCALE), kept);
};

/** Where the tacho flashes, as the house's rev bar does in the same mode on the same ladder. */
const shiftPoint = (ctx: FaceContext): Expr => and(eq(zoneSetting.revBar(ctx.face), str('shift')), iff(carLadderOnScreens(), carLadderFlash(), overRevEither()));

const rectItem = (name: string, r: Rect, colour: 'ground' | 'ink', extra: Partial<RectangleItem> = {}): RectangleItem => ({
  kind: 'rect',
  name,
  rect: roundRect(r),
  backgroundColor: lcdColour(colour),
  ...extra,
});

/** One scale's tacho, `thousands` to the full width of the well. */
function scaleLayer(ctx: FaceContext, well: Rect, row: Rect, thousands: number, numeralSize: number): LayerItem {
  const k = well.height / 90;
  const top = (at: number): number => well.top + Math.round(at * k);
  const hundreds = thousands * 10;
  const pitch = well.width / hundreds;
  const width = Math.min(TACHO.segment, Math.max(2, Math.round(pitch * 0.6)));
  const centre = (h: number): number => well.left + h * pitch;
  const name = `revBar.scale${thousands}`;
  const extent = rect(centre(0) - width / 2, row.top, well.width + width, row.height);
  // The left edge of the unlit part: the gap after the last hundred the revs have reached.
  const reached = truncate(div(min(max(rpms(), num(0)), num(thousands * 1000)), num(100)));
  const maskLeft = add(num(Math.round(centre(0) + width / 2)), mul(reached, num(Math.round(pitch * 1000) / 1000)));
  const children: Item[] = [
    rectItem(`${name}.ghosts`, extent, 'ink', { opacity: GHOST_OPACITY }),
    withMoreBindings(rectItem(`${name}.lit`, extent, 'ink', { blink: { delayMs: REDLINE_BLINK_MS } }), { BlinkEnabled: shiftPoint(ctx) }),
    withMoreBindings(rectItem(`${name}.unlit`, extent, 'ground'), { Left: maskLeft }),
    withMoreBindings(rectItem(`${name}.unlitGhosts`, extent, 'ink', { opacity: GHOST_OPACITY }), { Left: maskLeft }),
  ];
  for (let h = 0; h < hundreds; h++) {
    const from = Math.round(centre(h) + width / 2);
    const to = Math.round(centre(h + 1) - width / 2);
    if (to > from) children.push(rectItem(`${name}.gap${String(h).padStart(3, '0')}`, rect(from, row.top, to - from, row.height), 'ground'));
  }
  const ruleTop = top(TACHO.rule.top);
  children.push(inkRect(`${name}.rule`, rect(well.left, ruleTop, well.width, TACHO.rule.height)));
  const tickTop = ruleTop + TACHO.rule.height;
  const numeralWidth = segmentWidth('DSEG7Regular', '88', numeralSize);
  for (let h = 0; h <= hundreds; h += 5) {
    const major = h % 10 === 0;
    const tick = major ? TACHO.tick.major : TACHO.tick.minor;
    const x = Math.round(centre(h) - tick.width / 2);
    children.push(inkRect(`${name}.tick${String(h).padStart(3, '0')}`, rect(x, tickTop, tick.width, Math.round(tick.height * k))));
    if (major) {
      const n = String(h / 10);
      children.push(segment(`${name}.numeral${n.padStart(2, '0')}`, 'DSEG7Regular', n, Math.round(centre(h) - numeralWidth / 2), top(TACHO.numerals.top), numeralWidth, { size: numeralSize, hAlign: 'center' }));
    }
  }
  return withMoreBindings({ kind: 'layer', name, children } satisfies LayerItem, { Visible: eq(scaleOf(redlineRpm()), num(thousands)) });
}

/** The numerals' size: the canvas's 20 px where a thousand is wide enough for two cells, smaller where it is not, and never under 14. */
function numeralSizeOf(well: Rect): number {
  const k = well.height / 90;
  const wanted = Math.max(14, Math.round(TACHO.numerals.size * k));
  const thousand = well.width / SCALES[SCALES.length - 1]!;
  const fits = Math.floor((0.85 * thousand) / (2 * 0.816));
  return Math.min(wanted, fits);
}

export function aimTacho(ctx: FaceContext): Item[] {
  const well = regionRect(ctx.regions, 'revBarWell');
  const row = regionRect(ctx.regions, 'revBar');
  const numeralSize = numeralSizeOf(well);
  const k = well.height / 90;
  const letterSize = Math.round(TACHO.letters.size * k);
  const line = Math.round(TACHO.letters.line * k);
  const lettersLeft = well.left + well.width + RPM_LETTERS.gap;
  const letterWidth = segmentWidth('DSEG14Regular', 'M', letterSize);
  // The unlit mask and its ghost run off the tacho's right end as the revs climb, so the ground is
  // put back over the row from the end of the widest segment to the edge of the face.
  const end = Math.ceil(well.left + well.width + TACHO.segment / 2);
  return [
    ...SCALES.map((thousands) => scaleLayer(ctx, well, row, thousands, numeralSize)),
    rectItem('revBar.end', rect(end, row.top, ctx.layout.width - end, row.height), 'ground'),
    ...['R', 'P', 'M'].map((letter, i) =>
      segment(`revBar.letter${letter}`, 'DSEG14Regular', letter, lettersLeft + Math.round((RPM_LETTERS.width - letterWidth) / 2), well.top + i * line + Math.round((line - letterSize) / 2), letterWidth, { size: letterSize, hAlign: 'center' }),
    ),
  ];
}
