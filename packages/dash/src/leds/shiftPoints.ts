/**
 * The per-gear shift table, and where it sits among the places a ladder can come from.
 *
 * ADR 0014 settled that openDash mirrors the car rather than carrying a table, and XOR-233 amended
 * it: derived by default, a table overriding where one exists. This module is the table half.
 *
 * **The precedence, highest first, and what of it is built.** This is a description of the code as
 * it stands, not of the design:
 *
 *   1. **This table**, per car and per gear, for a car somebody has measured. *Built, on the RPM
 *      strips only.* `tabledStageLit` and `tabledOverRev` have exactly one caller in the build,
 *      `rpmStrip.ts`, which emits one conditional group per car and gear the table covers
 *      (`test/leds.test.ts` calls them too, which is a test and not a surface). No screen surface
 *      and not the flag box reads the table: the rev bar, the rev arc, the companion's speedo and
 *      the box's gear are all on rung 3 or 4 unconditionally.
 *   2. **SimHub's own per-gear redline**, when the user has turned it on by hand —
 *      `CarSettings_RPMRedLinePerGearOverride` is 1 and `CarSettings_CurrentGearRedLineRPM` then
 *      varies with the gear. Their numbers, not ours. **Not built.** `simhubPerGear` below is the
 *      switch, nothing calls it, and `CarSettings_RPMRedLinePerGearOverride` appears in no
 *      generated `.ledsprofile` and no generated `.simhubdash`. It is kept as the worked-out form
 *      of a rung somebody may add, and is dead until something does.
 *
 *      `CarSettings_CurrentGearRedLineRPM` is a different matter and **does** appear in the build —
 *      in `openDash.simhubdash` and `openDash Companion.simhubdash`, the two packages that draw the
 *      speedo page at a `wide` box and so keep its Redline field. `simhubRedlineRpm` in
 *      `../shift.ts` is its one body, and `redlineRpm` there prints it as the fallback for a car
 *      that publishes no ladder of its own. That is rung 4's world, not this rung: it is read as a
 *      plain redline, never gated on the override, and so never per gear.
 *   3. **The car's own ladder** from iRacing's four `DriverCarSL*` RPMs — one set for the car.
 *      *Built, everywhere.* It lives in `../shift.ts`; `mirrorAvailable` there is the gate, and is
 *      the only definition of it.
 *   4. **SimHub's bands**, for a car that publishes no ladder at all (ADR 0004). *Built,
 *      everywhere*, as the other half of the same per-frame choice.
 *
 * **Nothing diverges today, and that is why the gap is easy to miss.** `data/shift-points.json`
 * ships empty (see its own `$meta.empty`), so rung 1 emits no containers, rung 2 does not exist,
 * and every surface in the build is reading rungs 3 and 4 — the same two expressions, on the same
 * frame. The first measured car put into the table is also the first time a strip and a screen in
 * one rig will say different things, and closing that is work rather than a property of the model.
 *
 * What is *not* in the list at all is a derived per-gear source, because there is not one. iRacing
 * publishes no per-gear shift data of any kind, and SimHub's learned table is unreachable from an
 * expression and is seeded identically for every gear on iRacing anyway. The evidence is in
 * docs/research/simhub-led-sources.md.
 */
import table from '../../../../data/shift-points.json';
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { rpms } from '../shift.ts';
// The car a table entry is keyed by. One body, in `second/values.ts`; this file used to carry a
// byte-identical second one, which is the defect ADR 0014 records for `gearRedline`.
import { carModel } from '../second/values.ts';

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


/**
 * Whether the user has turned SimHub's own per-gear redline on for this car.
 *
 * Rung 2 of the header's precedence, and **nothing calls this**: `CarSettings_RPMRedLinePerGearOverride`
 * appears in nothing the build writes. It is written out rather than described so that adding the
 * rung is a wiring job rather than a research one.
 *
 * The other half of the rung — SimHub's redline RPM itself — is not written out here, because it is
 * not dead: `simhubRedlineRpm` in `../shift.ts` reads it as the fallback half of `redlineRpm`, and
 * that is its one body. This file used to carry a byte-identical second one under the name
 * `simhubGearRedline`, which is how the companion's speedo came to print SimHub's number beside a
 * bar reading the car's.
 */
export const simhubPerGear = (): Expr => eq(isnull(game('CarSettings_RPMRedLinePerGearOverride'), num(0)), num(1));

/** Whether this car and gear have a measured entry: the condition a table override is gated on. */
export const tabledGear = (model: string, gear: string): Expr => and(eq(carModel(), str(model)), eq(gearNumber(), num(Number(gear))));

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
