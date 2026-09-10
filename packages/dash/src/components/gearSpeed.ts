/**
 * gearSpeed: the gear (Barlow Condensed Bold 260) beside the speed (SemiBold 116) with its unit
 * under it, the pair centred in the hero column as the canvas's flex container does it:
 * gap 24 between them, the block centred horizontally, each part centred vertically.
 * The gear is drawn in its own cell (metrics GEAR_CELL) so that "N" fits: 135 + 24 + 159 = 318.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { cells, gearCells, monoWidth } from '../design/metrics.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { ds } from '../tokens.ts';

const { game, fmt, iff, eq, str } = ncalc;

/** Gap between the speed and its unit on the canvas (not a token). */
export const SPEED_UNIT_GAP = 6;
/** Characters budgeted for the speed. */
export const SPEED_CHARS = { digits: 3, specials: 0 } as const;
/** The gear is one character; SimHub reports "N" and "R" too, drawn in the (letter-wide) gear cell. */
export const GEAR_CHARS = { digits: 1, specials: 0 } as const;

export function gearSpeed(column: Rect, prefix = 'hero'): Item[] {
  const gearFs = ds.size.gear;
  const speedFs = ds.size.hero;
  const unitFs = ds.size.labelSm;
  const gap = ds.space[5];
  const gearMono = gearCells(gearFs);
  const gearWidth = monoWidth(gearMono, GEAR_CHARS);
  const speedWidth = monoWidth(cells('SemiBold', speedFs), SPEED_CHARS);
  const blockWidth = gearWidth + gap + speedWidth;
  const x0 = column.left + (column.width - blockWidth) / 2;
  const gearY = column.top + (column.height - gearFs) / 2;
  const speedBlock = speedFs + SPEED_UNIT_GAP + unitFs;
  const speedY = column.top + (column.height - speedBlock) / 2;
  const speedX = x0 + gearWidth + gap;
  return [
    numeral(`${prefix}.gear`, '4', x0, gearY, gearFs, GEAR_CHARS, { weight: 'Bold', mono: gearMono, bind: game('Gear') }),
    numeral(`${prefix}.speed`, '187', speedX, speedY, speedFs, SPEED_CHARS, { bind: fmt(game('SpeedLocal'), '0') }),
    unit(`${prefix}.speedUnit`, 'KM/H', speedX, speedY + speedFs + SPEED_UNIT_GAP, speedWidth, {
      bind: iff(eq(game('SpeedLocalUnit'), str('MPH')), str('MPH'), str('KM/H')),
    }),
  ];
}
