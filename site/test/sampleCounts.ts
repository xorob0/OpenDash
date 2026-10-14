/**
 * Counts and lists for the tests, which run from the repository root without the generators: the
 * sentences that take a `Counts` or a `Listed` are read with these, and they only have to be
 * plausible and agree with each other. The sizes are not quite the build's, so that a list typed by
 * hand in a cell fails.
 */
import type { Listed, Screen } from '../lib/compare.ts';
import type { Counts } from '../lib/counts.ts';

export const SAMPLE_COUNTS: Counts = {
  packages: 14,
  faces: 10,
  companions: 2,
  pitWalls: 2,
  themedPackages: 16,
  themes: 2,
  pages: 21,
  zoneAPages: 4,
  bandDPages: 7,
  barFields: 10,
  pitWallPages: 3,
  pitWallZonePages: 11,
  stripShapes: 62,
  ledProfiles: 158,
  flags: 15,
  glyphs: 69,
};

const screen = (width: number, height: number, round = false): Screen => ({ width, height, round });

/** Eight rectangular sizes, which is what each sample theme claims. */
const RECTANGULAR = [
  screen(1920, 480),
  screen(1024, 600),
  screen(1280, 480),
  screen(1280, 400),
  screen(850, 480),
  screen(800, 480),
  screen(800, 286),
  screen(600, 686),
];

export const SAMPLE_LISTED: Listed = {
  faces: [...RECTANGULAR, screen(800, 800, true), screen(480, 480, true)],
  companions: [screen(850, 480), screen(480, 850)],
  pitWalls: [screen(1920, 1080), screen(1080, 1920)],
  themes: [
    { name: 'Porsche', cars: ['Porsche 911 GT3 R (992)', 'Porsche 911 GT3 Cup (992.2)', 'Porsche 911 GT3 Cup (992.1)', 'Porsche 911 GT3 R (991.2)'], sizes: RECTANGULAR },
    { name: 'AiM', cars: ['Global Mazda MX-5 Cup', 'Legends Ford Coupe', 'FIA Cross Car'], sizes: RECTANGULAR },
  ],
};
