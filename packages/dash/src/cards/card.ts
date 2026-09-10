/** A card: a changeable module that draws its items into any slot of its layout, at the slot's rung. */
import type { Item, Rect } from '../generator.ts';
import { cardMeta, type CardMeta } from '../contract.ts';
import { rungForSlot, type RungSpec } from '../design/rung.ts';

export interface Card extends CardMeta {
  /** Items for the card drawn in `slot`, every item name starting with `prefix` (unique within the screen). */
  build(slot: Rect, prefix: string): Item[];
}

export type CardBuilder = (slot: Rect, rung: RungSpec, prefix: string, meta: CardMeta) => Item[];

/** Binds a builder to its catalogue entry in contract.ts, which stays the single source of numbers and names. */
export function defineCard(id: string, builder: CardBuilder): Card {
  const meta = cardMeta(id);
  return { ...meta, build: (slot, prefix) => builder(slot, rungForSlot(slot), prefix, meta) };
}
