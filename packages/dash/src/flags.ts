/**
 * The flag catalogue, in priority order, for everything that shows a flag.
 *
 * One list, because the face, the pit wall and the flag box rank the same conditions and two
 * lists would eventually disagree about which of two live flags wins. The face draws a subset of
 * it — `FLAG_PRIORITY` in components/flagStrip.ts is derived from this file's order rather than
 * restated — and the 8x8 box draws all of it. XOR-58 folds the rest of the alert catalogue in
 * here; this is the flag half of it.
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
 */
import { ncalc } from './generator.ts';
import type { Expr } from './bind.ts';

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

/** The SimHub flag properties the face's band and ring draw. */
export type FaceFlag = 'Flag_Black' | 'Flag_Checkered' | 'Flag_Yellow' | 'Flag_Blue' | 'Flag_White' | 'Flag_Green';

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
  /** The face's own property for this condition, when the face draws it. */
  faceFlag?: FaceFlag;
}

/**
 * Highest priority first. The subset carrying `faceFlag` keeps the face's existing order exactly:
 * black, chequered, yellow, blue, white, green.
 *
 * The shape of it is: the session is stopped, then anything addressed to this car, then the race
 * is over, then slow down, then yield, then the lap, then the start gantry.
 */
export const FLAG_CATALOGUE: readonly FlagCondition[] = [
  { id: 'red', name: 'Red', critical: true, motion: 'moves', bits: ['red'] },
  { id: 'disqualify', name: 'Disqualified', critical: true, motion: 'moves', bits: ['disqualify'] },
  { id: 'black', name: 'Black', critical: true, motion: 'moves', bits: ['black'], faceFlag: 'Flag_Black' },
  { id: 'furled', name: 'Black furled', critical: true, motion: 'moves', bits: ['furled'] },
  { id: 'meatball', name: 'Meatball', critical: true, motion: 'moves', bits: ['repair'] },
  { id: 'chequered', name: 'Chequered', critical: false, motion: 'moves', bits: ['checkered'], faceFlag: 'Flag_Checkered' },
  // Full-course caution: in iRacing this is the pace car being deployed, which is the closest
  // honest reading of a safety car. It outranks a local yellow because it is the whole track.
  { id: 'caution', name: 'Full-course caution', critical: true, motion: 'moves', bits: ['caution', 'cautionWaving'] },
  { id: 'yellowWaving', name: 'Waved yellow', critical: true, motion: 'moves', bits: ['yellowWaving'] },
  { id: 'yellow', name: 'Yellow', critical: true, motion: 'held', bits: ['yellow'], faceFlag: 'Flag_Yellow' },
  { id: 'debris', name: 'Debris', critical: true, motion: 'moves', bits: ['debris'] },
  { id: 'blue', name: 'Blue', critical: true, motion: 'held', bits: ['blue'], faceFlag: 'Flag_Blue' },
  { id: 'white', name: 'White', critical: false, motion: 'held', bits: ['white'], faceFlag: 'Flag_White' },
  { id: 'green', name: 'Green', critical: false, motion: 'held', bits: ['green'], faceFlag: 'Flag_Green' },
  { id: 'startSet', name: 'Set', critical: false, motion: 'held', bits: ['startSet'] },
  { id: 'startReady', name: 'Ready', critical: false, motion: 'held', bits: ['startReady'] },
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
 * The condition is raised *and the driver has asked to see it*.
 *
 * "Critical flags only" is a guard on the non-critical conditions rather than a second copy of the
 * catalogue. Carrying the whole list twice, once per branch of the switch, reads better in the
 * file and costs twice the glyphs — and the tree is already written once per matrix, so the
 * doubling is eightfold by the time it reaches disk. This is the version that fits.
 */
export function conditionShown(condition: FlagCondition, criticalOnly: Expr): Expr {
  const raised = conditionRaised(condition);
  return condition.critical ? raised : and(not(criticalOnly), raised);
}

/**
 * The condition is shown and nothing above it in the catalogue is: one flag at a time, ranked the
 * same way everywhere. A flag the switch has silenced stops outranking the ones below it, so the
 * box shows the next one down rather than going dark.
 */
export function conditionVisible(condition: FlagCondition, criticalOnly: Expr, only: readonly FlagCondition[] = FLAG_CATALOGUE): Expr {
  const index = only.indexOf(condition);
  if (index < 0) throw new RangeError(`${condition.id} is not in the list it is being ranked within`);
  const higher = only.slice(0, index).map((c) => not(conditionShown(c, criticalOnly)));
  return and(...higher, conditionShown(condition, criticalOnly));
}

/** Nothing in the catalogue is being shown, which is what everything below the flags needs. */
export const noFlagShown = (criticalOnly: Expr, only: readonly FlagCondition[] = FLAG_CATALOGUE): Expr =>
  and(...only.map((c) => not(conditionShown(c, criticalOnly))));

/** The catalogue, or only the critical part of it. */
export const flagsShown = (criticalOnly: boolean): readonly FlagCondition[] =>
  criticalOnly ? FLAG_CATALOGUE.filter((c) => c.critical) : FLAG_CATALOGUE;
