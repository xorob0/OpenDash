/**
 * The comparison: one row per feature, one column per product. On a wide screen a table; on a
 * phone the same rows stacked, each cell labelled with its product, so nothing scrolls sideways.
 */
import { PRODUCTS, ROWS } from '../lib/compare';
import { VERSION } from '../lib/content.generated';
import { longDate } from '../lib/captures';
import { Mark } from './Mark';
import styles from './CompareTable.module.css';

export function CompareTable() {
  return (
    <div className={styles.wrap}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th scope="col" className={styles.feature}>
              <span className="label">Feature</span>
            </th>
            {PRODUCTS.map((p) => (
              <th key={p.id} scope="col" className={styles.product}>
                <span className={styles.productName}>{p.name}</span>
                <span className={styles.asOf}>{p.asOf ? `checked ${longDate(p.asOf)}` : `version ${VERSION}`}</span>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {ROWS.map((row) => (
            <tr key={row.id} className={styles.row}>
              <th scope="row" className={styles.feature}>
                {row.label}
              </th>
              {PRODUCTS.map((p) => (
                <td key={p.id} className={styles.cell} data-product={p.short}>
                  <Mark cell={row.cells[p.id]} />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
