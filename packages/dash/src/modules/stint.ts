/**
 * Module 18, Stint: how long you have been out, how much you have done, and what the last stop
 * cost. Every one of these is a per-car value on your own row rather than a session member, which
 * is why they all go through the player's leaderboard index.
 *
 * The pit window the design sheet draws is left out: a window is a strategy the plugin does not
 * compute, and a made-up one would be read as advice.
 *
 * The fuel the sheet draws beside the average is left out for the other reason. SimHub publishes
 * the last lap's consumption and the current lap's, and nothing that says how much of the tank
 * this stint has taken; laps since the stop multiplied by the rolling average is an estimate
 * wearing a measurement's label, which is the same objection the window answers to.
 *
 * The three lead values are one size rather than a hero and two peers. A hero is the top of its
 * ramp, so a rank holding one can never grow, and this page is drawn in boxes from 249 x 158 to
 * 437 x 510; rule 20 fills those only if the rank has somewhere left to go.
 */
import { ncalc } from '../generator.ts';
import { densityOf } from '../second/density.ts';
import { stack } from '../second/layout.ts';
import { CHARS, average5, carName, carNumber, clock, player } from '../second/values.ts';
import { defineModule, fieldsRow, fld } from './module.ts';

const { fmt, isnull, num, str, concat, driver, timespanToSeconds, game, ucase } = ncalc;

export const stint = defineModule('stint', (ctx) => {
  const d = densityOf(ctx.density);
  const me = player();
  const stintLaps = isnull(driver('lapsdonesincelastpitout', me), num(0));
  const stintSeconds = timespanToSeconds(isnull(driver('timesincelastpitout', me), num(0)));
  const stops = isnull(driver('pitcount', me), num(0));
  const lastStop = timespanToSeconds(isnull(driver('pitlastduration', me), num(0)));
  const completed = isnull(game('CompletedLaps'), num(0));
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          fld(ctx, 'stintLaps', 'Stint laps', { sample: '12', bind: fmt(stintLaps, '0'), chars: CHARS.position, fs: d.big }),
          fld(ctx, 'stintTime', 'Stint time', { sample: '0:21:40', bind: clock(stintSeconds), chars: CHARS.clock, fs: d.big }),
          fld(ctx, 'completed', 'Laps completed', { sample: '12', bind: fmt(completed, '0'), chars: CHARS.position, fs: d.big }),
        ],
        ctx,
      ),
      fieldsRow(
        [
          fld(ctx, 'stops', 'Stops', { sample: '1', bind: fmt(stops, '0'), chars: CHARS.position, fs: d.mid }),
          fld(ctx, 'lastStop', 'Last stop', { sample: '24.3', bind: fmt(lastStop, '0.0'), chars: CHARS.consumption, fs: d.mid, follower: { text: 's' } }),
        ],
        ctx,
      ),
      fieldsRow(
        [
          // The driver's initials are a proportional label and the car number a monospaced value,
          // rather than one string in digit cells. `YOU · #12` was the latter: the hash overruns
          // a cell cut for digits, and so would an M or a W in somebody's initials.
          fld(ctx, 'driver', 'Driver · YOU', { sample: '12', bind: carNumber(me), chars: CHARS.carNumber, fs: d.small }, {
            labelBind: concat(str('DRIVER · '), ucase(carName(me))),
            labelWidest: 'DRIVER · WWW',
          }),
          fld(ctx, 'avgLap', 'Avg lap', { sample: '1:43.055', bind: average5(), chars: CHARS.lapTime, fs: d.small }),
        ],
        ctx,
      ),
    ],
    ctx.density,
  );
});
