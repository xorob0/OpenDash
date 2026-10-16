/**
 * Which sim shows what, as a driver reads it (#1191).
 *
 * docs/sim-support.md is a table of every reading by every sim, and the site used to render it
 * whole: 114 rows, four columns, three cells in four saying Works or Untested. A driver asks one
 * question of it, which is what does not simply work on the sim they run, so this reduces a sim's
 * column to that: how many readings work, and the rest grouped by where they are drawn, each with
 * the first sentence of the document's note. A ticket number or a document link in a note is the
 * project's bookkeeping and is not printed, as the comparison's cells are not (#1188).
 */
import type { SimCell, SimMark, SimName, SimSection } from '../scripts/content';

/** The word a mark is printed as. Absent is the legend's own meaning, said the way the dash says it. */
export const MARK_WORD: Record<SimMark, string> = {
  Works: 'Works',
  Partly: 'Partly',
  Absent: 'Not published',
  Wrong: 'Wrong',
  Untested: 'Untested',
};

export interface SimException {
  reading: string;
  mark: SimMark;
  /** The first sentence of the document's note after the mark, with the bookkeeping gone. Can be empty. */
  note: string;
}

export interface SimExceptionGroup {
  /** The document's section: where the readings are drawn. */
  title: string;
  items: SimException[];
}

export interface SimSummary {
  sim: SimName;
  works: number;
  total: number;
  groups: SimExceptionGroup[];
}

/** `(#564)`, `(#1123, #567)` and `([flag-box.md](flag-box.md))`, with the space before them. */
const BOOKKEEPING = /\s*\((?:#\d+|\[[^\]]*\]\([^)]*\))(?:,\s*(?:#\d+|\[[^\]]*\]\([^)]*\)))*\)/g;

/** The bold mark a cell opens on, with the full stop or comma after it. */
const MARK = /^\*\*[A-Za-z]+\.?\*\*[.,]?\s*/;

export function note(cell: SimCell): string {
  const text = cell.text.replace(MARK, '').replace(BOOKKEEPING, '').trim();
  return /^(.*?[.!?])(?=\s|$)/.exec(text)?.[1] ?? text;
}

export function summarise(sections: readonly SimSection[], sim: SimName): SimSummary {
  let total = 0;
  let works = 0;
  const groups: SimExceptionGroup[] = [];
  for (const section of sections) {
    const items: SimException[] = [];
    for (const row of section.rows) {
      total += 1;
      const cell = row.cells[sim];
      if (cell.mark === 'Works') works += 1;
      else items.push({ reading: row.reading, mark: cell.mark, note: note(cell) });
    }
    if (items.length > 0) groups.push({ title: section.title, items });
  }
  return { sim, works, total, groups };
}
