/**
 * What `bun run dev` decides before it touches the VM: how its arguments are read, and that the
 * package names it offers are the ones the build actually produces. The rest is a sequence of
 * remote steps and is proved by running it.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { BASE_FACE, LARGE_FACE } from '../packages/dash/src/zones/index.ts';
import { LIST_ORDER, packageSize, parseArgs } from './dev.ts';

const MANIFEST = path.resolve(import.meta.dir, '../build/manifest.json');

describe('reading the arguments', () => {
  test('nothing means the base face and a race', () => {
    expect(parseArgs([])).toMatchObject({ packageName: 'OpenDash 850x480', scenario: 'race', noBuild: false, keep: false });
  });

  test('a package name is positional', () => {
    expect(parseArgs(['OpenDash Pit wall'])).toMatchObject({ packageName: 'OpenDash Pit wall', scenario: 'race' });
  });

  test('a scenario is a flag, and does not become the package', () => {
    expect(parseArgs(['--scenario', 'notc'])).toMatchObject({ packageName: 'OpenDash 850x480', scenario: 'notc' });
  });

  test('the two together', () => {
    expect(parseArgs(['OpenDash 850x480', '--scenario', 'notc'])).toMatchObject({ packageName: 'OpenDash 850x480', scenario: 'notc' });
  });

  test('a scenario may be written with an equals sign', () => {
    expect(parseArgs(['--scenario=notc'])).toMatchObject({ scenario: 'notc' });
  });

  test('the switches are read', () => {
    expect(parseArgs(['--no-build', '--keep'])).toMatchObject({ noBuild: true, keep: true });
  });

  test('help wins over everything else', () => {
    expect(parseArgs(['OpenDash', '--help'])).toEqual({ help: true });
  });
});

describe('the base face and the large one', () => {
  // The default has to be a package Dash Studio lists, or `bun run dev` with no arguments filters
  // the list by a name nothing matches and reports that it could not open an installed dashboard.
  test('both are packages this script can open', () => {
    const listed: readonly string[] = LIST_ORDER;
    expect(listed).toContain(BASE_FACE.folder);
    expect(listed).toContain(LARGE_FACE.folder);
  });

  test('the default with no arguments is the base face', () => {
    expect(parseArgs([])).toMatchObject({ packageName: BASE_FACE.folder });
  });
});

describe('the size a dash window is given', () => {
  test('it comes from the manifest the build writes', () => {
    if (!existsSync(MANIFEST)) return; // `bun run build` has not run in this tree
    const manifest = JSON.parse(readFileSync(MANIFEST, 'utf8')) as { packages: { folder: string; width: number; height: number }[] };
    for (const pkg of manifest.packages) {
      expect({ folder: pkg.folder, size: packageSize(pkg.folder) }).toMatchObject({
        size: { width: pkg.width, height: pkg.height },
      });
    }
  });

  test('a package that does not exist has no size', () => {
    expect(packageSize('OpenDash 1x1')).toBeUndefined();
  });
});
