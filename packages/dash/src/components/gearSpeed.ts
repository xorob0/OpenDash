/**
 * The gear and speed pair, in three arrangements:
 * - gearSpeedRow: the gear (Barlow Condensed Bold) beside the speed (SemiBold) with its unit
 *   under it, the pair centred in the hero column as the canvas's flex container does it: a gap
 *   between them, the block centred horizontally, each part centred vertically. Sizes 260 / 116
 *   with a 24 gap everywhere but the nano (180 / 64, gap 20).
 * - gearSpeedBand: the row in a full-width band (DisplayDash), the same geometry.
 * - gearSpeedStack (480 round): the gear centred, then a row of speed 64 and its unit centred
 *   under it, 4 px apart, the stack centred in the given rect.
 * The gear is drawn in its own cell (metrics GEAR_CELL) so that "N" fits: 135 + 24 + 159 = 318.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { canvasBaseline, canvasYForBaseline, cells, gearCells, monoWidth, UNIT_KMH_EM } from '../design/metrics.ts';
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

export interface GearSpeedSizes {
  /** Gear font size. */
  gear: number;
  /** Speed font size. */
  speed: number;
  /** Gap between the gear and the speed block. */
  gap: number;
}

export const GEAR_SPEED_SIZES = {
  standard: { gear: ds.size.gear, speed: ds.size.hero, gap: ds.space[5] },
  /** The nano's hero: the canvas draws 180 / 64 with a 20 gap, neither of which is a token. */
  nano: { gear: ds.size.gearSm, speed: 64, gap: 20 },
} as const satisfies Record<string, GearSpeedSizes>;

/** Speed size of the stacked variant, from the round canvas (not a token). */
export const STACK_SPEED_SIZE = 64;
/** Width budgeted for the unit in the stacked row: "KM/H" in Barlow Medium 13, the wider of the two units. */
export const STACK_UNIT_WIDTH = Math.ceil(UNIT_KMH_EM * ds.size.labelSm);

const speedUnitBind = iff(eq(game('SpeedLocalUnit'), str('MPH')), str('MPH'), str('KM/H'));

export function gearSpeedRow(column: Rect, sizes: GearSpeedSizes = GEAR_SPEED_SIZES.standard, prefix = 'hero'): Item[] {
  const gearFs = sizes.gear;
  const speedFs = sizes.speed;
  const unitFs = ds.size.labelSm;
  const gap = sizes.gap;
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
    unit(`${prefix}.speedUnit`, 'KM/H', speedX, speedY + speedFs + SPEED_UNIT_GAP, speedWidth, { bind: speedUnitBind }),
  ];
}

/** The row in a full-width band: the DisplayDash hero. Same geometry, centred in the band. */
export function gearSpeedBand(band: Rect, sizes: GearSpeedSizes = GEAR_SPEED_SIZES.standard, prefix = 'hero'): Item[] {
  return gearSpeedRow(band, sizes, prefix);
}

/** The gear over a row of speed and unit, the stack centred in `frame`: the 480 round hero. */
export function gearSpeedStack(frame: Rect, prefix = 'hero'): Item[] {
  const gearFs = ds.size.gear;
  const speedFs = STACK_SPEED_SIZE;
  const unitFs = ds.size.labelSm;
  const stackGap = ds.space[1];
  const rowGap = ds.space[2];
  const gearMono = gearCells(gearFs);
  const gearWidth = monoWidth(gearMono, GEAR_CHARS);
  const speedWidth = monoWidth(cells('SemiBold', speedFs), SPEED_CHARS);
  const rowWidth = speedWidth + rowGap + STACK_UNIT_WIDTH;
  const top = frame.top + (frame.height - (gearFs + stackGap + speedFs)) / 2;
  const gearX = frame.left + (frame.width - gearWidth) / 2;
  const rowY = top + gearFs + stackGap;
  const speedX = frame.left + (frame.width - rowWidth) / 2;
  const unitX = speedX + speedWidth + rowGap;
  const unitY = canvasYForBaseline(canvasBaseline(rowY, speedFs), unitFs);
  return [
    numeral(`${prefix}.gear`, '4', gearX, top, gearFs, GEAR_CHARS, { weight: 'Bold', mono: gearMono, bind: game('Gear') }),
    numeral(`${prefix}.speed`, '187', speedX, rowY, speedFs, SPEED_CHARS, { bind: fmt(game('SpeedLocal'), '0') }),
    unit(`${prefix}.speedUnit`, 'KM/H', unitX, unitY, STACK_UNIT_WIDTH, { bind: speedUnitBind }),
  ];
}
