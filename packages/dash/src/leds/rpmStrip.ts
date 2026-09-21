/**
 * The RPM strip profile: the first `.ledsprofile` openDash emits, and the one that proves the
 * approach. ADR 0013 for why it is generated at all, ADR 0014 for what makes it light.
 *
 * The centre is the mirror. Its two derived ladders are built from the *same* expressions the rev
 * bar is built from — `mirrorStageLit`, `simhubStageLit` and their two-ladder choice in
 * `../shift.ts` — so on those two rungs the strip and the screen do not merely agree, they cannot
 * disagree: there is one definition and both read it. That is what ADR 0014 was settled before this
 * ticket for.
 *
 * The per-gear table is the one thing here that the screen does not read. `tabledOverrides` below
 * is its only caller in the build — `test/leds.test.ts` reads the table too, which is a test rather
 * than a surface — so a car measured into `data/shift-points.json` would move this strip and leave
 * the face, the arc and the flag box on the derived ladder. That is not a property of the model and
 * it is not visible today, because the table ships empty and emits no containers at all; it is
 * recorded here, and in the header of `shiftPoints.ts`, so that the first measured car is not the
 * thing that discovers it.
 *
 * One LED is one `CustomStatus` container rather than one `ScriptedContent` for the whole run.
 * `ScriptedContent` was the obvious route — one Javascript formula returning a colour array — and
 * it was not taken, because the NCalc path keeps three properties this project values: the
 * expressions are the rev bar's own, byte for byte; the generator's validator checks every
 * function name, arity and declared property in them, which it cannot do inside a Javascript
 * body; and a lighting change is a readable diff rather than a changed script. It costs one
 * container per LED, which for a fourteen-LED centre is fourteen objects in a file nobody reads
 * by hand.
 */
import { ncalc, leds } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { DEFAULTS, flagBox, LED_CENTRES, setting } from '../contract.ts';
import type { LedCentre } from '../contract.ts';
import { stepLit } from './ladder.ts';
import { brake as brakeInput, fuelPercent, tankIsLow, throttle as throttleInput } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { ALL_EFFECTS, BLINK_OFF, FAST_BLINK_MS, SLOW_BLINK_MS, SPOTTER_EFFECTS, effectContainers, lampConditions, type LedEffect } from './effects.ts';
import { ignitionIsOn } from './gates.ts';
import { lampsOf, type EffectRole, type PlacedLamp } from './lamps.ts';
import { carCentre } from './mirror.ts';
import { centreStart, deviceLength, stripLength, type StripShape } from './strip.ts';

const { and, eq, not, str } = ncalc;

/** `isnull([OpenDash.LedCentre], 'rpm') = '<which>'`, the gate on each centre function. */
const centreIs = (which: LedCentre): Expr => eq(setting.ledCentre(), str(which));

/**
 * The condition of a container whose group has already decided it. A `CustomStatus` must carry an
 * `EnabledFormula`, so there is no way to write "always" but to write it; repeating the group's own
 * trigger in every child would be the same expression spelled `count` times and would read as a
 * second decision.
 */
const TRUE: Expr = 'true';

/**
 * Where SimHub's own bar begins, as a percentage of the redline it works out for the car.
 *
 * Fifteen points below it, which is about where a measured car's first LED sits: the BMW M4 GT4's
 * is 6450 of a 7050 redline in third, 91 per cent, and the spread over a whole grid is wider than
 * one number can be right about. This is SimHub's bar and not the car's, so it is a reasonable
 * shape rather than a measurement — the car's own bar is the other switch, and it is the one that
 * is exact.
 */
const SIMHUB_BAR_FLOOR = 85;

/**
 * SimHub's own rev bar: one `RPMSegments` container, `count` segments of one LED.
 *
 * This is the whole of what replaced `leftToRight`, `meetInMiddle` and `f1` (#369). Those were
 * openDash emitting one `CustomStatus` per LED per band per ladder -- three styles times two
 * ladders times `count` -- to draw a bar SimHub draws itself from its own per-car settings.
 * `RpmMode.RedlinePercent` means each segment's `StartValue` is a percentage of the redline SimHub
 * has for the car, so the thresholds follow the car without openDash knowing anything about it,
 * and the flash at the redline is SimHub's own.
 *
 * What is lost with them is real and is the trade #369 accepted: SimHub's bar fills left to right
 * and cannot meet in the middle, so a driver who wanted that look now has the car's own bar (where
 * the car has one) or a bar that fills one way. What is gained is that the ladder is SimHub's to
 * maintain, and a profile that was mostly rev containers is now mostly not.
 *
 * The colours are openDash's shift tokens, because `design/tokens.json` is the only place a colour
 * is defined and it has no SimHub-green in it. Whether this bar should carry SimHub's own plain
 * green, amber and red instead is a design-source question and is the author's -- see #369.
 */
const simHubBar = (count: number): leds.LedContainer => ({
  kind: 'rpmSegments',
  description: "SimHub's own rev bar",
  rpmMode: 'redlinePercent',
  blinkDelayMs: FAST_BLINK_MS,
  segments: Array.from({ length: count }, (_, k) => ({
    ledCount: 1,
    // Evenly from the floor to the redline. Rounded to a tenth: SimHub writes the value with the
    // invariant culture and a long fraction is noise in a file somebody may open.
    startValue: Math.round((SIMHUB_BAR_FLOOR + ((100 - SIMHUB_BAR_FLOOR) * k) / count) * 10) / 10,
    // Thirds, the last taking the remainder -- `stageOf` on the screens, kept in step by hand
    // because the screens count segments of a bar and this counts LEDs of a strip.
    color: bandColor(k, count),
  })),
});

/** The three bands of SimHub's bar, in openDash's tokens. See {@link simHubBar}. */
const SIMHUB_BAR_COLORS = [ds.purpose.shift.stage1, ds.purpose.shift.stage2, ds.purpose.shift.stage3] as const;

/** The band a segment belongs to: thirds, the last taking any remainder. */
const bandColor = (k: number, count: number): string => SIMHUB_BAR_COLORS[Math.min(2, Math.floor((k * 3) / count))] ?? SIMHUB_BAR_COLORS[2];

/**
 * The centre, as a rev bar: the car's own where the driver asked for it and the car has one, and
 * SimHub's own everywhere else.
 *
 * Two conditional groups rather than one tree with a condition in it, for the reason every other
 * choice on a strip is two groups: whichever is active in SimHub's own profile editor is the one
 * in use, which is how somebody debugging a strip finds out what they are looking at.
 *
 * Inside the first, `carCentre` makes the second choice -- the car's measured bar where the plugin
 * publishes one, and the same SimHub bar where it does not, which is what makes "the car's own"
 * safe to leave on for a driver whose car nobody has measured.
 */
const revCentre = (count: number): leds.LedContainer[] => [
  {
    kind: 'conditionalGroup',
    description: "the car's own rev bar",
    trigger: { expression: setting.ledCarRevBar() },
    children: carCentre(count, [simHubBar(count)]),
  },
  {
    kind: 'conditionalGroup',
    description: "SimHub's own rev bar",
    trigger: { expression: not(setting.ledCarRevBar()) },
    children: [simHubBar(count)],
  },
];

/** A progressive bar of `count` LEDs in one colour, driven by a 0..100 telemetry percentage. */
const pedalBar = (count: number, value: Expr, color: string, label: string): leds.LedContainer[] =>
  Array.from({ length: count }, (_, k) => ({
    kind: 'customStatus' as const,
    description: `${label} ${String(k + 1).padStart(2, '0')}`,
    startPosition: k + 1,
    ledCount: 1,
    color,
    enabledFormula: { expression: stepLit(value, k, count) },
  }));

/**
 * Throttle and brake filling outwards from the middle: brake takes the left half, running out from
 * the centre, throttle the right.
 *
 * An odd centre keeps the two halves equal and spends the spare LED on the middle, lit white and
 * lit always. Giving it to brake instead — which is what this did — made the two pedals read at
 * different scales on half the shapes, so a foot flat on each filled one side one LED further than
 * the other and the bar was never symmetrical about anything. A standing mark is also the only way
 * a driver can see where the middle *is* when neither pedal is down.
 */
const throttleBrakeBar = (count: number): leds.LedContainer[] => {
  const middle = count % 2 === 1 ? 1 : 0;
  const half = (count - middle) / 2;
  const brake = Array.from({ length: half }, (_, k) => ({
    kind: 'customStatus' as const,
    description: `brake ${String(k + 1).padStart(2, '0')}`,
    // Fills outwards: the LED nearest the middle is the first to light.
    startPosition: half - k,
    ledCount: 1,
    color: ds.color.danger.primary,
    enabledFormula: { expression: stepLit(brakeInput(), k, half) },
  }));
  const centre: leds.LedContainer[] =
    middle === 1
      ? [
          {
            kind: 'customStatus' as const,
            description: 'centre mark',
            startPosition: half + 1,
            ledCount: 1,
            color: ds.color.text.primary,
            enabledFormula: { expression: TRUE },
          },
        ]
      : [];
  const throttle = Array.from({ length: half }, (_, k) => ({
    kind: 'customStatus' as const,
    description: `throttle ${String(k + 1).padStart(2, '0')}`,
    startPosition: half + middle + 1 + k,
    ledCount: 1,
    color: ds.color.good.primary,
    enabledFormula: { expression: stepLit(throttleInput(), k, half) },
  }));
  return [...brake, ...centre, ...throttle];
};

/**
 * The fuel gauge: a bar that empties, and blinks once the tank is low. The *height* is
 * `FuelPercent`, which is SimHub's own; the *threshold* is {@link tankIsLow}, the laps remaining
 * against the one number in laps the driver set.
 *
 * The bar used to raise itself at five percent of the tank, which was a third answer to "am I low"
 * beside the box's and the lamp's. Five percent is also not a threshold a driver can act on: it is
 * two laps in one car and half a lap in another, which is the whole reason the setting is in laps.
 *
 * Slow, which is what the catalogue's own low-fuel lamp blinks at: one condition cannot be urgent on
 * the centre and merely true on a lamp of the same strip. Its off phase is the low-fuel colour rather
 * than darkness, because here the second colour is the fact being reported.
 */
const fuelBar = (count: number): leds.LedContainer[] => {
  const percent = fuelPercent();
  const low = tankIsLow();
  return Array.from({ length: count }, (_, k) => ({
    kind: 'customStatus' as const,
    description: `fuel ${String(k + 1).padStart(2, '0')}`,
    startPosition: k + 1,
    ledCount: 1,
    color: ds.purpose.fuel.nominal,
    enabledFormula: { expression: stepLit(percent, k, count) },
    blinkFormula: { expression: and(low, stepLit(percent, k, count)) },
    blinkColor: ds.purpose.fuel.low,
    blinkDelayMs: SLOW_BLINK_MS,
  }));
};

/** The four things the centre can be, each behind its own setting value. */
const centreFunctions = (count: number): leds.LedContainer[] => [
  {
    kind: 'conditionalGroup',
    description: 'centre: rpm',
    trigger: { expression: centreIs('rpm') },
    children: revCentre(count),
  },
  ...LED_CENTRES.filter((which) => which !== 'rpm').map((which) => ({
    kind: 'conditionalGroup' as const,
    description: `centre: ${which}`,
    trigger: { expression: centreIs(which) },
    children:
      which === 'brake' ? pedalBar(count, brakeInput(), ds.color.danger.primary, 'brake') : which === 'throttleBrake' ? throttleBrakeBar(count) : fuelBar(count),
  })),
];

/**
 * The effect catalogue placed on a shape.
 *
 * Every condition that is not the pit family lands on one lamp of one LED, which `lamps.ts`
 * allocates and which is the whole of the ranking: two live conditions on one lamp are resolved by
 * the lamp's own order rather than by which of them happens to sit later in the catalogue, and two
 * conditions on different lamps no longer contend at all.
 *
 * The lamps are emitted innermost first, so the side lamp — the outermost, and the only thing a
 * side can say that the centre cannot — is written last on each side and nothing composed above it
 * can take it.
 *
 * A condition whose lamp does not exist on this shape is dropped rather than moved onto the centre,
 * because a brow that flashes its rev LEDs for ABS is saying the wrong thing with the right light.
 * The flags are the exception: a shape with no lamps keeps them over the whole run, blanking it, as
 * every shape did before the lamps arrived. Deriving lamps at the ends of a bare run would reverse
 * a recorded decision (docs/research/lights-review.md), so a brow keeps what it has until that
 * decision is made.
 */
const effects = (shape: StripShape): leds.LedContainer[] => {
  const placed = lampsOf(shape);
  const lampGroup = ({ side, lamp, position }: PlacedLamp): leds.LedContainer[] => {
    const ranked = lampConditions(lamp, side);
    if (ranked.length === 0) return [];
    return [
      {
        kind: 'group',
        description: `${side} ${lamp.label} lamp`,
        startPosition: position,
        // Reversed by rank and then flattened, so that a flag's moving and held containers stay
        // beside each other in the file rather than at opposite ends of the lamp.
        children: ranked
          .map((effect, i) => effectContainers(effect, 1, 1, ranked.slice(0, i).map((higher) => higher.when)))
          .reverse()
          .flat(),
      },
    ];
  };
  // The whole run, blanked: what a conditional group is for, and what the canvas allows the pit
  // family alone.
  const wholeRun = (effect: LedEffect): leds.LedContainer => ({
    kind: 'conditionalGroup',
    description: effect.label,
    trigger: { expression: effect.when },
    clearBackgroundWhenActive: true,
    children: effectContainers(effect, 1, stripLength(shape)),
  });
  const lamps = [...placed].sort((a, b) => b.index - a.index).flatMap(lampGroup);
  // A car alongside over the whole run, when the driver has asked for that. The lamp version below
  // it is left exactly as it is: the group blanks its background when it triggers, so it paints over
  // the lamp rather than needing the lamp to know about it, and with the switch off it never
  // triggers at all. Under the pit family, which is the one thing nothing paints over.
  const spotterWhole = placed.length === 0
    ? []
    : SPOTTER_EFFECTS.map((effect) => ({
        kind: 'conditionalGroup' as const,
        description: `${effect.label}, whole strip`,
        trigger: { expression: and(eq(setting.ledSpotterWhole(), 'true'), effect.when) },
        clearBackgroundWhenActive: true,
        children: effectContainers(effect, 1, stripLength(shape)),
      }));
  // On a shape with no sides the flags take the whole run, and so does a car alongside: there is no
  // end to put a lamp on, so a strip with no sides drew flags and never a spotter at all -- which the
  // grid found the moment a brow stopped being a category and became 0/n/0. The spotter is drawn
  // after the flags rather than before, so it composes over them: that is the lamp model's own
  // ranking, where the outermost LED belongs to what is happening beside the car and nothing paints
  // it out.
  const wholeRoles: readonly EffectRole[] = placed.length === 0 ? ['strip', 'race', 'side'] : ['strip'];
  const whole = wholeRoles.flatMap((role) => ALL_EFFECTS().filter((e) => e.role === role));
  const ranked = [...whole.filter((e) => e.role !== 'strip'), ...whole.filter((e) => e.role === 'strip')];
  return [...lamps, ...spotterWhole, ...ranked.map(wholeRun)];
};

/** What a strip profile is called, in SimHub's profile list. Keeps the slashes: `openDash 4/14/4`. */
export const rpmStripProfileName = (shape: StripShape): string => `openDash ${shape.label}`;

/**
 * What the file is called. The shape's id rather than its label, because a label is `4/14/4` and a
 * slash is a path separator on every platform the build runs on.
 */
export const rpmStripFileName = (shape: StripShape): string => `openDash ${shape.id}`;

/** The whole tree for one shape, before any reversal is applied. */
const treeFor = (shape: StripShape): leds.LedContainer[] => [
  { kind: 'group', description: 'centre', startPosition: centreStart(shape), children: centreFunctions(shape.centre) },
  // After the rev ladder, so that it composes over it. The sides carry nothing but lamps: a brake
  // gradient used to fill them under the default centre, and a group filled red by the pedal is a
  // group on which an oil warning cannot come on. A driver who wants a pedal trace asks for one,
  // through the brake and throttleBrake centres, and gets it where it can be read.
  ...effects(shape),
  // The 3/10/3's two further runs of nine repeat the centre, so a device with three strips says the
  // same thing on all three rather than leaving two of them dark.
  ...(shape.extraRuns
    ? Array.from({ length: shape.extraRuns.count }, (_, i) => ({
        kind: 'group' as const,
        description: `extra run ${i + 1}`,
        startPosition: stripLength(shape) + i * shape.extraRuns!.length + 1,
        children: centreFunctions(shape.extraRuns!.length),
      }))
    : []),
];

/**
 * The rig's brightness, over everything a strip draws.
 *
 * `LightsBrightness`, `LightsNightBrightness` and `LightsNightMode` are named for the rig rather
 * than for one device, and the panel captions them "for every light openDash drives"; until this
 * container existed that sentence was untrue, because the only reader of the composed expression
 * was the flag box (`leds/profile.ts`), so a wheel strip and a brow ignored all three. Both
 * artefacts now read the one `flagBox.brightness()`, so day, night and the switch resolve in a
 * single place and the two cannot drift apart.
 *
 * `Groups.BrightnessFormulaGroup` is the strip's container of that kind and is one of the fifty-six
 * SimHub 9.12.6 resolves. It goes through `raw` because the generator models only the containers a
 * profile has needed so far, and `BrightnessFormula` is the field name its matrix sibling carries,
 * which was read off a real file; the strip spelling is the same by SimHub's own convention but has
 * not been seen on a strip, so it is the thing to look at first if a profile loads and ignores the
 * setting.
 */
const brightnessGroup = (children: readonly leds.LedContainer[]): leds.LedContainer => ({
  kind: 'raw',
  containerType: 'Groups.BrightnessFormulaGroup',
  description: 'the rig brightness, day or night',
  fields: { BrightnessFormula: leds.buildExpressionObject({ expression: flagBox.brightness() }) },
  children,
});

/**
 * The car switched on, over everything a strip draws.
 *
 * The box has asked this since it shipped and the strip never did, so a driver sitting in the
 * garage with the car off had a dark flag box beside a wheel drawing a pit-lane state, a brake
 * gradient and whatever else had a non-zero property. {@link ignitionIsOn} is the box's own gate,
 * read from one place by both.
 *
 * Unlike the box the strip goes fully dark rather than showing a standby mark: a mark on a strip is
 * a lit LED, and an LED lit to mean "off" is the confusion this gate exists to remove.
 */
const ignitionGroup = (children: readonly leds.LedContainer[]): leds.LedContainer => ({
  kind: 'conditionalGroup',
  description: 'only while the car is switched on',
  trigger: { expression: ignitionIsOn() },
  children,
});

/**
 * The profile for one strip shape. A strip the maker wired in some other order is the same tree
 * inside a `Groups.RemapGroup` that turns logical positions into physical ones, which is the whole
 * reason a new device is a row of numbers rather than a second profile.
 */
export function rpmStripProfile(shape: StripShape, profileId: string): leds.LedProfile {
  const length = deviceLength(shape);
  // Everything is inside a GameRunningGroup, and that is a correctness fix rather than tidiness.
  // Every native Status.* container tests GameRunning itself; CustomStatusContainer does not, and
  // its IsActiveBase catches a throwing expression and returns its default of 1.0 — so with the sim
  // closed, where the properties are null, a bare CustomStatus lights up. One native group gates
  // the lot. What the strip does when the game is NOT running is #300.
  // Brightness sits under the running gate rather than over it: nothing outside that gate paints,
  // so a brightness group above it would scale nothing and would only cost an evaluation with the
  // sim closed.
  // The ignition gate is the innermost of the three, because it is the only one of them a driver
  // turns on and off within a session. None of the three carries a startPosition: the fit rule in
  // the generator accumulates offsets down the tree, so a group that carried one would move every
  // LED beneath it.
  const running: leds.LedContainer = {
    kind: 'raw',
    containerType: 'Groups.GameRunningGroup',
    description: 'only while the sim is running',
    children: [brightnessGroup([ignitionGroup(treeFor(shape))])],
  };
  const tree = [running];
  return {
    name: rpmStripProfileName(shape),
    profileId,
    ledCount: length,
    containers: shape.positions ? [{ kind: 'remapGroup', description: 'the order this device is wired in', positions: shape.positions, children: tree }] : tree,
  };
}

/** The default the profile falls back to with no plugin installed, stated once for the tests. */
export const DEFAULT_CENTRE: LedCentre = DEFAULTS.LedCentre;
