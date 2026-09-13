/**
 * A zone face: the five parts composed into one screen.
 *
 * The rev bar in its well, the bar of settled values, zones B, A and C across the body, and band D
 * across the foot. Every zone is a widget pointing at a dashboard of its catalogue, with its screen
 * index bound to a plugin property, so a wheel button that increments that property changes the
 * page and nothing in the scene graph has to know.
 *
 * What this file does *not* do is decide any geometry. Every rectangle comes from the layout, which
 * read it off an artboard; see `docs/design/zones.md`.
 */
import type { Dashboard, DashboardMetadata, Item, Rect } from '../generator.ts';
import { FACE_ZONE_LETTERS, zoneCounter, type FaceZone } from '../contract.ts';
import { revBar } from '../components/revBar.ts';
import { band } from '../elements/band.ts';
import { rule } from '../elements/rule.ts';
import { flagStrip, FLAG_STRIP_STYLES } from '../components/flagStrip.ts';
import { pitLimiter } from '../components/pitLimiter.ts';
import { ds } from '../tokens.ts';
import { bar } from './bar.ts';
import { rectOf, type ZoneLayout } from './layout.ts';
import { label } from '../elements/label.ts';
import { densityForBox, densityOf } from '../second/density.ts';
import { zoneCounterX, zoneCounterWidth, zoneFrameMetrics, zoneTitleY } from '../second/header.ts';
import { widestCounter, zoneDashboardsFor, zoneLetterWidth, zoneWidget } from './pages.ts';

export const FACE_SCREEN_NAME = 'Main';

/** The zones a face embeds, each with the size its dashboard is drawn for. */
export const zonesOf = (layout: ZoneLayout): { zone: FaceZone; size: { width: number; height: number }; corners: boolean }[] =>
  FACE_ZONE_LETTERS.map((zone) => {
    const r = rectOf(layout, zone);
    return { zone, size: { width: r.width, height: r.height }, corners: zone === 'D' && layout.bandCorners };
  });

/** The items of a zone face's one screen. */
export function faceItems(layout: ZoneLayout): Item[] {
  const z = layout.zones;
  const items: Item[] = [];

  // The well the rev bar has sat in since the first token file named it, and which was never drawn.
  items.push(band('well', z.revBarWell, ds.purpose.block.well));
  items.push(...revBar({ left: z.revBar.left, top: z.revBar.top, width: z.revBar.width, height: z.revBar.height, gap: ds.space[2] }, 'revBar'));

  if (z.bar) {
    items.push(band('bar.ground', z.bar, ds.purpose.block.well));
    items.push(...bar(z.bar, 'bar.', { fieldsPerEnd: layout.barFieldsPerEnd }));
  }

  // One pixel between the zones, because a rule is the whole boundary where a block would be too
  // much. That is the reason most of the face is bare.
  for (const [name, gapLeft] of [
    ['rule.ba', z.zoneA.left - 1],
    ['rule.ac', z.zoneC.left - 1],
  ] as const) {
    if (gapLeft > z.zoneB.left) items.push(rule(name, gapLeft, z.zoneB.top, 1, z.zoneB.height));
  }

  for (const zone of FACE_ZONE_LETTERS) {
    items.push(zoneWidget(`zone${zone}`, zone, rectOf(layout, zone)));
    items.push(...zoneHeaderParts(zone, rectOf(layout, zone)));
  }

  // A flag takes the band over, because an alert outranks fuel. The same sixty pixels goes to
  // whichever has the better claim, which is why the face has no separate flag strip.
  items.push(...flagStrip(z.band, FLAG_STRIP_STYLES.standard, 'flag'));

  // The limiter covers zone A rather than taking room of its own: it is true for seconds at a time
  // and it is the one thing that matters while it is. Drawn last, so it is over the zone.
  items.push(...pitLimiter(z.pitLimiter, 'pitLimiter'));

  return items;
}

/**
 * The two ends of a zone's header, drawn by the face rather than by the zone: the letter and the
 * page counter.
 *
 * Zones B and C are the same rectangle on most faces, so one dashboard file serves both. A letter
 * inside it would say B in each, and a counter inside it would count the catalogue -- "15 / 21" on
 * a cycle of three, because a screen cannot know which zone's mask is deciding its length. The face
 * is the only thing that knows which rect is which zone, so both are drawn here, over the widget,
 * in the room the zone's header keeps at each end.
 *
 * Zones A and D carry no header, so they get neither.
 */
function zoneHeaderParts(zone: FaceZone, r: Rect): Item[] {
  if (zone === 'A' || zone === 'D') return [];
  const density = densityForBox({ width: r.width, height: r.height });
  const { padX } = zoneFrameMetrics(density);
  const size = densityOf(density).labelSm;
  const y = zoneTitleY(r, density);
  const counter = { kind: 'reserved', widest: widestCounter(zone, density) } as const;
  return [
    label(`zone${zone}.letter`, zone, r.left + padX, y, zoneLetterWidth(density), { size, color: ds.color.text.secondary }),
    label(`zone${zone}.counter`, counter.widest, zoneCounterX(r, counter, density), y, zoneCounterWidth(counter, density), {
      size,
      hAlign: 'right',
      bind: zoneCounter(zone),
      widest: counter.widest,
    }),
  ];
}

export interface FaceBuildOptions {
  version: string;
  simHubVersion: string;
  author: string;
}

export interface BuiltFace {
  main: Dashboard;
  /** One per distinct rectangle and catalogue: zone A's four pages, the modules, band D's eight. */
  zones: Dashboard[];
}

/** A zone face and the dashboards its zones cycle. */
export function buildZoneFace(layout: ZoneLayout, opts: FaceBuildOptions): BuiltFace {
  const metadata: DashboardMetadata = {
    title: layout.folder,
    author: opts.author,
    description: layout.description,
    version: opts.version,
    simHubVersion: opts.simHubVersion,
  };
  const main: Dashboard = {
    name: layout.folder,
    width: layout.width,
    height: layout.height,
    backgroundColor: layout.background,
    screens: [
      {
        name: FACE_SCREEN_NAME,
        inGame: true,
        idle: true,
        pit: true,
        backgroundColor: layout.background,
        items: faceItems(layout),
      },
    ],
    metadata,
  };
  return { main, zones: zoneDashboardsFor(zonesOf(layout), metadata) };
}
