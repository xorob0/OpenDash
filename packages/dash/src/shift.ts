/**
 * The shift model: when a shift light comes on, and why. ADR 0014.
 *
 * iRacing publishes, per car, the RPMs its own shift light runs on, and no code path in SimHub
 * reads them — so `CarSettings_RPMShiftLight1/2`, which ADR 0004 built the rev bar on, are
 * SimHub's idea of the car's lights rather than the car's. This module is the one place the four
 * property names appear, and the one definition the rev bar, the rev arc and every generated LED
 * profile read, so that a strip and a screen in the same rig light at the same instant for the
 * same reason.
 *
 * Two ladders, chosen per frame rather than per install: `mirror` when the car publishes a usable
 * set of thresholds, `simhub` (ADR 0004's behaviour, unchanged) when it does not. Which one is in
 * use is visible as which layer is visible — see `revSegments.ts`.
 */
import { ncalc } from './generator.ts';
import type { Expr } from './bind.ts';

const { prop, game, gt, ge, eq, mul, sub, num, isnull, and, max } = ncalc;

/** Where the session string puts the driver's own car. The same nested path `incidentLimit` reads. */
const DRIVER_INFO = 'DataCorePlugin.GameRawData.SessionData.DriverInfo.';

/**
 * The four RPMs iRacing publishes for the car's own shift light, and the only place their names
 * appear in the source. `first` is where the first light comes on, `shift` is the sim telling you
 * to shift, `last` is where the last light comes on, `blink` is over-rev.
 */
export const SHIFT_RPM_PROPERTIES = {
  first: `${DRIVER_INFO}DriverCarSLFirstRPM`,
  shift: `${DRIVER_INFO}DriverCarSLShiftRPM`,
  last: `${DRIVER_INFO}DriverCarSLLastRPM`,
  blink: `${DRIVER_INFO}DriverCarSLBlinkRPM`,
} as const;

/** Engine speed now. */
export const rpms = (): Expr => isnull(game('Rpms'), num(0));

const read = (name: string): Expr => isnull(prop(name), num(0));

/** First light on. */
export const firstRpm = (): Expr => read(SHIFT_RPM_PROPERTIES.first);

/** Last light on. */
export const lastRpm = (): Expr => read(SHIFT_RPM_PROPERTIES.last);

/** Shift now. Between `first` and `last` for every car that passes `mirrorAvailable`. */
export const shiftRpm = (): Expr => read(SHIFT_RPM_PROPERTIES.shift);

/** Over-rev, never below the last light, because a car may publish a ladder and no blink RPM. */
export const blinkRpm = (): Expr => max(read(SHIFT_RPM_PROPERTIES.blink), lastRpm());

/**
 * Whether the car published a usable ladder this frame, which is also what makes every expression
 * below safe: a first light above zero, a last light above it, and a shift RPM between the two so
 * that no band can be inverted. A band may still be zero-width — a car whose shift RPM is its last
 * light is ordinary — and its segments then light together rather than never lighting.
 *
 * Another sim, a session that has not started and a car that publishes zeros all fail this, and
 * the SimHub ladder is used instead. It is evaluated once per layer rather than once per segment,
 * which is what keeps the per-segment expressions short.
 */
export const mirrorAvailable = (): Expr =>
  and(gt(firstRpm(), num(0)), gt(lastRpm(), firstRpm()), ge(shiftRpm(), firstRpm()), ge(lastRpm(), shiftRpm()));

/**
 * Segment `local` of `count` in a band running `from` to `to`, lit on the same rule the SimHub
 * ladder uses: the band's progress times its segment count passed the segment's index. Written as
 * a cross-multiplication so that a zero-width band divides by nothing, and reduced to the entry
 * test for the first segment of a band, where the comparison is against zero.
 */
const bandLit = (from: Expr, to: Expr, local: number, count: number): Expr =>
  local === 0 ? gt(rpms(), from) : and(gt(rpms(), from), gt(mul(sub(rpms(), from), num(count)), mul(num(local), sub(to, from))));

/**
 * Stage `stage` (0, 1, 2), segment `local` of `count`, under the car's own ladder. Nothing is lit
 * below the first light; the first band runs first to shift, the second shift to last, and the
 * third lights together at the last light.
 */
export const mirrorStageLit = (stage: number, local: number, count: number): Expr =>
  stage === 0 ? bandLit(firstRpm(), shiftRpm(), local, count) : stage === 1 ? bandLit(shiftRpm(), lastRpm(), local, count) : ge(rpms(), lastRpm());

/** Over-rev: the flash, under the car's own ladder. */
export const mirrorOverRev = (): Expr => ge(rpms(), blinkRpm());

/**
 * Stage `stage`, segment `local` of `count`, under SimHub's own bands — ADR 0004 unchanged, and
 * what a car that publishes nothing still draws.
 */
export const simhubStageLit = (stage: number, local: number, count: number): Expr =>
  stage === 0
    ? gt(mul(game('CarSettings_RPMShiftLight1'), num(count)), num(local))
    : stage === 1
      ? gt(mul(game('CarSettings_RPMShiftLight2'), num(count)), num(local))
      : simhubRedline();

/** Redline reached, under SimHub's own bands. The flash of ADR 0004. */
export const simhubRedline = (): Expr => eq(game('CarSettings_RPMRedLineReached'), num(1));
