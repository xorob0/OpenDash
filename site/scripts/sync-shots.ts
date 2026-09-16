#!/usr/bin/env bun
/**
 * sync-shots: move a reviewed capture run into the site's public folder.
 *
 * `bun run shots` at the repository root photographs packages into build/shots/<scenario>/, which
 * is scratch and gitignored, and names them `01-opendash-green.png` — numbered so that a listing
 * reads in the order the loop ran. The site wants the opposite: a stable name it can hard-code in a
 * component, with no ordinal in it, because inserting a package into the list would otherwise
 * renumber every file after it and break every reference at once.
 *
 * So this renames `NN-<package>-<scenario>.png` to `<package>-<scenario>.png` and copies it in.
 * It is run by hand after looking at the captures, never as part of a build: a capture that caught
 * SimHub mid-reconnect is a photograph of a bug, and the only thing that catches one is an eye.
 *
 *   bun scripts/sync-shots.ts ../build/shots/green
 */
import { copyFileSync, mkdirSync, readdirSync } from 'node:fs';
import path from 'node:path';

const outDir = path.resolve(import.meta.dir, '..', 'public', 'shots');

/** `01-opendash-1280x480-green.png` -> `opendash-1280x480-green.png`. */
export const stableName = (file: string): string => file.replace(/^\d+-/, '');

if (import.meta.main) {
  const from = process.argv[2];
  if (!from) {
    console.error('usage: bun scripts/sync-shots.ts <dir of captures>');
    process.exit(1);
  }
  mkdirSync(outDir, { recursive: true });
  const files = readdirSync(from).filter((f) => f.endsWith('.png'));
  for (const file of files) {
    copyFileSync(path.join(from, file), path.join(outDir, stableName(file)));
    console.log(`${file} -> shots/${stableName(file)}`);
  }
  console.log(`${files.length} captures in site/public/shots`);
}
