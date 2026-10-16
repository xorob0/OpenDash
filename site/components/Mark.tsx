/**
 * One cell of the comparison: a square in a meaning colour, a word, the text, and for a refusal in
 * openDash's own column the reason in the scope's own words, linked to it. The issues a coming
 * feature is scheduled under stay in the data, where the tests hold them, and are not printed: a
 * ticket number is the project's bookkeeping, not something a visitor is weighing (#1188).
 */
import type { Cell } from '../lib/compare';
import { SCOPE_URL } from '../lib/site';
import styles from './Mark.module.css';

export function Mark({ cell }: { cell: Cell }) {
  return (
    <div className={styles.cell}>
      <p className={styles.head}>
        <span className={`${styles.square} ${styles[cell.mark]}`} aria-hidden="true" />
        <span className={styles.word}>{cell.word}</span>
      </p>
      {cell.text ? <p className={styles.text}>{cell.text}</p> : null}
      {cell.scope ? (
        <p className={styles.refs}>
          <a href={SCOPE_URL} className="link" rel="noopener">
            Why
          </a>
          : {cell.scope}
        </p>
      ) : null}
    </div>
  );
}
