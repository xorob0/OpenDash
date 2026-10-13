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
import { POP_UP_WIDTH, popUpVisible, popUpsOfKind } from '../src/components/popUp.ts';
import { FACE_SIZES, lapReviewSettingName } from '../src/contract.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';
import { ncalc, type Item, type LayerItem, type Rect, type TextItem } from '../src/generator.ts';
import { contains, overlaps, translate } from '../src/design/geometry.ts';
import { inkOf } from '../src/design/metrics.ts';
import { optionalRegionRect, regionRect, zoneRect, type Regions } from '../src/themes/anatomy.ts';
import { buildThemeFace, drawsInThisProcess } from '../src/themes/faces.ts';
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
            expect(layer.bindings?.Visible?.formula).toBe(popUpVisible(id_));
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

/**
 * The lap review gives way to the pit family, on every face as each theme ships it (#1053).
 *
 * The review is clamped to the body, and the body is where a theme draws the limiter state of its
 * own: the Porsche colours the whole of it green or red round the pit speed and the limit, and the
 * AiM writes `Pit Speed` over zone C. A review pushed after them hid both for its four seconds at the
 * line, which most pit lanes cross, and on the Porsche's faces and the house's nano its 160 px reach
 * the banner at the top of the hero as well. So the face is built as the theme ships it and its
 * conditions are evaluated at a crossing with the review set to `all`, once on the circuit and once
 * in each state of the family: wherever a layer drawing a state of the family is out, the review is
 * hidden, drawn under it, or clear of it. Every layer whose Visible is one of the family's states is
 * asked, which is the house's banner and a theme's own drawing of the limiter alike.
 */
describe('as each theme ships, the lap review never covers a state of the pit family', () => {
  const familyStates = new Map(PIT_ALERTS.map((spec) => [pitAlertVisible(spec.id), spec.id]));
  const shown = (layer: LayerItem, frame: Props): boolean => Boolean(evalNcalc(String(layer.bindings?.Visible?.formula), frame));
  const drawnParts = (layer: LayerItem): Exclude<Item, LayerItem>[] => [...walkItems(layer.children)].filter((item): item is Exclude<Item, LayerItem> => item.kind !== 'layer');

  for (const id of here) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const faceSize = FACE_SIZES.find((f) => f.width === size.width && f.height === size.height)!;
        const atTheLine: Props = {
          [`OpenDash.${lapReviewSettingName(faceSize)}`]: 'all',
          [`${G}.SessionTypeName`]: 'Race',
          [`${G}.CurrentLapTime`]: 1,
          [`${G}.LastLapTime`]: 98.4,
        };
        const screens = buildThemeFace(id, size, OPTS).built.main.screens.filter((screen) => screen.name.startsWith('Main'));
        expect(screens.length).toBeGreaterThanOrEqual(2);
        for (const screen of screens) {
          const layers = [...walkItems(screen.items)].filter((item): item is LayerItem => item.kind === 'layer');
          const review = layers.find((layer) => layer.name === 'lapReview')!;
          const family = layers.filter((layer) => familyStates.has(String(layer.bindings?.Visible?.formula)));
          // The house's five banners at the least, and a theme's limiter where it draws one.
          expect(family.length).toBeGreaterThanOrEqual(PIT_ALERTS.length);
          const reviewParts = drawnParts(review);
          for (const moment of MOMENTS) {
            const frame = { ...atTheLine, ...moment.car };
            const out = family.filter((layer) => shown(layer, frame)).map((layer) => familyStates.get(String(layer.bindings?.Visible?.formula)));
            const where = { face: `${id} ${size.width}x${size.height}`, screen: screen.name, state: moment.state ?? 'none' };
            // The moment is the state it is named for, and only that one.
            expect({ ...where, out: [...new Set(out)] }).toEqual({ ...where, out: moment.state ? [moment.state] : [] });
            // On the circuit the crossing opens the review, so the moments in the lane are asking a
            // question the face would otherwise answer with the review out.
            if (!moment.state) {
              expect({ ...where, review: shown(review, frame) }).toEqual({ ...where, review: true });
              continue;
            }
            for (const layer of family.filter((each) => shown(each, frame))) {
              const hidden = !shown(review, frame);
              const over = layers.indexOf(layer) > layers.indexOf(review);
              const clear = drawnParts(layer).every((part) => reviewParts.every((r) => !overlaps(part.rect, r.rect)));
              expect({ ...where, layer: layer.name, covered: !(hidden || over || clear) }).toEqual({ ...where, layer: layer.name, covered: false });
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
