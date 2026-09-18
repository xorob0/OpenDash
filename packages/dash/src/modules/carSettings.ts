/**
 * Module 9, Car settings: the driver-adjustable values. A field whose property the sim does not
 * publish is hidden rather than shown as `--`: the grid should say what this car has, not list
 * what this sim lacks.
 *
 * **The car's model and number are not among them.** They used to lead the page, as a label reading
 * "CAR · PORSCHE 992 CUP" over the number in digit cells, and they are the one thing on it that
 * cannot change while you drive and that you already know: you chose the car. The row they took is
 * a row the settings grid now has, which is the page's whole point. The model and the number keep
 * the places where they identify somebody else -- a leaderboard row, the relative, the opponents
 * page -- which is where a number is worth a cell.
 */
import { ncalc } from '../generator.ts';
import { densityOf } from '../second/density.ts';
import { stack } from '../second/layout.ts';
import { CHARS, absLevel, antiRollFront, antiRollRear, brakeBias, fuelMixture, tcLevel } from '../second/values.ts';
import { defineModule, drawnAt, fieldsRow, fld } from './module.ts';
import type { Expr } from '../bind.ts';
import type { FieldSpec } from '../second/field.ts';
import type { ModuleContext } from './module.ts';
import type { Archetype } from './shedding.ts';

const { fmt, isNull, not, or, gt, num, isnull, raw, game } = ncalc;

/**
 * A setting field that disappears when the sim does not publish the property behind it, and whose
 * rank then closes over the hole.
 *
 * `present` is the property that says the car **has** the setting, which is not always the one the
 * value is read from. SimHub normalises traction control and ABS into `TCLevel` and `ABSLevel` and
 * reports 0 for a car that has neither, so those two are tested on the raw iRacing field behind
 * them; the brake bias is read through an `isnull` default and so is never null itself.
 */
const settingField = (ctx: ModuleContext, id: string, label: string, expr: string, pattern: string, fs: number, present = expr): FieldSpec =>
  fld(ctx, id, label, { sample: '3', bind: fmt(expr, pattern), chars: CHARS.setting, fs }, { visibleBind: not(isNull(present)) });

/**
 * The same field, but told directly when it is there rather than handed a property to test.
 *
 * Apart from {@link settingField} because the two take different things: that one takes a property
 * and asks whether the sim published it, this one takes the answer. Passing a condition to the first
 * produces `!(isnull(<a boolean>))`, which is true whatever the boolean was -- a field that can no
 * longer hide, silently, which is worse than the gap it was meant to close.
 */
const presentField = (ctx: ModuleContext, id: string, label: string, expr: string, pattern: string, fs: number, visible: Expr): FieldSpec =>
  fld(ctx, id, label, { sample: '3', bind: fmt(expr, pattern), chars: CHARS.setting, fs }, { visibleBind: visible });

/**
 * Present when iRacing publishes the driver-adjustable control, or when SimHub has a level for it
 * anyway.
 *
 * Two questions, because `dcTractionControl` and `dcABS` answer "can the driver turn this knob",
 * which is narrower than "does this car have the system". A car with fixed traction control
 * publishes no knob, and the grid then drew no TC cell at all -- which reads as a car without
 * traction control rather than one whose TC is not adjustable. Reported from a rig as the settings
 * page maybe missing TC and ABS.
 *
 * SimHub's normalised `TCLevel` and `ABSLevel` are the second answer: zero for a car with neither,
 * so a level above zero is a system that exists whether or not its knob does. Either signal shows
 * the cell; neither still hides it, which is the rule this module is built on.
 */
const assistPresent = (knob: Expr, level: Expr): Expr => or(not(isNull(knob)), gt(isnull(level, num(0)), num(0)));

/** The canvas sets a readout group's pairs 20 px apart, which is closer than a row of fields. */
const SETTING_GAP = 20;

/**
 * How many cells a line of the grid holds, by the drawing the page is taking.
 *
 * The catalogue's own counts: three columns at `wide` and at `grid`, two at `tall narrow` and at
 * `tall`, always the same cell width so that the cells share an x down the block. `columnsAt` is
 * not the answer here, because it answers for a rank of lap times and a settings cell is a two-digit
 * number: a 274 px column holds two of these where it holds one time.
 *
 * Wrapping greedily instead gave three columns at 737 px, two at 437 and a ragged last line at
 * both, which rule 20 then grew and made more visible rather than less.
 */
const COLUMNS: Record<Archetype, number> = { wide: 3, grid: 3, tallNarrow: 2, tall: 2 };

/**
 * The cells the catalogue names, at `fs`, in the order it draws them.
 *
 * Exported because the wide car-telemetry page draws this grid beside its pedal traces, from the
 * same definitions and without the car line or the anti-roll bars, which the drawing there does
 * not carry. A second transcription of the same four readings would be the thing that drifts.
 *
 * Five of the catalogue's cells are missing and stay missing: TC slip, TC cut, Diff, Migr. and
 * KERS. Nothing in the repository records an iRacing property for any of them, and a cell bound to
 * a near neighbour is a reading with somebody else's number in it -- the bar's own strip binds its
 * DIFF cell to the rear anti-roll bar, which is exactly that mistake.
 */
export const settingCells = (ctx: ModuleContext, fs: number): FieldSpec[] => [
  presentField(ctx, 'tc', 'TC', tcLevel(), '0', fs, assistPresent(raw('dcTractionControl'), tcLevel())),
  settingField(ctx, 'bb', 'BB', brakeBias(), '0.0', fs, game('BrakeBias')),
  // `Map` is what the catalogue and the bar's own strip call this cell; iRacing publishes the
  // engine map as the mixture.
  settingField(ctx, 'mix', 'Map', fuelMixture(), '0', fs),
  presentField(ctx, 'abs', 'ABS', absLevel(), '0', fs, assistPresent(raw('dcABS'), absLevel())),
];

export const carSettings = defineModule('carSettings', (ctx) => {
  const d = densityOf(ctx.density);
  return stack(
    ctx.frame,
    [
      // One rank rather than two, in the order the catalogue draws them, so that the grid is one
      // block of equal cells: two ranks broke at whatever the first one had room for. The order is
      // the drawing's and not the shedding table's, which is where importance is written.
      fieldsRow(
        [
          ...settingCells(ctx, d.small),
          settingField(ctx, 'arbRear', 'ARB R', antiRollRear(), '0', d.small),
          settingField(ctx, 'arbFront', 'ARB F', antiRollFront(), '0', d.small),
        ],
        ctx,
        { lines: 'grid', columns: COLUMNS[drawnAt(ctx)], gap: SETTING_GAP },
      ),
    ],
    ctx.density,
  );
});
