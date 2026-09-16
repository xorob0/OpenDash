/**
 * The rev ladder on a strip, and the three ways it can fill.
 *
 * The *thresholds* are always the car's own (ADR 0014) — a style never changes when a light comes
 * on, only which LED takes which rung and what colour it is. That separation is the point: a
 * driver who prefers one look still gets their own car's shift points.
 *
 * The rungs themselves come from `../shift.ts`, the same module the rev bar reads, so a strip and
 * a screen in one rig cannot disagree.
 */
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import type { LedRpmStyle } from '../contract.ts';
import { mirrorOverRev, mirrorStageLit, simhubOverRev, simhubStageLit } from '../shift.ts';
import { ds } from '../tokens.ts';

const { gt, mul, num } = ncalc;

/** Which ladder a rung is read from: the car's own, or SimHub's bands for a car that has none. */
export type Ladder = 'mirror' | 'simhub';

/**
 * Where a style puts its rungs. `rungOf` is the ladder rung physical LED `k` takes, and `rungs` is
 * how many rungs the style has — which is not the LED count for a style that lights two at a time.
 */
export interface LadderOrder {
  rungs: number;
  rungOf: (k: number) => number;
}

/**
 * `leftToRight` is one rung per LED, in order. `meetInMiddle` lights both ends together and works
 * inwards, so it has half as many rungs and each lights a pair. `f1` fills left to right like the
 * first, and differs only in its colours and its flash.
 */
export const ladderOrder = (style: LedRpmStyle, count: number): LadderOrder =>
  style === 'meetInMiddle'
    ? { rungs: Math.ceil(count / 2), rungOf: (k) => Math.min(k, count - 1 - k) }
    : { rungs: count, rungOf: (k) => k };

/**
 * The three colours a style lights its bands in, always from `design/tokens.json`.
 *
 * `f1` is green, red and blue rather than green, amber and red — the look of a modern formula car's
 * wheel, drawn in openDash's own palette. It is not a copy of any car's sequence, and could not be:
 * the sim publishes thresholds and never colour (ADR 0014), so a car's own colours belong to Car
 * themes rather than here.
 */
export const ladderColors = (style: LedRpmStyle): readonly [string, string, string] =>
  style === 'f1'
    ? [ds.color.good.primary, ds.color.danger.primary, ds.color.info.primary]
    : [ds.purpose.shift.stage1, ds.purpose.shift.stage2, ds.purpose.shift.stage3];

/** The band (0, 1, 2) a rung falls in: thirds, the last third taking any remainder. */
export const bandOf = (rung: number, rungs: number): number => Math.min(2, Math.floor((rung * 3) / rungs));

/** Where a band starts and how many rungs it has, so a rung can be placed inside it. */
const bandSpan = (band: number, rungs: number): { start: number; count: number } => {
  const indexes = Array.from({ length: rungs }, (_, i) => i);
  return { start: indexes.findIndex((i) => bandOf(i, rungs) === band), count: indexes.filter((i) => bandOf(i, rungs) === band).length };
};

/** Whether rung `rung` of `rungs` is lit, under the chosen ladder. */
export const rungLit = (ladder: Ladder, rung: number, rungs: number): Expr => {
  const band = bandOf(rung, rungs);
  const { start, count } = bandSpan(band, rungs);
  const local = rung - start;
  return ladder === 'mirror' ? mirrorStageLit(band, local, count) : simhubStageLit(band, local, count);
};

/** Whether the bar is over-revving, under the chosen ladder. Neither half flashes in the last gear. */
export const overRev = (ladder: Ladder): Expr => (ladder === 'mirror' ? mirrorOverRev() : simhubOverRev());

/**
 * Whether a rung flashes. Every style flashes its top band at over-rev; `f1` flashes the *whole*
 * bar, which is what a formula wheel does and the reason it is a style of its own rather than a
 * palette swap.
 */
export const rungFlashes = (style: LedRpmStyle, rung: number, rungs: number): boolean => style === 'f1' || bandOf(rung, rungs) === 2;

/** Half period of the over-rev flash, the same 62 ms the rev bar blinks at. */
export const OVER_REV_BLINK_MS = Math.floor(1000 / ds.shiftLights.flashHz / 2);

/**
 * One LED of a progressive bar over a 0..100 input: lit once `value` has passed `k` of `count`
 * equal steps. A cross-multiplication rather than a division, for the same reason the rev bar's
 * is — nothing divides by a zero range.
 */
export const stepLit = (value: Expr, k: number, count: number): Expr => gt(mul(value, num(count)), num(k * 100));

/** A 0..1 input compared against a 0..1 threshold, for telemetry SimHub publishes as a fraction. */
export const fractionLit = (value: Expr, k: number, count: number): Expr => gt(mul(value, num(count)), num(k));
