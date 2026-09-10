/**
 * Card 11, Tyre pressures: the four corners from the last stop in SimHub's unit (Psi, Kpa or
 * Bar, upper-cased in the label), drawn in text.primary as the canvas does; `--` when 0.
 * One decimal wherever the grid column holds `ddd.d`. Where it does not, the widest values give
 * up their decimal first: integers from 100 (kPa) when only `dd.d` fits, integers from 10 (psi
 * and kPa, bar keeping its decimal) when only `ddd` fits. The tier is picked per layout from the
 * column width, so every cell fits its column and never draws over its neighbour.
 */
import type { Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { grid2x2, gridColumnWidth, type CellSpec } from '../components/grid2x2.ts';
import { cells, monoWidth, type Chars } from '../design/metrics.ts';
import type { RungSpec } from '../design/rung.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { PRESSURE_CHARS } from './chars.ts';
import { TYRE_CORNERS } from './tyreTemps.ts';

const { game, isnull, eq, lt, num, iff, str, fmt, concat, ucase } = ncalc;

export interface PressureTier {
  /** Values from here up are shown as integers; below it they keep one decimal. */
  integerFrom: number;
  /** The widest value of the tier: `ddd.d`, `dd.d` (or `ddd`), `ddd` (or `d.d`). */
  chars: Chars;
}

/** From the most precise down. The last tier is the fallback when nothing fits; the layout tests catch that. */
export const PRESSURE_TIERS: readonly PressureTier[] = [
  { integerFrom: Number.POSITIVE_INFINITY, chars: PRESSURE_CHARS },
  { integerFrom: 100, chars: { digits: 3, specials: 1 } },
  { integerFrom: 10, chars: { digits: 3, specials: 0 } },
];

/** The most precise tier whose widest value fits a grid column of `colWidth` px at the grid size `fs`. */
export function pressureTier(colWidth: number, fs: number): PressureTier {
  const mono = cells('SemiBold', fs);
  return PRESSURE_TIERS.find((t) => monoWidth(mono, t.chars) <= colWidth) ?? PRESSURE_TIERS[PRESSURE_TIERS.length - 1]!;
}

/** The tier of the pressures card in `slot` at `rung`. */
export const pressureTierFor = (slot: Rect, rung: RungSpec): PressureTier => pressureTier(gridColumnWidth(slot, rung), rung.grid);

/** `format(p, '0.0')`, or an integer from the tier's threshold up. */
export function pressureFormat(p: Expr, tier: PressureTier): Expr {
  return Number.isFinite(tier.integerFrom) ? iff(lt(p, num(tier.integerFrom)), fmt(p, '0.0'), fmt(p, '0')) : fmt(p, '0.0');
}

/** The design-time sample of a value under a tier. */
const sampleText = (value: number, tier: PressureTier): string => (value >= tier.integerFrom ? String(Math.round(value)) : value.toFixed(1));

function cell(corner: (typeof TYRE_CORNERS)[number], sample: number, tier: PressureTier): CellSpec {
  const p = isnull(game(`TyrePressure${corner}`), num(0));
  const noData = eq(p, num(0));
  return {
    sample: sampleText(sample, tier),
    bind: iff(noData, str('--'), pressureFormat(p, tier)),
    chars: tier.chars,
    color: ds.color.text.primary,
    colorBind: iff(noData, str(ds.color.text.dim), str(ds.color.text.primary)),
  };
}

export const tyrePressures = defineCard('tyrePressures', (slot, rung, prefix, meta) => {
  const tier = pressureTierFor(slot, rung);
  return grid2x2(
    slot,
    rung,
    prefix,
    { text: meta.label, bind: concat(str('PRESSURES '), ucase(isnull(game('TyrePressureUnit'), str('Psi'))), str(' · LAST STOP')) },
    [cell('FrontLeft', 27.8, tier), cell('FrontRight', 28.6, tier), cell('RearLeft', 27.1, tier), cell('RearRight', 27.9, tier)],
  );
});
