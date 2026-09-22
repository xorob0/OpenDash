import { describe, expect, test } from 'bun:test';
import { merge, stableName } from '../scripts/sync-shots.ts';

describe('stableName', () => {
  test.each([
    ['01-opendash-1280x480-gallery.png', 'gallery', 'opendash-1280x480.png'],
    ['14-opendash-pit-wall-portrait-gallery.png', 'gallery', 'opendash-pit-wall-portrait.png'],
    ['03-opendash-green.png', 'green', 'opendash.png'],
    ['page-lapTimes.png', 'gallery', 'page-lapTimes.png'],
    ['panel-lights.png', null, 'panel-lights.png'],
  ])('%s on %s -> %s', (file, scenario, expected) => {
    expect(stableName(file, scenario)).toBe(expected);
  });
});

describe('merge', () => {
  test('takes the run’s provenance and keeps the files it did not touch', () => {
    const before = {
      schema: 1 as const,
      version: '0.2.0-rc.1',
      commit: '4997cbd',
      date: '2026-09-16',
      simHubVersion: '9.12.6',
      scenario: 'green',
      files: { 'opendash.png': { kind: 'package' as const, package: 'OpenDash', width: 1920, height: 480, scenario: 'green' }, 'page-fuel.png': { kind: 'page' as const, page: 'fuel', width: 850, height: 480, scenario: 'green' } },
    };
    const after = merge(before, { version: '0.3.0-rc.5', commit: 'abc1234', date: '2026-09-23', simHubVersion: '9.12.6', scenario: 'gallery' }, {
      'opendash.png': { kind: 'package', package: 'OpenDash', width: 1920, height: 480, scenario: 'gallery' },
    });
    expect(after.version).toBe('0.3.0-rc.5');
    expect(after.scenario).toBe('gallery');
    expect(after.files['opendash.png']?.scenario).toBe('gallery');
    expect(after.files['page-fuel.png']?.scenario).toBe('green');
  });
});
