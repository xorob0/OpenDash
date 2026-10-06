/**
 * What a theme declares about the shape of a face: a function from a face size to a set of named
 * regions, each of which carries a role and a rectangle.
 *
 * The roles are the ones the face draws today and no more, because a role is something the face
 * has to know how to fill. A theme moves a region, or leaves out the bar, and the face follows; it
 * cannot ask for a part the face has no drawing for. The Porsche (#205) needs no new role: its top
 * strip is the bar, its dots are the rev bar, and its settings column and foot row are pages in
 * zone regions.
 *
 * This module imports types alone, since `themes/index.ts` reads it before `tokens.ts` has built `ds`.
 */
import type { Rect } from '../generator.ts';
import type { FaceZone } from '../contract.ts';
import type { Size } from '../design/geometry.ts';
import type { ZoneLayout } from '../zones/layout.ts';

/** The zones of the body, each a catalogue cycled from a wheel button. Band D is a role of its own. */
export type BodyZone = Exclude<FaceZone, 'D'>;

/**
 * Every role a region can have.
 *
 * `revBarWell` is the recess the shift lights sit in and `revBar` the segments inside it. `bar` is
 * the bar of settled values, the one role a face may go without. `zone` is one of the three body
 * zones, B, A and C, and `band` is band D. `pitAlert` is where the pit family is drawn, `hero` is
 * the rectangle the pop-ups, the change notifications and the lap review are centred on, and
 * `flagBody` is what a flag takes when its format is the full body.
 */
export type RegionRole = 'revBarWell' | 'revBar' | 'bar' | 'zone' | 'band' | 'pitAlert' | 'hero' | 'flagBody';

/** The roles a face has once at most, which is every role but the zones. */
export type SingleRole = Exclude<RegionRole, 'zone'>;

export type Region =
  | { readonly role: 'zone'; readonly zone: BodyZone; readonly rect: Rect }
  | { readonly role: SingleRole; readonly rect: Rect };

export type Regions = readonly Region[];

export interface Anatomy {
  /** The face sizes the theme draws. Each is one of `FACE_SIZES`, since the size is what names a face's settings. */
  readonly sizes: readonly Size[];
  /**
   * The regions of the face at one of `sizes`.
   *
   * It is handed the house face of that size rather than the bare size, because the house face
   * carries what a theme does not change yet (the background, the rev bar's gap, the bar's scale)
   * and because the default theme is then nothing but its rectangles handed back.
   */
  regions(face: ZoneLayout): Regions;
}

const BODY_ZONES: readonly BodyZone[] = ['B', 'A', 'C'];
const REQUIRED: readonly SingleRole[] = ['revBarWell', 'revBar', 'band', 'pitAlert', 'hero', 'flagBody'];

/** The rectangle of a role every face has. */
export function regionRect(regions: Regions, role: SingleRole): Rect {
  const rect = optionalRegionRect(regions, role);
  if (!rect) throw new Error(`the anatomy declares no ${role} region`);
  return rect;
}

/** The rectangle of a role a face may go without, which is the bar. */
export const optionalRegionRect = (regions: Regions, role: SingleRole): Rect | undefined => regions.find((r) => r.role === role)?.rect;

/** The rectangle a zone occupies: a body zone's own region, or the band for D. */
export function zoneRect(regions: Regions, zone: FaceZone): Rect {
  if (zone === 'D') return regionRect(regions, 'band');
  const region = regions.find((r) => r.role === 'zone' && r.zone === zone);
  if (!region) throw new Error(`the anatomy declares no region for zone ${zone}`);
  return region.rect;
}

/**
 * Refuses a set of regions the face cannot draw: a required role missing, a role or a zone given
 * twice, or a region that leaves the face. `where` names the face in the message.
 */
export function checkRegions(regions: Regions, size: Size, where: string): void {
  const problems: string[] = [];
  for (const role of REQUIRED) if (!regions.some((r) => r.role === role)) problems.push(`no ${role} region`);
  for (const zone of BODY_ZONES) {
    const count = regions.filter((r) => r.role === 'zone' && r.zone === zone).length;
    if (count !== 1) problems.push(`${count} regions for zone ${zone}`);
  }
  for (const role of [...REQUIRED, 'bar'] as const) {
    if (regions.filter((r) => r.role === role).length > 1) problems.push(`more than one ${role} region`);
  }
  for (const { role, rect } of regions) {
    const inside = rect.left >= 0 && rect.top >= 0 && rect.width > 0 && rect.height > 0 && rect.left + rect.width <= size.width && rect.top + rect.height <= size.height;
    if (!inside) problems.push(`the ${role} region ${JSON.stringify(rect)} is not inside ${size.width} x ${size.height}`);
  }
  if (problems.length > 0) throw new Error(`${where}: ${problems.join('; ')}`);
}
