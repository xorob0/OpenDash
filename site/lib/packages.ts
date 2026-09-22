/**
 * The editorial layer over the generated package list.
 *
 * `content.generated.ts` says which packages exist and how big each one is, because that is a fact
 * the build already knows. What a size is *for* is not in any build output — it is the sentence a
 * reader needs in order to pick one, and it comes from the install documentation and from having
 * looked at the screens. So it lives here, keyed by folder, and a package with no note still
 * renders: the note is a help, not a requirement.
 */
import { PACKAGES } from './content.generated';
import type { SitePackage } from '../scripts/content';

export interface PackageNote {
  /** What it is, in a few words. Sentence case, no full stop; the table puts it in a cell. */
  what: string;
  /** Set on the two sizes a first-time reader should be steered towards. */
  emphasis?: 'base' | 'large';
}

export const NOTES: Record<string, PackageNote> = {
  OpenDash: { what: 'The reference face. Every other size is this anatomy, redrawn' },
  'OpenDash 1280x480': { what: 'A wider DDU. The zones carry the car number and class beside every driver', emphasis: 'large' },
  'OpenDash 1280x400': { what: 'A shorter body, the same zones' },
  'OpenDash 1280x720': { what: 'The tall body lets zone C list eighteen drivers' },
  'OpenDash 850x480': { what: 'The common wheel-mounted DDU, and the tightest face that still carries all five parts', emphasis: 'base' },
  'OpenDash 800x480': { what: 'The 850 with twenty-five pixels less per side' },
  'OpenDash 800x286': { what: 'No bar: the height is not there. The fuel page, and no driver list' },
  'OpenDash 600x686': { what: 'Portrait. Zone A above B above C, and one bar field per end' },
  'OpenDash 480 round': { what: 'A round or square 480 DDU. Still the twelve-slot design of 0.1.x' },
  'OpenDash 800 round': { what: 'The larger round DDU. Still the twelve-slot design of 0.1.x' },
  'OpenDash Companion': { what: 'A phone or tablet beside the wheel, one module at a time' },
  'OpenDash Companion portrait': { what: 'The same, for a phone stood on end' },
  'OpenDash Pit wall': { what: 'Three pages for somebody who is not driving' },
  'OpenDash Pit wall portrait': { what: 'The same in one page, for a screen on its side' },
};

/** `OpenDash 1280x480` -> `opendash-1280x480`, which is what sync-shots names a capture. */
export const slug = (folder: string): string =>
  folder.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');

/** Where the capture of a package at a scenario lives. */
export const shotFor = (folder: string, scenario = 'green'): string => `/shots/${slug(folder)}-${scenario}.png`;

/**
 * Reading order, which is not manifest order.
 *
 * The manifest lists packages in the order the build produces them, which puts the superseded card
 * faces first and the two round faces before the reference one — an order that means something to
 * the build and nothing to somebody choosing a screen. A reader wants the reference face first,
 * then the rectangular faces widest to narrowest, then the round ones, which are a different
 * shape rather than a smaller size, and the second screens last.
 */
const KIND_RANK: Record<SitePackage['kind'], number> = { dash: 0, companion: 1, pitwall: 2 };

export function inReadingOrder(packages: readonly SitePackage[]): SitePackage[] {
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

export const ORDERED = inReadingOrder(PACKAGES);
export const FACES = ORDERED.filter((p) => p.kind === 'dash');
export const SECOND_SCREENS = ORDERED.filter((p) => p.kind !== 'dash');

/** `1920 × 480`, or `480 round` for a face that is the display itself. */
export const sizeLabel = (p: { width: number; height: number; round: boolean }): string =>
  p.round ? `${p.width} round` : `${p.width} × ${p.height}`;
