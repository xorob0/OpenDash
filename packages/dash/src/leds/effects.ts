/**
 * The effect catalogue: everything a strip shows that is not the rev ladder.
 *
 * Each effect is a row of data rather than a function, so that adding one is adding a row and the
 * whole catalogue can be read in one screen. What each row carries is the *source* — the exact
 * SimHub property, verified against the decompiled 9.12.6 assemblies and recorded in
 * docs/research/simhub-led-sources.md — and an honest note where iRacing does not publish it.
 *
 * **Best effort, where SimHub has a property and iRacing does not fill it.** `TCActive`,
 * `TurnIndicatorLeft`, `TurnIndicatorRight` and `ERSPercent` are real, exposed members of
 * `StatusDataBase`, filled by whichever game reader is running. The iRacing reader overrides the
 * first three with `[NotAvailable] return 0` and never overrides the ERS pair, so on iRacing they
 * are dark — and on a sim that does fill them they light. They ship for that reason.
 *
 * That is a different judgement from the one the second screens make, and deliberately so. The rule
 * in scope.md is that a module which reads something iRacing does not publish **says so rather than
 * drawing a zero**, and it is about a readout: `0.00` on a screen asserts a measurement that was
 * never taken. An LED that stays dark asserts nothing. So an effect whose property exists is shipped
 * and simply does not light, and {@link BEST_EFFORT} records which those are and why.
 *
 * **What is not shipped is what has no property at all**, in any sim: there is no headlight or beam
 * field in `StatusDataBase`, no `KERS` member anywhere in SimHub 9.12.6, and no water pressure. A
 * grep of the decompiled `GameReaderCommon.dll` finds zero of each. {@link NO_PROPERTY} lists them
 * with the nearest thing that does exist.
 */
import { ncalc, leds } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { FLAG_PRIORITY, flagVisible, type FlagProperty } from '../components/flagStrip.ts';
import { ds } from '../tokens.ts';
import { OVER_REV_BLINK_MS } from './ladder.ts';

const { add, and, eq, game, gt, isnull, not, num, or, prop, raw } = ncalc;

/** Where an effect sits on the strip. */
export type EffectPlacement =
  /** The left-hand group of LEDs. */
  | 'left'
  /** The right-hand group. */
  | 'right'
  /** Both side groups at once. */
  | 'sides'
  /** The whole strip, drawn over everything below it. */
  | 'all';

export interface LedEffect {
  id: string;
  /** What it is called in the panel and in the profile. */
  label: string;
  placement: EffectPlacement;
  /** When it lights. */
  when: Expr;
  color: string;
  /** When it blinks, if it does. */
  blinkWhen?: Expr;
  blinkColor?: string;
  blinkDelayMs?: number;
  /**
   * Blank what is underneath rather than composing over it. An effect that takes the whole strip
   * says what it means only if nothing else shows through.
   */
  exclusive?: boolean;
  /** The SimHub property this reads, for the guide and for the test that keeps the two honest. */
  source: string;
}

// --- sources, each verified rather than guessed ------------------------------------------------

/** SimHub's normalised game properties, null-safe as ADR 0003 requires of every read. */
const g = (name: string): Expr => isnull(game(name), num(0));
/** An iRacing raw telemetry var. Absent entirely on a car that does not publish it, hence isnull. */
const t = (name: string): Expr => isnull(raw(name), num(0));
/** `X = 1`, the shape SimHub's own flag and status properties take. */
const on = (name: string): Expr => eq(g(name), num(1));

/** Speed in the unit the user picked, and the pit lane limit in the same one. */
const speedLocal = (): Expr => isnull(game('SpeedLocal'), num(0));
const pitLimit = (): Expr => isnull(game('PitLimiterSpeed'), num(0));

/**
 * Over the pit lane limit. SimHub publishes no speeding property of any kind, so it is composed:
 * in the lane, a limit that is actually known, and above it by more than a tolerance. The
 * tolerance is what stops the light strobing as the limiter settles.
 */
export const PIT_SPEEDING_MARGIN = 1;
const pitSpeeding = (): Expr => and(on('IsInPitLane'), gt(pitLimit(), num(0)), gt(speedLocal(), add(pitLimit(), num(PIT_SPEEDING_MARGIN))));

/** The low-fuel alert SimHub itself computes, which is what the native LED container reads. */
const lowFuel = (): Expr => gt(g('CarSettings_FuelAlertActive'), num(0));

/**
 * The flags, in the order the face ranks them, read through the face's own `flagVisible` so that
 * the box and the screen cannot disagree about which of two live flags wins — the thing #276
 * names as the reason the alert catalogue matters here.
 */
const FLAG_COLORS: Record<FlagProperty, string> = {
  Flag_Black: ds.purpose.flag.black,
  Flag_Checkered: ds.purpose.flag.chequer,
  Flag_Yellow: ds.purpose.flag.yellow,
  Flag_Blue: ds.purpose.flag.blue,
  Flag_White: ds.purpose.flag.white,
  Flag_Green: ds.purpose.flag.green,
};

/** Half period of the yellow flag's flash on a strip, the 2 Hz the band uses. */
export const FLAG_BLINK_MS = 250;

/** One effect per flag, highest priority last so that it composes on top. */
export const flagEffects = (): LedEffect[] =>
  [...FLAG_PRIORITY]
    .reverse()
    .map((flag) => ({
      id: `flag.${flag.replace('Flag_', '').toLowerCase()}`,
      label: `${flag.replace('Flag_', '')} flag`,
      placement: 'all' as const,
      when: flagVisible(flag),
      color: FLAG_COLORS[flag] ?? ds.purpose.flag.white,
      exclusive: true,
      source: `DataCorePlugin.GameData.${flag}`,
      ...(flag === 'Flag_Yellow' ? { blinkWhen: flagVisible(flag), blinkColor: ds.purpose.flag.yellow, blinkDelayMs: FLAG_BLINK_MS } : {}),
    }));

/**
 * The catalogue, in composition order: what is later shows over what is earlier, which is how a
 * strip ranks two live things without a priority field of its own.
 */
export const SIDE_EFFECTS: readonly LedEffect[] = [
  {
    id: 'abs',
    label: 'ABS active',
    placement: 'sides',
    when: gt(g('ABSActive'), num(0)),
    color: ds.color.info.primary,
    source: 'DataCorePlugin.GameData.ABSActive',
  },
  {
    id: 'tc',
    label: 'Traction control',
    placement: 'sides',
    // One light carrying both facts, so it degrades rather than going dark. Steady means the dial is
    // set above zero, which every sim including iRacing fills; blinking means TC is actually cutting
    // in, which iRacing does not publish (GD_TCActive() is [NotAvailable] return 0) and other readers
    // do. On iRacing it is therefore a steady light that never blinks, which is the truth about
    // iRacing rather than a gap.
    when: gt(g('TCLevel'), num(0)),
    color: ds.color.caution.primary,
    blinkWhen: gt(g('TCActive'), num(0)),
    blinkColor: ds.color.caution.primary,
    blinkDelayMs: OVER_REV_BLINK_MS,
    source: 'DataCorePlugin.GameData.TCLevel, TCActive',
  },
  {
    id: 'ers',
    label: 'ERS charge',
    placement: 'sides',
    // ERS is also where KERS lands: SimHub models no KERS of its own — the string does not occur in
    // any of its assemblies — and normalises every hybrid store into this one percentage.
    when: gt(g('ERSPercent'), num(0)),
    color: ds.color.info.primary,
    // Nearly spent, which is the part a driver acts on.
    blinkWhen: and(gt(g('ERSPercent'), num(0)), gt(num(10), g('ERSPercent'))),
    blinkColor: ds.color.danger.primary,
    blinkDelayMs: OVER_REV_BLINK_MS * 2,
    source: 'DataCorePlugin.GameData.ERSPercent',
  },
  {
    id: 'drs',
    label: 'DRS',
    placement: 'sides',
    when: or(on('DRSAvailable'), on('DRSEnabled')),
    color: ds.color.good.primary,
    // Available is a steady light and open is a flashing one, which is how a driver tells them apart.
    blinkWhen: on('DRSEnabled'),
    blinkColor: ds.color.good.primary,
    blinkDelayMs: OVER_REV_BLINK_MS * 2,
    source: 'DataCorePlugin.GameData.DRSAvailable / DRSEnabled',
  },
  {
    id: 'p2p',
    label: 'Push to pass',
    placement: 'sides',
    when: gt(isnull(prop('DataCorePlugin.GameRawData.Telemetry.PlayerP2P_Count'), num(0)), num(0)),
    color: ds.purpose.alert.p2p,
    blinkWhen: eq(isnull(prop('DataCorePlugin.GameData.PushToPassActive'), num(0)), num(1)),
    blinkColor: ds.purpose.alert.p2p,
    blinkDelayMs: OVER_REV_BLINK_MS * 2,
    source: 'DataCorePlugin.GameData.PushToPassActive, GameRawData.Telemetry.PlayerP2P_Count',
  },
  {
    id: 'headlightFlash',
    label: 'Headlight flash',
    placement: 'sides',
    // The only source there is: SimHub normalises nothing for this, and a car without the control
    // does not publish the var at all, so the read has to survive the property being absent.
    when: gt(t('dcHeadlightFlash'), num(0)),
    color: ds.color.neutral.primary,
    source: 'DataCorePlugin.GameRawData.Telemetry.dcHeadlightFlash',
  },
  {
    id: 'lowFuel',
    label: 'Low fuel',
    placement: 'sides',
    when: lowFuel(),
    color: ds.purpose.fuel.low,
    blinkWhen: lowFuel(),
    blinkColor: ds.purpose.fuel.low,
    blinkDelayMs: OVER_REV_BLINK_MS * 4,
    source: 'DataCorePlugin.GameData.CarSettings_FuelAlertActive',
  },
  {
    id: 'waterTemp',
    label: 'Water temperature warning',
    placement: 'sides',
    // iRacing's EngineWarnings bitfield, bit 1. There is no normalised SimHub property for it, and
    // no oil-temperature or water-pressure bit exists at all — see NOT_ON_IRACING.
    when: engineWarning(1),
    color: ds.color.danger.primary,
    source: 'DataCorePlugin.GameRawData.Telemetry.EngineWarnings bit 1 (WaterTempWarning)',
  },
  {
    id: 'oilPressure',
    label: 'Oil pressure warning',
    placement: 'sides',
    when: engineWarning(4),
    color: ds.color.danger.primary,
    source: 'DataCorePlugin.GameRawData.Telemetry.EngineWarnings bit 4 (OilPressureWarning)',
  },
];

/**
 * One bit of iRacing's `EngineWarnings` word, tested arithmetically the way `values.ts` already
 * tests `PitSvFlags`. The raw dictionary stores it as a plain int, so `mod(truncate(x / bit), 2)`
 * is the whole of it.
 */
function engineWarning(bit: number): Expr {
  const { mod, truncate, div } = ncalc;
  return eq(mod(truncate(div(t('EngineWarnings'), num(bit))), num(2)), num(1));
}

/** The spotters, each on the side the car is actually on. */
export const SPOTTER_EFFECTS: readonly LedEffect[] = [
  {
    id: 'spotter.left',
    label: 'Car alongside, left',
    placement: 'left',
    when: gt(g('SpotterCarLeft'), num(0)),
    color: ds.color.caution.primary,
    blinkWhen: and(gt(g('SpotterCarLeft'), num(0)), gt(g('SpotterCarRight'), num(0))),
    blinkColor: ds.color.danger.primary,
    blinkDelayMs: OVER_REV_BLINK_MS * 2,
    source: 'DataCorePlugin.GameData.SpotterCarLeft',
  },
  {
    id: 'spotter.right',
    label: 'Car alongside, right',
    placement: 'right',
    when: gt(g('SpotterCarRight'), num(0)),
    color: ds.color.caution.primary,
    blinkWhen: and(gt(g('SpotterCarLeft'), num(0)), gt(g('SpotterCarRight'), num(0))),
    blinkColor: ds.color.danger.primary,
    blinkDelayMs: OVER_REV_BLINK_MS * 2,
    source: 'DataCorePlugin.GameData.SpotterCarRight',
  },
];

/**
 * The turn indicators, on the side being signalled — the same placement rule as the spotters, and
 * for the same reason: a light about a side belongs on that side.
 *
 * Best effort. `TurnIndicatorLeft` and `TurnIndicatorRight` are real `StatusDataBase` members and
 * the iRacing reader overrides both with `[NotAvailable] return 0`, so on iRacing these are dark and
 * on a sim that fills them they light. Note they are hard zero rather than null, so `isnull()`
 * cannot distinguish "off" from "not published" — which is why this is in {@link BEST_EFFORT}
 * rather than something the profile could detect and report.
 */
export const TURN_EFFECTS: readonly LedEffect[] = [
  {
    id: 'turn.left',
    label: 'Indicating left',
    placement: 'left',
    when: gt(g('TurnIndicatorLeft'), num(0)),
    color: ds.color.caution.primary,
    blinkWhen: gt(g('TurnIndicatorLeft'), num(0)),
    blinkColor: ds.color.caution.primary,
    blinkDelayMs: OVER_REV_BLINK_MS * 4,
    source: 'DataCorePlugin.GameData.TurnIndicatorLeft',
  },
  {
    id: 'turn.right',
    label: 'Indicating right',
    placement: 'right',
    when: gt(g('TurnIndicatorRight'), num(0)),
    color: ds.color.caution.primary,
    blinkWhen: gt(g('TurnIndicatorRight'), num(0)),
    blinkColor: ds.color.caution.primary,
    blinkDelayMs: OVER_REV_BLINK_MS * 4,
    source: 'DataCorePlugin.GameData.TurnIndicatorRight',
  },
];

/** The pit family, which takes the whole strip because it is about where the car is, not what it is doing. */
export const PIT_EFFECTS: readonly LedEffect[] = [
  {
    id: 'pit.lane',
    label: 'In the pit lane',
    placement: 'all',
    when: and(on('IsInPitLane'), not(pitSpeeding())),
    color: ds.purpose.pitLimiter,
    exclusive: true,
    source: 'DataCorePlugin.GameData.IsInPitLane',
  },
  {
    id: 'pit.limiter',
    label: 'Pit limiter on',
    placement: 'all',
    when: on('PitLimiterOn'),
    color: ds.purpose.pitLimiter,
    blinkWhen: on('PitLimiterOn'),
    blinkColor: ds.purpose.pitLimiter,
    blinkDelayMs: OVER_REV_BLINK_MS * 3,
    exclusive: true,
    source: 'DataCorePlugin.GameData.PitLimiterOn',
  },
  {
    id: 'pit.speeding',
    label: 'Speeding in the pit lane',
    placement: 'all',
    // Composed, because SimHub publishes no speeding property: in the lane, a known limit, over it.
    when: pitSpeeding(),
    color: ds.color.danger.primary,
    blinkWhen: pitSpeeding(),
    blinkColor: ds.color.danger.primary,
    blinkDelayMs: OVER_REV_BLINK_MS,
    exclusive: true,
    source: 'IsInPitLane + SpeedLocal + PitLimiterSpeed (SimHub publishes no speeding property)',
  },
];

/**
 * Effects that ship and are dark on iRacing, because the property exists and the iRacing reader does
 * not fill it. On a sim that does, they light — untested there, in the same sense the dashboards are
 * untested on another sim, and shipped on the same reasoning.
 *
 * An LED that stays dark asserts nothing, which is why this is a different judgement from the one a
 * readout gets: `0.00` on a screen claims a measurement nobody took.
 */
export const BEST_EFFORT: readonly { effect: string; property: string; reason: string }[] = [
  {
    effect: 'TC intervening',
    property: 'DataCorePlugin.GameData.TCActive',
    reason: 'IRacingManager.GD_TCActive() is [NotAvailable] and returns 0. The light is steady on the dial and blinks on the intervention, so on iRacing it is steady and never blinks.',
  },
  {
    effect: 'Turn indicators',
    property: 'DataCorePlugin.GameData.TurnIndicatorLeft / TurnIndicatorRight',
    reason: 'GD_TurnIndicatorLeft/Right are [NotAvailable] and return 0 — hard zero rather than null, so isnull() cannot tell "off" from "not published".',
  },
  {
    effect: 'ERS charge, and KERS with it',
    property: 'DataCorePlugin.GameData.ERSPercent',
    reason: 'The iRacing reader overrides neither GD_ERSMax nor GD_ERSStored, so ERSPercent is always 0 there. SimHub models no KERS of its own and normalises every hybrid store into this one percentage.',
  },
];

/**
 * What has no property at all, in any sim, with the nearest thing that does exist. These are not
 * refusals so much as absences: there is nothing to bind, in either the normalised layer or
 * iRacing's own.
 */
export const NO_PROPERTY: readonly { effect: string; reason: string; nearest: string }[] = [
  {
    effect: 'Headlights on, off, low or high beam',
    reason: 'StatusDataBase has no headlight, light or beam member — a grep of the decompiled GameReaderCommon.dll finds zero — and iRacing publishes no such variable. The only "Headlights" string in SimHub.Plugins.dll is a controller button role.',
    nearest: 'The flash-to-pass toggle, GameRawData.Telemetry.dcHeadlightFlash, which is shipped.',
  },
  {
    effect: 'Water pressure',
    reason: 'No StatusDataBase member and no iRacing variable. iRacing publishes oil pressure and water temperature, and no coolant pressure.',
    nearest: "The water temperature warning bit of iRacing's EngineWarnings, which is shipped, and the raw WaterLevel in litres.",
  },
  {
    effect: 'Oil temperature warning',
    reason: "iRacing's EngineWarnings word has a water-temperature bit and an oil-pressure bit and no oil-temperature bit.",
    nearest: 'GameData.OilTemperature, thresholded by whoever wants the lamp.',
  },
  {
    effect: 'Distance or time to the pit box',
    reason: 'No SimHub property and no iRacing variable; any figure would be an estimate rather than a reading.',
    nearest: 'The pit lane and limiter effects, which say where the car is rather than how far it has to go.',
  },
];

/** Every effect the catalogue ships, in composition order: later shows over earlier. */
export const ALL_EFFECTS = (): LedEffect[] => [...SIDE_EFFECTS, ...TURN_EFFECTS, ...SPOTTER_EFFECTS, ...PIT_EFFECTS, ...flagEffects()];

/** One effect as a container, over the run of LEDs its placement gives it. */
export const effectContainer = (effect: LedEffect, startPosition: number, ledCount: number): leds.LedContainer => ({
  kind: 'customStatus',
  description: effect.label,
  startPosition,
  ledCount,
  color: effect.color,
  enabledFormula: { expression: effect.when },
  ...(effect.blinkWhen ? { blinkFormula: { expression: effect.blinkWhen }, blinkColor: effect.blinkColor ?? effect.color, blinkDelayMs: effect.blinkDelayMs } : {}),
});
