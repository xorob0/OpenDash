/**
 * The round family: the face is the display, its outer 12 px are the flag ring and everything
 * else sits in the inner disc. The rev arc runs over the top with its centre on the face centre,
 * the gear and speed sit in the middle under the pit limiter, and the slots flank or underlie
 * them. Every rect of a spec is relative to the inner disc, as the design canvas draws them.
 * No rules are drawn and nothing lies outside the disc, since SimHub does not mask corners: the
 * ring is the outermost element. Every round DDU size is this builder with its own numbers.
 */
import type { Hex, Rect } from '../generator.ts';
import { rect, translate, type Circle, type Size } from '../design/geometry.ts';
import type { Padding } from '../design/rung.ts';
import type { GearVariant } from '../hero/hero.ts';
import { ds } from '../tokens.ts';
import { layoutDescription, type Layout } from './layout.ts';

/** Where the inner disc starts: the flag ring's width in from the face's bounding square. */
export const INNER_INSET = ds.indicator.flagRing.width;

export interface RoundSpec {
  folder: string;
  /** Face diameter: the canvas is this square and the display is its inscribed disc. */
  size: number;
  /** Radius of the circle the rev segments are centred on, and their size before rotation. */
  revArc: { r: number; segment: Size };
  /** The gear and speed arrangement, its rect relative to the inner disc. */
  gear: GearVariant;
  /** Pit limiter block, relative to the inner disc. */
  pitLimiter: Rect;
  slotSize: Size;
  /** Slot origins relative to the inner disc, in slot order. */
  slotOrigins: readonly (readonly [number, number])[];
  /** Card padding when not the rung's own (the 800 round's 110 px cells take the S padding at rung M). */
  cardPadding?: Padding;
  background?: Hex;
}

/** The face circle of a round layout of `size`. */
export const faceOf = (size: number): Circle => ({ cx: size / 2, cy: size / 2, r: size / 2 });

/** The diameter of the inner disc of a round layout of `size`. */
export const innerDiameter = (size: number): number => size - 2 * INNER_INSET;

export function roundLayout(spec: RoundSpec): Layout {
  const face = faceOf(spec.size);
  const inner = (r: Rect): Rect => translate(r, INNER_INSET, INNER_INSET);
  const slots = spec.slotOrigins.map(([left, top]) => inner(rect(left, top, spec.slotSize.width, spec.slotSize.height)));
  return {
    folder: spec.folder,
    description: layoutDescription('round', spec.size, spec.size, slots.length),
    width: spec.size,
    height: spec.size,
    shape: 'round',
    background: spec.background ?? ds.color.surface.base,
    slotSize: { ...spec.slotSize },
    slots,
    hero: {
      rev: { kind: 'revArc', circle: { cx: face.cx, cy: face.cy, r: spec.revArc.r }, segment: { ...spec.revArc.segment } },
      gear: { ...spec.gear, rect: inner(spec.gear.rect) },
      pitLimiter: inner(spec.pitLimiter),
      flags: { kind: 'flagRing', face },
    },
    rules: [],
    ...(spec.cardPadding ? { cardPadding: { ...spec.cardPadding } } : {}),
  };
}
