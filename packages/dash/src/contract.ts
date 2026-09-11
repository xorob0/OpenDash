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

/**
 * The default card of each slot, slot 1 first. Speed leads, so that even a two-slot face shows
 * it now that the hero holds the gear alone; the timing block follows, then the car values.
 * Tyre pressures is the one card no slot shows by default: in iRacing it only changes in the
 * pit stall, and tyre temperatures already carry that reading.
 */
export const DEFAULT_SLOT_CARDS: readonly number[] = [12, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

/** The default card number of a 1-based slot. */
export function defaultCardForSlot(slot: number): number {
  assertSlot(slot);
  const card = DEFAULT_SLOT_CARDS[slot - 1];
  if (card === undefined) throw new RangeError(`no default card for slot ${slot}`);
  return card;
}

/** `OpenDash.<name>`, the full SimHub property name. */
export const propertyName = (name: string): string => `${PROPERTY_PREFIX}.${name}`;

/** The properties the dash face reads: the four modes and the twelve slots. */
export function dashProperties(): string[] {
  const fixed = ['ShiftLights', 'PositionMode', 'DeltaReference', 'SessionProgress'];
  const slots = Array.from({ length: SLOT_MAX }, (_, i) => slotSettingName(i + 1));
  return [...fixed, ...slots].map(propertyName);
}

/** The properties only the companion and the pit wall read: module switches, zone pages, the URL. */
export function secondScreenProperties(): string[] {
  const modules = MODULE_CATALOGUE.map((m) => moduleSettingName(m.number));
  const pitWall = [...ZONE_LETTERS.map(zoneSettingName), WIDE_ZONE_SETTING, WEB_VIEW_SETTING];
  return [...modules, ...pitWall].map(propertyName);
}

/** Every property the plugin exposes, in the order the plugin attaches them. */
export function declaredProperties(): string[] {
  return [...dashProperties(), ...secondScreenProperties()];
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

/** The cards in card-number order. The plugin ships the same list in Contract.cs. */
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
  { number: 12, id: 'speed', label: 'SPEED', displayName: 'Speed' },
];

export function cardMeta(id: string): CardMeta {
  const meta = CARD_CATALOGUE.find((c) => c.id === id);
  if (!meta) throw new Error(`contract: no card with id ${id}`);
  return meta;
}


// --- Second screens: companion modules and pit wall zones -----------------------------------

/**
 * A companion module: one page of the companion dashboard, and also the content of a pit wall
 * zone when the zone list names it. Numbers are 1-based because the companion header counts
 * "n / 21" and the plugin's toggles are `CompanionModule01`..`21`.
 */
export interface ModuleMeta {
  number: number;
  /** Module id; also the screen name in the companion dashboard. */
  id: string;
  /** Name drawn in the companion header and shown in the plugin. */
  name: string;
  /** One line in the plugin's module list. */
  description: string;
  /** Whether the module's screen is enabled when the plugin has never been configured. */
  enabled: boolean;
}

/**
 * The 21 modules in page order. Three are off by default because iRacing does not carry their
 * data: virtual energy is a Le Mans Ultimate feature, iRacing reports no damage values at all,
 * and per-segment rival timing is not a SimHub property. They ship as honest "not available"
 * pages rather than as invented numbers, so a user on another sim can still switch them on.
 *
 * Module 17 draws the gear alone. The design sheet paired it with the speed, but a module shows
 * one thing: the speed has the speedo module, and the dash face settled the same question when
 * its hero became the gear alone.
 */
export const MODULE_CATALOGUE: readonly ModuleMeta[] = [
  { number: 1, id: 'lapTimes', name: 'Lap times', description: 'Last, session best and your best, with laps, estimate and delta.', enabled: true },
  { number: 2, id: 'delta', name: 'Delta', description: 'Live delta to the reference lap on a centre-zero bar.', enabled: true },
  { number: 3, id: 'sectors', name: 'Sectors', description: 'The three sectors of the last lap with their deltas.', enabled: true },
  { number: 4, id: 'speedo', name: 'Speedo', description: 'Speed, RPM, redline and the shift bar.', enabled: true },
  { number: 5, id: 'fuel', name: 'Fuel', description: 'Fuel left, time left, what to add and the per-lap use.', enabled: true },
  { number: 6, id: 'energy', name: 'Energy', description: 'Virtual energy. Le Mans Ultimate only; iRacing has none.', enabled: false },
  { number: 7, id: 'tyres', name: 'Tyres', description: 'Temperature, pressure, wear and compound per corner.', enabled: true },
  { number: 8, id: 'pitView', name: 'Pit view', description: 'The pit service order: fuel, tyres, repairs and tear-off.', enabled: true },
  { number: 9, id: 'carSettings', name: 'Car settings', description: 'TC, ABS, brake bias, mixture and anti-roll bars.', enabled: true },
  { number: 10, id: 'inputs', name: 'Inputs', description: 'Throttle, brake and clutch traces with bar gauges.', enabled: true },
  { number: 11, id: 'session', name: 'Session', description: 'Session type, position, class, lap and time left.', enabled: true },
  { number: 12, id: 'radar', name: 'Radar', description: 'Proximity radar with the spotter on both sides.', enabled: true },
  { number: 13, id: 'track', name: 'Track', description: 'The track map with every car on it.', enabled: true },
  { number: 14, id: 'leaderboard', name: 'Leaderboard', description: 'Position, driver, class, gap, best and last.', enabled: true },
  { number: 15, id: 'relative', name: 'Relative', description: 'The cars around you on track, you in the middle.', enabled: true },
  { number: 16, id: 'opponents', name: 'Opponents', description: 'The car ahead and the car behind, in detail.', enabled: true },
  { number: 17, id: 'gear', name: 'Gear', description: 'The gear, as large as the screen allows.', enabled: true },
  { number: 18, id: 'stint', name: 'Stint', description: 'Stint laps and time, stops and the last stop.', enabled: true },
  { number: 19, id: 'lapHistory', name: 'Lap history', description: 'Your last laps with the delta to the session best.', enabled: true },
  { number: 20, id: 'damage', name: 'Damage', description: 'Body and suspension damage. iRacing reports none.', enabled: false },
  { number: 21, id: 'trackRivals', name: 'Track rivals', description: 'Segment comparison against the field. Not a SimHub value.', enabled: false },
];

/** How many modules the companion cycles through; the header counter says "n / MODULE_COUNT". */
export const MODULE_COUNT = MODULE_CATALOGUE.length;

export function moduleMeta(id: string): ModuleMeta {
  const meta = MODULE_CATALOGUE.find((m) => m.id === id);
  if (!meta) throw new Error(`contract: no module with id ${id}`);
  return meta;
}

/** The module at a 1-based page number. */
export function moduleAt(number: number): ModuleMeta {
  const meta = MODULE_CATALOGUE.find((m) => m.number === number);
  if (!meta) throw new RangeError(`module must be 1..${MODULE_COUNT}, got ${number}`);
  return meta;
}

/** `CompanionModule01` .. `CompanionModule21` for a 1-based module number. */
export function moduleSettingName(number: number): string {
  moduleAt(number);
  return `CompanionModule${String(number).padStart(2, '0')}`;
}

/**
 * A page a pit wall zone can show. Standard pages fill a 639 px zone; the wide pages fill the
 * 1039 px one. Both lists are indexed from 0, because the zone setting is that index.
 */
export interface ZonePageMeta {
  number: number;
  /** The module the page draws, or `web` for the browser page, which is not a companion module. */
  id: string;
  name: string;
}

/** The eleven standard zone pages, in the order the plugin lists them. */
export const ZONE_PAGES: readonly ZonePageMeta[] = [
  { number: 0, id: 'fuel', name: 'Fuel' },
  { number: 1, id: 'tyres', name: 'Tyres' },
  { number: 2, id: 'opponents', name: 'Opponents' },
  { number: 3, id: 'pitView', name: 'Pit view' },
  { number: 4, id: 'relative', name: 'Relative' },
  { number: 5, id: 'leaderboard', name: 'Leaderboard' },
  { number: 6, id: 'lapHistory', name: 'Lap history' },
  { number: 7, id: 'web', name: 'Web view' },
  { number: 8, id: 'inputs', name: 'Inputs' },
  { number: 9, id: 'radar', name: 'Radar' },
  { number: 10, id: 'sectors', name: 'Sectors' },
];

/** The six wide zone pages, for the one zone that spans a column. */
export const WIDE_ZONE_PAGES: readonly ZonePageMeta[] = [
  { number: 0, id: 'inputs', name: 'Inputs' },
  { number: 1, id: 'web', name: 'Web view' },
  { number: 2, id: 'lapHistory', name: 'Lap history' },
  { number: 3, id: 'opponents', name: 'Opponents' },
  { number: 4, id: 'tyres', name: 'Tyres' },
  { number: 5, id: 'carTelemetry', name: 'Car telemetry' },
];

/** The four configurable zones of a pit wall page. */
export const ZONE_LETTERS = ['A', 'B', 'C', 'D'] as const;
export type ZoneLetter = (typeof ZONE_LETTERS)[number];

/** Default page of each zone: fuel, tyres, relative and opponents, which is what a spotter watches. */
export const DEFAULT_ZONE_PAGES: Record<ZoneLetter, number> = { A: 0, B: 1, C: 4, D: 2 };

/** Default page of the wide zone: the car telemetry trace with the settings grid beside it. */
export const DEFAULT_WIDE_ZONE_PAGE = 5;

/** `PitWallZoneA` .. `PitWallZoneD`. */
export const zoneSettingName = (letter: ZoneLetter): string => `PitWallZone${letter}`;
export const WIDE_ZONE_SETTING = 'PitWallWide';
export const WEB_VIEW_SETTING = 'WebViewUrl';

/** The URL the web view page shows until the user sets one. Empty means "nothing configured". */
export const DEFAULT_WEB_VIEW_URL = '';

/** Reads of the second-screen settings, each defaulted so the dashboards work without the plugin. */
export const secondScreen = {
  /**
   * `isnull([OpenDash.CompanionModule07], 1)`: a screen's enabled expression, which SimHub reads
   * as a number and treats as enabled when it is above zero. A boolean property converts to 1.
   */
  moduleEnabled: (number: number): Expr => isnull(prop(propertyName(moduleSettingName(number))), num(moduleAt(number).enabled ? 1 : 0)),
  /** `isnull([OpenDash.PitWallZoneA], 0)`: which page a zone's widget shows. */
  zonePage: (letter: ZoneLetter): Expr => isnull(prop(propertyName(zoneSettingName(letter))), num(DEFAULT_ZONE_PAGES[letter])),
  /** `isnull([OpenDash.PitWallWide], 5)`. */
  wideZonePage: (): Expr => isnull(prop(propertyName(WIDE_ZONE_SETTING)), num(DEFAULT_WIDE_ZONE_PAGE)),
  /** `isnull([OpenDash.WebViewUrl], '')`: the address of the web view page. */
  webViewUrl: (): Expr => isnull(prop(propertyName(WEB_VIEW_SETTING)), str(DEFAULT_WEB_VIEW_URL)),
};
