#!/usr/bin/env bun
/**
 * fonts: convert the repository's vendored Barlow files into the woff2 the site serves.
 *
 * The site sets its type in the same two families the dash does, and the files are already in the
 * repository under packages/dash/fonts — vendored there so a package can carry them. Downloading
 * them again from a font CDN would put a second copy of the same typeface under a second licence
 * trail, so this converts the vendored ones instead. The originals are never touched.
 *
 * Only the faces the site actually sets are converted; the rest of the family would be dead weight
 * in the image. That is the five its own copy uses, and the two more the dashboards draw with that
 * the copy does not: Barlow Condensed Light and Barlow Bold, which `design/advances.ts` measures and
 * the in-browser demo (#395) has to draw in the same faces to clip where SimHub clips. OFL.txt
 * travels with them, as it must.
 */
import { copyFileSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { compress } from 'wawoff2';

const repoRoot = path.resolve(import.meta.dir, '..', '..');
const fontsDir = path.join(repoRoot, 'packages', 'dash', 'fonts');
const outDir = path.resolve(import.meta.dir, '..', 'public', 'fonts');

/**
 * The faces the site sets, and nothing else. Barlow for copy, Barlow Condensed for numerals, and the
 * two the dashboards add for the demo: Barlow Bold and Barlow Condensed Light.
 */
export const FACES = [
  'Barlow-Regular.ttf',
  'Barlow-Medium.ttf',
  'Barlow-SemiBold.ttf',
  'Barlow-Bold.ttf',
  'BarlowCondensed-Light.ttf',
  'BarlowCondensed-SemiBold.ttf',
  'BarlowCondensed-Bold.ttf',
] as const;

if (import.meta.main) {
  mkdirSync(outDir, { recursive: true });
  let saved = 0;
  for (const face of FACES) {
    const ttf = await Bun.file(path.join(fontsDir, face)).arrayBuffer();
    const woff2 = await compress(new Uint8Array(ttf));
    const out = path.join(outDir, face.replace(/\.ttf$/, '.woff2'));
    writeFileSync(out, woff2);
    saved += ttf.byteLength - woff2.byteLength;
    console.log(`${face} -> ${path.basename(out)}  ${(ttf.byteLength / 1024).toFixed(0)}K -> ${(woff2.byteLength / 1024).toFixed(0)}K`);
  }
  // The licence travels with the fonts, exactly as it does into a .simhubdash.
  copyFileSync(path.join(fontsDir, 'OFL.txt'), path.join(outDir, 'OFL.txt'));
  console.log(`${FACES.length} faces, ${(saved / 1024).toFixed(0)}K saved, OFL.txt copied`);
}
