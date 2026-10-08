/**
 * The captures, and the version they were taken from.
 *
 * Every picture of a dashboard on this site is a photograph of the package through SimHub's own
 * renderer on the Windows VM, and `public/shots/captures.json` says which build, which commit and
 * which emulator scenario each one came from. Each entry says so for itself: a reshoot of the
 * packages alone leaves the pages as old as they are, and a sidecar that kept one version for the
 * whole folder relabelled them as the newest run's (#627). The site shows that version under the pictures, and
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

export { provenanceNote } from './provenance';

/** Which build a capture was taken from: what `scripts/shotsRun.ts` records for a run. */
export interface Provenance {
  /** The VERSION the capture was taken from. */
  version: string;
  commit: string;
  /** ISO date. */
  date: string;
  simHubVersion: string;
}

export interface CaptureEntry extends Provenance {
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
  schema: 2;
  files: Record<string, CaptureEntry>;
}

/**
 * Schema 1: one run's provenance for the whole folder, and none on the entries. Every sync
 * overwrote it with the newest run's, so it said which run last touched the folder rather than which
 * took a picture.
 */
export interface CapturesSidecarV1 extends Provenance {
  schema: 1;
  scenario: string;
  files: Record<string, Omit<CaptureEntry, keyof Provenance>>;
}

/** A sidecar of either schema, as the current one: a schema 1 file's provenance goes onto each entry. */
export function upgradeCaptures(raw: CapturesSidecar | CapturesSidecarV1): CapturesSidecar {
  if (raw.schema === 2) return raw;
  const { version, commit, date, simHubVersion } = raw;
  return { schema: 2, files: Object.fromEntries(Object.entries(raw.files).map(([file, e]) => [file, { ...e, version, commit, date, simHubVersion }])) };
}

/** The files whose capture was not taken from `version`. */
export const staleCaptures = (sidecar: CapturesSidecar, version: string): string[] =>
  Object.entries(sidecar.files).filter(([, e]) => e.version !== version).map(([file]) => file);

/** `public/` relative to where the site runs: `site/` in development and CI, `/app` in the container. */
export const CAPTURES_PATH = path.join(process.cwd(), 'public', 'shots', 'captures.json');

const EMPTY: CapturesSidecar = { schema: 2, files: {} };

export function readCaptures(file = CAPTURES_PATH): CapturesSidecar {
  if (!existsSync(file)) return EMPTY;
  return upgradeCaptures(JSON.parse(readFileSync(file, 'utf8')) as CapturesSidecar | CapturesSidecarV1);
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
