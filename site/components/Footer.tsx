/**
 * The footer. It carries the two things the site is obliged to carry — the licence and the
 * redistribution credits for the font and the icon set — and the navigation, and nothing else.
 */
import Link from 'next/link';
import { NAV } from '../lib/site';
import { VERSION } from '../lib/content.generated';
import { Wordmark } from './Wordmark';
import styles from './Footer.module.css';

export function Footer() {
  return (
    <footer className={`ruled ${styles.footer}`}>
      <div className={`page ${styles.grid}`}>
        <div className={styles.brand}>
          <Wordmark size={20} />
          <p className={styles.line}>
            An open-source sim racing dashboard for SimHub. Fourteen screens, one plugin, MIT
            licence.
          </p>
          <p className={`num ${styles.version}`}>{VERSION}</p>
        </div>

        <nav className={styles.col} aria-label="Footer">
          <p className="label">The dashboards</p>
          {NAV.map((item) => (
            <Link key={item.href} href={item.href} className={styles.link}>
              {item.label}
            </Link>
          ))}
          <Link href="/download" className={styles.link}>
            Download
          </Link>
        </nav>

        <div className={styles.col}>
          <p className="label">Credits</p>
          <p className={styles.fine}>
            Barlow and Barlow Condensed by Jeremy Tribby, redistributed under the{' '}
            <a className="link" href="/fonts/OFL.txt">
              SIL Open Font Licence 1.1
            </a>
            .
          </p>
          <p className={styles.fine}>
            Telltale pictograms from Material Design Icons by Pictogrammers, under the Apache
            Licence 2.0.
          </p>
          <p className={styles.fine}>
            SimHub is by SimHub / Wotever. OpenDash is not affiliated with it, with iRacing, or with
            any sim.
          </p>
        </div>
      </div>
    </footer>
  );
}
