/**
 * The clips: a few seconds of a dashboard, looping, recorded from SimHub's own renderer.
 *
 * `bun run clips` at the repository root records raw frames of a dash window on the Windows VM and
 * encodes them on the host; `sync-clips.ts` copies the result into `public/clips/` and records
 * what was taken in `clips.json`. A page asks `clipFor(folder)` and gets either the three files a
 * `<video>` needs or nothing, in which case it shows the still instead. A missing clip is therefore
 * a page with a photograph, never a broken player.
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { slug } from './packages';

export interface ClipEntry {
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
  schema: 1;
  version: string;
  commit: string;
  simHubVersion: string;
  clips: ClipEntry[];
}

export const CLIPS_PATH = path.join(process.cwd(), 'public', 'clips', 'clips.json');

const EMPTY: ClipsSidecar = { schema: 1, version: '', commit: '', simHubVersion: '', clips: [] };

export function readClips(file = CLIPS_PATH): ClipsSidecar {
  if (!existsSync(file)) return EMPTY;
  return JSON.parse(readFileSync(file, 'utf8')) as ClipsSidecar;
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
