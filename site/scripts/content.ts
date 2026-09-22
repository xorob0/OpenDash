#!/usr/bin/env bun
/**
 * content: generate the facts the site states from the sources that already hold them.
 *
 * Everything on this site that is a fact about the product is written down somewhere in the
 * repository already: which packages exist, how big each one is, what the twenty-one pages are
 * called, which three ship off, which strip shapes the build writes, where the parts of the base
 * face sit, what version this is and what each release changed. Restating any of it in a React
 * component would make the site a copy that goes stale silently, which is exactly the failure the
 * project refuses for the dashboards themselves.
 *
 * So the prose is the site's and the facts are the repository's. This reads:
 *
 *   build/manifest.json                    every built package, its kind and its size; the LED profiles
 *   packages/dash/src/contract.ts          the page catalogues and the bar fields
 *   packages/dash/src/flags.ts             the flags, in the order they take a surface
 *   packages/dash/src/leds/strip.ts        the strip shapes
 *   packages/dash/src/zones/index.ts       the base face and the rectangles of its parts
 *   VERSION                                the one version string
 *   CHANGELOG.md                           the release history
 *   build/*.simhubdash, OpenDash-plugin.zip  what there is to download, and how big
 *
 * and writes lib/content.generated.ts. `build/` is only populated after `bun run build` at the
 * repository root, which the Dockerfile runs in an earlier stage; when it is missing the downloads
 * come back empty and the Downloads page says so rather than inventing a file.
 *
 * The functions are exported and pure so that test/content.test.ts can hold them against the
 * modules they read, without the generated file having to exist.
 */
import { existsSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import {
  BAND_D_PAGES,
  BAR_FIELDS,
  MODULE_CATALOGUE,
  PIT_WALL_PAGES,
  PIT_WALL_WIDE_ZONE_PAGES,
  PIT_WALL_ZONE_PAGES,
  ZONE_A_PAGES,
} from '../../packages/dash/src/contract.ts';
import { FLAG_CATALOGUE, type FlagCondition } from '../../packages/dash/src/flags.ts';
import { ALL_SHAPES, LEGACY_SHAPES, type StripShape } from '../../packages/dash/src/leds/strip.ts';
import { BASE_FACE } from '../../packages/dash/src/zones/index.ts';
import type { ZoneLayout } from '../../packages/dash/src/zones/layout.ts';
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
 * The card faces are built beside the zone faces only so the two can be compared, and #146
 * deletes them. They are not a size anybody should install, so the site never lists them.
 */
export const SUPERSEDED = /^OpenDash slots /;

/** The manifest's own shape, of which the site uses these fields. */
export interface ManifestEntry {
  folder: string;
  kind: SitePackage['kind'];
  width: number;
  height: number;
  file: string;
}

export interface Manifest {
  version: string;
  simHubVersion: string;
  packages: ManifestEntry[];
  ledProfiles?: string[];
}

export function sitePackages(manifest: { packages: ManifestEntry[] }): SitePackage[] {
  return manifest.packages
    .filter((p) => !SUPERSEDED.test(p.folder))
    .map(({ folder, kind, width, height, file }) => ({ folder, kind, width, height, file, round: /round/.test(folder) }));
}

/** A downloadable file: what it is called and what it weighs. */
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

/** A strip shape as the Lights page draws it. */
export interface SiteStripShape {
  id: string;
  label: string;
  left: number;
  centre: number;
  right: number;
  /** The hardware the shape was added for, when it was added for one. */
  devices: string[];
  extraRuns: { count: number; length: number } | null;
  /** Shipped before the grid and kept for the wheels that have it; drawn apart from the grid. */
  legacy: boolean;
}

export function stripShapes(all: readonly StripShape[], legacy: readonly StripShape[]): SiteStripShape[] {
  const legacyIds = new Set(legacy.map((s) => s.id));
  return all.map((s) => ({
    id: s.id,
    label: s.label,
    left: s.left,
    centre: s.centre,
    right: s.right,
    devices: [...(s.devices ?? [])],
    extraRuns: s.extraRuns ? { count: s.extraRuns.count, length: s.extraRuns.length } : null,
    legacy: legacyIds.has(s.id),
  }));
}

/** A rectangle of a face, in the face's own pixels. */
export interface SiteRect {
  left: number;
  top: number;
  width: number;
  height: number;
}

export type FacePartId = 'revBar' | 'bar' | 'zoneB' | 'zoneA' | 'zoneC' | 'band';

export interface FacePart {
  id: FacePartId;
  rect: SiteRect;
}

/** The face the home page explains, and where its parts are. */
export interface SiteFace {
  folder: string;
  width: number;
  height: number;
  /** In reading order: the rev bar, the bar, then B, A, C across the body, then the band. */
  parts: FacePart[];
}

export function heroFace(layout: ZoneLayout): SiteFace {
  const z = layout.zones;
  const part = (id: FacePartId, r: SiteRect): FacePart => ({ id, rect: { left: r.left, top: r.top, width: r.width, height: r.height } });
  return {
    folder: layout.folder,
    width: layout.width,
    height: layout.height,
    // The nano has no bar, so the part is only listed where the face draws one.
    parts: [
      part('revBar', z.revBarWell),
      ...(z.bar ? [part('bar', z.bar)] : []),
      part('zoneB', z.zoneB),
      part('zoneA', z.zoneA),
      part('zoneC', z.zoneC),
      part('band', z.band),
    ],
  };
}

/** A flag as the Lights page lists it, in the order the catalogue ranks it. */
export interface SiteFlag {
  id: string;
  name: string;
  /** Survives the box's "critical flags only" switch. */
  critical: boolean;
}

export const flags = (catalogue: readonly FlagCondition[]): SiteFlag[] => catalogue.map(({ id, name, critical }) => ({ id, name, critical }));

/** A page or field of a face zone, as the contract lists it. */
export interface SitePage {
  number: number;
  id: string;
  name: string;
}

if (import.meta.main) {
  const manifestPath = path.join(buildDir, 'manifest.json');
  const manifest = existsSync(manifestPath) ? (JSON.parse(await Bun.file(manifestPath).text()) as Manifest) : null;
  const version = (await Bun.file(path.join(repoRoot, 'VERSION')).text()).trim();
  const changelog = await Bun.file(path.join(repoRoot, 'CHANGELOG.md')).text();
  const json = (value: unknown): string => JSON.stringify(value, null, 2);
  const pages = (list: readonly { number: number; id: string; name: string }[]): SitePage[] => list.map(({ number, id, name }) => ({ number, id, name }));

  const body = `/*
 * Generated by site/scripts/content.ts. Do not edit.
 * Every value here is read from the repository at build time; see that script for the sources.
 */
import type { Download, Release, SiteFace, SiteFlag, SitePackage, SitePage, SiteStripShape } from '../scripts/content.ts';

export const VERSION = ${JSON.stringify(version)};
export const SIMHUB_VERSION = ${JSON.stringify(manifest?.simHubVersion ?? '9.12.6')};

/** Every package the site shows, in manifest order. Empty when the repository has not been built. */
export const PACKAGES: SitePackage[] = ${json(manifest ? sitePackages(manifest) : [])};

/** The twenty-one pages: the companion's catalogue, and the pages zones B and C of every face draw from. */
export const MODULES = ${json(MODULE_CATALOGUE)} as const;

/** Zone A's pages. */
export const ZONE_A_PAGES: SitePage[] = ${json(pages(ZONE_A_PAGES))};

/** Band D's pages. */
export const BAND_D_PAGES: SitePage[] = ${json(pages(BAND_D_PAGES))};

/** The fields the bar's ends can carry. */
export const BAR_FIELDS: SitePage[] = ${json(pages(BAR_FIELDS))};

/** The pages a standard pit wall zone can show. */
export const PIT_WALL_ZONE_PAGES: SitePage[] = ${json(pages(PIT_WALL_ZONE_PAGES))};

/** The pages the wide pit wall zone can show. */
export const PIT_WALL_WIDE_ZONE_PAGES: SitePage[] = ${json(pages(PIT_WALL_WIDE_ZONE_PAGES))};

/** The pit wall's pages, and the zones each one places. */
export const PIT_WALL_PAGES = ${json(PIT_WALL_PAGES)} as const;

/** The flags, ranked: the first one that is out takes the surface. */
export const FLAGS: SiteFlag[] = ${json(flags(FLAG_CATALOGUE))};

/** Every strip shape the build writes a profile for. */
export const STRIP_SHAPES: SiteStripShape[] = ${json(stripShapes(ALL_SHAPES, LEGACY_SHAPES))};

/** The LED profile files the build wrote. Empty when the repository has not been built. */
export const LED_PROFILES: string[] = ${json(manifest?.ledProfiles ?? [])};

/** The base face and the rectangles of its parts, for the anatomy explainer. */
export const HERO_FACE: SiteFace = ${json(heroFace(BASE_FACE))};

/** What there is to download. Empty when the packages have not been built into build/. */
export const DOWNLOADS: Download[] = ${json(downloads(buildDir))};

/** The release history, newest first, cut from CHANGELOG.md. */
export const RELEASES: Release[] = ${json(releases(changelog))};
`;
  writeFileSync(outPath, body);
  const packages = manifest ? sitePackages(manifest).length : 0;
  console.log(
    `wrote lib/content.generated.ts (${packages} packages, ${MODULE_CATALOGUE.length} pages, ${ALL_SHAPES.length} strip shapes, ${downloads(buildDir).length} downloads, ${releases(changelog).length} releases)`,
  );
}
