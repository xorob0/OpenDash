/**
 * Which sim shows what: a row per part of the dash, a column per sim, and in each cell whether that
 * part works there, read from docs/sim-support.md at build time (#102, #1191). A part works when
 * every reading in it does; otherwise the cell counts the readings that do. The sims that are not
 * claimed share one column that says so, and the document is linked for the reading-by-reading
 * table.
 */
import type { SimName, SimSection } from '../scripts/content';
import { countByPart } from '../lib/sims';
import { SIM_SUPPORT_URL } from '../lib/site';
import styles from './SimSummary.module.css';

function Count({ works, total }: { works: number; total: number }) {
  const tone = works === total ? styles.works : works > 0 ? styles.partly : styles.none;
  return (
    <span className={styles.mark}>
      <span className={`${styles.square} ${tone}`} aria-hidden="true" />
      <span className={styles.word}>{works === total ? 'Works' : `${works} of ${total}`}</span>
    </span>
  );
}

export function SimSummary({ sims, supported, sections }: { sims: readonly SimName[]; supported: readonly SimName[]; sections: readonly SimSection[] }) {
  const claimed = sims.filter((sim) => supported.includes(sim));
  const unclaimed = sims.filter((sim) => !supported.includes(sim));
  const parts = countByPart(sections, claimed);
  return (
    <div className={styles.wrap}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th scope="col" className={styles.part}>
              <span className="label">Part</span>
            </th>
            {claimed.map((sim) => (
              <th key={sim} scope="col" className={styles.sim}>
                {sim}
              </th>
            ))}
            {unclaimed.length > 0 ? (
              <th scope="col" className={styles.sim}>
                {unclaimed.join(', ')}
              </th>
            ) : null}
          </tr>
        </thead>
        <tbody>
          {parts.map((part) => (
            <tr key={part.title} className={styles.row}>
              <th scope="row" className={styles.part}>
                {part.title}
              </th>
              {claimed.map((sim) => (
                <td key={sim} className={styles.cell}>
                  <Count works={part.works[sim]} total={part.total} />
                </td>
              ))}
              {unclaimed.length > 0 ? (
                <td className={styles.cell}>
                  <span className={styles.mark}>
                    <span className={`${styles.square} ${styles.none}`} aria-hidden="true" />
                    <span className={styles.word}>Untested</span>
                  </span>
                </td>
              ) : null}
            </tr>
          ))}
        </tbody>
      </table>
      <p className={`prose ${styles.more}`}>
        <a href={SIM_SUPPORT_URL} className="link" rel="noopener">
          Reading by reading, sim by sim
        </a>
      </p>
    </div>
  );
}
