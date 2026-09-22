/**
 * The facts about the site itself, and the sentences every page reuses.
 *
 * The canonical origin is read from the environment and has no default. The site is deployed to a
 * host this repository does not name, so a hard-coded fallback would only ever be wrong: with
 * NEXT_PUBLIC_SITE_URL unset the metadata simply carries no absolute URL, which degrades to
 * relative links rather than to somebody else's domain.
 *
 * The sentences live here so that the promise reads the same on every page that makes it. A page
 * imports the sentence rather than retyping it, and a test checks that the pages which have to
 * make the promise do. This file must not import `content.generated.ts`: the tests under `test/`
 * run from the repository root without the generators, and they read these sentences too.
 */
export const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL?.replace(/\/$/, '') ?? '';

/** The product, in prose. */
export const SITE_NAME = 'OpenDash';
/** The product, as the mark spells it. */
export const WORDMARK = 'openDash';
export const SITE_TAGLINE = 'Free SimHub dashboards for iRacing.';

export const REPO_URL = 'https://github.com/xorob0/OpenDash';
export const issueUrl = (n: number): string => `${REPO_URL}/issues/${n}`;
export const SCOPE_URL = `${REPO_URL}/blob/main/docs/scope.md`;
/** The one link beyond the repository: CC BY attribution links its source. */
export const CAR_DATA_URL = 'https://github.com/Lovely-Sim-Racing/lovely-car-data';
/** Linked once, on the install page, because a reader without SimHub has to get it first. */
export const SIMHUB_URL = 'https://www.simhubdash.com/';

/** The promise. The home, compare and download pages have to carry FREE_FOREVER, and a test says so. */
export const FREE_HEADLINE = 'Free, forever.';
export const FREE_FOREVER = 'OpenDash is free and always will be.';
export const NOTHING_TO_UNLOCK = 'No licence, no account, nothing to unlock.';

/** The claim about sims, which docs/scope.md limits to iRacing until #102 says otherwise. */
export const CLAIMED_SIM = 'Tested on iRacing only. Other sims may work and are not claimed.';

export const NO_TRACKING = 'No analytics, no cookies. This site records nothing about you.';

/**
 * The attribution the car light data carries. It is required wherever the lights are described,
 * on the site as in the plugin's Lights tab, because the data is CC BY-NC-SA 4.0 and a site is
 * marketing material.
 */
export const CAR_DATA_CREDIT =
  'Car light data: Lovely Car Data, by Lovely Sim Racing, ATSR and Gomez Sim Industries, CC BY-NC-SA 4.0. Fetched by the plugin, never bundled.';

/** The three things neither competitor offers, stated on the first screen. */
export const DIFFERENTIATORS = [
  {
    id: 'source',
    title: 'Generated from source, under MIT.',
    body: 'Every dashboard is built from TypeScript and design tokens. A new size or feature is a pull request, not a paid pack.',
  },
  {
    id: 'lights',
    title: 'Your car’s own shift lights.',
    body: 'Colours, order and per-gear thresholds from open data, on any LED strip.',
  },
  {
    id: 'plugin',
    title: 'Works without the plugin.',
    body: 'Every dashboard carries its own defaults. Double-click 1 file and drive. The plugin adds settings and updates.',
  },
] as const;

/** The top-level pages, in the order the nav lists them. Download is the call to action, not a nav item. */
export const NAV = [
  { href: '/screens', label: 'Screens' },
  { href: '/pages', label: 'Pages' },
  { href: '/lights', label: 'Lights' },
  { href: '/compare', label: 'Compare' },
  { href: '/install', label: 'Install' },
] as const;

export const DOWNLOAD = { href: '/download', label: 'Download' } as const;
