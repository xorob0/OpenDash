/**
 * The flag box: an 8x8 matrix, and the catalogue drawn sixty-four pixels at a time.
 *
 * ADR 0013 puts this first because it is the one piece of hardware whose content openDash already
 * owns — the flag colours are tokens, and the ranking is the face's own `flagVisible`, so the box
 * and the screen cannot disagree about which of two live flags wins.
 *
 * The matrix is a different driver from the strip, with its own container catalogue, its own
 * `ContainerType` spelling (the bare class name) and its own two-dimensional positioning. The
 * generator knows both dialects; this module only has to say `matrix`.
 *
 * **Sixty-four pixels is the whole design constraint.** A flag that is a colour is drawn as a
 * colour; a flag that is a *pattern* — the chequer, the black-and-white — has to be a pattern
 * legible at 8x8 from a metre away, which means whole rows and columns rather than detail. Nothing
 * here is scaled down from a bigger picture, because at this size there is nothing to scale.
 */
import { leds } from '../generator.ts';
import { FLAG_PRIORITY, flagVisible, type FlagProperty } from '../components/flagStrip.ts';
import { ds } from '../tokens.ts';

/** The matrix openDash generates for. SimHub drives up to four of them from one profile. */
export const MATRIX_SIZE = 8;

/** How long a still picture is held. One frame, so the duration only has to be longer than a frame. */
export const STILL_FRAME_MS = 1000;

type Pixels = (string | undefined)[][];

/** An 8x8 grid, every pixel the same colour. */
const solid = (color: string): Pixels => Array.from({ length: MATRIX_SIZE }, () => Array.from({ length: MATRIX_SIZE }, () => color));

/**
 * A chequer, in blocks of two so that eight pixels give a four-by-four board. One pixel per square
 * would alias into a grey smear at any distance, which is the whole reason the block size is here
 * rather than assumed.
 */
const chequer = (a: string, b: string, block = 2): Pixels =>
  Array.from({ length: MATRIX_SIZE }, (_, r) =>
    Array.from({ length: MATRIX_SIZE }, (_, c) => (Math.floor(r / block) + Math.floor(c / block)) % 2 === 0 ? a : b),
  );

/**
 * A diagonal split, which is how a flag that is two colours is told apart from one that is a
 * chequer at this size.
 */
const diagonal = (a: string, b: string): Pixels =>
  Array.from({ length: MATRIX_SIZE }, (_, r) => Array.from({ length: MATRIX_SIZE }, (_, c) => (c >= r ? a : b)));

/**
 * The picture each flag is drawn as.
 *
 * Black is the one that cannot be drawn as itself: a black flag on an unlit box is an unlit box, so
 * it is drawn as the *text* colour the face uses for it, which is the same choice
 * `flagStrip.ts` makes when it outlines the black band rather than filling it.
 */
export const FLAG_PICTURES: Record<FlagProperty, Pixels> = {
  Flag_Black: solid(ds.purpose.flag.black),
  Flag_Checkered: chequer(ds.purpose.flag.chequer, ds.color.surface.base),
  Flag_Yellow: solid(ds.purpose.flag.yellow),
  Flag_Blue: diagonal(ds.purpose.flag.blue, ds.color.surface.base),
  Flag_White: solid(ds.purpose.flag.white),
  Flag_Green: solid(ds.purpose.flag.green),
};

/** One still picture as an `AnimationContainer`. */
export const picture = (pixels: Pixels, description: string): leds.LedContainer => ({
  kind: 'animation',
  description,
  rows: MATRIX_SIZE,
  columns: MATRIX_SIZE,
  frames: [{ pixels, durationMs: STILL_FRAME_MS }],
});

/**
 * The flag catalogue as a tree: one conditional group per flag, ranked by the face's own
 * `flagVisible`, highest priority last so that it composes on top.
 */
export const flagCatalogue = (): leds.LedContainer[] =>
  [...FLAG_PRIORITY].reverse().map((flag) => ({
    kind: 'conditionalGroup' as const,
    description: `${flag.replace('Flag_', '')} flag`,
    trigger: { expression: flagVisible(flag) },
    clearBackgroundWhenActive: true,
    children: [picture(FLAG_PICTURES[flag] ?? solid(ds.purpose.flag.white), flag.replace('Flag_', ''))],
  }));

/** What the box is called in SimHub's profile list, and on disk. */
export const FLAG_BOX_PROFILE_NAME = 'openDash flag box';
export const FLAG_BOX_FILE_NAME = 'openDash flag box';

/**
 * The flag box profile.
 *
 * Everything is inside a `GameRunningGroupContainer` for the same reason the strip is: a
 * conditional group whose formula throws is treated as true, so a box driven by formulas lights up
 * with the sim closed. What the box does when the game is *not* running is XOR-229, and this is
 * where it hangs.
 */
export function flagBoxProfile(profileId: string): leds.LedProfile {
  return {
    name: FLAG_BOX_PROFILE_NAME,
    profileId,
    dialect: 'matrix',
    containers: [
      {
        kind: 'raw',
        containerType: 'GameRunningGroupContainer',
        description: 'only while the sim is running',
        children: flagCatalogue(),
      },
    ],
  };
}
