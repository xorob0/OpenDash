import Link from 'next/link';
import { INSTALL, NAV } from '../lib/site';

export default function NotFound() {
  return (
    <section className="section">
      <div className="page stack" style={{ gap: 'var(--space-6)' }}>
        <p className="label">404</p>
        <h1 className="h1">No page here.</h1>
        <p className="prose">The pages that exist:</p>
        <ul className="rows" style={{ maxWidth: '24rem' }}>
          <li>
            <Link href="/" className="link">
              Home
            </Link>
          </li>
          {NAV.map((item) => (
            <li key={item.href}>
              <Link href={item.href} className="link">
                {item.label}
              </Link>
            </li>
          ))}
          <li>
            <Link href={INSTALL.href} className="link">
              {INSTALL.label}
            </Link>
          </li>
        </ul>
      </div>
    </section>
  );
}
