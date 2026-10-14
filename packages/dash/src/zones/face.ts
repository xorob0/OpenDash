/**
 * A zone face: the five parts composed into a screen, twice.
 *
 * The rev bar in its well, the bar of settled values, zones B, A and C across the body, and band D
 * across the foot. Every zone is a widget pointing at a dashboard of its catalogue, with its screen
 * index bound to a plugin property, so a wheel button that increments that property changes the
 * page and nothing in the scene graph has to know.
 *
 * Twice, because `OpenDash.RevBar` `off` is not a hidden rev bar but a differently arranged screen:
 * the two arrangements are built here and SimHub shows whichever the setting enables. #189.
 *
 * What this file does *not* do is decide any geometry. Every rectangle comes from the regions a
 * theme's anatomy declares, which under the default theme are the layout's own, read off an
 * artboard; see `docs/design/zones.md`.
 */
import type { Dashboard, DashboardMetadata, Item, LayerItem, Rect, Screen } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { FACE_SIZES, FACE_ZONE_LETTERS, pagesForZone, setting, zone as zoneSetting, zoneCounter, type FaceSize, type FaceZone, type ThemeEntry } from '../contract.ts';
import { visibleOnlyWhile, withMoreBindings, type Expr } from '../bind.ts';
import { revBar } from '../components/revBar.ts';
import { band } from '../elements/band.ts';
import { rule } from '../elements/rule.ts';
import { flagSettled, flagSettledWidth, flagStrip, flagTakingBand, FLAG_STRIP_STYLES } from '../components/flagStrip.ts';
import { measureText } from '../design/advances.ts';
import { flagFull, flagFullOut } from '../components/flagFull.ts';
import { alertCondition, anyBandRaised } from '../flags.ts';
import { PIT_ALERTS, pitAlerts, pitAlertVisible } from '../components/pitAlerts.ts';
import { lastingPopUpFrame, lastingPopUps, popUpRoom, transientPopUps } from '../components/popUp.ts';
import { changeNotifications } from '../components/changeNotification.ts';
import { overlaps, rect } from '../design/geometry.ts';
import { idleScreen } from '../idle.ts';
import { lapReview, lapReviewFrame, lapReviewOut } from '../components/lapReview.ts';
import { ds } from '../tokens.ts';
import { bar } from './bar.ts';
import { regionsWithoutRevBar, zoneRegions, type ZoneLayout } from './layout.ts';
import { optionalRegionRect, regionRect, zoneRect, type Regions } from '../themes/anatomy.ts';
import type { FaceContext, ThemeDrawing } from '../themes/drawing.ts';
import { label } from '../elements/label.ts';
import { densityForBox } from '../second/density.ts';
import { zoneCounterX, zoneCounterWidth, zoneFrameMetrics, zoneTitleY } from '../second/header.ts';
import { widestCounter, zoneAGearInk, zoneDashboardsFor, zoneWidget } from './pages.ts';
import { walkItems } from '../walk.ts';

const { and, not, or } = ncalc;

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
export const zonesOf = (layout: ZoneLayout, regions: Regions = zoneRegions(layout)): { zone: FaceZone; size: { width: number; height: number }; corners: boolean }[] =>
  FACE_ZONE_LETTERS.map((zone) => {
    const r = zoneRect(regions, zone);
    return { zone, size: { width: r.width, height: r.height }, corners: zone === 'D' && layout.bandCorners };
  });

/**
 * The items of one arrangement of a zone face.
 *
 * `revBar` false leaves out the well and the segments entirely rather than hiding them: the regions
 * it is given have already moved the rest of the face up into the room they were taking, so there
 * is nothing left to hide them in.
 *
 * `regions` is where the theme puts each part, and the house face's own rectangles when no theme
 * says otherwise. Everything else, the background, the rev bar's gap and the bar's scale, is read
 * from `layout`.
 */
export function faceItems(
  layout: ZoneLayout,
  { revBar: withRevBar = true, regions = zoneRegions(layout), drawing = {}, theme }: { revBar?: boolean; regions?: Regions; drawing?: ThemeDrawing; theme?: ThemeEntry } = {},
): Item[] {
  const items: Item[] = [];
  const band_ = regionRect(regions, 'band');
  const body = (['A', 'B', 'C'] as const).map((zone) => ({ zone, rect: zoneRect(regions, zone) }));
  const ctx: FaceContext = { layout, face: sizeOf(layout), regions, withRevBar };

  if (withRevBar && drawing.revBar) {
    items.push(...drawing.revBar(ctx));
  } else if (withRevBar) {
    const segments = regionRect(regions, 'revBar');
    // The well the rev bar has sat in since the first token file named it, and which was never drawn.
    items.push(band('well', regionRect(regions, 'revBarWell'), ds.purpose.block.well));
    items.push(
      ...revBar(
        { left: segments.left, top: segments.top, width: segments.width, height: segments.height, gap: layout.revBarGap },
        'revBar',
        zoneSetting.revBar(sizeOf(layout)),
      ),
    );
  }

  const barRect = optionalRegionRect(regions, 'bar');
  if (barRect && drawing.bar) {
    items.push(...drawing.bar(ctx));
  } else if (barRect && layout.bar) {
    items.push(band('bar.ground', barRect, ds.purpose.block.well));
    items.push(...bar(barRect, 'bar.', { fieldsPerEnd: layout.barFieldsPerEnd, face: sizeOf(layout), scale: layout.bar }));
  }

  items.push(...(drawing.chrome ? drawing.chrome(ctx) : houseChrome(layout, band_, body)));

  const face = sizeOf(layout);
  for (const zone of FACE_ZONE_LETTERS) {
    items.push(zoneWidget(`zone${zone}`, face, zone, zoneRect(regions, zone), theme));
    items.push(...zoneHeaderParts(face, zone, zoneRect(regions, zone), drawing));
  }

  // A flag takes the band over, because an alert outranks fuel. The same sixty pixels goes to
  // whichever has the better claim, which is why the face has no separate flag strip.
  //
  // Both formats are drawn and one is shown, the way both rev bar arrangements are: a driver flips
  // the switch in the panel and the face in front of them changes. The format is asked once, on the
  // group, rather than on each of the catalogue's twenty conditions inside it, and a group whose
  // Visible is false has its children's bindings left unevaluated, so the format that is not chosen
  // costs nothing while it is not showing. That matters more than it did: the band ranks all
  // twenty now, where it drew the six properties SimHub normalises.
  //
  // The band format itself is two groups rather than one, #380: the flag takes the whole band for
  // the few seconds after it comes out or changes, and then settles into one rectangle of the face
  // that no reading uses, #731, which gives the page a driver was reading back while the flag is
  // still out. One window decides between them and it is asked twice, once per group, rather than
  // thirty times; SimHub keys that window by the text of the expression rather than by the item
  // asking, so the two groups share it and the band can never be in both phases or in neither.
  //
  // The settled group is listed here, after every zone widget, wherever its rectangle is: a widget
  // paints its dashboard's ground over whatever was drawn before it, so a flag listed before the
  // zone it sits on would be hidden by it.
  const takingBand = flagTakingBand();
  const bandFormat = zoneSetting.flagFormatIs(face, 'band');
  items.push(withMoreBindings({
    kind: 'layer',
    name: 'flag',
    children: flagStrip(band_, FLAG_STRIP_STYLES.standard, 'flag'),
  }, { Visible: and(bandFormat, takingBand) }));
  const settledFlag = drawing.flagSettled ? drawing.flagSettled(ctx) : houseFlagSettled(regions, drawing);
  const settledConditions = flagSettled(settledFlag, FLAG_STRIP_STYLES.standard, 'flagCorner');
  items.push(withMoreBindings({
    kind: 'layer',
    name: 'flagCorner',
    children: settledConditions,
  }, { Visible: and(bandFormat, not(takingBand)) }));

  // The other format: the flag takes zones B, A and C together, which costs the gear for as long as
  // it is out and is the trade the setting exists to offer. The rectangle is the body of whichever
  // face this is rather than one of eight tabulated ones, so the portrait face, the nano and the
  // arrangement without the rev bar are all right without a second table.
  items.push(withMoreBindings({
    kind: 'layer',
    name: 'flagFull',
    children: flagFull(regionRect(regions, 'flagBody'), 'flagFull'),
  }, { Visible: zoneSetting.flagFormatIs(face, 'full') }));

  // The pit family covers zone A rather than taking room of its own: one of them is true for
  // seconds at a time and it is the one thing that matters while it is. Drawn last, so it is over
  // the zone -- and over the full-screen flag, which covers this rectangle too: a driver serving a
  // stop under a red flag still has to know whether the limiter is on.
  const restyled = (drawn: Item[], boxes: 'inverse' | 'outline'): Item[] => (drawing.restyle ? drawing.restyle(drawn, { boxes }) : drawn);
  const banner = regionRect(regions, 'pitAlert');
  const pitFamily = restyled(pitAlerts(banner, 'pitAlert'), 'inverse');
  items.push(...pitFamily);

  // Every box of the pop-up family is drawn after the full-screen flag and the pit family, being over
  // the zones for its seconds, and none of them is drawn over either of those while it is out: a box
  // that would cover a safety layer waits for it (#1069, #1070). `givingWay` reads which layers each
  // box would cover off what this face draws, the house's banner and a theme's takeovers alike.
  const flagBody = regionRect(regions, 'flagBody');
  const takeovers = drawing.takeovers ? drawing.takeovers(ctx) : [];
  const giveWay = givingWay([
    { out: and(zoneSetting.flagFormatIs(face, 'full'), flagFullOut()), rects: [flagBody], family: 'flag' },
    { out: and(bandFormat, not(takingBand), anyBandRaised(settledConditions.map((layer) => alertCondition(layer.name.slice('flagCorner.'.length))))), rects: [settledFlag], family: 'flag' },
    ...pitStatesDrawn([...pitFamily, ...takeovers]),
  ]);

  // A pop-up that holds for as long as its condition does, low fuel for the last laps of a stint and
  // DRS for a straight, never covers the gear (#1047). It takes the pit banner's row, which every
  // theme puts where a state may sit for as long as the limiter is on in the lane, cut short where
  // the gear's ink on any of zone A's pages reaches into it, and it waits for the pit family, whose
  // row it is, and for the full-screen flag, whose name it reaches on the Porsche's nano and the
  // AiM's portrait face. Drawn before a theme's takeovers, which are states of the car that hold too
  // and are drawn over whatever they take: the AiM's engine warning over zone C, where its banner row is.
  const row = lastingPopUpFrame(banner, zoneAGearInk(zoneRect(regions, 'A'), drawing));
  items.push(...giveWay(restyled(lastingPopUps(row, 'popUp'), 'outline'), { pitFamily: 'waits already' }));
  items.push(...takeovers);

  // A pop-up that is an event, the lap time for the three seconds after the line, is drawn as a real
  // pop-up: the sheet's 560 by 120 centred on the hero, over the gear and over zones B and C either
  // side for those seconds (#1047). It is kept inside the body, off the rev bar, the bar of settled
  // values and band D, where a flag has the better claim on the same sixty pixels, and off the pit
  // banner: a theme whose hero is short brings a centred box up over the banner, so the box is
  // centred on the part of the hero the banner leaves wherever that happens (#1052). It does not open
  // over the full-screen flag, nor over a theme's drawing of a pit state, as the Porsche's limiter
  // body and the AiM's `Pit Speed` are, while that state is out.
  const hero = regionRect(regions, 'hero');
  const room = popUpRoom(hero, banner, flagBody);
  items.push(...giveWay(restyled(transientPopUps(room, flagBody, 'popUp'), 'outline')));

  // And the smaller box of the same kind, on the same rectangle: a car setting that has just moved,
  // for the three seconds SimHub's own window holds it. Ranked under the lap-time pop-up inside the
  // component, so a lap time at the line is never covered by a click of traction control, and under
  // the safety layers here, as the pop-up is. The house's box never reaches the banner, so a dial
  // turned in the pit box still says where it landed.
  items.push(...giveWay(drawing.changeNotifications ? drawing.changeNotifications(ctx) : restyled(changeNotifications(room, flagBody, 'notice'), 'outline')));

  // The largest of the family, last, and on the same rectangle again: the debrief of the lap just
  // finished, for the four seconds after the line.
  //
  // Against the rest of the family it is ranked by geometry rather than by an exclusion chain, which
  // is the one place this face does that and is worth saying why. The lap-time pop-up and the
  // notification are centred on the same hero and kept inside the same body, at the sheet's 560 by
  // 120 and 400 by 96, and the review is larger than both in both directions and is drawn over them,
  // so the two conditions that are true at the same moment -- a lap time at the line and a review of
  // that lap -- cannot both be read. A chain would have to reach into `popUp.ts`, whose conditions
  // know nothing of a face and so could not ask which face's setting is on. A lasting pop-up in the
  // banner row is covered for the review's four seconds where the review reaches the row, as on the
  // nano, and is out again after them.
  //
  // The pit family is the exception, and is ranked by condition: the review does not open while any
  // state of the family is out (`lapReviewOut`, #1053), whether or not it would reach that state's
  // drawing. On the nano the body is 194 px and a 160 px panel reaches the banner, on the Porsche's
  // faces it reaches the banner at the top of the gear's tile, and a theme draws the limiter state
  // over regions of its own, the Porsche over the whole body the review is clamped to and the AiM
  // over zone C. The full-screen flag is the other, as it is for every box of the family.
  items.push(...giveWay(restyled([lapReview(lapReviewFrame(hero, flagBody), lapReviewOut(face), 'lapReview')], 'outline'), { pitFamily: 'waits already' }));

  return items;
}

/** The Visible of each state of the pit family, by which a layer drawing that state is known. */
const PIT_STATES: ReadonlySet<Expr> = new Set(PIT_ALERTS.map((spec) => pitAlertVisible(spec.id)));

/** Every rectangle the items draw, at any depth, layers being groups rather than drawings. */
const drawnRects = (items: readonly Item[]): Rect[] => [...walkItems(items)].flatMap((item) => (item.kind === 'layer' ? [] : [item.rect]));

/** A layer's own Visible, or undefined where it has none or it is not a formula. */
const visibleOf = (item: Item): Expr | undefined => {
  const visible = item.bindings?.Visible;
  return visible?.mode === 'formula' && typeof visible.formula === 'string' ? visible.formula : undefined;
};

/** A layer the pop-up family may not be drawn over while it is out: when it is out, what it draws, and which it is. */
interface SafetyLayer {
  out: Expr;
  rects: readonly Rect[];
  family: 'flag' | 'pit';
}

/**
 * Every layer of `items` that draws a state of the pit family, known by its Visible being one of the
 * family's: the house's banners, and a theme's takeover shown on one of the family's conditions.
 *
 * Known by the text, so an item whose Visible joins something to a state's, say the limiter's and
 * "in the car", would no longer be known as drawing it, and the boxes over it would stop waiting for
 * it without a word; `popUpRegions.test.ts` finds the family the same way and would stop asking. Such
 * an item stops the build instead, to be drawn inside a layer shown on the state alone.
 */
const pitStatesDrawn = (items: readonly Item[]): SafetyLayer[] => {
  for (const item of walkItems(items)) {
    const visible = visibleOf(item);
    if (visible === undefined || PIT_STATES.has(visible)) continue;
    const joined = [...PIT_STATES].find((state) => visible.includes(state));
    if (joined !== undefined) {
      throw new Error(`face: ${item.name ?? item.kind} is shown on a state of the pit family joined to something else, so the pop-up family cannot tell it draws that state: ${visible}`);
    }
  }
  return [...walkItems(items)]
    .filter((item): item is LayerItem => item.kind === 'layer' && PIT_STATES.has(visibleOf(item) ?? ''))
    .map((layer) => ({ out: visibleOf(layer)!, rects: drawnRects(layer.children), family: 'pit' }));
};

/**
 * How the pop-up family gives way to the safety layers drawn in the body (#1069, #1070): the returned
 * function takes a member's boxes and hides each one while a safety layer it would be drawn over is
 * out.
 *
 * - **The flag.** The full-screen block, on a face whose format is `full`, while it draws a
 *   condition: it is the whole of `flagBody`, so every box inside the body waits for it, and a driver
 *   who chose the flag that cannot be missed reads its name whole at the line. And the settled flag,
 *   in the band format once the flag has had band D for its seconds, in the one rectangle #731 keeps
 *   for it, which only the nano's lap review reaches.
 * - **A state of the pit family**, while that state is out, wherever the face draws it under the box:
 *   the house's banner, and a theme's takeover that is shown on one of the family's conditions, as
 *   the Porsche's limiter body and the AiM's `Pit Speed` are. A member whose own condition already
 *   waits for the whole family, the lasting pop-ups and the lap review, passes
 *   `pitFamily: 'waits already'` and is asked about the flag alone.
 *
 * Which layers a box would cover is read off the rectangles drawn, so a theme that draws a state
 * somewhere new is ranked without a word here, and a box that covers none of them, as the house's
 * change notification covers no banner, still opens in the lane. The condition is joined to each
 * box's own, after it, so it costs nothing while the box is not out.
 */
function givingWay(safety: readonly SafetyLayer[]): (boxes: Item[], opts?: { pitFamily?: 'covered' | 'waits already' }) => Item[] {
  return (boxes, { pitFamily = 'covered' } = {}) =>
    boxes.map((box) => {
      if (box.kind !== 'layer') throw new Error(`${box.name}: a box of the pop-up family is a layer, so that it can be hidden whole`);
      const drawn = drawnRects([box]);
      const covers = (layer: SafetyLayer): boolean => layer.rects.some((r) => drawn.some((d) => overlaps(r, d)));
      const outs = new Set(safety.filter((layer) => (layer.family === 'flag' || pitFamily === 'covered') && covers(layer)).map((layer) => layer.out));
      return outs.size === 0 ? box : visibleOnlyWhile(box, not(or(...outs)));
    });
}

/**
 * The house face's own chrome: band D's well and the one-pixel rules between the parts, which is
 * what a theme's `chrome` replaces.
 */
function houseChrome(layout: ZoneLayout, band_: Rect, body: readonly { zone: 'A' | 'B' | 'C'; rect: Rect }[]): Item[] {
  const items: Item[] = [];
  // Band D sits in the same well as the bar: the artboards draw both recessed against the body, and
  // the two settled strips reading as one material is what makes the changeable middle read as the
  // changeable part. Drawn here as well as by the band's own screens, so that the face is right on
  // its own -- a face whose zone D widget has not resolved would otherwise show base colour where
  // the drawing has a well.
  items.push(band('band.ground', band_, ds.purpose.block.well));

  const rowTops = [...new Set(body.map(({ rect: r }) => r.top))].sort((a, b) => a - b);

  // One pixel between the zones, because a rule is the whole boundary where a block would be too
  // much. That is the reason most of the face is bare. Drawn between two zones that start on the
  // same row, named after the pair from left to right, so that a theme which puts the gear at an
  // edge is ruled where its zones meet rather than where the house face's do.
  for (const top of rowTops) {
    const row = body.filter(({ rect: r }) => r.top === top).sort((a, b) => a.rect.left - b.rect.left);
    for (const [left, right] of row.slice(1).map((next, i) => [row[i]!, next] as const)) {
      const gapLeft = right.rect.left - 1;
      if (gapLeft > left.rect.left) items.push(rule(`rule.${left.zone.toLowerCase()}${right.zone.toLowerCase()}`, gapLeft, left.rect.top, 1, left.rect.height));
    }
  }

  // The same pixel across the face: the artboards leave an empty row above every row of the body
  // and above band D, and draw the rule in it. Read off the rects rather than tabulated per face,
  // so that the portrait face, which stacks its zones into four rows, and the arrangement that
  // gives the rev bar's room back are both right without a second table.
  const startingAt = (top: number): string => body.filter(({ rect: r }) => r.top === top).map(({ zone }) => zone).join('');
  const across: [string, number][] = rowTops.map((top, i) => [i === 0 ? 'rule.body' : `rule.zone${startingAt(top)}`, top]);
  across.push(['rule.band', band_.top]);
  for (const [name, top] of across) {
    // A rule is a boundary between two parts, and the top edge of the face is not one: the nano
    // with its rev bar off starts its body on row 1, with only the face's margin above it.
    if (top > 1) items.push(rule(name, 0, top - 1, layout.width, 1));
  }
  return items;
}

/**
 * A zone's page counter, drawn by the face rather than by the zone.
 *
 * Zones B and C are the same rectangle on most faces, so one dashboard file serves both, and a
 * counter inside it would count the catalogue -- "15 / 22" on a cycle of three, because a screen
 * cannot know which zone's mask is deciding its length. The face is the only thing that knows which
 * rect is which zone, so the counter is drawn here, over the widget, in the room the zone's header
 * keeps at its right.
 *
 * Zone A carries no header, so it gets none, and band D counts nothing, being eight fields across a
 * strip rather than a cycle a driver pages through deliberately. No zone draws its letter, the page
 * name being what says what is showing (#708).
 */
function zoneHeaderParts(face: FaceSize, zone: FaceZone, r: Rect, drawing: ThemeDrawing): Item[] {
  if (zone === 'A' || zone === 'D' || drawing.zoneCounters === false) return [];
  const density = densityForBox({ width: r.width, height: r.height });
  const house = zoneFrameMetrics(density, 'face');
  const metrics = drawing.zoneHeaderSize === undefined ? house : { ...house, size: drawing.zoneHeaderSize };
  const size = metrics.size;
  const y = zoneTitleY(r, metrics);
  const counter = { kind: 'reserved', widest: widestCounter(zone, size) } as const;
  return [
    label(`zone${zone}.counter`, counter.widest, zoneCounterX(r, counter, metrics), y, zoneCounterWidth(counter, metrics), {
      size,
      hAlign: 'right',
      bind: zoneCounter(face, zone),
      widest: counter.widest,
    }),
  ];
}

/**
 * Between the settled flag and the two things beside it in zone C's title line, the page name and the
 * counter. Four pixels, because on the 800 x 480 face the room between the widest page name and the
 * counter is seventy-three, and the widest shortest name, YELLOW, wants sixty-four of them.
 */
const FLAG_SETTLED_GAP = 4;

/**
 * Where the house face's flag settles, #731: in zone C's title line, against the page counter, in the
 * room no page name reaches.
 *
 * The title line is the one part of every house face that holds no reading on any page: the page's
 * name at its left, the counter at its right, and nothing between them, with room on every face for
 * every condition's shortest name. It is measured rather than tabulated, from the widest name zone C's
 * catalogue can show, set as the title is set, to the counter's widest reading, each with
 * {@link FLAG_SETTLED_GAP} clear of it. The rectangle is that room or `flagSettledWidth()`,
 * whichever is less, held against the counter, and its height is the title line's, so that it reads as
 * part of the zone's header rather than as a box over the page.
 *
 * Zone C because it is the zone right of the gear on a landscape face and the one under B on the
 * portrait one, and either way it is one place, the same on every face of the house.
 */
export function houseFlagSettled(regions: Regions, drawing: ThemeDrawing = {}): Rect {
  const r = zoneRect(regions, 'C');
  const density = densityForBox({ width: r.width, height: r.height });
  const house = zoneFrameMetrics(density, 'face');
  const metrics = drawing.zoneHeaderSize === undefined ? house : { ...house, size: drawing.zoneHeaderSize };
  const counter = { kind: 'reserved', widest: widestCounter('C', metrics.size) } as const;
  // The counter is right-aligned in a box a little wider than its widest reading, so the room ends where
  // that reading starts rather than at the box.
  const counterRun = zoneCounterX(r, counter, metrics) + zoneCounterWidth(counter, metrics) - measureText('BarlowMedium', counter.widest, metrics.size);
  const right = Math.floor(counterRun) - FLAG_SETTLED_GAP;
  const title = Math.max(...pagesForZone('C').map((page) => measureText('BarlowMedium', page.name, metrics.size)));
  const left = Math.ceil(r.left + metrics.padX + title) + FLAG_SETTLED_GAP;
  const width = Math.min(right - left, flagSettledWidth());
  return rect(right - width, r.top + metrics.padTop, width, metrics.title);
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
function faceScreen(layout: ZoneLayout, regions: Regions, withRevBar: boolean, drawing: ThemeDrawing, theme?: ThemeEntry): Screen {
  return {
    name: withRevBar ? FACE_SCREEN_NAME : FACE_SCREEN_NAME_NO_REV_BAR,
    inGame: true,
    // Not idle any more. A face with no game behind it is a rev bar at zero over three zones of
    // dashes, which is the bug #763 is about; `idle.ts` is the screen SimHub shows instead.
    idle: false,
    pit: true,
    backgroundColor: layout.background,
    items: faceItems(layout, { revBar: withRevBar, regions, drawing, theme }),
    // This face's own answer, not the rig's: a wheel that carries LEDs across its top and a display
    // that does not are two screens on one rig, and the switch used to answer for both at once.
    enabledExpression: withRevBar ? not(zoneSetting.revBarIs(sizeOf(layout), 'off')) : zoneSetting.revBarIs(sizeOf(layout), 'off'),
  };
}

/**
 * A zone face and the dashboards its zones cycle, in the regions a theme gives it or the house
 * face's own, drawn the house's way except where a theme's drawing says otherwise.
 *
 * `theme` is the theme's catalogue entry, which says what band D opens on; left out, the band opens
 * where the house's does.
 */
export function buildZoneFace(layout: ZoneLayout, opts: FaceBuildOptions, regions: Regions = zoneRegions(layout), drawing: ThemeDrawing = {}, theme?: ThemeEntry): BuiltFace {
  const metadata: DashboardMetadata = {
    title: layout.folder,
    author: opts.author,
    description: layout.description,
    version: opts.version,
    simHubVersion: opts.simHubVersion,
  };
  const off = regionsWithoutRevBar(regions);
  const main: Dashboard = {
    name: layout.folder,
    width: layout.width,
    height: layout.height,
    backgroundColor: layout.background,
    // One idle screen for the face and not one per arrangement: the rev bar's setting says how the
    // face is laid out while a game is running, and a rig at rest has no rev bar to arrange. It goes
    // last, so screen 0 is still the face SimHub previews and the tests reach for.
    screens: [faceScreen(layout, regions, true, drawing, theme), faceScreen(layout, off, false, drawing, theme), idleScreen({ frame: rect(0, 0, layout.width, layout.height), background: layout.background })],
    metadata,
  };
  // Both arrangements' rectangles, deduplicated by zoneDashboardsFor: the zones the rev bar's room
  // does not reach keep the one dashboard they already had.
  return { main, zones: zoneDashboardsFor(sizeOf(layout), [...zonesOf(layout, regions), ...zonesOf(layout, off)], metadata, drawing) };
}
