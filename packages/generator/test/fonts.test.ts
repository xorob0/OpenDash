/** TrueType name table reading against the bundled Barlow files. */

import { describe, expect, test } from 'bun:test';
import { fontKey, fontNamesOf, parseTtfNames, readTtfNames, subfamilyHasWeight } from '../src/fonts.ts';
import { BARLOW_CONDENSED_BOLD, BARLOW_CONDENSED_SEMIBOLD, BARLOW_MEDIUM } from './fixtures.ts';

describe('readTtfNames', () => {
  test('reads the typographic family and subfamily of Barlow Medium', () => {
    const names = readTtfNames(BARLOW_MEDIUM);
    expect(names.family).toBe('Barlow');
    expect(names.subfamily).toBe('Medium');
    expect(names.legacyFamily).toBe('Barlow Medium');
    expect(names.postScriptName).toBe('Barlow-Medium');
  });

  test('reads Barlow Condensed faces', () => {
    expect(readTtfNames(BARLOW_CONDENSED_SEMIBOLD)).toMatchObject({ family: 'Barlow Condensed', subfamily: 'SemiBold' });
    expect(readTtfNames(BARLOW_CONDENSED_BOLD)).toMatchObject({ family: 'Barlow Condensed', subfamily: 'Bold' });
  });

  test('rejects files that are not fonts', () => {
    expect(() => parseTtfNames(new TextEncoder().encode('not a font at all, really'))).toThrow(/Not a TrueType font/);
    expect(() => parseTtfNames(new Uint8Array(4))).toThrow(/Not a TrueType font/);
  });
});

describe('fontNamesOf', () => {
  test('falls back to the file name when the file is missing', () => {
    expect(fontNamesOf('/nowhere/BarlowCondensed-SemiBold.ttf')).toMatchObject({ family: 'BarlowCondensed', subfamily: 'SemiBold' });
    expect(fontNamesOf('/nowhere/Barlow.ttf')).toMatchObject({ family: 'Barlow', subfamily: 'Regular' });
  });

  test('reads the file when it exists', () => {
    expect(fontNamesOf(BARLOW_MEDIUM).family).toBe('Barlow');
  });
});

describe('matching helpers', () => {
  test('fontKey ignores spaces and case', () => {
    expect(fontKey('Barlow Condensed')).toBe('barlowcondensed');
    expect(fontKey('BarlowCondensed')).toBe(fontKey('Barlow  condensed'));
  });

  test('subfamilyHasWeight maps WPF weights to subfamily names', () => {
    expect(subfamilyHasWeight('SemiBold', 'SemiBold')).toBe(true);
    expect(subfamilyHasWeight('Semi Bold', 'SemiBold')).toBe(true);
    expect(subfamilyHasWeight('Regular', 'Normal')).toBe(true);
    expect(subfamilyHasWeight('Bold Italic', 'Bold')).toBe(true);
    expect(subfamilyHasWeight('Medium', 'Bold')).toBe(false);
    expect(subfamilyHasWeight('Heavy', 'Black')).toBe(true);
  });
});
