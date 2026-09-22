/** writePackage and zipPackage: on-disk layout, sidecars, fonts, and a reproducible zip. */

import { afterAll, beforeAll, describe, expect, test } from 'bun:test';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { FONTS_DIR, PACKAGE_EXTENSION, PREVIEW_EXTENSION, ZIP_MTIME, listFiles, previewMtime, readZip, writePackage, zipEntries, zipPackage } from '../src/package.ts';
import { buildMetadataObject, serializeDashboard } from '../src/serialize.ts';
import { pngBytes, samplePackage } from './fixtures.ts';

let root: string;
beforeAll(() => {
  root = mkdtempSync(join(tmpdir(), 'opendash-generator-'));
});
afterAll(() => {
  rmSync(root, { recursive: true, force: true });
});

describe('writePackage', () => {
  test('writes <folder>/<name>.djson, sidecars and _SHFonts', () => {
    const out = join(root, 'write');
    const pkg = samplePackage();
    const written = writePackage(pkg, out);
    expect(written.folder).toBe(join(out, 'OpenDash'));
    expect(listFiles(written.folder)).toEqual([
      'OpenDash.djson',
      'OpenDash.djson.metadata',
      `${FONTS_DIR}/Barlow-Medium.ttf`,
      `${FONTS_DIR}/BarlowCondensed-SemiBold.ttf`,
      'cards.djson',
      'cards.djson.metadata',
    ]);
    expect(written.files).toHaveLength(6);
    expect(written.files.every((f) => existsSync(f))).toBe(true);
  });

  test('file contents are the serialiser output and the sidecar equals the Metadata key', () => {
    const out = join(root, 'contents');
    const pkg = samplePackage();
    const { folder } = writePackage(pkg, out);
    const main = readFileSync(join(folder, 'OpenDash.djson'), 'utf8');
    expect(main).toBe(serializeDashboard(pkg.dashboards[0]!, { packageName: 'OpenDash' }));
    const parsed = JSON.parse(main) as { Metadata: unknown };
    const sidecar = JSON.parse(readFileSync(join(folder, 'OpenDash.djson.metadata'), 'utf8'));
    expect(sidecar).toEqual(parsed.Metadata);
    expect(sidecar).toEqual(buildMetadataObject(pkg.dashboards[0]!));
    expect(sidecar.Width).toBe(1920);
    expect(JSON.parse(readFileSync(join(folder, 'cards.djson.metadata'), 'utf8')).ScreenCount).toBe(2);
  });

  test('fonts are byte-identical copies', () => {
    const out = join(root, 'fonts');
    const pkg = samplePackage();
    const { folder } = writePackage(pkg, out);
    for (const font of pkg.fonts) {
      const copy = readFileSync(join(folder, FONTS_DIR, font.split('/').pop()!));
      expect(Buffer.compare(copy, readFileSync(font))).toBe(0);
    }
  });

  test('cleans stale files by default and can be told not to', () => {
    const out = join(root, 'clean');
    const first = writePackage(samplePackage(), out);
    const stale = join(first.folder, 'stale.djson');
    writeFileSync(stale, '{}');
    writePackage(samplePackage(), out, { clean: false });
    expect(existsSync(stale)).toBe(true);
    writePackage(samplePackage(), out);
    expect(existsSync(stale)).toBe(false);
  });

  test('refuses two fonts with the same file name', () => {
    const pkg = samplePackage();
    pkg.fonts.push(pkg.fonts[0]!);
    expect(() => writePackage(pkg, join(root, 'dupfont'))).toThrow(/would both be written/);
  });

  test('a package without fonts has no _SHFonts folder', () => {
    const pkg = samplePackage();
    pkg.fonts = [];
    const { folder } = writePackage(pkg, join(root, 'nofonts'));
    expect(existsSync(join(folder, FONTS_DIR))).toBe(false);
  });

  test('a preview is written as <folder>.djson.png, whatever the source file was called', () => {
    const out = join(root, 'preview');
    const source = join(root, 'a-photograph-of-a-dash.png');
    const bytes = pngBytes(531, 300);
    writeFileSync(source, bytes);
    const pkg = samplePackage();
    pkg.preview = source;
    const { folder, files } = writePackage(pkg, out);
    // SimHub looks for the thumbnail beside the .djson under the main dashboard's own name, and
    // EditorModel.CleanDir deletes one filed under any other; the source name is not part of it.
    expect(listFiles(folder)).toContain(`OpenDash${PREVIEW_EXTENSION}`);
    expect(files).toContain(join(folder, `OpenDash${PREVIEW_EXTENSION}`));
    expect(Buffer.compare(readFileSync(join(folder, `OpenDash${PREVIEW_EXTENSION}`)), Buffer.from(bytes))).toBe(0);
  });

  test('a preview that is not a PNG is refused rather than installed as a thumbnail that never draws', () => {
    const out = join(root, 'preview-bad');
    const lying = join(root, 'lying.png');
    writeFileSync(lying, 'this is not a picture');
    const pkg = samplePackage();
    pkg.preview = lying;
    expect(() => writePackage(pkg, out)).toThrow(/is not a PNG/);
  });
});

describe('zipPackage', () => {
  test('produces <folder>.simhubdash whose entries are prefixed with the folder and sorted', () => {
    const out = join(root, 'zip');
    writePackage(samplePackage(), out);
    const zipped = zipPackage(out, 'OpenDash');
    expect(zipped.path).toBe(join(out, `OpenDash${PACKAGE_EXTENSION}`));
    expect(existsSync(zipped.path)).toBe(true);
    expect(zipped.entries).toEqual([
      'OpenDash/OpenDash.djson',
      'OpenDash/OpenDash.djson.metadata',
      'OpenDash/_SHFonts/Barlow-Medium.ttf',
      'OpenDash/_SHFonts/BarlowCondensed-SemiBold.ttf',
      'OpenDash/cards.djson',
      'OpenDash/cards.djson.metadata',
    ]);
    expect(zipped.entries).toContain('OpenDash/OpenDash.djson');
    expect(Buffer.compare(readFileSync(zipped.path), zipped.bytes)).toBe(0);
  });

  test('the archive round-trips and contains the written files', () => {
    const out = join(root, 'roundtrip');
    const { folder } = writePackage(samplePackage(), out);
    const zipped = zipPackage(out, 'OpenDash');
    const unzipped = readZip(zipped.bytes);
    expect(Object.keys(unzipped).sort()).toEqual([...zipped.entries].sort());
    expect(Object.keys(unzipped).every((e) => e.startsWith('OpenDash/'))).toBe(true);
    expect(new TextDecoder().decode(unzipped['OpenDash/OpenDash.djson'])).toBe(readFileSync(join(folder, 'OpenDash.djson'), 'utf8'));
    expect(JSON.parse(new TextDecoder().decode(unzipped['OpenDash/OpenDash.djson.metadata'])).DashboardVersion).toBe('0.1.0');
    expect(Buffer.compare(Buffer.from(unzipped['OpenDash/_SHFonts/Barlow-Medium.ttf']!), readFileSync(join(folder, FONTS_DIR, 'Barlow-Medium.ttf')))).toBe(0);
  });

  test('is reproducible: two builds in different directories are byte-identical', () => {
    const a = join(root, 'repro-a');
    const b = join(root, 'repro-b');
    writePackage(samplePackage(), a);
    writePackage(samplePackage(), b);
    const za = zipPackage(a, 'OpenDash');
    const zb = zipPackage(b, 'OpenDash');
    expect(Buffer.compare(za.bytes, zb.bytes)).toBe(0);
  });

  test('every entry carries the fixed timestamp', () => {
    const out = join(root, 'mtime');
    writePackage(samplePackage(), out);
    const { bytes } = zipPackage(out, 'OpenDash');
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    // Expected DOS fields for ZIP_MTIME: time 00:00:00, date 2000-01-01.
    const dosTime = (ZIP_MTIME.getHours() << 11) | (ZIP_MTIME.getMinutes() << 5) | (ZIP_MTIME.getSeconds() >> 1);
    const dosDate = ((ZIP_MTIME.getFullYear() - 1980) << 9) | ((ZIP_MTIME.getMonth() + 1) << 5) | ZIP_MTIME.getDate();
    expect(dosTime).toBe(0);
    expect(dosDate).toBe(0x2821);
    let headers = 0;
    for (let i = 0; i + 30 <= bytes.byteLength; i++) {
      if (view.getUint32(i, true) !== 0x04034b50) continue;
      headers++;
      expect(view.getUint16(i + 10, true)).toBe(dosTime);
      expect(view.getUint16(i + 12, true)).toBe(dosDate);
      const nameLength = view.getUint16(i + 26, true);
      i += 29 + nameLength;
    }
    expect(headers).toBe(6);
  });

  test('the thumbnail is stamped from its own bytes, so an update does not hit SimHub\'s cache', () => {
    // SimHub caches a decoded thumbnail under an MD5 of the file's path, its last write time and
    // 200. Extraction stamps the file with the zip entry's time, so a fixed one would give every
    // release the same key at the same path and a redrawn face would keep showing the old picture.
    const dosDate = (d: Date): number => ((d.getFullYear() - 1980) << 9) | ((d.getMonth() + 1) << 5) | d.getDate();
    const dosTime = (d: Date): number => (d.getHours() << 11) | (d.getMinutes() << 5) | (d.getSeconds() >> 1);

    const stampOf = (entries: Record<string, [Uint8Array, { mtime?: string | number | Date }]>, entry: string): Date => {
      const mtime = entries[entry]?.[1].mtime;
      if (!(mtime instanceof Date)) throw new Error(`${entry} carries no Date mtime`);
      return mtime;
    };

    const withPreview = (dir: string, bytes: Uint8Array): Record<string, [Uint8Array, { mtime?: string | number | Date }]> => {
      const pkg = samplePackage();
      pkg.preview = join(root, dir, 'source.png');
      mkdirSync(join(root, dir), { recursive: true });
      writeFileSync(pkg.preview, bytes);
      writePackage(pkg, join(root, dir, 'out'));
      return zipEntries(join(root, dir, 'out', 'OpenDash'), 'OpenDash');
    };

    const first = pngBytes(20, 12, 0x30);
    const second = pngBytes(20, 12, 0x31);
    const entry = `OpenDash/OpenDash${PREVIEW_EXTENSION}`;
    const a = withPreview('stamp-a', first);
    const b = withPreview('stamp-b', first);
    const c = withPreview('stamp-c', second);

    // Same picture, same stamp, wherever and whenever it was built: the archive stays reproducible.
    expect(stampOf(a, entry)).toEqual(stampOf(b, entry));
    expect(stampOf(a, entry)).toEqual(previewMtime(first));
    // A different picture, a different stamp, which is the whole point. Compared as zip stores it,
    // since a difference finer than two seconds would not survive the archive.
    const moved = dosDate(stampOf(c, entry)) !== dosDate(stampOf(a, entry)) || dosTime(stampOf(c, entry)) !== dosTime(stampOf(a, entry));
    expect(moved).toBe(true);
    // And nothing else moved.
    expect(stampOf(a, 'OpenDash/OpenDash.djson')).toBe(ZIP_MTIME);
    expect(stampOf(c, 'OpenDash/OpenDash.djson')).toBe(ZIP_MTIME);
  });

  test('previewMtime lands on a date and a second that zip can store', () => {
    for (let i = 0; i < 64; i++) {
      const when = previewMtime(pngBytes(4, 4, i));
      expect(when.getFullYear()).toBe(ZIP_MTIME.getFullYear());
      expect(when.getDate()).toBeGreaterThanOrEqual(1);
      expect(when.getDate()).toBeLessThanOrEqual(28);
      // Zip keeps seconds in two-second steps, so an odd one would not survive the round trip.
      expect(when.getSeconds() % 2).toBe(0);
    }
  });

  test('a custom mtime and level are honoured', () => {
    const out = join(root, 'custom');
    writePackage(samplePackage(), out);
    const dflt = zipPackage(out, 'OpenDash');
    const other = zipPackage(out, 'OpenDash', { mtime: new Date(2010, 5, 15, 12, 0, 0), level: 0 });
    expect(Buffer.compare(dflt.bytes, other.bytes)).not.toBe(0);
    expect(Object.keys(readZip(other.bytes))).toEqual(dflt.entries);
  });

  test('refuses a folder that is missing or has no <folder>.djson', () => {
    expect(() => zipPackage(join(root, 'nope'), 'OpenDash')).toThrow(/not a directory/);
    const out = join(root, 'nomain');
    const pkg = samplePackage();
    pkg.dashboards.shift();
    writePackage(pkg, out);
    expect(() => zipPackage(out, 'OpenDash')).toThrow(/no OpenDash\.djson/);
  });

  test('zipEntries uses the folder name given, not the directory name', () => {
    const out = join(root, 'entries');
    const { folder } = writePackage(samplePackage(), out);
    expect(Object.keys(zipEntries(folder, 'Renamed'))).toEqual([
      'Renamed/OpenDash.djson',
      'Renamed/OpenDash.djson.metadata',
      'Renamed/_SHFonts/Barlow-Medium.ttf',
      'Renamed/_SHFonts/BarlowCondensed-SemiBold.ttf',
      'Renamed/cards.djson',
      'Renamed/cards.djson.metadata',
    ]);
  });
});
