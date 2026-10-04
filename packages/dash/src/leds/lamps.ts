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
 * **What concerns the whole car is mirrored; what is local is not** (#694). The flags and, on a side
 * of three, the car's own warnings light both ends, because a stopped session or a failing engine is
 * not about one side. The spotter and the indicators light the end they are about, and so do the
 * aids, split by pedal: ABS on the left, which is the brake's side, and traction control, DRS and
 * push to pass on the right, the throttle's. A side of four or more splits the car's warnings the same
 * way, the engine on the left and the fuel on the right. Mirroring every lamp spent half the LEDs of a
 * 3/n/3 saying everything twice, which is why ABS, traction control, the temperature and the fuel used
 * to queue for one LED.
 *
 * Nothing here knows what a condition is beyond the few ids a lamp is narrowed to; the catalogue in
 * `effects.ts` carries the conditions and `rpmStrip.ts` turns a lamp into a container.
 */
import { rightStart, type StripShape } from './strip.ts';

/** What owns a lamp, outermost role first. */
export type LampRole = 'side' | 'race' | 'car' | 'aid';

/** Where a condition lands: on the lamp of one role, or on the whole strip. */
export type EffectRole = LampRole | 'strip';

/** Which end of the strip a side is. */
export type Side = 'left' | 'right';

export interface Lamp {
  /** The role that owns it, and whose conditions rank first on it. */
  role: LampRole;
  /** What it is called in the profile, which is the role except where two lamps share one. */
  label: string;
  /** Every role it draws, highest rank first. A lamp of its own role carries only that. */
  carries: readonly LampRole[];
  /** Where a lamp takes only part of a role, the effect ids it takes of that role. */
  only?: Partial<Record<LampRole, readonly string[]>>;
}

const lamp = (role: LampRole, label: string, carries: readonly LampRole[], only?: Partial<Record<LampRole, readonly string[]>>): Lamp => ({
  role,
  label,
  carries,
  ...(only ? { only } : {}),
});

const SIDE = lamp('side', 'side', ['side']);
const RACE = lamp('race', 'race', ['race']);

/** The aid of the brake pedal, which is the left one: ABS. */
export const BRAKE_AIDS: readonly string[] = ['abs'];
/** The aids of the throttle pedal, the right one: traction control, DRS and push to pass. */
export const THROTTLE_AIDS: readonly string[] = ['tc', 'drs', 'p2p'];
/** The car warnings about the engine, on the left of a side long enough to split them. */
export const ENGINE_WARNINGS: readonly string[] = ['oilPressure', 'temperature'];
/** The car warning about the fuel, on the right. */
export const FUEL_WARNINGS: readonly string[] = ['lowFuel'];

const aidsOf = (side: Side): readonly string[] => (side === 'left' ? BRAKE_AIDS : THROTTLE_AIDS);
const aidLamp = (side: Side): Lamp => lamp('aid', side === 'left' ? 'brake aid' : 'throttle aid', ['aid'], { aid: aidsOf(side) });
const carLamp = (side: Side): Lamp =>
  side === 'left' ? lamp('car', 'engine', ['car'], { car: ENGINE_WARNINGS }) : lamp('car', 'fuel', ['car'], { car: FUEL_WARNINGS });

/**
 * The second aid lamp of a five-LED side, which the canvas gives to DRS and push to pass, on the right,
 * where they are. It is a second view of the throttle's aids rather than a split of them: restricting
 * the first would hide a push to pass behind traction control while a lamp beside it sat dark.
 */
const SECOND_AID_IDS: readonly string[] = ['drs', 'p2p'];

/**
 * The lamps of one side, outermost first.
 *
 * A side of one carries three roles on its one LED, and it used to carry only the outermost. That
 * read well until the grid generated a 1/n/1: a strip with no sides at all gives the flags the whole
 * run, so adding one LED to each end *lost the flags altogether* — a driver who bought a wheel with
 * one lamp a side would have seen a car alongside and never a yellow. Sharing is the rule everywhere
 * below four lamps and this is simply where it ends up: side, then race, then the car's own, with
 * the aids dropped as they are at two.
 *
 * At three the car's warnings are on both inner LEDs and the aids beneath them split by pedal, so a
 * warning lights both ends and an aid one. At four and five the car lamp splits too, the engine left
 * and the fuel right. A five-LED side keeps a second aid lamp on the right for DRS and push to pass;
 * its left has nothing a fifth LED would add, and stays dark rather than repeating a lamp. A side
 * longer than five keeps the five-lamp assignment and leaves the rest dark, because no shape has one
 * and inventing a sixth role for a strip nobody owns would be a lamp with no meaning attached.
 */
export const lampsForSide = (count: number, side: Side): readonly Lamp[] =>
  count <= 0
    ? []
    : count === 1
      ? [lamp('side', 'side, flag and car', ['side', 'race', 'car'])]
      : count === 2
        ? [SIDE, lamp('car', 'car and flag', ['car', 'race'])]
        : count === 3
          ? [SIDE, RACE, lamp('car', side === 'left' ? 'car and brake aid' : 'car and throttle aid', ['car', 'aid'], { aid: aidsOf(side) })]
          : count === 4 || side === 'left'
            ? [SIDE, RACE, carLamp(side), aidLamp(side)]
            : [SIDE, RACE, carLamp(side), aidLamp(side), lamp('aid', 'second aid', ['aid'], { aid: SECOND_AID_IDS })];

/** One lamp of one side of one shape, with the LED it owns. */
export interface PlacedLamp {
  side: Side;
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
  ...lampsForSide(shape.left, 'left').map((l, index) => ({ side: 'left' as const, lamp: l, index, position: 1 + index })),
  ...lampsForSide(shape.right, 'right').map((l, index) => ({ side: 'right' as const, lamp: l, index, position: rightStart(shape) + shape.right - 1 - index })),
];
