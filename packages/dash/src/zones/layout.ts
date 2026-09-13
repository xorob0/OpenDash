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


// --- The face with no rev bar ----------------------------------------------------------------
//
// XOR-138. A driver whose wheel already has LEDs across its top does not want a second set on the
// screen, and hiding the segments alone leaves the well lit by nothing: a recess at the top of the
// screen holding no light, with the first real content some way down from the edge. On the nano
// that recess and its gap are a twelfth of the screen.
//
// So the face has two arrangements rather than one, and they are both fixed at build time. The
// alternative was binding `Top` and `Height` on the parts below the well, and it was refused twice
// over: `Height` is bound in no shipping dashboard we have seen, and a box whose height is decided
// at runtime is a box `textFit.test.ts` and `secondScreens.test.ts` cannot measure against. Two
// geometries measure as easily as one. XOR-73 (ADR 0011) put positional personalisation in its
// second bucket; this is that bucket answered with a build-time flag rather than a binding.
//
// **These rectangles are derived and the canvas has not signed them off.** Every other rect in this
// directory is read off an artboard, and `docs/design/zones.md` §10 records the disagreement: what
// the canvas owes is which of the three answers this is, and XOR-138 is where that is tracked. The
// derivation below is deliberately one rule with no free parameters, so that replacing it with
// eight drawn tables is a deletion rather than an unpicking.

/**
 * The height the face gets back when the rev bar is off: the well, and the gap between it and the
 * first thing under it.
 *
 * The gap is part of it, because a recess with nothing under it for four pixels is as much of a hole
 * as the recess. The margin **above** the well is not: every artboard leaves one to two pixels there
 * and that is the top margin of the face rather than anything the rev bar owns. Giving it back too
 * would put the bar's labels hard against the top edge, where their line box starts a pixel above
 * the canvas and WPF clips the row.
 *
 * The first thing under the well is the bar where there is one, and the top of the body where there
 * is not, which is the nano.
 */
export const revBarReclaim = (zones: ZoneRects): number => (zones.bar?.top ?? bodyTop(zones)) - zones.revBarWell.top;

/** The top of the body: the highest of the three zones, which is all three of them in landscape. */
const bodyTop = (zones: ZoneRects): number => Math.min(zones.zoneA.top, zones.zoneB.top, zones.zoneC.top);

/** `rect` moved up by `by`, keeping its size. */
const liftedBy = (r: Rect, by: number): Rect => ({ ...r, top: r.top - by });

/**
 * The same rectangles with the rev bar's room given back.
 *
 * One rule: the bar rises to where the well began, the zones that start the body rise with it and
 * grow by what they gained, and everything else stays exactly where the artboard put it. Band D
 * does not move, because it is measured from the bottom edge and the bottom edge has not changed;
 * the limiter moves with zone A, because it is drawn over zone A and nowhere else.
 *
 * In landscape B, A and C all start the body, so all three grow. In portrait only zone A does, and
 * B and C keep both their rectangles and their zone dashboards -- which is why a portrait package
 * gains one zone dashboard and a landscape one gains two.
 */
export function zonesWithoutRevBar(zones: ZoneRects): ZoneRects {
  const reclaim = revBarReclaim(zones);
  const top = bodyTop(zones);
  const grown = (r: Rect): Rect => (r.top === top ? { ...r, top: r.top - reclaim, height: r.height + reclaim } : r);
  const zoneA = grown(zones.zoneA);
  return {
    ...zones,
    ...(zones.bar ? { bar: liftedBy(zones.bar, reclaim) } : {}),
    zoneA,
    zoneB: grown(zones.zoneB),
    zoneC: grown(zones.zoneC),
    pitLimiter: liftedBy(zones.pitLimiter, zones.zoneA.top - zoneA.top),
  };
}

/** The layout as it is drawn with the rev bar off. The face builds both and shows one. */
export const layoutWithoutRevBar = (layout: ZoneLayout): ZoneLayout => ({ ...layout, zones: zonesWithoutRevBar(layout.zones) });
