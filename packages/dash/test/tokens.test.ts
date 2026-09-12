/** tokens.ts: alias resolution, every ds path exists, every colour is #RRGGBB, no brand colour. */
import { describe, expect, test } from 'bun:test';
import { ds, DS_TOKEN_PATHS, resolveToken, tokenExists, tokenNode } from '../src/tokens.ts';

const HEX6 = /^#[0-9A-F]{6}$/;
const BRAND = ['#33D9F2', '#5CE1F5', '#22909F'];

/**
 * A semantic colour read straight from the token file. `ds` deliberately does not expose the
 * state colours -- the purpose layer is the interface, so that a card names what it means rather
 * than which shade it wants -- but a test comparing a purpose token against the meaning behind it
 * has to reach one.
 */
const hexToken = (path: string): `#${string}` => resolveToken(path) as `#${string}`;

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
    expect(tokenNode('font.family.data')).toMatchObject({ value: 'openDash Display' });
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
    // The data family is Barlow Condensed under a name with no width word in it, so that WPF files
    // it as its own family rather than as a stretch of Barlow. See design/fontFiles.ts.
    expect(ds.font.data).toBe('openDash Display');
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

describe('the groups the zone face draws', () => {
  test('a licence is an ordered weight ramp, never iRacing\'s five colours', () => {
    const ramp = [ds.purpose.licence.r, ds.purpose.licence.d, ds.purpose.licence.c, ds.purpose.licence.b, ds.purpose.licence.a, ds.purpose.licence.pro];
    // Ordered: the weight never goes down as the licence goes up. That is the whole idea -- R is
    // the lightest mark on the screen and Pro the heaviest -- and it is what makes the badge
    // readable without taking a colour a state already owns.
    for (let i = 1; i < ramp.length; i++) {
      expect({ step: i, weight: ramp[i]!.weight, previous: ramp[i - 1]!.weight, rising: ramp[i]!.weight >= ramp[i - 1]!.weight }).toMatchObject({ rising: true });
    }
    expect(ds.purpose.licence.pro.weight).toBe(700);
    // Pro inverts, exactly as the player's own class chip does.
    expect(ds.purpose.licence.pro.value).toBe(ds.color.surface.base);
    expect(ds.purpose.licence.pro.fill).toBe(ds.color.text.primary);
    // None of the six is a state colour, which is the reason the ramp exists.
    const states = [hexToken('color.good.primary'), hexToken('color.caution.primary'), hexToken('color.danger.primary'), hexToken('color.info.primary'), hexToken('color.best.primary')];
    for (const step of ramp) expect({ value: step.value, isState: states.includes(step.value as never) }).toMatchObject({ isState: false });
  });

  test('a rating is achromatic, and only the change it took is a state', () => {
    expect(ds.purpose.rating.value).toBe(ds.color.text.primary);
    expect(ds.purpose.rating.gain).toBe(hexToken('color.good.primary'));
    expect(ds.purpose.rating.loss).toBe(hexToken('color.danger.primary'));
  });

  test('a block says when a fill is allowed, and a well is recessed rather than raised', () => {
    expect(ds.purpose.block.fill).toBe(ds.color.surface.zone);
    expect(ds.purpose.block.well).toBe(ds.color.surface.inset);
    expect(ds.purpose.block.rule).toBe(ds.color.surface.raised);
    // The well is darker than the ground it sits in; that is what makes it read as recessed.
    expect(ds.purpose.block.well).not.toBe(ds.purpose.block.fill);
  });

  test('a drawing has an outline, an ink and a dim, and they differ', () => {
    const { outline, ink, dim } = ds.purpose.illustration;
    expect(new Set([outline, ink, dim]).size).toBe(3);
  });

  test('the telltale colours are the standard\'s, and an unlit lamp is dim rather than absent', () => {
    expect(ds.purpose.telltale.info).toBe(hexToken('color.info.primary'));
    expect(ds.purpose.telltale.good).toBe(hexToken('color.good.primary'));
    expect(ds.purpose.telltale.caution).toBe(hexToken('color.caution.primary'));
    expect(ds.purpose.telltale.danger).toBe(hexToken('color.danger.primary'));
    expect(ds.purpose.telltale.off).toBe(ds.color.text.dim);
  });

  test('the token file records which version of the design it is', () => {
    expect(tokenNode('$meta')).toMatchObject({ version: '0.9.2' });
  });
});
