/**
 * Every picture the site promises exists.
 *
 * The site's pages are generated from the repository — the module catalogue decides how many cards
 * the Modules page draws, the build manifest decides how many faces the gallery shows — so adding a
 * module or a screen size adds a picture slot on its own, silently, and the only symptom is a
 * broken image on a page nobody reloaded. The captures come from a Windows VM and cannot be made
 * by CI, so the check that they were made has to live here.
 *
 * `bun scripts/modules.ts` and `bun run shots` are what produce them; `site/README.md` is the loop.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { MODULE_CATALOGUE } from '../../packages/dash/src/contract.ts';

const repoRoot = path.resolve(import.meta.dir, '..', '..');
const shots = path.join(repoRoot, 'site', 'public', 'shots');

/** `openDash 1280x480` -> `opendash-1280x480`, which is what sync-shots names a capture. */
const slug = (folder: string): string =>
  folder.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');

describe('module captures', () => {
  test.each(MODULE_CATALOGUE.map((m) => [m.id, m.name] as const))(
    'module %s (%s) has a capture',
    (id) => {
      expect(existsSync(path.join(shots, `module-${id}.png`))).toBe(true);
    },
  );

  test('the three that ship off are photographed too, because the picture is the claim', () => {
    for (const module of MODULE_CATALOGUE.filter((m) => !m.enabled)) {
      expect(existsSync(path.join(shots, `module-${module.id}.png`))).toBe(true);
    }
  });
});

describe('package captures', () => {
  // build/ is output, and the repository's own `bun test` runs before `bun run build` does. So this
  // asserts what it can when the manifest is there — which is the site's CI job, where the manifest
  // is downloaded from the dash job — and says plainly that it did not when it is not.
  const manifestPath = path.join(repoRoot, 'build', 'manifest.json');
  const built = existsSync(manifestPath);

  test.if(built)('every shipped package has a capture', async () => {
    const manifest = (await Bun.file(manifestPath).json()) as { packages: { folder: string }[] };
    // The superseded card faces are built for comparison on a rig and never shown, so they are
    // never photographed either; lib/packages.ts filters them out of every listing.
    const shipped = manifest.packages.filter((p) => !p.folder.startsWith('openDash slots '));
    expect(shipped.length).toBeGreaterThan(0);
    for (const pkg of shipped) {
      expect(existsSync(path.join(shots, `${slug(pkg.folder)}-green.png`))).toBe(true);
    }
  });

  test.if(!built)('skipped: build/manifest.json is not there, so there is nothing to check against', () => {
    expect(built).toBe(false);
  });
});

describe('the hero', () => {
  // Named in app/page.tsx and in the Anatomy component rather than derived from anything, so it is
  // the one capture a rename could orphan without any list noticing.
  test('the reference face capture the home page and the anatomy both point at exists', () => {
    expect(existsSync(path.join(shots, 'opendash-green.png'))).toBe(true);
  });
});
