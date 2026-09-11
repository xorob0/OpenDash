/**
 * The hero module: what a driver reads by reflex, fixed per layout. It holds one readout, the
 * gear, plus the indicators that belong to no card: the rev segments in a row (rev bar) or on a
 * circle (rev arc), the flags as a band at the bottom edge or as a ring on the rim, and the pit
 * limiter block above the gear. Every other value, speed included, is a card in a slot.
 */
import type { Item, Rect } from '../generator.ts';
import { flagRing } from '../components/flagRing.ts';
import { flagStrip, type FlagStripStyle } from '../components/flagStrip.ts';
import { gear } from '../components/gear.ts';
import { pitLimiter } from '../components/pitLimiter.ts';
import { revArc, type RevArcFrame } from '../components/revArc.ts';
import { revBar, type RevBarFrame } from '../components/revBar.ts';
import type { Circle } from '../design/geometry.ts';

export type RevVariant = ({ kind: 'revBar' } & RevBarFrame) | ({ kind: 'revArc' } & RevArcFrame);

/** The gear's rect: a column between the slot grids, a full-width band, or the middle of a round face. */
export interface GearVariant {
  rect: Rect;
  /** Font size when not the standard 260 (the nano's 180). */
  size?: number;
}

export type FlagVariant = { kind: 'flagStrip'; rect: Rect; style?: FlagStripStyle } | { kind: 'flagRing'; face: Circle };

export interface HeroGeometry {
  rev: RevVariant;
  gear: GearVariant;
  pitLimiter: Rect;
  flags: FlagVariant;
}

export function revItems(rev: RevVariant): Item[] {
  switch (rev.kind) {
    case 'revBar':
      return revBar(rev);
    case 'revArc':
      return revArc(rev);
  }
}

export function gearItems(variant: GearVariant): Item[] {
  return gear(variant.rect, variant.size);
}

export function flagItems(flags: FlagVariant): Item[] {
  switch (flags.kind) {
    case 'flagStrip':
      return flagStrip(flags.rect, flags.style);
    case 'flagRing':
      return flagRing(flags.face);
  }
}

/** Rev segments, gear, pit limiter, flags: in that order, so the flags draw last (the ring is the outermost element of a round face). */
export function hero(geometry: HeroGeometry): Item[] {
  return [...revItems(geometry.rev), ...gearItems(geometry.gear), ...pitLimiter(geometry.pitLimiter), ...flagItems(geometry.flags)];
}
