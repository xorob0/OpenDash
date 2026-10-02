/**
 * The generator's pure functions, held against the modules they read.
 *
 * `lib/content.generated.ts` is gitignored and may not exist when the root `bun test` runs, so
 * nothing here imports it. What is checked is that the functions which write it agree with the
 * catalogues in packages/dash, and with the build manifest when there is one.
 */
import { describe, expect, test } from 'bun:test';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { FLAG_CATALOGUE } from '../../packages/dash/src/flags.ts';
import { ALL_SHAPES, LEGACY_SHAPES } from '../../packages/dash/src/leds/strip.ts';
import { BASE_FACE } from '../../packages/dash/src/zones/index.ts';
import { downloads, flags, heroFace, readBuildManifest, releases, sitePackages, stripShapes, type Manifest } from '../scripts/content.ts';

const repoRoot = path.resolve(import.meta.dir, '..', '..');
const manifest: Manifest | null = readBuildManifest(repoRoot);

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

  test('name two conditions alike only where their motion tells them apart, the two blacks aside', () => {
    // The yellow being waved is named as the standing one is, #497, and the list tells them apart by
    // the blink rather than by a word the driver never sees.
    const yellows = list.filter((f) => f.id === 'yellowWaving' || f.id === 'yellow');
    expect(yellows.map(({ id, name, blinks }) => ({ id, name, blinks }))).toEqual([
      { id: 'yellowWaving', name: 'Yellow', blinks: true },
      { id: 'yellow', name: 'Yellow', blinks: false },
    ]);
    // The furled black is named as the black flag is, #497, and neither blinks, so the list carries
    // them as two rows alike, told apart by their rank; the box tells them apart by a bar that walks
    // against an outline that waves. That pair is the only one allowed to share a row.
    const blacks = list.filter((f) => f.id === 'furled' || f.id === 'black');
    expect(blacks.map(({ id, name, blinks }) => ({ id, name, blinks }))).toEqual([
      { id: 'furled', name: 'Black', blinks: false },
      { id: 'black', name: 'Black', blinks: false },
    ]);
    const others = list.filter((f) => f.id !== 'furled');
    expect(new Set(others.map((f) => `${f.name} ${f.blinks}`)).size).toBe(others.length);
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

  test('carries no file name, because a package is a screen the plugin installs and not a download', () => {
    const [p] = sitePackages({ packages: [{ folder: 'OpenDash 850x480', kind: 'dash', width: 850, height: 480, file: 'OpenDash 850x480.simhubdash' }] });
    expect(Object.keys(p!)).toEqual(['folder', 'kind', 'width', 'height', 'round']);
  });

  test.if(manifest !== null)('is the fourteen packages the plugin installs', () => {
    expect(sitePackages(manifest!)).toHaveLength(14);
  });
});

describe('the downloads', () => {
  test('are the plugin zip and nothing else the build wrote beside it', () => {
    const dir = mkdtempSync(path.join(tmpdir(), 'opendash-downloads-'));
    try {
      for (const f of ['OpenDash-plugin.zip', 'OpenDash 850x480.simhubdash', 'OpenDash slots 850x480.simhubdash', 'OpenDash 3-9-3.ledsprofile', 'manifest.json']) {
        writeFileSync(path.join(dir, f), 'x'.repeat(f.length));
      }
      expect(downloads(dir)).toEqual([{ file: 'OpenDash-plugin.zip', bytes: 'OpenDash-plugin.zip'.length }]);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  test('are none when nothing was built', () => {
    expect(downloads(path.join(tmpdir(), 'opendash-no-such-build'))).toEqual([]);
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
