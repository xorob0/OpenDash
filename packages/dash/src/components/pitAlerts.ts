/**
 * pitAlerts: the second alert engine, drawn in the rectangle the limiter banner already had.
 *
 * The face used to draw one band with one condition, `PitLimiterOn = 1`, which meant that the
 * limiter left on down the pit straight and the limiter correctly engaged in the lane were the same
 * drawing. The canvas asks for a pit family instead, and this is it: five states ranked among
 * themselves, sharing one rectangle so that two of them can never draw at once and no geometry
 * moves.
 *
 * **The lane gate is per entry and not on the list.** The canvas calls the pit alerts "a separate
 * engine, evaluated only in the pit lane", and that cannot be read literally, because the state it
 * cares most about is the limiter still on *after* leaving the lane. Four of the five carry
 * `isInPitLane()`; the fifth is by definition outside it.
 *
 * **The ranking is what is stopping the car, worst first.** A dead engine, then a switch the driver
 * has to move, then the correct state last: `PIT LIMITER` is information and everything above it is
 * a mistake. The pit alerts are not ranked against the race alerts, because the two draw in
 * different rectangles and never contend: band D holds the flag while this rectangle holds the pit
 * state, and a driver serving a stop under a full-course caution needs both. In the full-screen flag
 * format the block does cover this rectangle, and the face draws the pit alert over it deliberately,
 * for the same reason.
 *
 * Two of `alertBand`'s three shapes, at the artboards' border: a filled band for the correct state,
 * outlined bands for the four that are not. `purpose.pitLimiter` and `purpose.flag.white` are both
 * `#FFFFFF`, so the mistake and the correct state differ in shape rather than in colour, which is
 * the same reasoning the flag box writes down for the same pair of states.
 *
 * The name is drawn here rather than through `alertBand`'s own, because a band of the flag
 * catalogue sets its name in 700 and nothing else on a face may: `faceWeights.test.ts` holds the
 * face to the gear and the flag name alone. A pit alert keeps the 500 the limiter banner has always
 * been set in.
 */
import type { Hex, Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { ALERT_BAND_BORDER } from './alertBand.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { isInPitLane } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { PIT_LIMITER_BLINK_MS } from './pitLimiter.ts';

const { and, div, eq, game, gt, isNull, isnull, mod, not, num, raw, truncate } = ncalc;

/** The limiter is engaged. Null-safe, so a sim that publishes nothing draws no pit alert at all. */
const limiterOn = (): Expr => eq(isnull(game('PitLimiterOn'), num(0)), num(1));

/**
 * This car has a limiter to engage.
 *
 * `PitLimiterOn` is one bit of iRacing's `EngineWarnings` and reads 0 on a car that has no limiter,
 * which is indistinguishable from a limiter switched off: a reminder read off that alone would nag
 * every lap in the lane of a car that cannot comply. `dcPitSpeedLimiterToggle` is the in-car control
 * itself and is simply absent on a car without one, which is the same presence test the bar's strip
 * cells use for traction control and ABS.
 */
const hasLimiter = (): Expr => not(isNull(raw('dcPitSpeedLimiterToggle')));

/**
 * The ignition is off.
 *
 * The `isnull` default is 1 and that is the whole point of it: a sim that does not publish
 * `EngineIgnitionOn` would otherwise draw the alarm for ever. The flag box answers the same
 * condition with a dim standby mark rather than a band, because a dark box has to be distinguishable
 * from a profile that failed to load; a face has no such constraint and says it in words.
 */
const ignitionOff = (): Expr => eq(isnull(game('EngineIgnitionOn'), num(1)), num(0));

/**
 * The engine has stopped: bit 8 of iRacing's `EngineWarnings` word, `irsdk_EngineWarnings`'s
 * stalled bit, tested arithmetically because that is the only way NCalc can test a bit.
 *
 * Written here rather than shared with `leds/effects.ts` and `zones/telltales.ts`, which each carry
 * the same three operations for their own bits: the three are one line each and lifting them would
 * mean a fourth file before anything reads two of them.
 */
const engineStalled = (): Expr => gt(mod(truncate(div(isnull(raw('EngineWarnings'), num(0)), num(8))), num(2)), num(0));

/** One state of the pit family: what it says, how it is dressed and when it is out. */
export interface PitAlertSpec {
  id: string;
  /** Drawn upper-cased, as every band on the face is. */
  label: string;
  /** Filled for the state that is correct, outlined for the four that are not. */
  shape: 'filled' | 'outlined';
  colour: Hex;
  /** True while it is out. The exclusion chain adds the higher states' negations. */
  when: Expr;
  /** Half period of the band's blink, for the one state that blinks. */
  blinkMs?: number;
}

/**
 * The pit family, highest priority first.
 *
 * The ignition sits above the stalled engine because it is the switch the driver can move, and
 * because iRacing may well raise both when a driver kills the engine in the box: where it does, the
 * chain shows the one that names what to do about it. That has not been checked against a running
 * sim, so the order is the safe reading rather than a measured one.
 */
export const PIT_ALERTS: readonly PitAlertSpec[] = [
  { id: 'ignition', label: 'Ignition off', shape: 'outlined', colour: ds.purpose.alert.power, when: and(isInPitLane(), ignitionOff()) },
  { id: 'engine', label: 'Engine off', shape: 'outlined', colour: ds.purpose.alert.power, when: and(isInPitLane(), engineStalled()) },
  { id: 'engage', label: 'Engage limiter', shape: 'outlined', colour: ds.purpose.pitLimiter, when: and(isInPitLane(), not(limiterOn()), hasLimiter()) },
  { id: 'disengage', label: 'Disengage limiter', shape: 'outlined', colour: ds.purpose.pitLimiter, when: and(limiterOn(), not(isInPitLane())) },
  { id: 'limiter', label: 'Pit limiter', shape: 'filled', colour: ds.purpose.pitLimiter, when: and(limiterOn(), isInPitLane()), blinkMs: PIT_LIMITER_BLINK_MS },
];

/** `spec.when` and no higher state's condition: the chain band D and the pop-ups both rank with. */
export function pitAlertVisible(id: string): Expr {
  const index = PIT_ALERTS.findIndex((a) => a.id === id);
  const spec = PIT_ALERTS[index];
  if (!spec) throw new Error(`pitAlerts: no alert ${id}`);
  return and(...PIT_ALERTS.slice(0, index).map((a) => not(a.when)), spec.when);
}

/**
 * One state's band: filled in its colour with the name knocked out, or the face's own ground
 * outlined and named in it. An outlined band is opaque like a filled one, because zone A is under
 * it and the band is there to be read rather than to tint what it covers.
 */
function pitAlertBand(name: string, frame: Rect, spec: PitAlertSpec): Item[] {
  const filled = spec.shape === 'filled';
  return [
    band(`${name}.band`, frame, filled ? spec.colour : ds.color.surface.base, filled ? {} : { border: { color: spec.colour, width: ALERT_BAND_BORDER } }),
    label(`${name}.label`, spec.label, frame.left, frame.top + (frame.height - ds.size.label) / 2, frame.width, {
      color: filled ? ds.color.surface.base : spec.colour,
      hAlign: 'center',
    }),
  ];
}

/** The whole family over one rectangle, one layer each. */
export function pitAlerts(frame: Rect, prefix = 'pitAlert'): Item[] {
  return PIT_ALERTS.map((spec) => {
    const name = `${prefix}.${spec.id}`;
    const children = pitAlertBand(name, frame, spec);
    return {
      kind: 'layer',
      name,
      children,
      ...(spec.blinkMs === undefined ? {} : { blink: { enabled: true, delayMs: spec.blinkMs } }),
      ...withBindings({ Visible: pitAlertVisible(spec.id) }),
    };
  });
}
