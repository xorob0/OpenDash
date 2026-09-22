/**
 * The flag catalogue, in priority order, for everything that shows a flag.
 *
 * One list, because the face, the pit wall and the flag box rank the same conditions and two
 * lists would eventually disagree about which of two live flags wins. All three surfaces draw all
 * fifteen: the 8x8 box as pictures, the face's band D through `components/flagStrip.ts`, and the
 * pit wall header as a name and a colour. #109 folds the rest of the alert catalogue in here;
 * this is the flag half of it.
 *
 * **What is here is what iRacing publishes.** A drawn alert that never fires is worse than an
 * absent one, because nobody finds out until a race, so a condition iRacing does not publish is
 * not in this list and is written down in docs/design/flag-box.md with the reason.
 *
 * The detection comes from `SessionFlagsDetails`, SimHub's per-bit explosion of iRacing's
 * `SessionFlags` (see docs/research/simhub-dash-format.md). The six normalised `Flag_*` properties
 * are a lossy summary of it: `Flag_Yellow` is `yellow`, `yellowWaving`, `caution` and
 * `cautionWaving` folded together, and `Flag_Black` is only the `black` bit, so a furled black and
 * a disqualification are invisible through them.
 *
 * The catalogue carries no duration. The canvas asks for a configurable three seconds per alert
 * and `design/tokens.json` states it as `indicator.alert.durationMs`, but a duration needs a clock
 * and neither NCalc nor the plugin has one under ADR 0009: a condition here shows for exactly as
 * long as its bits are set. That difference is recorded in docs/design/flag-box.md.
 */
import { ncalc, type Hex } from './generator.ts';
import type { Expr } from './bind.ts';
import { ds } from './tokens.ts';

const { and, eq, isnull, not, num, or, prop } = ncalc;

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
 */
export type AlertBandSpec =
  | { shape: 'filled'; colour: Hex; label: string; flash?: true }
  | { shape: 'outlined'; colour: Hex; label: string }
  | { shape: 'chequer' };

export interface FlagCondition {
  /** Stable id: the container description in the profile and the key in the docs table. */
  id: string;
  /** As the pit wall names it. */
  name: string;
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
  /** How band D draws it. Every condition has one, so that the band and the box cannot differ. */
  band: AlertBandSpec;
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
 * Highest priority first, in the numbering the canvas's alert catalogue gives, whose entries
 * OpenDash can raise are 3 RedFlag, 4 Disqualified, 5 BlackFurled, 6 BlackFlag, 7 SafetyCar,
 * 11 YellowFlag, 16 WhiteLastLap, 18 Meatball, 19 Debris, 20 BlueFlag, 21 GreenFlag,
 * 22 GreenSet/GreenReady and 23 ChequeredFlag. The waved yellow sits where the canvas puts its
 * 9 DoubleYellow and 10 YellowSector, which iRacing does not publish.
 *
 * **One rule governs where the code departs from that numbering: no condition the critical-flags
 * switch can silence outranks one it cannot.** `conditionShown` silences the non-critical half of
 * the list, so a non-critical condition ranked above a critical one would mean that *turning the
 * switch off* hides a flag, which is the opposite of what the switch says. Two entries move for it:
 *
 * - The chequer, numbered 23, was second of the list and hid a yellow thrown at a race finishing
 *   under one. It is last now, which is both the canvas's rank and the rule.
 * - The white, numbered 16, sits below the blue at 20 rather than above it: the white is news and
 *   the blue is addressed to this car.
 *
 * The meatball also keeps a rank the canvas does not give it, 18 there and fifth here, and that one
 * is not the rule but the same reading as the shape below: a flag calling this car in outranks a
 * condition of the track. Besides, the LED strip draws it on the black family's lamp, since its
 * orange aliases the caution amber, and it could not be ranked between the two yellows without a
 * second lamp to put it on. Both divergences are recorded in docs/design/flag-box.md.
 *
 * The shape of the list is: the session is stopped, then anything addressed to this car, then slow
 * down, then yield, then the lap, then the start gantry, then the race is over.
 */
export const FLAG_CATALOGUE: readonly FlagCondition[] = [
  { id: 'red', name: 'Red', critical: true, motion: 'moves', bits: ['red'], band: { shape: 'filled', colour: ds.purpose.flag.red, label: 'RED FLAG' } },
  { id: 'disqualify', name: 'Disqualified', critical: true, motion: 'moves', bits: ['disqualify'], band: { shape: 'outlined', colour: ds.purpose.flag.black, label: 'DISQUALIFIED' } },
  { id: 'furled', name: 'Black furled', critical: true, motion: 'moves', bits: ['furled'], band: { shape: 'outlined', colour: ds.purpose.flag.black, label: 'BLACK FLAG · FURLED' } },
  { id: 'black', name: 'Black', critical: true, motion: 'moves', bits: ['black'], band: { shape: 'outlined', colour: ds.purpose.flag.black, label: 'BLACK FLAG' }, faceFlag: 'Flag_Black' },
  { id: 'meatball', name: 'Meatball', critical: true, motion: 'moves', bits: ['repair'], band: { shape: 'filled', colour: ds.purpose.flag.orange, label: 'MEATBALL' } },
  // Full-course caution: in iRacing this is the pace car being deployed, which is the closest
  // honest reading of a safety car. It outranks a local yellow because it is the whole track.
  {
    id: 'caution',
    name: 'Full-course caution',
    critical: true,
    motion: 'moves',
    bits: ['caution', 'cautionWaving'],
    band: { shape: 'filled', colour: ds.purpose.alert.safetyCar, label: 'SAFETY CAR' },
  },
  // The flash is the waved yellow's and not the standing yellow's, which is the rule the box keeps
  // under "waving is blinking". Band D used to flash on SimHub's `Flag_Yellow`, which folds the two
  // together, so a standing yellow strobed for as long as it was out.
  {
    id: 'yellowWaving',
    name: 'Waved yellow',
    critical: true,
    motion: 'moves',
    bits: ['yellowWaving'],
    band: { shape: 'filled', colour: ds.purpose.flag.yellow, label: 'WAVED YELLOW', flash: true },
  },
  { id: 'yellow', name: 'Yellow', critical: true, motion: 'held', bits: ['yellow'], band: { shape: 'filled', colour: ds.purpose.flag.yellow, label: 'YELLOW FLAG' }, faceFlag: 'Flag_Yellow' },
  // `purpose.flag.debris` is the yellow, and the canvas draws the band as that yellow under danger
  // stripes. The stripes are a fourth shape and are not drawn: the name carries the difference on
  // the standard band, and on the nano, which writes no name, a debris flag reads as a yellow.
  { id: 'debris', name: 'Debris', critical: true, motion: 'moves', bits: ['debris'], band: { shape: 'filled', colour: ds.purpose.flag.debris, label: 'DEBRIS' } },
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
];

/** The face's flag properties, in this catalogue's order. */
export const FACE_FLAG_PRIORITY: readonly FaceFlag[] = FLAG_CATALOGUE.map((c) => c.faceFlag).filter((f): f is FaceFlag => f !== undefined);

export const flagCondition = (id: string): FlagCondition => {
  const found = FLAG_CATALOGUE.find((c) => c.id === id);
  if (found === undefined) throw new RangeError(`no flag condition ${JSON.stringify(id)}`);
  return found;
};

/** Any of the condition's bits is set. */
export const conditionRaised = (condition: FlagCondition, test: BitTest = bitSet): Expr => or(...condition.bits.map(test));

/**
 * How a surface reads a whole condition, which is the seam the ranking is parameterised over. The
 * box reads the bits; band D reads them null-safely and honours `limiter`, and it has to be the
 * same reading in the layer's own term and in the negated terms of every layer below it, or two
 * conditions could draw at once.
 */
export type RaisedTest = (condition: FlagCondition) => Expr;

/**
 * Band D's reading: null-safe, because a bare read of an absent property is not a boolean and the
 * band has to stay dark on a sim that publishes no `SessionFlagsDetails` rather than light up.
 */
export const bandRaised: RaisedTest = (condition) =>
  condition.limiter ? eq(isnull(prop(`DataCorePlugin.GameData.${condition.limiter}`), num(0)), num(1)) : conditionRaised(condition, safeBitSet);

/**
 * Whether a surface has the "critical flags only" switch, as an expression, or `false` for a
 * surface that has none and shows the whole list. Band D is the second kind: the box's switch is
 * `OpenDash.FlagBoxCriticalOnly` and belongs to the box, whereas a driver who wants band D quieter
 * has the flag format setting instead. Writing that as `false` rather than as a literal expression
 * keeps the term out of every layer's `Visible` rather than emitting `!(false)` fifteen times.
 */
export type CriticalOnly = Expr | false;

/**
 * The condition is raised *and the driver has asked to see it*.
 *
 * "Critical flags only" is a guard on the non-critical conditions rather than a second copy of the
 * catalogue. Carrying the whole list twice, once per branch of the switch, reads better in the
 * file and costs twice the glyphs — and the tree is already written once per matrix, so the
 * doubling is eightfold by the time it reaches disk. This is the version that fits.
 */
export function conditionShown(condition: FlagCondition, criticalOnly: CriticalOnly, read: RaisedTest = conditionRaised): Expr {
  const raised = read(condition);
  return condition.critical || criticalOnly === false ? raised : and(not(criticalOnly), raised);
}

/**
 * The condition is shown and nothing above it in the catalogue is: one flag at a time, ranked the
 * same way everywhere. A flag the switch has silenced stops outranking the ones below it, so the
 * box shows the next one down rather than going dark.
 */
export function conditionVisible(condition: FlagCondition, criticalOnly: CriticalOnly, only: readonly FlagCondition[] = FLAG_CATALOGUE, read: RaisedTest = conditionRaised): Expr {
  const index = only.indexOf(condition);
  if (index < 0) throw new RangeError(`${condition.id} is not in the list it is being ranked within`);
  const higher = only.slice(0, index).map((c) => not(conditionShown(c, criticalOnly, read)));
  return and(...higher, conditionShown(condition, criticalOnly, read));
}

/** Nothing in the catalogue is being shown, which is what everything below the flags needs. */
export const noFlagShown = (criticalOnly: CriticalOnly, only: readonly FlagCondition[] = FLAG_CATALOGUE, read: RaisedTest = conditionRaised): Expr =>
  and(...only.map((c) => not(conditionShown(c, criticalOnly, read))));

/** The catalogue, or only the critical part of it. */
export const flagsShown = (criticalOnly: boolean): readonly FlagCondition[] =>
  criticalOnly ? FLAG_CATALOGUE.filter((c) => c.critical) : FLAG_CATALOGUE;
