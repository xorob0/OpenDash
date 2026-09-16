/**
 * The shedding order: that every page declares one, that what it declares is what it draws, and
 * that the table still says what the catalogue's eighty-four drawings say.
 *
 * The table is data rather than mechanism, so the checks are about drift. A field renamed in a
 * module and not in the table is a page that silently keeps everything; a shape that names a field
 * the page does not have is a line nobody will notice is dead.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
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
  test('lap times keeps twelve at wide, six at tall and four at grid and at tall narrow', () => {
    const at = (shape: Archetype): readonly string[] => (sheddingFor('lapTimes') as { keeps: Record<Archetype, readonly string[]> }).keeps[shape];
    // Twelve is the companion artboard's drawing: the six a zone draws, then the five-lap average,
    // the position and the stint lap, then the three sectors of the last lap. The catalogue's own
    // zone drawings keep six, so `tall` keeps six and the extra rank lives at `wide` alone.
    expect(at('wide')).toHaveLength(12);
    expect(at('tall')).toEqual(['last', 'sessionBest', 'yourBest', 'laps', 'estimated', 'delta']);
    expect(at('grid')).toEqual(['last', 'sessionBest', 'yourBest', 'delta']);
    // The catalogue draws two here and the build takes four: a zone that stacks one column has the
    // height for them, and 234 px of the base face's zone B was empty. zones.md §10 records it.
    expect(at('tallNarrow')).toEqual(['last', 'sessionBest', 'yourBest', 'delta']);

    // And the page draws exactly that, which is the half a table alone cannot promise.
    expect(namesAt('lapTimes', 'tallNarrow').filter((n) => n.endsWith('.value')).sort()).toEqual(['delta.value', 'last.value', 'sessionBest.value', 'yourBest.value']);
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

/**
 * The written table and the code one, kept in step the way `contract.ts` and `Contract.cs` are.
 *
 * docs/design/zones.md is the specification a reader reaches for and the canvas is what both were
 * read off, so a table in one and not the other is the drift every docs ticket in this repository
 * has been about.
 */
describe('docs/design/zones.md carries the same table', () => {
  const doc = readFileSync(new URL('../../../docs/design/zones.md', import.meta.url), 'utf8');
  const section = doc.slice(doc.indexOf('### The table'), doc.indexOf('## 6. Band D'));
  const cells = (text: string): string[] =>
    text
      .split('·')
      .map((part) => part.trim().replace(/`/g, ''))
      .filter((part) => part.length > 0);

  test('every page that sheds has a row, and the row is the declaration', () => {
    const rows = new Map<string, string[][]>();
    for (const line of section.split('\n')) {
      if (!line.startsWith('| ') || line.startsWith('| № ') || line.startsWith('|---')) continue;
      const [, , page, ...rest] = line.split('|').map((c) => c.trim());
      rows.set(page!, rest.slice(0, 4).map(cells));
    }
    const written = new Map(MODULE_CATALOGUE.filter((m) => SHEDDING[m.id]!.kind !== 'nothing').map((m) => [m.name, m.id]));
    expect([...rows.keys()].sort()).toEqual([...written.keys()].sort());
    for (const [name, id] of written) {
      const entry = SHEDDING[id]!;
      if (entry.kind === 'nothing') continue;
      expect({ id, row: rows.get(name) }).toEqual({ id, row: [entry.keeps.wide, entry.keeps.grid, entry.keeps.tallNarrow, entry.keeps.tall].map((ids) => [...ids]) });
    }
  });

  test('and lists the pages with nothing to shed, with the reason the code gives', () => {
    const listed = new Map<string, string>();
    for (const line of section.split('\n')) {
      const match = /^- \*\*[^*]+\*\* \(`([^`]+)`\) — (.+)\.$/.exec(line);
      if (match) listed.set(match[1]!, match[2]!);
    }
    const expected = new Map(Object.entries(SHEDDING).flatMap(([id, entry]) => (entry.kind === 'nothing' ? [[id, entry.why] as const] : [])));
    expect(Object.fromEntries(listed)).toEqual(Object.fromEntries(expected));
  });
});

test('a page the table does not name is an error rather than a page that keeps everything', () => {
  expect(() => sheddingFor('nosuchpage')).toThrow(/no page/);
  // The two zone pages that are not modules go through no table at all.
  expect(keepsAt(undefined, shapeOf(SHAPE_ARCHETYPES.wide))).toBeUndefined();
});
