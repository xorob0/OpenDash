/**
 * Images: describing a file, the descriptor the `.djson` carries, and the `.ressources` sidecar.
 *
 * The PNGs here are built byte by byte rather than vendored, so that the expected width, height,
 * length and MD5 are properties of bytes this file can see rather than of a binary nobody reads.
 */

import { afterAll, beforeAll, describe, expect, test } from 'bun:test';
import { createHash } from 'node:crypto';
import { deflateSync } from 'node:zlib';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { unzipSync } from 'fflate';
import { describeImage, md5Hex, pngSize } from '../src/images.ts';
import type { Dashboard, ImageItem } from '../src/model.ts';
import { buildImageDescriptor, buildItemObject, buildDashboardObject, ITEM_TYPES } from '../src/serialize.ts';
import { resourcesZip, writePackage, listFiles } from '../src/package.ts';
import { validatePackage } from '../src/validate.ts';
import { DECLARED, samplePackage } from './fixtures.ts';

const OPTS = { declaredProperties: DECLARED, propertyPrefix: 'OpenDash' };

/** A valid one-colour PNG of the given size, so a test can assert dimensions it chose. */
const png = (width: number, height: number, byte = 0x7f): Uint8Array => {
  const chunk = (type: string, body: Uint8Array): number[] => {
    const head = [...Buffer.from(type, 'ascii'), ...body];
    const crc = crc32(Uint8Array.from(head));
    return [...be32(body.length), ...head, ...be32(crc)];
  };
  const be32 = (n: number): number[] => [(n >>> 24) & 255, (n >>> 16) & 255, (n >>> 8) & 255, n & 255];
  const ihdr = Uint8Array.from([...be32(width), ...be32(height), 8, 2, 0, 0, 0]);
  // One filter byte then three bytes a pixel, which is what colour type 2 at depth 8 means.
  const raw = Buffer.concat(Array.from({ length: height }, () => Buffer.from([0, ...Array(width * 3).fill(byte)])));
  return Uint8Array.from([
    0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
    ...chunk('IHDR', ihdr),
    ...chunk('IDAT', new Uint8Array(deflateSync(raw))),
    ...chunk('IEND', new Uint8Array(0)),
  ]);
};

const crc32 = (bytes: Uint8Array): number => {
  let crc = 0xffffffff;
  for (const byte of bytes) {
    crc ^= byte;
    for (let i = 0; i < 8; i++) crc = crc & 1 ? (crc >>> 1) ^ 0xedb88320 : crc >>> 1;
  }
  return (crc ^ 0xffffffff) >>> 0;
};

let root: string;
let iconPath: string;
let iconBytes: Uint8Array;
beforeAll(() => {
  root = mkdtempSync(join(tmpdir(), 'opendash-images-'));
  iconBytes = png(24, 18);
  iconPath = join(root, 'brake-warning.png');
  writeFileSync(iconPath, iconBytes);
});
afterAll(() => {
  rmSync(root, { recursive: true, force: true });
});

describe('describeImage', () => {
  test('reads the pixels, the byte count and the MD5 off the file', () => {
    const asset = describeImage(iconPath);
    expect(asset).toEqual({
      name: 'brake-warning',
      extension: '.png',
      path: iconPath,
      width: 24,
      height: 18,
      length: iconBytes.length,
      md5: createHash('md5').update(iconBytes).digest('hex'),
    });
    expect(asset.md5).toMatch(/^[0-9a-f]{32}$/);
  });

  test('the name may differ from the file name, because the item references the name', () => {
    expect(describeImage(iconPath, 'telltale.brake').name).toBe('telltale.brake');
  });

  test('width and height are the image, not the box it is drawn in', () => {
    expect(pngSize(png(1, 1))).toEqual({ width: 1, height: 1 });
    expect(pngSize(png(1024, 7))).toEqual({ width: 1024, height: 7 });
  });

  test('anything that is not a PNG is refused rather than described wrongly', () => {
    const jpeg = join(root, 'flag.jpg');
    writeFileSync(jpeg, iconBytes);
    expect(() => describeImage(jpeg)).toThrow(/only PNG images are packed/);
    const lying = join(root, 'lying.png');
    writeFileSync(lying, Uint8Array.from([1, 2, 3, 4, 5, 6, 7, 8, 9]));
    expect(() => describeImage(lying)).toThrow(/is not a PNG/);
  });

  test('md5Hex is of the bytes themselves', () => {
    expect(md5Hex(Uint8Array.from([]))).toBe('d41d8cd98f00b204e9800998ecf8427e');
  });
});

describe('the descriptor in the .djson', () => {
  test('carries the eight fields SimHub writes, in its order', () => {
    const asset = describeImage(iconPath);
    expect(Object.keys(buildImageDescriptor(asset))).toEqual(['Name', 'Extension', 'Modified', 'Optimized', 'Width', 'Height', 'Length', 'MD5']);
    expect(buildImageDescriptor(asset)).toMatchObject({ Modified: false, Optimized: false, Width: 24, Height: 18 });
  });

  test('a dashboard with no images still writes an empty Images list', () => {
    const pkg = samplePackage();
    expect(buildDashboardObject(pkg.dashboards[0]!, { packageName: pkg.folderName }).Images).toEqual([]);
  });

  test('a dashboard with images lists every one of them', () => {
    const pkg = samplePackage();
    const dashboard: Dashboard = { ...pkg.dashboards[0]!, images: [describeImage(iconPath, 'a'), describeImage(iconPath, 'b')] };
    const images = buildDashboardObject(dashboard, { packageName: pkg.folderName }).Images as { Name: string }[];
    expect(images.map((i) => i.Name)).toEqual(['a', 'b']);
  });
});

describe('the image item', () => {
  const item: ImageItem = { kind: 'image', name: 'Telltale', image: 'brake-warning', rect: { left: 10, top: 20, width: 48, height: 36 } };

  test('is sized from its rect, never from the image', () => {
    const o = buildItemObject(item, 'OpenDash/OpenDash.djson/Race');
    expect(o).toMatchObject({
      $type: ITEM_TYPES.image,
      Image: 'brake-warning',
      AutoSize: false,
      Left: 10,
      Top: 20,
      Width: 48,
      Height: 36,
    });
  });

  test('referencing an image the dashboard does not declare is an error, not a blank box', () => {
    const pkg = samplePackage();
    const main = pkg.dashboards[0]!;
    main.screens[0]!.items.push(item);
    const missing = validatePackage(pkg, OPTS).errors.filter((i) => i.code === 'image/missing');
    expect(missing).toHaveLength(1);
    expect(missing[0]!.message).toContain('brake-warning');

    main.images = [describeImage(iconPath)];
    expect(validatePackage(pkg, OPTS).errors.filter((i) => i.code === 'image/missing')).toHaveLength(0);
  });

  test('two images of one name would collapse into one entry, so it is refused', () => {
    const pkg = samplePackage();
    pkg.dashboards[0]!.images = [describeImage(iconPath, 'same'), describeImage(iconPath, 'same')];
    expect(validatePackage(pkg, OPTS).errors.some((i) => i.code === 'name/duplicate' && i.message.includes('same'))).toBe(true);
  });
});

describe('the .ressources sidecar', () => {
  test('holds one entry per image at the root, named <Name><Extension>', () => {
    const pkg = samplePackage();
    const dashboard: Dashboard = { ...pkg.dashboards[0]!, images: [describeImage(iconPath, 'zebra'), describeImage(iconPath, 'alpha')] };
    const entries = unzipSync(resourcesZip(dashboard));
    expect(Object.keys(entries).sort()).toEqual(['alpha.png', 'zebra.png']);
    expect([...entries['alpha.png']!]).toEqual([...iconBytes]);
  });

  test('is written beside the .djson and lands inside the package', () => {
    const pkg = samplePackage();
    pkg.dashboards[0]!.images = [describeImage(iconPath)];
    const written = writePackage(pkg, join(root, 'packed'));
    expect(listFiles(written.folder)).toContain('OpenDash.djson.ressources');
    // The dashboard with no images gets no sidecar: an empty zip beside every .djson would be noise.
    expect(listFiles(written.folder)).not.toContain('cards.djson.ressources');
  });

  test('bytes that no longer match the descriptor stop the build', () => {
    const changed = join(root, 'changed.png');
    writeFileSync(changed, png(8, 8));
    const asset = describeImage(changed);
    writeFileSync(changed, png(8, 8, 0x01));
    const pkg = samplePackage();
    const dashboard: Dashboard = { ...pkg.dashboards[0]!, images: [asset] };
    expect(() => resourcesZip(dashboard)).toThrow(/changed after it was described/);
  });
});
