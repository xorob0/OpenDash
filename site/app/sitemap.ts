import type { MetadataRoute } from 'next';
import { ROUTE_LIST } from '../lib/routes';
import { SITE_URL } from '../lib/site';

/**
 * The sitemap, read from the same route list the link test checks.
 *
 * With NEXT_PUBLIC_SITE_URL unset there is no origin to write absolute URLs against, and a sitemap
 * of relative URLs is not a sitemap. So it comes back empty rather than wrong, which is the same
 * choice the metadata makes.
 */
export default function sitemap(): MetadataRoute.Sitemap {
  if (!SITE_URL) return [];
  return ROUTE_LIST.map((r) => ({
    url: `${SITE_URL}${r.path}`,
    lastModified: new Date(),
    changeFrequency: r.changeFrequency,
    priority: r.path === '/' ? 1 : 0.7,
  }));
}
