/**
 * Card 10, Tyre temps: the four corners from the last stop in car orientation, in SimHub's unit
 * (the label says which), blue below 60 C, red above 100 C, `--` when there is no reading.
 *
 * At rung L the tread left follows each temperature on a second line, which is what the canvas
 * draws; a shorter slot keeps the plain numerals. The tyre drawing beside them waits on an image
 * item reaching this package.
 */
import { ncalc } from '../generator.ts';
import { cardFrame } from '../components/frame.ts';
import { grid2x2, type CellSpec } from '../components/grid2x2.ts';
import { fitLabelForm } from '../elements/label.ts';
import { TYRE_THRESHOLDS, WEAR_CAUTION } from '../second/wheel.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { TEMP_CHARS } from './chars.ts';

const { game, isnull, eq, lt, gt, num, iff, str, fmt, concat } = ncalc;

export const TYRE_CORNERS = ['FrontLeft', 'FrontRight', 'RearLeft', 'RearRight'] as const;

const unitExpr = game('TemperatureUnit');
/** Picks a value per unit, Celsius being the default. */
const perUnit = (f: string, k: string, c: string): string => iff(eq(unitExpr, str('Fahrenheit')), f, iff(eq(unitExpr, str('Kelvin')), k, c));

/** `100` per cent of the tread; the sign after it is a label, not a cell. */
const WEAR_CHARS = { digits: 3, specials: 0 } as const;

function cell(corner: (typeof TYRE_CORNERS)[number], sample: string, wearSample: string): CellSpec {
  const t = isnull(game(`TyreTemperature${corner}`), num(0));
  const cold = perUnit(num(TYRE_THRESHOLDS.Fahrenheit.cold), num(TYRE_THRESHOLDS.Kelvin.cold), num(TYRE_THRESHOLDS.Celcius.cold));
  const hot = perUnit(num(TYRE_THRESHOLDS.Fahrenheit.hot), num(TYRE_THRESHOLDS.Kelvin.hot), num(TYRE_THRESHOLDS.Celcius.hot));
  const noData = eq(t, num(0));
  const wear = isnull(game(`TyreWear${corner}`), num(0));
  return {
    sample,
    bind: iff(noData, str('--'), fmt(t, '0')),
    chars: TEMP_CHARS,
    color: ds.purpose.tyre.nominal,
    colorBind: iff(noData, str(ds.color.text.dim), iff(lt(t, cold), str(ds.purpose.tyre.cold), iff(gt(t, hot), str(ds.purpose.tyre.hot), str(ds.purpose.tyre.nominal)))),
    sub: {
      sample: wearSample,
      bind: iff(eq(wear, num(0)), str('--'), fmt(wear, '0')),
      chars: WEAR_CHARS,
      unit: '%',
      // Dim with no reading rather than amber, as the temperature above it is: a missing figure is
      // zero here, and amber would read as a tyre worn out.
      colorBind: iff(eq(wear, num(0)), str(ds.color.text.dim), iff(lt(wear, num(WEAR_CAUTION)), str(ds.color.caution.primary), str(ds.color.text.secondary))),
    },
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
    [cell('FrontLeft', '84', '79'), cell('FrontRight', '104', '61'), cell('RearLeft', '62', '86'), cell('RearRight', '88', '58')],
  );
});
