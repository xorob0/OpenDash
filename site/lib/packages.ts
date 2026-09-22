/**
 * The editorial layer over the generated package list, and the helpers that name a package.
 *
 * `content.generated.ts` says which packages exist and how big each one is, because that is a fact
 * the build already knows. What a size is *for* is not in any build output. It is the sentence a
 * reader needs in order to pick one, so it lives here, keyed by folder. A package with no note
 * still renders: the note is a help, not a requirement.
 *
 * Nothing here imports the generated content, so the tests under `test/` can use `slug` and
 * `inReadingOrder` from the repository root without the generators having run. The lists that do
 * depend on the build are in `faces.ts`.
 */
import type { SitePackage } from '../scripts/content';

export interface PackageNote {
  /** What it is, in a sentence or two. */
  what: string;
  /** Set on the two sizes a first-time reader should be steered towards. */
  emphasis?: 'base' | 'large';
}

export const NOTES: Record<string, PackageNote> = {
  OpenDash: { what: 'The reference face, 1920 px wide. Every other size is this layout, redrawn.' },
  'OpenDash 1280x480': { what: 'A wide DDU. Driver rows carry the car number and the class.', emphasis: 'large' },
  'OpenDash 1280x400': { what: 'The wide DDU with a shorter body.' },
  'OpenDash 1280x720': { what: 'A tall body. Zone C lists 18 drivers.' },
  'OpenDash 850x480': { what: 'The common wheel DDU. All 5 parts.', emphasis: 'base' },
  'OpenDash 800x480': { what: 'The 850 face, 25 px narrower on each side.' },
  'OpenDash 800x286': { what: 'The nano. No bar, the fuel page, no driver list.' },
  'OpenDash 600x686': { what: 'Portrait. Zone A above B above C.' },
  'OpenDash 480 round': { what: 'A round or square 480 DDU. Still the old 12-slot design.' },
  'OpenDash 800 round': { what: 'The larger round DDU. Still the old 12-slot design.' },
  'OpenDash Companion': { what: 'A phone or tablet beside the wheel. 1 page at a time.' },
  'OpenDash Companion portrait': { what: 'The same, for a phone stood on end.' },
  'OpenDash Pit wall': { what: '3 pages for whoever is not driving.' },
  'OpenDash Pit wall portrait': { what: 'The same in 1 page, for a screen on its side.' },
};

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
