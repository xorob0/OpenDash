/**
 * The packages as the pages list them: in reading order, with their note, their capture name and
 * the download the build produced for them.
 *
 * This is the one place the generated package list, the generated download list and the editorial
 * notes meet. The picker, the size list and the download page all read from here, so a package
 * cannot be listed on one page and missing from another.
 */
import { DOWNLOADS, PACKAGES } from './content.generated';
import { hasCapture, packageFile, stillFor } from './captures';
import { NOTES, inReadingOrder, slug, type PackageNote } from './packages';
import type { SitePackage } from '../scripts/content';

export interface PackageOption extends SitePackage {
  slug: string;
  /** What the built .simhubdash weighs, or undefined when this build did not produce one. */
  bytes?: number;
  note?: PackageNote;
}

const option = (p: SitePackage): PackageOption => ({
  ...p,
  slug: slug(p.folder),
  bytes: DOWNLOADS.find((d) => d.file === p.file)?.bytes,
  note: NOTES[p.folder],
});

export const ALL: readonly PackageOption[] = inReadingOrder(PACKAGES).map(option);
export const FACES: readonly PackageOption[] = ALL.filter((p) => p.kind === 'dash');
export const SECOND_SCREENS: readonly PackageOption[] = ALL.filter((p) => p.kind !== 'dash');
export const COMPANIONS: readonly PackageOption[] = ALL.filter((p) => p.kind === 'companion');
export const PIT_WALLS: readonly PackageOption[] = ALL.filter((p) => p.kind === 'pitwall');

/** The base size, 850 x 480, which is what the picker opens on and what a reader should try first. */
export const BASE_FACE: PackageOption | undefined = FACES.find((f) => f.note?.emphasis === 'base');
/** The large size, 1280 x 480. */
export const LARGE_FACE: PackageOption | undefined = FACES.find((f) => f.note?.emphasis === 'large');

export const byFolder = (folder: string): PackageOption | undefined => ALL.find((p) => p.folder === folder);

/** The faces as the picker draws them: with their capture's URL when it has been photographed. */
export interface PickerFace {
  folder: string;
  slug: string;
  width: number;
  height: number;
  round: boolean;
  file: string;
  bytes?: number;
  what?: string;
  emphasis?: 'base' | 'large';
  capture: string | null;
}

export const pickerFaces = (): PickerFace[] =>
  FACES.map((f) => ({
    folder: f.folder,
    slug: f.slug,
    width: f.width,
    height: f.height,
    round: f.round,
    file: f.file,
    bytes: f.bytes,
    what: f.note?.what,
    emphasis: f.note?.emphasis,
    capture: hasCapture(packageFile(f.folder)) ? stillFor(f.folder) : null,
  }));
