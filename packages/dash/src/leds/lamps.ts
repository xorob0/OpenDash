/**
 * The lamps of a side: which LED a condition lands on, and who outranks whom once it is there.
 *
 * A side used to be one run handed whole to whichever effect composed last, so a flag blanked the
 * spotter and two live conditions were resolved by their position in an array. A side is instead an
 * ordered set of lamps, counted from the outside in, each of exactly one LED and each owned by one
 * role: the outermost carries what is happening beside the car, then race control, then the car's
 * own warnings, then the aids. The order is the driver's rather than the catalogue's — what the
 * outside world is doing to you first, what your car is doing to itself next, what an aid is doing
 * for you last — and the outermost LED is the one peripheral vision actually reaches.
 *
 * Under four lamps a role shares rather than moves, because a lamp that changes LED between two
 * wheels is a lamp the driver has to relearn on each of them. Three lamps put the aids beneath the
 * car warnings; two put the flags beneath them and drop the aids from the list altogether, dropped
 * rather than shadowed so that a missing aid is a statement about a two-LED side rather than an
 * accident of ranking.
 *
 * Nothing here knows what a condition is, only which role owns it; the catalogue in `effects.ts`
 * carries the conditions and `rpmStrip.ts` turns a lamp into a container.
 */
import { rightStart, type StripShape } from './strip.ts';

/** What owns a lamp, outermost role first. */
export type LampRole = 'side' | 'race' | 'car' | 'aid';

/** Where a condition lands: on the lamp of one role, or on the whole strip. */
export type EffectRole = LampRole | 'strip';

export interface Lamp {
  /** The role that owns it, and whose conditions rank first on it. */
  role: LampRole;
  /** What it is called in the profile, which is the role except where two lamps share one. */
  label: string;
  /** Every role it draws, highest rank first. A lamp of its own role carries only that. */
  carries: readonly LampRole[];
  /** Where a lamp takes only part of a role, the effect ids it takes. */
  only?: readonly string[];
}

const lamp = (role: LampRole, label: string, carries: readonly LampRole[], only?: readonly string[]): Lamp => ({
  role,
  label,
  carries,
  ...(only ? { only } : {}),
});

const SIDE = lamp('side', 'side', ['side']);
const RACE = lamp('race', 'race', ['race']);
const CAR = lamp('car', 'car', ['car']);
const AID = lamp('aid', 'aid', ['aid']);

/**
 * The second aid lamp of a five-LED side, which the canvas gives to DRS and push to pass. It is a
 * second view of the aid list rather than a split of it: with five lamps a side there is room for
 * two aids at once, and restricting the outer one would hide a push to pass behind an ABS light
 * while a lamp beside it sat dark.
 */
const SECOND_AID_IDS: readonly string[] = ['drs', 'p2p'];

/**
 * The lamps of one side, outermost first.
 *
 * A side of one is the side lamp alone, by the same rule that drops the aids at two: what is left
 * when there is no room is the outermost, which is the one thing a side can say that the centre
 * cannot. A side longer than five keeps the five-lamp assignment and leaves the rest to the brake
 * gradient, because no shape has one and inventing a sixth role for a strip nobody owns would be a
 * lamp with no meaning attached.
 */
export const lampsForSide = (count: number): readonly Lamp[] =>
  count <= 0
    ? []
    : count === 1
      ? [SIDE]
      : count === 2
        ? [SIDE, lamp('car', 'car and flag', ['car', 'race'])]
        : count === 3
          ? [SIDE, RACE, lamp('car', 'car and aid', ['car', 'aid'])]
          : count === 4
            ? [SIDE, RACE, CAR, AID]
            : [SIDE, RACE, CAR, AID, lamp('aid', 'second aid', ['aid'], SECOND_AID_IDS)];

/** One lamp of one side of one shape, with the LED it owns. */
export interface PlacedLamp {
  side: 'left' | 'right';
  lamp: Lamp;
  /** How far in from the outside it sits: 0 is the outermost. */
  index: number;
  /** 1-based position in the shape's main run. */
  position: number;
}

/**
 * Every lamp of a shape, both sides, each on the LED it owns.
 *
 * The left side counts inwards from LED 1 and the right counts inwards from the last LED of the
 * run, so that "second from the outside" means the same thing at both ends — which is what makes
 * the race lamp LED 2 and LED 21 on a 4/14/4 rather than LED 2 and LED 20.
 */
export const lampsOf = (shape: StripShape): readonly PlacedLamp[] => [
  ...lampsForSide(shape.left).map((l, index) => ({ side: 'left' as const, lamp: l, index, position: 1 + index })),
  ...lampsForSide(shape.right).map((l, index) => ({ side: 'right' as const, lamp: l, index, position: rightStart(shape) + shape.right - 1 - index })),
];
