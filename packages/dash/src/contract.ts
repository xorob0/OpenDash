/**
 * The settings contract between the dashboard and the plugin: property names, defaults, value
 * sets and the card catalogue. Every expression that reads an `OpenDash.*` property goes through
 * `setting`, so `declaredProperties()` is the single list the validator checks against, and
 * `foreignProperties()` is what keeps each package inside its own screen's half of it.
 * plugin/OpenDash/Contract.cs mirrors this file.
 */
import { ncalc } from './generator.ts';
import type { Expr } from './bind.ts';
// The shapes, for the run lengths the mirror publishes. strip.ts imports nothing, so this is the
// one direction the two can face.
import { ALL_SHAPES } from './leds/strip.ts';

const { add, and, concat, div, eq, fmt, iff, isnull, left, lt, mod, num, or, prop, str, truncate } = ncalc;

export const PROPERTY_PREFIX = 'OpenDash';

/** The largest slot count any layout declares; the plugin exposes exactly this many slot settings. */
export const SLOT_MAX = 12;

export type RevBarMode = 'shift' | 'rpm' | 'off';
export type PositionMode = 'overall' | 'class';
export type DeltaReference = 'session' | 'alltime';
export type SessionProgress = 'auto' | 'laps' | 'time';
/**
 * What the middle of an RGB strip shows. It decides the middle alone: the sides of a strip are
 * lamps, and nothing a driver chooses here reaches them.
 *
 * The sides carried a brake gradient under this setting's default, which is what the hardware
 * makers put there and what DNR puts on the same LEDs. It is gone, because a group filled red by
 * the pedal is a group on which an oil warning cannot come on, and the warning is the thing a side
 * exists for. Brake is still available where it can be read, as a centre function.
 *
 * Four, not five. `rpmOnly` lit the same centre as `rpm` and differed only in leaving the sides
 * dark, which is a decision about the sides rather than about the centre, so it is retired into
 * `rpm`; {@link RETIRED_LED_CENTRE} is what a settings file written before the retirement carries,
 * and the plugin migrates it rather than letting a strip match no group and go dark.
 */
export type LedCentre = 'rpm' | 'brake' | 'throttleBrake' | 'fuel';
/**
 * How the rev ladder fills the strip.
 *
 * The three openDash styles decide the *look*, never the *when*: the thresholds are the car's own
 * either way (ADR 0014), and a style only chooses which LED takes which rung and what colour it is.
 *
 * `car` is not one of those. It is the car's own bar -- its LEDs, its colours, its order, its
 * flash, in the gear it is in -- from a table the plugin fetches (ADR 0018), and it is the default
 * because openDash's opinion is that the car is right. A car with no table, or a rig with no
 * plugin, falls back to the ladder iRacing publishes, drawn `leftToRight`.
 */
export type LedRpmStyle = 'car' | 'leftToRight' | 'meetInMiddle' | 'f1';

/** What the mirror does when the car's bar and the strip are not the same length. */
export type LedMirrorFit = 'stretch' | 'exact';

/**
 * What the top of a rectangular face carries: the shift lights, a plain RPM bar, or nothing
 * at all. A mode rather than a second boolean, because the three are one decision -- what is at
 * the top of the face -- and two booleans would have a fourth state that means nothing.
 *
 * `shift` names the state and not the source. Which ladder lights it is the car's business rather
 * than a setting: the car's own RPMs where it publishes them, SimHub's bands where it does not
 * (ADR 0014). There is no fourth value for that and there should not be one.
 *
 * `ShiftLights` is not retired with it. It has shipped, it is one of the four names the plugin
 * attaches first, and README publishes it as a property an LED profile may read; #170 is the
 * rule that an rc.2 user's properties do not vanish without a release of warning. It stays as the
 * deprecated alias that {@link setting.revBar} falls back to.
 */
export const REV_BAR_MODES: readonly RevBarMode[] = ['shift', 'rpm', 'off'];

/** Appended to the group every screen shares rather than folded into the four fixed names, which have shipped. */
export const REV_BAR_SETTING = 'RevBar';

/**
 * What a blue flag band says beyond its colour: nothing, the class of the car behind, or that
 * car's position and class.
 *
 * Shared rather than a face's, and that is the difference from the flag *format*, which is per
 * screen. The format decides how much of one screen a flag takes and so differs between a rim read
 * at arm's length and a display in the corner of the eye; this decides what the band is allowed to
 * *say*, which is the same answer wherever it is written. Every band that carries a name reads it,
 * and a band that carries none -- the nano's twelve pixel strip and the companion's -- reads
 * nothing and draws nothing extra.
 *
 * Appended after the rev bar for the reason the rev bar is appended after the slots: both halves of
 * the contract assert the shared group by index and a new name goes on the end of it.
 */
export type BlueFlagDetail = 'none' | 'class' | 'positionClass';
export const BLUE_FLAG_DETAILS: readonly BlueFlagDetail[] = ['none', 'class', 'positionClass'];
export const BLUE_FLAG_DETAIL_SETTING = 'BlueFlagDetail';

export const POSITION_MODES: readonly PositionMode[] = ['overall', 'class'];
export const DELTA_REFERENCES: readonly DeltaReference[] = ['session', 'alltime'];
export const SESSION_PROGRESS_MODES: readonly SessionProgress[] = ['auto', 'laps', 'time'];
export const LED_CENTRES: readonly LedCentre[] = ['rpm', 'brake', 'throttleBrake', 'fuel'];
export const LED_RPM_STYLES: readonly LedRpmStyle[] = ['car', 'leftToRight', 'meetInMiddle', 'f1'];
export const LED_MIRROR_FITS: readonly LedMirrorFit[] = ['stretch', 'exact'];

/**
 * The run lengths the plugin publishes a mirrored bar for: every centre length a shape uses.
 *
 * Derived from the shapes rather than written down beside them. It was a list, and a list is a
 * second place to remember: a shape whose centre was not in it got no mirror at all, silently, since
 * the profile simply reads a property nobody attaches. Deriving it means the grid can grow a centre
 * without anybody noticing they owed this file a line -- which is the same reason the grid exists.
 *
 * Each entry is still a property name and a property name is still a public interface (ADR 0003), so
 * the list is sorted and `declared-properties.txt` pins what comes out of it.
 */
export const MIRROR_RUN_LENGTHS: readonly number[] = [...new Set(ALL_SHAPES.map((shape) => shape.centre))].sort((a, b) => a - b);

/** How many characters one colour takes in a packed run: `#AARRGGBB`. */
export const MIRROR_COLOR_WIDTH = 9;

/** The fifth centre, retired into `rpm`. Named so that the plugin can migrate it rather than guess. */
export const RETIRED_LED_CENTRE = 'rpmOnly';

export const DEFAULTS = {
  ShiftLights: true,
  RevBar: 'shift' as RevBarMode,
  PositionMode: 'overall' as PositionMode,
  DeltaReference: 'session' as DeltaReference,
  SessionProgress: 'auto' as SessionProgress,
  LedCentre: 'rpm' as LedCentre,
  LedRpmStyle: 'car' as LedRpmStyle,
  LedMirrorFit: 'stretch' as LedMirrorFit,
  LedFlagAnimation: true,
  LedSpotterWhole: false,
  // Nothing extra, because a blue flag is read by its colour and the band is the one place a
  // driver already knows to look; the class of the car behind is a thing to ask for rather than a
  // thing to be given while lifting.
  BlueFlagDetail: 'none' as BlueFlagDetail,
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
 * The properties the dash face reads: the four modes, the twelve slots, the rev bar and the zones.
 *
 * `RevBar` comes after the slots rather than beside the mode it supersedes. The four fixed names
 * have shipped and the plugin's own tests assert them by index, so a new setting is appended to the
 * group every screen shares and never inserted into it. That group is where it belongs rather than
 * with one face's zones: the round faces' rev arc and the companion's speedo draw the same segments
 * and read the same setting, and `foreignProperties` would deny them a face's property.
 */
export function dashProperties(): string[] {
  const fixed = ['ShiftLights', 'PositionMode', 'DeltaReference', 'SessionProgress'];
  const slots = Array.from({ length: SLOT_MAX }, (_, i) => slotSettingName(i + 1));
  return [...[...fixed, ...slots, REV_BAR_SETTING, BLUE_FLAG_DETAIL_SETTING].map(propertyName), ...zoneProperties()];
}

/** The properties only a generated LED profile reads. ADR 0013. */
export function ledProperties(): string[] {
  return [LED_CENTRE_SETTING, LED_RPM_STYLE_SETTING, LED_FLAG_ANIMATION_SETTING, LED_MIRROR_FIT_SETTING, LED_MIRROR_READY, ...MIRROR_RUN_LENGTHS.map(ledMirrorRunName), LED_SPOTTER_WHOLE_SETTING].map(propertyName);
}

/** The name of the setting choosing what the middle of a strip shows. */
export const LED_CENTRE_SETTING = 'LedCentre';

/** The name of the setting choosing how the rev ladder fills the strip. */
export const LED_RPM_STYLE_SETTING = 'LedRpmStyle';

/**
 * Whether a flag on a strip moves at all.
 *
 * On, because movement is what a flag is read by at the edge of vision. Off holds every flag from
 * the frame it would have settled on and never turns one off, which is what a driver who finds a
 * blinking rim distracting is actually asking for; it is a switch rather than a rate, because a
 * rate is the standard's decision and not the driver's.
 *
 * Appended after the two the strips already read rather than inserted beside them, for the reason
 * `RevBar` is appended to the shared group: both halves of the contract are pinned in order.
 */
export const LED_FLAG_ANIMATION_SETTING = 'LedFlagAnimation';

/** The name of the setting choosing how a car's bar is fitted to a strip that is a different length. */
export const LED_MIRROR_FIT_SETTING = 'LedMirrorFit';

/**
 * Whether a car alongside takes the whole strip rather than the lamp at that end.
 *
 * Off, because the lamps are what a side is for: one LED at each end, the outermost, is what
 * peripheral vision reaches and it leaves the rev ladder readable while the car is there. On is for
 * the driver who wants to be unable to miss it, and for the strips where a lamp is not enough — a
 * brow above a monitor has no ends to speak of, and a wheel whose sides are one LED has a lamp
 * carrying three roles at once.
 *
 * The side it is on is still the side it lights: whole means the whole run, not both runs, so a car
 * on the right fills the strip amber and a car on the left fills it too, and the two are the same
 * picture. That is the cost of the setting and the reason it is off by default; a driver asking for
 * it is asking for "something is beside me" rather than "something is beside me on this side".
 */
export const LED_SPOTTER_WHOLE_SETTING = 'LedSpotterWhole';

/**
 * Whether the plugin is publishing a mirrored bar this frame: it has a table for this car, the
 * driver has asked for it, and the sim is in a gear it can draw. The one gate the mirror layer of
 * every strip profile hangs on, and false for all five of the ways there can be no mirror -- no
 * plugin, no tables fetched, no entry for the car, an entry that would not read, or a driver who
 * chose one of openDash's own styles.
 */
export const LED_MIRROR_READY = 'LedMirrorReady';

/**
 * `LedMirror14`: a whole 14-LED run of the car's own bar, as one string.
 *
 * Fixed-width `#AARRGGBB` colours end to end, which is what lets the profile take LED `k` out of it
 * with {@link setting.ledMirrorAt}. One property per run length rather than one per LED is 12 names
 * in the contract instead of 149.
 */
export const ledMirrorRunName = (length: number): string => `LedMirror${length}`;

/** The properties only the companion and the pit wall read: module switches, zone pages, the URL. */
export function secondScreenProperties(): string[] {
  const pitWall = [...allPitWallZoneSettingNames(), PIT_WALL_START_PAGE_SETTING, PIT_WALL_PAGE_SETTING, WEB_VIEW_SETTING, PIT_WALL_CLASS_ONLY_SETTING];
  return [...companionProperties(), ...pitWall].map(propertyName);
}

/** Every property the plugin exposes, in the order the plugin attaches them. */
export function declaredProperties(): string[] {
  // The flag box's settings and the strips' are both here: they are one set of lights with two
  // kinds of hardware behind them, and a profile of either kind reads from this one list.
  return [...dashProperties(), ...secondScreenProperties(), ...flagBoxProperties(), ...ledProperties()];
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
  /** `isnull([OpenDash.LedCentre], 'rpm')` */
  ledCentre: (): Expr => isnull(prop(propertyName(LED_CENTRE_SETTING)), str(DEFAULTS.LedCentre)),
  /** `isnull([OpenDash.LedRpmStyle], 'car')` */
  ledRpmStyle: (): Expr => isnull(prop(propertyName(LED_RPM_STYLE_SETTING)), str(DEFAULTS.LedRpmStyle)),
  /** `isnull([OpenDash.LedFlagAnimation], true)`: whether a flag on a strip moves. */
  ledFlagAnimation: (): Expr => isnull(prop(propertyName(LED_FLAG_ANIMATION_SETTING)), String(DEFAULTS.LedFlagAnimation)),
  /** `isnull([OpenDash.LedSpotterWhole], false)`: whether a car alongside takes the whole strip. */
  ledSpotterWhole: (): Expr => isnull(prop(propertyName(LED_SPOTTER_WHOLE_SETTING)), String(DEFAULTS.LedSpotterWhole)),
  /** `isnull([OpenDash.BlueFlagDetail], 'none')`: what a blue band says beyond its colour. */
  blueFlagDetail: (): Expr => isnull(prop(propertyName(BLUE_FLAG_DETAIL_SETTING)), str(DEFAULTS.BlueFlagDetail)),
  /** `isnull([OpenDash.BlueFlagDetail], 'none') = 'class'`: whether the band is in the given detail. */
  blueFlagDetailIs: (detail: BlueFlagDetail): Expr => eq(setting.blueFlagDetail(), str(detail)),
  /** `isnull([OpenDash.LedMirrorFit], 'stretch')`. Read by the plugin rather than by a profile. */
  ledMirrorFit: (): Expr => isnull(prop(propertyName(LED_MIRROR_FIT_SETTING)), str(DEFAULTS.LedMirrorFit)),
  /** `isnull([OpenDash.LedMirrorReady], 0) = 1`: whether there is a mirrored bar to draw. */
  ledMirrorReady: (): Expr => eq(isnull(prop(propertyName(LED_MIRROR_READY)), num(0)), num(1)),
  /**
   * `left(isnull([OpenDash.LedMirror14], ''), 18, 9)`: the colour of LED `k` (0-based) of a run of
   * `length`, out of the one string the plugin publishes for that run.
   *
   * SimHub's `left(value, startIndex, length)` returns an empty string rather than throwing when the
   * value is short or absent, and `DynamicColor` falls back to its own colour on an empty one -- so
   * a rig with no plugin draws nothing here rather than failing. The group is gated on
   * {@link setting.ledMirrorReady} in any case.
   */
  ledMirrorAt: (length: number, k: number): Expr => left(isnull(prop(propertyName(ledMirrorRunName(length))), str('')), MIRROR_COLOR_WIDTH, k * MIRROR_COLOR_WIDTH),
};


// --- The zone face -------------------------------------------------------------------------
//
// Additive. `Slot01` to `Slot12` stay declared and stay tested until the card path is retired in
// #146, because ten faces still read them and README.md publishes them as properties an LED
// profile may read.
//
// The shape of these is the whole point of the model. A slot is arranged once, with a mouse,
// before a session; a zone is changed with a thumb in the middle of a lap. So what the contract
// carries is a page number a button can advance, not an assignment a panel writes.

/**
 * Every face size that ships, and therefore every group of zone properties the plugin attaches.
 *
 * The list lives here rather than in `zones/` because the contract is what the plugin mirrors: a
 * face the contract does not name has no properties, whatever the build emits. `zones/index.ts`
 * reads it back, so the two cannot disagree.
 */
export const FACE_SIZES: readonly FaceSize[] = [
  { width: 1920, height: 480, body: 'row', parts: [769, 380, 769], hasBar: true, barFieldsPerEnd: 2, rows: { revBar: 48, bar: 56, body: 314, band: 60 } },
  { width: 1280, height: 480, body: 'row', parts: [469, 340, 469], hasBar: true, barFieldsPerEnd: 2, rows: { revBar: 44, bar: 54, body: 320, band: 60 } },
  { width: 1280, height: 400, body: 'row', parts: [469, 340, 469], hasBar: true, barFieldsPerEnd: 2, rows: { revBar: 36, bar: 50, body: 258, band: 54 } },
  { width: 850, height: 480, body: 'row', parts: [274, 300, 274], hasBar: true, barFieldsPerEnd: 2, rows: { revBar: 40, bar: 50, body: 328, band: 60 } },
  { width: 800, height: 480, body: 'row', parts: [249, 300, 249], hasBar: true, barFieldsPerEnd: 2, rows: { revBar: 40, bar: 50, body: 328, band: 60 } },
  { width: 1280, height: 720, body: 'row', parts: [469, 340, 469], hasBar: true, barFieldsPerEnd: 2, rows: { revBar: 48, bar: 56, body: 554, band: 60 } },
  { width: 800, height: 286, body: 'row', parts: [269, 260, 269], hasBar: false, barFieldsPerEnd: 2, rows: { revBar: 33, bar: 0, body: 194, band: 58 } },
  { width: 600, height: 686, body: 'column', parts: [234, 160, 150], hasBar: true, barFieldsPerEnd: 1, rows: { revBar: 36, bar: 46, body: 546, band: 56 } },
];

/**
 * A face, and the little of its shape that anything outside the build needs.
 *
 * The plugin draws a plan of the face in its panel, and a plan drawn to one face's proportions for
 * every face is how the 800 x 286 came to be offered bar fields for a bar it does not have. What is
 * carried here is therefore only what a plan needs, and `zoneFace.test.ts` checks it against the
 * real layouts so the two cannot drift.
 */
export interface FaceSize {
  width: number;
  height: number;
  /** `row` lays zone B, zone A and zone C side by side; `column` stacks A over B over C. */
  body: 'row' | 'column';
  /** Relative sizes of the three body zones, in the order that body draws them. */
  parts: readonly [number, number, number];
  /** False on the nano at 800 x 286, where the height for a bar is not there. */
  hasBar: boolean;
  /** Two per end on a wide face, one in portrait. */
  barFieldsPerEnd: 1 | 2;
  /** The heights of the four rows the face stacks, which is what a plan of it scales from. */
  rows: FaceRows;
}

/**
 * The four rows of a face, from the top: the strip the rev bar sits in, the bar, the body the three
 * zones share, and band D.
 *
 * Here for the same reason `parts` is: the plugin draws a plan of the face and cannot read a layout
 * file, and a plan whose rows are four constants draws a 1920 x 480 face's 48, 56, 314 and 60 as 19,
 * 24, 150 and 26. `zoneFace.test.ts` holds each number against the real rectangles in
 * `zones/faces/*.ts`, so a face that is redrawn cannot leave its plan behind.
 *
 * `bar` is zero on the nano, which has no bar. `body` counts the whole region the three zones
 * occupy, the one-pixel seams between them included, so that a portrait face whose zones are
 * stacked measures the same way as a wide one whose zones are side by side.
 */
export interface FaceRows {
  revBar: number;
  bar: number;
  body: number;
  band: number;
}

/** The zone letters of a face's body, in the order that body draws them. */
export const bodyOrder = (face: FaceSize): readonly [FaceZone, FaceZone, FaceZone] =>
  face.body === 'column' ? ['A', 'B', 'C'] : ['B', 'A', 'C'];

/**
 * The prefix a face's settings carry, for instance `Face1920x480`.
 *
 * Concatenated rather than separated by a dot, because SimHub already puts one dot in front of
 * every property name and whether its parser accepts a second inside the name is unverified.
 *
 * Every face carries one, so that a rig of a 1920 face, an 850 face and a pit wall is configured
 * apart rather than sharing one set of zones. This completes an idiom the contract already has:
 * the pit wall's properties carry `PitWall` and the companion's carry `CompanionModule`, and only
 * the face behaved as though there could be just one of it. It is done now rather than later
 * because property names are a public interface under ADR 0003, so `OpenDash.ZoneA` today and
 * `OpenDash.Face1920x480ZoneA` tomorrow would break whoever had built on the first.
 */
export const facePrefix = (face: FaceSize): string => `Face${face.width}x${face.height}`;

/** The face a prefix names, or undefined when nothing ships at that size. */
export const faceForPrefix = (prefix: string): FaceSize | undefined =>
  FACE_SIZES.find((face) => facePrefix(face) === prefix);

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
 * docs/design/zones.md. The car page needs the telltale pictograms and arrives with #148; the
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

export const zonePageSettingName = (face: FaceSize, zone: FaceZone): string => `${facePrefix(face)}Zone${zone}`;
export const zoneMaskSettingName = (face: FaceSize, zone: FaceZone): string => `${facePrefix(face)}Zone${zone}Pages`;
export const zoneStartSettingName = (face: FaceSize, zone: FaceZone): string => `${facePrefix(face)}Zone${zone}Start`;
export const zoneClassOnlySettingName = (face: FaceSize, zone: FaceZone): string => `${facePrefix(face)}Zone${zone}ClassOnly`;
export const barFieldSettingName = (face: FaceSize, slot: BarSlot): string => `${facePrefix(face)}Bar${slot}`;

/**
 * Whether a zone's list pages show the player's own class rather than the whole field.
 *
 * Off, because most racing is single-class and a driver in one would not thank us for a
 * leaderboard that hides nobody but says it does.
 */
export const DEFAULT_ZONE_CLASS_ONLY = false;

/**
 * The zone and page a held button shows, as one property rather than a pair per zone: a glance is
 * one thing a driver configures once, and four more properties for it would be four more rows in
 * the panel for no more expressiveness. Encoded as `zoneIndex * 100 + page`.
 */
export const quickGlanceSettingName = (face: FaceSize): string => `${facePrefix(face)}QuickGlance`;
/** Zone C on the track page, which is what a glance is usually for. */
export const DEFAULT_QUICK_GLANCE = 2 * 100 + 12;

/**
 * How a face draws a flag: over the band it shares with whatever else has a claim on those sixty
 * pixels, or over the whole face.
 *
 * `band` is the default because it is what the face has always drawn and because a flag that takes
 * the screen also takes the gear with it. `full` is for the driver who wants a flag to be the only
 * thing on the face while it is up, which is the choice the nano's fifty-eight pixel band cannot
 * offer on its own.
 */
export type FlagFormat = 'band' | 'full';
export const FLAG_FORMATS: readonly FlagFormat[] = ['band', 'full'];
export const DEFAULT_FLAG_FORMAT: FlagFormat = 'band';

/**
 * `Face1920x480FlagFormat`.
 *
 * Per screen and not per rig, declared beside the zones for the reason the zones are: a rig with a
 * 1920 on the dash and an 850 on the rim is two screens read at two distances, and the one in the
 * driver's peripheral vision is exactly the one a full-face flag is for. `facePropertyNames` puts
 * it last, after the glance, because the names before it have shipped and both halves of the
 * contract assert the group by index.
 */
export const flagFormatSettingName = (face: FaceSize): string => `${facePrefix(face)}FlagFormat`;

/**
 * `Face1920x480RevBar`: what the top of *this* face carries.
 *
 * Per screen, and it should always have been. A rig with a 1920 on the dash and an 850 on the rim
 * is two screens at two distances, and the wheel that carries its own LEDs across the top is one of
 * them and not the other -- so "off, my wheel already has lights" was a rig-wide answer that turned
 * the bar off on the display as well. The rig-wide {@link REV_BAR_SETTING} stays attached as the
 * fallback below it, both because it has shipped and because the round faces' arc and the speedo
 * module still read it.
 */
export const revBarSettingName = (face: FaceSize): string => `${facePrefix(face)}RevBar`;

/**
 * When this face shows the lap review: never, in a race, or in every session.
 *
 * Per screen for the reason the flag format is, and more strongly: the review is 1200 by 160 and
 * takes the hero for four seconds at every crossing, so a rig with a display on the desk and a rim
 * in the driver's hands wants it on the one and certainly not on the other.
 *
 * Three values and not the canvas's four. `off`, `race` and `all` are answerable from
 * `SessionTypeName`, which iRacing publishes as `Race` for the one session type openDash can name
 * with certainty; a `practice` value would have to match a set of spellings -- lone, open, offline
 * testing, warmup -- that no committed trace carries, and a value that silently never matches is
 * worse than a value that is not offered. The absent one is recorded in the report rather than
 * guessed at here.
 */
export type LapReviewMode = 'off' | 'race' | 'all';
export const LAP_REVIEW_MODES: readonly LapReviewMode[] = ['off', 'race', 'all'];

/**
 * Off, because the panel covers the gear for four seconds of every lap and the lap-time pop-up
 * already gives a driver the two figures they wait for at the line in a third of the room. The
 * flag format's default is `band` for the same reason: what takes the face has to be asked for.
 */
export const DEFAULT_LAP_REVIEW: LapReviewMode = 'off';

/** `Face1920x480LapReview`. Appended after the flag format, which both halves assert by index. */
export const lapReviewSettingName = (face: FaceSize): string => `${facePrefix(face)}LapReview`;

export const quickGlanceValue = (zone: FaceZone, page: number): number => FACE_ZONE_LETTERS.indexOf(zone) * 100 + page;
export const quickGlanceZone = (value: number): FaceZone => FACE_ZONE_LETTERS[Math.floor(value / 100)] ?? 'A';
export const quickGlancePage = (value: number): number => value % 100;

/** Reads of the zone settings, each defaulted so a face works without the plugin. */
export const zone = {
  /** `isnull([OpenDash.Face1920x480ZoneB], 0)`: the page a zone is showing. */
  page: (face: FaceSize, z: FaceZone): Expr => isnull(prop(propertyName(zonePageSettingName(face, z))), num(DEFAULT_ZONE_PAGE[z])),
  /** `isnull([OpenDash.Face1920x480ZoneBPages], 2097151)`: which pages are enabled, as a mask. */
  mask: (face: FaceSize, z: FaceZone): Expr => isnull(prop(propertyName(zoneMaskSettingName(face, z))), num(defaultZoneMask(z))),
  /** `isnull([OpenDash.Face1920x480ZoneBStart], 0)`: the page the zone opens on. */
  start: (face: FaceSize, z: FaceZone): Expr => isnull(prop(propertyName(zoneStartSettingName(face, z))), num(DEFAULT_ZONE_PAGE[z])),
  /** `isnull([OpenDash.Face1920x480QuickGlance], 212)`: the zone and page a held button shows. */
  quickGlance: (face: FaceSize): Expr => isnull(prop(propertyName(quickGlanceSettingName(face))), num(DEFAULT_QUICK_GLANCE)),
  /** `isnull([OpenDash.Face1920x480BarLeft1], 0)`: which field an end of the bar shows. */
  barField: (face: FaceSize, slot: BarSlot): Expr => isnull(prop(propertyName(barFieldSettingName(face, slot))), num(DEFAULT_BAR_FIELDS[slot])),
  /** `isnull([OpenDash.Face1920x480ZoneCClassOnly], false)`: whether this zone's lists show the player's class. */
  classOnly: (face: FaceSize, z: FaceZone): Expr => isnull(prop(propertyName(zoneClassOnlySettingName(face, z))), String(DEFAULT_ZONE_CLASS_ONLY)),
  /**
   * `isnull([OpenDash.Face1920x480RevBar], isnull([OpenDash.RevBar], ...))`: what this face carries
   * at the top.
   *
   * Three fallbacks deep, and each earns its place: this face's own answer, then the rig's, then
   * the deprecated `ShiftLights` a package installed beside an rc.2 plugin would find. A settings
   * file written before the setting was per screen therefore keeps drawing what its owner chose,
   * on every face, until they answer one of them individually.
   */
  revBar: (face: FaceSize): Expr => isnull(prop(propertyName(revBarSettingName(face))), setting.revBar()),
  /** `... = 'off'`: whether this face is in the given rev bar mode. */
  revBarIs: (face: FaceSize, mode: RevBarMode): Expr => eq(zone.revBar(face), str(mode)),
  /** `isnull([OpenDash.Face1920x480FlagFormat], 'band')`: how this face draws a flag. */
  flagFormat: (face: FaceSize): Expr => isnull(prop(propertyName(flagFormatSettingName(face))), str(DEFAULT_FLAG_FORMAT)),
  /** `isnull([OpenDash.Face1920x480FlagFormat], 'band') = 'full'`: whether this face is in the given format. */
  flagFormatIs: (face: FaceSize, format: FlagFormat): Expr => eq(zone.flagFormat(face), str(format)),
  /**
   * `isnull([OpenDash.Face1920x480LapReview], 'off')`: when this face shows the lap review.
   *
   * The reading is here and what it is compared against is not: the session's own name lives in
   * `second/values.ts` with the rest of the telemetry, and this file cannot import it without a
   * cycle. `components/lapReview.ts` joins the two, which is the same seam `flagStrip.ts` sits on.
   */
  lapReview: (face: FaceSize): Expr => isnull(prop(propertyName(lapReviewSettingName(face))), str(DEFAULT_LAP_REVIEW)),
  /** `... = 'race'`: whether this face is in the given lap review mode. */
  lapReviewIs: (face: FaceSize, mode: LapReviewMode): Expr => eq(zone.lapReview(face), str(mode)),
};

/** Every property one face reads, which is the group the plugin attaches for it. */
export function facePropertyNames(face: FaceSize): string[] {
  const perZone = FACE_ZONE_LETTERS.flatMap((z) => [zonePageSettingName(face, z), zoneMaskSettingName(face, z), zoneStartSettingName(face, z), zoneClassOnlySettingName(face, z)]);
  const bar = BAR_SLOTS.map((slot) => barFieldSettingName(face, slot));
  return [...perZone, ...bar, quickGlanceSettingName(face), flagFormatSettingName(face), lapReviewSettingName(face), revBarSettingName(face)];
}

/** Every zone property of every face that ships. */
export function zoneProperties(): string[] {
  return FACE_SIZES.flatMap((face) => facePropertyNames(face)).map(propertyName);
}

/**
 * Bit `i` of a zone's mask, as arithmetic rather than as a bitwise operator.
 *
 * NCalc's grammar has `>>` and `&`, and `(mask >> i) & 1` would be half the characters. It is not
 * used because nothing in openDash has ever evaluated one on the VM, and an expression SimHub
 * cannot evaluate does not fail: it draws the empty string. That is the `left([Class], 4)` bug
 * that shipped for months and is why `ncalcFunctions.ts` exists. `truncate(x / n) % 2` is the same
 * question in three things the packages already rely on everywhere.
 *
 * The mask is an integer property, so the truncate is belt and braces rather than necessary, and
 * it costs nothing to keep the expression honest about what it means.
 */
const maskBit = (face: FaceSize, z: FaceZone, i: number): Expr => mod(truncate(div(zone.mask(face, z), num(2 ** i))), num(2));

/**
 * How long a zone's cycle is: the number of pages its mask leaves enabled.
 *
 * This is derived in the expression rather than published by the plugin, which is what
 * [ADR 0009](../../../docs/decisions/0009-does-the-plugin-compute.md) settled: a value is derived
 * from properties that already exist, so the package is still right on its own. Without the plugin
 * the mask reads as its default and the answer is the whole catalogue, which is exactly what a
 * driver with no plugin can cycle.
 */
export const zoneCycleLength = (face: FaceSize, z: FaceZone): Expr => add(...pagesForZone(z).map((_, i) => maskBit(face, z, i)));

/**
 * Where the page a zone is showing sits in its cycle, counting from one: the enabled pages before
 * it, plus itself. A page the mask has turned off counts as the one after the last enabled page
 * before it, which is a state the plugin's `Normalise` does not leave a zone in.
 */
export const zoneCyclePosition = (face: FaceSize, z: FaceZone): Expr =>
  add(num(1), ...pagesForZone(z).map((_, i) => iff(lt(num(i), zone.page(face, z)), maskBit(face, z, i), num(0))));

/** `2 / 3`: what a zone's header counts, which follows the mask and not the catalogue. */
export const zoneCounter = (face: FaceSize, z: FaceZone): Expr =>
  concat(fmt(zoneCyclePosition(face, z), '0'), str(' / '), fmt(zoneCycleLength(face, z), '0'));

/** Every counter a zone could draw, so a caller can measure the box for the widest of them. */
export function zoneCounterReadings(z: FaceZone): string[] {
  const n = pagesForZone(z).length;
  const readings: string[] = [];
  for (let length = 1; length <= n; length++) for (let position = 1; position <= length; position++) readings.push(`${position} / ${length}`);
  return readings;
}

/**
 * When a list page drawn in one of these zones shows the player's own class.
 *
 * Zones B and C are the same rectangle on most faces and so share one dashboard file, which means
 * a page in it cannot simply read "my zone's" setting: it has to ask which of the zones sharing the
 * file is showing it. The one ambiguity that leaves is both zones showing the same list page with
 * different settings, and docs/design/zones.md already records that two zones on one page is
 * reported and allowed; there they agree rather than disagreeing.
 */
export const zoneClassOnlyOnPage = (face: FaceSize, zones: readonly [FaceZone, ...FaceZone[]], page: number): Expr =>
  or(...zones.map((z) => and(eq(zone.page(face, z), num(page)), zone.classOnly(face, z))));

// --- What one screen owns ---------------------------------------------------------------------
//
// A rig is a set of screens, and a screen owns the settings it is configured with. The face
// prefixes above are one half of that; these are the other two screens and the rule that follows
// from all of them, which is that a package reads its own screen's properties and the ones every
// screen shares, and nothing else. `declaredProperties()` cannot express it: every screen's group
// is declared, so a face reading the face beside it validates cleanly and then moves when somebody
// configures the other screen.

/**
 * The prefix the pit wall's and the companion's settings carry.
 *
 * Fixed rather than derived from a size, because the landscape and the portrait package of each
 * are one screen in two orientations rather than two screens: a spotter who turns the monitor does
 * not expect to configure it again.
 */
export const PIT_WALL_PREFIX = 'PitWall';
export const COMPANION_PREFIX = 'Companion';

/** Every screen a rig can have, by the prefix its properties carry, in the order the plugin attaches them. */
export function screenPrefixes(): string[] {
  return [...FACE_SIZES.map(facePrefix), COMPANION_PREFIX, PIT_WALL_PREFIX];
}

/**
 * The properties one screen owns.
 *
 * `WebViewUrl` is the pit wall's although it carries no prefix: it was named before the idiom and
 * a published property cannot be renamed under ADR 0003, but no other screen has a web view, so
 * the group it belongs to is not in doubt.
 */
export function screenProperties(prefix: string): string[] {
  const face = faceForPrefix(prefix);
  if (face) return facePropertyNames(face).map(propertyName);
  if (prefix === PIT_WALL_PREFIX) {
    return [...allPitWallZoneSettingNames(), PIT_WALL_START_PAGE_SETTING, PIT_WALL_PAGE_SETTING, WEB_VIEW_SETTING, PIT_WALL_CLASS_ONLY_SETTING].map(propertyName);
  }
  if (prefix === COMPANION_PREFIX) return companionProperties().map(propertyName);
  throw new RangeError(`contract: no screen carries the prefix ${JSON.stringify(prefix)}`);
}

/**
 * Everything the package of one screen may not read: every property another screen owns. A card
 * face owns no screen and passes nothing, which leaves it the four modes, the twelve slots and the
 * rev bar.
 */
export function foreignProperties(owner?: string): string[] {
  const screens = screenPrefixes();
  if (owner !== undefined && !screens.includes(owner)) throw new RangeError(`contract: no screen carries the prefix ${JSON.stringify(owner)}`);
  return screens.filter((prefix) => prefix !== owner).flatMap(screenProperties);
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
 * `CompanionPage`: the module a companion is showing, as a 0-based page index.
 *
 * It is the companion's answer to a pit wall's `PitWallZoneA`: live state the plugin holds and the
 * dashboard follows, moved by the `CompanionNextModule` action and by the held glance. The start
 * module and the glance module are *not* properties beside it, and deliberately so -- a second-screen
 * property has to be read by a package, which `secondScreens.test.ts` enforces, and nothing on the
 * screen reads either of them: a start page is applied once by `Init` and a glance is a value the
 * hold copies into this one and copies back on release. That is the idiom the pit wall's own glance
 * landed on, and one idiom is enough.
 */
export const COMPANION_PAGE_SETTING = 'CompanionPage';

/** Lap times, which is the first module in page order and what a companion opens on. */
export const DEFAULT_COMPANION_PAGE = 0;

/**
 * Every property the companion owns, in the order the plugin attaches them.
 *
 * The twenty-one module switches, and then the page. The page is appended rather than inserted for
 * the reason every other name is: both halves of the contract assert this group by index.
 */
export function companionProperties(): string[] {
  return [...MODULE_CATALOGUE.map((m) => moduleSettingName(m.number)), COMPANION_PAGE_SETTING];
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
  // The three wide pages the sheet names by what the extra width buys: the lap history gains the
  // delta column, the opponents page the best lap beside the last, and the tyres page the second
  // pressure unit. The standard zone keeps the bare module name and draws the narrower page.
  { number: 2, id: 'lapHistory', name: 'Lap history · delta to best' },
  { number: 3, id: 'opponents', name: 'Opponents · best and last' },
  { number: 4, id: 'tyres', name: 'Tyres · psi and kPa' },
  { number: 5, id: 'carTelemetry', name: 'Car telemetry' },
];

/** The four configurable zones of a pit wall page. */
export const PIT_WALL_ZONE_LETTERS = ['A', 'B', 'C', 'D'] as const;
export type PitWallZoneLetter = (typeof PIT_WALL_ZONE_LETTERS)[number];

/**
 * The pages a pit wall draws, and the zones each of them places.
 *
 * **A zone belongs to a page, and this is what that fixes.** The four letters used to be the pit
 * wall's zones full stop, so the race page's A and the telemetry page's A were one setting: pointing
 * the race page's top zone at the fuel silently pointed the telemetry page's top zone there too, and
 * the tower page's pair collided with the telemetry page's lower two the same way. Nobody could tell
 * that from the panel, which listed four zones under one heading and drew a picture of three pages
 * around them. Reported as "I was not able to change the different zones", which is what it looks
 * like from the outside: you change one and another changes with it.
 *
 * Three pages of a landscape pit wall and the one portrait page, each owning its own. The portrait
 * page is a separate package and could have shared the race page's, but a spotter who has both
 * installed has two screens with different shapes and no reason to want them locked together.
 */
export interface PitWallZoneSlot {
  /** What the page calls this zone: the letter it draws, or `Wide` for the one that spans a column. */
  slot: string;
  kind: 'standard' | 'wide';
  /** The page it shows until somebody chooses, an index into the catalogue for its kind. */
  fallback: number;
}

export interface PitWallPageMeta {
  id: 'race' | 'tower' | 'telemetry' | 'portrait';
  /** What the panel calls this page. Also the middle of its settings' names. */
  name: string;
  /** Whether it is a page of the landscape package (the portrait one is its own package). */
  landscape: boolean;
  zones: readonly PitWallZoneSlot[];
}

/**
 * What each zone opens on.
 *
 * Chosen per page rather than copied across them, which is the point of the split: the race page
 * watches the field, the tower page has the wide zone for the telemetry trace and its pair for the
 * lists, and the telemetry page already draws the traces so its zones are given what the traces do
 * not say. A spotter who wants the fuel on all three can still say so; they simply are not made to.
 */
export const PIT_WALL_PAGES: readonly PitWallPageMeta[] = [
  {
    id: 'race',
    name: 'Race',
    landscape: true,
    zones: [
      { slot: 'A', kind: 'standard', fallback: 0 },
      { slot: 'B', kind: 'standard', fallback: 1 },
    ],
  },
  {
    id: 'tower',
    name: 'Tower',
    landscape: true,
    zones: [
      { slot: 'Wide', kind: 'wide', fallback: 5 },
      { slot: 'A', kind: 'standard', fallback: 4 },
      { slot: 'B', kind: 'standard', fallback: 2 },
    ],
  },
  {
    id: 'telemetry',
    name: 'Telemetry',
    landscape: true,
    zones: [
      { slot: 'A', kind: 'standard', fallback: 8 },
      { slot: 'B', kind: 'standard', fallback: 0 },
      { slot: 'C', kind: 'standard', fallback: 1 },
    ],
  },
  {
    id: 'portrait',
    name: 'Portrait',
    landscape: false,
    zones: [
      { slot: 'A', kind: 'standard', fallback: 0 },
      { slot: 'B', kind: 'standard', fallback: 1 },
      { slot: 'C', kind: 'standard', fallback: 4 },
      { slot: 'D', kind: 'standard', fallback: 2 },
    ],
  },
];

export const pitWallPage = (id: PitWallPageMeta['id']): PitWallPageMeta => {
  const page = PIT_WALL_PAGES.find((p) => p.id === id);
  if (!page) throw new Error(`no pit wall page ${id}`);
  return page;
};

export const pitWallZoneSlot = (id: PitWallPageMeta['id'], slot: string): PitWallZoneSlot => {
  const found = pitWallPage(id).zones.find((z) => z.slot === slot);
  if (!found) throw new Error(`pit wall page ${id} has no zone ${slot}`);
  return found;
};

/** `PitWallRaceA`, `PitWallTowerWide`, `PitWallTelemetryC`. */
export const pitWallZoneSettingName = (id: PitWallPageMeta['id'], slot: string): string =>
  `${PIT_WALL_PREFIX}${pitWallPage(id).name}${slot}`;

/** Every zone setting a pit wall of this orientation reads, in the order the panel lists them. */
export const pitWallZoneSettingNames = (landscape: boolean): string[] =>
  PIT_WALL_PAGES.filter((page) => page.landscape === landscape).flatMap((page) => page.zones.map((z) => pitWallZoneSettingName(page.id, z.slot)));

/** Every zone setting of every pit wall page, which is what the plugin attaches. */
export const allPitWallZoneSettingNames = (): string[] => PIT_WALL_PAGES.flatMap((page) => page.zones.map((z) => pitWallZoneSettingName(page.id, z.slot)));

/**
 * `PitWallStartPage`: which of the three landscape pages the pit wall opens on.
 *
 * A pit wall is left running on a second monitor, and which page it comes up on is the one thing a
 * spotter cannot change without walking over to it. The pages stay navigable -- this chooses the one
 * that is live, and the binding below moves it -- so nothing is taken away by answering it.
 */
export const PIT_WALL_START_PAGE_SETTING = 'PitWallStartPage';

/** `PitWallPage`: the page the pit wall is showing now. Live state the plugin holds, not a setting. */
export const PIT_WALL_PAGE_SETTING = 'PitWallPage';

/** The race page, which is what it opened on before there was a choice. */
export const DEFAULT_PIT_WALL_START_PAGE = 0;

/** The landscape pages, in the order the package draws them and the plugin numbers them. */
export const PIT_WALL_LANDSCAPE_PAGES = PIT_WALL_PAGES.filter((page) => page.landscape);

export const PIT_WALL_WIDE_ZONE_SETTING = 'PitWallWide';
export const WEB_VIEW_SETTING = 'WebViewUrl';

/**
 * `PitWallClassOnly`: whether this pit wall's lists show the player's own class.
 *
 * One setting for the screen and not one per zone, as a face has. A face's zones are four
 * rectangles of one dashboard and each can be told apart; a pit wall's are four widgets pointed at
 * one zone dashboard per rectangle, so zones A and B of the race page are the same file and a
 * per-zone filter could not reach one of them without reaching the other. The board beside them is
 * the screen's own and takes the same answer.
 *
 * Off, for the reason the face's is off: most racing is single-class, and a driver in one would not
 * thank us for a leaderboard that hides nobody but says it does.
 */
export const PIT_WALL_CLASS_ONLY_SETTING = 'PitWallClassOnly';
export const DEFAULT_PIT_WALL_CLASS_ONLY = false;

/** The URL the web view page shows until the user sets one. Empty means "nothing configured". */
export const DEFAULT_WEB_VIEW_URL = '';

/** Reads of the second-screen settings, each defaulted so the dashboards work without the plugin. */
export const secondScreen = {
  /**
   * `isnull([OpenDash.CompanionModule07], 1)`: a screen's enabled expression, which SimHub reads
   * as a number and treats as enabled when it is above zero. A boolean property converts to 1.
   */
  moduleEnabled: (number: number): Expr => isnull(prop(propertyName(moduleSettingName(number))), num(moduleAt(number).enabled ? 1 : 0)),
  /** `isnull([OpenDash.CompanionPage], 0)`: the module the companion is showing, 0-based. */
  companionPage: (): Expr => isnull(prop(propertyName(COMPANION_PAGE_SETTING)), num(DEFAULT_COMPANION_PAGE)),
  /**
   * A module's screen is enabled when the rotation leaves it on *and* it is the page the plugin is
   * showing, which is what makes the companion one screen at a time rather than a ring SimHub pages.
   *
   * Both halves earn their place. The page is what a wheel button moves, so it is what decides which
   * of the twenty-one is up; the rotation is still asked, so that a driver with no plugin sees the
   * first module they have left on rather than a screen they switched off, and so that the switches
   * are read by the package that offers them. SimHub re-evaluates every screen's expression each
   * frame and moves off a screen that has stopped being enabled, which is the same mechanism the two
   * arrangements of a zone face are chosen by.
   */
  moduleShown: (number: number): Expr => and(secondScreen.moduleEnabled(number), eq(secondScreen.companionPage(), num(number - 1))),
  /** `isnull([OpenDash.PitWallRaceA], 0)`: which page one page's zone shows. */
  zonePage: (id: PitWallPageMeta['id'], slot: string): Expr =>
    isnull(prop(propertyName(pitWallZoneSettingName(id, slot))), num(pitWallZoneSlot(id, slot).fallback)),
  /** `isnull([OpenDash.PitWallPage], 0)`: the landscape page the pit wall is showing. */
  pitWallPage: (): Expr => isnull(prop(propertyName(PIT_WALL_PAGE_SETTING)), num(DEFAULT_PIT_WALL_START_PAGE)),
  /** `isnull([OpenDash.PitWallPage], 0) = 1`: whether this page is the one that is live. */
  pitWallPageIs: (number: number): Expr => eq(secondScreen.pitWallPage(), num(number)),
  /** `isnull([OpenDash.WebViewUrl], '')`: the address of the web view page. */
  webViewUrl: (): Expr => isnull(prop(propertyName(WEB_VIEW_SETTING)), str(DEFAULT_WEB_VIEW_URL)),
  /** `isnull([OpenDash.PitWallClassOnly], false)`: whether this pit wall's lists show the player's class. */
  classOnly: (): Expr => isnull(prop(propertyName(PIT_WALL_CLASS_ONLY_SETTING)), String(DEFAULT_PIT_WALL_CLASS_ONLY)),
};

// --- The flag box ---------------------------------------------------------------------------
//
// An 8x8 LED matrix is not a screen, but its settings are ordinary SimHub properties for exactly
// the reason ADR 0003 gives for the screens: a property is readable by anything and changeable
// while driving. The profile reads these through `flagBox` below, every one wrapped in isnull()
// with its default, so a user who imports the profile and never installs the plugin still gets a
// working box. ADR 0013 is why the plugin does not install the profile itself.
//
// Rotation and serpentine wiring are deliberately absent: they are SimHub device settings decided
// by the corner the data cable enters, and a second place to set them would be a second place to
// disagree. The guide documents them instead.

/**
 * SimHub composes at most four matrix contents, so a box setting exists once per matrix. People do
 * own more than one box — two in the corners of a monitor stand, one showing flags and one showing
 * the gear, is a setup somebody will build on day one — and #175 settled that a screen owns its
 * settings as one group. A device is the same shape of thing, so it gets the same treatment.
 *
 * Rotation and serpentine wiring are deliberately **not** here. They are SimHub device settings
 * decided by the corner the data cable enters, and duplicating them would produce two places that
 * disagree. The guide documents them instead.
 *
 * Presets are not here either. openDash has no store: a setting *is* a SimHub property, which is
 * what makes it readable by anything and changeable while driving. A preset is a set of values with
 * a name, which is a different feature with its own storage, its own migration and its own failure
 * when a property is added.
 */
export const FLAG_BOX_MATRICES = [1, 2, 3, 4] as const;
export type FlagBoxMatrix = (typeof FLAG_BOX_MATRICES)[number];

/** What a matrix shows when nothing has taken it over. */
export type FlagBoxRest = 'dark' | 'gear';
export const FLAG_BOX_RESTS: readonly FlagBoxRest[] = ['dark', 'gear'];

/**
 * Which side of the rig a box is mounted on. The spotter reads it, and getting it wrong is worse
 * than having no box: one to the left of the wheel lighting for a car on the right is actively
 * dangerous. `both` is the single-box setup, where one panel has to show both sides.
 */
export type FlagBoxSide = 'both' | 'left' | 'right';
export const FLAG_BOX_SIDES: readonly FlagBoxSide[] = ['both', 'left', 'right'];

/** `FlagBoxMatrix1Rest` and its siblings. Prefixed, because a property name is a public interface. */
export const flagBoxMatrixSetting = (matrix: FlagBoxMatrix, name: string): string => `FlagBoxMatrix${matrix}${name}`;

/** What each matrix does by default: matrix 1 does everything, 2 to 4 are off. One box works out of the box. */
export interface FlagBoxMatrixDefaults {
  rest: FlagBoxRest;
  flags: boolean;
  /** The limiter, the lane and speeding. Its own switch: a driver who silences flags still wants it. */
  pit: boolean;
  spotter: boolean;
  warnings: boolean;
  side: FlagBoxSide;
}

export const FLAG_BOX_MATRIX_DEFAULTS: Record<FlagBoxMatrix, FlagBoxMatrixDefaults> = {
  1: { rest: 'gear', flags: true, pit: true, spotter: true, warnings: true, side: 'both' },
  2: { rest: 'dark', flags: false, pit: false, spotter: false, warnings: false, side: 'both' },
  3: { rest: 'dark', flags: false, pit: false, spotter: false, warnings: false, side: 'both' },
  4: { rest: 'dark', flags: false, pit: false, spotter: false, warnings: false, side: 'both' },
};

/**
 * Reads of one matrix's settings.
 *
 * Four of these were global: whether the catalogue was silenced to the critical flags, whether the
 * gear was the resting state, and the two temperature thresholds. A rig with a box in each corner
 * of a monitor stand could therefore not have one showing the whole catalogue and the other showing
 * the gear alone, which is precisely the setup the per-matrix group exists for, so they belong to a
 * box rather than to the tab. The old names are migrated by the plugin, since ADR 0003 makes a
 * property name a public interface.
 *
 * `lowFuelLaps` is deliberately *not* among them and stays on {@link flagBox}: it is the rig's one
 * answer to "am I low", read by the strip and the faces as well as by every box, and a per-box copy
 * of it would be four more places to disagree.
 */
export const flagBoxMatrix = (matrix: FlagBoxMatrix) => {
  const d = FLAG_BOX_MATRIX_DEFAULTS[matrix];
  const read = (name: string, fallback: Expr): Expr => isnull(prop(propertyName(flagBoxMatrixSetting(matrix, name))), fallback);
  return {
    rest: (): Expr => read('Rest', str(d.rest)),
    flags: (): Expr => read('Flags', String(d.flags)),
    pit: (): Expr => read('Pit', String(d.pit)),
    spotter: (): Expr => read('Spotter', String(d.spotter)),
    warnings: (): Expr => read('Warnings', String(d.warnings)),
    side: (): Expr => read('Side', str(d.side)),
    /** Quiet until something matters, for this panel. */
    criticalOnly: (): Expr => read('CriticalOnly', String(DEFAULT_FLAG_BOX_CRITICAL_ONLY)),
    /** The gear as this panel's resting state. */
    gear: (): Expr => read('Gear', String(DEFAULT_FLAG_BOX_GEAR)),
    /**
     * Whether this panel's digit flashes while the car is over-revving.
     *
     * Per panel rather than per rig, and a switch rather than a fact, because the flash is the one
     * thing on the box that repeats what a screen and a strip are already saying: a driver with a
     * rev bar in front of them has been told to shift twice before the box joins in, and a box
     * mounted in the corner of a monitor stand strobing at the edge of vision is what they turn
     * off. Off leaves the digit in the redline colour, which is still the whole of the message.
     */
    gearBlink: (): Expr => read('GearBlink', String(DEFAULT_FLAG_BOX_GEAR_BLINK)),
    /** Defaulted **per unit** from SimHub's own `TemperatureUnit`, as the global one was. */
    oilTemp: (): Expr => read('OilTemp', defaultByUnit(DEFAULT_OIL_TEMP)),
    waterTemp: (): Expr => read('WaterTemp', defaultByUnit(DEFAULT_WATER_TEMP)),
  };
};

/** The eleven property names of one matrix, in the order the plugin attaches them. */
export const flagBoxMatrixProperties = (matrix: FlagBoxMatrix): string[] =>
  // The four that moved here from the tab are appended rather than interleaved, for the reason
  // every other list in this file is: both halves of the contract are pinned in order.
  ['Rest', 'Flags', 'Pit', 'Spotter', 'Warnings', 'Side', 'CriticalOnly', 'Gear', 'OilTemp', 'WaterTemp', 'GearBlink'].map((n) => flagBoxMatrixSetting(matrix, n));

/**
 * Brightness and night mode are named `Lights*`, not `FlagBox*`, deliberately. A driver who owns a
 * flag box probably owns other lights, and "how bright are my lights and is it night" is one
 * answer for a rig rather than one per device. If openDash ever ships a second profile it reads
 * these same three properties; naming them per device now would mean renaming a public interface
 * later, which ADR 0003 says a property name is.
 */
export const LIGHTS_BRIGHTNESS_SETTING = 'LightsBrightness';
export const LIGHTS_NIGHT_BRIGHTNESS_SETTING = 'LightsNightBrightness';
export const LIGHTS_NIGHT_MODE_SETTING = 'LightsNightMode';

/**
 * How few laps of fuel is low, for every light openDash drives rather than for the box alone.
 *
 * Named `Lights*` for the reason the three above are: one threshold answers "am I low" for the
 * strip, the rev bar and the box, and three copies of it would be three places to disagree.
 * {@link FLAG_BOX_LOW_FUEL_LAPS_SETTING} is the name that shipped and is not retired with it:
 * it stays attached as the deprecated alias {@link flagBox.lowFuelLaps} falls back through, so
 * that a rig set up against rc.2 keeps the number its driver chose. ADR 0003 makes a published
 * property name a public interface, and #170 is the rule that one does not vanish without a
 * release of warning.
 */
export const LIGHTS_LOW_FUEL_LAPS_SETTING = 'LightsLowFuelLaps';


/** Percent. SimHub's own global brightness for the device applies on top of this. */
export const DEFAULT_LIGHTS_BRIGHTNESS = 100;

/**
 * Percent, at night. Sixty-four LEDs at full output beside a wheel in a dark room is genuinely
 * too bright, and no amount of good colour choice fixes it; a quarter is the starting point, and
 * it is a setting because the right number depends on the room.
 */
export const DEFAULT_LIGHTS_NIGHT_BRIGHTNESS = 25;

/** Off. A switch the driver flips, not a time of day we guess at. */
export const DEFAULT_LIGHTS_NIGHT_MODE = false;

/** On. The gear is the box's resting state; off leaves the panel dark rather than showing something else. */
export const DEFAULT_FLAG_BOX_GEAR = true;

/**
 * On. The digit flashes while the car is over-revving, which is the box's half of the one shift
 * message the rev bar and the strip also carry, and a driver who has only the box would lose the
 * loudest part of it if this defaulted off.
 */
export const DEFAULT_FLAG_BOX_GEAR_BLINK = true;

/**
 * Laps, not litres. A litre threshold means nothing without knowing the car; laps remaining means
 * something in every car, and SimHub already publishes `Fuel_RemainingLaps`.
 */
export const FLAG_BOX_LOW_FUEL_LAPS_SETTING = 'FlagBoxLowFuelLaps';
export const DEFAULT_FLAG_BOX_LOW_FUEL_LAPS = 2;

/**
 * Degrees, in **SimHub's unit**. A driver in Fahrenheit who sets 120 and gets a Celsius threshold
 * has been given a broken feature, and a threshold that silently converts is worse than one that
 * refuses — so the comparison is done in whatever unit `WaterTemperature` and `OilTemperature` are
 * already reported in, which is the user's, and the default is stated per unit below.
 */
/** 120 °C and 110 °C, and their equivalents, so a default is right in whatever unit is set. */
export const DEFAULT_OIL_TEMP: Record<string, number> = { Celcius: 120, Fahrenheit: 248, Kelvin: 393 };
export const DEFAULT_WATER_TEMP: Record<string, number> = { Celcius: 110, Fahrenheit: 230, Kelvin: 383 };

/**
 * Off, so the box shows the whole catalogue until the driver asks for quiet. The default is the
 * one that tells a driver the most; a box that stays dark through a chequered flag is a surprise,
 * and a surprise is a worse default than a busy one.
 */
export const DEFAULT_FLAG_BOX_CRITICAL_ONLY = false;

/**
 * Whether the spotter bar grows inwards or is simply there.
 *
 * The rig's rather than a box's, for the reason the brightness trio is: a driver who finds a moving
 * bar distracting finds it distracting on every panel they own.
 *
 * Off, unlike the flags' own switch, and the difference is the whole of what the box's vocabulary
 * says. Movement means act: a flag that ends or interrupts the race moves, and everything that
 * merely informs is held. A car alongside informs, so the bar that says so holds by default and
 * moves only for a driver who has asked it to.
 */
export const FLAG_BOX_SPOTTER_ANIMATION_SETTING = 'FlagBoxSpotterAnimation';
export const DEFAULT_FLAG_BOX_SPOTTER_ANIMATION = false;

/** Reads of the lights settings, each defaulted so the profile works without the plugin. */
export const flagBox = {
  /** `isnull([OpenDash.LightsBrightness], 100)`: the day brightness. */
  dayBrightness: (): Expr => isnull(prop(propertyName(LIGHTS_BRIGHTNESS_SETTING)), num(DEFAULT_LIGHTS_BRIGHTNESS)),
  /** `isnull([OpenDash.LightsNightBrightness], 25)`. */
  nightBrightness: (): Expr => isnull(prop(propertyName(LIGHTS_NIGHT_BRIGHTNESS_SETTING)), num(DEFAULT_LIGHTS_NIGHT_BRIGHTNESS)),
  /** `isnull([OpenDash.LightsNightMode], false)`. */
  nightMode: (): Expr => isnull(prop(propertyName(LIGHTS_NIGHT_MODE_SETTING)), String(DEFAULT_LIGHTS_NIGHT_MODE)),
  /** The brightness in force: the night value when night mode is on, else the day value. */
  brightness: (): Expr => iff(eq(flagBox.nightMode(), 'true'), flagBox.nightBrightness(), flagBox.dayBrightness()),
  /**
   * `isnull([OpenDash.LightsLowFuelLaps], isnull([OpenDash.FlagBoxLowFuelLaps], 2))`: how few laps
   * of fuel is low, for every light rather than for the box alone.
   *
   * Two fallbacks deep, exactly as {@link setting.revBar} is and for the same reason. The inner one
   * is the deprecated alias, so that a profile installed beside an rc.2 plugin -- which attaches
   * `FlagBoxLowFuelLaps` and not `LightsLowFuelLaps` -- still reads the number that user set; the
   * innermost is the default a profile without any plugin shows.
   */
  lowFuelLaps: (): Expr =>
    isnull(prop(propertyName(LIGHTS_LOW_FUEL_LAPS_SETTING)), isnull(prop(propertyName(FLAG_BOX_LOW_FUEL_LAPS_SETTING)), num(DEFAULT_FLAG_BOX_LOW_FUEL_LAPS))),
  /** `isnull([OpenDash.FlagBoxSpotterAnimation], false)`: whether the spotter bar grows. */
  spotterAnimation: (): Expr => isnull(prop(propertyName(FLAG_BOX_SPOTTER_ANIMATION_SETTING)), String(DEFAULT_FLAG_BOX_SPOTTER_ANIMATION)),
};

/** `if(unit = 'Fahrenheit', 248, if(unit = 'Kelvin', 393, 120))`, so no default is wrong in a unit. */
function defaultByUnit(byUnit: Record<string, number>): Expr {
  const unit = isnull(ncalc.game('TemperatureUnit'), str('Celcius'));
  const celsius = byUnit.Celcius ?? 0;
  return iff(eq(unit, str('Fahrenheit')), num(byUnit.Fahrenheit ?? celsius), iff(eq(unit, str('Kelvin')), num(byUnit.Kelvin ?? celsius), num(celsius)));
}

/** Every property the flag box profile reads. */
export function flagBoxProperties(): string[] {
  // Five, not nine. Critical flags only, the gear and the two temperature thresholds moved under
  // the matrix that owns them; what is left is the rig's brightness trio and the one low-fuel
  // threshold every light and every face reads.
  const global = [
    LIGHTS_BRIGHTNESS_SETTING,
    LIGHTS_NIGHT_BRIGHTNESS_SETTING,
    LIGHTS_NIGHT_MODE_SETTING,
    FLAG_BOX_LOW_FUEL_LAPS_SETTING,
    // Appended rather than placed beside the other Lights* names: this list is pinned in order by
    // packages/dash/test/declared-properties.txt, and both halves of the contract assert its head
    // by index, so a new name joins the end of the group and is never inserted into it.
    LIGHTS_LOW_FUEL_LAPS_SETTING,
    FLAG_BOX_SPOTTER_ANIMATION_SETTING,
  ];
  const perMatrix = FLAG_BOX_MATRICES.flatMap(flagBoxMatrixProperties);
  return [...global, ...perMatrix].map(propertyName);
}
