/**
 * badge: a driver's licence class as a letter, drawn out of the greys rather than in iRacing's own
 * five colours. Every one of those five is a state colour on this face, and a green B licence
 * beside a green delta is the confusion the first brand rule exists to prevent, so the class --
 * which is ordered -- is drawn as an ordered weight instead: R is the lightest mark on the screen,
 * the middle classes gain weight through it, and Pro is the heaviest and the only one inverted, a
 * dark letter on a light block. That ramp is `purpose.licence`, and this is what reads it.
 *
 * The block is measured from the letter. SimHub hands a text box to WPF as `MaxTextWidth` and WPF
 * clips whatever does not fit, so a block sized by hand to look square takes the edge off the glyph
 * it is drawn behind; the width here is the letter's own advances plus the padding either side,
 * floored at the block's height so that a one-letter badge stays the square the canvas draws and
 * only "PRO" grows past it.
 *
 * **Nothing draws one yet, on purpose.** The letter has no source: SimHub's per-car family carries
 * `driveriracingirating` and nothing about a licence or a safety rating, which `second/values.ts`
 * and `second/table.ts` both record. A badge bound to a guessed property is worse than a badge that
 * is not drawn, so the element is built against the tokens and waits for a reader.
 *
 * Two things a caller should know before wiring one. `FontWeight` is not among the properties
 * SimHub will bind, so a badge whose class arrives at runtime cannot change weight with it: it is
 * six badges, one per class, each with its own `Visible`. And a package ships the faces it draws
 * in, which for Barlow is Medium and Bold on a dash face and Medium alone on a second screen, so
 * B and A, which the ramp draws in SemiBold, and Pro, which it draws in Bold, need their file
 * added to `FACE_FONT_FILES` or `SCREEN_FONT_FILES` and measured into `design/advances.ts` before
 * `validateOrThrow` will let them onto anything.
 */
import type { FontWeight, Hex, Item } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { band } from './band.ts';
import { label } from './label.ts';
import { ds } from '../tokens.ts';

/** The iRacing classes, lightest first, which is the order the ramp is drawn in. */
export type LicenceClass = 'r' | 'd' | 'c' | 'b' | 'a' | 'pro';

export const LICENCE_CLASSES: readonly LicenceClass[] = ['r', 'd', 'c', 'b', 'a', 'pro'];

/** What each class wears. Pro is the one that spells itself out, as the canvas draws it. */
export const LICENCE_LETTERS: Record<LicenceClass, string> = { r: 'R', d: 'D', c: 'C', b: 'B', a: 'A', pro: 'PRO' };

/**
 * The letter and the block of a badge on a list row, which is the shape the canvas draws it at in
 * every drawing that has one: 12 px inside 18, whatever the row it sits in and whatever ramp the
 * values beside it are on. A badge does not follow the type ramp, being a mark rather than a value.
 */
export const BADGE_SIZE = 12;
export const BADGE_HEIGHT = 18;

/** Room either side of the letter. The class chip that sits beside it on the same row takes six too. */
const BADGE_PADDING = 6;

export interface BadgeOptions {
  /** Letter size and block height, for a caller whose badge is not the row's. */
  size?: number;
  height?: number;
  visibleBind?: Expr;
}

/** The face a token weight names. */
const faceWeight = (weight: number): FontWeight => (weight >= 700 ? 'Bold' : weight >= 600 ? 'SemiBold' : 'Medium');

/**
 * The letter's width in the wider of the two Barlow faces `advances.ts` measures.
 *
 * The ramp draws the same letter at three weights and only Medium and Bold are measured, so a
 * badge measured in one face and drawn in another is short of its glyph by the difference. Taking
 * the wider of the pair that brackets the ramp costs a fraction of a pixel and cannot clip, and a
 * class's box then stops depending on which rung of the ramp its weight sits on.
 */
const letterWidth = (text: string, size: number): number => Math.max(measureText('BarlowMedium', text, size), measureText('BarlowBold', text, size));

/** What a class's badge occupies: the square at the minimum, wider where the letters ask for it. */
export function badgeWidth(cls: LicenceClass, opts: BadgeOptions = {}): number {
  const size = opts.size ?? BADGE_SIZE;
  const height = opts.height ?? BADGE_HEIGHT;
  return Math.max(height, Math.ceil(2 * BADGE_PADDING + letterWidth(LICENCE_LETTERS[cls], size)));
}

/** A badge for `cls` whose top edge is `top`, the letter centred in the block both ways. */
export function badge(name: string, cls: LicenceClass, x: number, top: number, opts: BadgeOptions = {}): Item[] {
  const size = opts.size ?? BADGE_SIZE;
  const height = opts.height ?? BADGE_HEIGHT;
  const width = badgeWidth(cls, opts);
  const step = ds.purpose.licence[cls];
  // Only Pro carries a fill in the tokens, so only Pro draws a block; the rest are a letter alone.
  const fill: Hex | undefined = 'fill' in step ? step.fill : undefined;
  return [
    ...(fill === undefined ? [] : [band(`${name}.block`, rect(x, top, width, height), fill, { visibleBind: opts.visibleBind })]),
    // The letter's box is the whole block, centred: the padding is already in the block's width,
    // and taking it off the text box again would leave a 12 px letter 6 px to be drawn in.
    label(`${name}.text`, LICENCE_LETTERS[cls], x, top + (height - size) / 2, width, {
      size,
      color: step.value,
      weight: faceWeight(step.weight),
      hAlign: 'center',
      visibleBind: opts.visibleBind,
    }),
  ];
}
