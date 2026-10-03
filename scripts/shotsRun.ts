/**
 * What a capture run writes beside its pictures: `run.json`, the provenance the website's sidecar
 * is built from.
 *
 * A capture presented as the product has to say which product: which version, which commit,
 * whether the tree was clean, which emulator scenario, and how many laps SimHub had seen when the
 * picture was taken. `bun run shots` and `bun run modules` write it; `site/scripts/sync-shots.ts`
 * reads it and merges it into `site/public/shots/captures.json`, which the site shows under its
 * pictures. The reshoot in #375 is what made this necessary: the site said three times that none of
 * its pictures was a mock-up, and every one of them was a version old.
 *
 * One file per run, and one entry per picture. The tree is the run's, so the version, commit and
 * date are said once; the scenario is said by every capture, because a run of `bun run shots
 * --scenarios a,b` takes pictures under more than one (#627). Schema 1 said the scenario once, for
 * the whole file, and `shots` wrote a fresh file per scenario, so every picture but the last
 * scenario's lost its record; `readRun` still reads one, giving each capture the file's scenario.
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
  /** The emulator scenario that was running when the picture was taken, or null for none. */
  scenario: string | null;
  width: number;
  height: number;
  /** Laps the emulator had completed when the picture was taken; null for a picture of the panel. */
  lapsSeen: number | null;
}

export interface RunRecord {
  schema: 2;
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
export function provenance(): Omit<RunRecord, 'captures'> {
  return {
    schema: 2,
    version: readVersion(),
    commit: git('rev-parse', '--short', 'HEAD'),
    dirty: git('status', '--porcelain') !== '',
    date: new Date().toISOString().slice(0, 10),
    simHubVersion: simHubVersion(),
  };
}

export const writeRun = (outDir: string, run: RunRecord): void => writeFileSync(path.join(outDir, RUN_FILE), `${JSON.stringify(run, null, 2)}\n`);

/** A schema 1 record: one scenario for the file, none on its captures. */
type RunRecordV1 = Omit<RunRecord, 'schema' | 'captures'> & { schema: 1; scenario: string | null; captures: Record<string, Omit<RunCapture, 'scenario'>> };

/** A record of either schema, as the current one: a schema 1 file's scenario goes onto each capture. */
export function upgradeRun(raw: RunRecord | RunRecordV1): RunRecord {
  if (raw.schema === 2) return raw;
  const { scenario, captures, schema: _one, ...rest } = raw;
  return { schema: 2, ...rest, captures: Object.fromEntries(Object.entries(captures).map(([file, c]) => [file, { ...c, scenario }])) };
}

export function readRun(dir: string): RunRecord | null {
  const file = path.join(dir, RUN_FILE);
  if (!existsSync(file)) return null;
  return upgradeRun(JSON.parse(readFileSync(file, 'utf8')) as RunRecord | RunRecordV1);
}
