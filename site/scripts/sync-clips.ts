#!/usr/bin/env bun
/**
 * sync-clips: move the clips `bun run clips` encoded into the site's public folder, and record
 * where each came from in public/clips/clips.json.
 *
 * Run by hand after watching them, for the same reason sync-shots is: a clip that caught SimHub
 * reconnecting is a film of a bug. An entry is replaced by slug, so re-recording one clip leaves
 * the others as they were.
 *
 *   bun scripts/sync-clips.ts ../build/clips
 */
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import type { ClipRecord } from '../../scripts/clips.ts';
import type { ClipEntry, ClipsSidecar } from '../lib/clips.ts';

const outDir = path.resolve(import.meta.dir, '..', 'public', 'clips');
const sidecarPath = path.join(outDir, 'clips.json');

export function readSidecar(file = sidecarPath): ClipsSidecar {
  if (!existsSync(file)) return { schema: 1, version: '', commit: '', simHubVersion: '', clips: [] };
  return JSON.parse(readFileSync(file, 'utf8')) as ClipsSidecar;
}

/** The entry replaces any with the same slug; the sidecar's provenance becomes the newest clip's. */
export function mergeClip(sidecar: ClipsSidecar, entry: ClipEntry, record: Pick<ClipRecord, 'version' | 'commit' | 'simHubVersion'>): ClipsSidecar {
  return {
    schema: 1,
    version: record.version,
    commit: record.commit,
    simHubVersion: record.simHubVersion,
    clips: [...sidecar.clips.filter((c) => c.slug !== entry.slug), entry].sort((a, b) => a.slug.localeCompare(b.slug)),
  };
}

export function entryFor(record: ClipRecord, bytes: ClipEntry['bytes']): ClipEntry {
  return {
    slug: record.slug,
    package: record.package,
    scenario: record.scenario,
    width: record.recording.width,
    height: record.recording.height,
    fps: record.fps,
    seconds: record.seconds,
    frames: record.recording.frames,
    dropped: record.recording.dropped,
    takenAt: record.takenAt,
    files: { webm: `${record.slug}.webm`, mp4: `${record.slug}.mp4`, poster: `${record.slug}.png` },
    bytes,
  };
}

if (import.meta.main) {
  const from = process.argv[2];
  if (!from) {
    console.error('usage: bun scripts/sync-clips.ts <build/clips>');
    process.exit(1);
  }
  mkdirSync(outDir, { recursive: true });
  let sidecar = readSidecar();
  let count = 0;
  for (const dir of readdirSync(from)) {
    const recordPath = path.join(from, dir, 'record.json');
    if (!existsSync(recordPath)) continue;
    const record = JSON.parse(readFileSync(recordPath, 'utf8')) as ClipRecord;
    const files = ['webm', 'mp4', 'png'].map((ext) => path.join(from, dir, `${record.slug}.${ext}`));
    if (!files.every((f) => existsSync(f))) {
      console.error(`${dir}: not encoded yet (bun run clips --encode-only)`);
      continue;
    }
    for (const f of files) copyFileSync(f, path.join(outDir, path.basename(f)));
    const size = (f: string) => readFileSync(f).byteLength;
    sidecar = mergeClip(sidecar, entryFor(record, { webm: size(files[0]!), mp4: size(files[1]!), poster: size(files[2]!) }), record);
    console.log(`${dir} -> clips/${record.slug}.{webm,mp4,png}`);
    count += 1;
  }
  writeFileSync(sidecarPath, `${JSON.stringify(sidecar, null, 2)}\n`);
  console.log(`${count} clips into site/public/clips; ${sidecar.clips.length} in the sidecar`);
}
