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
export const SITE_TAGLINE = 'Free dashboards for SimHub, built for iRacing first.';

export const REPO_URL = 'https://github.com/xorob0/OpenDash';
/** The repository as the footer prints it, so the address is written once. */
export const REPO_LABEL = REPO_URL.replace(/^https:\/\//, '');
export const issueUrl = (n: number): string => `${REPO_URL}/issues/${n}`;
export const SCOPE_URL = `${REPO_URL}/blob/main/docs/scope.md`;
/** The one link beyond the repository: CC BY attribution links its source. */
export const CAR_DATA_URL = 'https://github.com/Lovely-Sim-Racing/lovely-car-data';
/** Linked once, on the install page, because a reader without SimHub has to get it first. */
export const SIMHUB_URL = 'https://www.simhubdash.com/';

/**
 * The promise. The home, compare and download pages have to carry FREE_FOREVER, and a test says so.
 *
 * It is a promise rather than a price: `docs/scope.md` refuses licensing, activation and accounts,
 * so "always will be" is a standing refusal and not a plan.
 */
export const FREE_HEADLINE = 'Free, forever.';
export const FREE_FOREVER = 'OpenDash is 100% free and always will be.';
export const NOTHING_TO_UNLOCK = 'No licence, no account, nothing to unlock.';

/**
 * The one file a user is offered. docs/scope.md makes the plugin the only way in (#438): a release
 * publishes this zip and nothing else a user could import, and this site serves the same.
 */
export const PLUGIN_ZIP = 'OpenDash-plugin.zip';

/** The distribution line, which the install and download pages both have to make, and a test says so. */
export const ONLY_WAY_IN = 'The plugin is the only way in: every dashboard and every LED profile reaches SimHub through it.';

/** What the line costs, said where it is made rather than discovered by the reader it excludes. */
export const NO_OTHER_ROUTE = 'A SimHub that cannot take a DLL into its own folder cannot run OpenDash. That is a choice, and the scope says what it buys.';

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

/**
 * The three reasons, on the first screen and nowhere else. One sentence each, because they are
 * read standing up: the thing neither competitor can offer, then what it gets you.
 */
export const DIFFERENTIATORS = [
  {
    id: 'free',
    title: 'Free and open source, under MIT',
    body: 'No tier, no key, no limit on how many machines: the dashboards are generated from source on GitHub, so a new size or a new field is a pull request and not a paid pack.',
  },
  {
    id: 'lights',
    title: 'LEDs and matrix panels',
    body: 'Shift lights in your car’s own colours and order, from an open table, on any of 62 strip shapes, with the flags and the spotter down the sides and an 8 × 8 flag box.',
  },
  {
    id: 'design',
    title: 'Modern design and features',
    body: 'A dashboard drawn for each screen, 21 pages on a wheel button, a phone companion and a pit wall, every one of them set up from one page in SimHub.',
  },
] as const;

/** The top-level pages, in the order the nav lists them. Download is the call to action, not a nav item. */
export const NAV = [
  { href: '/screens', label: 'Screens' },
  { href: '/pages', label: 'Pages' },
  { href: '/lights', label: 'Lights' },
  { href: '/compare', label: 'Compare' },
  { href: '/download', label: 'Download' },
] as const;

/** The call to action: the plugin is the way in, and the install page is where it starts. */
export const INSTALL = { href: '/install', label: 'Install' } as const;
export const DOWNLOAD = { href: '/download', label: 'Download' } as const;
