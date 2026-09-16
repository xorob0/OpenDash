#!/usr/bin/env bun
/**
 * content: generate the facts the site states from the sources that already hold them.
 *
 * Everything on this site that is a fact about the product — which packages exist, how big each
 * one is, what the twenty-one modules are called, which three ship off, what version this is and
 * what each release changed — is written down somewhere in the repository already. Restating any
 * of it in a React component would make the site a copy that goes stale silently, which is exactly
 * the failure the project refuses for the dashboards themselves.
 *
 * So the prose is the site's and the facts are the repository's. This reads:
 *
 *   build/manifest.json                    every built package, its kind and its size
 *   packages/dash/src/contract.ts          the module catalogue, and which modules ship off
 *   VERSION                                the one version string
 *   CHANGELOG.md                           the release history
 *   build/*.simhubdash, OpenDash-plugin.zip  what there is to download, and how big
 *
 * and writes lib/content.generated.ts. `build/` is only populated after `bun run build` at the
 * repository root, which the Dockerfile runs in an earlier stage; when it is missing the downloads
 * come back empty and the Downloads page says so rather than inventing a file.
 */
import { existsSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { MODULE_CATALOGUE } from '../../packages/dash/src/contract.ts';
import { sectionFor, versionsIn } from '../../scripts/changelog.ts';

const repoRoot = path.resolve(import.meta.dir, '..', '..');
const buildDir = path.join(repoRoot, 'build');
const outPath = path.resolve(import.meta.dir, '..', 'lib', 'content.generated.ts');

/** A package the site shows. The manifest's shape, minus the fields only the plugin cares about. */
export interface SitePackage {
  folder: string;
  kind: 'dash' | 'companion' | 'pitwall';
  width: number;
  height: number;
  file: string;
  /** Round faces are the display itself; the site frames them in a circle rather than a rectangle. */
  round: boolean;
}

/**
 * The card faces are built beside the zone faces only so the two can be compared, and XOR-95
 * deletes them. They are not a size anybody should install, so the site never lists them.
 */
const SUPERSEDED = /^openDash slots /;

/** The manifest's own shape, of which the site uses five fields. */
interface ManifestEntry {
  folder: string;
  kind: SitePackage['kind'];
  width: number;
  height: number;
  file: string;
}

export function sitePackages(manifest: { packages: ManifestEntry[] }): SitePackage[] {
  return manifest.packages
    .filter((p) => !SUPERSEDED.test(p.folder))
    .map(({ folder, kind, width, height, file }) => ({ folder, kind, width, height, file, round: /round/.test(folder) }));
}

/** A downloadable file: what it is called, what it weighs, and which package it carries. */
export interface Download {
  file: string;
  bytes: number;
}

export function downloads(dir: string): Download[] {
  if (!existsSync(dir)) return [];
  return readdirSync(dir)
    .filter((f) => f.endsWith('.simhubdash') || f === 'OpenDash-plugin.zip')
    .filter((f) => !SUPERSEDED.test(f))
    .sort()
    .map((file) => ({ file, bytes: statSync(path.join(dir, file)).size }));
}

/**
 * One release as the site shows it. The changelog is already parsed by scripts/changelog.ts, which
 * is what cuts a release body in CI, so this reuses it rather than writing a second parser that
 * could disagree with the one the release actually used.
 */
export interface Release {
  version: string;
  date: string | null;
  body: string;
  /** An `Unreleased` section is work landed on main that no tag carries yet. */
  unreleased: boolean;
  /** A version ending in a suffix such as `-rc.2` publishes as a pre-release. */
  preRelease: boolean;
}

/** The date beside a heading: `## 0.2.0-rc.1 (2026-09-13)`. */
const dateOf = (markdown: string, version: string): string | null => {
  const line = markdown.split('\n').find((l) => l.startsWith('## ') && l.slice(3).trim().split(/\s+/)[0] === version);
  return line?.match(/\((\d{4}-\d{2}-\d{2})\)/)?.[1] ?? null;
};

export function releases(markdown: string): Release[] {
  return versionsIn(markdown)
    .map((version) => ({
      version,
      date: dateOf(markdown, version),
      body: sectionFor(markdown, version) ?? '',
      unreleased: version.toLowerCase() === 'unreleased',
      preRelease: /-/.test(version),
    }))
    .filter((r) => r.body !== '');
}

if (import.meta.main) {
  const manifestPath = path.join(buildDir, 'manifest.json');
  const manifest = existsSync(manifestPath)
    ? (JSON.parse(await Bun.file(manifestPath).text()) as { version: string; simHubVersion: string; packages: ManifestEntry[] })
    : null;
  const version = (await Bun.file(path.join(repoRoot, 'VERSION')).text()).trim();
  const changelog = await Bun.file(path.join(repoRoot, 'CHANGELOG.md')).text();

  const body = `/*
 * Generated by site/scripts/content.generated.ts. Do not edit.
 * Every value here is read from the repository at build time; see that script for the sources.
 */
import type { Download, Release, SitePackage } from '../scripts/content.ts';

export const VERSION = ${JSON.stringify(version)};
export const SIMHUB_VERSION = ${JSON.stringify(manifest?.simHubVersion ?? '9.12.6')};

/** Every package the site shows, in manifest order. Empty when the repository has not been built. */
export const PACKAGES: SitePackage[] = ${JSON.stringify(manifest ? sitePackages(manifest) : [], null, 2)};

/** The twenty-one companion modules, which are also the zone pages of every face. */
export const MODULES = ${JSON.stringify(MODULE_CATALOGUE, null, 2)} as const;

/** What there is to download. Empty when the packages have not been built into build/. */
export const DOWNLOADS: Download[] = ${JSON.stringify(downloads(buildDir), null, 2)};

/** The release history, newest first, cut from CHANGELOG.md. */
export const RELEASES: Release[] = ${JSON.stringify(releases(changelog), null, 2)};
`;
  writeFileSync(outPath, body);
  const packages = manifest ? sitePackages(manifest).length : 0;
  console.log(`wrote lib/content.generated.ts (${packages} packages, ${MODULE_CATALOGUE.length} modules, ${downloads(buildDir).length} downloads, ${releases(changelog).length} releases)`);
}
