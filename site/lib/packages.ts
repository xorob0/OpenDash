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

export interface PackageNote {
  /** What it is, in a few words. Sentence case, no full stop; the table puts it in a cell. */
  what: string;
  /** Set on the two sizes a first-time reader should be steered towards. */
  emphasis?: 'base' | 'large';
}

export const NOTES: Record<string, PackageNote> = {
  openDash: { what: 'The reference face. Every other size is this anatomy, redrawn' },
  'openDash 1280x480': { what: 'A wider DDU. The zones carry the car number and class beside every driver', emphasis: 'large' },
  'openDash 1280x400': { what: 'A shorter body, the same zones' },
  'openDash 1280x720': { what: 'The tall body lets zone C list eighteen drivers' },
  'openDash 850x480': { what: 'The common wheel-mounted DDU, and the tightest face that still carries all five parts', emphasis: 'base' },
  'openDash 800x480': { what: 'The 850 with twenty-five pixels less per side' },
  'openDash 800x286': { what: 'No bar: the height is not there. The fuel page, and no driver list' },
  'openDash 600x686': { what: 'Portrait. Zone A above B above C, and one bar field per end' },
  'openDash 480 round': { what: 'A round or square 480 DDU. Still the twelve-slot design of 0.1.x' },
  'openDash 800 round': { what: 'The larger round DDU. Still the twelve-slot design of 0.1.x' },
  'openDash Companion': { what: 'A phone or tablet beside the wheel, one module at a time' },
  'openDash Companion portrait': { what: 'The same, for a phone stood on end' },
  'openDash Pit wall': { what: 'Three pages for somebody who is not driving' },
  'openDash Pit wall portrait': { what: 'The same in one page, for a screen on its side' },
};

/** `openDash 1280x480` -> `opendash-1280x480`, which is what sync-shots names a capture. */
export const slug = (folder: string): string =>
  folder.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');

/** Where the capture of a package at a scenario lives. */
export const shotFor = (folder: string, scenario = 'green'): string => `/shots/${slug(folder)}-${scenario}.png`;

export const FACES = PACKAGES.filter((p) => p.kind === 'dash');
export const SECOND_SCREENS = PACKAGES.filter((p) => p.kind !== 'dash');

/** `1920 × 480`, or `480 round` for a face that is the display itself. */
export const sizeLabel = (p: { width: number; height: number; round: boolean }): string =>
  p.round ? `${p.width} round` : `${p.width} × ${p.height}`;
