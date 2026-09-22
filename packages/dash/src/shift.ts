/**
 * The shift model: when a shift light comes on, and why. ADR 0014.
 *
 * iRacing publishes, per car, the RPMs its own shift light runs on, and no code path in SimHub
 * reads them — so `CarSettings_RPMShiftLight1/2`, which ADR 0004 built the rev bar on, are
 * SimHub's idea of the car's lights rather than the car's. This module is the one place the four
 * property names appear, and the one definition the rev bar, the rev arc, the flag box's gear, the
 * companion's redline readout and every generated LED profile read, so that a strip and a screen in
 * the same rig light at the same instant for the same reason — and so that a number printed beside
 * a bar is the number that bar turns red at.
 *
 * Two ladders, chosen per frame rather than per install: `mirror` when the car publishes a usable
 * set of thresholds, `simhub` (ADR 0004's behaviour, unchanged) when it does not. Which one is in
 * use is visible as which layer is visible — see `revSegments.ts`.
 *
 * There is a third, and it is the one exception to everything said above: the measured tables of
 * ADR 0018, whose bands arrive already computed because 85 files of per-gear thresholds are not
 * something an expression can read. It is offered to the flag box's digit alone and only when that
 * panel is set to it, and it falls back to the two above; see `carLadderStageEntered` at the foot
 * of this file.
 *
 * Two ways to ask. Something that lights a row of segments asks per segment, with
 * `mirrorStageLit` / `simhubStageLit` and the choice made structurally as two layers. Something
 * with a single thing to colour asks `stageEntered` and `overRevEither`, and something with a
 * number to print asks `redlineRpm`; all three carry the same choice inside one expression. They are the same thresholds either way, which is the whole point
 * of the module — and the flash is one of those thresholds rather than a property of the top band,
 * because it begins above the band and stops in the last gear.
 */
import { ncalc } from './generator.ts';
import type { Expr } from './bind.ts';
import { CAR_LADDER_OVER_REV, CAR_LADDER_STAGE, propertyName } from './contract.ts';

const { prop, game, raw, gt, ge, eq, mul, sub, num, isnull, and, or, not, max, iff } = ncalc;

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

/**
 * How many forward gears the car has, from the same DriverInfo block as the four RPMs. Used only
 * to find the gear there is nothing to shift out of.
 */
export const GEAR_COUNT_PROPERTY = `${DRIVER_INFO}DriverCarGearNumForward`;

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

/**
 * Top gear: the gear there is nothing to shift out of. Read from iRacing's own numeric gear rather
 * than `[Gear]`, which is a string ("N", "R", "1"), and false whenever the car does not say how
 * many gears it has — so a car that publishes no count keeps flashing as it did.
 *
 * ADR 0014, amended for #284.
 */
export const lastGear = (): Expr => {
  const count = isnull(prop(GEAR_COUNT_PROPERTY), num(0));
  return and(gt(count, num(0)), ge(isnull(raw('Gear'), num(0)), count));
};

/**
 * Over-rev: the flash, under the car's own ladder — and not in the last gear, where a flash is an
 * instruction that cannot be followed. The top band stays lit, so the bar still says the engine is
 * at its limit; it just stops asking for a shift that does not exist. SimHub's own `RPMSegments`
 * container carries `BlinkOnLastGear` for the same reason.
 */
export const mirrorOverRev = (): Expr => and(ge(rpms(), blinkRpm()), not(lastGear()));

/**
 * The same flash under SimHub's bands: ADR 0004's redline flag, with the same exception on it.
 *
 * The exception belongs to the flash rather than to either ladder: a gear there is nothing to shift
 * out of is a fact about the car, and where the thresholds were read from has no bearing on it. A
 * car that publishes zeros for the four RPMs falls here (see `mirrorAvailable`), and before this
 * it flashed for ever in the last gear, on the digit, the bar and the strip alike. `simhubStageLit`
 * reads `simhubRedline` directly and is left alone, so the top band still lights.
 */
export const simhubOverRev = (): Expr => and(simhubRedline(), not(lastGear()));

/**
 * Segment `local` of `count` in one of SimHub's two bands: the band's progress times its segment
 * count passed the segment's index, and reduced to the entry test for the first segment, where the
 * comparison is against zero. The same reduction `bandLit` makes, for the same reason — the entry
 * test is the thing anything with only one item to colour asks for, and it should be one string.
 */
const bandProgressLit = (which: 1 | 2, local: number, count: number): Expr =>
  local === 0 ? gt(band(which), num(0)) : gt(mul(band(which), num(count)), num(local));

/**
 * Stage `stage`, segment `local` of `count`, under SimHub's own bands — ADR 0004 unchanged, and
 * what a car that publishes nothing still draws.
 */
export const simhubStageLit = (stage: number, local: number, count: number): Expr =>
  stage === 0 ? bandProgressLit(1, local, count) : stage === 1 ? bandProgressLit(2, local, count) : simhubRedline();

/**
 * Whether the engine has entered stage `stage` (0, 1, 2) at all, under the car's own ladder. A
 * band is entered the moment its first segment lights, so this is that segment's test and nothing
 * more — which is why the segment count does not appear in it.
 */
export const mirrorStageEntered = (stage: number): Expr => mirrorStageLit(stage, 0, 1);

/** The same under SimHub's bands: a band progress value leaving zero, and the flag for the top. */
export const simhubStageEntered = (stage: number): Expr => simhubStageLit(stage, 0, 1);

/**
 * The two ladders as one expression: the car's own where it publishes one, SimHub's bands where it
 * does not.
 *
 * The rev bar, the rev arc and the strip make that choice *structurally*, as two layers or two
 * conditional groups whose visibility is bound to the same test, because a layer can be looked at
 * in Dash Studio and is how somebody debugging a car finds out which ladder it is on. Something
 * that has only one thing to colour — the gear on the flag box — has no second layer to put the
 * fallback in, so it takes the same choice inside the expression instead. Same test, same frame,
 * same answer.
 */
export const eitherLadder = (mirror: Expr, simhub: Expr): Expr => or(and(mirrorAvailable(), mirror), and(not(mirrorAvailable()), simhub));

/**
 * Which shift band the engine is in, whichever ladder the car is on: the one definition ADR 0014
 * promised, for a consumer that asks the question once rather than lighting a row of segments.
 */
export const stageEntered = (stage: number): Expr => eitherLadder(mirrorStageEntered(stage), simhubStageEntered(stage));

/**
 * Whether the engine is over-revving, whichever ladder the car is on: the flash, as one
 * expression, for the same kind of consumer `stageEntered` serves.
 *
 * It is emphatically *not* the same question as `stageEntered(2)`. Under the car's own ladder the
 * top band is entered at `Last` and the flash begins at `max(Blink, Last)`, and it stops in the
 * last gear; a consumer that flashes on the band instead flashes early and keeps flashing where
 * the bar deliberately does not. That divergence is what #284's review found on the flag box,
 * and it is why the flash is a definition here rather than a boolean each surface interprets.
 */
export const overRevEither = (): Expr => eitherLadder(mirrorOverRev(), simhubOverRev());

/**
 * SimHub's own redline RPM, and the only place `CarSettings_CurrentGearRedLineRPM` is spelled.
 *
 * It is the one number SimHub publishes that is an RPM rather than a band progress or a flag, and
 * with stock settings it is `DriverCarRedLine * 95/100` — one value, identical in every gear. Its
 * name says per gear and on iRacing that is only true when the user has turned
 * `CarSettings_RPMRedLinePerGearOverride` on by hand, which is rung 2 of `leds/shiftPoints.ts` and
 * is not built. Read here as a plain fallback redline and as nothing per-gear.
 */
export const simhubRedlineRpm = (): Expr => isnull(game('CarSettings_CurrentGearRedLineRPM'), num(0));

/**
 * The RPM the top band lights at, as a number to print rather than a threshold to compare. The
 * same definition and the same frame as `mirrorStageLit(2, …)`: under the car's own ladder the top
 * band is entered at `Last`, so `Last` is what a readout beside that bar has to say.
 *
 * Under SimHub's bands there is no such number to print — ADR 0004's fallback is two progress
 * values and a `RedLineReached` flag, none of which is an RPM — so the fallback is SimHub's own
 * redline, which is the nearest thing it publishes. That is the one place where the printed number
 * and the bar's red point are not the same quantity, and it is a property of what SimHub exposes
 * rather than a second model: a car on the fallback ladder publishes no ladder to print.
 *
 * ADR 0014's promise applied to a readout. Before this, the companion's speedo printed
 * `CarSettings_CurrentGearRedLineRPM` unconditionally, directly under a bar that went red at
 * `DriverCarSLLastRPM`, so a driver saw two different answers to the same question at once.
 */
export const redlineRpm = (): Expr => iff(mirrorAvailable(), lastRpm(), simhubRedlineRpm());

/**
 * One of SimHub's two band progress values, null-safe.
 *
 * The `isnull()` is not decoration. With no sim running these are null, and an expression that
 * throws is caught by `CustomStatusContainer.IsActiveBase`, which then returns its default of
 * `1.0` — so an LED reading a bare band lights up when the sim is closed. On a screen the same
 * failure is merely an unlit segment, which is why it went unnoticed until the strip.
 */
const band = (which: 1 | 2): Expr => isnull(game(`CarSettings_RPMShiftLight${which}`), num(0));

/** Redline reached, under SimHub's own bands. The flash of ADR 0004. */
export const simhubRedline = (): Expr => eq(isnull(game('CarSettings_RPMRedLineReached'), num(0)), num(1));

/**
 * The car's own measured bar, as the plugin publishes it: which of its three bands the engine is
 * in, and whether it is past that gear's redline.
 *
 * A third ladder, and the only one that is not an expression. The thresholds are 85 files of them
 * (ADR 0018), one row per gear, so there is nothing an NCalc string could read and the arithmetic
 * is the plugin's; what arrives here is its answer. `-1` is the absence of one — no tables on the
 * rig, no table for this car, or nothing on the rig asking for them — and it is what makes the
 * fallback the same fallback everything else has, which is {@link stageEntered}.
 *
 * It is deliberately *not* folded into {@link eitherLadder}. The rev bar, the arc and the strips
 * draw the car's whole bar rather than its bands, which they already do from the same tables by
 * mirroring it LED for LED; the bands are what something with a single thing to colour needs, and
 * the flag box's digit is the one surface asking.
 */
const carLadderStage = (): Expr => isnull(prop(propertyName(CAR_LADDER_STAGE)), num(-1));

/** Whether the car's own bands are being published this frame, which is the gate on reading them. */
export const carLadderAvailable = (): Expr => ge(carLadderStage(), num(0));

/** Whether the engine has entered band `stage` (0, 1, 2) of the car's own bar. */
export const carLadderStageEntered = (stage: number): Expr => ge(carLadderStage(), num(stage + 1));

/**
 * Over-rev on the car's own redline for the gear it is in — a separate threshold from the top band
 * here as everywhere, and with the same exception on it: never in the last gear, where a flash asks
 * for a shift that does not exist.
 *
 * The 47 measured cars in 85 that publish no flash at all report false at any RPM, because the
 * plugin answers this from the same `OverRev` the strips mirror: a flash the car never gives is a
 * warning OpenDash invented.
 */
export const carLadderOverRev = (): Expr => and(eq(isnull(prop(propertyName(CAR_LADDER_OVER_REV)), 'false'), 'true'), not(lastGear()));

/** `a` where `wanted` holds and `b` where it does not, for two ladders answering one question. */
export const eitherOf = (wanted: Expr, a: Expr, b: Expr): Expr => or(and(wanted, a), and(not(wanted), b));
