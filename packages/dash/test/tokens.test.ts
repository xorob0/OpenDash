/** tokens.ts: alias resolution, every ds path exists, every colour is #RRGGBB, no brand colour. */
import { describe, expect, test } from 'bun:test';
import { ds, DS_TOKEN_PATHS, resolveToken, tokenExists, tokenNode } from '../src/tokens.ts';

const HEX6 = /^#[0-9A-F]{6}$/;
const BRAND = ['#33D9F2', '#5CE1F5', '#22909F'];

describe('tokens', () => {
  test('resolves aliases recursively down to the palette', () => {
    expect(resolveToken('purpose.delta.slower')).toBe('#FF2D46');
    expect(resolveToken('purpose.flag.onFlag')).toBe('#0A0B0D');
    expect(resolveToken('card.rung.L.value')).toBe(64);
    expect(resolveToken('space.3')).toBe(12);
    expect(resolveToken('control.paddingX')).toBe(16);
  });

  test('rejects unknown paths and groups', () => {
    expect(() => resolveToken('color.surface.nope')).toThrow(/nothing at/);
    expect(() => resolveToken('color.surface')).toThrow(/group/);
    expect(tokenExists('color.surface.base')).toBe(true);
    expect(tokenExists('color.surface.nope')).toBe(false);
    expect(tokenNode('font.family.data')).toMatchObject({ value: 'Barlow Condensed' });
  });

  test('every ds path exists in tokens.json', () => {
    expect(DS_TOKEN_PATHS.length).toBeGreaterThan(50);
    for (const path of DS_TOKEN_PATHS) expect({ path, exists: tokenExists(path) }).toEqual({ path, exists: true });
  });

  test('every colour in ds is #RRGGBB and none is the brand cyan', () => {
    const colours: string[] = [];
    const walk = (node: unknown): void => {
      if (typeof node === 'string') {
        if (node.startsWith('#')) colours.push(node);
      } else if (node && typeof node === 'object') Object.values(node).forEach(walk);
    };
    walk(ds);
    expect(colours.length).toBeGreaterThan(20);
    for (const c of colours) {
      expect(c).toMatch(HEX6);
      expect(BRAND).not.toContain(c);
    }
  });

  test('exposes what the spec names', () => {
    expect(ds.font.data).toBe('Barlow Condensed');
    expect(ds.font.label).toBe('Barlow');
    expect(ds.size).toEqual({ gear: 260, gearSm: 180, hero: 116, lapTime: 64, value: 46, valueSm: 34, label: 15, labelSm: 13 });
    expect(ds.space).toEqual({ 1: 4, 2: 8, 3: 12, 4: 16, 5: 24, 6: 32, 7: 48, 8: 64 });
    expect(ds.shiftLights).toEqual({ segments: 15, height: 40, flashHz: 8 });
    expect(ds.indicator.flagBand).toEqual({ height: 40, heightSm: 32, flashHz: 2 });
    expect(ds.indicator.flagRing).toEqual({ width: 12 });
    expect(ds.indicator.pitLimiter).toEqual({ height: 36, heightSm: 28 });
    expect(ds.card.rung.L).toEqual({ minSlotWidth: 250, value: 64, denominator: 46, grid: 46 });
    expect(ds.card.rung.M).toEqual({ minSlotWidth: 180, value: 46, denominator: 34, grid: 34 });
    expect(ds.card.rung.S).toEqual({ minSlotWidth: 0, value: 34, denominator: 34, grid: 34 });
    expect(ds.card.padding.L).toEqual([12, 16]);
    expect(ds.card.padding.S).toEqual([8, 12]);
    expect(ds.color.surface.base).toBe('#0A0B0D');
    expect(ds.purpose.shift.unlit).toBe('#33383F');
  });
});
