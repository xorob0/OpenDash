/**
 * The counts of the build being served, for the pages. Tests read `counts.ts`, not this.
 */
import {
  BAND_D_PAGES,
  BAR_FIELDS,
  FLAGS,
  GLYPH_COUNT,
  LED_PROFILE_COUNT,
  MODULES,
  PACKAGES,
  PIT_WALL_PAGES,
  PIT_WALL_ZONE_PAGES,
  STRIP_SHAPES,
  THEMED_PACKAGES,
  THEMES,
  ZONE_A_PAGES,
} from './content.generated';
import type { Counts } from './counts';

export const COUNTS: Counts = {
  packages: PACKAGES.length,
  faces: PACKAGES.filter((p) => p.kind === 'dash').length,
  companions: PACKAGES.filter((p) => p.kind === 'companion').length,
  pitWalls: PACKAGES.filter((p) => p.kind === 'pitwall').length,
  themedPackages: THEMED_PACKAGES.length,
  themes: THEMES.filter((t) => t.id !== 'default').length,
  pages: MODULES.length,
  zoneAPages: ZONE_A_PAGES.length,
  bandDPages: BAND_D_PAGES.length,
  barFields: BAR_FIELDS.length,
  // The portrait pit wall is a package of its own with one page; the count is the landscape package's.
  pitWallPages: PIT_WALL_PAGES.filter((p) => p.landscape).length,
  pitWallZonePages: PIT_WALL_ZONE_PAGES.length,
  stripShapes: STRIP_SHAPES.length,
  ledProfiles: LED_PROFILE_COUNT,
  flags: FLAGS.length,
  glyphs: GLYPH_COUNT,
};
