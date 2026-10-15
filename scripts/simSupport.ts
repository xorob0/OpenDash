/**
 * simSupport: the table of which sim shows what, read out of docs/sim-support.md.
 *
 * The document is the table's one home (#102): it is written by hand, a row per page, card, module
 * and lamp, a column per sim, and every cell opens on one of the words in {@link SIM_MARKS}. Two
 * readers need it as data rather than as prose. `packages/dash/test/simSupport.test.ts` holds it to
 * the catalogues, so a page added to a catalogue without a row fails there, and the site's
 * `scripts/content.ts` renders it on the Pages page, so the website says what the document says
 * rather than a copy of it.
 *
 * The parse is for exactly the shape that file has and nothing else: a `## The table` heading, one
 * `### ` heading per place a reading is drawn, and under each a Markdown table whose header is
 * {@link SIM_TABLE_HEADER}. A cell is never split on a `|` inside it, because none of them carries one.
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';

const repoRoot = path.resolve(import.meta.dir, '..');

export const SIM_SUPPORT_FILE = path.join(repoRoot, 'docs', 'sim-support.md');

/** The columns after the first, in the order the document gives them. */
export const SIMS = ['iRacing', 'Assetto Corsa', 'ACC', 'Le Mans Ultimate'] as const;
export type SimName = (typeof SIMS)[number];

/** The sims 1.0 supports, whose columns may not say Untested. The rest may work and are not claimed. */
export const SUPPORTED_SIMS: readonly SimName[] = ['iRacing', 'Assetto Corsa'];

export const SIM_TABLE_HEADER = ['Reading', ...SIMS] as const;

/** The word a cell opens on, in bold, and what it means is the document's own legend. */
export const SIM_MARKS = ['Works', 'Partly', 'Absent', 'Wrong', 'Untested'] as const;
export type SimMark = (typeof SIM_MARKS)[number];

export interface SimCell {
  mark: SimMark;
  /** The whole cell as written, the mark included, in the document's inline Markdown. */
  text: string;
}

export interface SimRow {
  reading: string;
  cells: Record<SimName, SimCell>;
}

export interface SimSection {
  title: string;
  rows: SimRow[];
}

const cellsOf = (line: string): string[] =>
  line
    .trim()
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split('|')
    .map((cell) => cell.trim());

/** The bold word a cell opens on, `**Works.**` or `**Works**` followed by more. */
const markOf = (cell: string): SimMark | undefined => {
  const word = /^\*\*([A-Za-z]+)\.?\*\*/.exec(cell)?.[1];
  return SIM_MARKS.find((mark) => mark === word);
};

/**
 * The sections of the table, in the document's order. Throws on a table whose header is not the
 * sims' or a cell that does not open on a mark, naming the row, since either is a document the two
 * readers would misread rather than one they can skip.
 */
export function parseSimSupport(markdown: string): SimSection[] {
  const start = markdown.indexOf('\n## The table');
  if (start < 0) throw new Error('docs/sim-support.md: no "## The table" heading');
  const end = markdown.indexOf('\n## ', start + 1);
  const body = markdown.slice(start, end < 0 ? undefined : end);

  const sections: SimSection[] = [];
  let section: SimSection | undefined;
  let header = false;
  for (const line of body.split('\n')) {
    const heading = /^### (.+)$/.exec(line);
    if (heading) {
      section = { title: heading[1]!.trim(), rows: [] };
      sections.push(section);
      header = false;
      continue;
    }
    if (!line.trim().startsWith('|')) continue;
    if (section === undefined) throw new Error(`docs/sim-support.md: a table row before any section: ${line}`);
    const cells = cellsOf(line);
    if (!header) {
      if (cells.join('|') !== SIM_TABLE_HEADER.join('|')) throw new Error(`docs/sim-support.md, ${section.title}: the header is not ${SIM_TABLE_HEADER.join(' | ')}`);
      header = true;
      continue;
    }
    if (cells.every((cell) => /^-+$/.test(cell))) continue;
    if (cells.length !== SIM_TABLE_HEADER.length) throw new Error(`docs/sim-support.md, ${section.title}: "${cells[0]}" has ${cells.length} cells`);
    const [reading, ...rest] = cells;
    const row = { reading: reading!, cells: {} as Record<SimName, SimCell> };
    for (const [i, sim] of SIMS.entries()) {
      const text = rest[i]!;
      const mark = markOf(text);
      if (mark === undefined) throw new Error(`docs/sim-support.md, ${section.title}, ${reading}, ${sim}: the cell opens on none of ${SIM_MARKS.join(', ')}`);
      row.cells[sim] = { mark, text };
    }
    section.rows.push(row);
  }
  return sections;
}

export const readSimSupport = (file: string = SIM_SUPPORT_FILE): SimSection[] => parseSimSupport(readFileSync(file, 'utf8'));
