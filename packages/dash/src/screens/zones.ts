/**
 * The pit wall's data zones: a small dashboard of its own, one screen per page, embedded in the
 * page by a widget whose screen index is the zone's setting. It is the slot mechanism of the dash
 * face applied to a bigger screen, and it works for the same reason: a widget's screen index can
 * be bound, so changing a plugin setting changes what a zone shows without touching the file.
 *
 * One zone dashboard exists per distinct zone rectangle a package uses, because a widget scaled to
 * a box it was not drawn for would scale its type with it.
 */
import type { Dashboard, DashboardMetadata, Item, Screen, WidgetItem } from '../generator.ts';
import { secondScreen, PIT_WALL_WIDE_ZONE_PAGES, PIT_WALL_ZONE_PAGES, type PitWallZoneLetter, type PitWallZonePageMeta } from '../contract.ts';
import { rect } from '../design/geometry.ts';
import { pageBuilder } from '../modules/index.ts';
import { pageScreen, pagedDashboard, pagedWidget } from '../pagedDashboard.ts';
import { zoneFrame } from '../second/header.ts';
import { TRANSPARENT } from '../tokens.ts';
import type { Size } from '../design/geometry.ts';

export type ZoneKind = 'standard' | 'wide';

/**
 * The rectangle the canvas designs a zone of each kind on, which no page places.
 *
 * A zone is a widget, so the boxes a page is ever proved against are the ones the pit wall pages
 * happen to have room for, and 240 is not among them: a wide page that only works at the 255 px
 * the tower gives it would pass quietly. The fit suite builds every module into these as well as
 * into the placed rectangles, so the frame a page was drawn on is one a test can fail on, and a
 * kind added here is covered the day it is added rather than the day somebody lists it.
 */
export const ZONE_REFERENCE: Record<ZoneKind, Size> = {
  standard: { width: 639, height: 240 },
  wide: { width: 1279, height: 240 },
};

/** The pages a zone of this kind can show. */
export const pagesOf = (kind: ZoneKind): readonly PitWallZonePageMeta[] => (kind === 'wide' ? PIT_WALL_WIDE_ZONE_PAGES : PIT_WALL_ZONE_PAGES);

/** `zones-639x202` or `zones-wide-1039x255`: the dashboard name, which is also its file name. */
export const zoneDashboardName = (kind: ZoneKind, size: Size): string => `zones${kind === 'wide' ? '-wide' : ''}-${size.width}x${size.height}`;

/** One screen of a zone dashboard: the title bar and the page drawn in the body. */
export function zoneScreen(page: PitWallZonePageMeta, kind: ZoneKind, size: Size): Screen {
  const pages = pagesOf(kind);
  const frame = rect(0, 0, size.width, size.height);
  // The wide zone says so in its title, which is what the sheet draws: the same page is offered in
  // both kinds and a pit wall carries both at once, so the title is where the two are told apart.
  // The suffix is added here rather than in the catalogue, because the catalogue's name is also the
  // label the plugin puts in its wide-zone dropdown, where a suffix would read as a second page.
  const title = kind === 'wide' ? `${page.name} · Wide` : page.name;
  const { items: chrome, body } = zoneFrame(page.id, { frame, title, counter: { kind: 'static', page: page.number + 1, pages: pages.length } });
  // A zone dashboard is a pit wall screen, so both kinds label at the pit wall's 13 rather than the
  // face's 15: `PitWallZones.dc.html` writes eighty `.lblt` and not one `.lbl`.
  const density = kind === 'wide' ? 'wide' : 'panel';
  const items: Item[] = [...chrome, ...pageBuilder(page.id)({ frame: body, density, prefix: `${page.id}.` })];
  return pageScreen(page.id, items);
}

/** A zone dashboard: every page of its kind, in the order the plugin lists them. */
export function zoneDashboard(kind: ZoneKind, size: Size, metadata: DashboardMetadata): Dashboard {
  return pagedDashboard({
    name: zoneDashboardName(kind, size),
    size,
    screens: pagesOf(kind).map((page) => zoneScreen(page, kind, size)),
    metadata,
    description: `Zone pages drawn for a ${size.width} x ${size.height} zone.`,
  });
}

/** A zone on a page: the widget that embeds the zone dashboard, its page bound to the setting. */
export function zoneWidget(name: string, frame: { left: number; top: number; width: number; height: number }, kind: ZoneKind, letter: PitWallZoneLetter | 'wide'): WidgetItem {
  const size = { width: frame.width, height: frame.height };
  return pagedWidget({
    name,
    rect: { ...frame },
    fileName: `${zoneDashboardName(kind, size)}.djson`,
    initialScreenIndex: letter === 'wide' ? 5 : { A: 0, B: 1, C: 4, D: 2 }[letter],
    page: letter === 'wide' ? secondScreen.wideZonePage() : secondScreen.zonePage(letter),
    backgroundColor: TRANSPARENT,
  });
}
