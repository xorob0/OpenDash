/**
 * The helpers that name and order a package.
 *
 * `content.generated.ts` says which packages exist and how big each one is, because that is a fact
 * the build already knows; this file names them and orders them for a reader.
 *
 * Nothing here imports the generated content, so the tests under `test/` can use `slug` and
 * `inReadingOrder` from the repository root without the generators having run. The lists that do
 * depend on the build are in `faces.ts`.
 */
import type { SitePackage } from '../scripts/content';

/** `OpenDash 1280x480` -> `opendash-1280x480`: the name of its capture, its clip and its picker id. */
export const slug = (folder: string): string =>
  folder
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');

/**
 * Reading order, which is not manifest order.
 *
 * The manifest lists packages in the order the build produces them, which puts the superseded card
 * faces first and the two round faces before the reference one. A reader wants the reference face
 * first, then the rectangular faces widest to narrowest, then the round ones, which are a different
 * shape rather than a smaller size, and the second screens last.
 */
const KIND_RANK: Record<SitePackage['kind'], number> = { dash: 0, companion: 1, pitwall: 2 };

export function inReadingOrder<T extends SitePackage>(packages: readonly T[]): T[] {
  return [...packages].sort((a, b) => {
    if (KIND_RANK[a.kind] !== KIND_RANK[b.kind]) return KIND_RANK[a.kind] - KIND_RANK[b.kind];
    // The reference face leads its kind whatever its numbers say.
    if (a.folder === 'OpenDash') return -1;
    if (b.folder === 'OpenDash') return 1;
    if (a.round !== b.round) return a.round ? 1 : -1;
    if (a.width !== b.width) return b.width - a.width;
    return b.height - a.height;
  });
}

/** `1920 × 480`, or `480 round` for a face that is the display itself. */
export const sizeLabel = (p: { width: number; height: number; round: boolean }): string =>
  p.round ? `${p.width} round` : `${p.width} × ${p.height}`;

/** `Face`, `Companion` or `Pit wall`. */
export const kindLabel = (kind: SitePackage['kind']): string => (kind === 'dash' ? 'Face' : kind === 'companion' ? 'Companion' : 'Pit wall');

/** `1.2 MB`, for a download button. */
export function weigh(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${Math.max(1, Math.round(bytes / 1024))} KB`;
}
