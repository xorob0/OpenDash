/** Colour normalisation to SimHub's #AARRGGBB. */

import { describe, expect, test } from 'bun:test';
import { TRANSPARENT, hexToArgb, isHex, isNormalisedHex, normaliseHex, normalizeHex, withAlpha } from '../src/color.ts';

describe('normaliseHex', () => {
  test('adds an opaque alpha to #RRGGBB', () => {
    expect(normaliseHex('#0A0B0D')).toBe('#FF0A0B0D');
    expect(normaliseHex('#33d9f2')).toBe('#FF33D9F2');
  });

  test('keeps #AARRGGBB and upper-cases it', () => {
    expect(normaliseHex('#00ffffff')).toBe('#00FFFFFF');
    expect(normaliseHex('#80FFB300')).toBe('#80FFB300');
  });

  test('rejects anything else', () => {
    for (const bad of ['', '#', '0A0B0D', '#0A0B0', '#0A0B0DFF0', '#GGGGGG', 'red', '#0a0b0d ', ' #0a0b0d', '#0A0B0D\n']) {
      expect(() => normaliseHex(bad)).toThrow(TypeError);
    }
  });

  test('normalizeHex is the same function', () => {
    expect(normalizeHex).toBe(normaliseHex);
  });
});

describe('predicates', () => {
  test('isHex accepts both lengths in any case', () => {
    expect(isHex('#abcdef')).toBe(true);
    expect(isHex('#ABCDEF01')).toBe(true);
    expect(isHex('#abcde')).toBe(false);
    expect(isHex(12)).toBe(false);
    expect(isHex(undefined)).toBe(false);
  });

  test('isNormalisedHex only accepts the serialised form', () => {
    expect(isNormalisedHex('#FF0A0B0D')).toBe(true);
    expect(isNormalisedHex('#ff0a0b0d')).toBe(false);
    expect(isNormalisedHex('#0A0B0D')).toBe(false);
  });

  test('TRANSPARENT is SimHub transparent', () => {
    expect(TRANSPARENT).toBe('#00FFFFFF');
  });
});

describe('withAlpha and hexToArgb', () => {
  test('replaces the alpha channel from a 0..1 fraction', () => {
    expect(withAlpha('#F5F7FA', 0)).toBe('#00F5F7FA');
    expect(withAlpha('#F5F7FA', 1)).toBe('#FFF5F7FA');
    expect(withAlpha('#80F5F7FA', 0.5)).toBe('#80F5F7FA');
    expect(() => withAlpha('#F5F7FA', 1.5)).toThrow(RangeError);
    expect(() => withAlpha('#F5F7FA', Number.NaN)).toThrow(RangeError);
  });

  test('splits channels', () => {
    expect(hexToArgb('#80F5F7FA')).toEqual({ a: 128, r: 245, g: 247, b: 250 });
    expect(hexToArgb('#0A0B0D')).toEqual({ a: 255, r: 10, g: 11, b: 13 });
  });
});
