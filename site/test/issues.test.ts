/**
 * Every issue the site links is one GitHub still has, and every link says the number it goes to.
 *
 * The backlog moved from Linear to GitHub in one import, and the import deleted some of what it
 * created and filed it again under a later number. `docs/backlog.md` is the lookup table, so the
 * numbers the import used and no longer holds can be read from it: the import's creates ran from
 * the table's lowest number for as many numbers as the table has rows, and a number in that span the
 * table does not list answers "This issue was deleted". Four of the comparison's coming rows and the
 * home page's night mode link pointed at such numbers, which were the rows' Linear ids rather than
 * their issues (#813). Whether an issue is still open is GitHub's to answer and is not checked here.
 */
import { describe, expect, test } from 'bun:test';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { rows } from '../lib/compare.ts';
import { SAMPLE_COUNTS, SAMPLE_LISTED } from './sampleCounts.ts';

const site = path.resolve(import.meta.dir, '..');

function sources(dir: string): string[] {
  const out: string[] = [];
  const walk = (d: string) => {
    for (const name of readdirSync(d)) {
      const full = path.join(d, name);
      if (statSync(full).isDirectory()) walk(full);
      else if (/\.tsx?$/.test(name) && !name.endsWith('.generated.ts')) out.push(full);
    }
  };
  walk(dir);
  return out;
}

const FILES = ['app', 'components', 'lib'].flatMap((d) => sources(path.join(site, d)));

/** The numbers the import created and then deleted, read from the lookup table. */
function deletedByTheImport(): Set<number> {
  const table = readFileSync(path.join(site, '..', 'docs', 'backlog.md'), 'utf8');
  const listed = [...table.matchAll(/^\| XOR-\d+ \| \[#(\d+)\]/gm)].map((m) => Number(m[1]));
  const first = Math.min(...listed);
  const held = new Set(listed);
  const deleted = new Set<number>();
  for (let n = first; n < first + listed.length; n++) if (!held.has(n)) deleted.add(n);
  return deleted;
}

/** Every issue a page links: the comparison's rows, and each `issueUrl(n)` a source spells out. */
function cited(): { where: string; n: number }[] {
  const out: { where: string; n: number }[] = [];
  for (const row of rows(SAMPLE_COUNTS, SAMPLE_LISTED)) for (const n of row.cells.opendash.issues ?? []) out.push({ where: `compare/${row.id}`, n });
  for (const f of FILES) for (const m of readFileSync(f, 'utf8').matchAll(/issueUrl\((\d+)\)/g)) out.push({ where: path.relative(site, f), n: Number(m[1]) });
  return out;
}

describe('the issues the site links', () => {
  test('the lookup table gives the import a span with holes in it', () => {
    // The four numbers #813 found deleted, so that a table read wrongly fails here and not silently.
    const deleted = deletedByTheImport();
    for (const n of [84, 99, 116, 128]) expect(deleted.has(n)).toBe(true);
    for (const n of [85, 127, 149, 383]) expect(deleted.has(n)).toBe(false);
  });

  test('none is a number the import deleted', () => {
    const deleted = deletedByTheImport();
    expect(cited().filter((c) => deleted.has(c.n))).toEqual([]);
  });

  test('a link to an issue says the number it goes to', () => {
    const wrong: string[] = [];
    for (const f of FILES) {
      for (const m of readFileSync(f, 'utf8').matchAll(/issueUrl\((\d+)\)[^>]*>\s*#(\d+)/g)) {
        if (m[1] !== m[2]) wrong.push(`${path.relative(site, f)}: #${m[2]} links ${m[1]}`);
      }
    }
    expect(wrong).toEqual([]);
  });
});
