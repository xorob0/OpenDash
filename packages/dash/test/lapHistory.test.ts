/**
 * Module 19 against the drawing it is read from, `design/canvas/ZoneCatalogue.dc.html`.
 *
 * `secondScreens.test.ts` already measures every text of this page against its box, which is the
 * constraint and holds whatever the table is drawn to. What it cannot say is whether the table is
 * drawn to the *right* numbers, so the counts, the column widths and the colours are written out
 * here, off the catalogue rather than imported from the module, and a page that quietly changed
 * would fail this file rather than pass against its own new answer.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import type { Item, LayerItem, Rect, TextItem } from '../src/generator.ts';
import { DELTA_THRESHOLDS, lapHistory } from '../src/modules/lapHistory.ts';
import { densityOf, type Density } from '../src/second/density.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { DELTA_WIDEST } from '../src/second/values.ts';
import { widthAsDrawn } from './drawnStrings.ts';
import { evalNcalc } from './ncalcEval.ts';
import { moduleBoxes } from './secondScreens.test.ts';

const built = (width: number, height: number, density: Density = 'zone'): Item[] =>
  lapHistory.build({ frame: rect(0, 0, width, height), density, prefix: '', page: 'lapHistory' });

const builtIn = (frame: Rect, density: Density): Item[] => lapHistory.build({ frame, density, prefix: '', page: 'lapHistory' });

const textsIn = (items: readonly Item[]): TextItem[] => [...walkItems(items)].filter((i): i is TextItem => i.kind === 'text');

const named = (items: readonly Item[], name: string): TextItem | undefined => textsIn(items).find((i) => i.name === name);

const rowsLayer = (items: readonly Item[]): LayerItem | undefined => [...walkItems(items)].find((i): i is LayerItem => i.kind === 'layer' && i.name.endsWith('rows'));

/** The stamped row count: a repeated layer draws itself and then repeats. */
const rowsDrawn = (items: readonly Item[]): number => (rowsLayer(items)?.repetitions ?? 0) + 1;

/** The distance from one row to the next, which is the row's height: the rows are stacked flush. */
const pitchOf = (items: readonly Item[]): number => rowsLayer(items)?.repeatTopOffset ?? 0;

const bound = (item: TextItem, target: 'TextColor' | 'Text'): string | undefined => {
  const b = item.bindings?.[target];
  return b && b.mode === 'formula' && typeof b.formula === 'string' ? b.formula : undefined;
};

describe('how many laps the page lists', () => {
  // Six at the two boxes the catalogue gives a header, seven at the two tall ones. The build used
  // to list whatever the box held, which put ten rows of 26 px in a zone the drawing gives seven.
  test.each([
    ['wide', 6],
    ['grid', 6],
    ['tallNarrow', 7],
    ['tall', 7],
  ] as const)('%s lists %i', (archetype, expected) => {
    const size = SHAPE_ARCHETYPES[archetype];
    expect(rowsDrawn(built(size.width, size.height))).toBe(expected);
  });

  test('a box too short for its count lists fewer rather than drawing past its foot', () => {
    // Zone C of the 600 x 686 face: 98 px under the header holds four rows of 20, not the six its
    // own sheet draws.
    expect(rowsDrawn(built(580, 114, 'compact'))).toBe(4);
  });

  test('and never more rows than SimHub keeps previous laps', () => {
    expect(rowsDrawn(built(360, 1200))).toBeLessThanOrEqual(10);
  });
});

/**
 * #343: which of the three a driver's eye lands on.
 *
 * Every drawing of the page draws the lap number at the time's size and tells the two apart by colour
 * alone, and the build did the same, so the page read as a block of digits. The lap number is an index
 * and is drawn a step down the density ramp from the values: 24 under 34, 16 under 24, and 14 under
 * the compact zone's 18.
 */
describe('the lap number is an index, not a peer of the values', () => {
  const INDEX_UNDER: Record<number, number> = { 34: 24, 24: 16, 18: 14 };

  for (const box of moduleBoxes()) {
    test(`on a ${box.name}`, () => {
      const items = builtIn(box.frame, box.density);
      const lap = named(items, 'row.lap')!;
      const time = named(items, 'row.time')!;
      expect({ box: box.name, time: time.fontSize, lap: lap.fontSize }).toEqual({ box: box.name, time: time.fontSize, lap: INDEX_UNDER[time.fontSize]! });
      const delta = named(items, 'row.delta');
      if (delta) expect({ box: box.name, delta: delta.fontSize }).toEqual({ box: box.name, delta: time.fontSize });
    });
  }

  test('in the label grey, left in the row padding', () => {
    const lap = named(built(600, 280), 'row.lap')!;
    expect(lap.rect.left).toBe(8);
    expect(lap.textColor).toBe(ds.color.text.label);
    expect(lap.hAlign).toBe('left');
  });

  test('and on the row centre line beside the time, as a list centres its cells', () => {
    const items = built(600, 280);
    const centre = (item: TextItem): number => item.rect.top + item.fontSize * 0.1 + item.fontSize / 2;
    expect(Math.abs(centre(named(items, 'row.lap')!) - centre(named(items, 'row.time')!))).toBeLessThanOrEqual(1);
  });
});

/**
 * #343: the row height answers the box, in the order a list answers it (#328): the declared rows, then
 * the type they can carry, then the rest as the space between them.
 */
describe('the row answers the box', () => {
  test('the catalogue draws its four boxes at 34 px, and so does the build', () => {
    // `wide`, `grid` and `tall` are drawn at 34 in 34 px rows; `tall narrow` is drawn at 24 in 28,
    // with a fuel column the build does not have, and the two columns it does have fit at 34.
    for (const archetype of ['wide', 'grid', 'tallNarrow', 'tall'] as const) {
      const size = SHAPE_ARCHETYPES[archetype];
      expect({ archetype, time: named(built(size.width, size.height), 'row.time')!.fontSize }).toEqual({ archetype, time: 34 });
    }
  });

  test('the rows fill the body of every box the build produces', () => {
    for (const box of moduleBoxes()) {
      const items = builtIn(box.frame, box.density);
      const d = densityOf(box.density);
      const head = named(items, 'head.lap') !== undefined;
      const body = box.frame.height - (head ? d.headerHeight : 0);
      const rows = rowsDrawn(items);
      const pitch = pitchOf(items);
      // Filled to within a pixel a row, which is what flooring the pitch leaves; and never a row
      // shorter than the one the density draws, which is where the count was made.
      expect({ box: box.name, fills: rows * pitch <= body && body - rows * pitch < rows, floor: pitch >= d.rowHeight }).toEqual({ box: box.name, fills: true, floor: true });
    }
  });

  test('zone C of the 1280 x 720 face lists its seven laps down the zone, not at its top edge', () => {
    // 445 x 516 of body. The page drew seven rows of 26 px at 24 there, 182 px of list over 334 of
    // nothing; the laps now span the zone at the catalogue's 34.
    const items = built(445, 516);
    expect({ rows: rowsDrawn(items), pitch: pitchOf(items), time: named(items, 'row.time')!.fontSize }).toEqual({ rows: 7, pitch: 73, time: 34 });
  });

  test('the narrow zones of the 850 x 480 and 800 x 480 faces draw the time at 34', () => {
    // 250 and 225 px wide, 290 high: seven rows of 41. The compact zone's own row is 20 px at 18, which
    // is what the page drew there, in 140 px of a 290 px zone.
    for (const width of [250, 225]) {
      const items = built(width, 290, 'compact');
      expect({ width, rows: rowsDrawn(items), pitch: pitchOf(items), time: named(items, 'row.time')!.fontSize, lap: named(items, 'row.lap')!.fontSize }).toEqual({ width, rows: 7, pitch: 41, time: 34, lap: 24 });
    }
  });

  test('a row short of the 34 px step keeps the type its height allows', () => {
    // The pit wall's reference zone, 607 x 196 of body: six rows of 29 at the 24 px its sheet draws.
    const board = built(607, 196, 'panel');
    expect({ rows: rowsDrawn(board), pitch: pitchOf(board), time: named(board, 'row.time')!.fontSize }).toEqual({ rows: 6, pitch: 29, time: 24 });
    // The 800 x 286 face's zone B, 245 x 156: seven rows of 22 at the compact zone's own 18.
    const nano = built(245, 156, 'compact');
    expect({ rows: rowsDrawn(nano), pitch: pitchOf(nano), time: named(nano, 'row.time')!.fontSize }).toEqual({ rows: 7, pitch: 22, time: 18 });
  });

  test('the width is an edge as well as the height', () => {
    // Four hundred pixels is height enough for 34 in all three; what refuses it is the 147 px time
    // beside its 50 px lap number, which a 200 px zone cannot hold, and then the 24 px step's 104
    // beside 34, which a 150 px one cannot either. Below that is the density's own drawing.
    const time = (width: number): number => named(built(width, 400, 'compact'), 'row.time')!.fontSize;
    expect({ at250: time(250), at200: time(200), at150: time(150) }).toEqual({ at250: 34, at200: 24, at150: 18 });
  });

  test('and a box however tall buys space between the laps, never a larger lap', () => {
    // 34 is the largest the canvas draws a list at, anywhere.
    expect(named(built(360, 1200), 'row.time')!.fontSize).toBe(34);
  });
});

describe('the columns the catalogue draws', () => {
  const items = built(600, 280);

  test('the time is the catalogue\'s 104 at 24 px scaled to 34, a cell gap after the lap', () => {
    // The lap number's column is its four cells at 24 and their slack, 50, from the padding's 8.
    const time = named(items, 'row.time')!;
    expect({ left: time.rect.left, width: time.rect.width, align: time.hAlign }).toEqual({ left: 8 + 50 + 12, width: 147, align: 'left' });
  });

  test('the delta is the catalogue\'s 86 scaled to 34, right aligned and spread to the far edge of the padding', () => {
    const delta = named(items, 'row.delta')!;
    expect({ width: delta.rect.width, align: delta.hAlign, right: delta.rect.left + delta.rect.width }).toEqual({ width: 122, align: 'right', right: 592 });
  });

  test('the header names the columns the row draws, and no more', () => {
    expect(textsIn(items).filter((i) => i.name.startsWith('head.')).map((i) => i.text)).toEqual(['Lap', 'Time', 'Δ best']);
  });

  test('each heading stands over its column', () => {
    for (const id of ['lap', 'time', 'delta']) {
      const head = named(items, `head.${id}`)!;
      const cell = named(items, `row.${id}`)!;
      expect({ id, left: head.rect.left, width: head.rect.width }).toEqual({ id, left: cell.rect.left, width: cell.rect.width });
    }
  });

  test('a narrower shape lists lap and time alone: the catalogue draws fuel there and SimHub publishes none', () => {
    for (const [width, height] of [[430, 300], [274, 300], [360, 470]] as const) {
      const at = built(width, height);
      expect({ width, head: named(at, 'head.delta') === undefined, row: named(at, 'row.delta') === undefined }).toEqual({ width, head: true, row: true });
    }
  });
});

/**
 * #343's third clause: the catalogue writes a fuel target into the header, `Fuel · target 2.85` at
 * `wide` and the bare `Fuel` at `grid`, and the build refuses it with the column it heads. No
 * previous-lap property carries a consumption, so there is no fuel column, and a target beside the
 * delta's heading would read as a target for the delta. The number is also a setting nobody holds,
 * which is #326.
 */
describe('the fuel target is refused with its column', () => {
  for (const box of moduleBoxes()) {
    test(`on a ${box.name}`, () => {
      const heads = textsIn(builtIn(box.frame, box.density)).filter((i) => i.name.startsWith('head.'));
      expect({ box: box.name, fuel: heads.filter((i) => /fuel|target/i.test(i.text)).map((i) => i.text) }).toEqual({ box: box.name, fuel: [] });
    });
  }
});

describe('the delta to the session best is coloured in three bands', () => {
  const delta = named(built(600, 280), 'row.delta')!;
  const formula = bound(delta, 'TextColor')!;

  test('caution and danger are two colours rather than one', () => {
    // They were the same red: the caution branch read `purpose.fuel.low`, which resolves to
    // `color.danger.primary`, so half a second behind and a second behind looked alike.
    expect(ds.color.caution.primary).not.toBe(ds.purpose.delta.slower);
    expect(formula).toContain(ds.color.caution.primary);
    expect(formula).toContain(ds.purpose.delta.slower);
  });

  test('the session best is purple, and the thresholds are the ones the module declares', () => {
    expect(formula).toContain(ds.purpose.lap.sessionBest);
    expect(formula).toContain(String(DELTA_THRESHOLDS.caution));
    expect(formula).toContain(String(DELTA_THRESHOLDS.danger));
  });

  test('and the best lap carries that purple on its time as well', () => {
    expect(bound(named(built(600, 280), 'row.time')!, 'TextColor')).toContain(ds.purpose.lap.sessionBest);
  });
});

/**
 * #886: the column is cut for `+0.594`, five digit cells and a point, which leaves one whole digit at
 * three places. A lap ten seconds off the session best is common and the in-lap after a stop is a
 * minute or three off it, and the row keeps that lap for ten laps; drawn to three places regardless,
 * `+12.594` lost its last digit and `+123.456` its last two.
 */
describe('the delta keeps every reading inside its column, giving up places rather than digits', () => {
  const delta = named(built(600, 280), 'row.delta')!;
  // Row one, which is slot zero: the lap just completed.
  const formula = bound(delta, 'Text')!.replace(/\brepeatindex\(\)/g, '1');
  const drawn = (seconds: number): unknown => evalNcalc(formula, { 'PersistantTrackerPlugin.PreviousLap_00_DeltaToSessionBest': seconds });

  test('three places to ten seconds, two to a hundred, one to a thousand, and whole seconds past that', () => {
    // Written out rather than computed, and away from any rounding edge.
    const READINGS: readonly [number, string][] = [
      [0.594, '+0.594'],
      [-0.231, '−0.231'],
      [12.594, '+12.59'],
      [-45.678, '−45.68'],
      [123.456, '+123.5'],
      [-187.24, '−187.2'],
      [1234.56, '+1235'],
    ];
    for (const [seconds, text] of READINGS) expect({ seconds, drawn: drawn(seconds) }).toEqual({ seconds, drawn: text });
  });

  test('and the column is measured by the widest of them', () => {
    expect(delta.widest).toBe(DELTA_WIDEST);
    for (const seconds of [9.876, 98.765, 987.65, 9876.5]) {
      expect({ seconds, fits: widthAsDrawn(delta, String(drawn(seconds))) <= widthAsDrawn(delta, DELTA_WIDEST) }).toEqual({ seconds, fits: true });
    }
  });
});
