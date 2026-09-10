/**
 * Card 10, Tyre temps: the four corners from the last stop in car orientation, in SimHub's unit
 * (the label says which), blue below 60 C, red above 100 C, `--` when there is no reading.
 */
import { ncalc } from '../generator.ts';
import { grid2x2, type CellSpec } from '../components/grid2x2.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { TEMP_CHARS } from './chars.ts';

const { game, isnull, eq, lt, gt, num, iff, str, fmt, concat } = ncalc;

/** Cold and hot thresholds, 60 and 100 C, in each unit SimHub can report. */
export const TYRE_THRESHOLDS = {
  Celcius: { cold: 60, hot: 100 },
  Fahrenheit: { cold: 140, hot: 212 },
  Kelvin: { cold: 333, hot: 373 },
} as const;

export const TYRE_CORNERS = ['FrontLeft', 'FrontRight', 'RearLeft', 'RearRight'] as const;

const unitExpr = game('TemperatureUnit');
/** Picks a value per unit, Celsius being the default. */
const perUnit = (f: string, k: string, c: string): string => iff(eq(unitExpr, str('Fahrenheit')), f, iff(eq(unitExpr, str('Kelvin')), k, c));

function cell(corner: (typeof TYRE_CORNERS)[number], sample: string): CellSpec {
  const t = isnull(game(`TyreTemperature${corner}`), num(0));
  const cold = perUnit(num(TYRE_THRESHOLDS.Fahrenheit.cold), num(TYRE_THRESHOLDS.Kelvin.cold), num(TYRE_THRESHOLDS.Celcius.cold));
  const hot = perUnit(num(TYRE_THRESHOLDS.Fahrenheit.hot), num(TYRE_THRESHOLDS.Kelvin.hot), num(TYRE_THRESHOLDS.Celcius.hot));
  const noData = eq(t, num(0));
  return {
    sample,
    bind: iff(noData, str('--'), fmt(t, '0')),
    chars: TEMP_CHARS,
    color: ds.purpose.tyre.nominal,
    colorBind: iff(noData, str(ds.color.text.dim), iff(lt(t, cold), str(ds.purpose.tyre.cold), iff(gt(t, hot), str(ds.purpose.tyre.hot), str(ds.purpose.tyre.nominal)))),
  };
}

export const tyreTemps = defineCard('tyreTemps', (slot, rung, prefix, meta) =>
  grid2x2(
    slot,
    rung,
    prefix,
    { text: meta.label, bind: concat(str('TYRES '), perUnit(str('°F'), str('K'), str('°C')), str(' · LAST STOP')) },
    [cell('FrontLeft', '84'), cell('FrontRight', '104'), cell('RearLeft', '62'), cell('RearRight', '88')],
  ),
);
