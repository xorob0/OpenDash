/** A layout: one screen size, its hero geometry, its slot rects and the rules between them. */
import type { Hex, Rect } from '../generator.ts';
import type { HeroGeometry } from '../hero/hero.ts';
import type { Rung } from '../design/rung.ts';

export interface NamedRect {
  name: string;
  rect: Rect;
}

export interface Layout {
  /** Package folder and main dashboard name. */
  folder: string;
  width: number;
  height: number;
  background: Hex;
  /** Every slot has this size, so every card fits every slot. */
  slotSize: { width: number; height: number };
  /** Slot rects in slot order: `slots[i - 1]` is slot i. */
  slots: Rect[];
  /** The rung every card takes in this layout. */
  rung: Rung;
  hero: HeroGeometry;
  /** Separator rules, 1 px, surface.raised. */
  rules: NamedRect[];
}
