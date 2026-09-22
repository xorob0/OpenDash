/**
 * What `scripts/rig.ts` decides without a VM: that every face it names exists, that a screen takes
 * the stock namespace for its size, and that the pages it sets are pages the catalogues have.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { BAND_D_PAGES, MODULE_CATALOGUE, ZONE_A_PAGES } from '../packages/dash/src/contract.ts';
import { GALLERY, galleryRig, screenFor, type ManifestPackage } from './rig.ts';

const manifestPath = path.resolve(import.meta.dir, '..', 'build', 'manifest.json');
const manifest: { packages: ManifestPackage[] } | null = existsSync(manifestPath) ? JSON.parse(readFileSync(manifestPath, 'utf8')) : null;

describe('a seeded screen', () => {
  const pkg: ManifestPackage = { folder: 'OpenDash 1280x480', kind: 'dash', width: 1280, height: 480, file: 'OpenDash 1280x480.simhubdash' };

  test('takes the stock namespace for its size, so its package is extracted unchanged', () => {
    expect(screenFor(pkg, ['gearSpeedRevs', 'lapTimes', 'relative', 'fuel']).Namespace).toBe('Face1280x480');
  });

  test('is named after the package, because SimHub lists a dashboard by its title', () => {
    expect(screenFor(pkg, ['gearSpeedRevs', 'lapTimes', 'relative', 'fuel']).Name).toBe('OpenDash 1280x480');
  });

  test('sets the start pages as well as the current ones', () => {
    const face = screenFor(pkg, ['speed', 'tyres', 'leaderboard', 'stint']).Face as { Zones: number[]; Starts: number[] };
    expect(face.Zones).toEqual(face.Starts);
    expect(face.Zones[0]).toBe(ZONE_A_PAGES.find((p) => p.id === 'speed')!.number);
    expect(face.Zones[1]).toBe(MODULE_CATALOGUE.find((m) => m.id === 'tyres')!.number - 1);
    expect(face.Zones[3]).toBe(BAND_D_PAGES.find((p) => p.id === 'stint')!.number);
  });

  test('refuses a page no catalogue has', () => {
    expect(() => screenFor(pkg, ['gearSpeedRevs', 'nonesuch', 'relative', 'fuel'])).toThrow(/catalogue/);
  });
});

describe('the gallery rig', () => {
  test('shows a different pair of zones on every face', () => {
    const pairs = Object.values(GALLERY).map((g) => `${g.zones[1]}/${g.zones[2]}`);
    expect(new Set(pairs).size).toBe(pairs.length);
  });

  test('never opens a zone on a page that ships switched off', () => {
    const off = new Set(MODULE_CATALOGUE.filter((m) => !m.enabled).map((m) => m.id));
    for (const [folder, g] of Object.entries(GALLERY)) {
      expect({ folder, off: [g.zones[1], g.zones[2]].filter((id) => off.has(id)) }).toEqual({ folder, off: [] });
    }
  });

  test.if(manifest !== null)('names only packages the build produced', () => {
    expect(galleryRig(manifest!)).toHaveLength(Object.keys(GALLERY).length);
  });
});
