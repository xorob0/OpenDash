/**
 * Numbered steps, one per row, a rule between them. A step can be marked important, which is the
 * one everybody misses on the install page, and can carry one command in a block.
 */
import styles from './Steps.module.css';

export interface Step {
  title: React.ReactNode;
  body: React.ReactNode;
  important?: boolean;
  /** A single command shown in a block under the body. */
  command?: string;
}

export function Steps({ steps }: { steps: Step[] }) {
  return (
    <ol className={`rows ${styles.list}`}>
      {steps.map((s, i) => (
        <li key={i} className={`${styles.step} ${s.important ? styles.important : ''}`}>
          <span className={`num ${styles.n}`}>{i + 1}</span>
          <div className={styles.text}>
            <h3 className="h3">{s.title}</h3>
            <p className="prose">{s.body}</p>
            {s.command ? <pre className="pre">{s.command}</pre> : null}
          </div>
        </li>
      ))}
    </ol>
  );
}
