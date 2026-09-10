/**
 * Card 11, Tyre pressures: the four corners from the last stop, one decimal, in SimHub's unit
 * (Psi, Kpa or Bar, upper-cased in the label). Drawn in text.primary as the canvas does; `--` when 0.
 */
import { ncalc } from '../generator.ts';
import { grid2x2, type CellSpec } from '../components/grid2x2.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { PRESSURE_CHARS } from './chars.ts';
import { TYRE_CORNERS } from './tyreTemps.ts';

const { game, isnull, eq, num, iff, str, fmt, concat, ucase } = ncalc;

function cell(corner: (typeof TYRE_CORNERS)[number], sample: string): CellSpec {
  const p = isnull(game(`TyrePressure${corner}`), num(0));
  const noData = eq(p, num(0));
  return {
    sample,
    bind: iff(noData, str('--'), fmt(p, '0.0')),
    chars: PRESSURE_CHARS,
    color: ds.color.text.primary,
    colorBind: iff(noData, str(ds.color.text.dim), str(ds.color.text.primary)),
  };
}

export const tyrePressures = defineCard('tyrePressures', (slot, rung, prefix, meta) =>
  grid2x2(
    slot,
    rung,
    prefix,
    { text: meta.label, bind: concat(str('PRESSURES '), ucase(isnull(game('TyrePressureUnit'), str('Psi'))), str(' · LAST STOP')) },
    [cell('FrontLeft', '27.8'), cell('FrontRight', '28.6'), cell('RearLeft', '27.1'), cell('RearRight', '27.9')],
  ),
);
