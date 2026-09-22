/**
 * The car settings OpenDash watches: what each one reads, what it is called and how it is written.
 *
 * One list with two consumers, which is the point of the file. The bar's strip draws them settled,
 * as a rank of cells a driver reads between corners, and the change notification draws one of them
 * the moment it moves. A second list would eventually disagree with the first about what a value is,
 * and the disagreement would show as a strip and a notification quoting two numbers for the same
 * dial.
 *
 * **The two names are deliberately not one name.** The strip's cells are tight -- at 850 x 480 the
 * two ends of the bar take almost the whole width and the fifth cell is what is shed first -- while
 * the notification has four hundred pixels to itself. So "Bias" and "Brake bias" are both right, each
 * in its own box, and `notice` is the fuller one.
 *
 * **What is not here.** ERS mode, which the canvas lists, is left out while
 * docs/research/simhub-led-sources.md records `ERSPercent` as always zero and `EngineMap` as always
 * -1 on iRacing: a notification that can never fire is worse than an absent one. The bite point and
 * the wheel switches wait on the same check against the decompiled reader.
 */
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { absLevel, antiRollRear, brakeBias, fuelMixture, tcLevel } from './values.ts';

const { game, raw } = ncalc;

/**
 * One watched setting.
 *
 * `present` exists because for three of the seven the value and the evidence the car has the
 * control are not the same property: SimHub normalises traction control and ABS into `TCLevel` and
 * `ABSLevel` and reports 0 for a car that has neither, which is indistinguishable from a driver who
 * has turned them off, and the brake bias's reading is already wrapped in a default so is never null
 * itself. The raw iRacing field behind each is simply absent on a car without the control.
 */
export interface TrackedValue {
  id: string;
  /** What the bar's strip calls it, in its own narrow cell. */
  strip: string;
  /** What a change notification calls it, where there is room for the whole name. */
  notice: string;
  /** What DashStudio draws, and what the cell and the box are measured from. */
  sample: string;
  read: Expr;
  /** .NET format string the reading is written with. */
  pattern: string;
  /** What says the car has this setting; the value's own property when they are the same. */
  present?: Expr;
}

/**
 * The seven, in the order the canvas draws the strip. Cut is `dcTractionControl2`, the second
 * traction dial a GT3 car exposes beside TC level.
 *
 * Two of them read a property their label does not name, and both are left alone because settling
 * them is a drawing decision rather than a lookup. Diff reads `dcAntiRollRear`, which
 * `modules/carSettings.ts` already draws under the label ARB R, so the bar and page 09 publish one
 * number under two names. Slip reads `dcThrottleShape`, which is the throttle map. Neither a
 * differential nor a slip target is normalised by SimHub, and the iRacing variable set recorded in
 * `tools/irsdk-emulator/Catalog.cs` holds no such field, so a car that has either publishes it under
 * a name of its own and the binding cannot be looked up; `docs/design/zones.md` section 3 records
 * the disagreement. Those two therefore keep one name in both boxes, since a fuller name would have
 * to claim a meaning the repository says is not settled, while the five whose reading is settled
 * take the canvas's own words.
 *
 * TODO: bind Diff and Slip once the canvas says which in-car adjustment each shows, or rename them.
 */
export const TRACKED_VALUES: readonly TrackedValue[] = [
  { id: 'slip', strip: 'Slip', notice: 'Slip', sample: '4', read: raw('dcThrottleShape'), pattern: '0' },
  { id: 'tc', strip: 'TC', notice: 'TC', sample: '5', read: tcLevel(), pattern: '0', present: raw('dcTractionControl') },
  { id: 'cut', strip: 'Cut', notice: 'TC cut', sample: '2', read: raw('dcTractionControl2'), pattern: '0' },
  { id: 'bias', strip: 'Bias', notice: 'Brake bias', sample: '50.5', read: brakeBias(), pattern: '0.0', present: game('BrakeBias') },
  { id: 'abs', strip: 'ABS', notice: 'ABS', sample: '4', read: absLevel(), pattern: '0', present: raw('dcABS') },
  { id: 'map', strip: 'Map', notice: 'Engine map', sample: '1', read: fuelMixture(), pattern: '0' },
  { id: 'diff', strip: 'Diff', notice: 'Diff', sample: '4', read: antiRollRear(), pattern: '0' },
];

/** The entry with this id, or the failure to build: an id nothing claims is a typo, not a state. */
export function trackedValue(id: string): TrackedValue {
  const value = TRACKED_VALUES.find((v) => v.id === id);
  if (!value) throw new RangeError(`${id} is not a tracked value`);
  return value;
}
