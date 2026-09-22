/**
 * The pages, each with its capture on the 850 x 480 companion, its number and its one-line
 * description from the catalogue. A grid whose 1 px gaps are the rules between cells.
 */
import { Capture } from './Capture';
import { pageFile } from '../lib/captures';
import styles from './PageGrid.module.css';

export interface PageCard {
  number: number;
  id: string;
  name: string;
  description: string;
  /** What is missing, for a page that ships off. Replaces the description. */
  reason?: string;
}

export function PageGrid({ pages, width = 850, height = 480 }: { pages: readonly PageCard[]; width?: number; height?: number }) {
  return (
    <ul className={styles.grid}>
      {pages.map((p) => (
        <li key={p.id} className={styles.cell}>
          <Capture file={pageFile(p.id)} alt={`The ${p.name} page`} width={width} height={height} sizes="(min-width: 62rem) 30vw, (min-width: 48rem) 45vw, 100vw" />
          <div className={styles.text}>
            <p className={`num ${styles.number}`}>{String(p.number).padStart(2, '0')}</p>
            <h3 className="h3">{p.name}</h3>
            <p className={`prose ${styles.description}`}>{p.reason ?? p.description}</p>
          </div>
        </li>
      ))}
    </ul>
  );
}
