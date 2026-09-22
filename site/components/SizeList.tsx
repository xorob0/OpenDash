/**
 * Every package as a ruled row: the size, the kind, what it is for, and its download. This is the
 * list a reader without JavaScript gets, and the list the download page shows.
 */
import { kindLabel, sizeLabel, weigh } from '../lib/packages';
import type { PackageOption } from '../lib/faces';
import styles from './SizeList.module.css';

export function SizeList({ packages }: { packages: readonly PackageOption[] }) {
  return (
    <ul className={`rows ${styles.list}`}>
      {packages.map((p) => (
        <li key={p.folder} className={styles.row}>
          <span className={`num ${styles.size}`}>{sizeLabel(p)}</span>
          <span className={styles.kind}>
            {kindLabel(p.kind)}
            {p.note?.emphasis ? <span className={styles.tag}>{p.note.emphasis === 'base' ? 'Base size' : 'Large size'}</span> : null}
          </span>
          <span className={styles.what}>{p.note?.what ?? p.folder}</span>
          <span className={styles.get}>
            {p.bytes !== undefined ? (
              <a href={`/downloads/${p.file}`} download className="link">
                Download <span className="num">{weigh(p.bytes)}</span>
              </a>
            ) : (
              <span className={styles.notBuilt}>Not in this build</span>
            )}
          </span>
        </li>
      ))}
    </ul>
  );
}
