/**
 * The AiM LCD at every face size (#204): the tacho across the top, zones B, A and C side by side
 * under it, a rule, and band D along the foot.
 *
 * 1280 x 480 is the ticket's geometry table, "The drawing, in numbers", which is the `After`
 * component of the canvas. The other seven follow from it by the ticket's rules for the other
 * sizes rather than being drawn freehand: 1920 x 480 keeps every height and widens B and C, zone A
 * staying 240 wide; 1280 x 400 keeps every x and scales the tacho, the body and the foot in the
 * proportion 110 : 284 : 66; 1280 x 720 keeps the tacho and the foot and gives the body the rest;
 * 850 and 800 x 480 share the width 440 : 240 : 440 inside a narrower margin; 800 x 286 has a
 * 60 px tacho, a body of two rows and a shorter foot; and the portrait face stacks the tacho, zone A
 * across the width, B and C side by side, and the foot.
 *
 * Only rectangles here, because the registry reads this file before `ds` exists. The tacho's own
 * proportions inside its well, the segments, the rule and the numerals, are `tacho.ts`'s.
 */
import type { Rect } from '../../generator.ts';
import { FACE_SIZES } from '../../contract.ts';
import { rect } from '../../design/geometry.ts';
import type { Anatomy, Regions } from '../anatomy.ts';

/** One face's numbers: the margin and gap across, and the four heights down. */
interface Plan {
  /** Left and right of the LCD's content, and between the body's zones. */
  margin: number;
  gap: number;
  /** The tacho's well: its top and its height, segments, rule, ticks and numerals. */
  tachoTop: number;
  tacho: number;
  /** The body's top and height. */
  bodyTop: number;
  body: number;
  /** Band D's height, under the 2 px foot rule and its 4 px either side. */
  band: number;
}

/** The room the stacked RPM letters take at the tacho's right end, and the gap before them. */
export const RPM_LETTERS = { width: 44, gap: 16 } as const;

/** The foot rule and the room either side of it, from the canvas: 406 to 408, the band from 412. */
export const FOOT_RULE = { above: 4, height: 2, below: 4 } as const;

/** The canvas's 1280 x 480: margin 40, gaps 40, the tacho 14 to 104, the body 118 to 402, the band 412 to 472. */
const REFERENCE: Plan = { margin: 40, gap: 40, tachoTop: 14, tacho: 90, bodyTop: 118, body: 284, band: 60 };

/** Where the band ends, which has to leave a margin under it. */
const bottomOf = (plan: Plan): number => plan.bodyTop + plan.body + FOOT_RULE.above + FOOT_RULE.height + FOOT_RULE.below + plan.band;

const PLANS: Record<string, Plan> = {
  '1280x480': REFERENCE,
  '1920x480': REFERENCE,
  // 110 : 284 : 66 of 400 rather than of 480, every gap kept: the tacho 75 tall, the body 237, the band 50.
  '1280x400': { ...REFERENCE, tachoTop: 12, tacho: 75, bodyTop: 98, body: 236, band: 50 },
  // The tacho and the foot where they are at 480, and the body deeper by the 240 px the face adds.
  '1280x720': { ...REFERENCE, body: 524 },
  // A narrower margin and gap, so that the three regions keep what width there is.
  '850x480': { ...REFERENCE, margin: 24, gap: 24 },
  '800x480': { ...REFERENCE, margin: 24, gap: 24 },
  // The ticket's short face: a 60 px tacho, a body two rows deep, and a foot of one row at 24 px.
  '800x286': { margin: 24, gap: 24, tachoTop: 6, tacho: 60, bodyTop: 74, body: 152, band: 44 },
  '600x686': { margin: 24, gap: 24, tachoTop: 14, tacho: 90, bodyTop: 118, body: 484, band: 60 },
};

/** The zone A column at each width: 240 where there is room, the ticket's share of 440 : 240 : 440 where there is not. */
const zoneAWidth = (width: number, plan: Plan): number => Math.min(240, Math.round(((width - 2 * plan.margin - 2 * plan.gap) * 240) / 1120));

/** The portrait face's zone A, across the width above B and C, and the gap under it. */
const PORTRAIT_A = 180;

/** The planned regions of a face, the size named by its width and height. */
export function aimRegions(width: number, height: number): Regions {
  const plan = PLANS[`${width}x${height}`];
  if (!plan) throw new Error(`the AiM theme has no plan for ${width}x${height}`);
  if (bottomOf(plan) > height - 6) throw new Error(`the AiM plan for ${width}x${height} runs to ${bottomOf(plan)}`);
  const inner = width - 2 * plan.margin;
  const tachoWidth = inner - RPM_LETTERS.width - RPM_LETTERS.gap;
  const well = rect(plan.margin, plan.tachoTop, tachoWidth, plan.tacho);
  // The segments are the top of the well, five ninths of it as 50 is of 90 on the canvas.
  const segments = rect(plan.margin, plan.tachoTop, tachoWidth, Math.round((plan.tacho * 50) / 90));
  const footRule = plan.bodyTop + plan.body + FOOT_RULE.above;
  const band = rect(plan.margin, footRule + FOOT_RULE.height + FOOT_RULE.below, inner, plan.band);

  let a: Rect;
  let b: Rect;
  let c: Rect;
  if (height > width) {
    a = rect(plan.margin, plan.bodyTop, inner, PORTRAIT_A);
    const half = (inner - plan.gap) / 2;
    const top = plan.bodyTop + PORTRAIT_A + plan.gap;
    const rest = plan.bodyTop + plan.body - top;
    b = rect(plan.margin, top, Math.floor(half), rest);
    c = rect(plan.margin + Math.ceil(half) + plan.gap, top, Math.floor(half), rest);
  } else {
    const aWidth = zoneAWidth(width, plan);
    const side = Math.floor((inner - aWidth - 2 * plan.gap) / 2);
    b = rect(plan.margin, plan.bodyTop, side, plan.body);
    a = rect(plan.margin + side + plan.gap, plan.bodyTop, aWidth, plan.body);
    c = rect(width - plan.margin - side, plan.bodyTop, side, plan.body);
  }
  const lcdBelowTacho = plan.tachoTop + plan.tacho + 6;
  return [
    { role: 'revBarWell', rect: well },
    { role: 'revBar', rect: segments },
    { role: 'zone', zone: 'B', rect: b },
    { role: 'zone', zone: 'A', rect: a },
    { role: 'zone', zone: 'C', rect: c },
    { role: 'band', rect: band },
    // The house's pit banner keeps the top of zone C, where the limiter's own takeover covers it.
    { role: 'pitAlert', rect: rect(c.left, c.top, c.width, 30) },
    { role: 'hero', rect: a },
    // The full-screen flag takes the LCD below the tacho, the foot included.
    { role: 'flagBody', rect: rect(0, lcdBelowTacho, width, height - lcdBelowTacho) },
  ];
}

/** The foot rule's rectangle, which the chrome draws: the width of the band, 4 px over it. */
export const footRuleOf = (bandRect: Rect): Rect => rect(bandRect.left, bandRect.top - FOOT_RULE.below - FOOT_RULE.height, bandRect.width, FOOT_RULE.height);

export const aimAnatomy: Anatomy = {
  sizes: FACE_SIZES.map(({ width, height }) => ({ width, height })),
  regions: (face) => aimRegions(face.width, face.height),
};
