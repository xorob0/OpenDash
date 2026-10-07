/**
 * The themes a branch touches, which is what a pull request builds (#201) and what the conformance
 * harness checks (#200): one rule for both, so that the packages CI builds are the faces it checked.
 */
import { spawnSync } from 'node:child_process';
import { DEFAULT_THEME_ID, THEME_CATALOGUE } from '../contract.ts';
import { THEMES } from './index.ts';

/** What a branch is compared against to find the themes it touched. */
export const TOUCHED_BASE = 'origin/main';

const THEMES_DIR = 'packages/dash/src/themes/';

/** A set of themes, and why those, which a run prints so that a log says which one it was. */
export interface Selection {
  ids: string[];
  why: string;
}

/** Every catalogued theme with code; one with no code yet has nothing to build or check. */
export const themesWithCode = (): string[] => THEME_CATALOGUE.map((theme) => theme.id).filter((id) => Object.hasOwn(THEMES, id));

/**
 * The default and the themes the branch touched against {@link TOUCHED_BASE}.
 *
 * A theme is touched when a file under its own directory differs from the base, untracked files
 * included. A file of the machinery itself, directly under `themes/`, touches every theme, and so
 * does a base that cannot be read: a shallow checkout has no `origin/main`, and a run that cannot
 * tell what changed takes everything rather than guessing. A change outside `themes/`, to a module
 * say, touches no theme here, which is what `--all-themes` and the full matrix are for.
 */
export function touchedThemes(): Selection {
  const all = themesWithCode();
  const git = (args: string[]): string[] | undefined => {
    const run = spawnSync('git', args, { cwd: import.meta.dir, encoding: 'utf8' });
    return run.status === 0 ? run.stdout.split('\n').filter(Boolean) : undefined;
  };
  const changed = git(['diff', '--name-only', TOUCHED_BASE, '--', `:/${THEMES_DIR}`]);
  const added = git(['ls-files', '--others', '--exclude-standard', '--full-name', '--', `:/${THEMES_DIR}`]);
  if (changed === undefined || added === undefined) return { ids: all, why: `${TOUCHED_BASE} cannot be read, so every theme` };
  const touched = new Set<string>();
  for (const file of [...changed, ...added]) {
    const [first, ...rest] = file.slice(THEMES_DIR.length).split('/');
    if (rest.length === 0) return { ids: all, why: `the branch changes ${file}, which every theme is built by` };
    touched.add(first!);
  }
  const ids = all.filter((id) => id === DEFAULT_THEME_ID || touched.has(id));
  return { ids, why: touched.size === 0 ? `the default alone: the branch touches no theme against ${TOUCHED_BASE}` : `the default and the themes the branch touches against ${TOUCHED_BASE}` };
}
