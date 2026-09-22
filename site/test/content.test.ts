/**
 * The generator's pure functions, held against the modules they read.
 *
 * `lib/content.generated.ts` is gitignored and may not exist when the root `bun test` runs, so
 * nothing here imports it. What is checked is that the functions which write it agree with the
 * catalogues in packages/dash, and with the build manifest when there is one.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { FLAG_CATALOGUE } from '../../packages/dash/src/flags.ts';
import { ALL_SHAPES, LEGACY_SHAPES } from '../../packages/dash/src/leds/strip.ts';
import { BASE_FACE } from '../../packages/dash/src/zones/index.ts';
import { flags, heroFace, releases, sitePackages, stripShapes, type Manifest } from '../scripts/content.ts';

const repoRoot = path.resolve(import.meta.dir, '..', '..');
const manifestPath = path.join(repoRoot, 'build', 'manifest.json');
const manifest: Manifest | null = existsSync(manifestPath) ? (JSON.parse(readFileSync(manifestPath, 'utf8')) as Manifest) : null;

describe('the strip shapes', () => {
  const shapes = stripShapes(ALL_SHAPES, LEGACY_SHAPES);

  test('are the sixty-two the build writes, five of them legacy', () => {
    expect(shapes).toHaveLength(62);
    expect(shapes.filter((s) => s.legacy)).toHaveLength(5);
  });

  test('have unique ids', () => {
    expect(new Set(shapes.map((s) => s.id)).size).toBe(shapes.length);
  });

  test('every sided grid shape has equal sides', () => {
    for (const s of shapes.filter((x) => !x.legacy)) expect(s.left).toBe(s.right);
  });

  test.if(manifest !== null)('each one has a profile in the manifest, and the flag box makes sixty-three', () => {
    const profiles = new Set(manifest!.ledProfiles ?? []);
    for (const s of shapes) expect(profiles.has(`OpenDash ${s.id}.ledsprofile`)).toBe(true);
    expect(profiles.size).toBe(63);
  });
});

describe('the hero face', () => {
  const face = heroFace(BASE_FACE);

  test('is the base size', () => {
    expect(face.folder).toBe('OpenDash 850x480');
    expect(face.width).toBe(850);
    expect(face.height).toBe(480);
  });

  test('has the six parts in reading order, each inside the face', () => {
    expect(face.parts.map((p) => p.id)).toEqual(['revBar', 'bar', 'zoneB', 'zoneA', 'zoneC', 'band']);
    for (const { rect } of face.parts) {
      expect(rect.left).toBeGreaterThanOrEqual(0);
      expect(rect.top).toBeGreaterThanOrEqual(0);
      expect(rect.left + rect.width).toBeLessThanOrEqual(face.width);
      expect(rect.top + rect.height).toBeLessThanOrEqual(face.height);
    }
  });

  test('puts zone A between B and C, and the band at the foot', () => {
    const by = Object.fromEntries(face.parts.map((p) => [p.id, p.rect]));
    expect(by.zoneB.left + by.zoneB.width).toBeLessThanOrEqual(by.zoneA.left);
    expect(by.zoneA.left + by.zoneA.width).toBeLessThanOrEqual(by.zoneC.left);
    expect(by.band.top + by.band.height).toBe(face.height);
  });
});

describe('the flags', () => {
  const list = flags(FLAG_CATALOGUE);

  test('are the fifteen conditions in catalogue order, red first', () => {
    expect(list).toHaveLength(15);
    expect(list[0]!.id).toBe('red');
    expect(list[list.length - 1]!.id).toBe('chequered');
  });

  test('rank every critical flag above every non-critical one', () => {
    const firstQuiet = list.findIndex((f) => !f.critical);
    expect(list.slice(firstQuiet).every((f) => !f.critical)).toBe(true);
  });
});

describe('the package list', () => {
  test('drops the superseded card faces and marks the round ones', () => {
    const packages = sitePackages({
      packages: [
        { folder: 'OpenDash slots 1920x480', kind: 'dash', width: 1920, height: 480, file: 'OpenDash slots 1920x480.simhubdash' },
        { folder: 'OpenDash 480 round', kind: 'dash', width: 480, height: 480, file: 'OpenDash 480 round.simhubdash' },
        { folder: 'OpenDash Pit wall', kind: 'pitwall', width: 1920, height: 1080, file: 'OpenDash Pit wall.simhubdash' },
      ],
    });
    expect(packages.map((p) => p.folder)).toEqual(['OpenDash 480 round', 'OpenDash Pit wall']);
    expect(packages.map((p) => p.round)).toEqual([true, false]);
  });

  test.if(manifest !== null)('is the fourteen packages the plugin installs', () => {
    expect(sitePackages(manifest!)).toHaveLength(14);
  });
});

describe('the releases', () => {
  test('are cut from the changelog with their dates', () => {
    const list = releases(readFileSync(path.join(repoRoot, 'CHANGELOG.md'), 'utf8'));
    expect(list.length).toBeGreaterThan(3);
    for (const r of list) {
      expect(r.body).not.toBe('');
      if (!r.unreleased) expect(r.date).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    }
  });
});
