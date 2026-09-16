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
import type { Item, LayerItem, TextItem } from '../src/generator.ts';
import { DELTA_THRESHOLDS, lapHistory } from '../src/modules/lapHistory.ts';
import type { Density } from '../src/second/density.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';

const built = (width: number, height: number, density: Density = 'zone'): Item[] =>
  lapHistory.build({ frame: rect(0, 0, width, height), density, prefix: '', page: 'lapHistory' });

const textsIn = (items: readonly Item[]): TextItem[] => [...walkItems(items)].filter((i): i is TextItem => i.kind === 'text');

const named = (items: readonly Item[], name: string): TextItem | undefined => textsIn(items).find((i) => i.name === name);

/** The stamped row count: a repeated layer draws itself and then repeats. */
const rowsDrawn = (items: readonly Item[]): number => {
  const layer = [...walkItems(items)].find((i): i is LayerItem => i.kind === 'layer' && i.name.endsWith('rows'));
  return (layer?.repetitions ?? 0) + 1;
};

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

describe('the columns the catalogue draws', () => {
  const items = built(600, 280);

  test('the lap number is left in the row padding, in the label grey', () => {
    const lap = named(items, 'row.lap')!;
    expect(lap.rect.left).toBe(8);
    expect(lap.textColor).toBe(ds.color.text.label);
    expect(lap.hAlign).toBe('left');
  });

  test('the time is 104 wide, a cell gap after the lap', () => {
    const time = named(items, 'row.time')!;
    expect({ left: time.rect.left, width: time.rect.width, align: time.hAlign }).toEqual({ left: 70, width: 104, align: 'left' });
  });

  test('the delta is 86 wide, right aligned and spread to the far edge of the padding', () => {
    const delta = named(items, 'row.delta')!;
    expect({ width: delta.rect.width, align: delta.hAlign, right: delta.rect.left + delta.rect.width }).toEqual({ width: 86, align: 'right', right: 592 });
  });

  test('the header names the columns the row draws, and no more', () => {
    expect(textsIn(items).filter((i) => i.name.startsWith('head.')).map((i) => i.text)).toEqual(['LAP', 'TIME', 'Δ BEST']);
  });

  test('a narrower shape lists lap and time alone: the catalogue draws fuel there and SimHub publishes none', () => {
    for (const [width, height] of [[430, 300], [274, 300], [360, 470]] as const) {
      const at = built(width, height);
      expect({ width, head: named(at, 'head.delta') === undefined, row: named(at, 'row.delta') === undefined }).toEqual({ width, head: true, row: true });
    }
  });
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
