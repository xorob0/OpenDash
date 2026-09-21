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
 * The bands survive the styles.
 *
 * `ladderOrder`, `ladderColors`, `rungLit`, `bandSpan` and `OVER_REV_COLOR` were here and are gone
 * with #369: they drew openDash's own rev ladder on a strip, one `CustomStatus` per LED per band,
 * and a strip's rev bar is SimHub's own `RPMSegments` now. What is left is what the screens and
 * the flag box read -- the band a rung falls in, whether the engine is over-revving, and the two
 * step tests the pedal and fuel bars are built from.
 */
export const bandOf = (rung: number, rungs: number): number => Math.min(2, Math.floor((rung * 3) / rungs));

/**
 * Where a band starts and how many rungs it has, so a rung can be placed inside it. Exported
 * because `rpmStrip.ts` places a measured rung in its band too, and spelled it out inline until it
 * did: one band split, read from here by everything that colours a rung or lights one.
 */
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
