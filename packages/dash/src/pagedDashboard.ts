/**
 * The mechanism behind every zone openDash draws: a dashboard whose screens are the pages of one
 * catalogue, and a widget that shows one of them, its screen index bound to a plugin property.
 *
 * Change the property and the widget switches screen. A wheel button that increments it is
 * therefore a wheel button that changes the page, with nothing in the scene graph needing to know
 * it happened. The pit wall has worked this way since it shipped and the face's zones work the same
 * way, which is the main reason the zone face was cheap to build -- so the two say it once here
 * rather than twice, once in `screens/zones.ts` and once in `zones/pages.ts`, which is what they
 * used to do.
 *
 * What is *not* shared is the catalogue, the chrome and the setting. A pit wall zone is chosen with
 * a mouse by somebody who is not driving and its list includes a web view; a face zone is cycled
 * with a thumb at speed and draws from the twenty-one modules. Those stay where they are; only the
 * shape of the file and the shape of the widget are one thing.
 *
 * Where this file finally lives is XOR-100's question, along with the rest of what `second/` holds.
 */
import type { Dashboard, DashboardMetadata, Item, Rect, Screen, WidgetItem } from './generator.ts';
import { withBindings, type Expr } from './bind.ts';
import type { Size } from './design/geometry.ts';
import { ds } from './tokens.ts';

export interface PagedDashboardSpec {
  /** The dashboard's name, which is also its file name. */
  name: string;
  size: Size;
  /** One screen per page of the catalogue, in the order the plugin lists them. */
  screens: Screen[];
  metadata: DashboardMetadata;
  /** The one line the metadata carries about what this file is. */
  description: string;
}

/** A dashboard of one screen per page: what a zone widget points at. */
export function pagedDashboard(spec: PagedDashboardSpec): Dashboard {
  return {
    name: spec.name,
    width: spec.size.width,
    height: spec.size.height,
    backgroundColor: ds.color.surface.base,
    screens: spec.screens,
    metadata: { ...spec.metadata, title: `${spec.metadata.title} ${spec.name}`, description: spec.description },
  };
}

export interface PagedWidgetSpec {
  /** Item name, unique within the screen that places it. */
  name: string;
  rect: Rect;
  /** The `.djson` the widget points at, which is a paged dashboard's name. */
  fileName: string;
  /** The page shown before any binding is evaluated, which is what a package without the plugin draws. */
  initialScreenIndex: number;
  /** The setting the shown page follows. */
  page: Expr;
  backgroundColor?: `#${string}`;
}

/**
 * The widget that shows one page of a paged dashboard.
 *
 * The dashboard is drawn for this rectangle, so there is nothing to scale; `autoSize` is on because
 * SimHub reads it as "this widget is the size its file says", which is the same rectangle.
 */
export function pagedWidget(spec: PagedWidgetSpec): WidgetItem {
  return {
    kind: 'widget',
    name: spec.name,
    rect: { ...spec.rect },
    fileName: spec.fileName,
    initialScreenIndex: spec.initialScreenIndex,
    autoSize: true,
    ...(spec.backgroundColor ? { backgroundColor: spec.backgroundColor } : {}),
    ...withBindings({ InitialScreenIndex: spec.page }),
  };
}

/** One screen of a paged dashboard: a page named after itself, drawn from the items given. */
export const pageScreen = (name: string, items: Item[]): Screen => ({
  name,
  inGame: true,
  idle: true,
  pit: false,
  backgroundColor: ds.color.surface.base,
  items,
});
