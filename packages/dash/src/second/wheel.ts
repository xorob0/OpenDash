/**
 * One corner of the tyre module: the reading a driver acts on (temperature), the one they check
 * (pressure), how much tread is left (a bar behind the numbers) and the compound.
 *
 * iRacing reports the pressure the car left the pit box with, not a live one, which is why the
 * module's footer says "last stop"; the temperature and the wear are live.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { canvasBaseline, canvasYForBaseline, cells, monoWidth } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { ds } from '../tokens.ts';
import { barGauge } from './gauge.ts';
import { COMPOUND_CHARS, COMPOUND_WIDEST, chipText } from './chip.ts';
import { densityOf, type Density } from './density.ts';
import { CHARS, tyreChangeScheduled, tyrePressure, tyreTemperature, tyreWear, type Corner } from './values.ts';

const { iff, eq, lt, gt, str, num, fmt, isnull, game } = ncalc;

/** Tread left below this percentage is drawn in caution. */
export const WEAR_CAUTION = 65;

/** Cold and hot thresholds in each unit SimHub can report, as on the dash's tyre card. */
export const TYRE_THRESHOLDS = {
  Celcius: { cold: 60, hot: 100 },
  Fahrenheit: { cold: 140, hot: 212 },
  Kelvin: { cold: 333, hot: 373 },
} as const;

const unitExpr = game('TemperatureUnit');
const perUnit = (f: string, k: string, c: string): string => iff(eq(unitExpr, str('Fahrenheit')), f, iff(eq(unitExpr, str('Kelvin')), k, c));

/** Width of the wear bar and the gap between it and the numbers. */
export const WEAR_BAR = { width: 6, gap: 10 } as const;

/** The temperature's colour: blue when cold, red when hot, white in between, dim with no reading. */
export function temperatureColour(corner: Corner): string {
  const t = tyreTemperature(corner);
  const cold = perUnit(num(TYRE_THRESHOLDS.Fahrenheit.cold), num(TYRE_THRESHOLDS.Kelvin.cold), num(TYRE_THRESHOLDS.Celcius.cold));
  const hot = perUnit(num(TYRE_THRESHOLDS.Fahrenheit.hot), num(TYRE_THRESHOLDS.Kelvin.hot), num(TYRE_THRESHOLDS.Celcius.hot));
  return iff(eq(t, num(0)), str(ds.color.text.dim), iff(lt(t, cold), str(ds.purpose.tyre.cold), iff(gt(t, hot), str(ds.purpose.tyre.hot), str(ds.purpose.tyre.nominal))));
}

/** Design-time readings per corner, so the editor shows four wheels rather than the same one four times. */
export const CORNER_SAMPLES: Record<Corner, { temperature: string; pressure: string }> = {
  FrontLeft: { temperature: '84', pressure: '27.8' },
  FrontRight: { temperature: '104', pressure: '28.6' },
  RearLeft: { temperature: '62', pressure: '27.1' },
  RearRight: { temperature: '88', pressure: '27.9' },
};

/** One wheel drawn in `frame`. */
export function wheel(name: string, frame: Rect, corner: Corner, density: Density): Item[] {
  const d = densityOf(density);
  const temp = tyreTemperature(corner);
  const pressure = tyrePressure(corner);
  const wear = tyreWear(corner);
  const tempFs = d.big;
  const pressureFs = d.tiny;
  const secondFs = d.labelSm;
  const blockHeight = tempFs + d.fieldGap + secondFs;
  const top = frame.top + Math.max(0, (frame.height - blockHeight) / 2);
  const x = frame.left + WEAR_BAR.width + WEAR_BAR.gap;
  const tempWidth = monoWidth(cells('SemiBold', tempFs), CHARS.temperature);
  const pressureX = x + tempWidth + d.fieldGap * 2;
  const baseline = canvasBaseline(top, tempFs);
  const items: Item[] = [
    band(`${name}.wear.track`, rect(frame.left, frame.top, WEAR_BAR.width, frame.height), ds.color.surface.raised),
    barGauge(`${name}.wear`, rect(frame.left, frame.top, WEAR_BAR.width, frame.height), wear, {
      fill: ds.color.text.primary,
      fillBind: iff(lt(wear, num(WEAR_CAUTION)), str(ds.purpose.fuel.low), str(ds.color.text.primary)),
      max: 100,
    }),
    numeral(`${name}.temp`, CORNER_SAMPLES[corner].temperature, x, top, tempFs, CHARS.temperature, {
      bind: iff(eq(temp, num(0)), str('--'), fmt(temp, '0')),
      colorBind: temperatureColour(corner),
      maxWidth: frame.width - (x - frame.left),
    }),
    numeral(`${name}.pressure`, CORNER_SAMPLES[corner].pressure, pressureX, canvasYForBaseline(baseline, pressureFs), pressureFs, CHARS.pressure, {
      bind: iff(eq(pressure, num(0)), str('--'), fmt(pressure, '0.0')),
      color: ds.color.text.secondary,
      maxWidth: Math.max(0, frame.left + frame.width - pressureX),
    }),
    // The compound is cut to one letter, as on the leaderboard's tyre column: a compound is H, M,
    // S or W, and a binding that can draw a whole word into a box built for "M" is the clipping
    // fault this file's box measurements exist to prevent.
    label(`${name}.compound`, 'M', x, top + tempFs + d.fieldGap, 40, {
      size: secondFs,
      color: ds.color.text.secondary,
      bind: chipText(isnull(ncalc.driver('fronttyrecompound', ncalc.playerPosition()), str('')), COMPOUND_CHARS),
      widest: COMPOUND_WIDEST,
    }),
  ];
  // A 6 px square marks a wheel the pit box is set to change: SimHub cannot draw the tick the
  // design sheet uses, and a square in the same place reads the same way.
  const markerX = x + 40;
  items.push({
    ...band(`${name}.change`, rect(markerX, top + tempFs + d.fieldGap + Math.max(0, (secondFs - 6) / 2), 6, 6), ds.color.text.primary),
    ...withBindings({ Visible: tyreChangeScheduled(corner) }),
  });
  items.push(
    unit(`${name}.changeLabel`, 'CHG', markerX + 10, top + tempFs + d.fieldGap, 34, {
      visibleBind: tyreChangeScheduled(corner),
    }),
  );
  return items;
}
