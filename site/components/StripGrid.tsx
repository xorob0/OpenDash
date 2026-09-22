/**
 * The strip shapes as a grid: sides down, centres across, every cell a shape the build writes a
 * profile for. Then the bare runs, then the legacy shapes with the devices they were added for.
 * All of it is read from the generated list; nothing here names a shape by hand.
 */
import { BARE, CENTRES, LEGACY, SIDES, length, shapeAt } from '../lib/strips';
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

      <div className={styles.groups}>
        <div className={styles.group}>
          <h3 className="h3">Bare runs</h3>
          <p className="prose">Nothing at the ends: 4 to 25 LEDs in a row. A brow, or a strip along the top of a monitor.</p>
          <ul className={styles.runs}>
            {BARE.map((s) => (
              <li key={s.id} className={styles.run}>
                <StripGlyph left={0} centre={s.centre} right={0} led={6} gap={2} title={`${s.centre} LEDs`} />
                <span className={`num ${styles.shapeLabel}`}>{s.centre}</span>
              </li>
            ))}
          </ul>
        </div>

        <div className={styles.group}>
          <h3 className="h3">Legacy shapes</h3>
          <p className="prose">Shipped before the grid and kept, so a wheel that has one does not go dark on an update.</p>
          <ul className={`rows ${styles.legacy}`}>
            {LEGACY.map((s) => (
              <li key={s.id} className={styles.legacyRow}>
                <StripGlyph left={s.left} centre={s.centre} right={s.right} led={6} gap={2} title={s.label} />
                <span className={`num ${styles.shapeLabel}`}>{s.label}</span>
                <span className={styles.devices}>{s.devices.join(', ')}</span>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}
