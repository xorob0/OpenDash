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
 * has to move, then the correct state last: `Pit limiter` is information and everything above it is
 * a mistake. The pit alerts are not ranked against the race alerts, because the two draw in
 * different rectangles and never contend: band D holds the flag while this rectangle holds the pit
 * state, and a driver serving a stop under a full course yellow needs both. In the full-screen flag
 * format the block does cover this rectangle, and the face draws the pit alert over it deliberately,
 * for the same reason.
 *
 * **The ignition and the stall are shared with the alert catalogue, across the pit entry line.** In
 * the lane they are this family's, and out of it they are the catalogue's first two entries, drawn on
 * band D (`IGNITION_OFF` and `ENGINE_OFF` in flags.ts). Both read `ignitionOff` and `engineStopped`
 * from `second/values.ts` and split on the same `isInPitLane()`, so one condition is never drawn in
 * both places. The catalogue's half also asks whether anybody is in the car, since on the circuit
 * SimHub's reading of the ignition is off whenever nobody is.
 *
 * Two of `alertBand`'s five shapes, at the artboards' border: a filled band for the correct state,
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
import { withMoreBindings, type Expr } from '../bind.ts';
import { ALERT_BAND_BORDER } from './alertBand.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { engineStopped, ignitionOff, isInPitLane, pitLimiterOn } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { PIT_LIMITER_BLINK_MS } from './pitLimiter.ts';

const { and, isNull, not, raw } = ncalc;

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

/** One state of the pit family: what it says, how it is dressed and when it is out. */
export interface PitAlertSpec {
  id: string;
  /** Drawn as it is written, in sentence case like every label on the face. */
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
 * The ignition sits above the stopped engine because it is the switch the driver can move, and
 * because the two are raised together whenever it is off: SimHub's `EngineStarted` is 0 whenever its
 * ignition is, so the chain shows the one that names what to do about it.
 */
export const PIT_ALERTS: readonly PitAlertSpec[] = [
  { id: 'ignition', label: 'Ignition off', shape: 'outlined', colour: ds.purpose.alert.power, when: and(isInPitLane(), ignitionOff()) },
  { id: 'engine', label: 'Engine off', shape: 'outlined', colour: ds.purpose.alert.power, when: and(isInPitLane(), engineStopped()) },
  { id: 'engage', label: 'Engage limiter', shape: 'outlined', colour: ds.purpose.pitLimiter, when: and(isInPitLane(), not(pitLimiterOn()), hasLimiter()) },
  { id: 'disengage', label: 'Disengage limiter', shape: 'outlined', colour: ds.purpose.pitLimiter, when: and(pitLimiterOn(), not(isInPitLane())) },
  { id: 'limiter', label: 'Pit limiter', shape: 'filled', colour: ds.purpose.pitLimiter, when: and(pitLimiterOn(), isInPitLane()), blinkMs: PIT_LIMITER_BLINK_MS },
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
    return withMoreBindings({
      kind: 'layer',
      name,
      children,
      ...(spec.blinkMs === undefined ? {} : { blink: { enabled: true, delayMs: spec.blinkMs } }),
    }, { Visible: pitAlertVisible(spec.id) });
  });
}
