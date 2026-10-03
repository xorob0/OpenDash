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
  test('keeps the files it did not touch, and each capture keeps its own scenario', () => {
    const stamp = { version: '0.2.0-rc.1', commit: '4997cbd', date: '2026-09-16', simHubVersion: '9.12.6' };
    const before = {
      schema: 2 as const,
      files: {
        'opendash.png': { kind: 'package' as const, package: 'OpenDash', width: 1920, height: 480, scenario: 'green', ...stamp },
        'page-fuel.png': { kind: 'page' as const, page: 'fuel', width: 850, height: 480, scenario: 'green', ...stamp },
      },
    };
    const after = merge(before, { version: '0.3.0-rc.5', commit: 'abc1234', date: '2026-09-23', simHubVersion: '9.12.6' }, {
      'opendash.png': { kind: 'package', package: 'OpenDash', width: 1920, height: 480, scenario: 'gallery' },
    });
    expect(after.schema).toBe(2);
    expect(Object.keys(after.files).sort()).toEqual(['opendash.png', 'page-fuel.png']);
    expect(after.files['opendash.png']?.scenario).toBe('gallery');
    expect(after.files['page-fuel.png']).toEqual(before.files['page-fuel.png']);
  });
});

describe('a partial sync', () => {
  // A schema 1 sidecar, as it was committed until #627: one run's provenance for every file.
  const before = {
    schema: 1 as const,
    version: '0.2.0-rc.1',
    commit: '4997cbd',
    date: '2026-09-16',
    simHubVersion: '9.12.6',
    scenario: 'green',
    files: {
      'opendash.png': { kind: 'package' as const, package: 'OpenDash', width: 1920, height: 480, scenario: 'green' },
      'page-fuel.png': { kind: 'page' as const, page: 'fuel', width: 850, height: 480, scenario: 'green' },
    },
  };
  const run = { version: '0.3.0-rc.5', commit: 'abc1234', date: '2026-09-23', simHubVersion: '9.12.6' };
  const after = merge(before, run, { 'opendash.png': { kind: 'package', package: 'OpenDash', width: 1920, height: 480, scenario: 'gallery' } });

  test('stamps the captures it brought with the run that took them', () => {
    expect(after.files['opendash.png']).toMatchObject({ version: '0.3.0-rc.5', commit: 'abc1234', date: '2026-09-23' });
  });

  test('leaves the captures it did not bring with the run that took them, not the newest one', () => {
    expect(after.files['page-fuel.png']).toMatchObject({ version: '0.2.0-rc.1', commit: '4997cbd', date: '2026-09-16' });
  });
});
