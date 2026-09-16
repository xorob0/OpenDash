/**
 * The car's own bar, on a strip.
 *
 * This is the profile half of [ADR 0017](../../../../docs/decisions/0017-the-cars-own-lights.md), and
 * it is deliberately the thin half. Nothing here knows how many LEDs a car has, what colour they
 * are, what order they light in or how fast they flash: the plugin knows all of that, from a table
 * it fetches, and publishes one run of colours per frame. What is generated is a row of
 * `DynamicColor` containers that each take one colour out of that run.
 *
 * So the pattern vocabulary a driver would name — meet in the middle, left to right, three blocks,
 * one at a time, all red — appears nowhere in this file and nowhere in the generated profile. It is
 * the numbers in the table, and the strip draws whatever they say.
 *
 * The screens do not read this yet, and that is a gap rather than a shape: the rev bar and the rev
 * arc still draw openDash's tokens at the published ladder's thresholds, so a strip and a face in
 * one rig now disagree about a car the table covers. #353 decides what a screen should do with a
 * car's colours; the mechanism is the property below either way.
 *
 * **Why a colour formula rather than a lit/unlit formula.** `CustomStatus`, which every other effect
 * in `rpmStrip.ts` uses, has one colour chosen at build time and an expression that says whether to
 * show it. A mirrored LED changes colour with the car, so the colour is the thing that has to be
 * computed — which is `DynamicColor`, whose `ColorFormula` SimHub evaluates as a string and parses
 * with `ColorConverter`.
 */
import { leds, ncalc } from '../generator.ts';
import { MIRROR_COLOR_WIDTH, MIRROR_RUN_LENGTHS, setting } from '../contract.ts';

/**
 * Whether the plugin can mirror a run of this length.
 *
 * A length the plugin does not publish is a strip with no mirror, and silently: the profile would
 * read a property nobody attaches. The build refuses instead — `leds.test.ts` checks the shapes
 * against {@link MIRROR_RUN_LENGTHS}, and this is the assertion at the point of use.
 */
export const canMirror = (count: number): boolean => MIRROR_RUN_LENGTHS.includes(count);

/**
 * One `DynamicColor` per LED, each reading its own colour out of the run the plugin publishes.
 *
 * One container per LED rather than one `ScriptedContent` for the whole run, for the reason
 * `rpmStrip.ts` gives for the ladder: a Javascript body is not something the generator can validate,
 * and an NCalc expression is. The cost is fourteen objects in a file nobody reads by hand.
 */
export const mirrorRun = (count: number): leds.LedContainer[] => {
  if (!canMirror(count)) {
    throw new RangeError(`no mirrored run of ${count} LEDs: add it to MIRROR_RUN_LENGTHS on both sides of the contract, or the strip silently loses its mirror`);
  }
  return Array.from({ length: count }, (_, k) => ({
    kind: 'dynamicColor' as const,
    description: `car ${String(k + 1).padStart(2, '0')}`,
    startPosition: k + 1,
    ledCount: 1,
    colorFormula: { expression: setting.ledMirrorAt(count, k) },
  }));
};

/**
 * The `car` style: the car's own bar where there is one, and `fallback` where there is not.
 *
 * The two are siblings under one gate rather than one tree with a condition in it, because that is
 * how a driver debugging their strip finds out which they are on — the same reason the two ladders
 * below are two layers (ADR 0014). `LedMirrorReady` is false for every way there can be no mirror:
 * no plugin, no tables fetched yet, no entry for this car, an entry that would not read, or a car
 * the driver has not started. All five look the same from here, which is what makes the fallback
 * total.
 *
 * The mirror clears the background because it owns the whole run: an LED the car does not light is
 * dark, not whatever the layer underneath would have put there.
 */
export const carCentre = (count: number, fallback: readonly leds.LedContainer[]): leds.LedContainer[] => [
  {
    kind: 'conditionalGroup',
    description: "the car's own lights",
    trigger: { expression: setting.ledMirrorReady() },
    clearBackgroundWhenActive: true,
    children: mirrorRun(count),
  },
  {
    kind: 'conditionalGroup',
    description: 'no table for this car: the ladder iRacing publishes',
    trigger: { expression: ncalc.not(setting.ledMirrorReady()) },
    children: [...fallback],
  },
];

/** How wide one colour is in a published run, re-exported so a test can slice one the way SimHub does. */
export { MIRROR_COLOR_WIDTH };
