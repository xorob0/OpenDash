/**
 * The states an emulated strip can hold, and what lights where.
 *
 * A side is not a block that lights as one. The profile gives it single LEDs, counted from the
 * outside in: the outermost carries what is happening beside the car, then race control, then the
 * car's own warnings, then the aids, and on a side too short for four a lamp carries more than one
 * role rather than moving. The pit family is the exception and takes the whole run, because it is
 * about where the car is rather than what it is doing. `LAMPS` in the generated content carries
 * that assignment, so the drawing follows the profile instead of guessing at it.
 */

/** The ink a lamp takes, each one a token the dash itself draws with. */
export type Ink = 'good' | 'caution' | 'danger' | 'info' | 'yellow' | 'white';

export interface Lit {
  ink: Ink;
  blink?: boolean;
}

export interface StripFrame {
  label: string;
  /** How much of the centre the revs have taken, 0 to 1. */
  revs: number;
  /** The top of the sweep: the lit centre flashes. */
  shift?: boolean;
  /** The pit family, which takes every LED. */
  strip?: Lit;
  /** A car alongside: the outermost lamp, on that side only. */
  spotter?: { left?: boolean; right?: boolean };
  /** Race control: the flag lamp. */
  race?: Lit;
  /** The car's own: low fuel, oil, water. */
  car?: Lit;
  /** An aid: ABS, traction control. */
  aid?: Lit;
}

export const FRAMES: Record<string, StripFrame> = {
  revs: { label: 'Revs, in the car’s own colours', revs: 0.7 },
  shift: { label: 'Shift now', revs: 1, shift: true },
  blue: { label: 'Blue flag', revs: 0.45, race: { ink: 'info' } },
  yellow: { label: 'Waved yellow', revs: 0.45, race: { ink: 'yellow', blink: true } },
  spotter: { label: 'A car on your left', revs: 0.62, spotter: { left: true } },
  fuel: { label: 'Low fuel', revs: 0.5, car: { ink: 'danger', blink: true } },
  abs: { label: 'ABS, under braking', revs: 0.18, aid: { ink: 'caution' } },
  limiter: { label: 'Pit limiter', revs: 0, strip: { ink: 'white', blink: true } },
};

/** How long the live strip takes to run through everything, in seconds. */
export const LOOP = 21;

/** One period of the rev sweep, which is what the gear changes are counted in. */
const SWEEP = 2.6;

/**
 * The live strip's script: revs climbing and dropping with each gear, and the rest arriving over
 * them the way it does on a lap, one thing at a time.
 */
export function scripted(t: number): StripFrame {
  const phase = (t % SWEEP) / SWEEP;
  const revs = 0.25 + 0.75 * Math.min(1, phase / 0.92);
  const shift = phase > 0.92;
  const at = (from: number, to: number): boolean => t >= from && t < to;

  if (at(15.5, 18)) return { ...FRAMES.limiter!, label: FRAMES.limiter!.label };
  const over: StripFrame = { label: 'Revs, in the car’s own colours', revs, shift };
  if (at(4, 6.5)) return { ...over, race: { ink: 'info' }, label: 'Blue flag' };
  if (at(6.5, 9)) return { ...over, race: { ink: 'yellow', blink: true }, label: 'Waved yellow' };
  if (at(9, 11.5)) return { ...over, spotter: { left: true }, label: 'A car on your left' };
  if (at(11.5, 13.5)) return { ...over, aid: { ink: 'caution' }, label: 'ABS, under braking' };
  if (at(13.5, 15.5)) return { ...over, car: { ink: 'danger', blink: true }, label: 'Low fuel' };
  if (at(18, 21)) return { ...over, race: { ink: 'good' }, label: 'Green flag' };
  return over;
}
