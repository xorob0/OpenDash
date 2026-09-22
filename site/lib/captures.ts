/**
 * The captures, and the version they were taken from.
 *
 * Every picture of a dashboard on this site is a photograph of the package through SimHub's own
 * renderer on the Windows VM, and `public/shots/captures.json` says which build, which commit and
 * which emulator scenario each one came from. The site shows that version under the pictures, and
 * when it is not the version being served the note says so: a stale photograph presented as the
 * product is the failure #375 was filed for.
 *
 * File names carry no scenario. A capture is `<slug>.png` for a package and `page-<id>.png` for a
 * page, and the sidecar says what scenario it was, so re-shooting on another scenario changes a
 * line in one file rather than every reference on the site.
 *
 * The sidecar is read from disk rather than imported, so a build without it still typechecks; the
 * pages then say the pictures are missing rather than failing to build. `sync-shots.ts` writes it.
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { slug } from './packages';

export interface CaptureEntry {
  kind: 'package' | 'page' | 'panel';
  /** The package folder, for a `package` capture. */
  package?: string;
  /** The catalogue id, for a `page` capture. */
  page?: string;
  /** The plugin tab, for a `panel` capture. */
  panel?: string;
  width: number;
  height: number;
  /** The emulator scenario that was running, or null for a picture of the plugin's panel. */
  scenario: string | null;
}

export interface CapturesSidecar {
  schema: 1;
  /** The VERSION the captures were taken from. */
  version: string;
  commit: string;
  /** ISO date. */
  date: string;
  simHubVersion: string;
  /** The scenario most captures share. */
  scenario: string;
  files: Record<string, CaptureEntry>;
}

/** `public/` relative to where the site runs: `site/` in development and CI, `/app` in the container. */
export const CAPTURES_PATH = path.join(process.cwd(), 'public', 'shots', 'captures.json');

const EMPTY: CapturesSidecar = { schema: 1, version: '', commit: '', date: '', simHubVersion: '', scenario: '', files: {} };

export function readCaptures(file = CAPTURES_PATH): CapturesSidecar {
  if (!existsSync(file)) return EMPTY;
  return JSON.parse(readFileSync(file, 'utf8')) as CapturesSidecar;
}

export const CAPTURES: CapturesSidecar = readCaptures();

export const packageFile = (folder: string): string => `${slug(folder)}.png`;
export const pageFile = (id: string): string => `page-${id}.png`;
export const panelFile = (tab: string): string => `panel-${tab}.png`;

export const stillFor = (folder: string): string => `/shots/${packageFile(folder)}`;
export const pageStill = (id: string): string => `/shots/${pageFile(id)}`;
export const panelStill = (tab: string): string => `/shots/${panelFile(tab)}`;

/** Whether the sidecar names a capture, which is how a page decides between a picture and a note. */
export const hasCapture = (file: string): boolean => file in CAPTURES.files;

/** `22 September 2026`, for a caption. */
export function longDate(iso: string): string {
  const d = new Date(`${iso}T00:00:00Z`);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' });
}
