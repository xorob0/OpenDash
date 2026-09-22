/**
 * Every path and anchor the site has, in one place.
 *
 * The sitemap is generated from this list, and `test/links.test.ts` checks that every internal
 * link in a page or component points at a path and an anchor listed here. A link to a section that
 * was renamed therefore fails a test rather than scrolling to the top of the page in silence.
 */
export interface Route {
  path: string;
  /** The `id`s a page declares, which is what `#anchor` links may name. */
  anchors: readonly string[];
  changeFrequency: 'weekly' | 'monthly';
}

export const ROUTES = {
  home: { path: '/', anchors: ['why', 'screen', 'pages', 'second-screens', 'lights', 'status', 'get'], changeFrequency: 'weekly' },
  screens: { path: '/screens', anchors: ['faces', 'anatomy', 'fit', 'companion', 'pit-wall', 'round', 'not-listed'], changeFrequency: 'monthly' },
  pages: { path: '/pages', anchors: ['catalogue', 'face', 'glance'], changeFrequency: 'monthly' },
  lights: { path: '/lights', anchors: ['car', 'strips', 'strip-shows', 'flag-box', 'tab'], changeFrequency: 'monthly' },
  compare: { path: '/compare', anchors: ['table', 'scheduled'], changeFrequency: 'monthly' },
  install: { path: '/install', anchors: ['plugin', 'after', 'nothing-showing', 'manual'], changeFrequency: 'monthly' },
  download: { path: '/download', anchors: ['plugin', 'packages', 'source', 'releases'], changeFrequency: 'weekly' },
} as const satisfies Record<string, Route>;

export const ROUTE_LIST: readonly Route[] = Object.values(ROUTES);

/** The paths the first site had, so that a link somebody kept still lands somewhere. */
export const REDIRECTS = [
  { source: '/dashes', destination: '/screens' },
  { source: '/second-screens', destination: '/screens#companion' },
  { source: '/modules', destination: '/pages' },
  { source: '/flag-box', destination: '/lights#flag-box' },
] as const;

/** Static files a page may link to besides its routes. */
export const STATIC_PREFIXES = ['/downloads/', '/shots/', '/clips/', '/fonts/', '/flag-box.svg', '/icon.svg'] as const;

export const routeFor = (path: string): Route | undefined => ROUTE_LIST.find((r) => r.path === path);

/** Whether `href` is a path the site serves, with an anchor the page declares if it carries one. */
export function isInternalLink(href: string): boolean {
  if (STATIC_PREFIXES.some((p) => href.startsWith(p))) return true;
  const [path, anchor] = href.split('#');
  const route = routeFor(path === '' ? '/' : path);
  if (!route) return false;
  return anchor === undefined || route.anchors.includes(anchor);
}
