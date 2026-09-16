/**
 * The phase of the chequered flag, on the band and on the ring.
 *
 * Every artboard fills the flag band with `repeating-conic-gradient(#F5F7FA 0 25%, #0A0B0D 0 50%)`
 * over a tile the height of the band: 60 px at 1280x480, 1280x720 and 850x480, 54 at 1280x400, 58
 * on the nano and 56 at 600x686. That gradient sweeps clockwise from twelve o'clock, so the light
 * quadrant of every tile is its top right and its top left is the ground, which is to say the band
 * opens dark at its left edge and the first light square begins one square in. A board drawn in
 * the other phase carries the right colours and the right cell while being the artboard's inverse
 * at every square, so the colours alone are not what wants pinning here.
 *
 * The round faces draw the same board as marks around the rim rather than as squares, and the two
 * have to agree: whatever the band does at its left edge, the ring does at twelve o'clock.
 */
import { describe, expect, test } from 'bun:test';
import { CHEQUER_COUNT, CHEQUER_STEP } from '../src/components/flagRing.ts';
import { contains, rect } from '../src/design/geometry.ts';
import type { Item, LayerItem, RectangleItem } from '../src/generator.ts';
import { hero } from '../src/hero/hero.ts';
import { layout480round, layout800round, type Layout } from '../src/layouts/index.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, faceItems, type ZoneLayout } from '../src/zones/index.ts';

/** The two colours every artboard quotes for the chequer. */
const CHEQUER = '#F5F7FA';
const GROUND = '#0A0B0D';

/** The tile each artboard quotes, which is the band's height. */
const TILE: Record<string, number> = {
  openDash: 60,
  'openDash 1280x480': 60,
  'openDash 1280x400': 54,
  'openDash 850x480': 60,
  'openDash 800x480': 60,
  'openDash 1280x720': 60,
  'openDash 800x286': 58,
  'openDash 600x686': 56,
};

const chequerChildren = (items: readonly Item[]): RectangleItem[] => {
  const layer = items.find((i): i is LayerItem => i.kind === 'layer' && i.name === 'flag.chequered');
  if (!layer) throw new Error('no chequered flag');
  return layer.children.map((c) => {
    if (c.kind !== 'rect') throw new Error(`${c.name} is not a rect`);
    return c;
  });
};

/** How far into a tile the first light square sits, as a fraction of the tile. */
const bandPhase = (face: ZoneLayout): number => {
  const { band } = face.zones;
  const top = chequerChildren(faceItems(face)).slice(1).filter((c) => c.rect.top === band.top);
  return (Math.min(...top.map((c) => c.rect.left)) - band.left) / band.height;
};

/** The same fraction on a rim, where a tile is one check and the gap after it. */
const ringPhase = (layout: Layout): number => (chequerChildren(hero(layout.hero))[0]!.rotation ?? 0) / CHEQUER_STEP;

describe('the chequered band', () => {
  for (const face of ZONE_FACES) {
    describe(face.folder, () => {
      const { band } = face.zones;
      const check = band.height / 2;
      const children = chequerChildren(faceItems(face));
      const ground = children[0]!;
      const squares = children.slice(1);

      test('lays the ground over the whole band and tiles it with light squares half the band high', () => {
        expect(ds.color.surface.base).toBe(GROUND);
        expect(ds.purpose.flag.chequer).toBe(CHEQUER);
        expect(TILE[face.folder]).toBe(band.height);
        expect(ground.name).toBe('flag.chequered.band');
        expect(ground.rect).toEqual(band);
        expect(ground.backgroundColor).toBe(GROUND);
        expect(squares.length).toBe(Math.ceil(band.width / check));
        for (const square of squares) {
          expect({ name: square.name, color: square.backgroundColor }).toEqual({ name: square.name, color: CHEQUER });
          expect(square.rect.height).toBe(check);
          expect(square.rect.width).toBeLessThanOrEqual(check);
          expect({ name: square.name, inside: contains(band, square.rect) }).toEqual({ name: square.name, inside: true });
        }
      });

      test('opens on the ground: the top row starts one square in, and the bottom row is the one that starts at the left edge', () => {
        const columnOf = (square: RectangleItem): number => (square.rect.left - band.left) / check;
        const top = squares.filter((s) => s.rect.top === band.top);
        const bottom = squares.filter((s) => s.rect.top === band.top + check);
        expect(top.map(columnOf)).not.toContain(0);
        expect(top.every((s) => columnOf(s) % 2 === 1)).toBe(true);
        expect(bottom.every((s) => columnOf(s) % 2 === 0)).toBe(true);
        expect(top.find((s) => s.rect.left === band.left + check)?.rect).toEqual(rect(band.left + check, band.top, check, check));
        expect(bottom.find((s) => s.rect.left === band.left)?.rect).toEqual(rect(band.left, band.top + check, check, check));
        expect(squares.filter((s) => s.rect.top !== band.top && s.rect.top !== band.top + check)).toEqual([]);
      });

      test('carries no label', () => {
        expect([...walkItems(children)].filter((i) => i.kind === 'text')).toEqual([]);
      });
    });
  }
});

describe('the chequered ring', () => {
  for (const layout of [layout480round, layout800round]) {
    test(`${layout.folder} draws ${CHEQUER_COUNT} checks in the band's phase, none of them at twelve o'clock`, () => {
      const checks = chequerChildren(hero(layout.hero));
      expect(checks).toHaveLength(CHEQUER_COUNT);
      checks.forEach((check, k) => {
        expect({ name: check.name, color: check.backgroundColor }).toEqual({ name: check.name, color: CHEQUER });
        expect(check.rotation).toBe((k + 0.5) * CHEQUER_STEP);
      });
      expect(checks.map((c) => (c.rotation ?? 0) % 360)).not.toContain(0);
    });
  }

  test('the ring and the band open on the ground by the same fraction of a tile', () => {
    for (const face of ZONE_FACES) expect({ face: face.folder, phase: bandPhase(face) }).toEqual({ face: face.folder, phase: 0.5 });
    for (const layout of [layout480round, layout800round]) expect({ face: layout.folder, phase: ringPhase(layout) }).toEqual({ face: layout.folder, phase: 0.5 });
  });
});
