/**
 * The row of sectors the pit wall's lap delta panel draws under its delta.
 *
 * Six fields where the box has room, three where it does not, and one colour across each pair. The
 * pair is the point: a sector and its delta are two readings of one fact, so a driver who saw them
 * disagree would have to work out which of the two to believe.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { sectorColour, sectorFields } from '../src/second/sectors.ts';
import { ds } from '../src/tokens.ts';
import type { TextItem } from '../src/generator.ts';

const row = (width: number): TextItem[] =>
  sectorFields('p.', rect(0, 0, width, 60), 'wide', 24).filter((i): i is TextItem => i.kind === 'text');

const labels = (width: number): string[] =>
  row(width)
    .filter((i) => i.name.endsWith('.label'))
    .map((i) => i.text);

describe('the sectors under a lap delta', () => {
  test('are the three times and then the three deltas, in that order', () => {
    // The sheet groups them rather than interleaving them, so the times read as a set and the
    // deltas as another; interleaved, a driver comparing two sectors reads across a delta.
    expect(labels(1248)).toEqual(['S1', 'S2', 'S3', 'Δ S1', 'Δ S2', 'Δ S3']);
  });

  test('are packed from the left edge at one pitch, not spread across thirds', () => {
    const lefts = row(1248)
      .filter((i) => i.name.endsWith('.label'))
      .map((i) => i.rect.left);
    expect(lefts[0]).toBe(0);
    const pitches = new Set(lefts.slice(1).map((left, i) => left - lefts[i]!));
    expect(pitches.size).toBe(1);
  });

  test('share one colour between a sector and its delta', () => {
    for (const sector of [1, 2, 3]) {
      const time = row(1248).find((i) => i.name === `p.s${sector}.value`);
      const delta = row(1248).find((i) => i.name === `p.delta${sector}.value`);
      const formula = (i?: TextItem): string => String(i?.bindings?.TextColor?.formula ?? '');
      expect(formula(time)).toBe(sectorColour(sector));
      expect(formula(delta)).toBe(formula(time));
    }
    // And that colour is the three the design names, purple for a session best over green and red.
    expect(sectorColour(1)).toContain(ds.purpose.lap.sessionBest);
    expect(sectorColour(1)).toContain(ds.purpose.delta.faster);
    expect(sectorColour(1)).toContain(ds.purpose.delta.slower);
  });

  test('fall back to three times carrying their deltas in their labels when six will not fit', () => {
    // The older form, and the one a zone was always given: a bound label reading "S1 · −0.29".
    const narrow = labels(240);
    expect(narrow).toHaveLength(3);
    expect(narrow.every((l) => !l.startsWith('Δ'))).toBe(true);
    const bound = row(240).find((i) => i.name === 'p.s1.label');
    expect(String(bound?.bindings?.Text?.formula ?? '')).toContain('S1');
  });

  test('never draw past the frame they are given, at any width', () => {
    for (const width of [1248, 800, 560, 400, 300, 240, 180]) {
      for (const item of row(width)) {
        expect({ width, name: item.name, inside: item.rect.left >= 0 && item.rect.left + item.rect.width <= width }).toEqual({
          width,
          name: item.name,
          inside: true,
        });
      }
    }
  });
});
