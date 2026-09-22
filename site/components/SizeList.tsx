/**
 * Every package as a ruled row: the size and the kind, and on the download page the file itself.
 * This is the list a reader without JavaScript gets under the picker.
 */
import { kindLabel, sizeLabel, weigh } from '../lib/packages';
import type { PackageOption } from '../lib/faces';
import styles from './SizeList.module.css';

export function SizeList({ packages, downloads = false }: { packages: readonly PackageOption[]; downloads?: boolean }) {
  return (
    <ul className={`rows ${styles.list}`}>
      {packages.map((p) => (
        <li key={p.folder} className={styles.row}>
          <span className={`num ${styles.size}`}>{sizeLabel(p)}</span>
          <span className={styles.kind}>{kindLabel(p.kind)}</span>
          {downloads ? (
            <span className={styles.get}>
              {p.bytes !== undefined ? (
                <a href={`/downloads/${p.file}`} download className="link">
                  {p.file} <span className="num">{weigh(p.bytes)}</span>
                </a>
              ) : (
                <span className={styles.notBuilt}>Not in this build</span>
              )}
            </span>
          ) : null}
        </li>
      ))}
    </ul>
  );
}
