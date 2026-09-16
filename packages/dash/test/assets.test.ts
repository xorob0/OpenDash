/**
 * The registry and the rule that places what it holds.
 *
 * Two things can go wrong with artwork and neither shows up on a screenshot. A file that is not
 * there, or not the file the descriptor was measured from, makes SimHub draw nothing at all; and a
 * picture placed at a size rather than cut from its box is stretched, because SimHub fits an image
 * to the rect it is given and keeps no proportions of its own. The first is what the registry is
 * checked for here, the second is rule 18 and is what {@link assetBox} is checked for.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readFileSync, statSync } from 'node:fs';
import { assetBox, assetNamed, assetPath, ASSET_SOURCES, ASSETS, imageOf, WHEEL_CHANGE_TICK } from '../src/design/assets.ts';
import { bottom, rect, right } from '../src/design/geometry.ts';
import { md5Hex, pngSize } from '../src/generator.ts';

/**
 * What a package would carry. Small because a package is a zip SimHub extracts and reads at
 * startup, and because an asset is only ever drawn in a box a few tens of pixels across: the
 * resolution that stays crisp there is nowhere near the resolution a file this size would hold.
 */
const BUDGET_BYTES = 64 * 1024;

describe('the registry', () => {
  test('every asset is a file that is there, and a PNG', () => {
    expect(ASSETS.length).toBeGreaterThan(0);
    for (const asset of ASSETS) {
      const file = assetPath(asset);
      expect([asset.name, existsSync(file)]).toEqual([asset.name, true]);
      const bytes = new Uint8Array(readFileSync(file));
      expect(() => pngSize(bytes, asset.file)).not.toThrow();
      expect(file.endsWith('.png')).toBe(true);
    }
  });

  test('every asset names a source that has been declared', () => {
    for (const asset of ASSETS) expect(Object.keys(ASSET_SOURCES)).toContain(asset.source);
  });

  test('no two assets share a name, which would be one entry in the sidecar', () => {
    expect(new Set(ASSETS.map((a) => a.name)).size).toBe(ASSETS.length);
    for (const asset of ASSETS) expect(assetNamed(asset.name)).toBe(asset);
    expect(assetNamed('mdi-car-brake-abs')).toBeUndefined();
  });

  test('a description is measured off the bytes on disk', () => {
    const tick = imageOf(WHEEL_CHANGE_TICK);
    const bytes = new Uint8Array(readFileSync(assetPath(WHEEL_CHANGE_TICK)));
    expect(tick).toEqual({
      name: WHEEL_CHANGE_TICK.name,
      extension: '.png',
      path: assetPath(WHEEL_CHANGE_TICK),
      ...pngSize(bytes),
      length: bytes.length,
      md5: md5Hex(bytes),
    });
    // Measured once and handed back, so a second package drawing it does not reread the file.
    expect(imageOf(WHEEL_CHANGE_TICK)).toBe(tick);
  });

  test('every asset stays within what a package can afford to carry', () => {
    for (const asset of ASSETS) expect([asset.file, statSync(assetPath(asset)).size < BUDGET_BYTES]).toEqual([asset.file, true]);
  });
});

describe('a drawing is cut from its box', () => {
  const tick = imageOf(WHEEL_CHANGE_TICK);
  const wide = { width: 200, height: 100 };

  test('the box grows with the frame rather than staying the size it was', () => {
    const small = assetBox(rect(0, 0, 40, 40), tick);
    const large = assetBox(rect(0, 0, 80, 80), tick);
    expect(small.width).toBe(40);
    expect(large.width).toBe(80);
    expect(large.height / small.height).toBe(2);
  });

  test('the source keeps its proportions, whatever shape the frame is', () => {
    const square = assetBox(rect(0, 0, 300, 120), tick);
    expect(square.width).toBe(square.height);
    expect(square.height).toBe(120);
    const stretched = assetBox(rect(0, 0, 120, 300), wide);
    expect(stretched.width / stretched.height).toBeCloseTo(2, 10);
    expect(stretched.width).toBe(120);
  });

  test('a cap holds however tall the box is, which is how the car takes a third of its zone', () => {
    const frame = rect(0, 0, 600, 400);
    for (const height of [200, 400, 2000]) {
      const box = assetBox(rect(0, 0, frame.width, height), wide, { maxWidth: frame.width / 3 });
      expect(box.width).toBe(200);
      expect(box.height).toBe(100);
    }
  });

  test('what comes back is centred in the frame and inside it', () => {
    const frame = rect(30, 70, 300, 120);
    const box = assetBox(frame, tick);
    expect(box.left + box.width / 2).toBe(frame.left + frame.width / 2);
    expect(box.top + box.height / 2).toBe(frame.top + frame.height / 2);
    expect(box.left).toBeGreaterThanOrEqual(frame.left);
    expect(box.top).toBeGreaterThanOrEqual(frame.top);
    expect(right(box)).toBeLessThanOrEqual(right(frame));
    expect(bottom(box)).toBeLessThanOrEqual(bottom(frame));
  });

  test('a frame with no room draws nothing rather than drawing outside it', () => {
    const box = assetBox(rect(10, 10, 0, 40), tick);
    expect([box.width, box.height]).toEqual([0, 0]);
  });
});
