/**
 * The dashboards a zone cycles through.
 *
 * A zone is a `WidgetItem` whose `InitialScreenIndex` is bound to a plugin property, pointing at a
 * dashboard with one screen per page. Change the property and the widget switches screen; a wheel
 * button that increments the property is therefore a wheel button that changes the page, with
 * nothing in the scene graph needing to know it happened. That is the same mechanism the pit wall
 * has used since it shipped, which is the main reason the zone face is cheap to build.
 *
 * One dashboard per distinct rectangle **and catalogue**. A widget scaled to a box it was not drawn
 * for would scale its type with it, and zone A's four pages are not zone B's twenty-two, so the two
 * cannot share a file even at the same size.
 */
import type { Dashboard, DashboardMetadata, Item, Rect, Screen, TextItem, WidgetItem } from '../generator.ts';
import {
  DEFAULT_ZONE_PAGE,
  defaultZonePage,
  isHeldBack,
  pagesForZone,
  zone as zoneSetting,
  zoneClassOnlyOnPage,
  zoneCounterReadings,
  type FaceSize,
  type FaceZone,
  type FaceZonePageMeta,
  type ThemeEntry,
} from '../contract.ts';
import { measureText } from '../design/advances.ts';
import { bottom, overlaps, rect, translate, type Size } from '../design/geometry.ts';
import { inkOf } from '../design/metrics.ts';
import { walkItems } from '../walk.ts';
import { pageBuilder } from '../modules/index.ts';
import { sessionNotice, withSessionGate } from '../modules/module.ts';
import { pageScreen, pagedDashboard, pagedWidget } from '../pagedDashboard.ts';
import { zoneFrame, zoneFrameMetrics } from '../second/header.ts';
import { densityForBox } from '../second/density.ts';
import { shapeOf } from '../second/shape.ts';
import { ds } from '../tokens.ts';
import { bandCorners, bandPageItems, bandPageRoom } from './bandPages.ts';
import { zoneAPage } from './zoneAPages.ts';
import type { ThemeDrawing } from '../themes/drawing.ts';
import { regionRect, zoneRect, type Regions } from '../themes/anatomy.ts';

/** Which catalogue a zone dashboard carries. A and D have their own; B and C share the modules. */
export type ZoneKind = 'zoneA' | 'module' | 'band';

export const kindOf = (zone: FaceZone): ZoneKind => (zone === 'A' ? 'zoneA' : zone === 'D' ? 'band' : 'module');

/**
 * The zones one dashboard file serves. Usually one; B and C where they are the same rectangle, in
 * which case a page in the file has to ask which of them is showing it before it reads a per-zone
 * setting. Non-empty by construction, so `zones[0]` needs no guard.
 */
export type ZoneGroup = readonly [FaceZone, ...FaceZone[]];

/** `zoneface-module-769x314`: the dashboard name, which is also its file name. */
export const zoneDashboardName = (kind: ZoneKind, size: Size): string => `zoneface-${kind}-${size.width}x${size.height}`;

/** One of zone A's pages drawn in `frame`, the theme's way where it draws one and the house's otherwise. */
const zoneAItems = (page: string, frame: Rect, drawing: ThemeDrawing): Item[] =>
  drawing.zoneAPage?.(page, frame, `${page}.`) ?? zoneAPage(page, frame, `${page}.`, drawing.gearGhosts ?? true);

/**
 * A run of zone A that draws the gear: the gear itself, the neighbours ghosted beside it, and the
 * unlit cell an LCD draws under it. Never the small label a page sets after it.
 */
const GEAR_RUN = /(^|\.)gear(\.(value|ghost|below|above))?$/;

/** The runs of a zone A page that draw the gear, layers walked through. */
const gearRunsOf = (items: readonly Item[]): TextItem[] => [...walkItems(items)].filter((item): item is TextItem => item.kind === 'text' && GEAR_RUN.test(item.name));

/**
 * How far down zone A the pit banner reaches, which the gear's ink has to begin below (#1182): from
 * the zone's top to the banner's foot where a face draws the banner over zone A, as the house and the
 * Porsche do at the top of it, and nothing where the banner is drawn clear of the zone, as the AiM
 * draws it over zone C and the Porsche's nano above the gear's tile.
 *
 * The pit family is opaque and is out for as long as a driver is in the lane, so a gear whose ink
 * reached into it was drawn without its top for the whole stop.
 */
export function bannerClearance(regions: Regions): number {
  const zoneA = zoneRect(regions, 'A');
  const banner = regionRect(regions, 'pitAlert');
  return overlaps(zoneA, banner) ? bottom(banner) - zoneA.top : 0;
}

/**
 * The drawing zone A's pages are drawn with where the pit banner reaches `clear` down the zone: the
 * house's pages told to begin the gear's ink below it, and any page, the house's or a theme's own,
 * refused if the banner would still cover its gear, so that a page or a face that brings the gear up
 * under the banner fails the build rather than ships.
 *
 * It is the theme's own hook for a page of zone A that carries the clearance, rather than a number
 * threaded through every zone dashboard, because that hook is already where a page of zone A is asked
 * for, and zones B, C and D have no use for it.
 */
export function zoneADrawing(drawing: ThemeDrawing, clear: number): ThemeDrawing {
  if (clear <= 0) return drawing;
  return {
    ...drawing,
    zoneAPage: (page, frame, prefix) => {
      const items = drawing.zoneAPage?.(page, frame, prefix) ?? zoneAPage(page, frame, prefix, drawing.gearGhosts ?? true, clear);
      const covered = gearRunsOf(items).filter((run) => inkOf(run).top < frame.top + clear);
      if (covered.length > 0) {
        throw new Error(`zone A: the pit banner reaches ${clear} px down a ${frame.width} x ${frame.height} column and covers the top of ${covered.map((run) => run.name).join(', ')}`);
      }
      return items;
    },
  };
}

/**
 * Where the gear marks the face on each of zone A's pages, in the face's own coordinates: the ink of
 * every run that draws it, drawn in zone A as the zone's dashboard draws it, under the face's banner.
 *
 * Read off the pages rather than off a table, because the gear is cut from the column (rule 18) and
 * so moves with every face, every arrangement and every theme's page. It is what a pop-up that stays
 * up for as long as its condition holds is kept clear of (#1047): the gear may be on whichever page a
 * driver cycles to while it is up. The track page draws no gear and adds nothing.
 */
export function zoneAGearInk(regions: Regions, drawing: ThemeDrawing = {}): Rect[] {
  const zoneA = zoneRect(regions, 'A');
  const frame = rect(0, 0, zoneA.width, zoneA.height);
  const drawn = zoneADrawing(drawing, bannerClearance(regions));
  return pagesForZone('A').flatMap((page) => gearRunsOf(zoneAItems(page.id, frame, drawn)).map((item) => translate(inkOf(item), zoneA.left, zoneA.top)));
}

/**
 * One screen of a zone dashboard.
 *
 * Zones B and C carry a permanent header with the page name, because the canvas says outright that
 * there is no row of page dots and that the name is what says which page is showing. Zone A carries
 * none: it is the gear, and 22 px of the column it is sized to is too much to spend saying so. What zone A does instead is #154.
 */
export function zonePageScreen(face: FaceSize, zones: ZoneGroup, page: FaceZonePageMeta, size: Size, corners = false, drawing: ThemeDrawing = {}): Screen {
  const frame = rect(0, 0, size.width, size.height);
  const zone = zones[0];
  const ground = drawing.zoneGround?.(zone) ?? groundOf(zone);

  let items: Item[];

  if (zone === 'A') {
    // No header: see the comment on zoneAPages.
    items = zoneAItems(page.id, frame, drawing);
  } else if (zone === 'D') {
    // A band draws no header either. It is one rank across the whole width, the corner blocks say
    // what is at each end, and a title line would take a third of the height to say "fuel" above a
    // field already labelled FUEL.
    //
    // The filter is handed to every page and read by D7 alone, because the rest of the band lists
    // nobody: on a page of three gaps "my class only" is the car ahead in class rather than a
    // shorter list, which is the whole of #210.
    items = [
      ...withBandSessionNotice(
        page,
        frame,
        corners,
        (drawing.bandPage ?? ((id, at, prefix, classOnly) => bandPageItems(id, at, prefix, corners, classOnly)))(page.id, frame, `${page.id}.`, zoneClassOnlyOnPage(face, zones, page.number)),
      ),
      ...(corners ? bandCorners(frame, `${page.id}.corner.`) : []),
    ];
  } else {
    // The chrome is prefixed `zone.` rather than with the page id, because a module already names
    // its own items after itself: the track page draws `track.title` and so did the header.
    const density = densityForBox(size);
    const metrics = zoneFrameMetrics(density, 'face');
    // The counter is not in the title. Zones B and C are the same rectangle on most faces, so they
    // share one dashboard file, and a counter baked in here would count the catalogue rather than
    // the cycle the zone's own mask leaves. The face draws it, and the frame keeps the room.
    const { items: chrome, body } = zoneFrame(
      `${page.id}.zone`,
      { frame, title: page.name, counter: { kind: 'reserved', widest: widestCounter(zone, metrics.size) } },
      density,
      'face',
    );
    const themed = drawing.moduleFrame?.(page, frame, body);
    const at = themed?.body ?? body;
    items = [
      ...(themed ? themed.items : chrome),
      ...pageBuilder(page.id)({ frame: at, density, prefix: `${page.id}.`, shape: shapeOf(at), classOnly: zoneClassOnlyOnPage(face, zones, page.number) }),
    ];
  }

  return pageScreen(page.id, drawing.restyle ? drawing.restyle(items) : items, ground);
}

/**
 * The body a module page is drawn in, in a zone dashboard of `size`: what the house's zone frame cuts
 * under its header, or what a theme's `moduleFrame` gives the page instead, and the whole frame for a
 * page of the theme's own, which has no header. The conformance harness holds every page to it, so it
 * is the one answer to where a page may draw.
 */
export function moduleBodyOf(page: { id: string; name: string }, size: Size, drawing: ThemeDrawing = {}): Rect {
  const frame = rect(0, 0, size.width, size.height);
  if (drawing.modulePages?.some((p) => p.id === page.id)) return frame;
  const { body } = zoneFrame('probe', { frame, title: page.name, counter: { kind: 'reserved', widest: '21 / 21' } }, densityForBox(size), 'face');
  return drawing.moduleFrame?.(page, frame, body).body ?? body;
}

/**
 * The band pages that have nothing to draw until the game has a session, and say so (#406).
 *
 * The same four questions the module catalogue answers for zones B and C, answered here because
 * band D's pages are its own rather than modules: fuel use, the stint, the sectors and the three
 * gaps of the relative are all timing, and the other four are car state or a notice of their own.
 */
export const BAND_PAGES_NEEDING_SESSION: readonly string[] = ['fuel', 'stint', 'sectors', 'relative'];

/**
 * A band page's rank and its notice, one of them on the screen at a time, the way `defineModule`
 * arranges a module's.
 *
 * It is here rather than inside `bandPageItems` so that `bandPages.test.ts` keeps reading the rank
 * directly: what that file is about is which fields a band draws and where, and it should not have
 * to walk a layer to ask.
 *
 * The notice sits in the room the page had rather than across the band, so it clears the corner
 * blocks at both ends. It keeps the page's name, unlike a module's in a zone, because a band draws no
 * header to say it for it. Drawn at `zone` density, whose label size is the
 * `ds.size.label` the band's own labels are set in.
 */
function withBandSessionNotice(page: FaceZonePageMeta, frame: Rect, corners: boolean, items: Item[]): Item[] {
  if (!BAND_PAGES_NEEDING_SESSION.includes(page.id)) return items;
  const room = bandPageRoom(frame, corners);
  return withSessionGate(`${page.id}.`, sessionNotice(page), rect(room.left, frame.top, room.width, frame.height), 'zone', items);
}

/**
 * What a zone's pages are drawn on.
 *
 * Band D is recessed on every artboard -- `background: #060708`, the same well the rev bar and the
 * bar sit in -- and a widget paints its own dashboard's ground over the face, so the well has to be
 * the band's own ground rather than a rectangle drawn behind its widget.
 */
const groundOf = (zone: FaceZone): `#${string}` => (zone === 'D' ? ds.purpose.block.well : ds.color.surface.base);

/**
 * The screens of a zone's catalogue in the plugin's order, each with the page whose number shows it:
 * what a zone dashboard draws before the pages a theme adds, and what the face reads when it has to
 * know where a page's items fall under something it draws over the zone (#1165).
 *
 * A held-back page's screen is the house's opening page renamed, as {@link zoneDashboard} says why.
 */
export function zonePageScreens(face: FaceSize, zones: ZoneGroup, size: Size, corners = false, drawing: ThemeDrawing = {}): { page: FaceZonePageMeta; screen: Screen }[] {
  const zone = zones[0];
  const pages = pagesForZone(zone);
  return pages.map((page) => ({
    page,
    screen: isHeldBack(zone, page.id)
      ? { ...zonePageScreen(face, zones, pages[DEFAULT_ZONE_PAGE[zone]]!, size, corners, drawing), name: page.id }
      : zonePageScreen(face, zones, page, size, corners, drawing),
  }));
}

/**
 * A zone dashboard: every page of its catalogue, in the order the plugin lists them, and after
 * band D's and the modules' the pages a theme adds to them, which go at the end so that no page leaves
 * its index.
 *
 * A held-back page keeps its screen, so that the pages after it keep their indices, and that screen
 * draws the house's opening page, fuel, rather than the held page: the plugin never shows it, and a
 * property that says it anyway, from a settings file nothing has normalised, falls back to a page
 * rather than to an empty well or to the held page itself (#969). The copy is named after the page it
 * stands in for, which is what keeps its screen id and its items' ids its own. It is the house's page
 * and not a theme's opening one, because a theme's band page may carry what a face draws once, such
 * as the Porsche's crest.
 */
export function zoneDashboard(face: FaceSize, zones: ZoneGroup, size: Size, metadata: DashboardMetadata, corners = false, drawing: ThemeDrawing = {}): Dashboard {
  const zone = zones[0];
  const kind = kindOf(zone);
  const ground = drawing.zoneGround?.(zone) ?? groundOf(zone);
  const frame = rect(0, 0, size.width, size.height);
  const added = zone === 'D' ? (drawing.bandPages ?? []) : zone === 'A' ? [] : (drawing.modulePages ?? []);
  const screens = [
    ...zonePageScreens(face, zones, size, corners, drawing).map(({ screen }) => screen),
    ...added.map((page) => pageScreen(page.id, [...page.items(frame, `${page.id}.`), ...(corners ? bandCorners(frame, `${page.id}.corner.`) : [])], ground)),
  ];
  return pagedDashboard({
    name: zoneDashboardName(kind, size),
    size,
    screens,
    metadata,
    description: `${screens.length} pages drawn for a ${size.width} x ${size.height} zone.`,
    background: ground,
  });
}

/**
 * The widget that embeds a zone dashboard in a face, its screen bound to the zone's property.
 *
 * `theme` is the catalogue entry of the theme the face is drawn in. Every zone opens on its start page,
 * `defaultZonePage`: zone C on the relative, and on a theme that adds band pages the band on the first
 * of them, with no plugin as with one (#718), and zones B and C on the pages its `opensOn` names (#203).
 */
export function zoneWidget(name: string, face: FaceSize, zone: FaceZone, frame: { left: number; top: number; width: number; height: number }, theme?: ThemeEntry): WidgetItem {
  const size = { width: frame.width, height: frame.height };
  // The screen the widget shows before its binding resolves, which is what Dash Studio's editor, the
  // Overview thumbnails and the first frame draw: the binding's own fallback, the zone's start page, so
  // that the file and a dash without the plugin open on the same page (#908). A page's number is its
  // screen index on every zone dashboard, the theme's pages numbered on after the house's.
  return pagedWidget({
    name,
    rect: { ...frame },
    fileName: `${zoneDashboardName(kindOf(zone), size)}.djson`,
    initialScreenIndex: defaultZonePage(zone, theme),
    page: zoneSetting.page(face, zone, theme),
  });
}

/**
 * Every distinct zone dashboard a face needs, one per rectangle and catalogue it uses.
 *
 * Zones that land on the same file are grouped rather than dropped, because the file has to know
 * which zones read it: a page that filters to the player's class asks whether *the zone showing it*
 * was set to, and answering that with the wrong letter would have zone B follow zone C's setting.
 *
 * `clear` is how far down zone A the pit banner reaches (`bannerClearance`), which zone A's pages
 * draw the gear below. One file is one drawing, so two arrangements that share zone A's rectangle and
 * not the banner's reach are refused rather than one of them drawn with the other's gear.
 */
export function zoneDashboardsFor(face: FaceSize, zones: readonly { zone: FaceZone; size: Size; corners?: boolean; clear?: number }[], metadata: DashboardMetadata, drawing: ThemeDrawing = {}): Dashboard[] {
  const groups = new Map<string, { zones: [FaceZone, ...FaceZone[]]; size: Size; corners: boolean; clear: number }>();
  for (const { zone, size, corners, clear = 0 } of zones) {
    const key = zoneDashboardName(kindOf(zone), size);
    const group = groups.get(key);
    if (group) {
      if (group.clear !== clear) throw new Error(`${key}: drawn once for two reaches of the pit banner, ${group.clear} px and ${clear} px`);
      if (!group.zones.includes(zone)) group.zones.push(zone);
      continue;
    }
    groups.set(key, { zones: [zone], size, corners: corners ?? false, clear });
  }
  return [...groups.values()].map((g) => zoneDashboard(face, g.zones, g.size, metadata, g.corners, zoneADrawing(drawing, g.clear)));
}

/**
 * The widest counter a zone can draw, which is the string its box is measured for.
 *
 * Every reading is tried rather than assuming the longest is the largest number twice over: the
 * label face is proportional, so "18 / 19" is wider than "21 / 21" at some sizes, and a box cut to
 * the wrong one clips a digit in a header that is otherwise never wrong. The size is passed rather
 * than taken from the density, because the header is measured in one place and drawn in another and
 * the two have to be the same number.
 */
export function widestCounter(zone: FaceZone, size: number): string {
  let widest = '';
  let width = -1;
  for (const reading of zoneCounterReadings(zone)) {
    const w = measureText('BarlowMedium', reading, size);
    if (w > width) {
      width = w;
      widest = reading;
    }
  }
  return widest;
}
