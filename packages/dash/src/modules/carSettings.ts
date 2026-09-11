/**
 * Module 9, Car settings: the driver-adjustable values. A field whose property the sim does not
 * publish is hidden rather than shown as `--`: the grid should say what this car has, not list
 * what this sim lacks.
 */
import { ncalc } from '../generator.ts';
import { densityOf } from '../second/density.ts';
import { stack } from '../second/layout.ts';
import { CHARS, absLevel, antiRollFront, antiRollRear, brakeBias, carModel, carNumber, fuelMixture, player, tcLevel } from '../second/values.ts';
import { defineModule, fieldsRow, fld } from './module.ts';
import type { FieldSpec } from '../second/field.ts';
import type { ModuleContext } from './module.ts';

const { fmt, isNull, not, concat, str } = ncalc;

/** A setting field that disappears when the sim does not publish the property behind it. */
const settingField = (ctx: ModuleContext, id: string, label: string, expr: string, pattern: string, fs: number): FieldSpec =>
  fld(ctx, id, label, { sample: '3', bind: fmt(expr, pattern), chars: CHARS.setting, fs }, { visibleBind: not(isNull(expr)) });

export const carSettings = defineModule('carSettings', (ctx) => {
  const d = densityOf(ctx.density);
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          fld(ctx, 'car', 'Car', {
            sample: 'GT3 · #12',
            bind: concat(carModel(), str(' · '), carNumber(player())),
            chars: CHARS.classPosition,
            fs: d.mid,
          }),
        ],
        ctx,
      ),
      fieldsRow(
        [
          settingField(ctx, 'tc', 'TC', tcLevel(), '0', d.small),
          settingField(ctx, 'abs', 'ABS', absLevel(), '0', d.small),
          settingField(ctx, 'bb', 'BB', brakeBias(), '0.0', d.small),
          settingField(ctx, 'mix', 'Mix', fuelMixture(), '0', d.small),
        ],
        ctx,
      ),
      fieldsRow(
        [
          settingField(ctx, 'arbFront', 'ARB F', antiRollFront(), '0', d.small),
          settingField(ctx, 'arbRear', 'ARB R', antiRollRear(), '0', d.small),
        ],
        ctx,
      ),
    ],
    ctx.density,
  );
});
