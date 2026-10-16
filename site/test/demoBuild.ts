/**
 * The built packages, read the way the demo reads them, for the demo's tests. Not a test itself.
 *
 * Null when `build/` is absent or from another version, which `readBuildManifest` says once; the
 * tests that need a build then skip with a message, as `captures.test.ts` does.
 */
import path from 'node:path';
import { readBuildManifest } from '../scripts/content.ts';
import { demoEntries, demoGroup, packageFiles } from '../scripts/demo-data.ts';
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
        const library = new Map<string, SceneDashboard>();
        const raw = new Map<string, unknown>();
        // Through the demo's own reader, which takes the zip when the unpacked folder is absent: CI's
        // Website job has only the zips.
        for (const [file, data] of packageFiles(entry)) {
          if (!file.endsWith('.djson')) continue;
          const json = JSON.parse(data.toString('utf8').replace(/^\ufeff/, '')) as unknown;
          raw.set(file, json);
          library.set(file, parseDashboard(json, file));
        }
        return { folder: entry.folder, group: demoGroup(entry)!, theme: entry.theme ?? 'default', width: entry.width, height: entry.height, main: `${entry.folder}.djson`, library, raw };
      })
    : [];
