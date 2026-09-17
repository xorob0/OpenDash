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
 *   - **A full-course caution is two flags, not one.** The whole-track condition is waved left and
 *     right in turn, so it never looks like a local yellow, which is the panel solid.
 *   - **Movement means act.** A flag that ends or interrupts the race moves; a flag that informs is
 *     held, which is why blue is a still panel although it is the flag a driver sees most often.
 *     `motion` in flags.ts is that rule written down and flagBox.test.ts holds the drawings to it,
 *     so a picture cannot quietly gain or lose the frames that carry its meaning.
 */
import type { Hex, MatrixFrame } from '../generator.ts';
import { ds } from '../tokens.ts';
import { blinkFrames, pixelsOf, still, type Grid, type Palette } from './glyph.ts';

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

/** One step of a picture that grows, short enough that the four frames read as one movement. */
const GROW_MS = 100;

/**
 * How long the last frame of a growing flag lasts. SimHub has no play-once: an animation loops, so
 * a flag that grows and then stays is a sequence whose final frame simply outlasts the rest of it.
 */
export const HOLD_MS = 20000;

/** One step of a picture that walks, which is half a blink, so the box keeps the band's rate. */
const STEP_MS = Math.round(1000 / BLINK_HZ / 2);

const solid = (ch: string): Grid => Array.from({ length: 8 }, () => ch.repeat(8));

/** The eight-by-eight field of nothing: the dark half of a blink. */
export const DARK: Grid = solid('.');

const framesOf = (steps: readonly (readonly [Grid, number])[], name: string): MatrixFrame[] =>
  steps.map(([grid, durationMs]) => ({ durationMs, pixels: pixelsOf(grid, FLAG_PALETTE, name) }));

/**
 * Grows in three steps and then stays. The growth is what catches an eye that was not on the box;
 * the last frame is the one that still has to be right twenty seconds later.
 */
const growThenHold = (steps: readonly [Grid, Grid, Grid, Grid], name: string): MatrixFrame[] =>
  framesOf([[steps[0], GROW_MS], [steps[1], GROW_MS], [steps[2], GROW_MS], [steps[3], HOLD_MS]], name);

/** Walks: one picture per step, every step the same length. */
const walk = (steps: readonly Grid[], name: string): MatrixFrame[] => framesOf(steps.map((grid) => [grid, STEP_MS] as const), name);

// --- The grids -------------------------------------------------------------------------------

/** Red, first step: the flag opening out of the middle of the panel. */
export const RED_2: Grid = [
  '........',
  '........',
  '........',
  '...RR...',
  '...RR...',
  '........',
  '........',
  '........',
];

/** Red, second step. */
export const RED_4: Grid = [
  '........',
  '........',
  '..RRRR..',
  '..RRRR..',
  '..RRRR..',
  '..RRRR..',
  '........',
  '........',
];

/** Red, third step. */
export const RED_6: Grid = [
  '........',
  '.RRRRRR.',
  '.RRRRRR.',
  '.RRRRRR.',
  '.RRRRRR.',
  '.RRRRRR.',
  '.RRRRRR.',
  '........',
];

/** Red: the session is stopped. The only condition that takes the whole box in one colour. */
export const RED: Grid = solid('R');

/** Disqualified, first step: the cross, still closed in on itself. */
export const DISQUALIFY_4: Grid = [
  '........',
  '........',
  '..K..K..',
  '...KK...',
  '...KK...',
  '..K..K..',
  '........',
  '........',
];

/** Disqualified, second step. */
export const DISQUALIFY_6: Grid = [
  '........',
  '.K....K.',
  '..K..K..',
  '...KK...',
  '...KK...',
  '..K..K..',
  '.K....K.',
  '........',
];

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

/** Black, the smaller flag of the wave. An outline again, never a filled panel: black is unlit. */
export const SMALL_BLACK: Grid = [
  '........',
  '.KKKKKK.',
  '.K....K.',
  '.K....K.',
  '.K....K.',
  '.K....K.',
  '.KKKKKK.',
  '........',
];

/** Black furled, the bar at the top of its walk. */
export const FURLED_HIGH: Grid = [
  '........',
  '........',
  'KKKKKKKK',
  'KKKKKKKK',
  '........',
  '........',
  '........',
  '........',
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

/** Black furled, the bottom of its walk. One row above the start gantry's bar, and a different hue. */
export const FURLED_LOW: Grid = [
  '........',
  '........',
  '........',
  '........',
  'KKKKKKKK',
  'KKKKKKKK',
  '........',
  '........',
];

/** Meatball, first step. The same four pixels as the standby mark, but in amber and not alone. */
export const MEATBALL_2: Grid = [
  '........',
  '........',
  '........',
  '...OO...',
  '...OO...',
  '........',
  '........',
  '........',
];

/** Meatball, second step. Round already, so it is never read as a square of another colour. */
export const MEATBALL_4: Grid = [
  '........',
  '........',
  '...OO...',
  '..OOOO..',
  '..OOOO..',
  '...OO...',
  '........',
  '........',
];

/** Meatball, third step. */
export const MEATBALL_6: Grid = [
  '........',
  '..OOOO..',
  '.OOOOOO.',
  '.OOOOOO.',
  '.OOOOOO.',
  '.OOOOOO.',
  '..OOOO..',
  '........',
];

/** Meatball: the orange disc. Round, so it is not read as a flag of any other colour. */
export const MEATBALL: Grid = [
  '...OO...',
  '.OOOOOO.',
  'OOOOOOOO',
  'OOOOOOOO',
  'OOOOOOOO',
  'OOOOOOOO',
  '.OOOOOO.',
  '...OO...',
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

/** Chequered, the board inverted: the second half of the wave, and the reason it reads as a flag. */
export const CHEQUERED_INVERSE: Grid = [
  '..CC..CC',
  '..CC..CC',
  'CC..CC..',
  'CC..CC..',
  '..CC..CC',
  '..CC..CC',
  'CC..CC..',
  'CC..CC..',
];

/** Full-course caution, the first of the two flags: half a panel, so it is never a local yellow. */
export const CAUTION_LEFT: Grid = [
  'YYYY....',
  'YYYY....',
  'YYYY....',
  'YYYY....',
  'YYYY....',
  'YYYY....',
  'YYYY....',
  'YYYY....',
];

/** Full-course caution, the second flag. Two waved in turn is how the whole track is called. */
export const CAUTION_RIGHT: Grid = [
  '....YYYY',
  '....YYYY',
  '....YYYY',
  '....YYYY',
  '....YYYY',
  '....YYYY',
  '....YYYY',
  '....YYYY',
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

/** Debris, the stripes one column further along. Written out rather than rotated: a glyph is read. */
export const DEBRIS_STEP2: Grid = [
  'YYSYYYSY',
  'YSYYYSYY',
  'SYYYSYYY',
  'YYYSYYYS',
  'YYSYYYSY',
  'YSYYYSYY',
  'SYYYSYYY',
  'YYYSYYYS',
];

/** Debris, two columns along. Three of the four phases: the fourth would be the first again. */
export const DEBRIS_STEP3: Grid = [
  'YSYYYSYY',
  'SYYYSYYY',
  'YYYSYYYS',
  'YYSYYYSY',
  'YSYYYSYY',
  'SYYYSYYY',
  'YYYSYYYS',
  'YYSYYYSY',
];

/** Blue: the full panel, nothing cut out of it. Blue informs rather than acts, so it does not move. */
export const BLUE: Grid = solid('B');

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
      return growThenHold([RED_2, RED_4, RED_6, RED], id);
    case 'disqualify':
      // The cross closes from the middle outwards rather than blinking, because the panel is never
      // dark while the one flag that ends the driver's race whether they react or not is out.
      return walk([DISQUALIFY_4, DISQUALIFY_6, DISQUALIFY], id);
    case 'black':
      return blinkFrames(BLACK, SMALL_BLACK, FLAG_PALETTE, BLINK_HZ, id);
    case 'furled':
      return walk([FURLED_HIGH, FURLED, FURLED_LOW], id);
    case 'meatball':
      return growThenHold([MEATBALL_2, MEATBALL_4, MEATBALL_6, MEATBALL], id);
    case 'chequered':
      return blinkFrames(CHEQUERED, CHEQUERED_INVERSE, FLAG_PALETTE, BLINK_HZ, id);
    case 'caution':
      return blinkFrames(CAUTION_LEFT, CAUTION_RIGHT, FLAG_PALETTE, BLINK_HZ, id);
    case 'yellowWaving':
      return blinkFrames(YELLOW, DARK, FLAG_PALETTE, BLINK_HZ, id);
    case 'yellow':
      return still(YELLOW, FLAG_PALETTE, id);
    case 'debris':
      return walk([DEBRIS, DEBRIS_STEP2, DEBRIS_STEP3], id);
    case 'blue':
      return still(BLUE, FLAG_PALETTE, id);
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

// --- The box when nothing is happening -------------------------------------------------------

/**
 * Ignition off: a dim mark in the middle, so that a box which is dark *because the car is off*
 * does not look like a box which is dark because the profile failed to load. It is deliberately
 * the smallest thing that is still visibly on — four pixels of `purpose.shift.unlit`, the grey
 * the rev bar's unlit segments use.
 */
export const IGNITION_OFF: Grid = [
  '........',
  '........',
  '........',
  '...UU...',
  '...UU...',
  '........',
  '........',
  '........',
];

/** The palette the standby mark uses. Separate from the flags: nothing here is a flag colour. */
export const STANDBY_PALETTE: Palette = { U: ds.purpose.shift.unlit };

/** The standby mark's frames. */
export const ignitionOffFrames = (): MatrixFrame[] => still(IGNITION_OFF, STANDBY_PALETTE, 'ignitionOff');
