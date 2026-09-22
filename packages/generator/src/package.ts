/**
 * Writes a DashPackage to disk in the layout SimHub imports (`<folder>/<name>.djson`,
 * `<name>.djson.metadata`, `_SHFonts/*.ttf`) and zips that folder into a `.simhubdash`.
 * The zip is reproducible: entries are sorted, every entry carries the same fixed timestamp,
 * and nothing depends on the machine that builds it.
 */

import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { basename, join, resolve } from 'node:path';
import { unzipSync, zipSync, type ZipOptions } from 'fflate';
import type { Dashboard, DashPackage } from './model.ts';
import { md5Hex, pngSize } from './images.ts';
import { serializeDashboard, serializeMetadata } from './serialize.ts';

export const FONTS_DIR = '_SHFonts';
export const PACKAGE_EXTENSION = '.simhubdash';
export const METADATA_EXTENSION = '.djson.metadata';
/** SimHub's own spelling, missing an `s`. It is the file name on disk and cannot be corrected. */
export const RESOURCES_EXTENSION = '.djson.ressources';
/**
 * The thumbnail SimHub's dashboard list draws for a package.
 *
 * `GraphicalDashItem.LoadPreview` looks for `<djson path>.jpg` and then `<djson path>.png`, and
 * shows an empty box when it finds neither; DashStudio writes the `.png` on every save, which is
 * why a hand-made dashboard has one and a generated one did not. `EditorModel.CleanDir` deletes
 * any `<x>.djson.png` whose base name is not the folder's own, so the file belongs to the main
 * dashboard and a widget cannot carry one.
 */
export const PREVIEW_EXTENSION = '.djson.png';

/**
 * Timestamp stamped on every zip entry. Zip stores local date/time fields, so a Date built
 * from local components gives the same bytes in every time zone.
 */
export const ZIP_MTIME = new Date(2000, 0, 1, 0, 0, 0);

export interface WrittenPackage {
  /** Absolute path of `<outDir>/<folderName>`. */
  folder: string;
  /** Absolute paths of every file written, in write order. */
  files: string[];
}

export interface WritePackageOptions {
  /** Remove `<outDir>/<folderName>` first so stale files never leak into the zip. Default true. */
  clean?: boolean;
}

/** Writes `<outDir>/<folderName>/…` and returns what was written. Does not validate; call validatePackage first. */
export const writePackage = (pkg: DashPackage, outDir: string, opts: WritePackageOptions = {}): WrittenPackage => {
  const folder = resolve(outDir, pkg.folderName);
  if (opts.clean ?? true) rmSync(folder, { recursive: true, force: true });
  mkdirSync(folder, { recursive: true });
  const files: string[] = [];
  const write = (path: string, content: string): void => {
    writeFileSync(path, content, 'utf8');
    files.push(path);
  };
  for (const dashboard of pkg.dashboards) {
    const base = join(folder, `${dashboard.name}.djson`);
    write(base, serializeDashboard(dashboard, { packageName: pkg.folderName }));
    write(`${base}.metadata`, serializeMetadata(dashboard));
    if (dashboard.images?.length) {
      const sidecar = `${base}.ressources`;
      writeFileSync(sidecar, resourcesZip(dashboard));
      files.push(sidecar);
    }
  }
  if (pkg.fonts.length > 0) {
    const fontsDir = join(folder, FONTS_DIR);
    mkdirSync(fontsDir, { recursive: true });
    const seen = new Set<string>();
    for (const font of pkg.fonts) {
      const name = basename(font);
      if (seen.has(name.toLowerCase())) throw new Error(`Two fonts would both be written as ${FONTS_DIR}/${name}`);
      seen.add(name.toLowerCase());
      const target = join(fontsDir, name);
      copyFileSync(font, target);
      files.push(target);
    }
  }
  for (const notice of pkg.notices ?? []) {
    const target = join(folder, notice.name);
    copyFileSync(notice.path, target);
    files.push(target);
  }
  if (pkg.preview !== undefined) {
    // Read rather than copied blind: the one thing SimHub asks of this file is that it decode, and
    // a JPEG or a text file under a .png name is a thumbnail that silently fails to appear.
    pngSize(new Uint8Array(readFileSync(pkg.preview)), pkg.preview);
    const target = join(folder, `${pkg.folderName}${PREVIEW_EXTENSION}`);
    copyFileSync(pkg.preview, target);
    files.push(target);
  }
  return { folder, files };
};

/**
 * The `<name>.djson.ressources` bytes: one entry per image at the zip's root, named
 * `<Name><Extension>`, which is how SimHub's own dashboards store them.
 *
 * Reproducible on the same terms as the package zip, since this one ends up inside it: entries
 * sorted by name and every entry carrying the fixed timestamp.
 */
export const resourcesZip = (dashboard: Dashboard, opts: ZipPackageOptions = {}): Uint8Array => {
  const entryOptions: ZipOptions = { mtime: opts.mtime ?? ZIP_MTIME, level: opts.level ?? 6 };
  const entries: Record<string, [Uint8Array, ZipOptions]> = {};
  for (const image of [...(dashboard.images ?? [])].sort((a, b) => byCodeUnit(a.name, b.name))) {
    const bytes = new Uint8Array(readFileSync(image.path));
    // The descriptor in the .djson states this length and this MD5. Writing bytes that disagree
    // with what was measured would ship a dashboard whose images SimHub refuses, and the source
    // file changing between describeImage and here is exactly how that happens. The MD5 is what
    // is compared, because two different pictures of the same size are not unusual at all.
    if (bytes.length !== image.length || md5Hex(bytes) !== image.md5) {
      throw new Error(`${image.name}${image.extension} does not match its descriptor; it changed after it was described`);
    }
    entries[`${image.name}${image.extension}`] = [bytes, entryOptions];
  }
  return zipSync(entries, { os: 0 });
};

const byCodeUnit = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/** Every file below `dir` as a sorted list of `/`-separated paths relative to `dir`. */
export const listFiles = (dir: string): string[] => {
  const out: string[] = [];
  const walk = (current: string, prefix: string): void => {
    for (const entry of readdirSync(current)) {
      const full = join(current, entry);
      const rel = prefix ? `${prefix}/${entry}` : entry;
      if (statSync(full).isDirectory()) walk(full, rel);
      else out.push(rel);
    }
  };
  walk(dir, '');
  return out.sort(byCodeUnit);
};

export interface ZipPackageOptions {
  /** Entry timestamp; defaults to {@link ZIP_MTIME}. */
  mtime?: Date;
  /** Deflate level 0..9; default 6. */
  level?: ZipOptions['level'];
}

/**
 * The timestamp a thumbnail is stamped with: a function of its own bytes, and of nothing else.
 *
 * Every other entry carries {@link ZIP_MTIME}, because a reproducible zip is one where the same
 * sources give the same bytes. The thumbnail cannot, and the reason is SimHub's thumbnail cache:
 * `GraphicalDashItemThumbnail` keys a decoded copy under
 * `Thumbnails\<md5 of full path + LastWriteTime + 200>.png`, and `ZipArchiveEntry.ExtractToFile`
 * stamps the extracted file with the entry's time. A fixed time would therefore give every release
 * of a package the same cache key at the same path, and an update that redraws the face would go on
 * showing the picture the first install left behind, for ever.
 *
 * So the stamp moves when, and only when, the picture does. Same picture, same bytes, same cached
 * thumbnail; new picture, new key, and SimHub decodes it again. The components are derived one at a
 * time and the Date is built from local fields, because that is what zip stores and what keeps the
 * archive identical in every time zone.
 */
export const previewMtime = (bytes: Uint8Array): Date => {
  const digest = md5Hex(bytes);
  const part = (at: number, of: number): number => parseInt(digest.slice(at, at + 4), 16) % of;
  // Day 1..28 and an even second, so that no derived value can be a date zip cannot store or a
  // second it would round away.
  return new Date(ZIP_MTIME.getFullYear(), part(0, 12), 1 + part(4, 28), part(8, 24), part(12, 60), 2 * part(16, 30));
};

/**
 * `zipSync` input for a folder: `<folderName>/<relative path>` keys in sorted order, fixed mtime
 * everywhere but the thumbnail, which is stamped from its own bytes; see {@link previewMtime}.
 */
export const zipEntries = (folder: string, folderName: string, opts: ZipPackageOptions = {}): Record<string, [Uint8Array, ZipOptions]> => {
  const mtime = opts.mtime ?? ZIP_MTIME;
  const level = opts.level ?? 6;
  const entries: Record<string, [Uint8Array, ZipOptions]> = {};
  for (const rel of listFiles(folder)) {
    const bytes = new Uint8Array(readFileSync(join(folder, rel)));
    entries[`${folderName}/${rel}`] = [bytes, { mtime: rel.endsWith(PREVIEW_EXTENSION) ? previewMtime(bytes) : mtime, level }];
  }
  return entries;
};

export interface ZippedPackage {
  /** Absolute path of the `.simhubdash` written. */
  path: string;
  bytes: Uint8Array;
  /** Entry names in archive order. */
  entries: string[];
}

/** Zips `<outDir>/<folderName>/` into `<outDir>/<folderName>.simhubdash`. */
export const zipPackage = (outDir: string, folderName: string, opts: ZipPackageOptions = {}): ZippedPackage => {
  const folder = resolve(outDir, folderName);
  if (!existsSync(folder) || !statSync(folder).isDirectory()) throw new Error(`${folder} is not a directory; run writePackage first`);
  const entries = zipEntries(folder, folderName, opts);
  const names = Object.keys(entries);
  if (!names.includes(`${folderName}/${folderName}.djson`)) {
    throw new Error(`${folder} has no ${folderName}.djson; SimHub would not recognise the package`);
  }
  const bytes = zipSync(entries, { os: 0 });
  const path = resolve(outDir, `${folderName}${PACKAGE_EXTENSION}`);
  writeFileSync(path, bytes);
  return { path, bytes, entries: names };
};

/** Entry name to content, for inspecting a `.simhubdash`. */
export const readZip = (bytes: Uint8Array): Record<string, Uint8Array> => unzipSync(bytes);
