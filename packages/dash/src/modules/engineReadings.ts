/**
 * Module 22, Engine readings: the two temperatures, the two pressures, the voltage and the manifold
 * pressure, which is the page an engine dash opens on (#752).
 *
 * SimHub normalises three of the six. `WaterTemperature`, `OilTemperature` and `OilPressure` are on
 * `GameData` for every sim, already converted to the driver's temperature and pressure units. The
 * fuel pressure, the voltage and the manifold pressure have no name there (`StatusDataBase` in the
 * decompiled 9.12.6 GameReaderCommon.dll carries none of them), so they are read from iRacing's own
 * telemetry, in bar and volts, and are no data on every other sim.
 *
 * A `GameData` reading is a double that SimHub leaves at zero for a sim that does not fill it, so a
 * zero there is read as no data rather than drawn, which is what the tyre corners already do: a
 * running engine at exactly 0.00 °C or 0.00 psi is not a reading anybody would act on, whereas a
 * zero drawn for a sim that publishes nothing is a reading somebody would.
 */
import { ncalc } from '../generator.ts';
import { densityOf } from '../second/density.ts';
import { drawnFigure, drawnOr } from '../second/drawn.ts';
import { stack } from '../second/layout.ts';
import { CHARS, NO_VALUE, pressureUnit, temperatureMark } from '../second/values.ts';
import { PSI_PER_BAR } from '../second/wheel.ts';
import { defineModule, drawnAt, fieldsRow, fld } from './module.ts';
import type { Expr } from '../bind.ts';
import type { Chars } from '../design/metrics.ts';
import type { FieldSpec, Follower } from '../second/field.ts';
import type { ModuleContext } from './module.ts';
import type { Archetype } from './shedding.ts';

const { eq, fmt, game, iff, isNull, isnull, mul, num, raw, str } = ncalc;

const KPA_PER_BAR = 100;

/** A pressure iRacing writes in bar, in the unit SimHub draws `OilPressure` in, so that the page has one unit. */
const fromBar = (bar: Expr): Expr => {
  const unit = pressureUnit();
  return iff(eq(unit, str('bar')), bar, iff(eq(unit, str('kPa')), mul(bar, num(KPA_PER_BAR)), mul(bar, num(PSI_PER_BAR))));
};

const temperatureUnitFollower: Follower = { text: '°C', bind: temperatureMark(), widest: '°C' };
const pressureUnitFollower: Follower = { text: 'psi', bind: pressureUnit(), widest: 'kPa' };

/** Same grid as the car settings page, for the same reason: equal cells sharing an x down the block. */
const COLUMNS: Record<Archetype, number> = { wide: 3, grid: 3, tallNarrow: 2, tall: 2 };

const SETTING_GAP = 20;

interface Reading {
  id: string;
  label: string;
  sample: string;
  value: Expr;
  /** True where the sim has nothing to say, which draws `--` in place of the figure. */
  absent: Expr;
  decimals: number;
  chars: Chars;
  follower: Follower;
}

/** A `GameData` reading, absent when it is null or the zero SimHub leaves a field nobody filled at. */
const normalised = (name: string): { value: Expr; absent: Expr } => ({ value: game(name), absent: eq(isnull(game(name), num(0)), num(0)) });

/** An iRacing telemetry variable, absent when the sim does not publish it at all. */
const telemetry = (name: string): { value: Expr; absent: Expr } => ({ value: raw(name), absent: isNull(raw(name)) });

const readingField = (ctx: ModuleContext, r: Reading, fs: number): FieldSpec =>
  fld(ctx, r.id, r.label, {
    sample: r.sample,
    bind: iff(r.absent, str(NO_VALUE), fmt(r.value, r.decimals === 0 ? '0' : `0.${'0'.repeat(r.decimals)}`)),
    chars: r.chars,
    fs,
    follower: r.follower,
    drawn: drawnOr(r.absent, NO_VALUE, drawnFigure({ value: r.value, digits: r.chars.digits - (r.decimals === 0 ? 0 : 1), decimals: r.decimals })),
  });

/** The six, in the order a box too small for all of them keeps them: the manifold pressure goes first. */
const readings = (): Reading[] => {
  const fuelPressure = telemetry('FuelPress');
  const manifold = telemetry('ManifoldPress');
  return [
    { id: 'water', label: 'Water temp', sample: '88', ...normalised('WaterTemperature'), decimals: 0, chars: CHARS.temperature, follower: temperatureUnitFollower },
    { id: 'oilTemp', label: 'Oil temp', sample: '102', ...normalised('OilTemperature'), decimals: 0, chars: CHARS.temperature, follower: temperatureUnitFollower },
    { id: 'oilPressure', label: 'Oil pressure', sample: '79.8', ...normalised('OilPressure'), decimals: 1, chars: CHARS.pressure, follower: pressureUnitFollower },
    { id: 'fuelPressure', label: 'Fuel pressure', sample: '60.9', value: fromBar(fuelPressure.value), absent: fuelPressure.absent, decimals: 1, chars: CHARS.pressure, follower: pressureUnitFollower },
    { id: 'voltage', label: 'Voltage', sample: '13.8', ...telemetry('Voltage'), decimals: 1, chars: CHARS.pressure, follower: { text: 'V' } },
    { id: 'manifold', label: 'Manifold pressure', sample: '14.5', value: fromBar(manifold.value), absent: manifold.absent, decimals: 1, chars: CHARS.pressure, follower: pressureUnitFollower },
  ];
};

export const engineReadings = defineModule('engineReadings', (ctx) => {
  const d = densityOf(ctx.density);
  return stack(
    ctx.frame,
    [fieldsRow(readings().map((r) => readingField(ctx, r, d.small)), ctx, { lines: 'grid', columns: COLUMNS[drawnAt(ctx)], gap: SETTING_GAP })],
    ctx.density,
  );
});
