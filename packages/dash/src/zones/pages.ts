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
import type { Dashboard, DashboardMetadata, Item, Screen, WidgetItem } from '../generator.ts';
import { BAND_D_PAGES, FACE_ZONE_LETTERS, ZONE_A_PAGES, pagesForZone, zone as zoneSetting, zoneCounterReadings, type FaceZone, type FaceZonePageMeta } from '../contract.ts';
import { measureText } from '../design/advances.ts';
import { rect, type Size } from '../design/geometry.ts';
import { pageBuilder } from '../modules/index.ts';
import { pageScreen, pagedDashboard, pagedWidget } from '../pagedDashboard.ts';
import { zoneFrame } from '../second/header.ts';
import { densityForBox, densityOf, type Density } from '../second/density.ts';
import { shapeOf } from '../second/shape.ts';
import { ds } from '../tokens.ts';
import { bandCorners, bandPageItems } from './bandPages.ts';
import { zoneAPage } from './zoneAPages.ts';

/** Which catalogue a zone dashboard carries. A and D have their own; B and C share the modules. */
export type ZoneKind = 'zoneA' | 'module' | 'band';

export const kindOf = (zone: FaceZone): ZoneKind => (zone === 'A' ? 'zoneA' : zone === 'D' ? 'band' : 'module');

/** `zoneface-module-769x314`: the dashboard name, which is also its file name. */
export const zoneDashboardName = (kind: ZoneKind, size: Size): string => `zoneface-${kind}-${size.width}x${size.height}`;

/**
 * One screen of a zone dashboard.
 *
 * Zones B, C and D carry a permanent header -- the zone letter, then the page name -- because the
 * canvas says outright that there is no row of page dots and that the letter and the name are what
 * say which page is showing. Zone A carries none: it is the gear, and 22 px of the column it is
 * sized to is too much to spend saying so. What zone A does instead is XOR-103.
 */
export function zonePageScreen(zone: FaceZone, page: FaceZonePageMeta, size: Size, corners = false): Screen {
  const frame = rect(0, 0, size.width, size.height);

  let items: Item[];

  if (zone === 'A') {
    // No header: see the comment on zoneAPages.
    items = zoneAPage(page.id, frame, `${page.id}.`);
  } else if (zone === 'D') {
    // A band draws no header either. It is one rank across the whole width, the corner blocks say
    // what is at each end, and a title line would take a third of the height to say "fuel" above a
    // field already labelled FUEL.
    items = [...bandPageItems(page.id, frame, `${page.id}.`), ...(corners ? bandCorners(frame, `${page.id}.corner.`) : [])];
  } else {
    // The chrome is prefixed `zone.` rather than with the page id, because a module already names
    // its own items after itself: the track page draws `track.title` and so did the header.
    const density = densityForBox(size);
    // Neither the letter nor the counter is in the title. Zones B and C are the same rectangle on
    // most faces, so they share one dashboard file; a letter baked in here would draw B in both of
    // them, which is exactly what the first capture of the 1920 face showed, and a counter baked in
    // here would count the catalogue rather than the cycle the zone's own mask leaves. The face
    // draws both, and the frame keeps the room.
    const { items: chrome, body } = zoneFrame(
      `${page.id}.zone`,
      { frame, title: page.name, counter: { kind: 'reserved', widest: widestCounter(zone, density) }, indent: zoneLetterWidth(density) },
      density,
    );
    items = [...chrome, ...pageBuilder(page.id)({ frame: body, density, prefix: `${page.id}.`, shape: shapeOf(body) })];
  }

  return pageScreen(page.id, items);
}

/** A zone dashboard: every page of its catalogue, in the order the plugin lists them. */
export function zoneDashboard(zone: FaceZone, size: Size, metadata: DashboardMetadata, corners = false): Dashboard {
  const pages = pagesForZone(zone);
  const kind = kindOf(zone);
  return pagedDashboard({
    name: zoneDashboardName(kind, size),
    size,
    screens: pages.map((page) => zonePageScreen(zone, page, size, corners)),
    metadata,
    description: `${kind === 'zoneA' ? ZONE_A_PAGES.length : kind === 'band' ? BAND_D_PAGES.length : pages.length} pages drawn for a ${size.width} x ${size.height} zone.`,
  });
}

/** The widget that embeds a zone dashboard in a face, its screen bound to the zone's property. */
export function zoneWidget(name: string, zone: FaceZone, frame: { left: number; top: number; width: number; height: number }): WidgetItem {
  const size = { width: frame.width, height: frame.height };
  const start = pagesForZone(zone).findIndex((p) => p.number === 0);
  return pagedWidget({
    name,
    rect: { ...frame },
    fileName: `${zoneDashboardName(kindOf(zone), size)}.djson`,
    initialScreenIndex: Math.max(0, start),
    page: zoneSetting.page(zone),
  });
}

/** Every distinct zone dashboard a face needs, one per rectangle and catalogue it uses. */
export function zoneDashboardsFor(zones: readonly { zone: FaceZone; size: Size; corners?: boolean }[], metadata: DashboardMetadata): Dashboard[] {
  const seen = new Map<string, Dashboard>();
  for (const { zone, size, corners } of zones) {
    const key = zoneDashboardName(kindOf(zone), size);
    if (seen.has(key)) continue;
    seen.set(key, zoneDashboard(zone, size, metadata, corners ?? false));
  }
  return [...seen.values()];
}

/**
 * The widest counter a zone can draw, which is the string its box is measured for.
 *
 * Every reading is tried rather than assuming the longest is the largest number twice over: the
 * label face is proportional, so "18 / 19" is wider than "21 / 21" at some sizes, and a box cut to
 * the wrong one clips a digit in a header that is otherwise never wrong.
 */
export function widestCounter(zone: FaceZone, density: Density): string {
  const size = densityOf(density).labelSm;
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
 * same whichever zone it is serving. The gap after it is the canvas's double space.
 */
export const zoneLetterWidth = (density: Density): number =>
  Math.max(...FACE_ZONE_LETTERS.map((l) => Math.ceil(measureText('BarlowMedium', l, densityOf(density).labelSm)))) + 2 + ds.space[2];

/** The height a zone's header takes, which the body starts under. Zone A has none. */
export const headerHeightOf = (zone: FaceZone): number => (zone === 'A' ? 0 : densityOf('zone').label + 9);
