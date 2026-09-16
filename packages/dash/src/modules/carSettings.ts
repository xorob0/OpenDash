/**
 * Module 9, Car settings: the driver-adjustable values. A field whose property the sim does not
 * publish is hidden rather than shown as `--`: the grid should say what this car has, not list
 * what this sim lacks.
 */
import { ncalc } from '../generator.ts';
import { densityOf } from '../second/density.ts';
import { stack } from '../second/layout.ts';
import { CHARS, absLevel, antiRollFront, antiRollRear, brakeBias, carModel, carNumber, fuelMixture, player, tcLevel } from '../second/values.ts';
import { defineModule, drawnAt, fieldsRow, fld } from './module.ts';
import type { FieldSpec } from '../second/field.ts';
import type { ModuleContext } from './module.ts';
import type { Archetype } from './shedding.ts';

const { fmt, isNull, not, concat, str, ucase, raw, game } = ncalc;

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
  settingField(ctx, 'tc', 'TC', tcLevel(), '0', fs, raw('dcTractionControl')),
  settingField(ctx, 'bb', 'BB', brakeBias(), '0.0', fs, game('BrakeBias')),
  // `Map` is what the catalogue and the bar's own strip call this cell; iRacing publishes the
  // engine map as the mixture.
  settingField(ctx, 'mix', 'Map', fuelMixture(), '0', fs),
  settingField(ctx, 'abs', 'ABS', absLevel(), '0', fs, raw('dcABS')),
];

export const carSettings = defineModule('carSettings', (ctx) => {
  const d = densityOf(ctx.density);
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          // The model is a proportional label and the number a monospaced value. As one string it
          // was `GT3 · #12` in cells cut for digits, where the hash clipped and an M or a W in a
          // car's name would have done the same.
          fld(
            ctx,
            'car',
            'Car · GT3',
            { sample: '12', bind: carNumber(player()), chars: CHARS.carNumber, fs: d.mid },
            { labelBind: concat(str('CAR · '), ucase(carModel())), labelWidest: 'CAR · WWWWWWWWWW' },
          ),
        ],
        ctx,
      ),
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
