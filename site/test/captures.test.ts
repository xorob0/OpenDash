/**
 * Every picture the site promises exists, and the sidecar says where each one came from.
 *
 * A page names a capture by a rule (`<slug>.png` for a package, `page-<id>.png` for a page), and
 * `public/shots/captures.json` records the version, commit, date and scenario behind each file. A
 * capture that is missing is a page with a hole; one that predates the version being served is a
 * photograph of an older product presented as this one. The first fails here. The second warns,
 * because a release must not be blocked by a photograph, and fails only when
 * OPENDASH_SHOTS_STRICT=1 asks it to, which is the pre-release checklist's setting.
 *
 * The version is read off every entry. Until #627 the sidecar kept one for the whole folder and a
 * partial reshoot set it to the newest run's, so this check passed with most of the pictures old.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { MODULE_CATALOGUE } from '../../packages/dash/src/contract.ts';
import { pageFile, packageFile, readCaptures, staleCaptures, upgradeCaptures, type CapturesSidecar } from '../lib/captures.ts';
import { onSite, readBuildManifest, type Manifest } from '../scripts/content.ts';

const repoRoot = path.resolve(import.meta.dir, '..', '..');
const shots = path.resolve(import.meta.dir, '..', 'public', 'shots');
const sidecarPath = path.join(shots, 'captures.json');
const version = readFileSync(path.join(repoRoot, 'VERSION'), 'utf8').trim();
const manifest: Manifest | null = readBuildManifest(repoRoot);

const exists = (file: string): boolean => existsSync(path.join(shots, file));

describe('the captures sidecar', () => {
  test('exists and is well formed', () => {
    expect(existsSync(sidecarPath)).toBe(true);
    const sidecar: CapturesSidecar = readCaptures(sidecarPath);
    expect(sidecar.schema).toBe(2);
    expect(Object.keys(sidecar.files).length).toBeGreaterThan(0);
  });

  test('names only files that exist, and says of each one what it shows and which build took it', () => {
    const sidecar = readCaptures(sidecarPath);
    for (const [file, entry] of Object.entries(sidecar.files)) {
      expect({ file, exists: exists(file) }).toEqual({ file, exists: true });
      expect(['package', 'page', 'panel']).toContain(entry.kind);
      expect(entry.width).toBeGreaterThan(0);
      expect(entry.height).toBeGreaterThan(0);
      expect({ file, version: entry.version, commit: entry.commit, date: entry.date }).toEqual({
        file,
        version: expect.stringMatching(/^\d+\.\d+\.\d+/),
        commit: expect.stringMatching(/^[0-9a-f]{7,40}$/),
        date: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
      });
    }
  });

  test('was taken from the version being served, every picture of it, or says which were not', () => {
    const sidecar = readCaptures(sidecarPath);
    const stale = staleCaptures(sidecar, version);
    if (stale.length > 0) {
      const taken = [...new Set(stale.map((f) => `${sidecar.files[f]!.version} on ${sidecar.files[f]!.date}`))].join(', ');
      const message = `${stale.length} of ${Object.keys(sidecar.files).length} pictures in site/public/shots were captured from ${taken}; VERSION is ${version}${stale.length < Object.keys(sidecar.files).length ? `: ${stale.join(', ')}` : ''}. Reshoot with bun run shots and bun scripts/modules.ts, then site/scripts/sync-shots.ts.`;
      if (process.env.OPENDASH_SHOTS_STRICT === '1') throw new Error(message);
      console.warn(message);
    }
  });
});

describe('a stale capture', () => {
  const entry = (v: string) => ({ kind: 'page' as const, page: 'fuel', width: 850, height: 480, scenario: 'green', version: v, commit: 'abc1234', date: '2026-09-22', simHubVersion: '9.12.6' });

  test('is any picture not taken from the version being served, however new the others are', () => {
    const sidecar = { schema: 2 as const, files: { 'opendash.png': entry('0.3.0-rc.7'), 'page-fuel.png': entry('0.3.0-rc.6') } };
    expect(staleCaptures(sidecar, '0.3.0-rc.7')).toEqual(['page-fuel.png']);
    expect(staleCaptures(sidecar, '0.3.0-rc.6')).toEqual(['opendash.png']);
  });

  test('is read off every entry of a schema 1 sidecar too, which gave them all its one version', () => {
    const v1 = { schema: 1 as const, version: '0.3.0-rc.6', commit: '2b876e2', date: '2026-09-22', simHubVersion: '9.12.6', scenario: 'gallery', files: { 'page-fuel.png': { kind: 'page' as const, page: 'fuel', width: 850, height: 480, scenario: 'gallery' } } };
    expect(staleCaptures(upgradeCaptures(v1), '0.3.0-rc.7')).toEqual(['page-fuel.png']);
  });
});

describe('every page is photographed', () => {
  test.each(MODULE_CATALOGUE.map((m) => [m.id] as const))('page %s', (id) => {
    const file = pageFile(id);
    expect({ file, exists: exists(file) }).toEqual({ file, exists: true });
    expect(readCaptures(sidecarPath).files[file]?.kind).toBe('page');
  });
});

describe('every package is photographed', () => {
  const packages = manifest ? manifest.packages.filter(onSite) : [];

  test.if(manifest === null)('skipped: build/manifest.json is absent, run bun run build at the repository root', () => {
    expect(manifest).toBeNull();
  });

  test.each(packages.map((p) => [p.folder] as const))('%s', (folder) => {
    const file = packageFile(folder);
    expect({ file, exists: exists(file) }).toEqual({ file, exists: true });
    expect(readCaptures(sidecarPath).files[file]?.kind).toBe('package');
  });

  test('the hero, which two pages name by hand', () => {
    expect(exists(packageFile('OpenDash 850x480'))).toBe(true);
  });
});
