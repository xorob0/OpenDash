/**
 * The parts and the layer each is built from. An element is built from nothing, so an import from
 * a layer above it is the layering giving out; and the two elements the vocabulary was missing are
 * the sizes their sheet draws.
 */
import { describe, expect, test } from 'bun:test';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { rect } from '../src/design/geometry.ts';
import { bar, BAR_HEIGHT } from '../src/elements/bar.ts';
import { dot, DOT_SIZE } from '../src/elements/dot.ts';
import { ds } from '../src/tokens.ts';

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
