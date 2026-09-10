/**
 * The settings contract between the dashboard and the plugin: property names, defaults, value
 * sets and the card catalogue. Every expression that reads an `OpenDash.*` property goes through
 * `setting`, so `declaredProperties()` is the single list the validator checks against.
 * plugin/OpenDash/Contract.cs mirrors this file.
 */
import { ncalc } from './generator.ts';
import type { Expr } from './bind.ts';

const { isnull, prop, str, num } = ncalc;

export const PROPERTY_PREFIX = 'OpenDash';

/** The largest slot count any layout declares; the plugin exposes exactly this many slot settings. */
export const SLOT_MAX = 12;

export type PositionMode = 'overall' | 'class';
export type DeltaReference = 'session' | 'alltime';
export type SessionProgress = 'auto' | 'laps' | 'time';

export const POSITION_MODES: readonly PositionMode[] = ['overall', 'class'];
export const DELTA_REFERENCES: readonly DeltaReference[] = ['session', 'alltime'];
export const SESSION_PROGRESS_MODES: readonly SessionProgress[] = ['auto', 'laps', 'time'];

export const DEFAULTS = {
  ShiftLights: true,
  PositionMode: 'overall' as PositionMode,
  DeltaReference: 'session' as DeltaReference,
  SessionProgress: 'auto' as SessionProgress,
} as const;

/** `Slot01` .. `Slot12` for a 1-based slot index. */
export function slotSettingName(slot: number): string {
  assertSlot(slot);
  return `Slot${String(slot).padStart(2, '0')}`;
}

/** The default card number of a 1-based slot: cards 0..11 fill slots 1..12 in order. */
export function defaultCardForSlot(slot: number): number {
  assertSlot(slot);
  return slot - 1;
}

/** `OpenDash.<name>`, the full SimHub property name. */
export const propertyName = (name: string): string => `${PROPERTY_PREFIX}.${name}`;

/** Every property the plugin exposes, in the order the plugin attaches them. */
export function declaredProperties(): string[] {
  const fixed = ['ShiftLights', 'PositionMode', 'DeltaReference', 'SessionProgress'];
  const slots = Array.from({ length: SLOT_MAX }, (_, i) => slotSettingName(i + 1));
  return [...fixed, ...slots].map(propertyName);
}

function assertSlot(slot: number): void {
  if (!Number.isInteger(slot) || slot < 1 || slot > SLOT_MAX) throw new RangeError(`slot must be 1..${SLOT_MAX}, got ${slot}`);
}

/**
 * NCalc reads of the settings, each wrapped in `isnull(...)` with the default so that the
 * dashboard behaves without the plugin.
 */
export const setting = {
  /** `isnull([OpenDash.ShiftLights], true)` */
  shiftLights: (): Expr => isnull(prop(propertyName('ShiftLights')), String(DEFAULTS.ShiftLights)),
  /** `isnull([OpenDash.PositionMode], 'overall')` */
  positionMode: (): Expr => isnull(prop(propertyName('PositionMode')), str(DEFAULTS.PositionMode)),
  /** `isnull([OpenDash.DeltaReference], 'session')` */
  deltaReference: (): Expr => isnull(prop(propertyName('DeltaReference')), str(DEFAULTS.DeltaReference)),
  /** `isnull([OpenDash.SessionProgress], 'auto')` */
  sessionProgress: (): Expr => isnull(prop(propertyName('SessionProgress')), str(DEFAULTS.SessionProgress)),
  /** `isnull([OpenDash.Slot0i], default card number)` for a 1-based slot. */
  slot: (slot: number): Expr => isnull(prop(propertyName(slotSettingName(slot))), num(defaultCardForSlot(slot))),
};

export interface CardMeta {
  /** The value a slot setting takes. */
  number: number;
  /** Module id; also the screen name in cards.djson. */
  id: string;
  /** The label as drawn on the card. Bound labels show their default here. */
  label: string;
  /** Display name in the plugin's slot pickers. */
  displayName: string;
}

/** The twelve MVP cards in card-number order. The plugin ships the same list in Contract.cs. */
export const CARD_CATALOGUE: readonly CardMeta[] = [
  { number: 0, id: 'currentLap', label: 'CURRENT', displayName: 'Current lap' },
  { number: 1, id: 'lastLap', label: 'LAST', displayName: 'Last lap' },
  { number: 2, id: 'bestLap', label: 'BEST', displayName: 'Best lap' },
  { number: 3, id: 'delta', label: 'DELTA', displayName: 'Delta' },
  { number: 4, id: 'position', label: 'POSITION', displayName: 'Position' },
  { number: 5, id: 'session', label: 'LAP', displayName: 'Session' },
  { number: 6, id: 'fuel', label: 'FUEL', displayName: 'Fuel' },
  { number: 7, id: 'fuelLaps', label: 'FUEL LAPS', displayName: 'Fuel laps' },
  { number: 8, id: 'tc', label: 'TC', displayName: 'TC' },
  { number: 9, id: 'abs', label: 'ABS', displayName: 'ABS' },
  { number: 10, id: 'tyreTemps', label: 'TYRES °C · LAST STOP', displayName: 'Tyre temps' },
  { number: 11, id: 'tyrePressures', label: 'PRESSURES PSI · LAST STOP', displayName: 'Tyre pressures' },
];

export function cardMeta(id: string): CardMeta {
  const meta = CARD_CATALOGUE.find((c) => c.id === id);
  if (!meta) throw new Error(`contract: no card with id ${id}`);
  return meta;
}
