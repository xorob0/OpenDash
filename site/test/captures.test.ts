/**
 * Every picture the site promises exists, and the sidecar says where each one came from.
 *
 * A page names a capture by a rule (`<slug>.png` for a package, `page-<id>.png` for a page), and
 * `public/shots/captures.json` records the version, commit, date and scenario behind each file. A
 * capture that is missing is a page with a hole; one that predates the version being served is a
 * photograph of an older product presented as this one. The first fails here. The second warns,
 * because a release must not be blocked by a photograph, and fails only when
 * OPENDASH_SHOTS_STRICT=1 asks it to, which is the pre-release checklist's setting.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { MODULE_CATALOGUE } from '../../packages/dash/src/contract.ts';
import { pageFile, packageFile, readCaptures, type CapturesSidecar } from '../lib/captures.ts';
import { SUPERSEDED, type Manifest } from '../scripts/content.ts';

const repoRoot = path.resolve(import.meta.dir, '..', '..');
const shots = path.resolve(import.meta.dir, '..', 'public', 'shots');
const sidecarPath = path.join(shots, 'captures.json');
const version = readFileSync(path.join(repoRoot, 'VERSION'), 'utf8').trim();
const manifestPath = path.join(repoRoot, 'build', 'manifest.json');
const manifest: Manifest | null = existsSync(manifestPath) ? (JSON.parse(readFileSync(manifestPath, 'utf8')) as Manifest) : null;

const exists = (file: string): boolean => existsSync(path.join(shots, file));

describe('the captures sidecar', () => {
  test('exists and is well formed', () => {
    expect(existsSync(sidecarPath)).toBe(true);
    const sidecar: CapturesSidecar = readCaptures(sidecarPath);
    expect(sidecar.schema).toBe(1);
    expect(sidecar.version).toMatch(/^\d+\.\d+\.\d+/);
    expect(sidecar.commit).toMatch(/^[0-9a-f]{7,40}$/);
    expect(sidecar.date).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(Object.keys(sidecar.files).length).toBeGreaterThan(0);
  });

  test('names only files that exist, and describes each one', () => {
    const sidecar = readCaptures(sidecarPath);
    for (const [file, entry] of Object.entries(sidecar.files)) {
      expect({ file, exists: exists(file) }).toEqual({ file, exists: true });
      expect(['package', 'page', 'panel']).toContain(entry.kind);
      expect(entry.width).toBeGreaterThan(0);
      expect(entry.height).toBeGreaterThan(0);
    }
  });

  test('was taken from the version being served, or says which one it was', () => {
    const sidecar = readCaptures(sidecarPath);
    if (sidecar.version !== version) {
      const message = `site/public/shots were captured from ${sidecar.version} on ${sidecar.date}; VERSION is ${version}. Reshoot with bun run shots and bun scripts/modules.ts, then site/scripts/sync-shots.ts.`;
      if (process.env.OPENDASH_SHOTS_STRICT === '1') throw new Error(message);
      console.warn(message);
    }
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
  const packages = manifest ? manifest.packages.filter((p) => !SUPERSEDED.test(p.folder)) : [];

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
