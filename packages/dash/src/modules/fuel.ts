/**
 * Module 5, Fuel: what is in the tank, how long it lasts, how many laps it is worth, and what a
 * stop and a lap cost.
 *
 * The lead rank is the tank, the time, the margin to the end of the race and the estimated laps,
 * because those are the numbers a driver reads before deciding whether to stop. The estimate used to
 * sit last, behind three spellings of one consumption, and the shedding table kept it there after
 * the drawing had moved it; `shedding.ts` now declares the fields in the order they are drawn here,
 * and says why that is the order (#334).
 *
 * The margin is the page's one answer rather than another measurement: the estimated laps beside it
 * and the laps left on the session page are the two terms of a subtraction a driver was doing
 * between corners, so it is drawn signed and coloured, green while the tank reaches the flag and red
 * once it does not, and it outranks the estimate wherever a box cannot carry both (#387).
 *
 * "Refuel" is the laps left times the average consumption, less what is in the tank, and never
 * negative. In a timed race the laps left are predicted from the time left and the best lap, and
 * from where the overall leader is on its lap, whose flag a car behind it waits for (#1027); until
 * a lap has been timed there is nothing to multiply, so it draws `--` rather than `0.0` (#1008).
 * It is caution amber rather than the low-fuel red because it is an instruction to the crew: the
 * red belongs to the level and to the bar under it, and an instruction drawn in it reads as an
 * alarm about the tank rather than as a figure to act on.
 */
import { ncalc } from '../generator.ts';
import { rect } from '../design/geometry.ts';
import { densityOf } from '../second/density.ts';
import { drawnFigure } from '../second/drawn.ts';
import { levelGauge } from '../second/gauge.ts';
import { stack } from '../second/layout.ts';
import {
  CHARS,
  clock,
  fuel as fuelLevel,
  fuelLapsLeft,
  fuelLastLap,
  fuelLastLapIsSettled,
  fuelPercent,
  fuelIsSettled,
  fuelPerLap,
  fuelThisLap,
  fuelToAddText,
  fuelToEndColour,
  fuelToEndDrawn,
  fuelToEndText,
  fuelToEndUnit,
  FUEL_TO_END_UNIT_WIDEST,
  FUEL_TO_END_WIDEST,
  fuelUnit,
  NO_VALUE,
  settledFuelTimeLeft,
  tankIsLow,
} from '../second/values.ts';
import { ds } from '../tokens.ts';
import { blockRow, defineModule, fieldsRow, fld } from './module.ts';

const { and, fmt, iff, gt, lt, num, str, eq } = ncalc;

/**
 * The level reads as low under the rig's one low-fuel threshold, `tankIsLow`, which is what the
 * strip, the box, the pop-up and the dash card ask too (#791), and only once a lap has said what one
 * costs. `fuelLapsLeft` reads the unpublished estimate as zero and zero is under any threshold, so
 * before the first crossing a full tank, the bar under it and the `--` the estimate draws for itself
 * were all painted in the low-fuel red, on the row this page had just been brought to one answer on
 * (#382); `tankIsLow` carries the same `fuelIsSettled` gate.
 */
const lowFuel = () => tankIsLow();

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
            // Beside the figure rather than at the end of its four cells: `30.35 L` had the mark a
            // cell out on the VM, and a tank under ten would have had it two. #387.
            drawn: drawnFigure({ value: fuelLevel(), digits: CHARS.fuel.digits - 1, decimals: 1 }),
          }),
          // Behind the consumption gate for the reason the estimate beside it is: SimHub derives
          // `Fuel_RemainingTime` from the per-lap figure, so on the out lap the range moves every
          // frame along with it, and the row would otherwise be an estimate saying it has none
          // beside a range naming one. The gate is on the seconds rather than around the drawing,
          // so the absence keeps the clock's own shape and the field has one spelling of nothing.
          fld(ctx, 'time', 'Fuel time', { sample: '0:31:40', bind: clock(settledFuelTimeLeft()), chars: CHARS.clock, fs: d.big }),
          // The answer, before the working. `Est. laps` and the session page's laps left are the two
          // terms of this subtraction and a driver was doing it himself between corners; the sign is
          // the whole of the reading, so the field is signed and coloured and the unit follows it,
          // a margin in laps and a margin in minutes being the same box on two different grids.
          fld(ctx, 'toEnd', 'Margin', {
            sample: '+1.4',
            bind: fuelToEndText(),
            widest: FUEL_TO_END_WIDEST,
            chars: CHARS.margin,
            fs: d.big,
            colorBind: fuelToEndColour(),
            follower: { text: 'laps', bind: fuelToEndUnit(), widest: FUEL_TO_END_UNIT_WIDEST },
            drawn: fuelToEndDrawn(),
          }),
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
            bind: fuelToAddText(),
            chars: CHARS.fuel,
            fs: d.mid,
            color: ds.color.caution.primary,
          }),
          fld(ctx, 'average', 'Per lap', { ...consumption(fuelPerLap(), fuelIsSettled()), fs: d.mid }),
          fld(ctx, 'lastLap', 'Last lap', { ...consumption(fuelLastLap(), fuelLastLapIsSettled()), fs: d.mid }),
          fld(ctx, 'thisLap', 'This lap', { ...consumption(fuelThisLap(), gt(fuelThisLap(), num(0))), fs: d.mid }),
        ],
        ctx,
      ),
      // Rigid: four pixels of bar under the numerals, with no type in it to hold a hierarchy
      // against, so it takes its height out of the budget rather than stopping the page growing.
      blockRow(
        gaugeHeight,
        (bottom) => [
          levelGauge(`${ctx.prefix}gauge`, rect(ctx.frame.left, bottom - gaugeHeight, ctx.frame.width, gaugeHeight), fuelPercent(), {
            fillBind: iff(lowFuel(), str(ds.purpose.fuel.low), str(ds.color.text.primary)),
            value: 38,
          }),
        ],
        true,
      ),
    ],
    ctx.density,
    // The catalogue draws this page with `justify-content: space-between` at every shape, so the
    // height a zone has to spare goes between the ranks rather than around them.
    { justify: 'spaceBetween' },
  );
});
