/**
 * A zone face: the five parts composed into a screen, twice.
 *
 * The rev bar in its well, the bar of settled values, zones B, A and C across the body, and band D
 * across the foot. Every zone is a widget pointing at a dashboard of its catalogue, with its screen
 * index bound to a plugin property, so a wheel button that increments that property changes the
 * page and nothing in the scene graph has to know.
 *
 * Twice, because `OpenDash.RevBar` `off` is not a hidden rev bar but a differently arranged screen:
 * the two arrangements are built here and SimHub shows whichever the setting enables. XOR-138.
 *
 * What this file does *not* do is decide any geometry. Every rectangle comes from the layout, which
 * read it off an artboard; see `docs/design/zones.md`.
 */
import type { Dashboard, DashboardMetadata, Item, Rect, Screen } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { FACE_ZONE_LETTERS, setting, type FaceZone } from '../contract.ts';
import { revBar } from '../components/revBar.ts';
import { band } from '../elements/band.ts';
import { rule } from '../elements/rule.ts';
import { flagStrip, FLAG_STRIP_STYLES } from '../components/flagStrip.ts';
import { pitLimiter } from '../components/pitLimiter.ts';
import { ds } from '../tokens.ts';
import { bar } from './bar.ts';
import { layoutWithoutRevBar, rectOf, type ZoneLayout } from './layout.ts';
import { label } from '../elements/label.ts';
import { densityForBox, densityOf } from '../second/density.ts';
import { zoneFrameMetrics, zoneTitleY } from '../second/header.ts';
import { zoneDashboardsFor, zoneLetterWidth, zoneWidget } from './pages.ts';

const { not } = ncalc;

export const FACE_SCREEN_NAME = 'Main';
/**
 * The same face with the rev bar's room given back to the body, shown when `OpenDash.RevBar` is
 * `off`. Two screens rather than two packages: a driver flips a switch in the panel and the face in
 * front of them changes, which is what every other setting here does, and SimHub picks the screen
 * for them -- `EditorModel.CheckGameModeScreen` re-evaluates every `ScreenEnabledExpression` each
 * frame and moves off a screen that has stopped being enabled (verified against SimHub 9.12.6).
 */
export const FACE_SCREEN_NAME_NO_REV_BAR = 'Main, rev bar off';

/** The zones a face embeds, each with the size its dashboard is drawn for. */
export const zonesOf = (layout: ZoneLayout): { zone: FaceZone; size: { width: number; height: number }; corners: boolean }[] =>
  FACE_ZONE_LETTERS.map((zone) => {
    const r = rectOf(layout, zone);
    return { zone, size: { width: r.width, height: r.height }, corners: zone === 'D' && layout.bandCorners };
  });

/**
 * The items of one arrangement of a zone face.
 *
 * `revBar` false leaves out the well and the segments entirely rather than hiding them: the layout
 * it is given has already moved the rest of the face up into the room they were taking, so there is
 * nothing left to hide them in.
 */
export function faceItems(layout: ZoneLayout, { revBar: withRevBar = true }: { revBar?: boolean } = {}): Item[] {
  const z = layout.zones;
  const items: Item[] = [];

  if (withRevBar) {
    // The well the rev bar has sat in since the first token file named it, and which was never drawn.
    items.push(band('well', z.revBarWell, ds.purpose.block.well));
    items.push(...revBar({ left: z.revBar.left, top: z.revBar.top, width: z.revBar.width, height: z.revBar.height, gap: ds.space[2] }, 'revBar'));
  }

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
    items.push(...zoneLetter(zone, rectOf(layout, zone)));
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
 * The letter in a zone's header, drawn by the face rather than by the zone.
 *
 * Zones B and C are the same rectangle on most faces, so one dashboard file serves both; a letter
 * inside it would say B in each. The face is the only thing that knows which rect is which, so the
 * letter is drawn here, over the widget, in the gap the header keeps for it.
 *
 * Zones A and D carry no header, so they get no letter.
 */
function zoneLetter(zone: FaceZone, r: Rect): Item[] {
  if (zone === 'A' || zone === 'D') return [];
  const density = densityForBox({ width: r.width, height: r.height });
  const { padX } = zoneFrameMetrics(density);
  return [
    label(`zone${zone}.letter`, zone, r.left + padX, zoneTitleY(r, density), zoneLetterWidth(density), {
      size: densityOf(density).labelSm,
      color: ds.color.text.secondary,
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
  /**
   * One per distinct rectangle and catalogue: zone A's four pages, the modules, band D's eight --
   * and again for the rectangles the rev-bar-off arrangement grows, which is two more on a
   * landscape face and one on a portrait one.
   */
  zones: Dashboard[];
}

/** One arrangement of the face, with the expression that decides when SimHub shows it. */
function faceScreen(layout: ZoneLayout, name: string, withRevBar: boolean): Screen {
  return {
    name,
    inGame: true,
    idle: true,
    pit: true,
    backgroundColor: layout.background,
    items: faceItems(layout, { revBar: withRevBar }),
    enabledExpression: withRevBar ? not(setting.revBarIs('off')) : setting.revBarIs('off'),
  };
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
  const off = layoutWithoutRevBar(layout);
  const main: Dashboard = {
    name: layout.folder,
    width: layout.width,
    height: layout.height,
    backgroundColor: layout.background,
    screens: [faceScreen(layout, FACE_SCREEN_NAME, true), faceScreen(off, FACE_SCREEN_NAME_NO_REV_BAR, false)],
    metadata,
  };
  return { main, zones: zoneDashboardsFor([...zonesOf(layout), ...zonesOf(off)], metadata) };
}
