/**
 * The built packages, read the way the demo reads them, for the demo's tests. Not a test itself.
 *
 * Null when `build/` is absent or from another version, which `readBuildManifest` says once; the
 * tests that need a build then skip with a message, as `captures.test.ts` does.
 */
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { readBuildManifest } from '../scripts/content.ts';
import { demoEntries, demoGroup } from '../scripts/demo-data.ts';
import type { DemoGroup } from '../lib/demo/types.ts';
import { parseDashboard, type SceneDashboard } from '../lib/demo/scene.ts';

export const repoRoot = path.resolve(import.meta.dir, '..', '..');

export interface BuiltFace {
  folder: string;
  group: DemoGroup;
  theme: string;
  width: number;
  height: number;
  main: string;
  library: Map<string, SceneDashboard>;
  /** The documents as JSON, for a test that counts what the parser kept. */
  raw: Map<string, unknown>;
}

const manifest = readBuildManifest(repoRoot);

export const HAS_BUILD = manifest !== null;

export const builtFaces = (): BuiltFace[] =>
  manifest
    ? demoEntries(manifest).map((entry) => {
        const dir = path.join(repoRoot, 'build', entry.folder);
        const library = new Map<string, SceneDashboard>();
        const raw = new Map<string, unknown>();
        for (const file of readdirSync(dir).filter((f) => f.endsWith('.djson'))) {
          const json = JSON.parse(readFileSync(path.join(dir, file), 'utf8').replace(/^﻿/, '')) as unknown;
          raw.set(file, json);
          library.set(file, parseDashboard(json, file));
        }
        return { folder: entry.folder, group: demoGroup(entry)!, theme: entry.theme ?? 'default', width: entry.width, height: entry.height, main: `${entry.folder}.djson`, library, raw };
      })
    : [];
