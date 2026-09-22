/**
 * The handful of facts about the site itself, as opposed to about the product.
 *
 * The canonical origin is read from the environment and has no default. The site is deployed to a
 * host this repository does not name, so a hard-coded fallback would only ever be wrong: with
 * NEXT_PUBLIC_SITE_URL unset the metadata simply carries no absolute URL, which degrades to
 * relative links rather than to somebody else's domain.
 */
export const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL?.replace(/\/$/, '') ?? '';

export const SITE_NAME = 'OpenDash';
export const SITE_TAGLINE = 'An open-source sim racing dashboard for SimHub.';

/** The top-level pages, in the order the nav lists them. */
export const NAV = [
  { href: '/dashes', label: 'Dashes' },
  { href: '/modules', label: 'Modules' },
  { href: '/second-screens', label: 'Second screens' },
  { href: '/flag-box', label: 'Flag box' },
  { href: '/install', label: 'Install' },
] as const;
