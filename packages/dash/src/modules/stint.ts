/**
 * Module 18, Stint: where you are in the race, how long you have been out, how much longer the fuel
 * lasts, and what the last stop cost.
 *
 * **The lead rank answers "how much of this race is left", from three directions.** The lap out of
 * the estimated total is the one a driver says out loud; the fuel time is how long the tank lasts
 * whatever the clock says; and the stint time is how long this run has been. A page about a stint
 * that could not say how far through the race it was is the gap testing found.
 *
 * The driver's name and car number used to sit at the foot of it and no longer do. A number drawn
 * as a full field is a fact about somebody you already know the identity of -- yourself -- and it
 * was taking a cell from figures that change. It stays where it earns its place, which is a list of
 * other people's cars.
 *
 * The pit window the design sheet draws is left out: a window is a strategy the plugin does not
 * compute, and a made-up one would be read as advice.
 *
 * The fuel the sheet draws beside the average is left out for the other reason. SimHub publishes
 * the last lap's consumption and the current lap's, and nothing that says how much of the tank
 * this stint has taken; laps since the stop multiplied by the rolling average is an estimate
 * wearing a measurement's label, which is the same objection the window answers to.
 *
 * The three lead values are one size rather than a hero and two peers. On the companion and the
 * pit wall a hero is the top of its ramp, so a rank holding one can never grow there, and this page
 * is drawn in boxes from 249 x 158 to 437 x 510. On a tall face the three are promoted together
 * instead, which is the catalogue's own `tall` drawing, 76 over a 34 px rank; see `leadRankSize`.
 */
import { ncalc } from '../generator.ts';
import { densityOf } from '../second/density.ts';
import { stack } from '../second/layout.ts';
import { drawnFigure } from '../second/drawn.ts';
import { CHARS, average5, clock, LAP_TIME_WIDEST, lapOfTotal, player, settledFuelTimeLeft } from '../second/values.ts';
import { defineModule, fieldsRow, fld, leadRankSize } from './module.ts';

const { fmt, isnull, num, driver, timespanToSeconds, game } = ncalc;

export const stint = defineModule('stint', (ctx) => {
  const d = densityOf(ctx.density);
  const me = player();
  const stintLaps = isnull(driver('lapsdonesincelastpitout', me), num(0));
  const stintSeconds = timespanToSeconds(isnull(driver('timesincelastpitout', me), num(0)));
  const stops = isnull(driver('pitcount', me), num(0));
  const lastStop = timespanToSeconds(isnull(driver('pitlastduration', me), num(0)));
  const completed = isnull(game('CompletedLaps'), num(0));
  const lead = leadRankSize(ctx);
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          fld(ctx, 'lap', 'Lap', { sample: '12 / 43', bind: lapOfTotal(), chars: CHARS.lapOfTotal, fs: lead }),
          // Gated on a completed lap for the reason `fuelIsSettled` gives: before one, SimHub is
          // extrapolating a partial lap and this clock runs backwards and forwards as you drive.
          // The gate is on the seconds, so an unsettled range reads `-:--:--` as the stint time
          // beside it does when it has nothing to count, rather than `--` in a row of clocks.
          fld(ctx, 'fuelTime', 'Fuel time', { sample: '0:31:40', bind: clock(settledFuelTimeLeft()), chars: CHARS.clock, fs: lead }),
          fld(ctx, 'stintTime', 'Stint time', { sample: '0:21:40', bind: clock(stintSeconds), chars: CHARS.clock, fs: lead }),
        ],
        ctx,
      ),
      fieldsRow(
        [
          fld(ctx, 'stintLaps', 'Stint laps', { sample: '12', bind: fmt(stintLaps, '0'), chars: CHARS.position, fs: d.mid }),
          fld(ctx, 'completed', 'Laps completed', { sample: '12', bind: fmt(completed, '0'), chars: CHARS.position, fs: d.mid }),
          fld(ctx, 'stops', 'Stops', { sample: '1', bind: fmt(stops, '0'), chars: CHARS.position, fs: d.mid }),
          fld(ctx, 'lastStop', 'Last stop', {
            sample: '24.3',
            bind: fmt(lastStop, '0.0'),
            chars: CHARS.consumption,
            fs: d.mid,
            follower: { text: 's' },
            drawn: drawnFigure({ value: lastStop, digits: CHARS.consumption.digits - 1, decimals: 1 }),
          }),
        ],
        ctx,
      ),
      fieldsRow(
        [fld(ctx, 'avgLap', 'Avg lap', { sample: '1:43.055', bind: average5(), chars: CHARS.lapTime, widest: LAP_TIME_WIDEST, fs: d.small })],
        ctx,
      ),
    ],
    ctx.density,
  );
});
