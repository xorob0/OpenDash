/**
 * The rectangles the four pit wall pages cut themselves into, and the row budgets that follow.
 *
 * `pitwallPanels.test.ts` measures the right column's fixed panels and `telemetryTraces.test.ts`
 * the five plots. This is the page above both of them: where the board ends and the zone column
 * begins, what each zone rectangle comes to, and how much of a board's band its rows actually use.
 * A zone rectangle is not a private number. It names the zone dashboard the package carries and it
 * is the box every page of the catalogue is then cut for, so a rectangle that moves sheds a row
 * somewhere; pinning it here is what turns that into a diff somebody reads.
 *
 * The containment check at the foot is what the artboards' `overflow: hidden` amounts to on a
 * screen that has no clipping of its own. SimHub draws an item where it is put, so a board that
 * outgrew its column would carry on under the zone beside it rather than stop at the rule, and
 * nothing but this says it does not.
 */
import { describe, expect, test } from 'bun:test';
import { bottom, contains, rect, right, type Rect } from '../src/design/geometry.ts';
import type { Item, Screen } from '../src/generator.ts';
import { PIT_WALL_ROWS, TELEMETRY_TRACES, TOWER_ROWS, portraitPage, racePage, telemetryPage, towerPage, tracePanelHeight } from '../src/screens/pitwall.ts';
import { PIT_WALL_HEADER } from '../src/screens/pitwallHeader.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';

/** An item with a box, which is everything a page draws: a layer carries none and is not one. */
type Placed = Extract<Item, { rect: Rect }>;

const LANDSCAPE = { width: 1920, height: 1080 };
const PORTRAIT_SIZE = { width: 1080, height: 1920 };

const RACE = racePage(LANDSCAPE.width, LANDSCAPE.height);
const TOWER = towerPage(LANDSCAPE.width, LANDSCAPE.height);
const TELEMETRY = telemetryPage(LANDSCAPE.width, LANDSCAPE.height);
const PORTRAIT = portraitPage(PORTRAIT_SIZE.width, PORTRAIT_SIZE.height);

const placed = (screen: Screen): Placed[] => [...walkItems(screen.items)].filter((i): i is Placed => i.kind !== 'layer');

const named = (screen: Screen, name: string): Placed => {
  const item = placed(screen).find((i) => i.name === name);
  if (!item) throw new Error(`no item named ${name}`);
  return item;
};

const boxOf = (screen: Screen, name: string): Rect => named(screen, name).rect;

/** Every zone the page embeds, in the order it places them, with the dashboard each one asks for. */
const zonesOf = (screen: Screen): { name: string; rect: Rect; file: string }[] =>
  placed(screen)
    .filter((i) => i.kind === 'widget')
    .map((i) => ({ name: i.name, rect: i.rect, file: i.kind === 'widget' ? i.fileName : '' }));

/**
 * What a board really stamps. The rows are one repeated layer rather than one item each, so the
 * count is the layer's repetitions and the last row is where the pitch puts it.
 */
function boardOf(screen: Screen, name: string): { rows: number; pitch: number; top: number; bottom: number } {
  const stamped = screen.items.find((i) => i.name === `${name}.rows`);
  if (stamped?.kind !== 'layer') throw new Error(`no stamped rows named ${name}.rows`);
  const rows = (stamped.repetitions ?? 0) + 1;
  const pitch = stamped.repeatTopOffset ?? 0;
  const first = [...walkItems([stamped])].find((i) => i.name === `${name}.row.background`);
  if (!first || first.kind === 'layer') throw new Error(`no first row of ${name}`);
  return { rows, pitch, top: first.rect.top, bottom: first.rect.top + (rows - 1) * pitch + first.rect.height };
}

const BODY_TOP = PIT_WALL_HEADER.height;
const LANDSCAPE_BODY = LANDSCAPE.height - BODY_TOP;

describe('the header is the same band on all four pages', () => {
  test('64 px tall, and nothing it draws reaches the body', () => {
    expect(BODY_TOP).toBe(64);
    for (const [page, screen] of [
      ['race', RACE],
      ['tower', TOWER],
      ['telemetry', TELEMETRY],
      ['portrait', PORTRAIT],
    ] as const) {
      for (const item of placed(screen).filter((i) => i.name.startsWith(`${page}.header`))) {
        expect({ page, item: item.name, bottom: bottom(item.rect), fits: bottom(item.rect) <= BODY_TOP }).toMatchObject({ fits: true });
      }
    }
  });
});

describe('the landscape pages divide on one column', () => {
  // Both artboards set the zone column at 639 and the board beside it at 1280 and 1279; the pixel
  // the telemetry sheet leaves over goes to the plot, so that one split serves both pages and one
  // zone rectangle serves the catalogue on both.
  test('the divider is a 1 px rule at x = 1280, down the whole body', () => {
    expect({ race: boxOf(RACE, 'race.columnRule'), telemetry: boxOf(TELEMETRY, 'telemetry.columnRule') }).toEqual({
      race: rect(1280, BODY_TOP, 1, LANDSCAPE_BODY),
      telemetry: rect(1280, BODY_TOP, 1, LANDSCAPE_BODY),
    });
  });

  test('the tower divides further left, at its own 880', () => {
    expect(boxOf(TOWER, 'tower.columnRule')).toEqual(rect(880, BODY_TOP, 1, LANDSCAPE_BODY));
  });
});

describe('the zone rectangles', () => {
  test('the race page stacks two of 639, and what the four panels leave is theirs', () => {
    expect(zonesOf(RACE)).toEqual([
      { name: 'race.zoneA', rect: rect(1281, 666, 639, 206), file: 'zones-639x206.djson' },
      { name: 'race.zoneB', rect: rect(1281, 873, 639, 206), file: 'zones-639x206.djson' },
    ]);
  });

  test('the tower page draws a wide zone over two of 519', () => {
    expect(zonesOf(TOWER)).toEqual([
      { name: 'tower.wide', rect: rect(881, 465, 1039, 255), file: 'zones-wide-1039x255.djson' },
      { name: 'tower.zoneA', rect: rect(881, 721, 519, 359), file: 'zones-519x359.djson' },
      { name: 'tower.zoneB', rect: rect(1401, 721, 519, 359), file: 'zones-519x359.djson' },
    ]);
  });

  test('the telemetry page stacks three of 639 by 338, which is the artboard exactly', () => {
    expect(zonesOf(TELEMETRY)).toEqual([
      { name: 'telemetry.zoneA', rect: rect(1281, 64, 639, 338), file: 'zones-639x338.djson' },
      { name: 'telemetry.zoneB', rect: rect(1281, 403, 639, 338), file: 'zones-639x338.djson' },
      { name: 'telemetry.zoneC', rect: rect(1281, 742, 639, 338), file: 'zones-639x338.djson' },
    ]);
  });

  test('the portrait page draws a narrow column and a wide one, out to its last pixel', () => {
    const zones = zonesOf(PORTRAIT);
    expect(zones).toEqual([
      { name: 'portrait.zoneA', rect: rect(0, 976, 539, 471), file: 'zones-539x471.djson' },
      { name: 'portrait.zoneB', rect: rect(540, 976, 540, 471), file: 'zones-540x471.djson' },
      { name: 'portrait.zoneC', rect: rect(0, 1448, 539, 471), file: 'zones-539x471.djson' },
      { name: 'portrait.zoneD', rect: rect(540, 1448, 540, 471), file: 'zones-540x471.djson' },
    ]);
    // The sheet's own 539 and 540: two halves of an odd width stop a pixel short of the page.
    expect([...new Set(zones.map((z) => right(z.rect)))]).toEqual([539, PORTRAIT_SIZE.width]);
  });

  test('every rule the four pages draw is 1 px of surface.raised', () => {
    const rules = [RACE, TOWER, TELEMETRY, PORTRAIT].flatMap((screen) => placed(screen).filter((i) => i.name.toLowerCase().includes('rule')));
    expect(rules.length).toBeGreaterThan(0);
    for (const item of rules) {
      const thickness = Math.min(item.rect.width, item.rect.height);
      const colour = item.kind === 'rect' ? item.backgroundColor : undefined;
      expect({ item: item.name, thickness, colour }).toEqual({ item: item.name, thickness: 1, colour: ds.color.surface.raised });
    }
  });
});

describe('what a board stamps into the band it is given', () => {
  test('the race board stamps a full grid at 34 px under a 32 px header', () => {
    const board = boardOf(RACE, 'race.board');
    expect(board).toEqual({ rows: PIT_WALL_ROWS, pitch: 34, top: BODY_TOP + 32, bottom: BODY_TOP + 32 + PIT_WALL_ROWS * 34 });
    expect(board.bottom).toBeLessThanOrEqual(LANDSCAPE.height);
  });

  test('the tower board stamps the 33 rows its artboard says fit, at 28 px under a 28 px header', () => {
    const board = boardOf(TOWER, 'tower.board');
    expect({ rows: board.rows, pitch: board.pitch, header: board.top - BODY_TOP }).toEqual({ rows: 33, pitch: 28, header: 28 });
    expect(TOWER_ROWS).toBe(33);
    // 28 + 33 x 28 of the 1016 px body, which is what the row count is chosen against.
    expect(board.bottom - BODY_TOP).toBe(952);
    expect(board.bottom).toBeLessThanOrEqual(LANDSCAPE.height);
  });

  test('the portrait board is as tall as its own header and rows, and the rule closes it there', () => {
    const board = boardOf(PORTRAIT, 'portrait.board');
    expect({ rows: board.rows, pitch: board.pitch, header: board.top - BODY_TOP }).toEqual({ rows: PIT_WALL_ROWS, pitch: 32, header: 32 });
    // The band is the parts and nothing else: an empty strip under the last car is what a literal
    // height off the sheet leaves behind, and the sheet's own 856 counts two class bands this page
    // cannot draw.
    expect(board.bottom - BODY_TOP).toBe(32 + PIT_WALL_ROWS * 32);
    expect(boxOf(PORTRAIT, 'portrait.boardRule').top).toBe(board.bottom);
  });
});

/**
 * The regions a page cuts itself into, written out, and every item of the page inside one of them.
 *
 * They are coarse on purpose. The four fixed panels are measured one by one in
 * `pitwallPanels.test.ts` and the five plots in `telemetryTraces.test.ts`; what is missing, and
 * what this is, is the column those checks sit inside -- so a panel that grew past the rule, or a
 * board whose columns came to more than its width, fails here rather than on somebody's screen.
 */
const REGIONS: { page: string; screen: Screen; size: { width: number; height: number }; regions: { name: string; rect: Rect }[] }[] = [
  {
    page: 'race',
    screen: RACE,
    size: LANDSCAPE,
    regions: [
      { name: 'race.header', rect: rect(0, 0, 1920, BODY_TOP) },
      { name: 'race.board', rect: rect(0, BODY_TOP, 1280, LANDSCAPE_BODY) },
      { name: 'race.columnRule', rect: rect(1280, BODY_TOP, 1, LANDSCAPE_BODY) },
      { name: 'race.column', rect: rect(1281, BODY_TOP, 639, LANDSCAPE_BODY) },
    ],
  },
  {
    page: 'tower',
    screen: TOWER,
    size: LANDSCAPE,
    regions: [
      { name: 'tower.header', rect: rect(0, 0, 1920, BODY_TOP) },
      { name: 'tower.board', rect: rect(0, BODY_TOP, 880, LANDSCAPE_BODY) },
      { name: 'tower.columnRule', rect: rect(880, BODY_TOP, 1, LANDSCAPE_BODY) },
      { name: 'tower.column', rect: rect(881, BODY_TOP, 1039, LANDSCAPE_BODY) },
    ],
  },
  {
    page: 'telemetry',
    screen: TELEMETRY,
    size: LANDSCAPE,
    regions: [
      { name: 'telemetry.header', rect: rect(0, 0, 1920, BODY_TOP) },
      { name: 'telemetry.plot', rect: rect(0, BODY_TOP, 1280, LANDSCAPE_BODY) },
      { name: 'telemetry.columnRule', rect: rect(1280, BODY_TOP, 1, LANDSCAPE_BODY) },
      { name: 'telemetry.column', rect: rect(1281, BODY_TOP, 639, LANDSCAPE_BODY) },
    ],
  },
  {
    page: 'portrait',
    screen: PORTRAIT,
    size: PORTRAIT_SIZE,
    regions: [
      { name: 'portrait.header', rect: rect(0, 0, 1080, BODY_TOP) },
      { name: 'portrait.board', rect: rect(0, BODY_TOP, 1080, 801) },
      { name: 'portrait.panels', rect: rect(0, 865, 1080, 111) },
      { name: 'portrait.zones', rect: rect(0, 976, 1080, 944) },
    ],
  },
];

/** Which region an item belongs to: the column on a landscape page, the band on the portrait one. */
const regionFor = (page: string, name: string): string => {
  if (name.startsWith(`${page}.header`)) return `${page}.header`;
  if (page === 'portrait') {
    if (name.startsWith('portrait.board')) return 'portrait.board';
    if (name.startsWith('portrait.zone')) return 'portrait.zones';
    return 'portrait.panels';
  }
  if (name === `${page}.columnRule`) return `${page}.columnRule`;
  if (name.startsWith(`${page}.board`)) return `${page}.board`;
  if (page === 'telemetry' && !name.startsWith('telemetry.zone')) return 'telemetry.plot';
  return `${page}.column`;
};

describe('nothing is drawn outside the region it belongs to', () => {
  for (const { page, screen, size, regions } of REGIONS) {
    test(`${page} keeps every item in its own column`, () => {
      const byName = new Map(regions.map((r) => [r.name, r.rect]));
      const items = placed(screen);
      expect(items.length).toBeGreaterThan(0);
      for (const item of items) {
        const region = regionFor(page, item.name);
        const frame = byName.get(region);
        expect({ item: item.name, region, known: frame !== undefined }).toMatchObject({ known: true });
        // A text box is a WPF line box, taller than its ink at both ends: `textBox` puts its top a
        // tenth of the size above the line it was given and runs about a quarter below the
        // baseline. Both tails are transparent, and the same slack `secondScreens.test.ts` grants.
        const slack = item.kind === 'text' ? { above: Math.ceil(0.1 * item.fontSize) + 2, below: Math.ceil(0.25 * item.fontSize) + 2 } : { above: 0, below: 0 };
        const grown = rect(frame!.left, frame!.top - slack.above, frame!.width, frame!.height + slack.above + slack.below);
        expect({ item: item.name, region, rect: item.rect, inside: contains(grown, item.rect) }).toMatchObject({ inside: true });
      }
      for (const name of byName.keys()) {
        expect({ region: name, drawn: items.some((i) => regionFor(page, i.name) === name) }).toMatchObject({ drawn: true });
      }
    });

    test(`${page} covers its canvas with those regions and no more`, () => {
      const union = regions.reduce((acc, r) => rect(0, 0, Math.max(acc.width, right(r.rect)), Math.max(acc.height, bottom(r.rect))), rect(0, 0, 0, 0));
      expect(union).toEqual(rect(0, 0, size.width, size.height));
    });
  }

  test('a board stamps its last row inside the body, not under the zones beside it', () => {
    for (const [page, screen, name] of [
      ['race', RACE, 'race.board'],
      ['tower', TOWER, 'tower.board'],
      ['portrait', PORTRAIT, 'portrait.board'],
    ] as const) {
      const board = boardOf(screen, name);
      const frame = REGIONS.find((r) => r.page === page)!.regions.find((r) => r.name === `${page}.board`)!.rect;
      expect({ page, bottom: board.bottom, frame: bottom(frame), fits: board.bottom <= bottom(frame) }).toMatchObject({ fits: true });
    }
  });
});

describe('the telemetry column spends the body it is given', () => {
  test('five panels, their rules and the axis footer stay inside the 1016 px body', () => {
    const stacked = TELEMETRY_TRACES.reduce((sum, spec) => sum + tracePanelHeight(spec) + 1, 0);
    const footer = bottom(boxOf(TELEMETRY, 'telemetry.axisName'));
    expect(stacked).toBeLessThan(LANDSCAPE_BODY);
    expect(footer).toBeLessThanOrEqual(LANDSCAPE.height);
    expect(boxOf(TELEMETRY, 'telemetry.axisName').width).toBe(1280);
  });
});
