/**
 * One cell of the comparison: a square in a meaning colour, a word, the text, and for openDash's
 * own column the issue a coming feature is scheduled under or the scope line a refusal cites.
 */
import type { Cell } from '../lib/compare';
import { SCOPE_URL, issueUrl } from '../lib/site';
import styles from './Mark.module.css';

export function Mark({ cell }: { cell: Cell }) {
  return (
    <div className={styles.cell}>
      <p className={styles.head}>
        <span className={`${styles.square} ${styles[cell.mark]}`} aria-hidden="true" />
        <span className={styles.word}>{cell.word}</span>
      </p>
      {cell.text ? <p className={styles.text}>{cell.text}</p> : null}
      {cell.issues && cell.issues.length > 0 ? (
        <p className={styles.refs}>
          {cell.issues.map((n, i) => (
            <span key={n}>
              {i > 0 ? ', ' : ''}
              <a href={issueUrl(n)} className="link" rel="noopener">
                #{n}
              </a>
            </span>
          ))}
        </p>
      ) : null}
      {cell.scope ? (
        <p className={styles.refs}>
          <a href={SCOPE_URL} className="link" rel="noopener">
            docs/scope.md
          </a>
          : {cell.scope}
        </p>
      ) : null}
    </div>
  );
}
