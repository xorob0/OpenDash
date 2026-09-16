import type { MetadataRoute } from 'next';
import { SITE_URL } from '../lib/site';

/** Everything here is meant to be found. The only conditional part is the sitemap's absolute URL. */
export default function robots(): MetadataRoute.Robots {
  return {
    rules: [{ userAgent: '*', allow: '/' }],
    ...(SITE_URL ? { sitemap: `${SITE_URL}/sitemap.xml`, host: SITE_URL } : {}),
  };
}
