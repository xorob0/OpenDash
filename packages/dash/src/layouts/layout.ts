/**
 * A layout: one screen size and everything the build needs to make its package. It declares
 * its shape, the hero variants and their geometry, its slot rects, the 1 px separators it
 * draws, and its package folder name and description. The card rung is derived from the slot
 * size, never declared; a layout may only override the rung's padding.
 */
import type { Hex, Rect } from '../generator.ts';
import type { Size } from '../design/geometry.ts';
import { rungFor, rungSpec, type Padding, type Rung, type RungSpec } from '../design/rung.ts';
import type { HeroGeometry } from '../hero/hero.ts';

export type Shape = 'rect' | 'round';

export interface NamedRect {
  name: string;
  rect: Rect;
}

export interface Layout {
  /** Package folder and main dashboard name, e.g. "OpenDash 850x480". Spaces are allowed. */
  folder: string;
  /** Metadata description: "<w> x <h>, <n> slots", or "<n> slots, round". */
  description: string;
  width: number;
  height: number;
  /** A round face is the display itself: nothing is drawn outside the disc and there are no rules. */
  shape: Shape;
  background: Hex;
  /** Every slot has this size, so every card fits every slot. */
  slotSize: Size;
  /** Slot rects in slot order: `slots[i - 1]` is slot i. */
  slots: Rect[];
  hero: HeroGeometry;
  /** Separator rules, 1 px, surface.raised. Empty on round faces. */
  rules: NamedRect[];
  /** Card padding when not the rung's own: the nano's 109 px cells take the S padding at rung M. */
  cardPadding?: Padding;
}

/** The rung every card takes in a layout, from its slot size. */
export const rungOf = (layout: Layout): Rung => rungFor(layout.slotSize.width, layout.slotSize.height);

/** The rung spec every card is built with in a layout: its rung's sizes, with the layout's padding when it overrides. */
export function cardRung(layout: Layout): RungSpec {
  const spec = rungSpec(rungOf(layout));
  return layout.cardPadding ? { ...spec, padding: { ...layout.cardPadding } } : spec;
}

/** The metadata description of a layout. */
export const layoutDescription = (shape: Shape, width: number, height: number, slots: number): string =>
  shape === 'round' ? `${slots} slots, round` : `${width} x ${height}, ${slots} slots`;

/** `<prefix>01`, `<prefix>02`, ... for a list of rects. */
export const namedRects = (prefix: string, rects: readonly Rect[]): NamedRect[] =>
  rects.map((r, i) => ({ name: `${prefix}${String(i + 1).padStart(2, '0')}`, rect: r }));
