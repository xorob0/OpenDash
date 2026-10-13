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

const { game, raw, isNull, isnull, not, or, gt, num } = ncalc;

/**
 * One watched setting.
 *
 * `has` exists because for three of the seven the value and the evidence the car has the control
 * are not the same property: SimHub normalises traction control and ABS into `TCLevel` and
 * `ABSLevel` and reports 0 for a car that has neither, which is indistinguishable from a driver who
 * has turned them off, and the brake bias's reading is already wrapped in a default so is never null
 * itself. It is a condition rather than a property to null-test, because for two of the three the
 * answer is two properties, not one: see {@link assistPresent}.
 */
export interface TrackedValue {
  id: string;
  /** What the bar's strip calls it, in its own narrow cell. */
  strip: string;
  /** What a change notification calls it, where there is room for the whole name. */
  notice: string;
  /** What DashStudio draws. */
  sample: string;
  /**
   * The longest reading the setting can draw, which every box that shows it is cut from and measured
   * by: {@link LEVEL_WIDEST} for a level and {@link BIAS_WIDEST} for the bias. The boxes used to be
   * cut from the sample, and most samples are one digit, so the strip and the change notification
   * drew a TC of 10 in the one cell its `5` took. #596.
   */
  widest: string;
  read: Expr;
  /** .NET format string the reading is written with. */
  pattern: string;
  /** True when the car has this setting; the reading being published when nothing else says so. */
  has?: Expr;
}

/**
 * Whether the car has the setting, which is what every consumer asks before it draws or announces
 * one: the strip hides a cell without it, the notification stays quiet.
 */
export const hasSetting = (value: TrackedValue): Expr => value.has ?? not(isNull(value.read));

/**
 * Present when the sim publishes the driver-adjustable control, or when SimHub has a level for it
 * anyway.
 *
 * Two questions, because iRacing's `dcTractionControl` and `dcABS` answer "can the driver turn
 * this knob", which is narrower than "does this car have the system". A car with fixed traction
 * control publishes no knob, and the settings grid then drew no TC cell at all -- which reads as a
 * car without traction control rather than one whose TC is not adjustable. Reported from a rig as
 * the settings page maybe missing TC and ABS.
 *
 * SimHub's normalised `TCLevel` and `ABSLevel` are the second answer: zero for a car with neither,
 * so a level above zero is a system that exists whether or not its knob does. Either signal shows
 * the cell; neither still hides it, which is the rule the strip, the pit wall and the settings page
 * are all built on.
 *
 * The second answer is also the only one a sim other than iRacing can give. The `dc*` knobs are
 * iRacing's raw telemetry and exist nowhere else, so a strip that asked the knob alone showed an
 * Assetto Corsa car its brake bias and hid its TC and ABS (#549). What that sim writes into the
 * level, and from what, is #549's to measure; a level of zero there still hides the cell, which is
 * honest where a zero drawn as a dial would not be.
 */
export const assistPresent = (knob: Expr, level: Expr): Expr => or(not(isNull(knob)), gt(isnull(level, num(0)), num(0)));

/** The widest level a stepped setting draws: two digits, a GT3 car's TC and ABS dials running to twelve. */
export const LEVEL_WIDEST = '88';
/** The widest brake bias: two digits and a place, a front share always being under a hundred. */
export const BIAS_WIDEST = '88.8';

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
  { id: 'slip', strip: 'Slip', notice: 'Slip', sample: '4', read: raw('dcThrottleShape'), widest: LEVEL_WIDEST, pattern: '0' },
  { id: 'tc', strip: 'TC', notice: 'TC', sample: '5', read: tcLevel(), widest: LEVEL_WIDEST, pattern: '0', has: assistPresent(raw('dcTractionControl'), tcLevel()) },
  { id: 'cut', strip: 'Cut', notice: 'TC cut', sample: '2', read: raw('dcTractionControl2'), widest: LEVEL_WIDEST, pattern: '0' },
  { id: 'bias', strip: 'Bias', notice: 'Brake bias', sample: '50.5', read: brakeBias(), widest: BIAS_WIDEST, pattern: '0.0', has: not(isNull(game('BrakeBias'))) },
  { id: 'abs', strip: 'ABS', notice: 'ABS', sample: '4', read: absLevel(), widest: LEVEL_WIDEST, pattern: '0', has: assistPresent(raw('dcABS'), absLevel()) },
  { id: 'map', strip: 'Map', notice: 'Engine map', sample: '1', read: fuelMixture(), widest: LEVEL_WIDEST, pattern: '0' },
  { id: 'diff', strip: 'Diff', notice: 'Diff', sample: '4', read: antiRollRear(), widest: LEVEL_WIDEST, pattern: '0' },
];

/** The entry with this id, or the failure to build: an id nothing claims is a typo, not a state. */
export function trackedValue(id: string): TrackedValue {
  const value = TRACKED_VALUES.find((v) => v.id === id);
  if (!value) throw new RangeError(`${id} is not a tracked value`);
  return value;
}
