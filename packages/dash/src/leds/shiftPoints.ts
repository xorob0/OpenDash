/**
 * The per-gear shift table, and the three places a ladder can come from.
 *
 * ADR 0014 settled that openDash mirrors the car rather than carrying a table, and XOR-233 amended
 * it: derived by default, a table overriding where one exists. This module is the table half and
 * the precedence between them.
 *
 * The precedence, highest first:
 *
 *   1. **This table**, per car and per gear, for a car somebody has measured.
 *   2. **SimHub's own per-gear redline**, when the user has turned it on by hand —
 *      `CarSettings_RPMRedLinePerGearOverride` is 1 and `CarSettings_CurrentGearRedLineRPM` then
 *      varies with the gear. Their numbers, not ours.
 *   3. **The car's own ladder** from iRacing's four `DriverCarSL*` RPMs — one set for the car.
 *   4. **SimHub's bands**, for a car that publishes no ladder at all (ADR 0004).
 *
 * What is *not* in that list is a derived per-gear source, because there is not one. iRacing
 * publishes no per-gear shift data of any kind, and SimHub's learned table is unreachable from an
 * expression and is seeded identically for every gear on iRacing anyway. The evidence is in
 * docs/research/simhub-led-sources.md.
 */
import table from '../../../../data/shift-points.json';
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { firstRpm, lastRpm, rpms, shiftRpm } from '../shift.ts';

const { and, eq, game, gt, ge, isnull, num, raw, str } = ncalc;

/** One gear's four thresholds, in RPM. */
export interface GearShiftPoints {
  first: number;
  shift: number;
  last: number;
  blink: number;
}

/** One car's measured gears, and where the measurement came from. */
export interface CarShiftPoints {
  name: string;
  source: string;
  /** Keyed by forward gear as a string: "1", "2", … A gear left out falls back to the car's ladder. */
  gears: Record<string, GearShiftPoints>;
}

interface ShiftTable {
  $meta: unknown;
  cars: Record<string, CarShiftPoints>;
}

const data = table as unknown as ShiftTable;

/** Every car in the table, in the order the file lists them. */
export const SHIFT_TABLE: Record<string, CarShiftPoints> = data.cars ?? {};

/** The cars the table covers. Empty is a legitimate state: see the file's own `$meta.empty`. */
export const tabledCars = (): string[] => Object.keys(SHIFT_TABLE);

/** What is wrong with the table, or an empty list. Errors here fail `bun run check`. */
export function validateShiftTable(t: Record<string, CarShiftPoints> = SHIFT_TABLE): string[] {
  const problems: string[] = [];
  for (const [model, car] of Object.entries(t)) {
    if (!car.name?.trim()) problems.push(`${model}: needs a name`);
    // Provenance is not optional. A number nobody can trace is a shift light in the wrong place.
    if (!car.source?.trim()) problems.push(`${model}: needs a source saying how the numbers were got`);
    const gears = Object.entries(car.gears ?? {});
    if (gears.length === 0) problems.push(`${model}: has no gears, so it overrides nothing`);
    for (const [gear, p] of gears) {
      if (!/^[1-9][0-9]?$/.test(gear)) problems.push(`${model}: "${gear}" is not a forward gear number`);
      const values = [p.first, p.shift, p.last, p.blink];
      if (values.some((v) => typeof v !== 'number' || !Number.isFinite(v) || v <= 0)) {
        problems.push(`${model} gear ${gear}: first, shift, last and blink must all be RPM above zero`);
        continue;
      }
      // Ordered, or the bands invert and the ladder runs backwards.
      if (!(p.first <= p.shift && p.shift <= p.last && p.last <= p.blink)) {
        problems.push(`${model} gear ${gear}: needs first <= shift <= last <= blink, got ${values.join(' ')}`);
      }
    }
  }
  return problems;
}

/** iRacing's numeric gear. `[Gear]` is a string ("N", "R", "1"), so the raw one is the one to compare. */
const gearNumber = (): Expr => isnull(raw('Gear'), num(0));

/** The car SimHub says we are in, which is what a table entry is keyed by. */
export const carModel = (): Expr => isnull(game('CarModel'), str(''));

/** Whether the user has turned SimHub's own per-gear redline on for this car. */
export const simhubPerGear = (): Expr => eq(isnull(game('CarSettings_RPMRedLinePerGearOverride'), num(0)), num(1));

/** SimHub's redline for the gear we are in, which varies per gear only under {@link simhubPerGear}. */
export const simhubGearRedline = (): Expr => isnull(game('CarSettings_CurrentGearRedLineRPM'), num(0));

/** Whether this car and gear have a measured entry: the condition a table override is gated on. */
export const tabledGear = (model: string, gear: string): Expr => and(eq(carModel(), str(model)), eq(gearNumber(), num(Number(gear))));

/**
 * Whether the car's own published ladder is in use — i.e. nothing above it in the precedence
 * applies. Exported so the rev bar and the strip can agree about which rung of the precedence they
 * are on, and so a test can prove they do.
 */
export const usingPublishedLadder = (): Expr => and(gt(firstRpm(), num(0)), gt(lastRpm(), firstRpm()), ge(shiftRpm(), firstRpm()), ge(lastRpm(), shiftRpm()));

/**
 * Rung `local` of `count` in a band, against absolute RPMs from a table entry. The thresholds are
 * known at build time here, unlike the published ladder's, so the whole comparison folds to one
 * number and the expression is a single `>` rather than a cross-multiplication.
 */
export const tabledBandLit = (from: number, to: number, local: number, count: number): Expr => gt(rpms(), num(Math.round(from + ((to - from) * local) / count)));

/** Stage `stage`, rung `local` of `count`, from one gear's measured thresholds. */
export const tabledStageLit = (points: GearShiftPoints, stage: number, local: number, count: number): Expr =>
  stage === 0
    ? tabledBandLit(points.first, points.shift, local, count)
    : stage === 1
      ? tabledBandLit(points.shift, points.last, local, count)
      : ge(rpms(), num(points.last));

/** Over-rev, from one gear's measured thresholds. */
export const tabledOverRev = (points: GearShiftPoints): Expr => ge(rpms(), num(points.blink));
