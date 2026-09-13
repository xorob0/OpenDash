/**
 * The effect catalogue: everything a strip shows that is not the rev ladder.
 *
 * Each effect is a row of data rather than a function, so that adding one is adding a row and the
 * whole catalogue can be read in one screen. What each row carries is the *source* — the exact
 * SimHub property, verified against the decompiled 9.12.6 assemblies and recorded in
 * docs/research/simhub-led-sources.md — and an honest note where iRacing does not publish it.
 *
 * **Effects openDash refuses to draw, because iRacing does not publish them.** `TCActive` is
 * `[NotAvailable] return 0` in the iRacing reader, so a "TC intervening" light would be dark for
 * ever; `TurnIndicatorLeft/Right` are the same, and are hard zero rather than null, so not even
 * `isnull()` can tell that they are missing; `ERSPercent`, `ERSStored` and `ERSMax` are always 0
 * because the reader never overrides them; `KERS` does not exist in SimHub at all, in any sim.
 * Each of those is listed in {@link NOT_ON_IRACING} rather than shipped as a light that never
 * comes on, which is the same rule the second screens follow for a module iRacing cannot feed.
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
 * the box and the screen cannot disagree about which of two live flags wins — the thing XOR-225
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
    label: 'Traction control set',
    placement: 'sides',
    // Not "TC intervening": IRacingManager.GD_TCActive() is [NotAvailable] return 0, so the
    // intervention light would never come on. This is the dial being set above zero, which is real.
    when: gt(g('TCLevel'), num(0)),
    color: ds.color.caution.primary,
    source: 'DataCorePlugin.GameData.TCLevel',
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
 * What openDash will not draw on iRacing, and why. Each of these is a light a competitor shows and
 * openDash deliberately does not, because on iRacing it could only ever be dark — which is worse
 * than absent, since a dark light reads as "not happening" rather than "not known".
 */
export const NOT_ON_IRACING: readonly { effect: string; reason: string }[] = [
  { effect: 'TC intervening', reason: 'IRacingManager.GD_TCActive() is [NotAvailable] and returns 0. The TC light shows the dial instead.' },
  { effect: 'Turn indicators', reason: 'GD_TurnIndicatorLeft/Right are [NotAvailable] and return 0 — hard zero, not null, so isnull() cannot even detect the absence.' },
  { effect: 'ERS and battery charge', reason: 'ERSPercent, ERSStored and ERSMax exist but the iRacing reader never overrides them, so all three are always 0.' },
  { effect: 'KERS', reason: 'Does not exist in SimHub 9.12.6 at all, in any sim.' },
  { effect: 'Headlights on/off and beam', reason: 'No SimHub property and no iRacing variable. Only the flash-to-pass toggle exists.' },
  { effect: 'Water pressure', reason: 'No SimHub property and no iRacing variable; iRacing publishes oil pressure and water temperature, not water pressure.' },
  { effect: 'Oil temperature warning', reason: "iRacing's EngineWarnings word has a water-temp bit and an oil-pressure bit, and no oil-temp bit." },
  { effect: 'Distance to the pit box', reason: 'No SimHub property and no iRacing variable; any figure would be an estimate rather than a reading.' },
];

/** Every effect the catalogue ships, in composition order: later shows over earlier. */
export const ALL_EFFECTS = (): LedEffect[] => [...SIDE_EFFECTS, ...SPOTTER_EFFECTS, ...PIT_EFFECTS, ...flagEffects()];

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
