/**
 * The themes the build knows, by id. A theme is a build-time variant: the process is started for
 * one theme, `tokens.ts` resolves its overlay before `ds` is built, and every package that process
 * composes is drawn in it. There is no switching inside a process, because forty-odd modules bind
 * colours from `ds` at import time; a build of several themes is one process per theme.
 */
import type { Overlay } from '../tokens.ts';
import defaultOverlay from './default/overlay.json';

/** The environment variable naming the theme a process builds. Unset or empty is the default theme. */
export const THEME_ENV = 'OPENDASH_THEME';

export const DEFAULT_THEME_ID = 'default';

export interface Theme {
  /** What the theme changes in design/tokens.json. See `applyOverlay` for how it is read. */
  readonly overlay: Overlay;
}

export const THEMES: Record<string, Theme> = {
  /** The look `main` has always built: an overlay that changes nothing. */
  [DEFAULT_THEME_ID]: { overlay: defaultOverlay },
};

/** The theme {@link THEME_ENV} names, refusing one that is not registered rather than falling back. */
export function selectedThemeId(env: Record<string, string | undefined> = process.env): string {
  const id = env[THEME_ENV]?.trim() || DEFAULT_THEME_ID;
  if (!Object.hasOwn(THEMES, id)) throw new Error(`${THEME_ENV}=${JSON.stringify(id)} is not a theme; expected one of ${Object.keys(THEMES).join(', ')}`);
  return id;
}
