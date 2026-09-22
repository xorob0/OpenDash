/**
 * What a capture run writes beside its pictures: `run.json`, the provenance the website's sidecar
 * is built from.
 *
 * A capture presented as the product has to say which product: which version, which commit,
 * whether the tree was clean, which emulator scenario, and how many laps SimHub had seen when the
 * picture was taken. `bun run shots` and `bun scripts/modules.ts` write it; `site/scripts/sync-shots.ts`
 * reads it and merges it into `site/public/shots/captures.json`, which the site shows under its
 * pictures. The reshoot in #375 is what made this necessary: the site said three times that none of
 * its pictures was a mock-up, and every one of them was a version old.
 */
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { readVersion } from '../packages/dash/src/build.ts';

const repoRoot = path.resolve(import.meta.dir, '..');

export interface RunCapture {
  kind: 'package' | 'page' | 'panel';
  package?: string;
  page?: string;
  panel?: string;
  width: number;
  height: number;
  /** Laps the emulator had completed when the picture was taken; null for a picture of the panel. */
  lapsSeen: number | null;
}

export interface RunRecord {
  schema: 1;
  scenario: string | null;
  version: string;
  commit: string;
  dirty: boolean;
  /** ISO date. */
  date: string;
  simHubVersion: string;
  captures: Record<string, RunCapture>;
}

export const RUN_FILE = 'run.json';

const git = (...args: string[]): string => {
  const r = Bun.spawnSync(['git', ...args], { cwd: repoRoot, stdout: 'pipe', stderr: 'pipe' });
  return r.exitCode === 0 ? new TextDecoder().decode(r.stdout).trim() : '';
};

/** The build's SimHub version, from the manifest, or the default the generator stamps. */
export function simHubVersion(): string {
  const manifest = path.join(repoRoot, 'build', 'manifest.json');
  if (!existsSync(manifest)) return '9.12.6';
  try {
    return (JSON.parse(readFileSync(manifest, 'utf8')) as { simHubVersion?: string }).simHubVersion ?? '9.12.6';
  } catch {
    return '9.12.6';
  }
}

/** The provenance of a run started now, before any picture is taken. */
export function provenance(scenario: string | null): Omit<RunRecord, 'captures'> {
  return {
    schema: 1,
    scenario,
    version: readVersion(),
    commit: git('rev-parse', '--short', 'HEAD'),
    dirty: git('status', '--porcelain') !== '',
    date: new Date().toISOString().slice(0, 10),
    simHubVersion: simHubVersion(),
  };
}

export const writeRun = (outDir: string, run: RunRecord): void => writeFileSync(path.join(outDir, RUN_FILE), `${JSON.stringify(run, null, 2)}\n`);

export function readRun(dir: string): RunRecord | null {
  const file = path.join(dir, RUN_FILE);
  if (!existsSync(file)) return null;
  return JSON.parse(readFileSync(file, 'utf8')) as RunRecord;
}
