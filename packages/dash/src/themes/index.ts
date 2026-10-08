/**
 * The code of every theme, by id; what the build makes of each is the catalogue's, `THEME_CATALOGUE`
 * in `contract.ts`. A theme is a build-time variant: the process is started for
 * one theme, `tokens.ts` resolves its overlay before `ds` is built, and every package that process
 * composes is drawn in it. There is no switching inside a process, because forty-odd modules bind
 * colours from `ds` at import time; a build of several themes is one process per theme.
 */
import { DEFAULT_THEME_ID } from '../contract.ts';
import type { Overlay } from '../tokens.ts';
import type { Anatomy } from './anatomy.ts';
import { defaultAnatomy } from './default/anatomy.ts';
import defaultOverlay from './default/overlay.json';
import { aimAnatomy } from './aim/anatomy.ts';
import aimOverlay from './aim/overlay.json';
import { porscheAnatomy } from './porsche/anatomy.ts';
import porscheOverlay from './porsche/overlay.json';

/** The environment variable naming the theme a process builds. Unset or empty is the default theme. */
export const THEME_ENV = 'OPENDASH_THEME';

export { DEFAULT_THEME_ID };

export interface Theme {
  /** What the theme changes in design/tokens.json. See `applyOverlay` for how it is read. */
  readonly overlay: Overlay;
  /** Where the theme puts each part of a face, at every size it draws. See `themes/anatomy.ts`. */
  readonly anatomy: Anatomy;
}

export const THEMES: Record<string, Theme> = {
  /** The look `main` has always built: an overlay that changes nothing, over the zone anatomy. */
  [DEFAULT_THEME_ID]: { overlay: defaultOverlay, anatomy: defaultAnatomy },
  /** The 992 display of the GT3 R and both Cups, at 1280 x 480 (#205). Its drawing is in `drawings.ts`. */
  porsche: { overlay: porscheOverlay, anatomy: porscheAnatomy },
  /** The AiM LCD of the MX-5 Cup, the Legends and the Cross Car, at every size (#204). */
  aim: { overlay: aimOverlay, anatomy: aimAnatomy },
};

/** The theme {@link THEME_ENV} names, refusing one that is not registered rather than falling back. */
export function selectedThemeId(env: Record<string, string | undefined> = process.env): string {
  const id = env[THEME_ENV]?.trim() || DEFAULT_THEME_ID;
  if (!Object.hasOwn(THEMES, id)) throw new Error(`${THEME_ENV}=${JSON.stringify(id)} is not a theme; expected one of ${Object.keys(THEMES).join(', ')}`);
  return id;
}
