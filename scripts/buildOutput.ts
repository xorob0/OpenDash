/**
 * build/ as the tests find it.
 *
 * `bun run check` does not build, so a checkout may hold no build/ at all, which is the ordinary state
 * of a fresh clone, or one left by another commit, which a branch switch does not refresh. On a laptop
 * a test that reads build/ is skipped in either case, and the report says skipped rather than passed.
 *
 * CI is the other way about. Both workflows build every theme before `bun test` runs, so there a
 * build/ that is missing or from another version is the build having gone wrong, and a test that
 * reads it fails rather than skips. Until #909 CI tested first and built second, and every one of
 * these tests was skipped, or passed having asserted nothing, on every run that gated a merge.
 * `OnCI` in the plugin's tests is the same rule on the .NET side.
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

const ROOT = path.resolve(import.meta.dir, '..');

/** Whether this is a CI run, where the build has run before the tests. GitHub Actions sets both. */
export const ON_CI = Boolean(process.env.CI) || Boolean(process.env.GITHUB_ACTIONS);

/** Whether a test that reads build/ runs: whenever what it reads is there, and on CI whether or not it is. */
export const withBuild = (present: boolean): boolean => present || ON_CI;

/** Fails a test that reads build/ when what it reads is not there, saying what is missing and why on CI that is a failure. */
export function requireBuild(present: boolean, missing: string): void {
  if (present) return;
  throw new Error(
    `${missing}. ` +
      (ON_CI
        ? 'CI runs `bun run build --all-themes` before `bun test`, so here that is the build having gone wrong rather than a checkout that has not built (#909).'
        : 'Run `bun run build --all-themes` and try again.'),
  );
}

/** What a test read from build/, or the failure `requireBuild` gives when there was nothing to read. */
export function built<T>(found: T | null | undefined, missing = 'build/manifest.json is absent, or carries another version than VERSION'): T {
  requireBuild(found !== null && found !== undefined, missing);
  return found as T;
}

/**
 * build/manifest.json, or null when there is not one this checkout can trust: when there is none, or
 * when it carries another version than VERSION, which is build output a branch switch left behind.
 * A stale manifest names packages that no longer exist and misses the ones that do, so the tests
 * reading it would fail by the handful for a tree where nothing is wrong; null skips them instead,
 * except on CI, where `withBuild` runs them and `built` names the cause.
 */
export function readBuiltManifest<T extends { version?: string }>(repoRoot = ROOT, warn: (message: string) => void = console.warn): T | null {
  const file = path.join(repoRoot, 'build', 'manifest.json');
  if (!existsSync(file)) return null;
  const manifest = JSON.parse(readFileSync(file, 'utf8')) as T;
  const version = readFileSync(path.join(repoRoot, 'VERSION'), 'utf8').trim();
  if (manifest.version === version) return manifest;
  warn(
    `build/manifest.json carries version ${manifest.version}, whereas VERSION says ${version}. ` +
      (ON_CI
        ? 'CI built it from this commit, so the tests that read it fail rather than skip.'
        : 'That is build output which a branch switch does not refresh, so the tests that read it are ' +
          'being skipped rather than failed: run `bun run build` and try again.'),
  );
  return null;
}
