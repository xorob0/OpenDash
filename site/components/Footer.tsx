/**
 * The footer: the promise once more, every page, the repository, and the fine print the licences
 * require. The version is the one the site serves, read from the build.
 */
import Link from 'next/link';
import { VERSION } from '../lib/content.generated';
import { CAR_DATA_CREDIT, CAR_DATA_URL, INSTALL, NAV, NO_TRACKING, REPO_URL, SITE_NAME } from '../lib/site';
import { Wordmark } from './Wordmark';
import styles from './Footer.module.css';

export function Footer() {
  return (
    <footer className={styles.footer}>
      <div className={`page ${styles.grid}`}>
        <div className={styles.brand}>
          <Wordmark size={20} />
          <p className={styles.line}>Free dashboards for SimHub, built for iRacing first. MIT.</p>
          <p className={`num ${styles.version}`}>{VERSION}</p>
        </div>

        <nav className={styles.col} aria-label="Footer">
          <p className="label">Pages</p>
          <ul className={styles.list}>
            {NAV.map((item) => (
              <li key={item.href}>
                <Link href={item.href} className={styles.navLink}>
                  {item.label}
                </Link>
              </li>
            ))}
            <li>
              <Link href={INSTALL.href} className={styles.navLink}>
                {INSTALL.label}
              </Link>
            </li>
            <li>
              <a href={REPO_URL} className={styles.navLink} rel="noopener">
                GitHub
              </a>
            </li>
          </ul>
        </nav>

        <div className={styles.col}>
          <p className="label">Fine print</p>
          <p className={styles.fine}>{NO_TRACKING}</p>
          <p className={styles.fine}>
            Barlow and Barlow Condensed by Jeremy Tribby, redistributed under the{' '}
            <a href="/fonts/OFL.txt" className="link">
              SIL Open Font Licence 1.1
            </a>
            .
          </p>
          <p className={styles.fine}>
            {CAR_DATA_CREDIT}{' '}
            <a href={CAR_DATA_URL} className="link" rel="noopener">
              Source
            </a>
            .
          </p>
          <p className={styles.fine}>SimHub is by Wotever. {SITE_NAME} is not affiliated with SimHub, iRacing or any sim.</p>
        </div>
      </div>
    </footer>
  );
}
