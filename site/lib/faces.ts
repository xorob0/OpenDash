/**
 * The packages as the pages list them: in reading order, with their capture name.
 *
 * This is the one place the generated package list and the editorial notes meet. The picker, the
 * size list and the download page all read from here, so a package cannot be listed on one page
 * and missing from another. None of them carries a file to download: the plugin is the only way in
 * (#438), and the one download is its zip.
 */
import { PACKAGES, THEMED_PACKAGES, THEMES } from './content.generated';
import { hasCapture, packageFile, stillFor } from './captures';
import { clipFor } from './clips';
import { inReadingOrder, slug } from './packages';
import type { SitePackage } from '../scripts/content';

export interface PackageOption extends SitePackage {
  slug: string;
}

const option = (p: SitePackage): PackageOption => ({ ...p, slug: slug(p.folder) });

export const ALL: readonly PackageOption[] = inReadingOrder(PACKAGES).map(option);
export const FACES: readonly PackageOption[] = ALL.filter((p) => p.kind === 'dash');
export const SECOND_SCREENS: readonly PackageOption[] = ALL.filter((p) => p.kind !== 'dash');
export const COMPANIONS: readonly PackageOption[] = ALL.filter((p) => p.kind === 'companion');
export const PIT_WALLS: readonly PackageOption[] = ALL.filter((p) => p.kind === 'pitwall');

/** The 850 x 480 face: the common wheel DDU, the one the picker opens on and the hero shows. */
export const BASE_FACE: PackageOption | undefined = FACES.find((f) => f.folder === 'OpenDash 850x480');
/** The 1280 x 480 face, the wide DDU. */
export const LARGE_FACE: PackageOption | undefined = FACES.find((f) => f.folder === 'OpenDash 1280x480');

export const byFolder = (folder: string): PackageOption | undefined => ALL.find((p) => p.folder === folder);

/** The themed packages, in reading order, each with its capture name: `opendash-porsche-1280x480`. */
export const THEMED: readonly PackageOption[] = inReadingOrder(THEMED_PACKAGES).map(option);

/** A theme the site shows: the catalogue's entry with its packages. The default is not one. */
export interface CarTheme {
  id: string;
  name: string;
  cars: readonly string[];
  bandPages: readonly { id: string; name: string }[];
  packages: readonly PackageOption[];
}

export const CAR_THEMES: readonly CarTheme[] = THEMES.filter((t) => t.id !== 'default').map((t) => ({
  id: t.id,
  name: t.name,
  cars: t.cars,
  bandPages: t.bandPages,
  packages: THEMED.filter((p) => p.theme === t.id),
}));

export const themeById = (id: string): CarTheme | undefined => CAR_THEMES.find((t) => t.id === id);

/** The themed package at a size, for the page that shows a theme beside the house face of the same size. */
export const themedAt = (theme: CarTheme, size: { width: number; height: number }): PackageOption | undefined =>
  theme.packages.find((p) => p.width === size.width && p.height === size.height);

/** The faces as the picker draws them: with their capture's URL when it has been photographed. */
export interface PickerFace {
  folder: string;
  slug: string;
  width: number;
  height: number;
  round: boolean;
  /** The picture the cell shows: the clip's own first frame where there is a clip. */
  capture: string | null;
  /** What plays when the cell is chosen, where this size has been filmed. */
  clip: { webm: string; mp4: string } | null;
}

export const pickerFaces = (): PickerFace[] =>
  FACES.map((f) => {
    const clip = clipFor(f.folder);
    return {
      folder: f.folder,
      slug: f.slug,
      width: f.width,
      height: f.height,
      round: f.round,
      // The clip's poster is the clip's first frame, so a cell that starts playing does not jump.
      capture: clip?.poster ?? (hasCapture(packageFile(f.folder)) ? stillFor(f.folder) : null),
      clip: clip ? { webm: clip.webm, mp4: clip.mp4 } : null,
    };
  });
