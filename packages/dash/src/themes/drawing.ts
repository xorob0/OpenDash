/**
 * What a theme draws itself, where the house face's own drawing is not the car's (#205).
 *
 * The anatomy moves a part and the face follows; it cannot change what the part looks like. That is
 * enough for a theme that keeps the house's chrome, and it is not enough for the Porsche, whose shift
 * lights are sixteen dots, whose bar is the car's top strip and whose body is grey panels with the
 * readings in them. Each of the parts below is therefore one the face draws in its own way unless a
 * theme hands it a drawing, and the list is the parts the Porsche needed and no more. A theme with
 * no drawing is drawn exactly as the house face is, which is what keeps the default theme's packages
 * byte-identical: every hook is optional and every one left out is the house's code path unchanged.
 *
 * None of it touches a catalogue. The zones are still the house's widgets cycling the house's pages,
 * the flags, the pit family, the pop-ups and the lap review are still drawn by the face, and a page a
 * theme adds to band D goes at the end of the catalogue, as ADR 0015 requires, so that no zone opens
 * on a page its driver did not choose.
 *
 * Kept apart from `themes/index.ts`, and registered in `themes/drawings.ts` rather than in `THEMES`,
 * because a drawing reads `ds` and the registry is read before `ds` exists.
 */
import type { FaceSize, FaceZone } from '../contract.ts';
import type { Hex, Item, Rect } from '../generator.ts';
import type { ZoneLayout } from '../zones/layout.ts';
import type { Regions } from './anatomy.ts';

/** One arrangement of a face, as a drawing is handed it. */
export interface FaceContext {
  /** The house face of this size, under the package's own folder. */
  layout: ZoneLayout;
  face: FaceSize;
  /** The arrangement's regions: the anatomy's with the rev bar on, and derived from them with it off. */
  regions: Regions;
  withRevBar: boolean;
}

/** A page a theme adds to band D's catalogue, after the contract's eight. */
export interface ThemeBandPage {
  id: string;
  name: string;
  /** The page drawn in the band's frame, every item named under `prefix`. */
  items(frame: Rect, prefix: string): Item[];
}

export interface ThemeDrawing {
  /** The shift lights in the `revBar` region, in place of the house's segments and the well they sit in. */
  revBar?(ctx: FaceContext): Item[];
  /** What the `bar` region carries, in place of the house's bar of settled values and its ground. */
  bar?(ctx: FaceContext): Item[];
  /**
   * What the theme draws on the face behind and between the zones, in place of the house's rules and
   * band D's well: the parts of the car's drawing that are not a zone, a flag or an alert.
   */
  chrome?(ctx: FaceContext): Item[];
  /** States the theme draws over its regions, after the pit family and before the pop-ups. */
  takeovers?(ctx: FaceContext): Item[];
  /** The change notifications, in place of the house's box on the hero. */
  changeNotifications?(ctx: FaceContext): Item[];
  /** What a zone's pages are drawn on, in place of the house's base surface and band D's well. */
  zoneGround?(zone: FaceZone): Hex;
  /** Whether band D draws its corner blocks, in place of the house face's answer for this size. */
  bandCorners?: boolean;
  /** Pages added to band D's catalogue, after the contract's own. */
  bandPages?: readonly ThemeBandPage[];
}
