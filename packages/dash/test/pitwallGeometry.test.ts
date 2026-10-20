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
import { bottom, contains, overlaps, rect, right, type Rect } from '../src/design/geometry.ts';
import type { Item, Screen } from '../src/generator.ts';
import { PIT_WALL_ROWS, TELEMETRY_TRACES, TOWER_ROWS, portraitPage, racePage, telemetryPage, towerPage, tracePanelHeight } from '../src/screens/pitwall.ts';
import { PIT_WALL_HEADER } from '../src/screens/pitwallHeader.ts';
import { ds } from '../src/tokens.ts';
import { drawnParts, walkItems } from '../src/walk.ts';

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
      { name: 'portrait.zoneA', rect: rect(0, 1040, 539, 439), file: 'zones-539x439.djson' },
      { name: 'portrait.zoneB', rect: rect(540, 1040, 540, 439), file: 'zones-540x439.djson' },
      { name: 'portrait.zoneC', rect: rect(0, 1480, 539, 439), file: 'zones-539x439.djson' },
      { name: 'portrait.zoneD', rect: rect(540, 1480, 540, 439), file: 'zones-540x439.djson' },
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

  test('the portrait board is as tall as its own header and rows, and the rule closes it a flag band below', () => {
    const board = boardOf(PORTRAIT, 'portrait.board');
    expect({ rows: board.rows, pitch: board.pitch, header: board.top - BODY_TOP }).toEqual({ rows: PIT_WALL_ROWS, pitch: 32, header: 32 });
    // The board is the parts and nothing else, since the sheet's own 856 counts two class bands this
    // page cannot draw. What the column keeps under the last car is the flag band's 64 px (#1016),
    // so that a flag takes no row off a full grid.
    expect(board.bottom - BODY_TOP).toBe(32 + PIT_WALL_ROWS * 32);
    expect(boxOf(PORTRAIT, 'portrait.boardRule').top).toBe(board.bottom + PIT_WALL_HEADER.height);
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
      { name: 'race.body', rect: rect(0, BODY_TOP, 1920, LANDSCAPE_BODY) },
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
      { name: 'tower.body', rect: rect(0, BODY_TOP, 1920, LANDSCAPE_BODY) },
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
      { name: 'telemetry.body', rect: rect(0, BODY_TOP, 1920, LANDSCAPE_BODY) },
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
      { name: 'portrait.body', rect: rect(0, BODY_TOP, 1080, PORTRAIT_SIZE.height - BODY_TOP) },
      { name: 'portrait.board', rect: rect(0, BODY_TOP, 1080, 865) },
      { name: 'portrait.panels', rect: rect(0, 929, 1080, 111) },
      { name: 'portrait.zones', rect: rect(0, 1040, 1080, 880) },
    ],
  },
];

/** Which region an item belongs to: the column on a landscape page, the band on the portrait one. */
const regionFor = (page: string, name: string): string => {
  if (name.startsWith(`${page}.header`)) return `${page}.header`;
  // The flag is the page's and not a column's: a band the width of the body, or the body itself.
  // It is the one thing a pit wall draws across the divider, which is the point of drawing it.
  if (name.startsWith(`${page}.flag`)) return `${page}.body`;
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

/**
 * Where each page's band lies: the last 64 px of its left-hand column, the board's or the plot's.
 * The portrait board is 800 px under the header, and its column keeps the band's 64 px under it.
 */
const FLAG_BAND: Readonly<Record<'race' | 'tower' | 'telemetry' | 'portrait', Rect>> = {
  race: rect(0, LANDSCAPE.height - 64, 1280, 64),
  tower: rect(0, LANDSCAPE.height - 64, 880, 64),
  telemetry: rect(0, LANDSCAPE.height - 64, 1280, 64),
  portrait: rect(0, BODY_TOP + 800, 1080, 64),
};

/**
 * The flag, which is the page's and not a readout's.
 *
 * The header carried a colour block and a word until a rig reported that it did not work, and the
 * wall now answers the companion's three-way question instead: off, a band at the foot of the
 * page's left-hand column, or the body. What is pinned here is the geometry, because that is the
 * part a page's own layout can break -- a band that started at the top of the *screen* would cover
 * the header that says which page it is, a band under the header covers the board's headings and
 * P1 (#1016), and a full flag that stopped at a column would be a flag over half a wall.
 */
describe('the flag belongs to the page', () => {
  const layerOf = (screen: Screen, name: string): Extract<Item, { kind: 'layer' }> => {
    const layer = screen.items.find((i): i is Extract<Item, { kind: 'layer' }> => i.kind === 'layer' && i.name === name);
    if (!layer) throw new Error(`no layer named ${name}`);
    return layer;
  };
  const visibleOf = (item: Item): string => String(item.bindings?.Visible?.formula ?? '');

  for (const [page, screen, size] of [
    ['race', RACE, LANDSCAPE],
    ['tower', TOWER, LANDSCAPE],
    ['telemetry', TELEMETRY, LANDSCAPE],
    ['portrait', PORTRAIT, PORTRAIT_SIZE],
  ] as const) {
    const bandArea = FLAG_BAND[page];
    test(`${page} draws both formats over its body and neither over its header`, () => {
      const band = layerOf(screen, `${page}.flag`);
      const full = layerOf(screen, `${page}.flagFull`);
      expect({ band: visibleOf(band), full: visibleOf(full) }).toEqual({
        band: "(isnull([OpenDash.PitWallFlagFormat], 'band')) = ('band')",
        full: "(isnull([OpenDash.PitWallFlagFormat], 'band')) = ('full')",
      });
      // The band is the header's own height on every page, one object on a wall of any shape where a
      // proportion would draw it half as tall on the page twice as wide, at the foot of the column.
      const bandBoxes = [...walkItems(band.children)].filter((i): i is Placed => i.kind !== 'layer');
      const fullBoxes = [...walkItems(full.children)].filter((i): i is Placed => i.kind !== 'layer');
      expect(bandBoxes.length).toBeGreaterThan(0);
      expect(fullBoxes.length).toBeGreaterThan(0);
      const bodyBand = bandArea;
      expect(bodyBand.height).toBe(PIT_WALL_HEADER.height);
      const body = rect(0, BODY_TOP, size.width, size.height - BODY_TOP);
      for (const item of bandBoxes) {
        const slack = item.kind === 'text' ? Math.ceil(0.25 * item.fontSize) + 2 : 0;
        expect({ page, item: item.name, inside: contains(rect(bodyBand.left, bodyBand.top - slack, bodyBand.width, bodyBand.height + 2 * slack), item.rect) }).toMatchObject({ inside: true });
      }
      for (const item of fullBoxes) {
        expect({ page, item: item.name, inside: contains(body, item.rect) }).toMatchObject({ inside: true });
      }
    });
  }
});

/**
 * The band shown, and what it leaves readable (#1016).
 *
 * The band drew under the header, the width of the page, and a board's 32 px header row is followed
 * at once by P1: under a full course yellow the race and tower boards lost their column headings and
 * the leader's row, and the panels and zones beside them were cut through their titles. Every page
 * is laid out here as SimHub draws it, each stamped row at its own copy's place, and whatever the
 * band overlaps is named. The board's header row and its first row are never among them, and what
 * is is pinned page by page, so a layout that moves under the band is a diff somebody reads.
 */
describe('the flag band leaves the board readable', () => {
  /** Every drawn item outside the two flag layers, each stamped copy where SimHub stamps it. */
  const under = (screen: Screen, page: string): { name: string; rect: Rect; layers: readonly string[] }[] =>
    [...drawnParts(screen.items)]
      .filter(({ layers }) => !layers.some((l) => l.name === `${page}.flag` || l.name === `${page}.flagFull`))
      .map(({ item, layers }) => ({ name: item.name, rect: item.rect, layers: layers.map((l) => l.name) }));

  /**
   * The band as the page draws it: the box around everything its layer paints but its words, whose
   * line boxes run past the ink. Read off the page rather than off {@link FLAG_BAND}, so that a band
   * put back under the header fails here and not only in the test that pins where it is.
   */
  const bandOf = (screen: Screen, page: string): Rect => {
    const layer = screen.items.find((i) => i.kind === 'layer' && i.name === `${page}.flag`);
    if (layer?.kind !== 'layer') throw new Error(`no flag band on ${page}`);
    const boxes = [...walkItems(layer.children)].filter((i): i is Placed => i.kind !== 'layer' && i.kind !== 'text').map((i) => i.rect);
    const left = Math.min(...boxes.map((b) => b.left));
    const top = Math.min(...boxes.map((b) => b.top));
    return rect(left, top, Math.max(...boxes.map(right)) - left, Math.max(...boxes.map(bottom)) - top);
  };

  /** What the band covers: the items whose box overlaps it, by name and by the row it sits in. */
  const covered = (screen: Screen, page: string) => under(screen, page).filter((i) => overlaps(i.rect, bandOf(screen, page)));

  for (const [page, screen, board, pitch] of [
    ['race', RACE, 'race.board', 34],
    ['tower', TOWER, 'tower.board', 28],
    ['portrait', PORTRAIT, 'portrait.board', 32],
  ] as const) {
    test(`${page}: the board's header row and first row are clear of the band`, () => {
      const items = under(screen, page);
      const head = items.filter((i) => i.name.startsWith(`${board}.head`));
      const first = boardOf(screen, board);
      const firstRow = items.filter((i) => i.layers.includes(`${board}.rows`) && i.rect.top < first.top + pitch);
      expect(head.length).toBeGreaterThan(0);
      expect(firstRow.length).toBeGreaterThan(0);
      for (const item of [...head, ...firstRow]) {
        expect({ page, item: item.name, rect: item.rect, clear: !overlaps(item.rect, bandOf(screen, page)) }).toMatchObject({ clear: true });
      }
    });
  }

  test('race and tower: the band takes the foot of the board column, which no row reaches', () => {
    expect({ race: bandOf(RACE, 'race'), tower: bandOf(TOWER, 'tower') }).toEqual({ race: FLAG_BAND.race, tower: FLAG_BAND.tower });
    expect(covered(RACE, 'race')).toEqual([]);
    expect(covered(TOWER, 'tower')).toEqual([]);
    // The tower's thirty-three rows end exactly where the band begins.
    expect(boardOf(TOWER, 'tower.board').bottom).toBe(FLAG_BAND.tower.top);
  });

  test('telemetry: the band covers the axis footer and the rule over it, and no trace', () => {
    expect([...new Set(covered(TELEMETRY, 'telemetry').map((i) => i.name))].sort()).toEqual([
      'telemetry.axisEnd',
      'telemetry.axisName',
      'telemetry.axisStart',
      'telemetry.steering.rule',
    ]);
  });

  test('portrait: the band takes the foot the board column keeps for it, under P24', () => {
    expect(bandOf(PORTRAIT, 'portrait')).toEqual(FLAG_BAND.portrait);
    expect(covered(PORTRAIT, 'portrait')).toEqual([]);
    // All twenty-four rows end exactly where the band begins, and the rule closing the column under it.
    const board = boardOf(PORTRAIT, 'portrait.board');
    expect(board.bottom).toBe(FLAG_BAND.portrait.top);
    const boardRule = placed(PORTRAIT).find((i) => i.name === 'portrait.boardRule');
    expect(boardRule?.rect.top).toBe(bottom(FLAG_BAND.portrait));
  });

});
