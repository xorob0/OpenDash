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
 * for would scale its type with it, and zone A's four pages are not zone B's twenty-one, so the two
 * cannot share a file even at the same size.
 */
import type { Dashboard, DashboardMetadata, Hex, Item, Rect, Screen, WidgetItem } from '../generator.ts';
import {
  FACE_ZONE_LETTERS,
  pagesForZone,
  zone as zoneSetting,
  zoneClassOnlyOnPage,
  zoneCounterReadings,
  type FaceSize,
  type FaceZone,
  type FaceZonePageMeta,
} from '../contract.ts';
import { measureText } from '../design/advances.ts';
import { rect, type Size } from '../design/geometry.ts';
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

/**
 * One screen of a zone dashboard.
 *
 * Zones B, C and D carry a permanent header -- the zone letter, then the page name -- because the
 * canvas says outright that there is no row of page dots and that the letter and the name are what
 * say which page is showing. Zone A carries none: it is the gear, and 22 px of the column it is
 * sized to is too much to spend saying so. What zone A does instead is #154.
 */
export function zonePageScreen(face: FaceSize, zones: ZoneGroup, page: FaceZonePageMeta, size: Size, corners = false, ground: Hex = groundOf(zones[0])): Screen {
  const frame = rect(0, 0, size.width, size.height);
  const zone = zones[0];

  let items: Item[];

  if (zone === 'A') {
    // No header: see the comment on zoneAPages.
    items = zoneAPage(page.id, frame, `${page.id}.`);
  } else if (zone === 'D') {
    // A band draws no header either. It is one rank across the whole width, the corner blocks say
    // what is at each end, and a title line would take a third of the height to say "fuel" above a
    // field already labelled FUEL.
    //
    // The filter is handed to every page and read by D7 alone, because the rest of the band lists
    // nobody: on a page of three gaps "my class only" is the car ahead in class rather than a
    // shorter list, which is the whole of #210.
    items = [
      ...withBandSessionNotice(page, frame, corners, bandPageItems(page.id, frame, `${page.id}.`, corners, zoneClassOnlyOnPage(face, zones, page.number))),
      ...(corners ? bandCorners(frame, `${page.id}.corner.`) : []),
    ];
  } else {
    // The chrome is prefixed `zone.` rather than with the page id, because a module already names
    // its own items after itself: the track page draws `track.title` and so did the header.
    const density = densityForBox(size);
    const metrics = zoneFrameMetrics(density, 'face');
    // Neither the letter nor the counter is in the title. Zones B and C are the same rectangle on
    // most faces, so they share one dashboard file; a letter baked in here would draw B in both of
    // them, which is exactly what the first capture of the 1920 face showed, and a counter baked in
    // here would count the catalogue rather than the cycle the zone's own mask leaves. The face
    // draws both, and the frame keeps the room.
    const { items: chrome, body } = zoneFrame(
      `${page.id}.zone`,
      { frame, title: page.name, counter: { kind: 'reserved', widest: widestCounter(zone, metrics.size) }, indent: zoneLetterWidth(metrics.size) },
      density,
      'face',
    );
    items = [
      ...chrome,
      ...pageBuilder(page.id)({ frame: body, density, prefix: `${page.id}.`, shape: shapeOf(body), classOnly: zoneClassOnlyOnPage(face, zones, page.number) }),
    ];
  }

  return pageScreen(page.id, items, ground);
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
 * The notice sits in the room the page had rather than across the band, so it clears the zone letter
 * at one end and the corner blocks at both. It keeps the page's name, unlike a module's in a zone,
 * because a band draws no header to say it for it. Drawn at `zone` density, whose label size is the
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
 * A zone dashboard: every page of its catalogue, in the order the plugin lists them, and after
 * band D's the pages a theme adds to it, which go at the end so that no page leaves its index.
 */
export function zoneDashboard(face: FaceSize, zones: ZoneGroup, size: Size, metadata: DashboardMetadata, corners = false, drawing: ThemeDrawing = {}): Dashboard {
  const zone = zones[0];
  const pages = pagesForZone(zone);
  const kind = kindOf(zone);
  const ground = drawing.zoneGround?.(zone) ?? groundOf(zone);
  const frame = rect(0, 0, size.width, size.height);
  const added = zone === 'D' ? (drawing.bandPages ?? []) : [];
  const screens = [
    ...pages.map((page) => zonePageScreen(face, zones, page, size, corners, ground)),
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

/** The widget that embeds a zone dashboard in a face, its screen bound to the zone's property. */
export function zoneWidget(name: string, face: FaceSize, zone: FaceZone, frame: { left: number; top: number; width: number; height: number }): WidgetItem {
  const size = { width: frame.width, height: frame.height };
  const start = pagesForZone(zone).findIndex((p) => p.number === 0);
  return pagedWidget({
    name,
    rect: { ...frame },
    fileName: `${zoneDashboardName(kindOf(zone), size)}.djson`,
    initialScreenIndex: Math.max(0, start),
    page: zoneSetting.page(face, zone),
  });
}

/**
 * Every distinct zone dashboard a face needs, one per rectangle and catalogue it uses.
 *
 * Zones that land on the same file are grouped rather than dropped, because the file has to know
 * which zones read it: a page that filters to the player's class asks whether *the zone showing it*
 * was set to, and answering that with the wrong letter would have zone B follow zone C's setting.
 */
export function zoneDashboardsFor(face: FaceSize, zones: readonly { zone: FaceZone; size: Size; corners?: boolean }[], metadata: DashboardMetadata, drawing: ThemeDrawing = {}): Dashboard[] {
  const groups = new Map<string, { zones: [FaceZone, ...FaceZone[]]; size: Size; corners: boolean }>();
  for (const { zone, size, corners } of zones) {
    const key = zoneDashboardName(kindOf(zone), size);
    const group = groups.get(key);
    if (group) {
      if (!group.zones.includes(zone)) group.zones.push(zone);
      continue;
    }
    groups.set(key, { zones: [zone], size, corners: corners ?? false });
  }
  return [...groups.values()].map((g) => zoneDashboard(face, g.zones, g.size, metadata, g.corners, drawing));
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

/**
 * The room a zone's header keeps for its letter, which the face draws.
 *
 * The widest of A to D rather than the letter's own width, so that one shared dashboard indents the
 * same whichever zone it is serving. The gap after it is the canvas's double space, and nothing
 * more: the artboard's header is a baseline row with `gap: 8px` between the letter and the name.
 */
export const zoneLetterWidth = (size: number): number => Math.max(...FACE_ZONE_LETTERS.map((l) => Math.ceil(measureText('BarlowMedium', l, size)))) + 2 + ds.space[2];
