/**
 * The flag catalogue drawn sixty-four pixels at a time.
 *
 * Each glyph is eight strings of eight characters and a palette, so a reviewer reads the picture
 * rather than a pixel list. Every colour is a `purpose.flag.*` token; a hex value here is a bug.
 * They are drawn from the real flags, never from somebody else's grid.
 *
 * Sixty-four pixels is the whole design problem, and four decisions come out of it:
 *
 *   - **Black is the absence of light**, so a black flag is an outline and a disqualification is a
 *     cross, both in `purpose.flag.black` — which is the near-white the face outlines it with, for
 *     the same reason.
 *   - **Waving is blinking.** iRacing publishes `yellow` and `yellowWaving` separately and the two
 *     have to be told apart at a glance, which is a pattern question rather than a colour one. The
 *     rate is `indicator.flagBand.flashHz`, so the box pulses with the band on the face.
 *   - **A full-course caution is banded**, not solid, so the whole-track condition never looks
 *     like a local yellow.
 *   - **Blue moves.** A blue flag with a moving arrow says which way to look; a static blue square
 *     says a colour. It is two frames.
 */
import type { Hex, MatrixFrame } from '../generator.ts';
import { ds } from '../tokens.ts';
import { blinkFrames, still, type Grid, type Palette } from './glyph.ts';

/** Every character any grid below uses. One palette, so a typo is a build error. */
export const FLAG_PALETTE: Palette = {
  R: ds.purpose.flag.red,
  Y: ds.purpose.flag.yellow,
  S: ds.purpose.flag.debrisStripe,
  B: ds.purpose.flag.blue,
  W: ds.purpose.flag.white,
  G: ds.purpose.flag.green,
  C: ds.purpose.flag.chequer,
  K: ds.purpose.flag.black,
  O: ds.purpose.flag.orange,
};

/** How fast anything on this box blinks, from the token the face's band uses. */
export const BLINK_HZ = ds.indicator.flagBand.flashHz;

const solid = (ch: string): Grid => Array.from({ length: 8 }, () => ch.repeat(8));

/** The eight-by-eight field of nothing: the dark half of a blink. */
export const DARK: Grid = solid('.');

// --- The grids -------------------------------------------------------------------------------

/** Red: the session is stopped. The only condition that takes the whole box in one colour. */
export const RED: Grid = solid('R');

/** Disqualified: the black flag with a cross through it, which is how it is flown. */
export const DISQUALIFY: Grid = [
  'K......K',
  '.K....K.',
  '..K..K..',
  '...KK...',
  '...KK...',
  '..K..K..',
  '.K....K.',
  'K......K',
];

/** Black: an outline, because a black field on an unlit matrix is nothing at all. */
export const BLACK: Grid = [
  'KKKKKKKK',
  'K......K',
  'K......K',
  'K......K',
  'K......K',
  'K......K',
  'K......K',
  'KKKKKKKK',
];

/** Black furled: the same flag rolled up, so it is a bar rather than a field. */
export const FURLED: Grid = [
  '........',
  '........',
  '........',
  'KKKKKKKK',
  'KKKKKKKK',
  '........',
  '........',
  '........',
];

/** Meatball: the orange disc. Round, so it is not read as a flag of any other colour. */
export const MEATBALL: Grid = [
  '..OOOO..',
  '.OOOOOO.',
  'OOOOOOOO',
  'OOOOOOOO',
  'OOOOOOOO',
  'OOOOOOOO',
  '.OOOOOO.',
  '..OOOO..',
];

/** Chequered: a checkerboard of two-pixel squares, which is legible; one with a pole is not. */
export const CHEQUERED: Grid = [
  'CC..CC..',
  'CC..CC..',
  '..CC..CC',
  '..CC..CC',
  'CC..CC..',
  'CC..CC..',
  '..CC..CC',
  '..CC..CC',
];

/** Full-course caution: yellow in bands, so the whole track never looks like one corner. */
export const CAUTION: Grid = [
  'YYYYYYYY',
  'YYYYYYYY',
  '........',
  'YYYYYYYY',
  'YYYYYYYY',
  '........',
  'YYYYYYYY',
  'YYYYYYYY',
];

/** Yellow: solid and steady. Waved yellow is this, blinking. */
export const YELLOW: Grid = solid('Y');

/** Debris: yellow with danger stripes, the pattern `purpose.flag.debris` already describes. */
export const DEBRIS: Grid = [
  'YYYSYYYS',
  'YYSYYYSY',
  'YSYYYSYY',
  'SYYYSYYY',
  'YYYSYYYS',
  'YYSYYYSY',
  'YSYYYSYY',
  'SYYYSYYY',
];

/** Blue, arrow left of centre. The arrow is unlit, so it reads as a cut-out in the flag. */
export const BLUE_A: Grid = [
  'BBBBBBBB',
  'BBBBBBBB',
  'BBB.BBBB',
  'BBBB.BBB',
  'B......B',
  'BBBB.BBB',
  'BBB.BBBB',
  'BBBBBBBB',
];

/** Blue, arrow one pixel right: the second frame, which is what makes it move. */
export const BLUE_B: Grid = [
  'BBBBBBBB',
  'BBBBBBBB',
  'BBBB.BBB',
  'BBBBB.BB',
  'BB......',
  'BBBBB.BB',
  'BBBB.BBB',
  'BBBBBBBB',
];

/** White: the last lap. */
export const WHITE: Grid = solid('W');

/** Green. */
export const GREEN: Grid = solid('G');

/** The start gantry, holding: one bar. */
export const START_READY: Grid = [
  '........',
  '........',
  '........',
  '........',
  '........',
  'RRRRRRRR',
  'RRRRRRRR',
  '........',
];

/** The start gantry, set: two bars, so the sequence is a count rather than a colour change. */
export const START_SET: Grid = [
  '........',
  '........',
  '........',
  'RRRRRRRR',
  'RRRRRRRR',
  '........',
  'RRRRRRRR',
  'RRRRRRRR',
];

// --- What each condition draws ---------------------------------------------------------------

/**
 * The frames of each condition in `FLAG_CATALOGUE`, by id. A condition without an entry here has
 * no glyph and is not drawn, which is the case the table in docs/design/flag-box.md exists for.
 */
export function flagFrames(id: string): MatrixFrame[] | undefined {
  switch (id) {
    case 'red':
      return still(RED, FLAG_PALETTE, id);
    case 'disqualify':
      // Blinking, because it is the one flag that ends the driver's race whether they react or not.
      return blinkFrames(DISQUALIFY, DARK, FLAG_PALETTE, BLINK_HZ, id);
    case 'black':
      return still(BLACK, FLAG_PALETTE, id);
    case 'furled':
      return still(FURLED, FLAG_PALETTE, id);
    case 'meatball':
      return still(MEATBALL, FLAG_PALETTE, id);
    case 'chequered':
      return still(CHEQUERED, FLAG_PALETTE, id);
    case 'caution':
      return still(CAUTION, FLAG_PALETTE, id);
    case 'yellowWaving':
      return blinkFrames(YELLOW, DARK, FLAG_PALETTE, BLINK_HZ, id);
    case 'yellow':
      return still(YELLOW, FLAG_PALETTE, id);
    case 'debris':
      return still(DEBRIS, FLAG_PALETTE, id);
    case 'blue':
      return blinkFrames(BLUE_A, BLUE_B, FLAG_PALETTE, BLINK_HZ, id);
    case 'white':
      return still(WHITE, FLAG_PALETTE, id);
    case 'green':
      return still(GREEN, FLAG_PALETTE, id);
    case 'startSet':
      return still(START_SET, FLAG_PALETTE, id);
    case 'startReady':
      return still(START_READY, FLAG_PALETTE, id);
    default:
      return undefined;
  }
}
