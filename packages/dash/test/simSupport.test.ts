/**
 * docs/sim-support.md, the table of which sim shows what (#102), has a row for every page a driver can
 * reach, and every cell of it says something a reader can act on.
 *
 * The table is written by hand, and it goes stale the quiet way: a module added to the catalogue with
 * no row is a page whose behaviour on Assetto Corsa nobody has written down, and the first person to
 * ask is a driver on Assetto Corsa looking at it. So every catalogue a package offers is read here and
 * each of its pages has to be named by a row under the section that draws it: the modules, zone A,
 * band D's offered pages, the bar's fields, the pit wall's pages and its zone pages that are not
 * modules, the round faces' cards, and each car theme.
 *
 * A row names a page by the catalogue's own name, or several by those names joined with commas, as
 * the round faces' rows do.
 */
import { describe, expect, test } from 'bun:test';
import { parseSimSupport, readSimSupport, SIMS, SUPPORTED_SIMS, type SimSection } from '../../../scripts/simSupport.ts';
import {
  BAR_FIELDS,
  CARD_CATALOGUE,
  MODULE_CATALOGUE,
  offeredPages,
  PIT_WALL_PAGES,
  PIT_WALL_WIDE_ZONE_PAGES,
  PIT_WALL_ZONE_PAGES,
  THEME_CATALOGUE,
  ZONE_A_PAGES,
} from '../src/contract.ts';

const TABLE = readSimSupport();

const section = (title: string): SimSection => {
  const found = TABLE.find((s) => s.title === title);
  if (found === undefined) throw new Error(`docs/sim-support.md has no section "${title}"`);
  return found;
};

/**
 * The pages a section's rows name: each row's reading whole, and split where it lists several. Whole as
 * well, since a catalogue name may carry a comma of its own, as zone A's "Gear, speed, revs" does.
 */
const named = (title: string): Set<string> => new Set(section(title).rows.flatMap((row) => [row.reading, ...row.reading.split(', ').map((name) => name.trim())]));

/** The names of `pages` that no row of `title` names. */
const missing = (title: string, pages: readonly string[]): string[] => {
  const rows = named(title);
  return pages.filter((page) => !rows.has(page));
};

describe('docs/sim-support.md has a row for every page', () => {
  test('every module', () => {
    expect(missing('The modules', MODULE_CATALOGUE.map((m) => m.name))).toEqual([]);
  });

  test("every page of zone A, and every page band D offers", () => {
    expect(missing('Zone A', ZONE_A_PAGES.map((p) => p.name))).toEqual([]);
    expect(missing('Band D', offeredPages('D').map((p) => p.name))).toEqual([]);
  });

  test("every field of the bar", () => {
    expect(missing('The bar', BAR_FIELDS.map((f) => f.name))).toEqual([]);
  });

  test("every pit wall page, and every zone page that is not a module", () => {
    expect(missing('The pit wall', PIT_WALL_PAGES.map((p) => p.name))).toEqual([]);
    const modules = new Set(MODULE_CATALOGUE.map((m) => m.id));
    const own = [...PIT_WALL_ZONE_PAGES, ...PIT_WALL_WIDE_ZONE_PAGES].filter((p) => !modules.has(p.id)).map((p) => p.name);
    expect(own.length).toBeGreaterThan(0);
    expect(missing('The pit wall', [...new Set(own)])).toEqual([]);
  });

  test("every card of the round faces", () => {
    expect(missing('The round faces', CARD_CATALOGUE.map((c) => c.displayName))).toEqual([]);
  });

  test('every car theme', () => {
    const themes = THEME_CATALOGUE.filter((t) => t.cars.length > 0).map((t) => t.name);
    expect(themes.length).toBeGreaterThan(0);
    const rows = section('The themes').rows.map((row) => row.reading);
    expect(themes.filter((name) => !rows.some((reading) => reading.startsWith(`${name}:`)))).toEqual([]);
  });

  test('names no page twice in one section', () => {
    const twice: string[] = [];
    for (const s of TABLE) {
      const seen = new Set<string>();
      for (const row of s.rows) {
        if (seen.has(row.reading)) twice.push(`${s.title}: ${row.reading}`);
        seen.add(row.reading);
      }
    }
    expect(twice).toEqual([]);
  });
});

describe('every cell of docs/sim-support.md', () => {
  test('a supported sim is never untested', () => {
    // 1.0 supports iRacing and Assetto Corsa (docs/scope.md). A cell of either that says Untested is a
    // reading the claim covers and nobody has answered for.
    const untested = TABLE.flatMap((s) => s.rows.flatMap((row) => SUPPORTED_SIMS.filter((sim) => row.cells[sim].mark === 'Untested').map((sim) => `${s.title}: ${row.reading}, ${sim}`)));
    expect(untested).toEqual([]);
  });

  test('a cell that is wrong names the ticket that fixes it', () => {
    const unnamed = TABLE.flatMap((s) =>
      s.rows.flatMap((row) => SIMS.filter((sim) => row.cells[sim].mark === 'Wrong' && !/#\d+/.test(row.cells[sim].text)).map((sim) => `${s.title}: ${row.reading}, ${sim}`)),
    );
    expect(unnamed).toEqual([]);
  });

  test('the parse refuses a cell that opens on no mark, and a table with another header', () => {
    const doc = (header: string, row: string): string => `# T\n\n## The table\n\n### S\n\n${header}\n|---|---|---|---|---|\n${row}\n`;
    const header = '| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |';
    expect(parseSimSupport(doc(header, '| Gear | **Works.** | **Works.** | **Untested.** | **Untested.** |'))[0]?.rows[0]?.cells['Assetto Corsa'].mark).toBe('Works');
    expect(() => parseSimSupport(doc(header, '| Gear | **Works.** | Fine. | **Untested.** | **Untested.** |'))).toThrow(/Gear, Assetto Corsa/);
    expect(() => parseSimSupport(doc('| Reading | iRacing | ACC |', '| Gear | **Works.** | **Works.** |'))).toThrow(/header/);
  });
});
