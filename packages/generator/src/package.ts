/**
 * Writes a DashPackage to disk in the layout SimHub imports (`<folder>/<name>.djson`,
 * `<name>.djson.metadata`, `_SHFonts/*.ttf`) and zips that folder into a `.simhubdash`.
 * The zip is reproducible: entries are sorted, every entry carries the same fixed timestamp,
 * and nothing depends on the machine that builds it.
 */

import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { basename, join, resolve } from 'node:path';
import { unzipSync, zipSync, type ZipOptions } from 'fflate';
import type { DashPackage } from './model.ts';
import { serializeDashboard, serializeMetadata } from './serialize.ts';

export const FONTS_DIR = '_SHFonts';
export const PACKAGE_EXTENSION = '.simhubdash';
export const METADATA_EXTENSION = '.djson.metadata';

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
  return { folder, files };
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

/** `zipSync` input for a folder: `<folderName>/<relative path>` keys in sorted order, fixed mtime. */
export const zipEntries = (folder: string, folderName: string, opts: ZipPackageOptions = {}): Record<string, [Uint8Array, ZipOptions]> => {
  const entryOptions: ZipOptions = { mtime: opts.mtime ?? ZIP_MTIME, level: opts.level ?? 6 };
  const entries: Record<string, [Uint8Array, ZipOptions]> = {};
  for (const rel of listFiles(folder)) {
    entries[`${folderName}/${rel}`] = [new Uint8Array(readFileSync(join(folder, rel))), entryOptions];
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
