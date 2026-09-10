/** The inner frame of a card: the slot minus the rung's padding, and the canvas's vertical centring. */
import type { Rect } from '../generator.ts';
import type { RungSpec } from '../design/rung.ts';

export interface CardFrame {
  /** Left edge of the content. */
  x: number;
  innerWidth: number;
  innerTop: number;
  innerHeight: number;
}

export function cardFrame(slot: Rect, rung: RungSpec): CardFrame {
  return {
    x: slot.left + rung.padding.x,
    innerWidth: slot.width - 2 * rung.padding.x,
    innerTop: slot.top + rung.padding.y,
    innerHeight: slot.height - 2 * rung.padding.y,
  };
}

/** Top of a block of `height` centred in the slot: the canvas card is a vertically centred flex column. */
export const centredTop = (slot: Rect, height: number): number => slot.top + (slot.height - height) / 2;
