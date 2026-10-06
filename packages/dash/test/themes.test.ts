/**
 * The theme layer: an overlay over design/tokens.json, how it is resolved, what it refuses, and the
 * two properties every theme PR is reviewed against. The default theme builds exactly what `main`
 * built, and a theme that moves one palette entry moves every reading aliasing it and nothing else.
 */
import { describe, expect, test } from 'bun:test';
import path from 'node:path';
import { applyOverlay, BASE_TREE, DS_TOKEN_PATHS, resolveTokenIn, THEME_ID, tokenNodeIn, type Overlay, type Tree } from '../src/tokens.ts';
import { DEFAULT_THEME_ID, selectedThemeId, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { LARGE_FACE } from '../src/zones/index.ts';
import { faceDjson } from './fixtures/faceDjson.ts';
import greenTheme from './fixtures/greenTheme.json';

const OLD_GREEN = '#00D96A';
const NEW_GREEN = '#12AB34';

/** Every token path in the colour layers, which are the only ones an overlay may write. */
const colourTokenPaths = (tree: Tree): string[] => {
  const out: string[] = [];
  const walk = (node: unknown, at: string): void => {
    if (node === null || typeof node !== 'object') {
      out.push(at);
      return;
    }
    if ('value' in node) out.push(at);
    for (const [key, child] of Object.entries(node)) if (key !== 'value') walk(child, `${at}.${key}`);
  };
  for (const layer of ['palette', 'color', 'purpose']) walk(tree[layer], layer);
  return out;
};

describe('an overlay', () => {
  test('wins over the base at the same path, and its aliases resolve against the merged tree', () => {
    const merged = applyOverlay(BASE_TREE, { 'palette.green.200': NEW_GREEN, 'purpose.delta.slower': '{color.good.primary}' });
    expect(resolveTokenIn(merged, 'palette.green.200')).toBe(NEW_GREEN);
    // Reached through the base's own alias, color.good.primary -> palette.green.200.
    expect(resolveTokenIn(merged, 'purpose.delta.faster')).toBe(NEW_GREEN);
    // Reached through the overlay's alias, which resolves to the overlay's green and not the base's.
    expect(resolveTokenIn(merged, 'purpose.delta.slower')).toBe(NEW_GREEN);
    expect(resolveTokenIn(BASE_TREE, 'purpose.delta.slower')).toBe('#FF2D46');
  });

  test('leaves the base it was applied to untouched', () => {
    applyOverlay(BASE_TREE, { 'palette.green.200': NEW_GREEN });
    expect(resolveTokenIn(BASE_TREE, 'palette.green.200')).toBe(OLD_GREEN);
  });

  test('refuses a token the base does not define, a group, a layer that is not a colour, and an alias to nothing', () => {
    expect(() => applyOverlay(BASE_TREE, { 'palette.green.250': NEW_GREEN })).toThrow(/does not define/);
    expect(() => applyOverlay(BASE_TREE, { 'palette.gren.200': NEW_GREEN })).toThrow(/does not define/);
    expect(() => applyOverlay(BASE_TREE, { 'color.good': NEW_GREEN })).toThrow(/group/);
    expect(() => applyOverlay(BASE_TREE, { 'space.3': 14 })).toThrow(/palette, color, purpose only/);
    expect(() => applyOverlay(BASE_TREE, { 'purpose.delta.slower': '{color.nope.primary}' })).toThrow(/nothing at color.nope.primary/);
    expect(() => applyOverlay(BASE_TREE, { 'palette.green.200': '{color.good.primary}' })).toThrow(/alias cycle/);
  });
});

describe('the theme a process builds', () => {
  test('is the default unless the environment names a registered one', () => {
    expect(THEME_ID).toBe(DEFAULT_THEME_ID);
    expect(selectedThemeId({})).toBe(DEFAULT_THEME_ID);
    expect(selectedThemeId({ [THEME_ENV]: '' })).toBe(DEFAULT_THEME_ID);
    expect(selectedThemeId({ [THEME_ENV]: 'default' })).toBe(DEFAULT_THEME_ID);
    expect(() => selectedThemeId({ [THEME_ENV]: 'porsche' })).toThrow(/is not a theme/);
    expect(() => selectedThemeId({ [THEME_ENV]: 'toString' })).toThrow(/is not a theme/);
  });

  test('the default overlay changes nothing', () => {
    expect(THEMES[DEFAULT_THEME_ID]!.overlay).toEqual({});
    expect(applyOverlay(BASE_TREE, THEMES[DEFAULT_THEME_ID]!.overlay)).toEqual(BASE_TREE);
  });

  /**
   * One digest per zone face `.djson`, so that a machinery change which was meant to leave the
   * default look alone and did not is a failing test rather than a review finding. A face change
   * moves these on purpose, and a theme PR should never move them at all.
   */
  test('the default theme builds the zone faces it built before themes existed', () => {
    const digests = Object.fromEntries(Object.entries(faceDjson()).map(([file, text]) => [file, new Bun.CryptoHasher('sha256').update(text).digest('hex')]));
    expect(Object.keys(digests).length).toBeGreaterThan(8);
    expect(digests).toMatchSnapshot();
  });
});

describe('a theme that redefines one palette entry', () => {
  const overlay: Overlay = greenTheme;
  const themed = applyOverlay(BASE_TREE, overlay);

  test('changes every reading that aliases it and nothing else', () => {
    const changed: string[] = [];
    for (const at of colourTokenPaths(BASE_TREE)) {
      const before = resolveTokenIn(BASE_TREE, at);
      const after = resolveTokenIn(themed, at);
      if (before !== after) {
        expect({ at, before, after }).toEqual({ at, before: OLD_GREEN, after: NEW_GREEN });
        changed.push(at);
      } else expect({ at, before }).not.toEqual({ at, before: OLD_GREEN });
    }
    expect(changed).toContain('color.good.primary');
    expect(changed).toContain('purpose.delta.faster');
    expect(changed).toContain('purpose.shift.stage1');
    expect(changed).not.toContain('color.good.secondary');
    expect(changed.length).toBeGreaterThan(5);
  });

  test('draws the large face with that colour swapped and every other byte where it was', () => {
    const run = Bun.spawnSync([process.execPath, path.join(import.meta.dir, 'fixtures/greenFace.ts')], { stderr: 'pipe' });
    expect({ exit: run.exitCode, stderr: run.stderr.toString() }).toEqual({ exit: 0, stderr: '' });
    const green = JSON.parse(run.stdout.toString()) as Record<string, string>;
    const base = faceDjson([LARGE_FACE]);
    expect(Object.keys(green)).toEqual(Object.keys(base));
    const swap = (text: string): string => text.replaceAll(OLD_GREEN.slice(1), NEW_GREEN.slice(1));
    let reached = 0;
    for (const [file, text] of Object.entries(base)) {
      expect(text).not.toContain(NEW_GREEN.slice(1));
      if (text.includes(OLD_GREEN.slice(1))) reached++;
      expect({ file, same: green[file] === swap(text) }).toEqual({ file, same: true });
    }
    expect(reached).toBeGreaterThan(0);
  });
});

describe('every registered theme', () => {
  for (const [id, theme] of Object.entries({ ...THEMES, green: { overlay: greenTheme as Overlay } })) {
    test(`${id}: no colour the face reads is the brand cyan`, () => {
      const tree = applyOverlay(BASE_TREE, theme.overlay);
      const brand = new Set([
        ...['100', '200', '300'].map((shade) => resolveTokenIn(BASE_TREE, `palette.cyan.${shade}`)),
        ...['primary', 'secondary', 'tint'].map((shade) => resolveTokenIn(tree, `color.brand.${shade}`)),
      ]);
      expect(tokenNodeIn(tree, 'color.brand.primary')).toBeDefined();
      for (const at of DS_TOKEN_PATHS) {
        const value = resolveTokenIn(tree, at);
        expect({ at, brand: brand.has(value) }).toEqual({ at, brand: false });
      }
    });
  }
});
