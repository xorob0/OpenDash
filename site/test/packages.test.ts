import { describe, expect, test } from 'bun:test';
import { inReadingOrder, kindLabel, sizeLabel, slug, weigh } from '../lib/packages.ts';
import type { SitePackage } from '../scripts/content.ts';

describe('slug', () => {
  test.each([
    ['OpenDash', 'opendash'],
    ['OpenDash 1280x480', 'opendash-1280x480'],
    ['OpenDash 480 round', 'opendash-480-round'],
    ['OpenDash Pit wall portrait', 'opendash-pit-wall-portrait'],
  ])('%s -> %s', (folder, expected) => {
    expect(slug(folder)).toBe(expected);
  });
});

describe('labels', () => {
  test('a rectangle reads width by height and a round face reads its diameter', () => {
    expect(sizeLabel({ width: 1920, height: 480, round: false })).toBe('1920 × 480');
    expect(sizeLabel({ width: 480, height: 480, round: true })).toBe('480 round');
  });

  test('a kind has a word', () => {
    expect([kindLabel('dash'), kindLabel('companion'), kindLabel('pitwall')]).toEqual(['Face', 'Companion', 'Pit wall']);
  });

  test('a weight is a short number with its unit', () => {
    expect(weigh(1_300_000)).toBe('1.2 MB');
    expect(weigh(180_000)).toBe('176 KB');
    expect(weigh(10)).toBe('1 KB');
  });
});

describe('reading order', () => {
  const p = (folder: string, kind: SitePackage['kind'], width: number, height: number): SitePackage => ({
    folder,
    kind,
    width,
    height,
    file: `${folder}.simhubdash`,
    round: /round/.test(folder),
  });

  test('reference face first, widest next, round after rectangular, then companions, then pit walls', () => {
    const shuffled = [
      p('OpenDash Pit wall', 'pitwall', 1920, 1080),
      p('OpenDash 480 round', 'dash', 480, 480),
      p('OpenDash 850x480', 'dash', 850, 480),
      p('OpenDash Companion', 'companion', 850, 480),
      p('OpenDash 1280x400', 'dash', 1280, 400),
      p('OpenDash', 'dash', 1920, 480),
      p('OpenDash 1280x720', 'dash', 1280, 720),
    ];
    expect(inReadingOrder(shuffled).map((x) => x.folder)).toEqual([
      'OpenDash',
      'OpenDash 1280x720',
      'OpenDash 1280x400',
      'OpenDash 850x480',
      'OpenDash 480 round',
      'OpenDash Companion',
      'OpenDash Pit wall',
    ]);
  });
});
