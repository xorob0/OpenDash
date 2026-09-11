/**
 * Module 5, Fuel: what is in the tank, how long it lasts, what to add, and what each lap costs.
 *
 * "To add" is the laps left times the average consumption, less what is in the tank, and never
 * negative. It is amber because it is an instruction to the crew rather than a reading.
 */
import { ncalc } from '../generator.ts';
import { rect } from '../design/geometry.ts';
import { densityOf } from '../second/density.ts';
import { levelGauge } from '../second/gauge.ts';
import { stack } from '../second/layout.ts';
import {
  CHARS,
  clock,
  fuel as fuelLevel,
  fuelLapsLeft,
  fuelLastLap,
  fuelPercent,
  fuelPerLap,
  fuelThisLap,
  fuelTimeLeft,
  fuelToAdd,
  fuelUnit,
  NO_VALUE,
} from '../second/values.ts';
import { ds } from '../tokens.ts';
import { blockRow, defineModule, fieldsRow, fld } from './module.ts';

const { fmt, iff, gt, lt, num, str, eq } = ncalc;

/** Below a lap of fuel the level reads as low, which is the dash card's rule too. */
const lowFuel = () => lt(fuelLapsLeft(), num(1));

const consumption = (value: string, guard: string) => ({ sample: '2.84', bind: iff(guard, fmt(value, '0.00'), str(NO_VALUE)), chars: CHARS.consumption });

export const fuel = defineModule('fuel', (ctx) => {
  const d = densityOf(ctx.density);
  const gaugeHeight = d.bar;
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          fld(ctx, 'level', 'Fuel', {
            sample: '38.4',
            bind: fmt(fuelLevel(), '0.0'),
            chars: CHARS.fuel,
            fs: d.hero,
            colorBind: iff(lowFuel(), str(ds.purpose.fuel.low), str(ds.color.text.primary)),
            follower: { text: 'L', bind: fuelUnit() },
          }),
          fld(ctx, 'time', 'Fuel time', { sample: '0:31:40', bind: clock(fuelTimeLeft()), chars: CHARS.clock, fs: d.mid }),
          fld(ctx, 'toAdd', 'To add', {
            sample: '12.6',
            bind: iff(gt(fuelPerLap(), num(0)), fmt(fuelToAdd(), '0.0'), str(NO_VALUE)),
            chars: CHARS.fuel,
            fs: d.mid,
            color: ds.purpose.fuel.low,
          }),
        ],
        ctx,
      ),
      fieldsRow(
        [
          fld(ctx, 'lastLap', 'Last lap', { ...consumption(fuelLastLap(), gt(fuelLastLap(), num(0))), fs: d.small }),
          fld(ctx, 'thisLap', 'This lap', { ...consumption(fuelThisLap(), gt(fuelThisLap(), num(0))), fs: d.small }),
          fld(ctx, 'average', 'Average', { ...consumption(fuelPerLap(), gt(fuelPerLap(), num(0))), fs: d.small }),
          fld(ctx, 'lapsLeft', 'Est. laps', {
            sample: '11.2',
            bind: iff(gt(fuelPerLap(), num(0)), fmt(fuelLapsLeft(), '0.0'), str(NO_VALUE)),
            chars: CHARS.consumption,
            fs: d.small,
            colorBind: iff(lowFuel(), str(ds.purpose.fuel.low), str(ds.color.text.primary)),
          }),
        ],
        ctx,
      ),
      blockRow(gaugeHeight, (bottom) =>
        [
          levelGauge(`${ctx.prefix}gauge`, rect(ctx.frame.left, bottom - gaugeHeight, ctx.frame.width, gaugeHeight), fuelPercent(), {
            fillBind: iff(lowFuel(), str(ds.purpose.fuel.low), str(ds.color.text.primary)),
            value: 38,
          }),
        ],
      ),
    ],
    ctx.density,
  );
});
