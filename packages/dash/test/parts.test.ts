/**
 * The parts and the layer each is built from. An element is built from nothing, a component from
 * elements, so an import the other way round is the layering giving out; and the list row, the one
 * part of the vocabulary that is a filled rectangle, has to keep its three readings inside the
 * rectangle it fills.
 */
import { describe, expect, test } from 'bun:test';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { measureText } from '../src/design/advances.ts';
import { LINE_SPACING } from '../src/design/metrics.ts';
import { rect } from '../src/design/geometry.ts';
import { bar, BAR_HEIGHT } from '../src/elements/bar.ts';
import { dot, DOT_SIZE } from '../src/elements/dot.ts';
import { listRow, listRowHeight, LIST_ROW } from '../src/components/listRow.ts';
import { CHARS } from '../src/second/values.ts';
import { ds } from '../src/tokens.ts';
import type { TextItem } from '../src/generator.ts';

const SRC = join(import.meta.dir, '..', 'src');

const sourcesIn = (dir: string): { file: string; text: string }[] =>
  readdirSync(join(SRC, dir))
    .filter((f) => f.endsWith('.ts'))
    .map((f) => ({ file: `${dir}/${f}`, text: readFileSync(join(SRC, dir, f), 'utf8') }));

describe('the element layer is built from nothing above it', () => {
  for (const { file, text } of sourcesIn('elements')) {
    test(file, () => {
      const offenders = [...text.matchAll(/from '(\.\.\/(?:components|cards|layouts|modules|zones|hero)\/[^']+)'/g)].map((m) => m[1]);
      expect({ file, offenders }).toEqual({ file, offenders: [] });
    });
  }
});

describe('dot', () => {
  test('is a square of the element sheet\'s size', () => {
    const d = dot('d', 10, 20, ds.color.text.primary);
    expect(d.rect).toEqual(rect(10, 20, DOT_SIZE, DOT_SIZE));
    expect(DOT_SIZE).toBe(6);
  });

  test('takes the size of a sheet that draws it larger', () => {
    expect(dot('d', 0, 0, ds.color.text.primary, { size: 8 }).rect).toEqual(rect(0, 0, 8, 8));
  });
});

describe('bar', () => {
  test('is four pixels with the track raised and the fill primary', () => {
    const b = bar('b', 0, 0, 120, 'x');
    expect(BAR_HEIGHT).toBe(4);
    expect({ height: b.rect.height, track: b.backgroundColor, fill: b.gaugeColor }).toEqual({
      height: 4,
      track: ds.color.surface.raised,
      fill: ds.color.text.primary,
    });
  });
});

describe('listRow', () => {
  /** The canvas's own sample: 230 wide, "P4", "YOU" and "0.000" at rung S. */
  const frame = rect(0, 0, 230, listRowHeight());
  const items = listRow(
    'row',
    frame,
    {
      position: { sample: 'P4', chars: CHARS.position },
      name: { text: 'YOU' },
      gap: { sample: '0.000', chars: CHARS.relativeGap },
    },
  );
  const texts = items.filter((i): i is TextItem => i.kind === 'text');

  test('fills the row in purpose.block.fill', () => {
    const fill = items.find((i) => i.name === 'row.fill');
    expect(fill).toMatchObject({ kind: 'rect', backgroundColor: ds.purpose.block.fill, rect: frame });
  });

  test('draws the position, the name and the gap', () => {
    expect(texts.map((t) => t.text)).toEqual(['P4', '0.000', 'YOU']);
  });

  test('keeps every reading inside the padded rect', () => {
    const left = frame.left + LIST_ROW.padX;
    const right = frame.left + frame.width - LIST_ROW.padX;
    for (const t of texts) {
      expect({ item: t.name, inside: t.rect.left >= left && t.rect.left + t.rect.width <= right }).toEqual({ item: t.name, inside: true });
    }
  });

  test('keeps every reading inside the row, line box included', () => {
    for (const t of texts) {
      expect({ item: t.name, top: t.rect.top >= frame.top, bottom: t.rect.top + t.rect.height <= frame.top + frame.height }).toEqual({
        item: t.name,
        top: true,
        bottom: true,
      });
    }
  });

  test('every reading fits the box WPF clips it to', () => {
    for (const t of texts) {
      const mono = t.monospace;
      const drawn = t.widest ?? t.text;
      const width = mono
        ? [...drawn].reduce((sum, c) => sum + (mono.specialChars?.includes(c) ? mono.specialCharsWidth : mono.charWidth), 0)
        : measureText('BarlowMedium', drawn, t.fontSize);
      expect({ item: t.name, fits: width < t.rect.width }).toEqual({ item: t.name, fits: true });
      expect({ item: t.name, line: LINE_SPACING * t.fontSize <= t.rect.height }).toEqual({ item: t.name, line: true });
    }
  });

  test('drops a name the remainder cannot hold rather than clipping it', () => {
    const narrow = listRow(
      'row',
      rect(0, 0, 120, listRowHeight()),
      {
        position: { sample: 'P4', chars: CHARS.position },
        name: { text: 'Tomasz Kowalczyk' },
        gap: { sample: '0.000', chars: CHARS.relativeGap },
      },
    );
    expect(narrow.some((i) => i.name === 'row.name')).toBe(false);
  });
});
