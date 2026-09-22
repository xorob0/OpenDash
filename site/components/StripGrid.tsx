/**
 * The strip shapes as a grid: sides down, centres across, every cell a shape the build writes a
 * profile for, read from the generated list. The longer bare runs and the legacy shapes exist too
 * and are in the plugin's list; the grid is the picture of the idea.
 */
import { CENTRES, SIDES, length, shapeAt } from '../lib/strips';
import { StripGlyph } from './StripGlyph';
import styles from './StripGrid.module.css';

export function StripGrid() {
  return (
    <div className={styles.wrap}>
      <div className={styles.scroll}>
        <table className={styles.table}>
          <caption className="label">Sides down, centre across. Every cell is 1 profile.</caption>
          <thead>
            <tr>
              <th scope="col" className={styles.corner}>
                <span className="label">Sides</span>
              </th>
              {CENTRES.map((c) => (
                <th key={c} scope="col" className={`num ${styles.head}`}>
                  {c}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {SIDES.map((side) => (
              <tr key={side}>
                <th scope="row" className={`num ${styles.head}`}>
                  {side}
                </th>
                {CENTRES.map((centre) => {
                  const s = shapeAt(side, centre);
                  return (
                    <td key={centre} className={styles.cell}>
                      {s ? (
                        <div className={styles.shape}>
                          <StripGlyph left={s.left} centre={s.centre} right={s.right} led={6} gap={2} title={`${s.label}, ${length(s)} LEDs`} />
                          <span className={`num ${styles.shapeLabel}`}>{s.label}</span>
                        </div>
                      ) : null}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

    </div>
  );
}
