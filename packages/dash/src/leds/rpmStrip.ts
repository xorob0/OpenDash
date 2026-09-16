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
import { OVER_REV_BLINK_MS, ladderColors, ladderOrder, overRev, rungFlashes, rungLit, stepLit, type Ladder } from './ladder.ts';
import { brake as brakeInput, fuelPercent, throttle as throttleInput } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { ALL_EFFECTS, effectContainer, type LedEffect } from './effects.ts';
import { SHIFT_TABLE, tabledGear, tabledOverRev, tabledStageLit } from './shiftPoints.ts';
import { carCentre } from './mirror.ts';
import { centreStart, deviceLength, reversedPositions, rightStart, stripLength, type StripShape } from './strip.ts';

const { and, eq, gt, not, num, or, str } = ncalc;

/** `isnull([OpenDash.LedCentre], 'rpm') = '<which>'`, the gate on each centre function. */
const centreIs = (which: LedCentre): Expr => eq(setting.ledCentre(), str(which));

/** `isnull([OpenDash.LedRpmStyle], 'leftToRight') = '<which>'`, the gate on each style. */
const styleIs = (which: LedRpmStyle): Expr => eq(setting.ledRpmStyle(), str(which));

/** The rev ladder over `count` LEDs, in one style, under one of the two ladders. */
const rungs = (count: number, style: LedRpmStyle, which: Ladder): leds.LedContainer[] => {
  const order = ladderOrder(style, count);
  const colors = ladderColors(style);
  return Array.from({ length: count }, (_, k) => {
    const rung = order.rungOf(k);
    const band = Math.min(2, Math.floor((rung * 3) / order.rungs));
    return {
      kind: 'customStatus' as const,
      description: `rev ${String(k + 1).padStart(2, '0')}`,
      startPosition: k + 1,
      ledCount: 1,
      color: colors[band] ?? colors[2],
      enabledFormula: { expression: rungLit(which, rung, order.rungs) },
      ...(rungFlashes(style, rung, order.rungs)
        ? { blinkFormula: { expression: overRev(which) }, blinkColor: colors[2], blinkDelayMs: OVER_REV_BLINK_MS }
        : {}),
    };
  });
};

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
        children: Array.from({ length: count }, (_, k) => {
          const rung = order.rungOf(k);
          const band = Math.min(2, Math.floor((rung * 3) / order.rungs));
          const bandStart = Array.from({ length: order.rungs }, (_, i) => i).findIndex((i) => Math.min(2, Math.floor((i * 3) / order.rungs)) === band);
          const bandCount = Array.from({ length: order.rungs }, (_, i) => i).filter((i) => Math.min(2, Math.floor((i * 3) / order.rungs)) === band).length;
          return {
            kind: 'customStatus' as const,
            description: `rev ${String(k + 1).padStart(2, '0')}`,
            startPosition: k + 1,
            ledCount: 1,
            color: colors[band] ?? colors[2],
            enabledFormula: { expression: tabledStageLit(points, band, rung - bandStart, bandCount) },
            ...(rungFlashes(style, rung, order.rungs)
              ? { blinkFormula: { expression: tabledOverRev(points) }, blinkColor: colors[2], blinkDelayMs: OVER_REV_BLINK_MS }
              : {}),
          };
        }),
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
  LED_RPM_STYLES.map((style) => {
    // `car` is the car's whole bar and is not one of the three looks: its LEDs, its colours, its
    // order and its flash, from the table the plugin fetched (ADR 0017). Where there is no table it
    // falls back to the ladder below, drawn the way `leftToRight` draws it -- which is the same tree
    // the other three styles are, so the fallback is not a fourth thing to maintain.
    const ladder: leds.LedContainer[] = [
      {
        kind: 'conditionalGroup' as const,
        description: "the car's own shift lights",
        trigger: { expression: mirrorAvailable() },
        children: rungs(count, style === 'car' ? 'leftToRight' : style, 'mirror'),
      },
      {
        kind: 'conditionalGroup' as const,
        description: "SimHub's bands, for a car that publishes no ladder",
        trigger: { expression: not(mirrorAvailable()) },
        children: rungs(count, style === 'car' ? 'leftToRight' : style, 'simhub'),
      },
      // Last, so a measured gear composes over whichever ladder was derived for the car.
      ...tabledOverrides(count, style === 'car' ? 'leftToRight' : style),
    ];
    return {
      kind: 'conditionalGroup' as const,
      description: `style: ${style}`,
      trigger: { expression: styleIs(style) },
      children: style === 'car' ? carCentre(count, ladder) : ladder,
    };
  });

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
    blinkDelayMs: OVER_REV_BLINK_MS * 4,
  }));
};

/**
 * The five things the centre can be, each behind its own setting value.
 *
 * `rpm` and `rpmOnly` share one rev tree gated on either, rather than carrying a copy each: they
 * differ only in whether the sides light, which is decided over in {@link sides}. The rev tree is
 * already three styles times two ladders, so a second copy of it would be the largest thing in the
 * file and would say nothing new.
 */
const centreFunctions = (count: number): leds.LedContainer[] => [
  {
    kind: 'conditionalGroup',
    description: 'centre: rpm or rpmOnly',
    trigger: { expression: or(centreIs('rpm'), centreIs('rpmOnly')) },
    children: revCentre(count),
  },
  ...LED_CENTRES.filter((which) => which !== 'rpm' && which !== 'rpmOnly').map((which) => ({
    kind: 'conditionalGroup' as const,
    description: `centre: ${which}`,
    trigger: { expression: centreIs(which) },
    children:
      which === 'brake' ? pedalBar(count, brakeInput(), ds.color.danger.primary, 'brake') : which === 'throttleBrake' ? throttleBrakeBar(count) : fuelBar(count),
  })),
];

/**
 * The sides: brake, and only under the default centre. `rpmOnly` is the setting for somebody who
 * wants the strip to say one thing, so its sides stay dark rather than being filled with something
 * they did not ask for.
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
 * Composition order is the ranking: SimHub merges a container over the ones before it, so an
 * effect later in the list wins a tie. That is how a flag beats a spotter and a spotter beats the
 * rev ladder, without a priority field anywhere.
 *
 * A `sides` effect on a shape with no sides — a brow, a bare run — has nowhere to go. It is
 * dropped rather than moved onto the centre, because a brow that flashes its rev LEDs for ABS is
 * saying the wrong thing with the right light.
 */
const effects = (shape: StripShape): leds.LedContainer[] => {
  const runs = (effect: LedEffect): { start: number; count: number }[] => {
    if (effect.placement === 'all') return [{ start: 1, count: stripLength(shape) }];
    const left = shape.left > 0 ? [{ start: 1, count: shape.left }] : [];
    const right = shape.right > 0 ? [{ start: rightStart(shape), count: shape.right }] : [];
    return effect.placement === 'left' ? left : effect.placement === 'right' ? right : [...left, ...right];
  };
  return ALL_EFFECTS().flatMap((effect) => {
    const placed = runs(effect);
    if (placed.length === 0) return [];
    const containers = placed.map(({ start, count }) => effectContainer(effect, start, count));
    // An exclusive effect blanks what is under it, which is what a conditional group is for.
    return effect.exclusive
      ? [{ kind: 'conditionalGroup' as const, description: effect.label, trigger: { expression: effect.when }, clearBackgroundWhenActive: true, children: containers }]
      : containers;
  });
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
