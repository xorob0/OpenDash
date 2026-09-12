/**
 * A zone layout: one rectangular screen size, and the rectangles of its five parts.
 *
 * This is a type of its own rather than optional fields on `Layout`, and that is deliberate. Making
 * `slots` and `slotSize` optional would touch every one of the sixteen files that import the rung,
 * every card, and every test that reads a slot rect, for a change that is meant to add a face
 * rather than disturb ten working ones. Two types, one of which is retired whole in XOR-95, is the
 * cheaper shape and the revertible one.
 *
 * The numbers come from the artboards, read off them rather than derived: `design/canvas/Dash.dc.html`
 * and its siblings place every part absolutely, and `docs/design/zones.md` tabulates what they say.
 */
import type { Hex, Rect } from '../generator.ts';
import type { FaceZone } from '../contract.ts';

export interface ZoneRects {
  /** The recessed well the shift lights sit in. Full width, never moves. */
  revBarWell: Rect;
  /** The segments inside the well. */
  revBar: Rect;
  /**
   * The bar of settled values. Absent on the nano, where the height is not there and the band
   * carries the one changeable zone instead.
   */
  bar?: Rect;
  /** Zone A: the gear, read by reflex. A narrow column, because the gear wants height. */
  zoneA: Rect;
  zoneB: Rect;
  zoneC: Rect;
  /** Band D across the foot. A flag takes it over while one is out. */
  band: Rect;
  /** The pit limiter banner, drawn over zone A while the limiter is on. */
  pitLimiter: Rect;
}

export interface ZoneLayout {
  /** Package folder and main dashboard name, e.g. `openDash zones 1920x480`. */
  folder: string;
  description: string;
  width: number;
  height: number;
  background: Hex;
  zones: ZoneRects;
  /**
   * Whether band D draws a corner block at each end. The artboards draw them at 1920x480,
   * 1280x480, 1280x400 and 1280x720 and not at 850x480, 800x286 or 600x686; the threshold is those
   * drawings rather than a round number.
   */
  bandCorners: boolean;
  /** How many fields the bar's ends carry: two each on a wide face, one each in portrait. */
  barFieldsPerEnd: 1 | 2;
}

/** The rect a zone occupies. */
export function rectOf(layout: ZoneLayout, zone: FaceZone): Rect {
  if (zone === 'A') return layout.zones.zoneA;
  if (zone === 'B') return layout.zones.zoneB;
  if (zone === 'C') return layout.zones.zoneC;
  return layout.zones.band;
}

/** `1920 x 480, zones`: what the metadata says a package is. */
export const zoneLayoutDescription = (width: number, height: number): string => `${width} x ${height}, zones`;
