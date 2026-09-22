'use client';

/**
 * The header. It sticks to the top and gains its rule only once the page has been scrolled: an
 * unscrolled page has nothing above the header to separate it from, and a rule drawn across the
 * top of a hero is a line through a photograph.
 *
 * The repository is linked here as well as in the footer. A visitor deciding whether this is a
 * real project looks for the source before they look for anything else.
 */
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useEffect, useState } from 'react';
import { INSTALL, NAV, REPO_URL } from '../lib/site';
import { Wordmark } from './Wordmark';
import styles from './Nav.module.css';

export function Nav() {
  const pathname = usePathname();
  const [scrolled, setScrolled] = useState(false);
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  // A route change has to close the menu; the panel is not unmounted by navigation on its own.
  useEffect(() => setOpen(false), [pathname]);

  const current = (href: string) => (pathname === href || pathname.startsWith(`${href}/`) ? 'page' : undefined);

  return (
    <header className={`${styles.header} ${scrolled ? styles.scrolled : ''}`}>
      <div className={`page ${styles.bar}`}>
        <Link href="/" className={styles.home} aria-label="OpenDash, home">
          <Wordmark size={22} />
        </Link>

        <nav className={styles.links} aria-label="Primary">
          {NAV.map((item) => (
            <Link key={item.href} href={item.href} className={styles.link} aria-current={current(item.href)}>
              {item.label}
            </Link>
          ))}
          <a href={REPO_URL} className={styles.link} rel="noopener">
            GitHub
          </a>
        </nav>

        <div className={styles.end}>
          <Link href={INSTALL.href} className={styles.cta} aria-current={current(INSTALL.href)}>
            {INSTALL.label}
          </Link>
          <button
            className={styles.toggle}
            onClick={() => setOpen((v) => !v)}
            aria-expanded={open}
            aria-controls="menu"
            aria-label={open ? 'Close the menu' : 'Open the menu'}
          >
            <span className={styles.bars} data-open={open || undefined} />
          </button>
        </div>
      </div>

      <div id="menu" className={styles.menu} hidden={!open}>
        <div className="page">
          {NAV.map((item) => (
            <Link key={item.href} href={item.href} className={styles.menuLink} aria-current={current(item.href)}>
              {item.label}
            </Link>
          ))}
          <a href={REPO_URL} className={styles.menuLink} rel="noopener">
            GitHub
          </a>
          <Link href={INSTALL.href} className={`${styles.menuLink} ${styles.menuCta}`}>
            {INSTALL.label}
          </Link>
        </div>
      </div>
    </header>
  );
}
