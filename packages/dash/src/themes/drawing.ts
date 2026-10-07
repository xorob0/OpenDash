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
import type { Expr } from '../bind.ts';
import type { FaceSize, FaceZone } from '../contract.ts';
import type { Hex, Item, Rect } from '../generator.ts';
import type { ModuleContext } from '../modules/module.ts';
import type { FieldSpec } from '../second/field.ts';
import type { Density } from '../second/density.ts';
import type { StackRow } from '../second/layout.ts';
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

/**
 * How a theme lays out and dresses the fields of a module inside the module's own rectangle, where the
 * house's register is not the car's. The modules keep their fields, their bindings, their `widest`
 * declarations and their shedding order; only the layout and the chrome around each field change.
 *
 * It is read per process rather than handed down through every module, because a theme is chosen for
 * the whole process exactly as `ds` is, and a module is a function of a rectangle that should not have
 * to know which theme is drawing it. `themes/moduleRegister.ts` is where the process finds it.
 */
export interface ModuleRegister {
  /**
   * A rank of fields, which the page has already cut to what it keeps at its shape, as a stack row.
   * `order` is the page's shedding order, most important first, which the row's `shed` must honour.
   */
  fieldsRow(specs: readonly FieldSpec[], ctx: ModuleContext, order: readonly string[]): StackRow;
  /** A page's rows laid out in its box, the ranks of fields and the gauges, bars and drawings between them. */
  stack(frame: Rect, rows: readonly StackRow[], density: Density): Item[];
  /** A page drawn whole, for one that lays its fields out itself rather than through `fieldsRow`; undefined keeps the page's own. */
  page?(id: string, ctx: ModuleContext): Item[] | undefined;
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
  /** Whether the face draws the zone letters, B and C in their headers and D at the band's left; the house does. */
  zoneLetters?: boolean;
  /** The size a module zone's header, its title and its counter, is set at, in place of the house face's. */
  zoneHeaderSize?: number;
  /** A module page's header drawn the theme's way, in place of the house's title line: the page, the zone's frame and the body under it. */
  moduleHeader?(page: { id: string; name: string }, frame: Rect, body: Rect): Item[];
  /** Whether zone A draws the gear's two neighbours ghosted beside it; the house does. */
  gearGhosts?: boolean;
  /** Whether band D draws its corner blocks, in place of the house face's answer for this size. */
  bandCorners?: boolean;
  /** Pages added to band D's catalogue, after the contract's own. */
  bandPages?: readonly ThemeBandPage[];
  /** How the modules of zones B and C lay their fields out; see {@link ModuleRegister}. */
  modules?: ModuleRegister;
  /**
   * One of band D's own pages drawn the theme's way, every field the house page draws at this frame
   * drawn under the same id. `classOnly` is the zone's class filter, which the relative page reads.
   */
  bandPage?(page: string, frame: Rect, prefix: string, classOnly?: Expr): Item[];
}
