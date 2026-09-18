/**
 * Module 5, Fuel: what is in the tank, how long it lasts, how many laps it is worth, and what a
 * stop and a lap cost.
 *
 * The lead rank is the tank, the time and the estimated laps, because the estimate is the number a
 * driver reads before deciding whether to stop. It used to sit last, behind three spellings of one
 * consumption.
 *
 * "Refuel" is the laps left times the average consumption, less what is in the tank, and never
 * negative. It is caution amber rather than the low-fuel red because it is an instruction to the
 * crew: the red belongs to the level and to the bar under it, and an instruction drawn in it reads
 * as an alarm about the tank rather than as a figure to act on.
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
  fuelIsSettled,
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

/**
 * A bar drawn under a value, at the four pixels the zone sheet gives it.
 *
 * This used to say four "on the companion as on a zone", and the two sheets do not agree: every bar
 * of `CompanionModules.dc.html` is six pixels and it draws no four-pixel bar anywhere, while
 * `ZoneCatalogue.dc.html` uses both. Four is kept at both densities all the same, since the bar is
 * a reading's own underline rather than a row of its own and the thinner line is what keeps it from
 * reading as one; whether the companion should follow its own sheet to six is the author's, and it
 * is a question about the sheets rather than about this module.
 *
 * The density's `bar` is the six-pixel one that sits beside a label, which is the tyre wear's shape
 * and not this one.
 */
const VALUE_BAR = 4;

export const fuel = defineModule('fuel', (ctx) => {
  const d = densityOf(ctx.density);
  const gaugeHeight = VALUE_BAR;
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          fld(ctx, 'level', 'Fuel', {
            sample: '38.4',
            bind: fmt(fuelLevel(), '0.0'),
            chars: CHARS.fuel,
            fs: d.big,
            colorBind: iff(lowFuel(), str(ds.purpose.fuel.low), str(ds.color.text.primary)),
            follower: { text: 'L', bind: fuelUnit(), widest: 'gal' },
          }),
          fld(ctx, 'time', 'Fuel time', { sample: '0:31:40', bind: clock(fuelTimeLeft()), chars: CHARS.clock, fs: d.big }),
          fld(ctx, 'lapsLeft', 'Est. laps', {
            sample: '11.2',
            bind: iff(fuelIsSettled(), fmt(fuelLapsLeft(), '0.0'), str(NO_VALUE)),
            chars: CHARS.consumption,
            fs: d.big,
            colorBind: iff(lowFuel(), str(ds.purpose.fuel.low), str(ds.color.text.primary)),
          }),
        ],
        ctx,
      ),
      fieldsRow(
        [
          fld(ctx, 'toAdd', 'Refuel', {
            sample: '12.6',
            bind: iff(fuelIsSettled(), fmt(fuelToAdd(), '0.0'), str(NO_VALUE)),
            chars: CHARS.fuel,
            fs: d.mid,
            color: ds.color.caution.primary,
          }),
          fld(ctx, 'average', 'Per lap', { ...consumption(fuelPerLap(), fuelIsSettled()), fs: d.mid }),
          fld(ctx, 'lastLap', 'Last lap', { ...consumption(fuelLastLap(), gt(fuelLastLap(), num(0))), fs: d.mid }),
          fld(ctx, 'thisLap', 'This lap', { ...consumption(fuelThisLap(), gt(fuelThisLap(), num(0))), fs: d.mid }),
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
    // The catalogue draws this page with `justify-content: space-between` at every shape, so the
    // height a zone has to spare goes between the ranks rather than around them.
    { justify: 'spaceBetween' },
  );
});
