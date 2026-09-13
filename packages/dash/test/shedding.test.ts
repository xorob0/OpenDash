/**
 * The shedding order: that every page declares one, that what it declares is what it draws, and
 * that the table still says what the catalogue's eighty-four drawings say.
 *
 * The table is data rather than mechanism, so the checks are about drift. A field renamed in a
 * module and not in the table is a page that silently keeps everything; a shape that names a field
 * the page does not have is a line nobody will notice is dead.
 */
import { describe, expect, test } from 'bun:test';
import { MODULE_CATALOGUE } from '../src/contract.ts';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { LEADERBOARD_COLUMNS } from '../src/modules/leaderboard.ts';
import { RELATIVE_COLUMNS } from '../src/modules/relative.ts';
import { SHEDDING, archetypeOf, keepsAt, sheddingFor, type Archetype } from '../src/modules/shedding.ts';
import { ARCHETYPES, SHAPE_ARCHETYPES, shapeOf } from '../src/second/shape.ts';
import { walkItems } from '../src/walk.ts';

/** Every item name a page draws at a shape, which is how a declared id is checked against reality. */
const namesAt = (id: string, archetype: Archetype): string[] => {
  const size = SHAPE_ARCHETYPES[archetype];
  const module = MODULES.find((m) => m.id === id)!;
  const items = module.build({ frame: rect(0, 0, size.width, size.height), density: 'zone', prefix: '' });
  return items.flatMap((item) => [...walkItems([item])]).map((item) => item.name);
};

/** The ids a page actually drew: an item is `<id>.label`, `<id>.value`, `<id>.text` and so on. */
const drewId = (names: readonly string[], id: string): boolean => names.some((name) => name === id || name.startsWith(`${id}.`));

describe('every page declares its shedding order', () => {
  test('one entry per page of the catalogue, and no more', () => {
    expect(Object.keys(SHEDDING).sort()).toEqual(MODULE_CATALOGUE.map((m) => m.id).sort());
  });

  test('a page with nothing to shed says why rather than declaring an empty table', () => {
    for (const [id, entry] of Object.entries(SHEDDING)) {
      if (entry.kind !== 'nothing') continue;
      expect({ id, why: entry.why.length > 10 }).toMatchObject({ why: true });
    }
  });

  test('the wide row is the fullest form: no shape names a field it does not', () => {
    for (const [id, entry] of Object.entries(SHEDDING)) {
      if (entry.kind === 'nothing') continue;
      for (const shape of ARCHETYPES) {
        const extra = entry.keeps[shape].filter((field) => !entry.keeps.wide.includes(field));
        expect({ id, shape, extra }).toEqual({ id, shape, extra: [] });
      }
    }
  });

  test('and what it names is what the page draws', () => {
    for (const [id, entry] of Object.entries(SHEDDING)) {
      if (entry.kind === 'nothing') continue;
      if (entry.kind === 'columns') {
        const all = id === 'relative' ? RELATIVE_COLUMNS : LEADERBOARD_COLUMNS;
        expect({ id, unknown: entry.keeps.wide.filter((c) => !all.includes(c as (typeof all)[number])) }).toEqual({ id, unknown: [] });
        continue;
      }
      const names = namesAt(id, 'wide');
      expect({ id, missing: entry.keeps.wide.filter((field) => !drewId(names, field)) }).toEqual({ id, missing: [] });
    }
  });
});

describe('a shape takes the answer of one of the four the catalogue draws', () => {
  test('the four archetypes are themselves', () => {
    for (const name of ARCHETYPES) expect({ name, of: archetypeOf(shapeOf(SHAPE_ARCHETYPES[name])) }).toEqual({ name, of: name });
  });

  test('a box the canvas does not draw takes the nearest answer of the same width', () => {
    // The boxes the build really produces: a pit wall strip, a wide pit wall zone, a nano zone.
    expect(archetypeOf(shapeOf({ width: 607, height: 158 }))).toBe('grid');
    expect(archetypeOf(shapeOf({ width: 1007, height: 211 }))).toBe('wide');
    expect(archetypeOf(shapeOf({ width: 269, height: 194 }))).toBe('tallNarrow');
    expect(archetypeOf(shapeOf({ width: 802, height: 336 }))).toBe('wide'); // the companion page
    expect(archetypeOf(shapeOf({ width: 432, height: 706 }))).toBe('tall'); // the companion in portrait
  });
});

describe('the two the ticket works through', () => {
  test('lap times keeps six at wide, four at grid and two at tall narrow', () => {
    const at = (shape: Archetype): readonly string[] => (sheddingFor('lapTimes') as { keeps: Record<Archetype, readonly string[]> }).keeps[shape];
    expect(at('wide')).toHaveLength(6);
    expect(at('grid')).toEqual(['last', 'sessionBest', 'yourBest', 'delta']);
    expect(at('tallNarrow')).toEqual(['last', 'sessionBest']);

    // And the page draws exactly that, which is the half a table alone cannot promise.
    expect(namesAt('lapTimes', 'tallNarrow').filter((n) => n.endsWith('.value')).sort()).toEqual(['last.value', 'sessionBest.value']);
    // The laps and the estimate go before the delta does: shedding is not dropping the tail.
    expect(drewId(namesAt('lapTimes', 'grid'), 'delta')).toBe(true);
    expect(drewId(namesAt('lapTimes', 'grid'), 'laps')).toBe(false);
  });

  test('relative keeps position, code and gap at tall narrow, and every column at wide', () => {
    expect(keepsAt('relative', shapeOf(SHAPE_ARCHETYPES.tallNarrow))).toEqual(['pos', 'name', 'gap']);
    expect(keepsAt('relative', shapeOf(SHAPE_ARCHETYPES.wide))).toEqual([...RELATIVE_COLUMNS]);

    const narrow = namesAt('relative', 'tallNarrow');
    expect(narrow.some((n) => n.includes('head.class'))).toBe(false);
    expect(narrow.some((n) => n.includes('head.gap'))).toBe(true);
  });
});

test('a page the table does not name is an error rather than a page that keeps everything', () => {
  expect(() => sheddingFor('nosuchpage')).toThrow(/no page/);
  // The two zone pages that are not modules go through no table at all.
  expect(keepsAt(undefined, shapeOf(SHAPE_ARCHETYPES.wide))).toBeUndefined();
});
