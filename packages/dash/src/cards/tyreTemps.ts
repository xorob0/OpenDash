/**
 * Card 10, Tyre temps: the four corners from the last stop in car orientation, in SimHub's unit
 * (the label says which), blue below 60 C, red above 100 C, `--` when there is no reading.
 */
import { ncalc } from '../generator.ts';
import { cardFrame } from '../components/frame.ts';
import { grid2x2, type CellSpec } from '../components/grid2x2.ts';
import { fitLabelForm } from '../elements/label.ts';
import { TYRE_THRESHOLDS } from '../second/wheel.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { TEMP_CHARS } from './chars.ts';

const { game, isnull, eq, lt, gt, num, iff, str, fmt, concat } = ncalc;

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

/** The unit in the label; "°F" is the widest, so it is what the forms are measured by. */
const labelUnit = perUnit(str('°F'), str('K'), str('°C'));

/** Longest first: the stop note goes when the card is too narrow to hold it. */
const labelForms = (label: string) => [
  { sample: label, widest: 'TYRES °F · LAST STOP', bind: concat(str('TYRES '), labelUnit, str(' · LAST STOP')) },
  { sample: 'TYRES °C', widest: 'TYRES °F', bind: concat(str('TYRES '), labelUnit) },
];

export const tyreTemps = defineCard('tyreTemps', (slot, rung, prefix, meta) => {
  const form = fitLabelForm(labelForms(meta.label), cardFrame(slot, rung).innerWidth);
  return grid2x2(
    slot,
    rung,
    prefix,
    { text: form.sample, bind: form.bind },
    [cell('FrontLeft', '84'), cell('FrontRight', '104'), cell('RearLeft', '62'), cell('RearRight', '88')],
  );
});
