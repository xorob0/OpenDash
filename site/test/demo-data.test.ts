/**
 * The demo's data comes out of a built package whichever form the build left it in: the unpacked
 * folder on a developer's checkout, or the `.simhubdash` alone, which is all CI's Website job has
 * after it downloads the dash job's artifact. The two must agree file for file.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, mkdirSync, mkdtempSync, rmSync, symlinkSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { packageFiles } from '../scripts/demo-data.ts';
import { readBuildManifest } from '../scripts/content.ts';

const repoRoot = path.resolve(import.meta.dir, '..', '..');
const manifest = readBuildManifest(repoRoot);
const entry = manifest?.packages.find((p) => p.folder === 'OpenDash 850x480');
const unpacked = entry ? existsSync(path.join(repoRoot, 'build', entry.folder)) : false;

describe('a package read for the demo', () => {
  test.if(!entry || !unpacked)('skipped: build/OpenDash 850x480 is absent, run bun run build at the repository root', () => {
    expect(true).toBe(true);
  });

  test.if(!!entry && unpacked)('is the same whether it comes from the unpacked folder or from the zip alone', () => {
    const fromFolder = packageFiles(entry!);
    const zipsOnly = mkdtempSync(path.join(tmpdir(), 'opendash-demo-'));
    try {
      mkdirSync(zipsOnly, { recursive: true });
      symlinkSync(path.join(repoRoot, 'build', entry!.file), path.join(zipsOnly, entry!.file));
      const fromZip = packageFiles(entry!, zipsOnly);
      expect([...fromZip.keys()].sort()).toEqual([...fromFolder.keys()].sort());
      for (const [name, data] of fromFolder) expect({ name, same: fromZip.get(name)!.equals(data) }).toEqual({ name, same: true });
      expect([...fromFolder.keys()]).toContain('OpenDash 850x480.djson');
    } finally {
      rmSync(zipsOnly, { recursive: true, force: true });
    }
  });

  test('a package that was never built says so', () => {
    expect(() => packageFiles({ folder: 'OpenDash Nowhere', kind: 'dash', width: 1, height: 1, file: 'OpenDash Nowhere.simhubdash' }, path.join(tmpdir(), 'opendash-none'))).toThrow(/bun run build/);
  });
});
