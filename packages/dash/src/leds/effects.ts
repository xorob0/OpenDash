/**
 * The effect catalogue: everything a strip shows that is not the rev ladder.
 *
 * Each effect is a row of data rather than a function, so that adding one is adding a row and the
 * whole catalogue can be read in one screen. What each row carries is the *source* — the exact
 * SimHub property, verified against the decompiled 9.12.6 assemblies and recorded in
 * docs/research/simhub-led-sources.md — and an honest note where iRacing does not publish it.
 *
 * **Best effort, where SimHub has a property and iRacing does not fill it.** `TCActive`,
 * `TurnIndicatorLeft` and `TurnIndicatorRight` are real, exposed members of `StatusDataBase`,
 * filled by whichever game reader is running. The iRacing reader overrides all three with
 * `[NotAvailable] return 0`, so on iRacing they are dark — and on a sim that does fill them they
 * light. They ship for that reason.
 *
 * That is a different judgement from the one the second screens make, and deliberately so. The rule
 * in scope.md is that a module which reads something iRacing does not publish **says so rather than
 * drawing a zero**, and it is about a readout: `0.00` on a screen asserts a measurement that was
 * never taken. An LED that stays dark asserts nothing. So an effect whose property exists is shipped
 * wherever it earns its lamp and simply does not light, and {@link BEST_EFFORT} records which those
 * are and why.
 *
 * **What is not shipped is what has no property at all**, in any sim: there is no headlight or beam
 * field in `StatusDataBase`, no `KERS` member anywhere in SimHub 9.12.6, and no water pressure. A
 * grep of the decompiled `GameReaderCommon.dll` finds zero of each. {@link NO_PROPERTY} lists them
 * with the nearest thing that does exist.
 *
 * **And there is a third case, neither refused nor absent**: a property that exists, that some sim
 * fills, and that no lamp is spent on all the same. A side has four LEDs on the wheels most people
 * own, so a row earns its lamp or it does not ship; {@link DROPPED} records those and why, because
 * a reader who finds `ERSPercent` in SimHub and not here should be able to tell a judgement from an
 * oversight.
 */
import { ncalc, leds } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { FLAG_BLINK_MS } from '../components/flagStrip.ts';
import { setting } from '../contract.ts';
import { conditionRaised, flagCondition, safeBitSet, type FlagCondition } from '../flags.ts';
import { tankIsLow } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { type EffectRole, type Lamp } from './lamps.ts';

const { add, and, eq, game, gt, isnull, not, num, or, prop, raw } = ncalc;

export interface LedEffect {
  id: string;
  /** What it is called in the panel and in the profile. */
  label: string;
  /**
   * Which lamp it lands on, or `strip` for the whole run. A `strip` effect blanks what is
   * underneath rather than composing over it: an effect that takes every LED says what it means
   * only if nothing else shows through, and it is the one thing the canvas allows to hide the
   * ladder.
   */
  role: EffectRole;
  /** For an effect about one side of the car, the only side it lights. */
  side?: 'left' | 'right';
  /** When it lights. */
  when: Expr;
  color: string;
  /**
   * Whether the property behind it is one iRacing leaves at a hard zero: the light is real, exposed
   * and filled by some readers, and dark on iRacing whatever happens. {@link BEST_EFFORT} says which
   * and why; this is the same fact where the code can act on it, which is when two conditions share
   * one lamp and one of them can never come on.
   */
  bestEffort?: boolean;
  /** When it blinks, if it does. */
  blinkWhen?: Expr;
  /**
   * The off phase, where a second colour is itself a fact — push to pass is blue while it is there
   * and green while it is being spent. Left out, the lamp goes dark instead, which is what makes a
   * blink a blink; see {@link BLINK_OFF}.
   */
  blinkColor?: string;
  blinkDelayMs?: number;
  /** The SimHub property this reads, for the guide and for the test that keeps the two honest. */
  source: string;
}

// --- the two rates, and the colour of an off phase ---------------------------------------------

/**
 * The half periods of the only two rhythms a lamp blinks in: 4 Hz for what is urgent and 2 Hz for
 * what is merely true. Two is what the eye sorts at speed, and a rate that is neither reads as one
 * of them anyway, so a third would be a distinction the driver cannot collect.
 *
 * The slow one is the flag band's own, so a yellow on the face and a yellow on the strip flash
 * together rather than drifting against each other. Neither is derived from `shiftLights.flashHz`,
 * which was the previous arrangement and which tied every lamp on the strip to the over-rev flash:
 * a change to the shift lights moved the indicators with it, which is the kind of coupling nobody
 * discovers until both are wrong.
 *
 * The rev ladder's own over-rev flash takes the fast rate too (`rpmStrip.ts`), so these two are the
 * whole of what a strip blinks at. That leaves the strip at 4 Hz where the face's redline is still
 * at `shiftLights.flashHz`, which is 8: the canvas moves that token to 4 and closes the gap, and the
 * token is the author's to move.
 */
export const SLOW_BLINK_MS = FLAG_BLINK_MS;
export const FAST_BLINK_MS = Math.round(SLOW_BLINK_MS / 2);

/**
 * The off phase of every blink that does not name a second colour of its own.
 *
 * SimHub's `StaticColorContainerBase` fills the run with `BlinkingColor` while blinking and with
 * `Color` otherwise, so an effect writing its own colour into both fields alternates a colour with
 * itself and has never flashed at all: the first defect of the lights review, and the reason the
 * notes kept asking for blinking that was already there. Transparent will not do either, because
 * the merge drops transparent pixels and the rev ladder underneath would show through the gap, so
 * the off phase takes the darkest opaque value the palette has, which is the nearest thing the
 * design system holds to an LED that is simply off.
 *
 * It is exported because the rev ladder had the same defect in the one file the catalogue does not
 * reach: a top-band rung wrote its own red into both fields, so the over-rev flash was a red LED
 * staying red. One value for both, or a driver learns two off phases.
 */
export const BLINK_OFF = ds.color.surface.base;

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

/**
 * Low fuel, as {@link tankIsLow}: the laps remaining against the one threshold in laps the driver
 * set, which is `LightsLowFuelLaps` with the box's deprecated name behind it.
 *
 * It read `CarSettings_FuelAlertActive` before, which is SimHub's own alert and is what the native
 * LED container reads. That is a different question from the one the flag box asks, so a strip and
 * a box on the same rig could come on at different moments and a driver would have two answers to
 * "am I low" with one number in the panel that moved only one of them.
 */
const lowFuel = (): Expr => tankIsLow();

/**
 * The engine turning, which is what an engine warning has to be read against. `EngineWarnings` sets
 * its oil-pressure bit on an engine that is merely stopped as readily as on one that is failing, so
 * without this the car lamp is red in every garage and on every grid, and a lamp that is red when
 * nothing is wrong is a lamp the driver stops reading by the third session.
 */
const engineRunning = (): Expr => gt(g('Rpms'), num(0));

/**
 * What a race lamp can draw, highest rank first: one row per appearance, each taking the catalogue
 * conditions a lamp cannot tell apart.
 *
 * The strip used to rank the six normalised `Flag_*` summaries, which fold four iRacing bits into
 * `Flag_Yellow` and hide a furled black and a disqualification behind `Flag_Black` entirely, so the
 * box and the strip could disagree about which flag was out. These rows rank `FLAG_CATALOGUE`
 * through `SessionFlagsDetails` the way `profile.ts` ranks it for the matrix, so nine conditions
 * that could not reach a strip at all now do.
 *
 * **A row is an appearance, not a condition.** One LED has a hue and a rhythm and nothing else, so
 * fifteen conditions cannot each be drawn differently; the canvas's own answer is that the ones a
 * lamp cannot tell apart map onto the ones it can. A row therefore carries every condition that
 * draws the same way, and because the rows keep the catalogue's order and take contiguous runs of
 * it, ranking the rows *is* ranking the catalogue: the strip and the box cannot pick different
 * flags. A condition with no row is skipped rather than allowed to hold the lamp dark, which is the
 * same rule `drawnFlags()` applies to a condition with no glyph.
 *
 * On the rates: the black family is the fast tier because it is addressed to this car, and
 * everything else is the flag band's own 2 Hz, so a yellow on the face and a yellow on the strip
 * flash together. On the colours: where a row names a second lit colour it is because the second
 * colour is itself the fact, and where it does not, the off phase is {@link BLINK_OFF}.
 *
 * Two rows the canvas draws are missing, and both are blocked on a token rather than on this file:
 *
 *  - **Red, and the start gantry that draws in red held.** `purpose.fuel.low` resolves to
 *    `color.danger.primary`, the same `#FF2D46` as `purpose.flag.red`, and the low-fuel lamp blinks
 *    at the same 2 Hz a red flag would. On a two-LED side the flags share their lamp with the car
 *    warnings, so the two would be one light with two meanings. The canvas asks for amber on low
 *    fuel; until that token moves, a red flag is no more visible on a strip than it was before.
 *  - **The meatball in `purpose.flag.orange`.** That token resolves to `color.caution.primary`,
 *    which is the temperature warning's amber at the same fast rate and on the same shared lamp.
 *    The meatball is therefore folded into the black row, which is the family it belongs to and is
 *    at least the right instruction, rather than drawn as a light the driver already knows as a
 *    temperature warning.
 */
interface FlagRow {
  /** Stable id, suffixed onto `flag.`; the catalogue id of the condition the row is named for. */
  id: string;
  /** The container description in the profile, and what the panel calls it. */
  label: string;
  /** The catalogue ids this row draws, in the catalogue's own order. */
  conditions: readonly string[];
  color: string;
  /** The other half of the alternation, where the second colour is itself a fact. */
  blinkColor?: string;
  blinkDelayMs: number;
}

export const FLAG_ROWS: readonly FlagRow[] = [
  // The disqualification and the furled black are the black flag's own family and draw as it does;
  // the meatball is here for the reason the header gives rather than because it draws the same way.
  {
    id: 'black',
    label: 'Black flag',
    conditions: ['disqualify', 'black', 'furled', 'meatball'],
    color: ds.purpose.flag.black,
    blinkDelayMs: FAST_BLINK_MS,
  },
  // The whole track rather than this corner, said by alternating the flag yellow with the caution
  // amber. The amber is the steady half on purpose: the caution and the plain yellow are on one
  // lamp at the same 2 Hz, so the colour they are read by at the instant of a glance has to differ.
  {
    id: 'caution',
    label: 'Full-course caution',
    conditions: ['caution'],
    color: ds.color.caution.primary,
    blinkColor: ds.purpose.flag.yellow,
    blinkDelayMs: SLOW_BLINK_MS,
  },
  { id: 'yellow', label: 'Yellow flag', conditions: ['yellowWaving', 'yellow'], color: ds.purpose.flag.yellow, blinkDelayMs: SLOW_BLINK_MS },
  // The yellow hue at the fast rate, which is the one thing a lamp has left to say "and there is
  // something on the road" with.
  { id: 'debris', label: 'Debris flag', conditions: ['debris'], color: ds.purpose.flag.debris, blinkDelayMs: FAST_BLINK_MS },
  { id: 'blue', label: 'Blue flag', conditions: ['blue'], color: ds.purpose.flag.blue, blinkDelayMs: SLOW_BLINK_MS },
  { id: 'white', label: 'White flag', conditions: ['white'], color: ds.purpose.flag.white, blinkDelayMs: SLOW_BLINK_MS },
  { id: 'green', label: 'Green flag', conditions: ['green'], color: ds.purpose.flag.green, blinkDelayMs: SLOW_BLINK_MS },
  // Last, because the catalogue ranks the chequer last: it is news where everything above it is an
  // instruction, and a chequer that outranked them hid a yellow thrown at a race finishing under
  // one. The drawing is the reverse assignment of the black: ground steady, white on the blink.
  // SimHub fills the run with BlinkingColor while blinking and with Color otherwise, so exchanging
  // the two fields is exactly what antiphase means in this format and needs no phase control. It is
  // also what stops the two whites being one light, since both resolve to #F5F7FA.
  {
    id: 'chequered',
    label: 'Chequered flag',
    conditions: ['chequered'],
    color: BLINK_OFF,
    blinkColor: ds.purpose.flag.chequer,
    blinkDelayMs: SLOW_BLINK_MS,
  },
];

/** The catalogue conditions of one row, resolved once. */
const rowConditions = (row: FlagRow): FlagCondition[] => row.conditions.map(flagCondition);

/**
 * One effect per row, highest priority last so that it composes on top.
 *
 * A flag lives on the race lamp and on nothing else. It used to take the whole strip and blank it,
 * which is how a blue flag held for a minute took the rev ladder with it — the note the whole
 * lights review started from. What it costs is that a shape with no lamps has nowhere to put a
 * flag; `rpmStrip.ts` keeps the whole run there rather than dropping it, which is the behaviour a
 * brow has today and is not this change's to settle.
 */
export const flagEffects = (): LedEffect[] =>
  FLAG_ROWS.map((row, index) => {
    const mine = rowConditions(row);
    const above = FLAG_ROWS.slice(0, index).flatMap(rowConditions);
    // Null-safe, because a CustomStatus answers a throwing formula with on: a car that publishes no
    // SessionFlagsDetails would otherwise show every flag at once rather than none.
    const raised = (c: FlagCondition): Expr => conditionRaised(c, safeBitSet);
    const when = and(...above.map((c) => not(raised(c))), or(...mine.map(raised)));
    return {
      id: `flag.${row.id}`,
      label: row.label,
      role: 'race' as const,
      when,
      color: row.color,
      blinkWhen: when,
      ...(row.blinkColor ? { blinkColor: row.blinkColor } : {}),
      blinkDelayMs: row.blinkDelayMs,
      source: mine.flatMap((c) => c.bits).map((bit) => `DataCorePlugin.GameRawData.Telemetry.SessionFlagsDetails.Is${bit}`).join(', '),
    };
  }).reverse();

/**
 * The catalogue, lowest rank first within each role, which is also composition order: what is later
 * shows over what is earlier, and {@link lampConditions} reads the order backwards to get the rank.
 * The three warnings a car raises about itself carry the `car` role and everything a car does for
 * its driver carries `aid`, which is the distinction the lamps are built on: a car warning always
 * outranks an aid.
 *
 * Within a role the rank is the driver's cost of missing it. On the car lamp that is oil pressure,
 * then temperature, then fuel: the first ends the engine in a lap, the second in a stint, and the
 * third only ends the race. On the aid lamp it is ABS, then traction control, then DRS, then push to
 * pass, which is the order in which the car is doing something the driver did not ask for and then,
 * lower down, something the driver did.
 */
export const SIDE_EFFECTS: readonly LedEffect[] = [
  {
    id: 'p2p',
    label: 'Push to pass',
    role: 'aid',
    // Blue while one is in hand and green while one is being spent — the same available-then-active
    // pair DRS draws, which is what lets the two share the second aid lamp of a five-LED side.
    when: gt(isnull(prop('DataCorePlugin.GameRawData.Telemetry.PlayerP2P_Count'), num(0)), num(0)),
    color: ds.color.info.primary,
    blinkWhen: eq(isnull(prop('DataCorePlugin.GameData.PushToPassActive'), num(0)), num(1)),
    blinkColor: ds.color.good.primary,
    blinkDelayMs: FAST_BLINK_MS,
    source: 'DataCorePlugin.GameData.PushToPassActive, GameRawData.Telemetry.PlayerP2P_Count',
  },
  {
    id: 'drs',
    label: 'DRS',
    role: 'aid',
    when: or(on('DRSAvailable'), on('DRSEnabled')),
    color: ds.color.good.primary,
    // Available is a steady light and open is a flashing one, which is how a driver tells them apart.
    blinkWhen: on('DRSEnabled'),
    blinkDelayMs: FAST_BLINK_MS,
    source: 'DataCorePlugin.GameData.DRSAvailable / DRSEnabled',
  },
  {
    id: 'tc',
    label: 'Traction control',
    role: 'aid',
    bestEffort: true,
    // The intervention, and nothing else. It used to light on TCLevel as well, so that some light
    // was on wherever the dial sat, and since iRacing fills the dial and not the intervention the
    // result there was a lamp lit from the green flag to the flag: a lamp that is always on carries
    // no information, and one that means "the dial is at four" is not what the driver looks at when
    // a wheel spins. Dark on iRacing is the honest reading, and BEST_EFFORT says so.
    when: gt(g('TCActive'), num(0)),
    color: ds.color.info.primary,
    source: 'DataCorePlugin.GameData.TCActive',
  },
  {
    id: 'abs',
    label: 'ABS active',
    role: 'aid',
    when: gt(g('ABSActive'), num(0)),
    // Amber here and blue on traction control, which is the way round UN R121 and the car manuals
    // put the pair. The build drew them reversed, and a driver who has read either one anywhere else
    // reads the reversal as the other system.
    color: ds.color.caution.primary,
    source: 'DataCorePlugin.GameData.ABSActive',
  },
  {
    id: 'lowFuel',
    label: 'Low fuel',
    role: 'car',
    when: lowFuel(),
    color: ds.purpose.fuel.low,
    blinkWhen: lowFuel(),
    blinkDelayMs: SLOW_BLINK_MS,
    source: 'DataCorePlugin.Computed.Fuel_RemainingLaps against OpenDash.LightsLowFuelLaps',
  },
  {
    id: 'temperature',
    label: 'Water or oil temperature warning',
    role: 'car',
    // Both temperature bits of iRacing's EngineWarnings word: 1 for water and 0x0040 for oil. The
    // second has existed since 2021 season 2, and this file used to deny it outright under
    // NO_PROPERTY, which is a claim the review corrected rather than a property that arrived.
    // docs/research/simhub-led-sources.md still carries the old claim in two places and wants the
    // same correction; it is not this file's to make.
    when: or(engineWarning(1), engineWarning(64)),
    color: ds.color.caution.primary,
    blinkWhen: or(engineWarning(1), engineWarning(64)),
    blinkDelayMs: FAST_BLINK_MS,
    source: 'DataCorePlugin.GameRawData.Telemetry.EngineWarnings bits 1 (WaterTempWarning) and 64 (OilTempWarning)',
  },
  {
    id: 'oilPressure',
    label: 'Oil pressure warning',
    role: 'car',
    // Gated on the engine turning, because the bit is as true of an engine that is merely stopped as
    // of one that is failing, and the lamp otherwise greets the driver in every garage.
    when: and(engineWarning(4), engineRunning()),
    color: ds.color.danger.primary,
    blinkWhen: and(engineWarning(4), engineRunning()),
    blinkDelayMs: FAST_BLINK_MS,
    source: 'DataCorePlugin.GameRawData.Telemetry.EngineWarnings bit 4 (OilPressureWarning), gated on GameData.Rpms',
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

/**
 * The spotters, each on the side the car is actually on, and steady.
 *
 * They used to blink red when a car was alongside on both sides at once. With one lamp to a side,
 * two lit side lamps already *are* the both-sides signal, and a blink on top of it says the same
 * thing a second time in a rhythm the car lamp is using for something else. It is in
 * {@link DROPPED}.
 */
export const SPOTTER_EFFECTS: readonly LedEffect[] = [
  {
    id: 'spotter.left',
    label: 'Car alongside, left',
    role: 'side',
    side: 'left',
    when: gt(g('SpotterCarLeft'), num(0)),
    color: ds.color.caution.primary,
    source: 'DataCorePlugin.GameData.SpotterCarLeft',
  },
  {
    id: 'spotter.right',
    label: 'Car alongside, right',
    role: 'side',
    side: 'right',
    when: gt(g('SpotterCarRight'), num(0)),
    color: ds.color.caution.primary,
    source: 'DataCorePlugin.GameData.SpotterCarRight',
  },
];

/**
 * The turn indicators, on the side being signalled — the same rule as the spotters, and
 * for the same reason: a light about a side belongs on that side.
 *
 * Green at 2 Hz, which is ISO 2575 and the upper edge of the band UN R48 allows a real indicator.
 * They used to be the spotter's amber at the spotter's rate, so the two conditions that share the
 * side lamp were one light, and rank 2 was indistinguishable from rank 1 on the only lamp where
 * both can be live at once.
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
    role: 'side',
    bestEffort: true,
    side: 'left',
    when: gt(g('TurnIndicatorLeft'), num(0)),
    color: ds.color.good.primary,
    blinkWhen: gt(g('TurnIndicatorLeft'), num(0)),
    blinkDelayMs: SLOW_BLINK_MS,
    source: 'DataCorePlugin.GameData.TurnIndicatorLeft',
  },
  {
    id: 'turn.right',
    label: 'Indicating right',
    role: 'side',
    bestEffort: true,
    side: 'right',
    when: gt(g('TurnIndicatorRight'), num(0)),
    color: ds.color.good.primary,
    blinkWhen: gt(g('TurnIndicatorRight'), num(0)),
    blinkDelayMs: SLOW_BLINK_MS,
    source: 'DataCorePlugin.GameData.TurnIndicatorRight',
  },
];

/** The pit family, which takes the whole strip because it is about where the car is, not what it is doing. */
export const PIT_EFFECTS: readonly LedEffect[] = [
  {
    id: 'pit.lane',
    label: 'In the pit lane',
    role: 'strip',
    when: and(on('IsInPitLane'), not(pitSpeeding())),
    color: ds.purpose.pitLimiter,
    source: 'DataCorePlugin.GameData.IsInPitLane',
  },
  {
    id: 'pit.limiter',
    label: 'Pit limiter on',
    role: 'strip',
    when: on('PitLimiterOn'),
    color: ds.purpose.pitLimiter,
    // Slow: the limiter is a state the driver chose and is holding, not an event. It ran at 186 ms,
    // a rate of its own that was neither of the two and that the eye sorts as whichever it is nearer.
    blinkWhen: on('PitLimiterOn'),
    blinkDelayMs: SLOW_BLINK_MS,
    source: 'DataCorePlugin.GameData.PitLimiterOn',
  },
  {
    id: 'pit.speeding',
    label: 'Speeding in the pit lane',
    role: 'strip',
    // Composed, because SimHub publishes no speeding property: in the lane, a known limit, over it.
    when: pitSpeeding(),
    color: ds.color.danger.primary,
    // Fast: a penalty is accruing while it is lit, which is the one thing on the strip the driver
    // can end by acting this second. It ran at 62 ms, faster than anything else and read as urgency
    // the tier above the one that exists.
    blinkWhen: pitSpeeding(),
    blinkDelayMs: FAST_BLINK_MS,
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
    reason: 'IRacingManager.GD_TCActive() is [NotAvailable] and returns 0, so on iRacing the lamp is dark. It used to read TCLevel as well and was therefore lit there from the green flag onwards, which is a light that reports the dial rather than the intervention.',
  },
  {
    effect: 'Turn indicators',
    property: 'DataCorePlugin.GameData.TurnIndicatorLeft / TurnIndicatorRight',
    reason: 'GD_TurnIndicatorLeft/Right are [NotAvailable] and return 0 — hard zero rather than null, so isnull() cannot tell "off" from "not published".',
  },
];

/**
 * What has a property, and is not drawn all the same.
 *
 * This is neither {@link BEST_EFFORT} nor {@link NO_PROPERTY}: the property exists and some sim
 * fills it, and the row was still not given a lamp. A side has four LEDs on the wheels most people
 * own, so a condition earns one or it does not ship, and the reason it did not is worth more to a
 * later reader than the row would have been.
 */
export const DROPPED: readonly { effect: string; property: string; reason: string }[] = [
  {
    effect: 'ERS charge, and KERS with it',
    property: 'DataCorePlugin.GameData.ERSPercent',
    reason: 'A store that empties is a bar rather than a lamp, and one LED can say neither how much is left nor how fast it is going. The iRacing reader moreover overrides neither GD_ERSMax nor GD_ERSStored, so it would be dark on the only sim OpenDash is tested against. SimHub models no KERS of its own and folds every hybrid store into this one percentage, so KERS goes with it.',
  },
  {
    effect: 'Headlight flash',
    property: 'DataCorePlugin.GameRawData.Telemetry.dcHeadlightFlash',
    reason: "It reports the driver's own momentary button rather than anything about the car or the race, and an LED spent telling the driver what their hand just did is an LED not spent on an aid.",
  },
  {
    effect: 'Car alongside on both sides',
    property: 'DataCorePlugin.GameData.SpotterCarLeft and SpotterCarRight',
    reason: 'It used to blink both side lamps red. Now that a side lamp is one LED, two of them lit already are the both-sides signal, so the blink restated it in a rhythm the car lamp needs for something else.',
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
    nearest: 'The flash-to-pass toggle, GameRawData.Telemetry.dcHeadlightFlash, which exists and which DROPPED explains is not drawn.',
  },
  {
    effect: 'Water pressure',
    reason: 'No StatusDataBase member and no iRacing variable. iRacing publishes oil pressure and water temperature, and no coolant pressure.',
    nearest: "The water temperature warning bit of iRacing's EngineWarnings, which is shipped, and the raw WaterLevel in litres.",
  },
  // The oil temperature warning used to be here, on the claim that EngineWarnings carries no
  // oil-temperature bit. It carries 0x0040 and has since 2021 season 2, so the row was a mistake in
  // this file rather than an absence in iRacing, and the bit is read by the temperature lamp above.
  {
    effect: 'Distance or time to the pit box',
    reason: 'No SimHub property and no iRacing variable; any figure would be an estimate rather than a reading.',
    nearest: 'The pit lane and limiter effects, which say where the car is rather than how far it has to go.',
  },
];

/**
 * Every effect the catalogue ships, in composition order: later shows over earlier.
 *
 * The pit family is last because it is the only thing the canvas lets take the whole strip, and
 * something that takes the whole strip has to be the thing nothing paints over. It used to sit
 * ahead of the flags, so a flag — exclusive itself at the time — blanked the limiter.
 */
export const ALL_EFFECTS = (): LedEffect[] => [...SIDE_EFFECTS, ...TURN_EFFECTS, ...SPOTTER_EFFECTS, ...flagEffects(), ...PIT_EFFECTS];

/**
 * What one lamp of one side draws, highest rank first.
 *
 * The rank inside a role is the catalogue's own order read backwards, because composition order was
 * already the ranking — later shows over earlier — so stating it here is meant to change nothing a
 * driver has already seen. What is new is the rank *between* roles: a shared lamp takes its roles in
 * the order the lamp carries them, which is what puts every car warning ahead of every aid at three
 * lamps a side and ahead of every flag at two.
 */
export const lampConditions = (lamp: Lamp, side: 'left' | 'right'): LedEffect[] => {
  const carried = lamp.carries.flatMap((role) =>
    ALL_EFFECTS()
      .filter((e) => e.role === role && (e.side === undefined || e.side === side) && (lamp.only === undefined || lamp.only.includes(e.id)))
      .reverse(),
  );
  // An effect drawn exactly as something already on this lamp is dropped, highest rank keeping the
  // appearance. One LED drawn the same way by two conditions is one light with two meanings, and the
  // driver reads whichever of them they learned first; the rule used to be a test over the shapes
  // that shipped, and the grid found the pair it could not have -- a side of one carries both the
  // flags and what is beside the car, and the green flag and the left turn indicator are the same
  // green at the same rate. Dropping rather than recolouring, because the colours are the author's
  // (design/tokens.json) and a lamp that cannot say two things apart should say the more important
  // of them.
  // A light that can never come on loses to one that can, whatever their ranks: the green flag and
  // the left turn indicator are the same green at the same rate, and on iRacing the indicator is a
  // hard zero. Rank decides everything else, `carries` being highest first.
  const appearanceOf = (e: LedEffect): string => `${e.color} ${e.blinkWhen ? String(e.blinkDelayMs) : 'steady'}`;
  const ordered = [...carried.filter((e) => !e.bestEffort), ...carried.filter((e) => e.bestEffort)];
  const won = new Map<string, LedEffect>();
  for (const effect of ordered) {
    if (!won.has(appearanceOf(effect))) won.set(appearanceOf(effect), effect);
  }
  return carried.filter((effect) => won.get(appearanceOf(effect)) === effect);
};

/**
 * One effect as a container, over the run of LEDs its role gives it.
 *
 * `above` is what outranks it on the same lamp. The conditions of a lamp are emitted lowest rank
 * first, so SimHub's merge would already leave the highest on top; the explicit `not()` of each
 * higher condition is what makes that readable in a diff rather than a property of the order the
 * children happen to be in. `profile.ts` guards the flag box's layers the same way and for the same
 * reason.
 */
export const effectContainer = (effect: LedEffect, startPosition: number, ledCount: number, above: readonly Expr[] = []): leds.LedContainer => ({
  kind: 'customStatus',
  description: effect.label,
  startPosition,
  ledCount,
  color: effect.color,
  enabledFormula: { expression: above.length === 0 ? effect.when : and(effect.when, ...above.map(not)) },
  ...(effect.blinkWhen ? { blinkFormula: { expression: effect.blinkWhen }, blinkColor: effect.blinkColor ?? BLINK_OFF, blinkDelayMs: effect.blinkDelayMs } : {}),
});

/** `isnull([OpenDash.LedFlagAnimation], true) = true`: whether a flag on a strip moves at all. */
const flagsMove = (): Expr => eq(setting.ledFlagAnimation(), 'true');

/**
 * The colour a flag settles on once it stops moving.
 *
 * `StaticColorContainerBase` alternates `Color` with `BlinkingColor`, and on most rows `Color` is the
 * lit half and {@link BLINK_OFF} the dark one, so holding a row means holding its `Color`. The
 * chequered row is written the other way round deliberately — a dark ground blinking white, which is
 * what stops it being the white flag with a blink — so reading its `Color` would hold a chequered
 * flag dark, and going dark is the one thing this switch promises never to do.
 */
const heldColor = (effect: LedEffect): string => (effect.color === BLINK_OFF ? (effect.blinkColor ?? effect.color) : effect.color);

/**
 * One effect as the containers it needs: one of them, or for a flag the moving one and the held one.
 *
 * `LedFlagAnimation` is a switch on the movement rather than on the flags, which is the whole of the
 * promise: off does not turn a flag off, it holds the flag from the moment it is out on the colour
 * the moving one would have settled on. A driver who finds a blinking rim distracting asks for a rim
 * that stops moving, and they are still owed the flag.
 *
 * Two mutually exclusive containers rather than one whose blink is gated, because a held chequered
 * flag is not the colour a moving one shows steadily; see {@link heldColor}. Both carry the same
 * rank, so what outranks a moving flag outranks a held one.
 */
export const effectContainers = (effect: LedEffect, startPosition: number, ledCount: number, above: readonly Expr[] = []): leds.LedContainer[] => {
  // Flags and nothing else. The race lamp carries the catalogue and carries nothing besides, and the
  // switch is named for a flag; a limiter that stopped blinking would be a state, not a flag held.
  if (effect.role !== 'race' || effect.blinkWhen === undefined) return [effectContainer(effect, startPosition, ledCount, above)];
  const moving: LedEffect = { ...effect, when: and(effect.when, flagsMove()), blinkWhen: and(effect.blinkWhen, flagsMove()) };
  const held: LedEffect = { ...effect, label: `${effect.label}, held`, when: and(effect.when, not(flagsMove())), color: heldColor(effect), blinkWhen: undefined };
  return [effectContainer(moving, startPosition, ledCount, above), effectContainer(held, startPosition, ledCount, above)];
};
