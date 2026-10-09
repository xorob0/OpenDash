/**
 * Shared value of the TC and ABS cards and the pit wall's TC and ABS cells: the level as an
 * integer, OFF at zero in assist.off, and `--` in assist.none when the car has no such system.
 *
 * "Has no such system" is `assistPresent`, the one test the bar's strip, the pit wall and the car
 * settings page hide a cell by: the iRacing knob, or a level above zero. It was the knob alone here,
 * so a car with fixed traction control, or any car on a sim that publishes no `dc*` knobs, drew `--`
 * over a level the strip beside it was drawing.
 *
 * The pit wall read the level a second time with its own `fmt`, so a car with TC switched off read
 * `0` there and `OFF` on the card. One body, so a reading has one spelling wherever it is drawn.
 */
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import type { ValueSpec } from '../components/readout.ts';
import { assistPresent } from '../second/tracked.ts';
import { absLevel, tcLevel } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { ASSIST_CHARS } from './chars.ts';

const { isnull, eq, num, iff, not, raw, str, fmt } = ncalc;

/** The two assists a car can publish, by SimHub's normalised level and iRacing's in-car knob. */
export const ASSISTS = {
  tc: { knob: 'dcTractionControl', level: tcLevel },
  abs: { knob: 'dcABS', level: absLevel },
} as const;

export type Assist = keyof typeof ASSISTS;

/** The car has this assist: {@link assistPresent} over its knob and its level. */
export const hasAssist = (assist: Assist): Expr => assistPresent(raw(ASSISTS[assist].knob), ASSISTS[assist].level());

export function assistValue(assist: Assist, sample: string): ValueSpec {
  const level = isnull(ASSISTS[assist].level(), num(0));
  const none = not(hasAssist(assist));
  const off = eq(level, num(0));
  return {
    sample,
    bind: iff(none, str('--'), iff(off, str('OFF'), fmt(level, '0'))),
    chars: ASSIST_CHARS,
    // The widest of what the binding draws, in every face it is set in: wider than `--` and than
    // any two-digit level.
    widest: 'OFF',
    colorBind: iff(none, str(ds.purpose.assist.none), iff(off, str(ds.purpose.assist.off), str(ds.color.text.primary))),
  };
}
