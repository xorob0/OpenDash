/**
 * The gates that sit above a light's tree, for both kinds of light.
 *
 * A strip and a flag box are one set of lights with two kinds of hardware behind them, and the
 * questions asked above their trees are the same two questions: is the sim running, and is the car
 * switched on. They were answered in two places, and only one of them asked the second question at
 * all — a wheel strip lit the garage while the box beside it showed its standby mark. One
 * definition here is what stops the two from drifting again.
 *
 * The brightness gate is deliberately *not* here. It is a composition of three declared settings
 * and it lives with every other setting read, in `contract.ts` as `flagBox.brightness()`; a second
 * name for it in this file would be the drift this file exists to prevent.
 */
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { ignitionOn } from '../second/values.ts';

const { eq, isnull, num } = ncalc;

/**
 * Whether the car is switched on, **defaulted to on**, which is the reverse of the convention every
 * other read in this project follows.
 *
 * `EngineIgnitionOn` is a normalised `GameData` read and a sim that does not fill it gives null.
 * Defaulted to off, as an unknown normally is, that null would black out every LED openDash drives
 * for the whole of a session in that sim, and a dark strip is indistinguishable from a profile that
 * failed to load. The cost of the other default is a strip that lights in the garage of a sim which
 * publishes no ignition, which is the state of every sim today and is what a driver already has.
 */
const ignition = (): Expr => isnull(ignitionOn(), num(1));

/** The car is switched on: everything a light draws hangs under this. */
export const ignitionIsOn = (): Expr => eq(ignition(), num(1));

/** The car is switched off, which is a real condition rather than the absence of one. */
export const ignitionIsOff = (): Expr => eq(ignition(), num(0));
