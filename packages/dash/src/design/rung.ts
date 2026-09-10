/**
 * Card rungs. A card picks its rung from the slot it lands in, never from its content:
 * L when the slot is at least 250 wide and 150 tall, M from 180 wide, S below.
 */
import type { Rect } from '../generator.ts';
import { ds } from '../tokens.ts';

export type Rung = 'L' | 'M' | 'S';

/** Card padding: top and bottom, left and right. */
export interface Padding {
  y: number;
  x: number;
}

export interface RungSpec {
  rung: Rung;
  /** Card value font size. */
  value: number;
  /** Denominator font size, one rung down. */
  denominator: number;
  /** Grid cell font size. */
  grid: number;
  padding: Padding;
}

/** The canvas states the L rung also needs 150 px of height; tokens.json only carries the width. */
export const RUNG_L_MIN_HEIGHT = 150;

export function rungFor(width: number, height: number): Rung {
  if (width >= ds.card.rung.L.minSlotWidth && height >= RUNG_L_MIN_HEIGHT) return 'L';
  if (width >= ds.card.rung.M.minSlotWidth) return 'M';
  return 'S';
}

export function rungSpec(rung: Rung): RungSpec {
  const sizes = ds.card.rung[rung];
  const [y, x] = ds.card.padding[rung];
  return { rung, value: sizes.value, denominator: sizes.denominator, grid: sizes.grid, padding: { y, x } };
}

export const rungForSlot = (slot: Rect): RungSpec => rungSpec(rungFor(slot.width, slot.height));
