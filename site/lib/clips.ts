/**
 * The clips: a few seconds of a dashboard, looping, recorded from SimHub's own renderer.
 *
 * `bun run clips` at the repository root records raw frames of a dash window on the Windows VM and
 * encodes them on the host; `sync-clips.ts` copies the result into `public/clips/` and records
 * what was taken in `clips.json`. A page asks `clipFor(folder)` and gets either the three files a
 * `<video>` needs or nothing, in which case it shows the still instead. A missing clip is therefore
 * a page with a photograph, never a broken player.
 *
 * Each clip says which build it was recorded from, since re-recording one leaves the others as they
 * were; a version kept once for the whole sidecar relabelled every older clip as the newest (#627).
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { slug } from './packages';

/** Which build a clip was recorded from; its date is `takenAt`. */
export interface ClipProvenance {
  version: string;
  commit: string;
  simHubVersion: string;
}

export interface ClipEntry extends ClipProvenance {
  slug: string;
  package: string;
  scenario: string;
  width: number;
  height: number;
  fps: number;
  seconds: number;
  frames: number;
  dropped: number;
  /** ISO timestamp. */
  takenAt: string;
  files: { webm: string; mp4: string; poster: string };
  bytes: { webm: number; mp4: number; poster: number };
}

export interface ClipsSidecar {
  schema: 2;
  clips: ClipEntry[];
}

/** Schema 1: the newest recording's provenance for the whole sidecar, and none on the clips. */
export interface ClipsSidecarV1 extends ClipProvenance {
  schema: 1;
  clips: Omit<ClipEntry, keyof ClipProvenance>[];
}

/** A sidecar of either schema, as the current one: a schema 1 file's provenance goes onto each clip. */
export function upgradeClips(raw: ClipsSidecar | ClipsSidecarV1): ClipsSidecar {
  if (raw.schema === 2) return raw;
  const { version, commit, simHubVersion } = raw;
  return { schema: 2, clips: raw.clips.map((c) => ({ ...c, version, commit, simHubVersion })) };
}

export const CLIPS_PATH = path.join(process.cwd(), 'public', 'clips', 'clips.json');

const EMPTY: ClipsSidecar = { schema: 2, clips: [] };

export function readClips(file = CLIPS_PATH): ClipsSidecar {
  if (!existsSync(file)) return EMPTY;
  return upgradeClips(JSON.parse(readFileSync(file, 'utf8')) as ClipsSidecar | ClipsSidecarV1);
}

export const CLIPS: ClipsSidecar = readClips();

/** What a `<video>` needs for a package, or undefined when no clip of it has been recorded. */
export interface ClipSources {
  webm: string;
  mp4: string;
  poster: string;
  width: number;
  height: number;
  entry: ClipEntry;
}

export function clipFor(folder: string, sidecar: ClipsSidecar = CLIPS): ClipSources | undefined {
  const entry = sidecar.clips.find((c) => c.slug === slug(folder));
  if (!entry) return undefined;
  return {
    webm: `/clips/${entry.files.webm}`,
    mp4: `/clips/${entry.files.mp4}`,
    poster: `/clips/${entry.files.poster}`,
    width: entry.width,
    height: entry.height,
    entry,
  };
}
