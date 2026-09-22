/** writePackage and zipPackage: on-disk layout, sidecars, fonts, and a reproducible zip. */

import { afterAll, beforeAll, describe, expect, test } from 'bun:test';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { FONTS_DIR, PACKAGE_EXTENSION, ZIP_MTIME, listFiles, readZip, writePackage, zipEntries, zipPackage } from '../src/package.ts';
import { buildMetadataObject, serializeDashboard } from '../src/serialize.ts';
import { samplePackage } from './fixtures.ts';

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
