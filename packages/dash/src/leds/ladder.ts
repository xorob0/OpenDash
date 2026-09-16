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
 * first, and differs only in its colours.
 */
export const ladderOrder = (style: LedRpmStyle, count: number): LadderOrder =>
  style === 'meetInMiddle'
    ? { rungs: Math.ceil(count / 2), rungOf: (k) => Math.min(k, count - 1 - k) }
    : { rungs: count, rungOf: (k) => k };

/**
 * The colour a style lights each of its three bands in, always from `design/tokens.json`.
 *
 * `f1` is green and red rather than green, amber and red — the look of a modern formula car's
 * wheel, drawn in openDash's own palette — and it is green as far as the shift point rather than
 * through a middle band of its own, which is why the first two bands hold one colour instead of
 * the style carrying a band split of its own. On a fourteen-rung run that is green to rung 9 and
 * red from rung 10, exactly where the top band begins, so the colour still says which band the
 * engine is in.
 *
 * Blue is no band's colour in any style now: it belongs to the over-rev alone (see
 * {@link OVER_REV_COLOR}), which is what a formula wheel keeps it for and what it could not mean
 * while the top band was also drawn in it.
 *
 * None of this is a copy of any car's sequence, and could not be: the sim publishes thresholds and
 * never colour (ADR 0014), so a car's own colours belong to Car themes rather than here.
 */
export const ladderColors = (style: LedRpmStyle): readonly [string, string, string] =>
  style === 'f1'
    ? [ds.color.good.primary, ds.color.good.primary, ds.color.danger.primary]
    : [ds.purpose.shift.stage1, ds.purpose.shift.stage2, ds.purpose.shift.stage3];

/**
 * What the whole bar turns over the blink RPM, in every style.
 *
 * `color.info.primary` is read directly because there is no purpose token for it: the canvas asks
 * for a `purpose.shift.blink` aliased to the same `palette.blue.200`, and `design/tokens.json` is
 * the author's file. The hex is the one the canvas draws either way; only the name is missing.
 */
export const OVER_REV_COLOR: string = ds.color.info.primary;

/** The band (0, 1, 2) a rung falls in: thirds, the last third taking any remainder. */
export const bandOf = (rung: number, rungs: number): number => Math.min(2, Math.floor((rung * 3) / rungs));

/**
 * Where a band starts and how many rungs it has, so a rung can be placed inside it. Exported
 * because `rpmStrip.ts` places a measured rung in its band too, and spelled it out inline until it
 * did: one band split, read from here by everything that colours a rung or lights one.
 */
export const bandSpan = (band: number, rungs: number): { start: number; count: number } => {
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

// Whether a rung flashes used to be a question asked here, and it is not one any more. Every style
// flashed its top band and `f1` flashed the whole bar, so over-rev read as the top of the ladder
// blinking in two of the three styles and as a bar changing colour in the third. It is one layer
// over the rungs now, in one colour, emitted by `rpmStrip.ts` after them — so a style decides which
// LED takes which rung and what colour it is, and decides nothing at all about the flash.

// The half period of the over-rev flash used to be declared here, as `1000 / shiftLights.flashHz / 2`,
// and every blink on the strip was a multiple of it: the indicators at four times, the limiter at
// three, the fuel bar at four again. A strip has two rates and they live in `effects.ts`, so the
// over-rev flash takes the fast one like everything else urgent rather than setting a rate the rest
// of the strip is measured against. The face keeps `shiftLights.flashHz` for its own redline.

/**
 * One LED of a progressive bar over a 0..100 input: lit once `value` has passed `k` of `count`
 * equal steps. A cross-multiplication rather than a division, for the same reason the rev bar's
 * is — nothing divides by a zero range.
 */
export const stepLit = (value: Expr, k: number, count: number): Expr => gt(mul(value, num(count)), num(k * 100));

/** A 0..1 input compared against a 0..1 threshold, for telemetry SimHub publishes as a fraction. */
export const fractionLit = (value: Expr, k: number, count: number): Expr => gt(mul(value, num(count)), num(k));
