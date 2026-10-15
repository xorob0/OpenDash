/**
 * Which sim shows what: docs/sim-support.md's table, section by section, read at build time (#102).
 *
 * The document is the table's one home and the site renders it rather than restating it, so a cell
 * changed there changes here on the next build. Each cell opens on its mark, which is drawn as the
 * comparison draws its own, a square in a meaning colour beside the word, and the rest of the cell is
 * the document's sentence through the same inline parser the release notes use.
 *
 * On a wide screen a table per section; on a phone the rows stack, each cell labelled with its sim,
 * as the comparison does, so nothing scrolls sideways.
 */
import { Fragment, type ReactNode } from 'react';
import type { SimCell, SimMark, SimName, SimSection } from '../scripts/content';
import { toInline, type Inline } from '../lib/markdown';
import styles from './SimTable.module.css';

/** One parsed span as an element. A repository-relative link keeps its text and loses its target. */
function span(node: Inline, key: number): ReactNode {
  switch (node.kind) {
    case 'strong':
      return <strong key={key}>{node.text}</strong>;
    case 'em':
      return <em key={key}>{node.text}</em>;
    case 'code':
      return <code key={key}>{node.text}</code>;
    case 'link':
      return node.href ? (
        <a key={key} href={node.href} className="link" rel="noopener">
          {node.text}
        </a>
      ) : (
        <Fragment key={key}>{node.text}</Fragment>
      );
    default:
      return <Fragment key={key}>{node.text}</Fragment>;
  }
}

/** The cell's sentence after the bold mark it opens on. */
const rest = (cell: SimCell): string => cell.text.replace(/^\*\*[A-Za-z]+\.?\*\*[.,]?\s*/, '');

const MARK_CLASS: Record<SimMark, string> = {
  Works: styles.works ?? '',
  Partly: styles.partly ?? '',
  Absent: styles.absent ?? '',
  Wrong: styles.wrong ?? '',
  Untested: styles.untested ?? '',
};

function Cell({ cell }: { cell: SimCell }) {
  const text = rest(cell);
  return (
    <div className={styles.mark}>
      <p className={styles.head}>
        <span className={`${styles.square} ${MARK_CLASS[cell.mark]}`} aria-hidden="true" />
        <span className={styles.word}>{cell.mark}</span>
      </p>
      {text ? <p className={styles.text}>{toInline(text).map(span)}</p> : null}
    </div>
  );
}

export function SimTable({ sims, sections }: { sims: readonly SimName[]; sections: readonly SimSection[] }) {
  return (
    <div className={styles.wrap}>
      {sections.map((section) => (
        <div key={section.title} className={styles.section}>
          <h3 className={`label ${styles.title}`}>{section.title}</h3>
          <table className={styles.table}>
            <thead>
              <tr>
                <th scope="col" className={styles.reading}>
                  <span className="label">Reading</span>
                </th>
                {sims.map((sim) => (
                  <th key={sim} scope="col" className={styles.sim}>
                    {sim}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {section.rows.map((row) => (
                <tr key={row.reading} className={styles.row}>
                  <th scope="row" className={styles.reading}>
                    {toInline(row.reading).map(span)}
                  </th>
                  {sims.map((sim) => (
                    <td key={sim} className={styles.cell} data-sim={sim}>
                      <Cell cell={row.cells[sim]} />
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}
    </div>
  );
}
