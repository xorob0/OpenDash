/**
 * Which sim shows what, as a driver reads it (#1191).
 *
 * docs/sim-support.md is a table of every reading by every sim, 96 rows by four, and the site used
 * to render it whole. A driver asks one question of it, whether the part of the dash they look at
 * works on the sim they run, so this reduces it to a row per part, a column per sim, and in each
 * cell how many of that part's readings work. A sim that is not claimed has nothing confirmed on
 * it, whatever its column expects, so its cells carry no count.
 */
import type { SimName, SimSection } from '../scripts/content';

export interface SimPart {
  /** The document's section: where the readings are drawn. */
  title: string;
  total: number;
  /** Readings marked Works, per sim. */
  works: Record<SimName, number>;
}

export function countByPart(sections: readonly SimSection[], sims: readonly SimName[]): SimPart[] {
  return sections.map((section) => {
    const works = Object.fromEntries(sims.map((sim) => [sim, 0])) as Record<SimName, number>;
    for (const row of section.rows) for (const sim of sims) if (row.cells[sim].mark === 'Works') works[sim] += 1;
    return { title: section.title, total: section.rows.length, works };
  });
}
