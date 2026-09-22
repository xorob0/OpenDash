/**
 * The gallery thumbnails: that a package finds its own, and that none is left behind.
 *
 * What cannot be asserted here is that a thumbnail exists at all, or that it shows the face as it
 * is drawn today. A preview is a photograph taken from SimHub on the Windows VM, so a test that
 * demanded one would refuse every pull request from a contributor who does not have the VM, which
 * ADR 0008 is the record of not doing. The build says so instead, once per package, as
 * `warning preview/missing`.
 *
 * The question that can be asked without a VM is the other one: a capture that belongs to no
 * package any more. A renamed size leaves its picture in the directory, where nothing looks for
 * it, and it is then committed and embedded in every release for ever. Deleting a file is
 * something anybody can do, so this one is a failure rather than a warning.
 */
import { describe, expect, test } from 'bun:test';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { composePackages } from '../src/build.ts';
import { orphanedPreviews, previewFileName, previewFor, PREVIEWS_DIR, readPreviewRun, unrecordedPreviews } from '../src/previews.ts';

const FOLDERS = composePackages({ version: '0.0.0-test', log: () => {} }, true).map((c) => c.pkg.folderName);

describe('previewFor', () => {
  test('finds the capture named for the package folder and nothing else', () => {
    const dir = mkdtempSync(path.join(tmpdir(), 'opendash-previews-'));
    try {
      writeFileSync(path.join(dir, 'OpenDash 850x480.png'), 'x');
      expect(previewFor('OpenDash 850x480', dir)).toBe(path.join(dir, 'OpenDash 850x480.png'));
      expect(previewFor('OpenDash 800x480', dir)).toBeUndefined();
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  test('a directory that does not exist yet is simply no previews, not a crash', () => {
    const gone = path.join(tmpdir(), 'opendash-previews-absent');
    rmSync(gone, { recursive: true, force: true });
    expect(previewFor('OpenDash', gone)).toBeUndefined();
    expect(orphanedPreviews(['OpenDash'], gone)).toEqual([]);
  });
});

describe('orphanedPreviews', () => {
  test('reports a capture no package claims, and ignores anything that is not one', () => {
    const dir = mkdtempSync(path.join(tmpdir(), 'opendash-previews-'));
    try {
      mkdirSync(path.join(dir, 'ignored'));
      writeFileSync(path.join(dir, 'OpenDash.png'), 'x');
      writeFileSync(path.join(dir, 'OpenDash 3 Fanalab.png'), 'x');
      writeFileSync(path.join(dir, 'README.md'), 'x');
      // The renamed shape is exactly the case: `OpenDash 3/9/3 Fanalab` became `... Fanatec` and
      // the old profile sat in build/ until a release carried it. A picture would do the same.
      expect(orphanedPreviews(['OpenDash'], dir)).toEqual(['OpenDash 3 Fanalab.png']);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  test('every committed capture belongs to a package this build composes', () => {
    expect(orphanedPreviews(FOLDERS)).toEqual([]);
  });
});

describe('run.json', () => {
  test('every committed picture says which build it is a picture of, and says nothing about one that is not there', () => {
    // The weakness of photographing a generated package is that the photograph does not age with
    // the source. It cannot be tested away, but a picture that arrived without a record -- copied
    // in by hand, or scaled by something other than `bun run previews` -- says nothing at all, and
    // that can be.
    expect(unrecordedPreviews(FOLDERS)).toEqual({ file: [], record: [] });
  });

  test('a record carries the version and the commit, not just a date', () => {
    for (const [folder, record] of Object.entries(readPreviewRun().previews)) {
      expect({ folder, version: record.version.length > 0, commit: record.commit.length > 0 }).toEqual({ folder, version: true, commit: true });
      expect(record.width).toBeGreaterThan(0);
      expect(record.height).toBeGreaterThan(0);
    }
  });
});

describe('the build', () => {
  test('gives each package the capture named for its folder', () => {
    // Not "every package has one": the VM is what takes them, and the build warns rather than
    // failing when one is missing. What is asserted is that the wiring is by folder name, which is
    // the name SimHub insists the file carry.
    for (const folder of FOLDERS) {
      const found = previewFor(folder);
      if (found === undefined) continue;
      expect(found).toBe(path.join(PREVIEWS_DIR, previewFileName(folder)));
    }
  });
});
