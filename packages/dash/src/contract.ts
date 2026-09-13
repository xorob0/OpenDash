/**
 * The settings contract between the dashboard and the plugin: property names, defaults, value
 * sets and the card catalogue. Every expression that reads an `OpenDash.*` property goes through
 * `setting`, so `declaredProperties()` is the single list the validator checks against.
 * plugin/OpenDash/Contract.cs mirrors this file.
 */
import { ncalc } from './generator.ts';
import type { Expr } from './bind.ts';

const { eq, iff, isnull, prop, str, num } = ncalc;

export const PROPERTY_PREFIX = 'OpenDash';

/** The largest slot count any layout declares; the plugin exposes exactly this many slot settings. */
export const SLOT_MAX = 12;

export type RevBarMode = 'shift' | 'rpm' | 'off';
export type PositionMode = 'overall' | 'class';
export type DeltaReference = 'session' | 'alltime';
export type SessionProgress = 'auto' | 'laps' | 'time';

/**
 * What the top of a rectangular face carries: SimHub's shift lights, a plain RPM bar, or nothing
 * at all. A mode rather than a second boolean, because the three are one decision -- what is at
 * the top of the face -- and two booleans would have a fourth state that means nothing.
 *
 * `ShiftLights` is not retired with it. It has shipped, it is one of the four names the plugin
 * attaches first, and README publishes it as a property an LED profile may read; XOR-119 is the
 * rule that an rc.2 user's properties do not vanish without a release of warning. It stays as the
 * deprecated alias that {@link setting.revBar} falls back to.
 */
export const REV_BAR_MODES: readonly RevBarMode[] = ['shift', 'rpm', 'off'];

/** Appended to the property list rather than folded into the four fixed names, which have shipped. */
export const REV_BAR_SETTING = 'RevBar';

export const POSITION_MODES: readonly PositionMode[] = ['overall', 'class'];
export const DELTA_REFERENCES: readonly DeltaReference[] = ['session', 'alltime'];
export const SESSION_PROGRESS_MODES: readonly SessionProgress[] = ['auto', 'laps', 'time'];

export const DEFAULTS = {
  ShiftLights: true,
  RevBar: 'shift' as RevBarMode,
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

/**
 * The properties the dash face reads: the four modes, the twelve slots, the zones and the rev bar.
 *
 * `RevBar` is last rather than beside the mode it supersedes. The four fixed names have shipped and
 * the plugin's own tests assert them by index, so a new setting is appended and never inserted.
 */
export function dashProperties(): string[] {
  const fixed = ['ShiftLights', 'PositionMode', 'DeltaReference', 'SessionProgress'];
  const slots = Array.from({ length: SLOT_MAX }, (_, i) => slotSettingName(i + 1));
  return [...[...fixed, ...slots].map(propertyName), ...zoneProperties(), propertyName(REV_BAR_SETTING)];
}

/** The properties only the companion and the pit wall read: module switches, zone pages, the URL. */
export function secondScreenProperties(): string[] {
  const modules = MODULE_CATALOGUE.map((m) => moduleSettingName(m.number));
  const pitWall = [...PIT_WALL_ZONE_LETTERS.map(pitWallZoneSettingName), PIT_WALL_WIDE_ZONE_SETTING, WEB_VIEW_SETTING];
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
  /** `isnull([OpenDash.ShiftLights], true)`. Deprecated; {@link setting.revBar} is the one to read. */
  shiftLights: (): Expr => isnull(prop(propertyName('ShiftLights')), String(DEFAULTS.ShiftLights)),
  /**
   * `isnull([OpenDash.RevBar], if(isnull([OpenDash.ShiftLights], true), 'shift', 'rpm'))`: what the
   * top of the face carries.
   *
   * Two fallbacks deep, and both of them earn their place. The inner one is the default without the
   * plugin, which is what ADR 0003 requires of every read; the outer one is the deprecated alias, so
   * that a package installed beside an rc.2 plugin -- which attaches `ShiftLights` and not `RevBar`
   * -- still honours the switch that user set. `off` is reachable only through `RevBar`, which is
   * correct: a plugin that has never heard of the mode cannot have been asked for it.
   */
  revBar: (): Expr => isnull(prop(propertyName(REV_BAR_SETTING)), iff(setting.shiftLights(), str('shift'), str('rpm'))),
  /** `isnull([OpenDash.RevBar], ...) = 'off'`: whether the face is in the given rev bar mode. */
  revBarIs: (mode: RevBarMode): Expr => eq(setting.revBar(), str(mode)),
  /** `isnull([OpenDash.PositionMode], 'overall')` */
  positionMode: (): Expr => isnull(prop(propertyName('PositionMode')), str(DEFAULTS.PositionMode)),
  /** `isnull([OpenDash.DeltaReference], 'session')` */
  deltaReference: (): Expr => isnull(prop(propertyName('DeltaReference')), str(DEFAULTS.DeltaReference)),
  /** `isnull([OpenDash.SessionProgress], 'auto')` */
  sessionProgress: (): Expr => isnull(prop(propertyName('SessionProgress')), str(DEFAULTS.SessionProgress)),
  /** `isnull([OpenDash.Slot0i], default card number)` for a 1-based slot. */
  slot: (slot: number): Expr => isnull(prop(propertyName(slotSettingName(slot))), num(defaultCardForSlot(slot))),
};


// --- The zone face -------------------------------------------------------------------------
//
// Additive. `Slot01` to `Slot12` stay declared and stay tested until the card path is retired in
// XOR-95, because ten faces still read them and README.md publishes them as properties an LED
// profile may read.
//
// The shape of these is the whole point of the model. A slot is arranged once, with a mouse,
// before a session; a zone is changed with a thumb in the middle of a lap. So what the contract
// carries is a page number a button can advance, not an assignment a panel writes.

/** The four zones of a rectangular face. Band D is a zone: it cycles a catalogue like the rest. */
export const FACE_ZONE_LETTERS = ['A', 'B', 'C', 'D'] as const;
export type FaceZone = (typeof FACE_ZONE_LETTERS)[number];

/** A page a zone can show. Indexed from 0, because the zone setting is that index. */
export interface FaceZonePageMeta {
  number: number;
  /** The module the page draws, or a zone-A / band-D page id that is not a module. */
  id: string;
  name: string;
}

/**
 * Zone A's four pages. The one a driver reads by reflex, which is why the zone is a narrow column:
 * the gear wants height, not width.
 */
export const ZONE_A_PAGES: readonly FaceZonePageMeta[] = [
  { number: 0, id: 'gearSpeedRevs', name: 'Gear, speed, revs' },
  { number: 1, id: 'gearAlone', name: 'Gear alone' },
  { number: 2, id: 'speed', name: 'Speed' },
  { number: 3, id: 'track', name: 'Track' },
];

/**
 * Band D's eight pages. Fuel by default, because that is what a driver checks on a straight.
 *
 * The catalogue artboard is headed "seven pages" and draws D1 through D8; the drawings are more
 * specific than the caption, so eight is taken and the disagreement is recorded in
 * docs/design/zones.md. The car page needs the telltale pictograms and arrives with XOR-97; the
 * mask is sized for eight from the start so that adding it costs nothing.
 */
export const BAND_D_PAGES: readonly FaceZonePageMeta[] = [
  { number: 0, id: 'fuel', name: 'Fuel' },
  { number: 1, id: 'energy', name: 'Energy' },
  { number: 2, id: 'stint', name: 'Stint' },
  { number: 3, id: 'tyres', name: 'Tyres' },
  { number: 4, id: 'weather', name: 'Weather' },
  { number: 5, id: 'sectors', name: 'Sectors' },
  { number: 6, id: 'relative', name: 'Relative' },
  { number: 7, id: 'car', name: 'Car' },
];

/** Zones B and C draw from the full module catalogue, which is no longer companion-only. */
export const zoneBCPages = (): readonly FaceZonePageMeta[] => MODULE_CATALOGUE.map((m) => ({ number: m.number - 1, id: m.id, name: m.name }));

/** The catalogue a zone draws from. */
export function pagesForZone(zone: FaceZone): readonly FaceZonePageMeta[] {
  if (zone === 'A') return ZONE_A_PAGES;
  if (zone === 'D') return BAND_D_PAGES;
  return zoneBCPages();
}

/**
 * The bar's end fields. Two per end, which is what every face artboard draws; the anatomy caption
 * saying three and the spec drawing showing one are both superseded, and docs/design/zones.md
 * records the disagreement.
 *
 * Ten rather than the canvas's eleven: strength of field is not published by SimHub in any form,
 * and ADR 0009 decided that a field which can never have a value is not offered.
 */
export const BAR_FIELDS: readonly FaceZonePageMeta[] = [
  { number: 0, id: 'raceTime', name: 'Race time' },
  { number: 1, id: 'lap', name: 'Lap' },
  { number: 2, id: 'timeLeft', name: 'Time left' },
  { number: 3, id: 'clock', name: 'Clock' },
  { number: 4, id: 'simulatedTime', name: 'Simulated time' },
  { number: 5, id: 'position', name: 'Position' },
  { number: 6, id: 'classPosition', name: 'Class' },
  { number: 7, id: 'incidents', name: 'Incidents' },
  { number: 8, id: 'airTemp', name: 'Air temperature' },
  { number: 9, id: 'trackTemp', name: 'Track temperature' },
];

/** The bar's four slots, left to right. A portrait face draws only the first of each end. */
export const BAR_SLOTS = ['Left1', 'Left2', 'Right1', 'Right2'] as const;
export type BarSlot = (typeof BAR_SLOTS)[number];

/** Race and Lap on the left, Position and Class on the right, as the artboards draw them. */
export const DEFAULT_BAR_FIELDS: Record<BarSlot, number> = { Left1: 0, Left2: 1, Right1: 5, Right2: 6 };

/**
 * Which page each zone opens on: the gear, lap times, the relative, and fuel. Those are the four
 * a driver would put there if asked, which is what a default is for.
 */
export const DEFAULT_ZONE_PAGE: Record<FaceZone, number> = { A: 0, B: 0, C: 14, D: 0 };

/**
 * Which pages are enabled, as a bit mask, which is what sets the length of a zone's cycle. All of
 * them by default: a driver turns off what they do not want rather than turning on what they do.
 */
export const defaultZoneMask = (zone: FaceZone): number => (1 << pagesForZone(zone).length) - 1;

export const zonePageSettingName = (zone: FaceZone): string => `Zone${zone}`;
export const zoneMaskSettingName = (zone: FaceZone): string => `Zone${zone}Pages`;
export const zoneStartSettingName = (zone: FaceZone): string => `Zone${zone}Start`;
export const barFieldSettingName = (slot: BarSlot): string => `Bar${slot}`;

/**
 * The zone and page a held button shows, as one property rather than a pair per zone: a glance is
 * one thing a driver configures once, and four more properties for it would be four more rows in
 * the panel for no more expressiveness. Encoded as `zoneIndex * 100 + page`.
 */
export const QUICK_GLANCE_SETTING = 'QuickGlance';
/** Zone C on the track page, which is what a glance is usually for. */
export const DEFAULT_QUICK_GLANCE = 2 * 100 + 12;

export const quickGlanceValue = (zone: FaceZone, page: number): number => FACE_ZONE_LETTERS.indexOf(zone) * 100 + page;
export const quickGlanceZone = (value: number): FaceZone => FACE_ZONE_LETTERS[Math.floor(value / 100)] ?? 'A';
export const quickGlancePage = (value: number): number => value % 100;

/** Reads of the zone settings, each defaulted so a face works without the plugin. */
export const zone = {
  /** `isnull([OpenDash.ZoneB], 0)`: the page a zone is showing. */
  page: (z: FaceZone): Expr => isnull(prop(propertyName(zonePageSettingName(z))), num(DEFAULT_ZONE_PAGE[z])),
  /** `isnull([OpenDash.ZoneBPages], 2097151)`: which pages are enabled, as a mask. */
  mask: (z: FaceZone): Expr => isnull(prop(propertyName(zoneMaskSettingName(z))), num(defaultZoneMask(z))),
  /** `isnull([OpenDash.ZoneBStart], 0)`: the page the zone opens on. */
  start: (z: FaceZone): Expr => isnull(prop(propertyName(zoneStartSettingName(z))), num(DEFAULT_ZONE_PAGE[z])),
  /** `isnull([OpenDash.QuickGlance], 212)`: the zone and page a held button shows. */
  quickGlance: (): Expr => isnull(prop(propertyName(QUICK_GLANCE_SETTING)), num(DEFAULT_QUICK_GLANCE)),
  /** `isnull([OpenDash.BarLeft1], 0)`: which field an end of the bar shows. */
  barField: (slot: BarSlot): Expr => isnull(prop(propertyName(barFieldSettingName(slot))), num(DEFAULT_BAR_FIELDS[slot])),
};

/** Every property the zone face reads. */
export function zoneProperties(): string[] {
  const perZone = FACE_ZONE_LETTERS.flatMap((z) => [zonePageSettingName(z), zoneMaskSettingName(z), zoneStartSettingName(z)]);
  const bar = BAR_SLOTS.map(barFieldSettingName);
  return [...perZone, ...bar, QUICK_GLANCE_SETTING].map(propertyName);
}

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
 *
 * These carry a `PIT_WALL_` prefix because the dash face now has zones of its own, and the two
 * are deliberately different catalogues. A pit wall zone is chosen with a mouse by somebody who
 * is not driving, in a 607 by 158 strip, and its list includes a web view that no face would ever
 * show. A face zone is cycled with a thumb at speed and draws from the full twenty-one. Merging
 * them would mean either offering a driver a browser page or denying a spotter one.
 */
export interface PitWallZonePageMeta {
  number: number;
  /** The module the page draws, or `web` for the browser page, which is not a companion module. */
  id: string;
  name: string;
}

/** The eleven standard zone pages, in the order the plugin lists them. */
export const PIT_WALL_ZONE_PAGES: readonly PitWallZonePageMeta[] = [
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
export const PIT_WALL_WIDE_ZONE_PAGES: readonly PitWallZonePageMeta[] = [
  { number: 0, id: 'inputs', name: 'Inputs' },
  { number: 1, id: 'web', name: 'Web view' },
  { number: 2, id: 'lapHistory', name: 'Lap history' },
  { number: 3, id: 'opponents', name: 'Opponents' },
  { number: 4, id: 'tyres', name: 'Tyres' },
  { number: 5, id: 'carTelemetry', name: 'Car telemetry' },
];

/** The four configurable zones of a pit wall page. */
export const PIT_WALL_ZONE_LETTERS = ['A', 'B', 'C', 'D'] as const;
export type PitWallZoneLetter = (typeof PIT_WALL_ZONE_LETTERS)[number];

/** Default page of each zone: fuel, tyres, relative and opponents, which is what a spotter watches. */
export const PIT_WALL_DEFAULT_ZONE_PAGES: Record<PitWallZoneLetter, number> = { A: 0, B: 1, C: 4, D: 2 };

/** Default page of the wide zone: the car telemetry trace with the settings grid beside it. */
export const PIT_WALL_DEFAULT_WIDE_ZONE_PAGE = 5;

/** `PitWallZoneA` .. `PitWallZoneD`. */
export const pitWallZoneSettingName = (letter: PitWallZoneLetter): string => `PitWallZone${letter}`;
export const PIT_WALL_WIDE_ZONE_SETTING = 'PitWallWide';
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
  zonePage: (letter: PitWallZoneLetter): Expr => isnull(prop(propertyName(pitWallZoneSettingName(letter))), num(PIT_WALL_DEFAULT_ZONE_PAGES[letter])),
  /** `isnull([OpenDash.PitWallWide], 5)`. */
  wideZonePage: (): Expr => isnull(prop(propertyName(PIT_WALL_WIDE_ZONE_SETTING)), num(PIT_WALL_DEFAULT_WIDE_ZONE_PAGE)),
  /** `isnull([OpenDash.WebViewUrl], '')`: the address of the web view page. */
  webViewUrl: (): Expr => isnull(prop(propertyName(WEB_VIEW_SETTING)), str(DEFAULT_WEB_VIEW_URL)),
};
