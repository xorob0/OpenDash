/**
 * Every package as a ruled row: the size and the kind. On the download page it is what the plugin
 * zip carries, and no row is a file of its own: the plugin is the only way in (#438).
 */
import { kindLabel, sizeLabel } from '../lib/packages';
import type { PackageOption } from '../lib/faces';
import styles from './SizeList.module.css';

export function SizeList({ packages }: { packages: readonly PackageOption[] }) {
  return (
    <ul className={`rows ${styles.list}`}>
      {packages.map((p) => (
        <li key={p.folder} className={styles.row}>
          <span className={`num ${styles.size}`}>{sizeLabel(p)}</span>
          <span className={styles.kind}>{kindLabel(p.kind)}</span>
        </li>
      ))}
    </ul>
  );
}
