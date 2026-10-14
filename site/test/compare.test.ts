/**
 * The comparison is complete and honest by construction: every row has a cell for every product,
 * a coming feature names its issue, a refusal has its reason, and no competitor cell is left
 * blank. A cell we could not stand behind is cut from the row, never hedged.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { CHECKED_ON, PRODUCTS, rows, scheduled } from '../lib/compare.ts';
import { sizeLabel } from '../lib/packages.ts';
import { FREE_FOREVER, THEMES_FREE } from '../lib/site.ts';
import { SAMPLE_COUNTS, SAMPLE_LISTED } from './sampleCounts.ts';

const ROWS = rows(SAMPLE_COUNTS, SAMPLE_LISTED);

/** Lower case, one kind of apostrophe and quote, no Markdown emphasis, and single spaces. */
const normalise = (s: string): string =>
  s
    .replace(/\*\*/g, '')
    .replace(/[‘’]/g, "'")
    .replace(/[“”]/g, '"')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();

/**
 * The refusals of docs/scope.md's "What is deliberately not built", by name: each paragraph that
 * opens on a bold name starts one, and the paragraphs up to the next bold name are its body. A
 * sentence that tells the line's history, as "This line replaces the sentence that used to stand..."
 * does, is left out of the body: it quotes the position the refusal retired, and a scope quoting it
 * would cite the refusal for the stance it overturned (#548).
 */
function refusalsInScope(): Map<string, string> {
  const doc = readFileSync(path.resolve(import.meta.dir, '..', '..', 'docs', 'scope.md'), 'utf8');
  const start = doc.indexOf('## What is deliberately not built');
  const section = doc.slice(start, doc.indexOf('\n## ', start + 1));
  const out = new Map<string, string>();
  let name: string | undefined;
  for (const paragraph of section.split(/\n\s*\n/)) {
    const bold = /^\*\*([^*]+?)\.?\*\*/.exec(paragraph.trim());
    if (bold) name = normalise(bold[1]!);
    const kept = normalise(paragraph)
      .split(/(?<=\.) /)
      .filter((sentence) => !/\breplace[sd]?\b|\bused to\b/.test(sentence))
      .join(' ');
    if (name !== undefined) out.set(name, `${out.get(name) ?? ''} ${kept}`);
  }
  return out;
}

describe('the comparison', () => {
  test('every row has a cell for every product', () => {
    for (const row of ROWS) for (const p of PRODUCTS) expect({ row: row.id, product: p.id, has: row.cells[p.id] !== undefined }).toEqual({ row: row.id, product: p.id, has: true });
  });

  test('a coming feature names the issue it is scheduled under', () => {
    for (const row of ROWS) {
      const c = row.cells.opendash;
      if (c.mark === 'soon') expect({ row: row.id, issues: c.issues?.length ?? 0 }).not.toEqual({ row: row.id, issues: 0 });
      for (const n of c.issues ?? []) expect(Number.isInteger(n) && n > 0).toBe(true);
    }
  });

  test('a coming feature says what is there today and what is coming', () => {
    // The list under the table prints the sentence beside the label, so an empty one is a blank line there (#813).
    const blank = ROWS.filter((r) => r.cells.opendash.mark === 'soon' && r.cells.opendash.text.trim().length < 10).map((r) => r.id);
    expect(blank).toEqual([]);
  });

  test('a refusal has a reason, and a word that is not the mark repeated', () => {
    for (const row of ROWS) {
      const c = row.cells.opendash;
      if (c.mark === 'notBuilt') expect(c.text.length).toBeGreaterThan(10);
      expect({ row: row.id, repeats: c.text.trim().toLowerCase() === c.word.toLowerCase() + '.' }).toEqual({ row: row.id, repeats: false });
    }
  });

  test('every competitor cell says something', () => {
    const blank = [];
    for (const row of ROWS) for (const p of PRODUCTS) if (p.id !== 'opendash' && row.cells[p.id].text.trim().length < 10) blank.push(`${row.id}/${p.id}`);
    expect(blank).toEqual([]);
  });

  test('a refusal cites the line docs/scope.md has for it, in the document’s own words', () => {
    // The stream overlay's cell quoted a sentence docs/scope.md had replaced, and nothing read the
    // document to notice (#548). A scope is the refusal's bold name, a colon, and a quotation.
    const refusals = refusalsInScope();
    const wrong: string[] = [];
    for (const row of ROWS) {
      const c = row.cells.opendash;
      if (c.word === 'Refused' && !c.scope) wrong.push(`${row.id}: refused without a scope line`);
      if (!c.scope) continue;
      const colon = c.scope.indexOf(': ');
      const name = normalise(c.scope.slice(0, colon));
      const quote = normalise(c.scope.slice(colon + 2)).replace(/\.$/, '');
      const body = refusals.get(name);
      if (colon < 0 || body === undefined) wrong.push(`${row.id}: "${c.scope.slice(0, colon)}" is not a refusal in docs/scope.md`);
      else if (!body.includes(quote)) wrong.push(`${row.id}: docs/scope.md does not say "${quote}" under "${name}"`);
    }
    expect(wrong).toEqual([]);
  });

  test('no cell says it was not checked', () => {
    // compare.ts promises a claim we could not stand behind is cut rather than softened, and the car
    // themes row's DNR cell said "Not checked" under a partial mark (#811).
    const hedged: string[] = [];
    for (const row of ROWS) {
      for (const p of PRODUCTS) {
        const c = row.cells[p.id];
        if (/\bnot (checked|read|verified|known)\b|\bunknown\b|\bunchecked\b/i.test(`${c.word} ${c.text}`)) hedged.push(`${row.id}/${p.id}`);
      }
    }
    expect(hedged).toEqual([]);
  });

  test('every cell carries a word', () => {
    for (const row of ROWS) for (const p of PRODUCTS) expect({ cell: `${row.id}/${p.id}`, word: row.cells[p.id].word.length > 0 }).toEqual({ cell: `${row.id}/${p.id}`, word: true });
  });

  test('the price row makes the promise, themes included', () => {
    const price = ROWS.find((r) => r.id === 'price')?.cells.opendash.text;
    expect(price).toContain(FREE_FOREVER);
    expect(price).toContain(THEMES_FREE);
  });

  test('the car themes are included, and the user’s own colours are still coming', () => {
    expect(ROWS.find((r) => r.id === 'themes')?.cells.opendash.word).toBe('Free');
    expect(scheduled(SAMPLE_COUNTS, SAMPLE_LISTED).map((r) => r.id)).toContain('colours');
    expect(scheduled(SAMPLE_COUNTS, SAMPLE_LISTED).map((r) => r.id)).not.toContain('themes');
  });

  test('the counts in the cells are the build’s', () => {
    expect(ROWS.find((r) => r.id === 'sizes')?.cells.opendash.text).toContain(`${SAMPLE_COUNTS.faces} faces`);
    expect(ROWS.find((r) => r.id === 'flagBox')?.cells.opendash.text).toContain(`${SAMPLE_COUNTS.glyphs} glyphs`);
  });

  test('the sizes cell lists as many sizes as it counts, and they are the build’s', () => {
    // A count read from the build beside a list typed by hand is the drift #560 ended for counts (#819).
    const text = ROWS.find((r) => r.id === 'sizes')?.cells.opendash.text ?? '';
    const m = /^(\d+) faces: (.+)\.$/.exec(text);
    expect(m).not.toBeNull();
    const listed = m![2]!.split(', ');
    expect({ counted: Number(m![1]), listed: listed.length }).toEqual({ counted: SAMPLE_COUNTS.faces, listed: SAMPLE_COUNTS.faces });
    expect(listed).toEqual(SAMPLE_LISTED.faces.map(sizeLabel));
  });

  test('the second screens name the sizes the build makes', () => {
    const companion = ROWS.find((r) => r.id === 'companion')?.cells.opendash.text ?? '';
    for (const s of SAMPLE_LISTED.companions) expect(companion).toContain(sizeLabel(s));
    expect(companion).toContain(`${SAMPLE_COUNTS.pages} pages`);
    const pitWall = ROWS.find((r) => r.id === 'pitWall')?.cells.opendash.text ?? '';
    for (const s of SAMPLE_LISTED.pitWalls) expect(pitWall).toContain(sizeLabel(s));
    expect(pitWall).toContain(`${SAMPLE_COUNTS.pitWallPages} pages`);
  });

  test('the car themes cell names every theme the build makes', () => {
    // The cell described the Porsche alone after the AiM had shipped (#819).
    const text = ROWS.find((r) => r.id === 'themes')?.cells.opendash.text ?? '';
    expect(text).toStartWith(`${SAMPLE_LISTED.themes.length} car themes`);
    for (const t of SAMPLE_LISTED.themes) expect(text).toContain(t.name);
    expect(text).toContain(`${SAMPLE_LISTED.themes.reduce((n, t) => n + t.cars.length, 0)} cars`);
    expect(text).toContain('every rectangular size');
  });

  test('the competitor cells carry the date they were read', () => {
    expect(CHECKED_ON).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    // A row read after the rest of the table carries its own date, which the page prints beside
    // CHECKED_ON; the car themes row's DNR cell was read three weeks after the table it sits in (#811).
    for (const row of ROWS.filter((r) => r.readOn !== undefined)) {
      expect({ row: row.id, date: /^\d{4}-\d{2}-\d{2}$/.test(row.readOn!), later: row.readOn! > CHECKED_ON }).toEqual({ row: row.id, date: true, later: true });
    }
    expect(ROWS.find((r) => r.id === 'themes')?.readOn).toBeDefined();
  });

  test('row ids are unique', () => {
    expect(new Set(ROWS.map((r) => r.id)).size).toBe(ROWS.length);
  });
});
