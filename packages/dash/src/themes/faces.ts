/**
 * A theme's faces, built: the one function that turns a theme's anatomy into the dashboards of a
 * face, at every size the theme claims, so that the conformance harness (#200) and the build of
 * every theme (#201) ask the same question in the same way.
 *
 * Kept apart from `themes/index.ts` because it builds faces, which needs `ds`, whereas the registry
 * is read before `ds` exists.
 */
import type { Size } from '../design/geometry.ts';
import { THEME_ID } from '../tokens.ts';
import { ZONE_FACES, buildZoneFace, type BuiltFace, type FaceBuildOptions, type ZoneLayout } from '../zones/index.ts';
import { checkRegions, type Regions } from './anatomy.ts';
import { THEME_ENV, THEMES, type Theme } from './index.ts';

export interface ThemeFace {
  /** The house face of this size, which carries what the theme does not change: the background, the rev bar's gap, the bar's scale. */
  layout: ZoneLayout;
  /** Where the theme puts each part, with the rev bar on. The other arrangement is derived from them. */
  regions: Regions;
  built: BuiltFace;
}

const named = (size: Size): string => `${size.width}x${size.height}`;

/**
 * Whether this process draws the theme in its own colours: it is the process's theme, or its
 * overlay is the process's, as a test theme with an empty one is in a default process.
 */
export const drawsInThisProcess = (themeId: string): boolean =>
  themeId === THEME_ID || JSON.stringify(THEMES[themeId]?.overlay) === JSON.stringify(THEMES[THEME_ID]!.overlay);

/**
 * The theme, refusing one whose colours are not this process's.
 *
 * A theme's colours are fixed when `ds` is built, once per process, whereas its anatomy is read
 * here per call; a theme with an overlay other than the process's would therefore be drawn in its
 * own places and someone else's colours, silently. A theme whose overlay is the process's, as a
 * test theme with an empty one is in a default process, builds in place.
 */
function themeFor(themeId: string): Theme {
  const theme = THEMES[themeId];
  if (!theme) throw new Error(`${JSON.stringify(themeId)} is not a theme; expected one of ${Object.keys(THEMES).join(', ')}`);
  if (!drawsInThisProcess(themeId)) {
    throw new Error(`the ${themeId} theme has colours of its own and this process draws ${THEME_ID}'s; start it with ${THEME_ENV}=${themeId}`);
  }
  return theme;
}

/** One face of a theme, refusing a size the theme does not claim and regions the face cannot draw. */
export function buildThemeFace(themeId: string, size: Size, opts: FaceBuildOptions): ThemeFace {
  const { anatomy } = themeFor(themeId);
  if (!anatomy.sizes.some((s) => s.width === size.width && s.height === size.height)) {
    throw new Error(`the ${themeId} theme does not draw ${named(size)}; it draws ${anatomy.sizes.map(named).join(', ')}`);
  }
  const layout = ZONE_FACES.find((f) => f.width === size.width && f.height === size.height);
  if (!layout) throw new Error(`the ${themeId} theme claims ${named(size)}, which is not a face that ships`);
  const regions = anatomy.regions(layout);
  checkRegions(regions, size, `the ${themeId} theme at ${named(size)}`);
  return { layout, regions, built: buildZoneFace(layout, opts, regions) };
}

/** Every face of a theme, one per size it claims, in the order it claims them. */
export const buildThemeFaces = (themeId: string, opts: FaceBuildOptions): ThemeFace[] =>
  themeFor(themeId).anatomy.sizes.map((size) => buildThemeFace(themeId, size, opts));
