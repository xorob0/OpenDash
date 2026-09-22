'use client';

/**
 * The header. It sits at the top of the page and sticks, and it gains its rule only once the page
 * has been scrolled — an unscrolled page has nothing above the header to separate it from, and a
 * rule drawn across the top of a hero is a line through a photograph.
 */
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useEffect, useState } from 'react';
import { NAV } from '../lib/site';
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

  return (
    <header className={`${styles.header} ${scrolled ? styles.scrolled : ''}`}>
      <div className={`page ${styles.bar}`}>
        <Link href="/" className={styles.home} aria-label="OpenDash, home">
          <Wordmark size={22} />
        </Link>

        <nav className={styles.links} aria-label="Primary">
          {NAV.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={styles.link}
              aria-current={pathname.startsWith(item.href) ? 'page' : undefined}
            >
              {item.label}
            </Link>
          ))}
        </nav>

        <div className={styles.end}>
          <Link href="/download" className={styles.cta}>
            Download
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

      <div id="menu" className={styles.menu} data-open={open || undefined} hidden={!open}>
        <div className="page">
          {NAV.map((item) => (
            <Link key={item.href} href={item.href} className={styles.menuLink}>
              {item.label}
            </Link>
          ))}
          <Link href="/download" className={styles.menuLink}>
            Download
          </Link>
        </div>
      </div>
    </header>
  );
}
