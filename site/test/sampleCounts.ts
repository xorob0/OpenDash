/**
 * Counts for the tests, which run from the repository root without the generators: the sentences
 * that take a `Counts` are read with these, and the numbers only have to be plausible.
 */
import type { Counts } from '../lib/counts.ts';

export const SAMPLE_COUNTS: Counts = {
  packages: 14,
  faces: 10,
  companions: 2,
  pitWalls: 2,
  themedPackages: 8,
  themes: 1,
  pages: 21,
  zoneAPages: 4,
  bandDPages: 8,
  barFields: 10,
  pitWallPages: 3,
  pitWallZonePages: 11,
  stripShapes: 62,
  ledProfiles: 158,
  flags: 15,
  glyphs: 69,
};
