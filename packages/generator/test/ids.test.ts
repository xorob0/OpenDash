/** stableGuid: deterministic, well formed, path sensitive. */

import { describe, expect, test } from 'bun:test';
import { createHash } from 'node:crypto';
import { GUID_PATTERN, dashboardPath, isGuid, itemPath, resolveItemId, screenPath, stableGuid, walkItems } from '../src/ids.ts';
import { ellipse, layer, rect } from './fixtures.ts';

describe('stableGuid', () => {
  test('is a lower-case 8-4-4-4-12 GUID', () => {
    expect(stableGuid('openDash/openDash')).toMatch(GUID_PATTERN);
    expect(isGuid(stableGuid('x'))).toBe(true);
  });

  test('is stable across calls and fixed for a known path', () => {
    expect(stableGuid('openDash/openDash')).toBe(stableGuid('openDash/openDash'));
    // Regression pin: SHA-1("openDash/openDash") with the version and variant bits set.
    expect(stableGuid('openDash/openDash')).toBe('ae8bdfad-7414-590c-a57a-83cebf3ae402');
  });

  test('carries version 5 and the RFC 4122 variant', () => {
    for (const path of ['a', 'openDash/cards/lastLap/value', 'ünïcödé/path']) {
      const guid = stableGuid(path);
      expect(guid.charAt(14)).toBe('5');
      expect(['8', '9', 'a', 'b']).toContain(guid.charAt(19));
    }
  });

  test('derives from the SHA-1 of the UTF-8 path', () => {
    const path = 'openDash/Main/Slot01';
    const sha = createHash('sha1').update(path, 'utf8').digest('hex');
    const guid = stableGuid(path).replace(/-/g, '');
    expect(guid.slice(0, 12)).toBe(sha.slice(0, 12));
    expect(guid.slice(20)).toBe(sha.slice(20, 32));
  });

  test('differs between paths, including case and separators', () => {
    const ids = new Set(['a/b', 'a/B', 'a//b', 'a/b/', 'ab', 'a-b'].map(stableGuid));
    expect(ids.size).toBe(6);
  });
});

describe('paths', () => {
  test('compose package, dashboard, screen and item names with slashes', () => {
    expect(dashboardPath('openDash', 'cards')).toBe('openDash/cards');
    expect(screenPath('openDash', 'cards', 'lastLap')).toBe('openDash/cards/lastLap');
    expect(itemPath('openDash/cards/lastLap', 'value')).toBe('openDash/cards/lastLap/value');
  });

  test('resolveItemId prefers an explicit id', () => {
    const explicit = '11111111-2222-3333-4444-555555555555';
    expect(resolveItemId(rect('r', { id: explicit }), 'p/r')).toBe(explicit);
    expect(resolveItemId(rect('r'), 'p/r')).toBe(stableGuid('p/r'));
  });
});

describe('walkItems', () => {
  test('visits depth first in document order with layer paths as parents', () => {
    const tree = [rect('a'), layer('L', [rect('b'), layer('M', [rect('c')])]), rect('d')];
    const seen: string[] = [];
    const depths: number[] = [];
    walkItems(tree, 'pkg/dash/screen', (v) => {
      seen.push(v.path);
      depths.push(v.depth);
      expect(v.id).toBe(stableGuid(v.path));
    });
    expect(seen).toEqual([
      'pkg/dash/screen/a',
      'pkg/dash/screen/L',
      'pkg/dash/screen/L/b',
      'pkg/dash/screen/L/M',
      'pkg/dash/screen/L/M/c',
      'pkg/dash/screen/d',
    ]);
    expect(depths).toEqual([0, 0, 1, 1, 2, 0]);
  });

  test('visits every kind, an ellipse included, and only recurses into layers', () => {
    const seen: string[] = [];
    walkItems([ellipse('ring'), layer('L', [ellipse('inner')])], 'p', (v) => seen.push(`${v.item.kind}:${v.path}`));
    expect(seen).toEqual(['ellipse:p/ring', 'layer:p/L', 'ellipse:p/L/inner']);
  });

  test('reports enclosing layers outermost first', () => {
    const inner = layer('M', [rect('c')]);
    const outer = layer('L', [inner]);
    walkItems([outer], 'p', (v) => {
      if (v.item.name === 'c') expect(v.parents.map((p) => p.name)).toEqual(['L', 'M']);
    });
  });
});
