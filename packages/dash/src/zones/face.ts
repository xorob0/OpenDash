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
import { FACE_SIZES, FACE_ZONE_LETTERS, setting, zone as zoneSetting, zoneCounter, type FaceSize, type FaceZone } from '../contract.ts';
import { withBindings } from '../bind.ts';
import { revBar } from '../components/revBar.ts';
import { band } from '../elements/band.ts';
import { rule } from '../elements/rule.ts';
import { flagStrip, FLAG_STRIP_STYLES } from '../components/flagStrip.ts';
import { flagFull } from '../components/flagFull.ts';
import { pitAlerts } from '../components/pitAlerts.ts';
import { popUps } from '../components/popUp.ts';
import { ds } from '../tokens.ts';
import { bar } from './bar.ts';
import { bodyRect, layoutWithoutRevBar, rectOf, type ZoneLayout } from './layout.ts';
import { label } from '../elements/label.ts';
import { measureText } from '../design/advances.ts';
import { densityForBox } from '../second/density.ts';
import { zoneCounterX, zoneCounterWidth, zoneFrameMetrics, zoneTitleY } from '../second/header.ts';
import { bandMetrics } from './bandPages.ts';
import { widestCounter, zoneDashboardsFor, zoneLetterWidth, zoneWidget } from './pages.ts';

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

/**
 * The contract's entry for a layout, which is what names its settings.
 *
 * Looked up rather than constructed: the contract is the list the plugin mirrors, so a layout it
 * does not name has no properties and that is a build error rather than a face with a prefix
 * nobody attached.
 */
export const sizeOf = (layout: ZoneLayout): FaceSize => {
  const face = FACE_SIZES.find((f) => f.width === layout.width && f.height === layout.height);
  if (!face) throw new Error(`${layout.folder} is ${layout.width} by ${layout.height}, which FACE_SIZES does not name`);
  return face;
};

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
    items.push(...revBar({ left: z.revBar.left, top: z.revBar.top, width: z.revBar.width, height: z.revBar.height, gap: layout.revBarGap }, 'revBar'));
  }

  if (z.bar && layout.bar) {
    items.push(band('bar.ground', z.bar, ds.purpose.block.well));
    items.push(...bar(z.bar, 'bar.', { fieldsPerEnd: layout.barFieldsPerEnd, face: sizeOf(layout), scale: layout.bar }));
  }

  // Band D sits in the same well as the bar: the artboards draw both recessed against the body, and
  // the two settled strips reading as one material is what makes the changeable middle read as the
  // changeable part. Drawn here as well as by the band's own screens, so that the face is right on
  // its own -- a face whose zone D widget has not resolved would otherwise show base colour where
  // the drawing has a well.
  items.push(band('band.ground', z.band, ds.purpose.block.well));

  // One pixel between the zones, because a rule is the whole boundary where a block would be too
  // much. That is the reason most of the face is bare.
  for (const [name, gapLeft] of [
    ['rule.ba', z.zoneA.left - 1],
    ['rule.ac', z.zoneC.left - 1],
  ] as const) {
    if (gapLeft > z.zoneB.left) items.push(rule(name, gapLeft, z.zoneB.top, 1, z.zoneB.height));
  }

  // The same pixel across the face: the artboards leave an empty row above every row of the body
  // and above band D, and draw the rule in it. Read off the rects rather than tabulated per face,
  // so that the portrait face, which stacks its zones into four rows, and the arrangement that
  // gives the rev bar's room back are both right without a second table.
  const rowTops = [...new Set([z.zoneA.top, z.zoneB.top, z.zoneC.top])].sort((a, b) => a - b);
  const startingAt = (top: number): string => (['A', 'B', 'C'] as const).filter((zone) => rectOf(layout, zone).top === top).join('');
  const across: [string, number][] = rowTops.map((top, i) => [i === 0 ? 'rule.body' : `rule.zone${startingAt(top)}`, top]);
  across.push(['rule.band', z.band.top]);
  for (const [name, top] of across) {
    // A rule is a boundary between two parts, and the top edge of the face is not one: the nano
    // with its rev bar off starts its body on row 1, with only the face's margin above it.
    if (top > 1) items.push(rule(name, 0, top - 1, layout.width, 1));
  }

  const face = sizeOf(layout);
  for (const zone of FACE_ZONE_LETTERS) {
    items.push(zoneWidget(`zone${zone}`, face, zone, rectOf(layout, zone)));
    items.push(...zoneHeaderParts(face, zone, rectOf(layout, zone)));
  }

  // A flag takes the band over, because an alert outranks fuel. The same sixty pixels goes to
  // whichever has the better claim, which is why the face has no separate flag strip.
  //
  // Both formats are drawn and one is shown, the way both rev bar arrangements are: a driver flips
  // the switch in the panel and the face in front of them changes. The format is asked once, on the
  // group, rather than on each of the catalogue's fifteen conditions inside it, and a group whose
  // Visible is false has its children's bindings left unevaluated, so the format that is not chosen
  // costs nothing while it is not showing. That matters more than it did: the band draws all
  // fifteen now, where it drew the six properties SimHub normalises.
  items.push({
    kind: 'layer',
    name: 'flag',
    children: flagStrip(z.band, FLAG_STRIP_STYLES.standard, 'flag'),
    ...withBindings({ Visible: zoneSetting.flagFormatIs(face, 'band') }),
  });

  // The other format: the flag takes zones B, A and C together, which costs the gear for as long as
  // it is out and is the trade the setting exists to offer. The rectangle is the body of whichever
  // face this is rather than one of eight tabulated ones, so the portrait face, the nano and the
  // arrangement without the rev bar are all right without a second table.
  items.push({
    kind: 'layer',
    name: 'flagFull',
    children: flagFull(bodyRect(layout), 'flagFull'),
    ...withBindings({ Visible: zoneSetting.flagFormatIs(face, 'full') }),
  });

  // The pit family covers zone A rather than taking room of its own: one of them is true for
  // seconds at a time and it is the one thing that matters while it is. Drawn last, so it is over
  // the zone -- and over the full-screen flag, which covers this rectangle too: a driver serving a
  // stop under a red flag still has to know whether the limiter is on.
  items.push(...pitAlerts(z.pitLimiter, 'pitAlert'));

  // A pop-up covers the hero, which on this face is zone A: the gear and the speed are what a
  // driver can give up for the three seconds a lap time is worth more than either. The box is
  // centred on that rectangle rather than placed, so it clears the limiter banner at the top of the
  // column, the bar of settled values above it and band D below, where a flag has the better claim
  // on the same sixty pixels. Drawn after the limiter, which is the only other thing over a zone.
  items.push(...popUps(z.zoneA, 'popUp'));

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
 * Zone A carries no header, so it gets neither. Band D carries the letter alone: the artboards
 * open the band with a D in the same ink as the zone letters, and its pages count nothing, being
 * eight fields across a strip rather than a cycle a driver pages through deliberately.
 */
function zoneHeaderParts(face: FaceSize, zone: FaceZone, r: Rect): Item[] {
  if (zone === 'A') return [];
  if (zone === 'D') return [bandLetter(r)];
  const density = densityForBox({ width: r.width, height: r.height });
  const metrics = zoneFrameMetrics(density, 'face');
  const size = metrics.size;
  const y = zoneTitleY(r, metrics);
  const counter = { kind: 'reserved', widest: widestCounter(zone, size) } as const;
  return [
    label(`zone${zone}.letter`, zone, r.left + metrics.padX, y, zoneLetterWidth(size), { size }),
    label(`zone${zone}.counter`, counter.widest, zoneCounterX(r, counter, metrics), y, zoneCounterWidth(counter, metrics), {
      size,
      hAlign: 'right',
      bind: zoneCounter(face, zone),
      widest: counter.widest,
    }),
  ];
}

/**
 * Band D's letter, centred on the height of the band rather than on a header row it has none of.
 *
 * Drawn by the face for the same reason B's and C's are: the band's dashboard is one file per
 * rectangle and knows neither which zone it is serving nor that there is a letter. `bandMetrics`
 * is the table the band already lays itself out from, so the letter and the rank it stands before
 * take their padding from the same row of it.
 */
function bandLetter(r: Rect): Item {
  const size = ds.size.label;
  const width = Math.ceil(measureText('BarlowMedium', 'D', size)) + 2;
  return label('zoneD.letter', 'D', r.left + bandMetrics(r).padX, r.top + (r.height - size) / 2, width, { size });
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

/**
 * One arrangement of the face, with the expression that decides when SimHub shows it.
 *
 * The name follows the flag rather than being passed beside it, so a screen cannot end up named for
 * one arrangement and drawn as the other.
 */
function faceScreen(layout: ZoneLayout, withRevBar: boolean): Screen {
  return {
    name: withRevBar ? FACE_SCREEN_NAME : FACE_SCREEN_NAME_NO_REV_BAR,
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
    screens: [faceScreen(layout, true), faceScreen(off, false)],
    metadata,
  };
  // Both arrangements' rectangles, deduplicated by zoneDashboardsFor: the zones the rev bar's room
  // does not reach keep the one dashboard they already had.
  return { main, zones: zoneDashboardsFor(sizeOf(layout), [...zonesOf(layout), ...zonesOf(off)], metadata) };
}
