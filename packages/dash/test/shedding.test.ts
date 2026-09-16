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
import { PARTS, PREFERS_TALL_NARROW, SHEDDING, archetypeFor, archetypeOf, keepsAt, sheddingFor, type Archetype } from '../src/modules/shedding.ts';
import { densityForBox, type Density } from '../src/second/density.ts';
import { zoneFrame } from '../src/second/header.ts';
import { ARCHETYPES, SHAPE_ARCHETYPES, shapeOf } from '../src/second/shape.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, layoutWithoutRevBar, zonesOf } from '../src/zones/index.ts';

/** Every item name a page draws in a box, which is how a declared id is checked against reality. */
const namesIn = (id: string, box: { width: number; height: number }, density: Density = 'zone'): string[] => {
  const module = MODULES.find((m) => m.id === id)!;
  const items = module.build({ frame: rect(0, 0, box.width, box.height), density, prefix: '' });
  return items.flatMap((item) => [...walkItems([item])]).map((item) => item.name);
};

/** The same at one of the four shapes the catalogue draws. */
const namesAt = (id: string, archetype: Archetype): string[] => namesIn(id, SHAPE_ARCHETYPES[archetype]);

/** The ids a page actually drew: an item is `<id>.label`, `<id>.value`, `<id>.text` and so on. */
const drewId = (names: readonly string[], id: string): boolean => names.some((name) => name === id || name.startsWith(`${id}.`));

/** A table's cells are named after the row rather than after the page, so a column reads as a suffix. */
const drewColumn = (names: readonly string[], id: string): boolean => names.some((name) => name.endsWith(`.row.${id}`) || name.includes(`.row.${id}.`));

/**
 * What proves a part was drawn, where the page does not draw it under its own name.
 *
 * Pit view's corner toggles are the one: the catalogue writes them as the single line
 * `Tyres · RIGHTS` and the module draws one toggle per corner, so the part is declared as the
 * drawing names it and proved by what the module draws.
 */
const PART_ITEMS: Record<string, readonly string[]> = { 'pitView.tyres': ['FrontLeft', 'FrontRight', 'RearLeft', 'RearRight'] };

const drewPart = (names: readonly string[], page: string, id: string): boolean =>
  (PART_ITEMS[`${page}.${id}`] ?? [id]).every((item) => drewId(names, item));

/** The ids a page declares at a drawing but does not draw in this box. */
const undrawn = (page: string, names: readonly string[], archetype: Archetype): string[] => {
  const entry = SHEDDING[page]!;
  const missing: string[] = [];
  if (entry.kind === 'fields') missing.push(...entry.keeps[archetype].filter((id) => !drewId(names, id)));
  if (entry.kind === 'columns') missing.push(...entry.keeps[archetype].filter((id) => !drewColumn(names, id)));
  const parts = PARTS[page];
  if (parts) missing.push(...parts[archetype].filter((id) => !drewPart(names, page, id)));
  return missing;
};

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

describe('the parts that are neither fields nor columns', () => {
  test('a page declares no part it cannot draw', () => {
    for (const [page, parts] of Object.entries(PARTS)) {
      const names = namesAt(page, 'wide');
      expect({ page, missing: parts.wide.filter((id) => !drewPart(names, page, id)) }).toEqual({ page, missing: [] });
    }
  });

  test('and draws a part at the shapes that declare it, and at no other', () => {
    for (const [page, parts] of Object.entries(PARTS)) {
      for (const archetype of ARCHETYPES) {
        const names = namesAt(page, archetype);
        const drawn = parts.wide.filter((id) => drewPart(names, page, id));
        expect({ page, archetype, drawn }).toEqual({ page, archetype, drawn: [...parts[archetype]] });
      }
    }
  });

  test('the tyres caption is the one that was being lost to arithmetic, and is not any more', () => {
    // The module sized its two rows to the frame exactly, so `rowsThatFit` shed the caption at
    // every size rather than at the one shape the catalogue drops it.
    expect(drewId(namesIn('tyres', { width: 437, height: 276 }), 'footer')).toBe(true);
    expect(drewId(namesIn('tyres', { width: 254, height: 292 }, 'compact'), 'footer')).toBe(false);
  });
});

describe('two pages take a drawing the bands cannot ask for', () => {
  const drawingOf = (page: string, box: { width: number; height: number }): Archetype => archetypeFor(page, shapeOf(box), box);

  test('a grid box shorter than the floor gives them the tall narrow drawing', () => {
    // The 1280 x 400 face's zone body in both arrangements, against the 1280 x 480 face's.
    for (const page of PREFERS_TALL_NARROW) {
      expect({ page, at: drawingOf(page, { width: 437, height: 214 }) }).toEqual({ page, at: 'tallNarrow' });
      expect({ page, at: drawingOf(page, { width: 437, height: 248 }) }).toEqual({ page, at: 'tallNarrow' });
      expect({ page, at: drawingOf(page, { width: 437, height: 276 }) }).toEqual({ page, at: 'grid' });
      // The catalogue's own grid drawing is drawn at 430 x 300 and stays itself.
      expect({ page, at: drawingOf(page, SHAPE_ARCHETYPES.grid) }).toEqual({ page, at: 'grid' });
      // A wide short box is the 600 x 686 face and the pit wall's strips, which is a different
      // question and answered by `archetypeOf` alone.
      expect({ page, at: drawingOf(page, { width: 580, height: 124 }) }).toEqual({ page, at: 'grid' });
    }
  });

  test('and no other page is moved by it', () => {
    for (const { id } of MODULE_CATALOGUE) {
      if (PREFERS_TALL_NARROW.includes(id)) continue;
      expect({ id, at: drawingOf(id, { width: 437, height: 214 }) }).toEqual({ id, at: 'grid' });
    }
  });

  test('so the 1280 x 400 zone draws four car settings cells and a lap history with no header', () => {
    const box = { width: 437, height: 214 };
    const settings = namesIn('carSettings', box);
    expect(['car', 'tc', 'abs', 'bb'].every((id) => drewId(settings, id))).toBe(true);
    expect(['mix', 'arbFront', 'arbRear'].some((id) => drewId(settings, id))).toBe(false);
    expect(drewId(namesIn('lapHistory', box), 'head')).toBe(false);
    expect(drewId(namesIn('lapHistory', { width: 437, height: 276 }), 'head')).toBe(true);
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
  test('lap times keeps six at wide and four at grid and at tall narrow', () => {
    const at = (shape: Archetype): readonly string[] => (sheddingFor('lapTimes') as { keeps: Record<Archetype, readonly string[]> }).keeps[shape];
    expect(at('wide')).toHaveLength(6);
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
    expect(keepsAt('relative', 'tallNarrow')).toEqual(['pos', 'name', 'gap']);
    expect(keepsAt('relative', 'wide')).toEqual([...RELATIVE_COLUMNS]);

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
  const PARTS_HEADING = '### The parts that are not fields';
  const section = doc.slice(doc.indexOf('### The table'), doc.indexOf(PARTS_HEADING));
  const partsSection = doc.slice(doc.indexOf(PARTS_HEADING), doc.indexOf('## 6. Band D'));
  const cells = (text: string): string[] =>
    text
      .split('·')
      .map((part) => part.trim().replace(/`/g, ''))
      .filter((part) => part.length > 0);
  /** The four cells of a row of either table, by the page name they are written against. */
  const rowsOf = (text: string): Map<string, string[][]> => {
    const rows = new Map<string, string[][]>();
    for (const line of text.split('\n')) {
      if (!line.startsWith('| ') || line.startsWith('| № ') || line.startsWith('|---')) continue;
      const [, , page, ...rest] = line.split('|').map((c) => c.trim());
      rows.set(page!, rest.slice(0, 4).map(cells));
    }
    return rows;
  };

  test('every page that sheds has a row, and the row is the declaration', () => {
    const rows = rowsOf(section);
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

  test('and the second table, the parts that are neither fields nor columns', () => {
    const rows = rowsOf(partsSection);
    const written = new Map(MODULE_CATALOGUE.filter((m) => PARTS[m.id]).map((m) => [m.name, m.id]));
    expect([...rows.keys()].sort()).toEqual([...written.keys()].sort());
    for (const [name, id] of written) {
      const parts = PARTS[id]!;
      expect({ id, row: rows.get(name) }).toEqual({ id, row: [parts.wide, parts.grid, parts.tallNarrow, parts.tall].map((ids) => [...ids]) });
    }
  });
});

/**
 * The last check, and the one no table can make on its own: at every rectangle the build really
 * hands a zone, the ids a page draws are the ids it declares.
 *
 * A declaration is a design decision and a box is a fact, so the two can disagree. Where they do,
 * something dropped the field after the declaration had kept it: `rowsThatFit` taking a row off the
 * stack, a rank shedding its tail to fit the width, a list's column popping for the name beside it.
 * Each of those is a shed nobody decided, and each belongs to a ticket of its own. They are pinned
 * below rather than tolerated, so that a new one fails this suite instead of a reviewer, and so
 * that fixing one is a line removed from the list.
 */
describe('at every zone body the build produces, the ids drawn are the ids declared', () => {
  /** Page by page, what a real zone body drops although the table kept it. */
  const UNDECLARED: Record<string, readonly string[]> = {
    // A fourth lap time does not fit a 214 px body beside three others at their grown size, so the
    // rank sheds the delta the `grid` drawing keeps. readability-pass.md §1 owns the redraw.
    '1280x400 469x258 lapTimes': ['delta'],
    '800x286 269x194 lapTimes': ['delta'],
    // The gap column pops when the name column is squeezed under its minimum, which on a list is
    // the column the page exists for: XOR is tracking it as the 800 x 480 list gap.
    '800x480 249x328 leaderboard': ['gap'],
    '800x480 249x328 relative': ['gap'],
    '800x480 249x366 leaderboard': ['gap'],
    '800x480 249x366 relative': ['gap'],
    // The refuel figure and the five-lap average are the last two fields of a rank the 600 x 686
    // face's 114 px zones have no room for: two ranks plus the level bar need about 124 px at the
    // compact ramp, so the two lead readings are what survive. The nano keeps its rank whole now
    // that fuel leads with the tank, the time and the laps rather than spreading three readings
    // over one line.
    '600x686 600x160 fuel': ['toAdd', 'average'],
    '600x686 600x150 fuel': ['toAdd', 'average'],
    // Half the subject: a page about the car ahead and the car behind draws one of them. This is
    // the clearest defect of the set, and it is the opponents block's own ticket.
    '800x286 269x194 opponents': ['behind.gap', 'behind.name'],
    '600x686 600x160 opponents': ['behind.gap', 'behind.name', 'behind.num', 'behind.class', 'behind.detail'],
    '600x686 600x150 opponents': ['behind.gap', 'behind.name', 'behind.num', 'behind.class', 'behind.detail'],
    // The two anti-roll bars are the cells the 124 px band cannot hold.
    '600x686 600x160 carSettings': ['arbFront', 'arbRear'],
    '600x686 600x150 carSettings': ['arbFront', 'arbRear'],
    // Zone C of the 600 x 686 face is 114 px, ten less than zone B, and that is where a second row
    // stops fitting at all: the delta loses its scale, the sectors their three lap times, the
    // stint its two stops.
    '600x686 600x150 delta': ['scale'],
    '600x686 600x150 sectors': ['yourBest', 'last', 'sessionBest'],
    '600x686 600x150 stint': ['stops', 'lastStop'],
    // And zone B with it, for the same reason as fuel above.
    '600x686 600x160 stint': ['stops', 'lastStop'],
    // The nano's session keeps the position and the class, which is what the page is read for, and
    // sheds the lap and the time left: its two counters each carry a denominator now, and a
    // denominator at 0.7 of a value is wider than the small label it replaced.
    '800x286 269x194 session': ['lap', 'timeLeft'],
  };

  /** Every rectangle the build hands a zone: both arrangements of every face, deduplicated. */
  const bodies = ((): [string, { body: { width: number; height: number }; density: Density }][] => {
    const seen = new Map<string, { body: { width: number; height: number }; density: Density }>();
    for (const layout of ZONE_FACES) {
      for (const arrangement of [layout, layoutWithoutRevBar(layout)]) {
        for (const zone of zonesOf(arrangement)) {
          if (zone.zone === 'A' || zone.zone === 'D') continue;
          const key = `${layout.width}x${layout.height} ${zone.size.width}x${zone.size.height}`;
          if (seen.has(key)) continue;
          const density = densityForBox(zone.size);
          // The body a zone really gives its page. Neither the counter nor the letter moves it:
          // `zoneFrame` cuts the body from the frame and the density alone.
          const { body } = zoneFrame('zone', { frame: rect(0, 0, zone.size.width, zone.size.height), title: 'Lap times', counter: { kind: 'reserved', widest: '21 / 21' } }, density);
          seen.set(key, { body, density });
        }
      }
    }
    return [...seen.entries()];
  })();

  test('the faces really produce the rectangles this is checked against', () => {
    // Not a count: the eight faces' own rectangles are their business. What matters here is that
    // the list is the build's and not a copy of it, so the two below are spot checks.
    expect(bodies.map(([key]) => key)).toContain('1280x400 469x258');
    expect(bodies.map(([key]) => key)).toContain('600x686 600x150');
    expect(bodies.length).toBeGreaterThan(8);
  });

  test('and every page at every one of them draws what it declared', () => {
    const found: Record<string, string[]> = {};
    for (const [key, { body, density }] of bodies) {
      for (const { id } of MODULE_CATALOGUE) {
        const missing = undrawn(id, namesIn(id, body, density), archetypeFor(id, shapeOf(body), body));
        if (missing.length > 0) found[`${key} ${id}`] = missing;
      }
    }
    expect(found).toEqual(UNDECLARED as Record<string, string[]>);
  });
});

test('a page the table does not name is an error rather than a page that keeps everything', () => {
  expect(() => sheddingFor('nosuchpage')).toThrow(/no page/);
  // The two zone pages that are not modules go through no table at all.
  expect(keepsAt(undefined, 'wide')).toBeUndefined();
});
