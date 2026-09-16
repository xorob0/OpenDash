import Link from 'next/link';
import { NAV } from '../lib/site';

export default function NotFound() {
  return (
    <section className="section page" style={{ paddingBlock: '8rem 6rem' }}>
      <p className="label" style={{ marginBottom: 'var(--space-4)' }}>
        404
      </p>
      <h1 className="h1" style={{ marginBottom: 'var(--space-5)' }}>
        No page here.
      </h1>
      <p className="prose" style={{ marginBottom: 'var(--space-6)' }}>
        Whatever this link pointed at is not part of the site. The pages that are:
      </p>
      <ul style={{ display: 'flex', flexWrap: 'wrap', gap: 'var(--space-5)' }}>
        {[{ href: '/', label: 'Home' }, ...NAV, { href: '/download', label: 'Download' }].map((item) => (
          <li key={item.href}>
            <Link href={item.href} className="link">
              {item.label}
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
