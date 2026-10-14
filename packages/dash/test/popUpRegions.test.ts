/**
 * Every box of the pop-up family, on every face of every theme, inside the region it belongs to
 * (#1047).
 *
 * A pop-up is a function of the regions a face declares, as a module is a function of its zone, so
 * it is held to them the way `secondScreens.test.ts` holds a module to its frame: the face is built,
 * and every item the family draws is found inside the region it belongs to. Which region depends on
 * how long the box stays out, which is the author's ruling on this pull request.
 *
 * - **A transient box**, the lap time at the line, the change notification and the lap review, is
 *   out for seconds and is drawn as a real pop-up over the whole face: inside the body, zones B, A
 *   and C, and clear of the rev bar, the bar of settled values, band D and the pit banner.
 * - **A lasting box**, low fuel and DRS, is out for as long as its condition holds, and **never
 *   covers the gear**. It keeps to the pit banner's row, which the pit family has first, and it is
 *   held clear of the gear's ink on every page of zone A a driver can cycle to, in every arrangement
 *   of every face of every theme, as each theme ships. That is what a 380 px pop-up over the 1920
 *   face's gear broke, for as long as the tank was under its threshold.
 * - **Every box** gives way to the two safety layers drawn in the body, the full-screen flag and the
 *   pit family, by condition: a box drawn over one of them is hidden while it is out (#1053, #1069,
 *   #1070). That is evaluated on every face as each theme ships, at the end of this file.
 *
 * The lap review is the widest of the transient boxes on purpose: it is clamped to the body as the
 * other two are, and is checked here for the deltas each face keeps.
 *
 * The faces are built from each theme's anatomy with the house's drawing, which is how the
 * conformance harness builds a test theme. A theme's own drawing that moves a member of the family
 * elsewhere, as the Porsche's change notification is drawn in zone C's panel, is checked by the
 * harness against its frame; what is checked here is the house's family over the theme's regions,
 * which is the stricter question, since it asks it of every theme's hero.
 *
 * **Each theme is measured in a process of its own.** The family's rectangles are the theme's
 * regions, but what is drawn inside them is measured, and a theme's overlay changes the face the
 * figures are set in: the Porsche's are Barlow at normal width and the AiM's are the seven-segment
 * face, both wider than the house's condensed figures. A fit taken in the default process is one
 * the theme's package never draws, which is how the Porsche's portrait review was once said to keep
 * both deltas that its own build sheds (#1047). So the default process checks the themes that draw
 * in its fonts and starts this file again once for each of the others, as `conformance.test.ts` does.
 */
import { afterAll, describe, expect, test } from 'bun:test';
import { LAP_REVIEW_PAD_X, LAP_REVIEW_PAD_X_NARROW } from '../src/components/lapReview.ts';
import { PIT_ALERTS, pitAlertOut, pitAlertVisible } from '../src/components/pitAlerts.ts';
import { FUEL_POP_UP, POP_UP_WIDTH, popUpVisible, popUpsOfKind } from '../src/components/popUp.ts';
import { FACE_SIZES, flagFormatSettingName, lapReviewSettingName, type FaceSize } from '../src/contract.ts';
import { ALERT_CATALOGUE, alertCondition, flagBit, isFlag, type AlertCondition } from '../src/flags.ts';
import { hasSetting, TRACKED_VALUES } from '../src/second/tracked.ts';
import { evalNcalc, type Calls, type Props } from './ncalcEval.ts';
import { ncalc, type Item, type LayerItem, type Rect, type TextItem } from '../src/generator.ts';
import { contains, overlaps, translate } from '../src/design/geometry.ts';
import { inkOf } from '../src/design/metrics.ts';
import { optionalRegionRect, regionRect, zoneRect, type Regions } from '../src/themes/anatomy.ts';
import { buildThemeFace, drawsInThisProcess } from '../src/themes/faces.ts';
import { THEME_DRAWINGS } from '../src/themes/drawings.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, faceItems, layoutWithoutRevBar, regionsWithoutRevBar, type ZoneLayout } from '../src/zones/index.ts';
import { GEAR_LEFT_THEME_ID, gearLeftTheme } from './fixtures/gearLeftTheme.ts';

const { not } = ncalc;

// The test theme with the gear at the left edge is the case a centred box was most wrong on, so it
// is asked too, and taken out again once this file has run.
THEMES[GEAR_LEFT_THEME_ID] = gearLeftTheme;
afterAll(() => {
  delete THEMES[GEAR_LEFT_THEME_ID];
});

interface Face {
  name: string;
  theme: string;
  /** `1920x480`, the key the tables below are written in. */
  size: string;
  layout: ZoneLayout;
  regions: Regions;
  revBar: boolean;
  items: Item[];
}

/** The themes this process draws in their own fonts: the others are checked by a process of their own. */
const here = Object.keys(THEMES).filter(drawsInThisProcess);

/** Every face of every theme this process draws, in both arrangements. */
const faces: Face[] = here.flatMap((id) =>
  THEMES[id]!.anatomy.sizes.flatMap((size) => {
    const layout = ZONE_FACES.find((face) => face.width === size.width && face.height === size.height)!;
    const on = THEMES[id]!.anatomy.regions(layout);
    const off = regionsWithoutRevBar(on);
    const offLayout = layoutWithoutRevBar(layout);
    const key = `${size.width}x${size.height}`;
    return [
      { name: `${id} ${key}`, theme: id, size: key, layout, regions: on, revBar: true, items: faceItems(layout, { regions: on }) },
      { name: `${id} ${key}, rev bar off`, theme: id, size: key, layout: offLayout, regions: off, revBar: false, items: faceItems(offLayout, { regions: off, revBar: false }) },
    ];
  }),
);

/** The parts a member of the family draws, found by the prefix the face gives it. */
const partsOf = (items: Item[], prefix: string): Item[] =>
  items.flatMap((item) => [...walkItems([item])]).filter((item) => item.kind !== 'layer' && item.name.startsWith(prefix));

/** What no box of the family may cover, on any face: what carries a state of its own outside the body. */
const settled = (face: Face): [string, Rect | undefined][] => [
  ['the rev bar well', face.revBar ? regionRect(face.regions, 'revBarWell') : undefined],
  ['the bar', optionalRegionRect(face.regions, 'bar')],
  ['band D', regionRect(face.regions, 'band')],
];

/** The prefixes the transient pop-ups and the change notifications are drawn under. */
const TRANSIENT = [...popUpsOfKind('transient').map((spec) => `popUp.${spec.id}.`), 'notice.'];
/** The prefixes the lasting pop-ups are drawn under. */
const LASTING = popUpsOfKind('lasting').map((spec) => `popUp.${spec.id}.`);

const partsOfAll = (items: Item[], prefixes: readonly string[]): Exclude<Item, LayerItem>[] =>
  prefixes.flatMap((prefix) => partsOf(items, prefix)).filter((item): item is Exclude<Item, LayerItem> => item.kind !== 'layer');

/**
 * The lap time at the line and the change notification are out for three seconds, and the author's
 * ruling is that a box out for that long may be a real pop-up over the whole face rather than kept to
 * zone A (#1047): the sheet's 560 by 120 and 400 by 96, over the gear and over zones B and C for
 * those seconds. What it may still not cover is what lies outside the body, and the pit banner.
 */
describe('the transient pop-up and the change notifications are real pop-ups, inside the body', () => {
  for (const face of faces) {
    test(`${face.name}`, () => {
      const body = regionRect(face.regions, 'flagBody');
      const parts = partsOfAll(face.items, TRANSIENT);
      expect(parts.length).toBeGreaterThan(0);
      for (const item of parts) {
        expect({ face: face.name, item: item.name, rect: item.rect, body, inside: contains(body, item.rect) }).toMatchObject({ inside: true });
        for (const [what, box] of settled(face)) {
          if (!box) continue;
          expect({ face: face.name, item: item.name, what, covered: overlaps(box, item.rect) }).toMatchObject({ covered: false });
        }
      }
      // And at the sheet's size wherever the body has the room, which is every face that ships.
      const box = parts.find((item) => item.name === 'popUp.lap.box')!;
      expect({ face: face.name, width: box.rect.width }).toEqual({ face: face.name, width: Math.min(POP_UP_WIDTH, body.width) });
    });
  }
});

/**
 * The pit banner is inside the hero on every face, at the top of zone A on the house's and of the
 * gear's tile on the Porsche's, and it is the one thing there with a state of its own: a pop-up
 * centred on a short hero once covered its bottom on the Porsche's 1280 x 400 and 600 x 686, and on
 * the portrait face the lower half of `Engage limiter` with it (#1052). Asked of the transient boxes
 * over the theme's regions, and again of the face as the theme ships it, where the banner is the one
 * the face draws and a theme's own notifications are drawn where the theme puts them. The lasting
 * pop-ups share the banner's row and give it up by condition, which is asked further down.
 */
describe('the transient pop-up and the change notifications keep clear of the pit banner', () => {
  for (const face of faces) {
    test(`${face.name}`, () => {
      const banner = regionRect(face.regions, 'pitAlert');
      const parts = partsOfAll(face.items, TRANSIENT);
      expect(parts.length).toBeGreaterThan(0);
      for (const item of parts) {
        expect({ face: face.name, item: item.name, rect: item.rect, banner, covered: overlaps(banner, item.rect) }).toMatchObject({ covered: false });
      }
    });
  }
});

/** The lasting pop-ups are drawn in the pit banner's row and nowhere else. */
describe('the lasting pop-ups keep to the pit banner\'s row', () => {
  for (const face of faces) {
    test(`${face.name}`, () => {
      const banner = regionRect(face.regions, 'pitAlert');
      const parts = partsOfAll(face.items, LASTING);
      expect(parts.length).toBeGreaterThan(0);
      for (const item of parts) {
        expect({ face: face.name, item: item.name, rect: item.rect, banner, inside: contains(banner, item.rect) }).toMatchObject({ inside: true });
      }
    });
  }
});

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

/** Every item a screen draws, layers walked through. */
const drawnOn = (items: readonly Item[]): Exclude<Item, LayerItem>[] => [...walkItems(items)].filter((item): item is Exclude<Item, LayerItem> => item.kind !== 'layer');

/**
 * The gear's ink on every page of zone A, as the face ships it: the zone A widget's rectangle on the
 * screen, and every run of every page of the zone dashboard it points at that draws the gear, its
 * ghosted neighbours and an LCD's unlit cell included, moved into the face's coordinates. The ink is
 * the cap height and its overshoot down to the baseline, in the faces whose metrics the build knows,
 * and the whole box in any other (`inkOf`).
 *
 * Read from the built package rather than from the function the face places the row by, so that a
 * page, a theme's drawing of zone A or a face that moves the gear is caught here whatever the face
 * thought it was clearing.
 */
function gearInkOn(screen: { items: Item[] }, zones: readonly { name: string; screens: { name: string; items: Item[] }[] }[]): { page: string; ink: Rect }[] {
  const widget = drawnOn(screen.items).find((item) => item.kind === 'widget' && item.name === 'zoneA');
  if (!widget || widget.kind !== 'widget') throw new Error('the face has no zone A widget');
  const dashboard = zones.find((zone) => `${zone.name}.djson` === widget.fileName);
  if (!dashboard) throw new Error(`no zone dashboard ${widget.fileName}`);
  return dashboard.screens.flatMap((page) =>
    drawnOn(page.items)
      .filter((item): item is TextItem => item.kind === 'text' && /(^|\.)gear(\.(value|ghost|below|above))?$/.test(item.name))
      .map((item) => ({ page: page.name, ink: translate(inkOf(item), widget.rect.left, widget.rect.top) })),
  );
}

/**
 * **A lasting pop-up never covers the gear** (#1047), which is the author's ruling on this pull
 * request: low fuel stays up for the last laps of a stint, and a box over the gear for that long
 * hides the one reading zone A is for. Asked of every face of every theme as it ships, in both
 * arrangements, against every page of zone A a driver can cycle to while the box is up.
 */
describe('as each theme ships, no lasting pop-up covers the gear on any page of zone A', () => {
  for (const id of here) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const { main, zones } = buildThemeFace(id, size, OPTS).built;
        const screens = main.screens.filter((screen) => screen.name.startsWith('Main'));
        expect(screens.length).toBeGreaterThanOrEqual(2);
        for (const screen of screens) {
          const gear = gearInkOn(screen, zones);
          // Three of zone A's four pages draw the gear; the track page draws none.
          expect(new Set(gear.map(({ page }) => page)).size).toBeGreaterThanOrEqual(3);
          const lasting = drawnOn(screen.items).filter((item) => LASTING.some((prefix) => item.name.startsWith(prefix)));
          expect(lasting.length).toBeGreaterThan(0);
          for (const item of lasting) {
            for (const { page, ink } of gear) {
              const where = { face: `${id} ${size.width}x${size.height}`, screen: screen.name, item: item.name, page };
              expect({ ...where, rect: item.rect, ink, covered: overlaps(item.rect, ink) }).toMatchObject({ ...where, covered: false });
            }
          }
        }
      });
    }
  }
});

/** What a car is doing, as the properties the pit family reads; the in-car limiter control is present. */
const G = 'DataCorePlugin.GameData';
const car = (lane: boolean, limiter: boolean, ignition = true, started = ignition): Props => ({
  [`${G}.IsInPitLane`]: lane ? 1 : 0,
  [`${G}.PitLimiterOn`]: limiter ? 1 : 0,
  [`${G}.EngineIgnitionOn`]: ignition ? 1 : 0,
  [`${G}.EngineStarted`]: started ? 1 : 0,
  'DataCorePlugin.GameRawData.Telemetry.dcPitSpeedLimiterToggle': 1,
});
/** On the circuit, and in each state of the pit family. */
const MOMENTS: { state?: string; car: Props }[] = [
  { car: car(false, false) },
  { state: 'ignition', car: car(true, false, false) },
  { state: 'engine', car: car(true, false, true, false) },
  { state: 'engage', car: car(true, false) },
  { state: 'disengage', car: car(false, true) },
  { state: 'limiter', car: car(true, true) },
];

/**
 * The banner's row is the pit family's first: a lasting pop-up is drawn only while no state of the
 * family is out, so the two never share the row, and an outlined banner, which is drawn over what is
 * under it rather than over a ground of its own, is never read through a low-fuel box.
 */
describe('as each theme ships, a lasting pop-up gives the banner\'s row to the pit family', () => {
  for (const id of here) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const screens = buildThemeFace(id, size, OPTS).built.main.screens.filter((screen) => screen.name.startsWith('Main'));
        for (const screen of screens) {
          const layers = [...walkItems(screen.items)].filter((item): item is LayerItem => item.kind === 'layer' && LASTING.some((prefix) => `${item.name}.` === prefix));
          expect(layers.map((layer) => layer.name)).toEqual(LASTING.map((prefix) => prefix.slice(0, -1)));
          for (const layer of layers) {
            const id_ = layer.name.slice('popUp.'.length);
            // Its own condition, which waits for the whole family, with what the face adds after it.
            expect(String(layer.bindings?.Visible?.formula)).toContain(popUpVisible(id_));
            expect(popUpVisible(id_)).toContain(not(pitAlertOut()));
          }
        }
        for (const moment of MOMENTS) {
          expect({ state: moment.state ?? 'none', free: Boolean(evalNcalc(not(pitAlertOut()), moment.car)) }).toEqual({ state: moment.state ?? 'none', free: !moment.state });
        }
      });
    }
  }
});

describe('as each theme ships, no transient pop-up or change notification covers the pit banner the face draws', () => {
  for (const id of here) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const built = buildThemeFace(id, size, OPTS).built.main;
        const screens = built.screens
          .map((screen) => ({ screen: screen.name, items: [...walkItems(screen.items)].filter((item): item is Exclude<Item, LayerItem> => item.kind !== 'layer') }))
          .filter(({ items }) => items.some((item) => item.name.startsWith('pitAlert.')));
        // Both arrangements, the rev bar on and off, each with its own banner.
        expect(screens.length).toBeGreaterThanOrEqual(2);
        for (const { screen, items } of screens) {
          const banners = items.filter((item) => item.name.startsWith('pitAlert.'));
          const family = items.filter((item) => TRANSIENT.some((prefix) => item.name.startsWith(prefix)));
          expect(family.length).toBeGreaterThan(0);
          for (const item of family) {
            for (const banner of banners) {
              expect({ face: `${id} ${size.width}x${size.height}`, screen, item: item.name, rect: item.rect, banner: banner.name, covered: overlaps(banner.rect, item.rect) }).toMatchObject({ covered: false });
            }
          }
        }
      });
    }
  }
});

describe('the lap review keeps to the body, and says so', () => {
  for (const face of faces) {
    test(`${face.name}`, () => {
      const body = regionRect(face.regions, 'flagBody');
      const parts = partsOf(face.items, 'lapReview.');
      expect(parts.length).toBeGreaterThan(0);
      for (const item of parts) {
        if (item.kind === 'layer') continue;
        expect({ face: face.name, item: item.name, rect: item.rect, body, inside: contains(body, item.rect) }).toMatchObject({ inside: true });
        for (const [what, box] of settled(face)) {
          if (!box) continue;
          expect({ face: face.name, item: item.name, what, covered: overlaps(box, item.rect) }).toMatchObject({ covered: false });
        }
      }
      // And it contains the lap-time pop-up and the change notification it is drawn over, which is
      // the ranking by geometry `face.ts` relies on: a lap time at the line and its review are out
      // together, and only the review may be read.
      const review = parts.find((item) => item.name === 'lapReview.box');
      for (const name of ['popUp.lap.box', ...partsOf(face.items, 'notice.').filter((item) => item.name.endsWith('.box')).map((item) => item.name)]) {
        const box = partsOf(face.items, name).find((item) => item.name === name);
        if (!box || box.kind === 'layer' || !review || review.kind === 'layer') throw new Error(`${face.name} draws no ${name} or no review`);
        expect({ face: face.name, box: name, covered: contains(review.rect, box.rect) }).toEqual({ face: face.name, box: name, covered: true });
      }
    });
  }
});

/**
 * The faces whose lap review draws the lap time alone, by theme, measured in the theme's own fonts.
 *
 * Pinned rather than tolerated, the way `conformance.test.ts` pins what a theme gets wrong: a face
 * listed here that comes to keep its deltas fails, and so does one left out that loses them. The
 * deltas are the review's floor (`lapReviewFit`), and the house's condensed figures keep it on every
 * face. A 64 px lap time and two 46 px deltas are 680 px of Barlow at normal width and 808 of the
 * seven-segment face, and these bodies are narrower than that inside even the tighter padding: the
 * Porsche's 694, 705 and 582, the AiM's 850, 800 and 600. The Porsche's 800 x 480 and 800 x 286
 * kept their deltas while the review was clamped to the face rather than to the body, and gave them
 * up for not covering the settings column and the telltales (#1047).
 */
const LAP_TIME_ALONE: Record<string, readonly string[]> = {
  porsche: ['800x480', '800x286', '600x686'],
  aim: ['850x480', '800x480', '800x286', '600x686'],
};

/**
 * The faces whose review tightens its side padding from the sheet's 32 to 24 to keep its deltas: the
 * Porsche's 850 x 480, whose 743 px body is one pixel short of them inside the sheet's padding.
 */
const NARROW_PADDING: Record<string, readonly string[]> = {
  porsche: ['850x480'],
};

describe('the lap review keeps its deltas wherever the face it is drawn in holds them', () => {
  for (const face of faces) {
    test(`${face.name}`, () => {
      const parts = partsOf(face.items, 'lapReview.');
      const has = (id: string): boolean => parts.some((item) => item.name === `lapReview.${id}.value`);
      const alone = LAP_TIME_ALONE[face.theme]?.includes(face.size) ?? false;
      expect({ face: face.name, lap: has('lap'), vsBest: has('vsBest'), vsPrevious: has('vsPrevious') }).toEqual({
        face: face.name,
        lap: true,
        vsBest: !alone,
        vsPrevious: !alone,
      });
      const box = parts.find((item): item is Exclude<Item, LayerItem> => item.kind !== 'layer' && item.name === 'lapReview.box')!;
      const lap = parts.find((item): item is Exclude<Item, LayerItem> => item.kind !== 'layer' && item.name === 'lapReview.lap.value')!;
      const narrow = NARROW_PADDING[face.theme]?.includes(face.size) ?? false;
      expect({ face: face.name, padX: lap.rect.left - box.rect.left }).toEqual({ face: face.name, padX: narrow ? LAP_REVIEW_PAD_X_NARROW : LAP_REVIEW_PAD_X });
    });
  }
});

/** The top-level layer of each box of the pop-up family, as the face lists them. */
const FAMILY = /^(popUp\.[a-z]+|notice\.[A-Za-z]+|lapReview)$/;

/** Whether a layer is shown in a frame, by its own Visible; SimHub's windows are answered as open. */
const WINDOWS: Calls = { changed: () => true, isincreasing: () => false, isdecreasing: () => false };
const shown = (layer: LayerItem, frame: Props, windows: Calls = WINDOWS): boolean => Boolean(evalNcalc(String(layer.bindings?.Visible?.formula ?? 'true'), frame, windows));
/** Every window shut: a flag that has had band D for its seconds and settled, and no setting moving. */
const SETTLED: Calls = { changed: () => false, isincreasing: () => false, isdecreasing: () => false };
const drawnParts = (layer: LayerItem): Exclude<Item, LayerItem>[] => [...walkItems(layer.children)].filter((item): item is Exclude<Item, LayerItem> => item.kind !== 'layer');
const covers = (a: LayerItem, b: LayerItem): boolean => drawnParts(a).some((part) => drawnParts(b).some((other) => overlaps(part.rect, other.rect)));

/** Every property an expression reads, set to 1: the frame in which a condition that asks only for presence holds. */
const reads = (expression: string): Props => Object.fromEntries([...expression.matchAll(/\[([A-Za-z0-9_.]+)\]/g)].map((m) => [m[1]!, 1]));

/** A race lap just finished, with the review asked for at every crossing. */
const atTheLine = (face: FaceSize): Props => ({
  [`OpenDash.${lapReviewSettingName(face)}`]: 'all',
  [`${G}.SessionTypeName`]: 'Race',
  [`${G}.CurrentLapTime`]: 1,
  [`${G}.LastLapTime`]: 98.4,
});

/**
 * A frame in which a box of the family is out on the circuit, whatever face it is on: the moment its
 * own condition holds and no higher box of its kind's does. A test that asks whether a box is hidden
 * under a safety layer asks it here, so that "hidden" means the face kept it shut rather than that it
 * had nothing to say.
 */
function wanting(name: string, face: FaceSize): Props {
  const id = name.slice(name.indexOf('.') + 1);
  const mid = { [`${G}.CurrentLapTime`]: 30, [`${G}.LastLapTime`]: 98.4 };
  if (name === 'lapReview' || name === 'popUp.lap') return atTheLine(face);
  if (name === 'popUp.drs') return { [`${G}.DRSAvailable`]: 1 };
  if (name === 'popUp.fuel') return { ...reads(FUEL_POP_UP.when), 'DataCorePlugin.Computed.Fuel_RemainingLaps': 0.5 };
  if (name.startsWith('notice.')) {
    const value = TRACKED_VALUES.find((each) => each.id === id)!;
    return { ...reads(`${hasSetting(value)} ${value.read}`), ...mid };
  }
  throw new Error(`no frame wants ${name} out`);
}

/** A face as the theme ships it: its main screens' layers, and the contract's entry for its size. */
function shipped(id: string, size: { width: number; height: number }): { face: FaceSize; layout: ZoneLayout; regions: Regions; screens: { name: string; layers: LayerItem[] }[] } {
  const face = FACE_SIZES.find((f) => f.width === size.width && f.height === size.height)!;
  const { layout, regions, built } = buildThemeFace(id, size, OPTS);
  const screens = built.main.screens
    .filter((screen) => screen.name.startsWith('Main'))
    .map((screen) => ({ name: screen.name, layers: [...walkItems(screen.items)].filter((item): item is LayerItem => item.kind === 'layer') }));
  // Both arrangements, the rev bar on and off.
  expect(screens.length).toBeGreaterThanOrEqual(2);
  return { face, layout, regions, screens };
}

/**
 * No box of the pop-up family covers a state of the pit family while it is out, on every face as each
 * theme ships it (#1053, #1070).
 *
 * The family is drawn over the body, and the body is where a theme draws the limiter state of its
 * own: the Porsche colours the whole of it green or red round the pit speed and the limit, and the
 * AiM writes `Pit Speed` over zone C. The review hid both for its four seconds at the line until
 * #1053, and the lap pop-up hid the Porsche's pit speed for its three until #1070; the house's change
 * notification, centred on the AiM's narrow faces, reached `Pit Speed` too. So the face is built as
 * the theme ships it, and every box is put in the frame that brings it out, once on the circuit and
 * once in each state of the family: wherever a layer drawing a state of the family is out, each box
 * is hidden, drawn under it, or clear of it. Every layer whose Visible is one of the family's states is
 * asked, which is the house's banner and a theme's own drawing of the limiter alike. On the circuit
 * every box is out, so the moments in the lane are asking a question the face would otherwise answer
 * with the box drawn.
 */
describe('as each theme ships, no box of the pop-up family covers a state of the pit family', () => {
  const familyStates = new Map(PIT_ALERTS.map((spec) => [pitAlertVisible(spec.id), spec.id]));

  for (const id of here) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const { face, screens } = shipped(id, size);
        for (const screen of screens) {
          const family = screen.layers.filter((layer) => familyStates.has(String(layer.bindings?.Visible?.formula)));
          const boxes = screen.layers.filter((layer) => FAMILY.test(layer.name));
          // The house's five banners at the least, and a theme's limiter where it draws one; and every
          // member of the pop-up family.
          expect(family.length).toBeGreaterThanOrEqual(PIT_ALERTS.length);
          expect(new Set(boxes.map((box) => box.name.split('.')[0]))).toEqual(new Set(['popUp', 'notice', 'lapReview']));
          for (const box of boxes) {
            for (const moment of MOMENTS) {
              const frame = { ...wanting(box.name, face), ...moment.car };
              const where = { face: `${id} ${size.width}x${size.height}`, screen: screen.name, box: box.name, state: moment.state ?? 'none' };
              const out = family.filter((layer) => shown(layer, frame)).map((layer) => familyStates.get(String(layer.bindings?.Visible?.formula)));
              // The moment is the state it is named for, and only that one.
              expect({ ...where, out: [...new Set(out)] }).toEqual({ ...where, out: moment.state ? [moment.state] : [] });
              if (!moment.state) {
                expect({ ...where, shown: shown(box, frame) }).toEqual({ ...where, shown: true });
                continue;
              }
              for (const layer of family.filter((each) => shown(each, frame))) {
                const hidden = !shown(box, frame);
                const over = screen.layers.indexOf(layer) > screen.layers.indexOf(box);
                expect({ ...where, layer: layer.name, covered: !(hidden || over || !covers(box, layer)) }).toEqual({ ...where, layer: layer.name, covered: false });
              }
            }
          }
        }
      });
    }
  }
});

/** The bits that raise a flag of the catalogue, and the frames that raise each of its car alerts. */
const RAISED_ALERTS: Record<string, Props> = {
  ignition: { [`${G}.EngineIgnitionOn`]: 0 },
  engine: { [`${G}.EngineIgnitionOn`]: 1, [`${G}.EngineStarted`]: 0 },
  incident: { 'DataCorePlugin.GameRawData.Telemetry.PlayerCarMyIncidentCount': 2 },
};
const raising = (condition: AlertCondition): Props => {
  if (isFlag(condition)) {
    return condition.limiter
      ? { [`${G}.${condition.limiter}`]: 1 }
      : { [String(flagBit(condition.bits[0]!)).slice(1, -1)]: 1 };
  }
  const props = RAISED_ALERTS[condition.id];
  if (!props) throw new Error(`no frame raises ${condition.id}`);
  return props;
};

/**
 * With the flag format at `full`, no box of the pop-up family is drawn over the full-screen flag while
 * it is out, on every face as each theme ships it (#1069).
 *
 * The block is the whole body and every box of the family is inside the body, so at every crossing
 * under a flag the lap pop-up was drawn over the middle of the flag's name and the review over nearly
 * all of it, and on the Porsche's nano and the AiM's portrait face low fuel reached its top. Each
 * condition the block draws is raised in turn, with every box in the frame that brings it out: the
 * condition's layer is shown, and each box drawn over it is hidden. The band format is the control,
 * and the ticket's other line: under a flag that sits in band D the lap pop-up and the review still
 * open at the line. Once the flag has had band D for its seconds it settles into the one rectangle of
 * the face #731 keeps for it, and a box drawn over that rectangle waits for it too: the nano's lap
 * review is the one that reaches it.
 */
describe('as each theme ships, no box of the pop-up family covers the flag', () => {
  for (const id of here) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const { face, screens } = shipped(id, size);
        const format = `OpenDash.${flagFormatSettingName(face)}`;
        const onTrack = { 'DataCorePlugin.GameRawData.Telemetry.IsOnTrack': true, [`${G}.EngineIgnitionOn`]: 1, [`${G}.EngineStarted`]: 1 };
        for (const screen of screens) {
          const block = screen.layers.find((layer) => layer.name === 'flagFull')!;
          const conditions = screen.layers.filter((layer) => layer.name.startsWith('flagFull.') && !layer.name.slice('flagFull.'.length).includes('.'));
          const boxes = screen.layers.filter((layer) => FAMILY.test(layer.name));
          const corner = screen.layers.find((layer) => layer.name === 'flagCorner')!;
          const settledAs = (id_: string): LayerItem | undefined => screen.layers.find((layer) => layer.name === `flagCorner.${id_}`);
          expect(conditions.length).toBe(ALERT_CATALOGUE.filter((c) => !('neutral' in c && c.neutral)).length);
          for (const box of boxes) {
            const wants = { ...onTrack, ...wanting(box.name, face) };
            const where = { face: `${id} ${size.width}x${size.height}`, screen: screen.name, box: box.name };
            // With no flag out the box opens, in either format.
            expect({ ...where, shown: shown(box, { ...wants, [format]: 'full' }) }).toEqual({ ...where, shown: true });
            for (const layer of conditions) {
              const condition = alertCondition(layer.name.slice('flagFull.'.length));
              const frame = { ...wants, ...raising(condition), [format]: 'full' };
              const at = { ...where, condition: condition.id };
              // The frame raises this condition and the block draws it.
              expect({ ...at, block: shown(block, frame) && shown(layer, frame) }).toEqual({ ...at, block: true });
              if (covers(box, layer)) expect({ ...at, covered: shown(box, frame) }).toEqual({ ...at, covered: false });
              // In the band format the flag is in band D, and the events at the line still open. Once
              // it has settled, they open unless they would cover the settled flag. The incident is
              // raised by a window of its own and is asked in band D's phase alone.
              if (box.name === 'popUp.lap' || box.name === 'lapReview') {
                const band = { ...frame, [format]: 'band' };
                expect({ ...at, band: shown(box, band) }).toEqual({ ...at, band: true });
                const settled = settledAs(condition.id);
                if (settled && condition.id !== 'incident') {
                  expect({ ...at, settled: shown(corner, band, SETTLED) && shown(settled, band, SETTLED) }).toEqual({ ...at, settled: true });
                  expect({ ...at, settledCovered: shown(box, band, SETTLED) && covers(box, settled) }).toEqual({ ...at, settledCovered: false });
                  expect({ ...at, open: shown(box, band, SETTLED) }).toEqual({ ...at, open: !covers(box, settled) });
                }
              }
            }
          }
        }
      });
    }
  }
});

/**
 * The takeovers of each theme that are not a state of the pit family, by name: states of the car the
 * theme draws over its regions for as long as they hold, after the pit family and the full-screen
 * flag. Pinned, so that a takeover a theme adds is asked about below rather than drawn unranked.
 */
const CAR_TAKEOVERS: Record<string, readonly string[]> = {
  porsche: ['lowFuel'],
  aim: ['warning'],
};

/**
 * A theme's takeovers as its drawing returns them, before the face ranks them: those that draw a state
 * of the pit family, which are that family, and the others, which are states of the car.
 */
function takeoversOf(id: string, { face, layout, regions }: { face: FaceSize; layout: ZoneLayout; regions: Regions }): { family: string[]; car: LayerItem[] } {
  const takeovers = THEME_DRAWINGS[id]?.takeovers;
  if (!takeovers) return { family: [], car: [] };
  const drawn = takeovers({ layout, face, regions, withRevBar: true }).filter((item): item is LayerItem => item.kind === 'layer');
  const states = new Set(PIT_ALERTS.map((spec) => pitAlertVisible(spec.id)));
  const ofFamily = (layer: LayerItem): boolean => states.has(String(layer.bindings?.Visible?.formula));
  return { family: drawn.filter(ofFamily).map((layer) => layer.name), car: drawn.filter((layer) => !ofFamily(layer)) };
}

/** A frame in which a takeover of the car is out, read off its own condition: every property it reads set to 1. */
const bringingOut = (takeover: LayerItem): Props => reads(String(takeover.bindings?.Visible?.formula));

/** The one layer of a screen by that name, which a takeover's is. */
const theLayer = (layers: readonly LayerItem[], name: string): LayerItem => {
  const found = layers.filter((layer) => layer.name === name);
  expect({ name, count: found.length }).toEqual({ name, count: 1 });
  return found[0]!;
};

/**
 * No takeover of a theme covers a state of the pit family while it is out, on every face as each
 * theme ships it (#1097).
 *
 * A theme's takeovers are drawn after the pit family, and the AiM's engine warning is a box over zone
 * C, whose top 30 px are the AiM's banner row: for as long as an engine warning held, `Disengage
 * limiter`, `Engage limiter`, `Ignition off` and `Engine off` were drawn under it. The Porsche's fuel
 * alarm fills zone C's panel, which is inside the limiter body, so a car in the lane on its last ten
 * litres had a red panel over the right of a green body that says it is under the limit. A takeover
 * of the car gives way as a box of the pop-up family does: with it brought out, once on the circuit
 * and once in each state of the family, every layer drawing that state that is out is either drawn
 * over the takeover or not covered by it, or the takeover is hidden.
 */
describe('as each theme ships, no takeover of the car covers a state of the pit family', () => {
  const familyStates = new Map(PIT_ALERTS.map((spec) => [pitAlertVisible(spec.id), spec.id]));

  for (const id of here.filter((each) => THEME_DRAWINGS[each]?.takeovers)) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const ships = shipped(id, size);
        const { screens } = ships;
        const takeovers = takeoversOf(id, ships);
        expect({ theme: id, car: takeovers.car.map((layer) => layer.name) }).toEqual({ theme: id, car: [...(CAR_TAKEOVERS[id] ?? [])] });
        for (const screen of screens) {
          const family = screen.layers.filter((layer) => familyStates.has(String(layer.bindings?.Visible?.formula)));
          expect(family.length).toBeGreaterThanOrEqual(PIT_ALERTS.length + takeovers.family.length);
          for (const own of takeovers.car) {
            const takeover = theLayer(screen.layers, own.name);
            for (const moment of MOMENTS) {
              const frame = { ...bringingOut(own), ...moment.car };
              const where = { face: `${id} ${size.width}x${size.height}`, screen: screen.name, takeover: own.name, state: moment.state ?? 'none' };
              // Its own condition holds in every moment, so a hidden takeover is one the face kept shut.
              expect({ ...where, own: shown(own, frame) }).toEqual({ ...where, own: true });
              if (!moment.state) {
                expect({ ...where, shown: shown(takeover, frame) }).toEqual({ ...where, shown: true });
                continue;
              }
              const out = family.filter((layer) => shown(layer, frame));
              expect({ ...where, out: [...new Set(out.map((layer) => familyStates.get(String(layer.bindings?.Visible?.formula))))] }).toEqual({ ...where, out: [moment.state] });
              for (const layer of out) {
                const hidden = !shown(takeover, frame);
                const over = screen.layers.indexOf(layer) > screen.layers.indexOf(takeover);
                expect({ ...where, layer: layer.name, covered: !(hidden || over || !covers(takeover, layer)) }).toEqual({ ...where, layer: layer.name, covered: false });
              }
            }
          }
        }
      });
    }
  }
});

/**
 * With the flag format at `full`, no takeover of a theme covers the flag while it is out, on every
 * face as each theme ships it (#1098).
 *
 * The block is the whole body and a takeover is drawn after it, so on the Porsche's faces the fuel
 * alarm's red panel covered the right of YELLOW, FULL COURSE YELLOW and BLACK for as long as the tank
 * was under ten litres, and on the AiM's the engine warning covered zone C's part of every name. A
 * takeover of the car ranks under the flag as the house's low-fuel pop-up does (zones.md §1): each
 * condition the block draws is raised in turn with the takeover brought out, and a takeover drawn
 * over the condition's layer is hidden. In the band format the flag settles, once it has had band D
 * for its seconds, into the one rectangle #731 keeps for it, and a takeover over that waits for it
 * too; with no flag out the takeover is shown in either format.
 */
describe('as each theme ships, no takeover of the car covers the flag', () => {
  for (const id of here.filter((each) => THEME_DRAWINGS[each]?.takeovers)) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const ships = shipped(id, size);
        const { face, screens } = ships;
        const takeovers = takeoversOf(id, ships);
        const format = `OpenDash.${flagFormatSettingName(face)}`;
        const onTrack = { 'DataCorePlugin.GameRawData.Telemetry.IsOnTrack': true, [`${G}.EngineIgnitionOn`]: 1, [`${G}.EngineStarted`]: 1 };
        for (const screen of screens) {
          const block = screen.layers.find((layer) => layer.name === 'flagFull')!;
          const conditions = screen.layers.filter((layer) => layer.name.startsWith('flagFull.') && !layer.name.slice('flagFull.'.length).includes('.'));
          const corner = screen.layers.find((layer) => layer.name === 'flagCorner')!;
          expect(conditions.length).toBe(ALERT_CATALOGUE.filter((c) => !('neutral' in c && c.neutral)).length);
          for (const own of takeovers.car) {
            const takeover = theLayer(screen.layers, own.name);
            const wants = { ...onTrack, ...bringingOut(own) };
            const where = { face: `${id} ${size.width}x${size.height}`, screen: screen.name, takeover: own.name };
            for (const value of ['full', 'band']) {
              expect({ ...where, format: value, shown: shown(takeover, { ...wants, [format]: value }) }).toEqual({ ...where, format: value, shown: true });
            }
            // Every takeover of the car is drawn over the block, which is the whole body, so every
            // condition asks a question here.
            expect({ ...where, overTheBlock: conditions.every((layer) => covers(takeover, layer)) }).toEqual({ ...where, overTheBlock: true });
            for (const layer of conditions) {
              const condition = alertCondition(layer.name.slice('flagFull.'.length));
              const frame = { ...wants, ...raising(condition), [format]: 'full' };
              const at = { ...where, condition: condition.id };
              expect({ ...at, own: shown(own, frame), block: shown(block, frame) && shown(layer, frame) }).toEqual({ ...at, own: true, block: true });
              expect({ ...at, covered: shown(takeover, frame) }).toEqual({ ...at, covered: false });
              const settled = screen.layers.find((each) => each.name === `flagCorner.${condition.id}`);
              if (settled && condition.id !== 'incident') {
                const band = { ...frame, [format]: 'band' };
                expect({ ...at, settled: shown(corner, band, SETTLED) && shown(settled, band, SETTLED) }).toEqual({ ...at, settled: true });
                expect({ ...at, settledCovered: shown(takeover, band, SETTLED) && covers(takeover, settled) }).toEqual({ ...at, settledCovered: false });
              }
            }
          }
        }
      });
    }
  }
});

if (THEME_ID === DEFAULT_THEME_ID) {
  for (const id of Object.keys(THEMES).filter((each) => !drawsInThisProcess(each))) {
    test(
      `the ${id} theme, in a process drawing its fonts`,
      () => {
        const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: id }, stdout: 'pipe', stderr: 'pipe' });
        const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
        expect({ theme: id, exit: run.exitCode, output }).toEqual({ theme: id, exit: 0, output: '' });
      },
      120_000,
    );
  }
}
