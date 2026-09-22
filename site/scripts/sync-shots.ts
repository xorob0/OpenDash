#!/usr/bin/env bun
/**
 * sync-shots: move a reviewed capture run into the site's public folder, and record where it came
 * from.
 *
 * `bun run shots` photographs packages into build/shots/<scenario>/ as `NN-<slug>-<scenario>.png`,
 * numbered so a listing reads in the order the loop ran; `bun scripts/modules.ts` photographs the
 * pages as `page-<id>.png`. Both write a run.json beside the pictures with the version, commit,
 * scenario and laps seen. The site wants a stable name with no ordinal and no scenario in it, so a
 * reshoot on another scenario changes a line in one file rather than every reference on the site:
 * `<slug>.png` for a package, `page-<id>.png` for a page, `panel-<tab>.png` for the plugin.
 *
 * This copies the pictures under those names and merges the run's provenance into
 * public/shots/captures.json, which is what the pages read to say under a picture which version
 * it shows. It is run by hand after looking at the captures, never as part of a build: a capture
 * that caught SimHub mid-reconnect is a photograph of a bug, and the only thing that catches one
 * is an eye.
 *
 *   bun scripts/sync-shots.ts ../build/shots/gallery
 *   bun scripts/sync-shots.ts ../build/shots/modules
 *   bun scripts/sync-shots.ts --panel lights ../build/panel/lights.png
 *   bun scripts/sync-shots.ts --drop-missing          # forget entries whose file is gone
 */
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { provenance, readRun, type RunCapture } from '../../scripts/shotsRun.ts';
import type { CaptureEntry, CapturesSidecar } from '../lib/captures.ts';

const outDir = path.resolve(import.meta.dir, '..', 'public', 'shots');
const sidecarPath = path.join(outDir, 'captures.json');

/** `01-opendash-1280x480-gallery.png` -> `opendash-1280x480.png`; a page or panel name is kept. */
export function stableName(file: string, scenario: string | null): string {
  if (/^(page|panel)-/.test(file)) return file;
  let name = file.replace(/^\d+-/, '');
  if (scenario) name = name.replace(new RegExp(`-${scenario.toLowerCase().replace(/[^a-z0-9]+/g, '-')}\\.png$`), '.png');
  return name;
}

export function readSidecar(file = sidecarPath): CapturesSidecar {
  if (!existsSync(file)) return { schema: 1, version: '', commit: '', date: '', simHubVersion: '', scenario: '', files: {} };
  return JSON.parse(readFileSync(file, 'utf8')) as CapturesSidecar;
}

const toEntry = (c: RunCapture, scenario: string | null): CaptureEntry => ({
  kind: c.kind,
  ...(c.package ? { package: c.package } : {}),
  ...(c.page ? { page: c.page } : {}),
  ...(c.panel ? { panel: c.panel } : {}),
  width: c.width,
  height: c.height,
  scenario: c.kind === 'panel' ? null : scenario,
});

/** The run's provenance becomes the sidecar's, and its files join the sidecar's files. */
export function merge(sidecar: CapturesSidecar, run: { version: string; commit: string; date: string; simHubVersion: string; scenario: string | null }, files: Record<string, CaptureEntry>): CapturesSidecar {
  return {
    schema: 1,
    version: run.version,
    commit: run.commit,
    date: run.date,
    simHubVersion: run.simHubVersion,
    scenario: run.scenario ?? sidecar.scenario,
    files: { ...sidecar.files, ...files },
  };
}

function pngSize(file: string): { width: number; height: number } {
  const buf = readFileSync(file);
  return { width: buf.readUInt32BE(16), height: buf.readUInt32BE(20) };
}

function syncDir(from: string): number {
  const run = readRun(from);
  if (!run) {
    console.error(`${from} has no run.json; it was not made by bun run shots or bun scripts/modules.ts`);
    return 1;
  }
  const files: Record<string, CaptureEntry> = {};
  for (const [file, capture] of Object.entries(run.captures)) {
    const source = path.join(from, file);
    if (!existsSync(source)) {
      console.error(`${file} is in run.json but not on disk; skipped`);
      continue;
    }
    const name = stableName(file, run.scenario);
    copyFileSync(source, path.join(outDir, name));
    files[name] = toEntry(capture, run.scenario);
    console.log(`${file} -> shots/${name}`);
  }
  if (run.dirty) console.warn('the tree was dirty when these were taken; the commit in the sidecar is not the whole story');
  writeFileSync(sidecarPath, `${JSON.stringify(merge(readSidecar(), run, files), null, 2)}\n`);
  console.log(`${Object.keys(files).length} captures from ${run.version} (${run.commit}) on ${run.scenario ?? 'no scenario'} into site/public/shots`);
  return 0;
}

function syncPanel(tab: string, source: string): number {
  if (!existsSync(source)) {
    console.error(`${source} does not exist`);
    return 1;
  }
  const name = `panel-${tab}.png`;
  copyFileSync(source, path.join(outDir, name));
  const size = pngSize(source);
  const sidecar = readSidecar();
  const files = { [name]: toEntry({ kind: 'panel', panel: tab, ...size, lapsSeen: null }, null) };
  // A panel picture joins the run the dashboards were shot in; it does not restamp the sidecar's
  // version, commit or date, which are the run's. A sidecar with no run yet takes the tree's.
  const run = sidecar.version ? sidecar : { ...provenance(null), scenario: sidecar.scenario };
  writeFileSync(sidecarPath, `${JSON.stringify(merge(sidecar, run, files), null, 2)}\n`);
  console.log(`${source} -> shots/${name} (${size.width}x${size.height})`);
  return 0;
}

function dropMissing(): number {
  const sidecar = readSidecar();
  const kept = Object.fromEntries(Object.entries(sidecar.files).filter(([file]) => existsSync(path.join(outDir, file))));
  const dropped = Object.keys(sidecar.files).length - Object.keys(kept).length;
  writeFileSync(sidecarPath, `${JSON.stringify({ ...sidecar, files: kept }, null, 2)}\n`);
  console.log(`${dropped} entries dropped`);
  return 0;
}

if (import.meta.main) {
  const argv = process.argv.slice(2);
  mkdirSync(outDir, { recursive: true });
  let code: number;
  if (argv[0] === '--panel' && argv[1] && argv[2]) code = syncPanel(argv[1], path.resolve(argv[2]));
  else if (argv[0] === '--drop-missing') code = dropMissing();
  else if (argv[0] && !argv[0].startsWith('--')) code = syncDir(path.resolve(argv[0]));
  else {
    console.error('usage: bun scripts/sync-shots.ts <dir of captures> | --panel <tab> <file.png> | --drop-missing');
    code = 1;
  }
  const stale = readdirSync(outDir).filter((f) => f.endsWith('.png') && !(f in readSidecar().files));
  if (stale.length > 0) console.warn(`${stale.length} picture${stale.length > 1 ? 's' : ''} in site/public/shots with no sidecar entry: ${stale.join(', ')}`);
  process.exit(code);
}
