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
import { DEFAULTS, LED_CENTRES, LED_RPM_STYLES, setting } from '../contract.ts';
import type { LedCentre, LedRpmStyle } from '../contract.ts';
import { mirrorAvailable } from '../shift.ts';
import { bandOf, bandSpan, ladderColors, ladderOrder, overRev, OVER_REV_COLOR, rungLit, stepLit, type Ladder } from './ladder.ts';
import { brake as brakeInput, fuelPercent, throttle as throttleInput } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { ALL_EFFECTS, BLINK_OFF, FAST_BLINK_MS, SLOW_BLINK_MS, effectContainer, lampConditions, type LedEffect } from './effects.ts';
import { lampsOf, type PlacedLamp } from './lamps.ts';
import { SHIFT_TABLE, tabledGear, tabledOverRev, tabledStageLit } from './shiftPoints.ts';
import { centreStart, deviceLength, reversedPositions, rightStart, stripLength, type StripShape } from './strip.ts';

const { and, eq, gt, not, num, str } = ncalc;

/** `isnull([OpenDash.LedCentre], 'rpm') = '<which>'`, the gate on each centre function. */
const centreIs = (which: LedCentre): Expr => eq(setting.ledCentre(), str(which));

/** `isnull([OpenDash.LedRpmStyle], 'leftToRight') = '<which>'`, the gate on each style. */
const styleIs = (which: LedRpmStyle): Expr => eq(setting.ledRpmStyle(), str(which));

/**
 * The condition of a container whose group has already decided it. A `CustomStatus` must carry an
 * `EnabledFormula`, so there is no way to write "always" but to write it; repeating the group's own
 * trigger in every child would be the same expression spelled `count` times and would read as a
 * second decision.
 */
const TRUE: Expr = 'true';

/** The rev ladder over `count` LEDs, in one style, under one of the two ladders. */
const rungs = (count: number, style: LedRpmStyle, which: Ladder): leds.LedContainer[] => {
  const order = ladderOrder(style, count);
  const colors = ladderColors(style);
  return Array.from({ length: count }, (_, k) => {
    const rung = order.rungOf(k);
    return {
      kind: 'customStatus' as const,
      description: `rev ${String(k + 1).padStart(2, '0')}`,
      startPosition: k + 1,
      ledCount: 1,
      color: colors[bandOf(rung, order.rungs)] ?? colors[2],
      enabledFormula: { expression: rungLit(which, rung, order.rungs) },
    };
  });
};

/**
 * Over-rev: the whole run in one colour, flashing, over whatever the rungs beneath it were drawing.
 *
 * It is a layer rather than a property of the rungs, and that is three corrections in one shape.
 * Over-rev is a state of the bar and not of its top band, so every style says it the same way and a
 * driver who changes style changes the ladder's look and not what it tells them. It is one colour —
 * {@link OVER_REV_COLOR} — rather than each rung's own, so the bar reads as having turned rather
 * than as having brightened in places. And the off phase is {@link BLINK_OFF} rather than the
 * colour itself: `StaticColorContainerBase` alternates `Color` with `BlinkingColor`, both fields
 * held one hex on a flashing rung, and the over-rev flash had therefore never flashed at all.
 *
 * `clearBackgroundWhenActive` because a bar that is over-revving is not also a ladder part way up,
 * and the rate is the catalogue's fast one, so over-rev is the same urgency on the centre that oil
 * pressure is on a lamp. The gate is the trigger alone; each LED is then unconditionally its
 * colour, which is what one LED of a bar that has all turned one colour is.
 */
const overRevLayer = (count: number, when: Expr): leds.LedContainer => ({
  kind: 'conditionalGroup',
  description: 'over-rev',
  trigger: { expression: when },
  clearBackgroundWhenActive: true,
  children: Array.from({ length: count }, (_, k) => ({
    kind: 'customStatus' as const,
    description: `over-rev ${String(k + 1).padStart(2, '0')}`,
    startPosition: k + 1,
    ledCount: 1,
    color: OVER_REV_COLOR,
    enabledFormula: { expression: TRUE },
    blinkFormula: { expression: TRUE },
    blinkColor: BLINK_OFF,
    blinkDelayMs: FAST_BLINK_MS,
  })),
});

/** One derived ladder: its rungs, and the over-rev layer that takes the bar from them. */
const ladderLayers = (count: number, style: LedRpmStyle, which: Ladder): leds.LedContainer[] => [...rungs(count, style, which), overRevLayer(count, overRev(which))];

/**
 * The measured overrides, one `Groups.CustomConditionalGroup` per car and gear the table covers.
 *
 * These come *after* the two derived ladders so that they compose over them: a car in the table
 * gets its measured gear, and every other car and gear keeps the ladder iRacing publishes. That is
 * the "derived by default, table overrides" of XOR-233, and it is why an empty table costs nothing
 * — no entries, no containers, no change to any profile.
 *
 * A car keyed here is matched on `CarModel` rather than by a `Groups.GameCarModelGroup`, because
 * the native group keys on SimHub's own car-choice model and the table keys on the model string a
 * contributor can read off the property list.
 */
const tabledOverrides = (count: number, style: LedRpmStyle): leds.LedContainer[] =>
  Object.entries(SHIFT_TABLE).flatMap(([model, car]) =>
    Object.entries(car.gears).map(([gear, points]) => {
      const order = ladderOrder(style, count);
      const colors = ladderColors(style);
      return {
        kind: 'conditionalGroup' as const,
        description: `${car.name}, gear ${gear}`,
        trigger: { expression: tabledGear(model, gear) },
        clearBackgroundWhenActive: true,
        children: [
          ...Array.from({ length: count }, (_, k) => {
            const rung = order.rungOf(k);
            const band = bandOf(rung, order.rungs);
            const span = bandSpan(band, order.rungs);
            return {
              kind: 'customStatus' as const,
              description: `rev ${String(k + 1).padStart(2, '0')}`,
              startPosition: k + 1,
              ledCount: 1,
              color: colors[band] ?? colors[2],
              enabledFormula: { expression: tabledStageLit(points, band, rung - span.start, span.count) },
            };
          }),
          // Inside the measured group rather than beside it, so that a car in the table over-revs on
          // its own measured blink RPM and never on the one its ladder publishes.
          overRevLayer(count, tabledOverRev(points)),
        ],
      };
    }),
  );

/**
 * The centre, as the shift ladder: one conditional group per style, and inside each the two
 * ladders — exactly as the rev bar draws its `shift` state as two layers. (The bar has a third,
 * the plain RPM bar; a strip has no equivalent, because a strip that is not showing revs is
 * showing one of the other centres.) Whichever is active is the ladder the car is on, which is how
 * the strip is debugged.
 */
const revCentre = (count: number): leds.LedContainer[] =>
  LED_RPM_STYLES.map((style) => ({
    kind: 'conditionalGroup' as const,
    description: `style: ${style}`,
    trigger: { expression: styleIs(style) },
    children: [
      {
        kind: 'conditionalGroup' as const,
        description: "the car's own shift lights",
        trigger: { expression: mirrorAvailable() },
        children: ladderLayers(count, style, 'mirror'),
      },
      {
        kind: 'conditionalGroup' as const,
        description: "SimHub's bands, for a car that publishes no ladder",
        trigger: { expression: not(mirrorAvailable()) },
        children: ladderLayers(count, style, 'simhub'),
      },
      // Last, so a measured gear composes over whichever ladder was derived for the car.
      ...tabledOverrides(count, style),
    ],
  }));

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
 * the centre, throttle the right. An odd centre gives brake the extra LED, since a driver watching
 * this is watching the brake.
 */
const throttleBrakeBar = (count: number): leds.LedContainer[] => {
  const brakeCount = Math.ceil(count / 2);
  const throttleCount = count - brakeCount;
  const brake = Array.from({ length: brakeCount }, (_, k) => ({
    kind: 'customStatus' as const,
    description: `brake ${String(k + 1).padStart(2, '0')}`,
    // Fills outwards: the LED nearest the middle is the first to light.
    startPosition: brakeCount - k,
    ledCount: 1,
    color: ds.color.danger.primary,
    enabledFormula: { expression: stepLit(brakeInput(), k, brakeCount) },
  }));
  const throttle = Array.from({ length: throttleCount }, (_, k) => ({
    kind: 'customStatus' as const,
    description: `throttle ${String(k + 1).padStart(2, '0')}`,
    startPosition: brakeCount + 1 + k,
    ledCount: 1,
    color: ds.color.good.primary,
    enabledFormula: { expression: stepLit(throttleInput(), k, throttleCount) },
  }));
  return [...brake, ...throttle];
};

/**
 * The fuel gauge: a bar that empties, and blinks below five percent. `FuelPercent` is SimHub's own,
 * so there is nothing computed here.
 *
 * Slow, which is what the catalogue's own low-fuel lamp blinks at: one condition cannot be urgent on
 * the centre and merely true on a lamp of the same strip. Its off phase is the low-fuel colour rather
 * than darkness, because here the second colour is the fact being reported.
 */
const fuelBar = (count: number): leds.LedContainer[] => {
  const percent = fuelPercent();
  const low = gt(num(5), percent);
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
 * The sides: brake, and only under the default centre. Under any other centre they stay dark rather
 * than being filled with something the driver did not ask for.
 */
const sides = (shape: StripShape): leds.LedContainer[] => {
  if (shape.left === 0 && shape.right === 0) return [];
  const brake = brakeInput();
  const side = (start: number, count: number, label: string): leds.LedContainer => ({
    kind: 'group',
    description: `${label} side`,
    startPosition: start,
    children: Array.from({ length: count }, (_, k) => ({
      kind: 'customStatus' as const,
      description: `${label} brake ${String(k + 1).padStart(2, '0')}`,
      startPosition: k + 1,
      ledCount: 1,
      color: ds.color.danger.primary,
      enabledFormula: { expression: stepLit(brake, k, count) },
    })),
  });
  return [
    {
      kind: 'conditionalGroup',
      description: 'sides: brake, under the default centre only',
      trigger: { expression: centreIs('rpm') },
      children: [
        ...(shape.left > 0 ? [side(1, shape.left, 'left')] : []),
        ...(shape.right > 0 ? [side(rightStart(shape), shape.right, 'right')] : []),
      ],
    },
  ];
};

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
        children: ranked.map((effect, i) => effectContainer(effect, 1, 1, ranked.slice(0, i).map((higher) => higher.when))).reverse(),
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
    children: [effectContainer(effect, 1, stripLength(shape))],
  });
  const lamps = [...placed].sort((a, b) => b.index - a.index).flatMap(lampGroup);
  const whole = ALL_EFFECTS().filter((e) => e.role === 'strip' || (placed.length === 0 && e.role === 'race'));
  return [...lamps, ...whole.map(wholeRun)];
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
  ...sides(shape),
  // After the rev ladder and the brake sides, so that it composes over them.
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
 * The profile for one strip shape. A reversed strip is the same tree inside a `Groups.RemapGroup`
 * that turns logical positions into physical ones, which is the whole reason a new device is a row
 * of numbers rather than a second profile.
 */
export function rpmStripProfile(shape: StripShape, profileId: string): leds.LedProfile {
  const length = deviceLength(shape);
  // Everything is inside a GameRunningGroup, and that is a correctness fix rather than tidiness.
  // Every native Status.* container tests GameRunning itself; CustomStatusContainer does not, and
  // its IsActiveBase catches a throwing expression and returns its default of 1.0 — so with the sim
  // closed, where the properties are null, a bare CustomStatus lights up. One native group gates
  // the lot. What the strip does when the game is NOT running is XOR-249.
  const running: leds.LedContainer = {
    kind: 'raw',
    containerType: 'Groups.GameRunningGroup',
    description: 'only while the sim is running',
    children: treeFor(shape),
  };
  const tree = [running];
  return {
    name: rpmStripProfileName(shape),
    profileId,
    ledCount: length,
    containers: shape.reversed ? [{ kind: 'remapGroup', description: 'wired from the far end', positions: reversedPositions(length), children: tree }] : tree,
  };
}

/** The default the profile falls back to with no plugin installed, stated once for the tests. */
export const DEFAULT_CENTRE: LedCentre = DEFAULTS.LedCentre;
