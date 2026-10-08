/**
 * The numbers the copy says, read from the build rather than typed.
 *
 * The site's rule is that no fact the repository already holds is restated in a page, and a count
 * is the fact most easily retyped: "14 dashboards" was true for a month and then the Porsche
 * packages arrived, "122 LED profiles" was true for a week. A sentence that needs a number takes it
 * from a `Counts`, which `liveCounts.ts` fills from `content.generated.ts`, and `test/copy.test.ts`
 * refuses a digit followed by one of these nouns anywhere in the site's source (#560).
 *
 * This file imports nothing generated, so the sentences in `site.ts` and `compare.ts` that take a
 * `Counts` can be read by the tests from the repository root without the generators having run.
 */
export interface Counts {
  /** The packages the plugin offers in the house look: faces, companions and pit walls. */
  packages: number;
  faces: number;
  companions: number;
  pitWalls: number;
  /** The themed packages, one per theme per size it claims. */
  themedPackages: number;
  /** The themes beside the default, as the catalogue lists them. */
  themes: number;
  /** The page catalogue zones B and C, the companion and the pit wall zones draw from. */
  pages: number;
  zoneAPages: number;
  /** Band D's house pages; a theme may add its own after them. */
  bandDPages: number;
  barFields: number;
  /** The landscape pit wall's pages; the portrait one is a package of its own with one page. */
  pitWallPages: number;
  pitWallZonePages: number;
  stripShapes: number;
  ledProfiles: number;
  flags: number;
  /** The flag box's glyphs. */
  glyphs: number;
}

/** Every package, house and themed: what "dashboards" means on the download page. */
export const dashboards = (c: Counts): number => c.packages + c.themedPackages;

/**
 * `14 dashboards`, or `every dashboard` while the count is unknown. A count read from the build is
 * zero when the repository has not been built, which is the ordinary state of a checkout that
 * only typechecks; the sentence then says what is true rather than printing a zero.
 */
export function counted(n: number, noun: string, plural = `${noun}s`): string {
  return n > 0 ? `${n} ${n === 1 ? noun : plural}` : `every ${noun}`;
}
