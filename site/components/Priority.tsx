/**
 * What takes the panel, in order: the flags from the generated catalogue, then the pit states,
 * then the warnings. The first thing on this list that is true is what a strip or the box shows.
 */
import { FLAGS } from '../lib/content.generated';
import { PIT_STATES, SPOTTER, WARNINGS } from '../lib/lights';
import styles from './Priority.module.css';

export function Priority() {
  return (
    <div className={styles.wrap}>
      <ol className={`rows ${styles.list}`}>
        {FLAGS.map((f, i) => (
          <li key={f.id} className={styles.row}>
            <span className={`num ${styles.n}`}>{i + 1}</span>
            <span className={styles.name}>{f.name}</span>
            <span className={styles.tag}>{f.critical ? 'Critical' : 'Dropped when quiet'}</span>
          </li>
        ))}
        <li className={styles.row}>
          <span className={`num ${styles.n}`}>{FLAGS.length + 1}</span>
          <span className={styles.name}>Pit: {PIT_STATES.join(', then ').toLowerCase()}</span>
          <span className={styles.tag}>Critical</span>
        </li>
        <li className={styles.row}>
          <span className={`num ${styles.n}`}>{FLAGS.length + 2}</span>
          <span className={styles.name}>Warnings: {WARNINGS.join(', then ').toLowerCase()}</span>
          <span className={styles.tag}>Critical</span>
        </li>
      </ol>
      <p className={`prose ${styles.spotter}`}>{SPOTTER} A switch keeps only the critical ones.</p>
    </div>
  );
}
