/**
 * The hero module: what a driver reads by reflex, fixed per layout. It holds one readout, the
 * gear, plus the indicators that belong to no card: the rev segments in a row (rev bar) or on a
 * circle (rev arc), the flags as a band at the bottom edge or as a ring on the rim, and the pit
 * limiter block above the gear. Every other value, speed included, is a card in a slot.
 */
import type { Item, Rect } from '../generator.ts';
import { band } from '../elements/band.ts';
import { flagRing } from '../components/flagRing.ts';
import { flagStrip, type FlagStripStyle } from '../components/flagStrip.ts';
import { gear } from '../components/gear.ts';
import { pitLimiter } from '../components/pitLimiter.ts';
import { revArc, type RevArcFrame } from '../components/revArc.ts';
import { REV_WELL_PAD_X, REV_WELL_PAD_Y, revBar, type RevBarFrame } from '../components/revBar.ts';
import type { Circle } from '../design/geometry.ts';
import { rect } from '../design/geometry.ts';
import { ds } from '../tokens.ts';

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

/**
 * The recessed ground the segments sit in: the segment row grown by the margin every zone artboard
 * draws around it.
 *
 * The card faces drew their segments on the bare ground, which made them the odd ones out rather
 * than a second design: `purpose.block.well` is defined as the recessed ground behind a gauge, and
 * the rev bar is the gauge the whole face is built around.
 */
const wellUnder = (bar: RevBarFrame): Rect =>
  rect(Math.max(0, bar.left - REV_WELL_PAD_X), Math.max(0, bar.top - REV_WELL_PAD_Y), bar.width + 2 * REV_WELL_PAD_X, bar.height + 2 * REV_WELL_PAD_Y);

export function revItems(rev: RevVariant): Item[] {
  switch (rev.kind) {
    case 'revBar':
      return [band('rev.well', wellUnder(rev), ds.purpose.block.well), ...revBar(rev)];
    // A round face draws its segments on an arc, where a rectangular well would be a box behind a
    // curve. Whether the rim takes an annular well of its own is the author's, and until it is
    // settled the two round faces keep the bare ground they have always had.
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
