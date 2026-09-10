/**
 * The hero module: what a driver reads by reflex, fixed per layout. Each part comes in the
 * variant the face needs: the rev segments in a row (rev bar) or on a circle (rev arc); the gear
 * and speed as a row, a full-width band or a stack; the flags as a band at the bottom edge or
 * as a ring on the rim; and the pit limiter block above the gear.
 */
import type { Item, Rect } from '../generator.ts';
import { flagRing } from '../components/flagRing.ts';
import { flagStrip, type FlagStripStyle } from '../components/flagStrip.ts';
import { gearSpeedBand, gearSpeedRow, gearSpeedStack, type GearSpeedSizes } from '../components/gearSpeed.ts';
import { pitLimiter } from '../components/pitLimiter.ts';
import { revArc, type RevArcFrame } from '../components/revArc.ts';
import { revBar, type RevBarFrame } from '../components/revBar.ts';
import type { Circle } from '../design/geometry.ts';

export type RevVariant = ({ kind: 'revBar' } & RevBarFrame) | ({ kind: 'revArc' } & RevArcFrame);

export type GearSpeedVariant =
  | { kind: 'gearSpeedRow'; rect: Rect; sizes?: GearSpeedSizes }
  | { kind: 'gearSpeedBand'; rect: Rect; sizes?: GearSpeedSizes }
  | { kind: 'gearSpeedStack'; rect: Rect };

export type FlagVariant = { kind: 'flagStrip'; rect: Rect; style?: FlagStripStyle } | { kind: 'flagRing'; face: Circle };

export interface HeroGeometry {
  rev: RevVariant;
  gearSpeed: GearSpeedVariant;
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

export function gearSpeedItems(variant: GearSpeedVariant): Item[] {
  switch (variant.kind) {
    case 'gearSpeedRow':
      return gearSpeedRow(variant.rect, variant.sizes);
    case 'gearSpeedBand':
      return gearSpeedBand(variant.rect, variant.sizes);
    case 'gearSpeedStack':
      return gearSpeedStack(variant.rect);
  }
}

export function flagItems(flags: FlagVariant): Item[] {
  switch (flags.kind) {
    case 'flagStrip':
      return flagStrip(flags.rect, flags.style);
    case 'flagRing':
      return flagRing(flags.face);
  }
}

/** Rev segments, gear and speed, pit limiter, flags: in that order, so the flags draw last (the ring is the outermost element of a round face). */
export function hero(geometry: HeroGeometry): Item[] {
  return [...revItems(geometry.rev), ...gearSpeedItems(geometry.gearSpeed), ...pitLimiter(geometry.pitLimiter), ...flagItems(geometry.flags)];
}
