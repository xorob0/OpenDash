import type { MetadataRoute } from 'next';
import { NAV, SITE_URL } from '../lib/site';

/**
 * The sitemap. Every page is static and there are seven of them, so it is the nav plus the two
 * pages the nav does not carry.
 *
 * With NEXT_PUBLIC_SITE_URL unset there is no origin to write absolute URLs against, and a sitemap
 * of relative URLs is not a sitemap. So it comes back empty rather than wrong, which is the same
 * choice the metadata makes.
 */
export default function sitemap(): MetadataRoute.Sitemap {
  if (!SITE_URL) return [];
  const paths = ['/', ...NAV.map((n) => n.href), '/download'];
  return paths.map((path) => ({
    url: `${SITE_URL}${path}`,
    lastModified: new Date(),
    changeFrequency: path === '/download' ? ('weekly' as const) : ('monthly' as const),
    priority: path === '/' ? 1 : 0.7,
  }));
}
