/**
 * The alert catalogue, in priority order, for everything that shows a flag or an alert.
 *
 * One list, because the face, the companion, the pit wall and the flag box rank the same conditions
 * and two lists would eventually disagree about which of two live conditions wins. It is
 * `ALERT_CATALOGUE`, twenty conditions: the fifteen flags iRacing raises and five conditions of the
 * car and of this car's session that it publishes without a flag. Every band ranks all twenty -- the
 * face's band D through `components/flagStrip.ts`, the full-screen block through
 * `components/flagFull.ts`, and the companion and the pit wall through the same two -- and draws
 * each of them wherever it can, which is everywhere but for the two neutral alerts `CarAlert.neutral`
 * describes. The 8x8 box and the LED strips draw `FLAG_CATALOGUE`, which is the flag half of the same
 * list in the same order rather than a second list. Whether the lights ever draw the other five is a question for
 * them; a picture of an incident count on sixty-four pixels is not an obvious one.
 *
 * **What is here is what iRacing publishes.** A drawn alert that never fires is worse than an
 * absent one, because nobody finds out until a race, so a condition iRacing does not publish is
 * not in this list and is written down in docs/design/flag-box.md with the reason.
 *
 * The flags' detection comes from `SessionFlagsDetails`, SimHub's per-bit explosion of iRacing's
 * `SessionFlags` (see docs/research/simhub-dash-format.md). The six normalised `Flag_*` properties
 * are a lossy summary of it: `Flag_Yellow` is `yellow`, `yellowWaving`, `caution` and
 * `cautionWaving` folded together, and `Flag_Black` is only the `black` bit, so a furled black and
 * a disqualification are invisible through them. The other five each carry their own condition, and
 * each of those reads `inTheCar` from `second/values.ts`, #312's one test of whether anybody is
 * driving: SimHub reads iRacing's ignition as off whenever there is no voltage, which is whenever the
 * driver is in the garage, and an alert raised there is noise. They read it as "no" in a sim that
 * does not say, so the car alerts are iRacing's in the way the flags are.
 *
 * The catalogue carries no duration of its own, and *whether* a condition shows is exactly as long
 * as it holds: the canvas asks for a configurable three seconds per alert, and a flag that went dark
 * on a clock while it was still flying would be a lie. What has a duration is *how* band D shows it
 * -- the whole band for a few seconds and its corner blocks afterwards, #380 -- and the two alerts
 * that are events rather than states, an incident and a flash of the headlights, which hold for
 * those same seconds after the value they watch moves. Every one of those clocks is SimHub's
 * `changed()` window rather than one of ours, which is what ADR 0009 admits. `raisedRank` below is
 * the value the band's window watches. The difference between the canvas's per-alert duration and
 * the bits is recorded in docs/design/flag-box.md.
 */
import { ncalc, type Hex } from './generator.ts';
import type { Expr } from './bind.ts';
import { ds } from './tokens.ts';
import { engineStopped, hasIncidentLimit, ignitionOff, incidentLimit, incidents, inTheCar, isInPitLane } from './second/values.ts';

const { and, changed, concat, eq, fmt, game, gt, iff, isNull, isnull, not, num, or, prop, raw, str } = ncalc;

/** A member of `iRacingSDK.SessionFlags`, as SimHub spells it. Camel case: the enum's own. */
export type SessionFlagBit =
  | 'checkered'
  | 'white'
  | 'green'
  | 'yellow'
  | 'red'
  | 'blue'
  | 'debris'
  | 'crossed'
  | 'yellowWaving'
  | 'oneLapToGreen'
  | 'greenHeld'
  | 'tenToGo'
  | 'fiveToGo'
  | 'randomWaving'
  | 'caution'
  | 'cautionWaving'
  | 'black'
  | 'disqualify'
  | 'servicible'
  | 'furled'
  | 'repair'
  | 'startHidden'
  | 'startReady'
  | 'startSet'
  | 'startGo';

/**
 * `[DataCorePlugin.GameRawData.Telemetry.SessionFlagsDetails.Is<bit>]`. One boolean per bit, so
 * no bitwise operator is needed — which matters, because whether SimHub's NCalc exposes one is
 * not established.
 */
export const flagBit = (bit: SessionFlagBit): Expr => prop(`DataCorePlugin.GameRawData.Telemetry.SessionFlagsDetails.Is${bit}`);

/** How a surface reads one bit. The two are {@link bitSet} and {@link safeBitSet}. */
export type BitTest = (bit: SessionFlagBit) => Expr;

/** True when the bit is set. SimHub hands a boolean through as 1 or 0. */
export const bitSet: BitTest = (bit) => eq(flagBit(bit), num(1));

/**
 * The same read, null-safe, which is what a strip needs and a matrix does not.
 *
 * `CustomStatusContainer.IsActiveBase` catches a throwing expression and returns its default of
 * 1.0, so on a car or a sim that publishes no `SessionFlagsDetails` a bare read does not leave the
 * lamp dark: it leaves every flag *on*, and the highest-ranked one wins. The box's `when` group
 * tolerates the same throw and shows nothing, which is why one catalogue can be read two ways and
 * why the difference is a parameter here rather than a second copy of the ranking.
 */
export const safeBitSet: BitTest = (bit) => eq(isnull(flagBit(bit), num(0)), num(1));

/**
 * The six SimHub flag properties, which are what the round face's ring and the pit wall header
 * still read. They are a lossy summary, as the header of this file says, and band D no longer
 * reads them: `components/flagStrip.ts` draws the whole catalogue off the bits instead.
 */
export type FaceFlag = 'Flag_Black' | 'Flag_Checkered' | 'Flag_Yellow' | 'Flag_Blue' | 'Flag_White' | 'Flag_Green';

/**
 * How a band draws the condition: three shapes and no fourth, which is the canvas's own rule for
 * the alert catalogue. A filled bar carries its label in `purpose.flag.onFlag`; an outlined bar is
 * an opaque `surface.base` ground with a 3 px border and its label both in the alert's colour,
 * which is how a near-black flag is drawn on a near-black face; the chequer is the board, and it
 * is the one condition with no name to write on it.
 *
 * It lives beside the condition rather than in the component for the reason `motion` does: a
 * condition that reaches the catalogue without a shape, a colour and a name is a condition the face
 * cannot draw, and the type is what refuses it.
 *
 * A filled bar may carry a `short` form of its label, which is the name where the label does not fit.
 * Every surface that writes the name writes the longest of the two that fits the room it has, so the
 * name is as long as the room allows rather than as short as the smallest room. One condition has
 * one, the full course yellow, whose FULL COURSE YELLOW is FCY on a block too narrow for it, #497; the
 * others are short enough everywhere they are written, and a second form nobody reads would be a
 * second name to keep in step with the first.
 */
export type AlertBandSpec =
  | { shape: 'filled'; colour: Hex; label: string; short?: string; flash?: true; run?: BandRun }
  | { shape: 'outlined'; colour: Hex; label: string; run?: BandRun }
  | { shape: 'chequer' };

/** The names a band can write for the condition, longest first, and none for the chequer. */
export const bandNames = (spec: AlertBandSpec): readonly string[] => {
  if (spec.shape === 'chequer') return [];
  return spec.shape === 'filled' && spec.short !== undefined ? [spec.label, spec.short] : [spec.label];
};

/**
 * What the whole band writes in place of its label, for the one condition with a number to say: the
 * incident, with its count and the limit it is counted against.
 *
 * Only the band writes it, and only while the condition has the whole band. A corner block writes the
 * label, and the nano writes nothing, which is the rule the blue flag's detail already keeps: the
 * detail belongs to the seconds a condition has the band. `sample` is what DashStudio draws and
 * `widest` is the longest string the binding can produce, which is what `textFit.test.ts` measures
 * the band against and what `widest.test.ts` holds the binding to.
 */
export interface BandRun {
  sample: string;
  bind: Expr;
  widest: string;
}

/** What every entry of the catalogue carries, a flag or not. */
interface CatalogueEntry {
  /** Stable id: the container description in the profile and the key in the docs table. */
  id: string;
  /** As the pit wall names it. */
  name: string;
  /** How band D draws it. Every condition has one, so that the band and the box cannot differ. */
  band: AlertBandSpec;
}

export interface FlagCondition extends CatalogueEntry {
  /**
   * Whether the box shows it when the driver has asked for critical flags only: a flag addressed
   * to this car, or one that means slow down. The chequer, the white, the green and the start
   * gantry are news rather than instructions, so they are not critical.
   */
  critical: boolean;
  /**
   * Whether the picture moves or stays still. Movement means act: a flag that ends or interrupts
   * the race moves, a flag that informs is held, and that one rule is what lets the box be learned
   * away from the car. It lives with the condition rather than with the drawing because it is a
   * fact about the flag; `flagBox.test.ts` holds the drawings to it, so a picture cannot quietly
   * gain or lose the frames that carry its meaning.
   */
  motion: 'moves' | 'held';
  /** The iRacing bits that raise it. Any one of them is enough. */
  bits: readonly SessionFlagBit[];
  /**
   * The normalised property a band reads *instead of* the bits, for a condition whose bit is held
   * far longer than the thing it announces.
   *
   * Only the green flag has one. iRacing sets `green` for the whole green-flag stint, whereas the
   * green flag is an event the canvas gives three seconds; SimHub passes `Flag_Green` through a
   * `GreenLimiter` and reports it only shortly after the flag is raised, which is the only clock
   * OpenDash has. A band covers a page the driver is reading, so it takes the limited reading; the
   * 8x8 box, whose green costs nothing while it is lit, keeps the bit. Without this, band D would
   * be a solid green bar over the fuel page for an entire race.
   */
  limiter?: FaceFlag;
  /** The SimHub property that answers for this condition on the ring and the pit wall header. */
  faceFlag?: FaceFlag;
}

/**
 * A condition of the car, or of this car's part in the session, that iRacing publishes without a
 * flag. The canvas ranks it in the same list as the flags and draws it in the same shapes, so every
 * band ranks it and draws it the way it draws a flag, the two neutral ones apart; the lights do not,
 * and `FLAG_CATALOGUE` leaves it out.
 */
export interface CarAlert extends CatalogueEntry {
  /**
   * When it is raised, null-safe, so that a sim publishing nothing raises nothing and every surface
   * can rank it with one reading. A flag is read differently by the box and by a band, which is what
   * `RaisedTest` is for; an alert has this reading and no other.
   */
  when: Expr;
  /**
   * What the driver's own hand did rather than anything the car or the race is saying: push to pass
   * and the headlight flash, which the canvas dresses in `purpose.alert.p2p`.
   *
   * That token is `color.neutral.primary`, which is white, and white is already two flags: the white
   * flag is filled with it, and the black family is outlined in the `#F5F7FA` beside it. On a band
   * that writes a name the name tells them apart. Where no name is written -- the nano's strip, and a
   * corner block too narrow for the word -- a neutral alert draws nothing rather than a colour that
   * says "last lap" or "black flag". Nor does it take the full-screen block, which is the flag that
   * cannot be missed and has no business covering the gear for the length of a push to pass.
   */
  neutral?: true;
}

/** A flag or a car alert: an entry of `ALERT_CATALOGUE`. */
export type AlertCondition = FlagCondition | CarAlert;

/** The flags are the entries iRacing raises through `SessionFlags`, which are the ones with bits. */
export const isFlag = (condition: AlertCondition): condition is FlagCondition => 'bits' in condition;

/**
 * How long an event holds once the value it watches moves: `indicator.alert.durationMs`, the canvas's
 * three seconds per alert, which is also how long a flag keeps the whole band.
 */
export const ALERT_EVENT_MS = ds.indicator.alert.durationMs;

/**
 * Somebody is in the car, as iRacing says it, and no if the sim says nothing: every car alert's
 * first condition. `inTheCar` explains why this caller answers "no" where the change notification
 * answers "yes".
 */
const driving = (): Expr => inTheCar(false);

/**
 * Out on the circuit and in the car, which is where the two power alerts belong.
 *
 * In the lane the pit family says the same thing in the limiter's own rectangle
 * (`components/pitAlerts.ts`), so these leave the lane to it: one condition drawn twice on one face
 * would be two answers to one question. Out of the lane nothing drew either of them, and a car
 * stalled on the grass after a spin is exactly where a driver has to be told to press the starter.
 */
const outOnCircuit = (condition: Expr): Expr => and(driving(), not(isInPitLane()), condition);

/**
 * The ignition is off, out on the circuit. It sits above the stalled engine for the reason the pit
 * family gives: it is the switch the driver can move, and where iRacing raises both it is the one that
 * names what to do about it.
 *
 * The canvas numbers the pair the other way, 1 EngineOff and 2 IgnitionOff. The pit family's order is
 * kept, so that a face does not answer the same two conditions in two orders depending on which side
 * of the pit entry line the car is.
 */
const IGNITION_OFF: CarAlert = {
  id: 'ignition',
  name: 'Ignition off',
  when: outOnCircuit(ignitionOff()),
  band: { shape: 'outlined', colour: ds.purpose.alert.power, label: 'IGNITION OFF' },
};

/**
 * The engine has stopped with the ignition on, out on the circuit: press the starter. SimHub's
 * `EngineStarted` rather than iRacing's stalled bit, for the hybrids `engineStopped` describes.
 */
const ENGINE_OFF: CarAlert = {
  id: 'engine',
  name: 'Engine off',
  when: outOnCircuit(engineStopped()),
  band: { shape: 'outlined', colour: ds.purpose.alert.power, label: 'ENGINE OFF' },
};

/** The incidents this driver has taken, as a number whatever the sim publishes. */
const incidentCount = (): Expr => isnull(incidents(), num(0));

/**
 * An incident has just been taken.
 *
 * It is an event rather than a state, which no flag is: the count only grows, and an incident is the
 * moment it grows. `changed()` is SimHub's window over it, open for three seconds after the count
 * moves, and nothing here keeps the count from before. A count falling back to zero, which is what a
 * new session does, raises nothing, since the window is only answered with the count above zero.
 *
 * **The window is asked first, and the order is load-bearing.** SimHub's NCalc evaluates `and` from
 * the left and stops at the first false (`EvaluationVisitor` in the NCalc.dll SimHub 9.12.6 ships),
 * and `changed()` records a value the first time it is asked and answers false. Asked after "the
 * count is above zero", the window would be asked for the first time on the frame of the first
 * incident, record it, and say nothing: every session's first incident would go untold.
 *
 * The same laziness decides what happens under a higher condition. A band ranks with `if()` and
 * `and`, so while a yellow has the band nothing asks this window at all; when the yellow clears it is
 * asked, finds the count moved, and opens then. An incident taken under a flag is told when the flag
 * has gone rather than lost behind it, which is the order the ranking asks for anyway. The one that
 * is lost is an incident taken before the window has been asked at all, which is a dashboard started
 * under a caution, or with the flag format off, that sees its first incident before anything clears.
 *
 * What a driver is shown is the running count, which is what the canvas's "Incident · 4x" draws,
 * rather than the size of the incident just taken: that would need the count from before the window,
 * and nothing on a dashboard remembers it. The limit follows where the session has one, which is the
 * pit wall header's reading of the same two properties.
 *
 * Outlined, where the canvas fills it. Its colour is `purpose.alert.incident`, which is the caution
 * amber `#FFB300`, and so is the meatball's `purpose.flag.orange`: filled, the two would be one band
 * on the nano, and a driver who has just hit something is the driver a meatball is most likely to be
 * for. Outlined in amber is a drawing nothing else in the catalogue makes.
 */
const INCIDENT: CarAlert = {
  id: 'incident',
  name: 'Incident',
  when: and(changed(num(ALERT_EVENT_MS), incidentCount()), driving(), gt(incidentCount(), num(0))),
  band: {
    shape: 'outlined',
    colour: ds.purpose.alert.incident,
    label: 'INCIDENT',
    run: {
      sample: 'INCIDENT · 4x / 17',
      bind: concat(str('INCIDENT · '), fmt(incidentCount(), '0'), str('x'), iff(hasIncidentLimit(), concat(str(' / '), incidentLimit()), str(''))),
      widest: 'INCIDENT · 999x / 999',
    },
  },
};

/**
 * Push to pass is in use: `GameData.PushToPassActive`, which SimHub's iRacing reader fills from
 * `CarIdxP2P_Status` at the player's index.
 *
 * The canvas writes "Push to pass · 3 left", and the count is published, `PlayerP2P_Count` from
 * `CarIdxP2P_Count`, but it is not written here. iRacing describes it as "count of usage (or
 * remaining in Race)", so one number means two things by session type, and no recording of a car
 * that has push to pass exists to say which one "3 left" would be showing. The name is certain; the
 * number waits for that recording.
 */
const PUSH_TO_PASS: CarAlert = {
  id: 'pushToPass',
  name: 'Push to pass',
  neutral: true,
  when: and(driving(), eq(isnull(game('PushToPassActive'), num(0)), num(1))),
  band: { shape: 'filled', colour: ds.purpose.alert.p2p, label: 'PUSH TO PASS' },
};

/**
 * The driver has flashed the headlights: `dcHeadlightFlash`, the in-car control itself, absent on a
 * car without one.
 *
 * It is the driver's own button, so it is an event in the incident's sense and is read the same way:
 * `changed()` asked first, so the window opens when the control moves either way and holds for three
 * seconds after it last did. That serves both of the readings the control has been given -- a
 * momentary button, which is what docs/research/lights-review.md takes it for, and a toggle, which is
 * what iRacing's own description calls it -- since either moves the value when the driver flashes.
 *
 * Unlike an incident, a flash under anything above it is not told later, on the reading the research
 * gives it: a momentary button is back where it was by the time the band is free, so the window,
 * asked then, finds nothing moved, which is right, since a flash from ten seconds ago is not news. Were
 * it a toggle, an odd number of flashes under a blue flag would be told when the blue cleared. Which it
 * is wants a recording from a car that has the control.
 *
 * The LED strips leave it out (`DROPPED` in `leds/effects.ts`): a lamp spent on what the driver's hand
 * just did is a lamp not spent on an aid. The band is not a lamp. This is the last condition of the
 * list, so it takes the band only when nothing else wants it, and it is what the canvas asks for.
 */
const HEADLIGHT_FLASH: CarAlert = {
  id: 'headlightFlash',
  name: 'Headlight flash',
  neutral: true,
  when: and(changed(num(ALERT_EVENT_MS), raw('dcHeadlightFlash')), driving(), not(isNull(raw('dcHeadlightFlash')))),
  band: { shape: 'outlined', colour: ds.purpose.alert.p2p, label: 'FLASH' },
};

/**
 * Highest priority first, in the numbering the canvas's alert catalogue gives, whose entries
 * OpenDash can raise are 1 EngineOff, 2 IgnitionOff, 3 RedFlag, 4 Disqualified, 5 BlackFurled,
 * 6 BlackFlag, 7 SafetyCar, 11 YellowFlag, 15 Incident, 16 WhiteLastLap, 18 Meatball, 19 Debris,
 * 20 BlueFlag, 21 GreenFlag, 22 GreenSet/GreenReady, 23 ChequeredFlag, 24 PushToPass and
 * 25 HeadlightFlash. The waved yellow sits where the canvas puts its 9 DoubleYellow and
 * 10 YellowSector, which iRacing does not publish, and the five entries the canvas numbers and
 * iRacing does not raise at all -- 8, 12, 13, 14 and 17 -- are in docs/design/flag-box.md with why.
 *
 * **One rule governs where the code departs from that numbering among the flags: no condition the
 * critical-flags switch can silence outranks one it cannot.** `conditionShown` silences the
 * non-critical half of the list, so a non-critical condition ranked above a critical one would mean
 * that *turning the switch off* hides a flag, which is the opposite of what the switch says. Two
 * entries move for it:
 *
 * - The chequer, numbered 23, was second of the list and hid a yellow thrown at a race finishing
 *   under one. It is last of the flags now, which is both the canvas's rank and the rule.
 * - The white, numbered 16, sits below the blue at 20 rather than above it: the white is news and
 *   the blue is addressed to this car.
 *
 * The meatball also keeps a rank the canvas does not give it, 18 there and seventh here, and that one
 * is not the rule but the same reading as the shape below: a flag calling this car in outranks a
 * condition of the track. Besides, the LED strip draws it on the black family's lamp, since its
 * orange aliases the caution amber, and it could not be ranked between the two yellows without a
 * second lamp to put it on. Both divergences are recorded in docs/design/flag-box.md.
 *
 * The car alerts are placed by the canvas's numbers against the flags either side of them, and two
 * of them move. The ignition and the stalled engine swap, for the reason `IGNITION_OFF` gives. The
 * incident, 15, keeps the slow-down flags above it and the blue and the white below, as the canvas
 * has it, and lands under the debris flag rather than over it because the code's debris flag sits
 * with the other flags that mean slow down rather than at the canvas's 19.
 *
 * The shape of the list is: the car cannot move, then the session is stopped, then anything
 * addressed to this car, then slow down, then what this car has just been given, then yield, then
 * the lap, then the start gantry, then the race is over, and last what the driver's own hand did.
 */
export const ALERT_CATALOGUE: readonly AlertCondition[] = [
  IGNITION_OFF,
  ENGINE_OFF,
  { id: 'red', name: 'Red', critical: true, motion: 'moves', bits: ['red'], band: { shape: 'filled', colour: ds.purpose.flag.red, label: 'RED FLAG' } },
  { id: 'disqualify', name: 'Disqualified', critical: true, motion: 'moves', bits: ['disqualify'], band: { shape: 'outlined', colour: ds.purpose.flag.black, label: 'DISQUALIFIED' } },
  { id: 'furled', name: 'Black furled', critical: true, motion: 'moves', bits: ['furled'], band: { shape: 'outlined', colour: ds.purpose.flag.black, label: 'BLACK FLAG · FURLED' } },
  { id: 'black', name: 'Black', critical: true, motion: 'moves', bits: ['black'], band: { shape: 'outlined', colour: ds.purpose.flag.black, label: 'BLACK FLAG' }, faceFlag: 'Flag_Black' },
  { id: 'meatball', name: 'Meatball', critical: true, motion: 'moves', bits: ['repair'], band: { shape: 'filled', colour: ds.purpose.flag.orange, label: 'MEATBALL' } },
  // The full course yellow: iRacing's caution of the whole track, which is the pace car being
  // deployed. It outranks a local yellow because it is the whole track. The canvas names it Safety
  // car (7 · SafetyCar), and the name here is the one decided in #497, which docs/design/flag-box.md
  // records against the canvas. It is the one name with a short form, since FULL COURSE YELLOW is
  // too long for the full-screen block on most faces at the size the other names set.
  {
    id: 'caution',
    name: 'Full course yellow',
    critical: true,
    motion: 'moves',
    bits: ['caution', 'cautionWaving'],
    band: { shape: 'filled', colour: ds.purpose.alert.safetyCar, label: 'FULL COURSE YELLOW', short: 'FCY' },
  },
  // The flash is the waved yellow's and not the standing yellow's, which is the rule the box keeps
  // under "waving is blinking". Band D used to flash on SimHub's `Flag_Yellow`, which folds the two
  // together, so a standing yellow strobed for as long as it was out.
  //
  // The flash is also all that tells the two apart, since both are named YELLOW FLAG, #497. A yellow
  // being waved is the yellow flag, the canvas names no other, and a driver reads the difference in
  // the band's movement rather than in a word.
  {
    id: 'yellowWaving',
    name: 'Yellow',
    critical: true,
    motion: 'moves',
    bits: ['yellowWaving'],
    band: { shape: 'filled', colour: ds.purpose.flag.yellow, label: 'YELLOW FLAG', flash: true },
  },
  { id: 'yellow', name: 'Yellow', critical: true, motion: 'held', bits: ['yellow'], band: { shape: 'filled', colour: ds.purpose.flag.yellow, label: 'YELLOW FLAG' }, faceFlag: 'Flag_Yellow' },
  // `purpose.flag.debris` is the yellow, and the canvas draws the band as that yellow under danger
  // stripes. The stripes are a fourth shape and are not drawn: the name carries the difference on
  // the standard band, and on the nano, which writes no name, a debris flag reads as a yellow.
  { id: 'debris', name: 'Debris', critical: true, motion: 'moves', bits: ['debris'], band: { shape: 'filled', colour: ds.purpose.flag.debris, label: 'DEBRIS' } },
  INCIDENT,
  { id: 'blue', name: 'Blue', critical: true, motion: 'held', bits: ['blue'], band: { shape: 'filled', colour: ds.purpose.flag.blue, label: 'BLUE FLAG' }, faceFlag: 'Flag_Blue' },
  // In iRacing the white bit is the last lap and nothing else, which is why the name says so.
  { id: 'white', name: 'White', critical: false, motion: 'held', bits: ['white'], band: { shape: 'filled', colour: ds.purpose.flag.white, label: 'WHITE · LAST LAP' }, faceFlag: 'Flag_White' },
  {
    id: 'green',
    name: 'Green',
    critical: false,
    motion: 'held',
    bits: ['green'],
    band: { shape: 'filled', colour: ds.purpose.flag.green, label: 'GREEN FLAG' },
    limiter: 'Flag_Green',
    faceFlag: 'Flag_Green',
  },
  { id: 'startSet', name: 'Set', critical: false, motion: 'held', bits: ['startSet'], band: { shape: 'outlined', colour: ds.purpose.flag.green, label: 'GREEN · SET' } },
  { id: 'startReady', name: 'Ready', critical: false, motion: 'held', bits: ['startReady'], band: { shape: 'outlined', colour: ds.purpose.flag.green, label: 'GREEN · READY' } },
  { id: 'chequered', name: 'Chequered', critical: false, motion: 'moves', bits: ['checkered'], band: { shape: 'chequer' }, faceFlag: 'Flag_Checkered' },
  PUSH_TO_PASS,
  HEADLIGHT_FLASH,
];

/**
 * The flags of the catalogue, in its order: what the 8x8 box, the LED strips and the round face's ring
 * rank, none of which draws a car alert. A filter over the one list rather than a second list, so the
 * two cannot disagree about which of two live flags wins.
 */
export const FLAG_CATALOGUE: readonly FlagCondition[] = ALERT_CATALOGUE.filter(isFlag);

/** The face's flag properties, in this catalogue's order. */
export const FACE_FLAG_PRIORITY: readonly FaceFlag[] = FLAG_CATALOGUE.map((c) => c.faceFlag).filter((f): f is FaceFlag => f !== undefined);

export const flagCondition = (id: string): FlagCondition => {
  const found = FLAG_CATALOGUE.find((c) => c.id === id);
  if (found === undefined) throw new RangeError(`no flag condition ${JSON.stringify(id)}`);
  return found;
};

/** Any entry of the catalogue by id, a flag or a car alert. */
export const alertCondition = (id: string): AlertCondition => {
  const found = ALERT_CATALOGUE.find((c) => c.id === id);
  if (found === undefined) throw new RangeError(`no alert condition ${JSON.stringify(id)}`);
  return found;
};

/** Any of the condition's bits is set. */
export const conditionRaised = (condition: FlagCondition, test: BitTest = bitSet): Expr => or(...condition.bits.map(test));

/**
 * How a surface reads a whole condition, which is the seam the ranking is parameterised over. The
 * box reads the bits; band D reads them null-safely and honours `limiter`, and it has to be the
 * same reading in the layer's own term and in the negated terms of every layer below it, or two
 * conditions could draw at once. A car alert has one reading, its own `when`, whichever surface
 * asks.
 */
export type RaisedTest = (condition: AlertCondition) => Expr;

/** A reading of the flags, carried over the whole catalogue: a car alert is read by its `when`. */
const readingFlags =
  (flag: (condition: FlagCondition) => Expr): RaisedTest =>
  (condition) =>
    isFlag(condition) ? flag(condition) : condition.when;

/** The box's reading, and the default: the bits, as they are. */
export const boxRaised: RaisedTest = readingFlags((condition) => conditionRaised(condition));

/**
 * Band D's reading: null-safe, because a bare read of an absent property is not a boolean and the
 * band has to stay dark on a sim that publishes no `SessionFlagsDetails` rather than light up.
 */
export const bandRaised: RaisedTest = readingFlags((condition) =>
  condition.limiter ? eq(isnull(prop(`DataCorePlugin.GameData.${condition.limiter}`), num(0)), num(1)) : conditionRaised(condition, safeBitSet),
);

/**
 * Whether a surface has the "critical flags only" switch, as an expression, or `false` for a
 * surface that has none and shows the whole list. Band D is the second kind: the box's switch is
 * `OpenDash.FlagBoxCriticalOnly` and belongs to the box, whereas a driver who wants band D quieter
 * has the flag format setting instead. Writing that as `false` rather than as a literal expression
 * keeps the term out of every layer's `Visible` rather than emitting `!(false)` twenty times.
 */
export type CriticalOnly = Expr | false;

/**
 * The condition is raised *and the driver has asked to see it*.
 *
 * "Critical flags only" is a guard on the non-critical conditions rather than a second copy of the
 * catalogue. Carrying the whole list twice, once per branch of the switch, reads better in the
 * file and costs twice the glyphs — and the tree is already written once per matrix, so the
 * doubling is eightfold by the time it reaches disk. This is the version that fits.
 *
 * The switch is a flag switch and silences flags. A car alert is never behind it, which is moot
 * today, since the one surface with the switch is the box and the box draws no car alert.
 */
export function conditionShown(condition: AlertCondition, criticalOnly: CriticalOnly, read: RaisedTest = boxRaised): Expr {
  const raised = read(condition);
  return !isFlag(condition) || condition.critical || criticalOnly === false ? raised : and(not(criticalOnly), raised);
}

/**
 * The condition is shown and nothing above it in the catalogue is: one condition at a time, ranked
 * the same way everywhere. A flag the switch has silenced stops outranking the ones below it, so the
 * box shows the next one down rather than going dark.
 */
export function conditionVisible(condition: AlertCondition, criticalOnly: CriticalOnly, only: readonly AlertCondition[] = FLAG_CATALOGUE, read: RaisedTest = boxRaised): Expr {
  const index = only.indexOf(condition);
  if (index < 0) throw new RangeError(`${condition.id} is not in the list it is being ranked within`);
  const higher = only.slice(0, index).map((c) => not(conditionShown(c, criticalOnly, read)));
  return and(...higher, conditionShown(condition, criticalOnly, read));
}

/**
 * How every band ranks a condition: the whole catalogue, read null-safely, with no critical switch.
 *
 * Band D's takeover, its settled corner blocks and the full-screen block are three drawings of one
 * reading, on the face, the companion and the pit wall alike, so it is written once here rather than
 * as the same four arguments in each of them.
 */
export const bandVisible = (condition: AlertCondition): Expr => conditionVisible(condition, false, ALERT_CATALOGUE, bandRaised);

/**
 * Which condition a surface is showing, as a number: its place in the list, counting from one, and
 * 0 when nothing is raised.
 *
 * It exists so that "the flag has just changed" can be asked once instead of twenty times, and so
 * that it is asked of the right thing. A layer's own `conditionVisible` answers whether *this*
 * condition is winning; a clock needs a value that moves when the *winner* moves, and the two are
 * not the same question. A full course yellow clearing to the local yellow underneath it never
 * moves the yellow's own bit, yet what the driver is being told has changed, so the yellow is owed
 * its moment on the whole band exactly as a fresh flag is.
 *
 * Ranked from the same list in the same order as `conditionVisible`, and read through the same
 * `RaisedTest`, so the number and the layer drawing it cannot disagree about which condition is out.
 * `if()` evaluates only the branch it takes, so the cost is the conditions down to the winner rather
 * than all of them. The list is the whole catalogue by default, since the band is the one surface
 * with a clock to drive.
 */
export const raisedRank = (read: RaisedTest = bandRaised, only: readonly AlertCondition[] = ALERT_CATALOGUE): Expr =>
  only.reduceRight<Expr>((below, condition, index) => iff(read(condition), num(index + 1), below), num(0));

/** Nothing in the list is being shown, which is what everything below the box's flags needs. */
export const noFlagShown = (criticalOnly: CriticalOnly, only: readonly AlertCondition[] = FLAG_CATALOGUE, read: RaisedTest = boxRaised): Expr =>
  and(...only.map((c) => not(conditionShown(c, criticalOnly, read))));

/** The flags, or only the critical part of them. */
export const flagsShown = (criticalOnly: boolean): readonly FlagCondition[] =>
  criticalOnly ? FLAG_CATALOGUE.filter((c) => c.critical) : FLAG_CATALOGUE;
