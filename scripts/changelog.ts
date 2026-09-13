#!/usr/bin/env bun
/**
 * changelog: the section of CHANGELOG.md for one version, which is what a release body says.
 *
 * The release used to publish GitHub's generated summary, which is a list of pull request titles
 * and reads as a list of pull request titles. CHANGELOG.md is written for a driver rather than for
 * a reviewer, and it is already the file README.md points at, so it is the one that should reach
 * the release page. This extracts it, and refuses a tag that has no section, because a release
 * whose notes are the previous release's notes is worse than a release with none.
 *
 *   bun scripts/changelog.ts 0.1.0-rc.3 [--out release-body.md]
 */
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const repoRoot = path.resolve(import.meta.dir, '..');

/** Where the sections live. One `## <version> (<date>)` heading each, newest first. */
export const CHANGELOG_FILE = path.join(repoRoot, 'CHANGELOG.md');

/** A leading `v` is the tag's, not the version's. */
export const versionOf = (tagOrVersion: string): string => tagOrVersion.trim().replace(/^v/, '');

/**
 * The body under `## <version>`, without its own heading, or null when there is no such section.
 *
 * The heading is matched on the version alone so that the date beside it is free to be whatever
 * the release was actually cut on, and the section ends at the next `## ` at the start of a line,
 * which is the next release.
 */
export const sectionFor = (markdown: string, tagOrVersion: string): string | null => {
  const version = versionOf(tagOrVersion);
  const lines = markdown.split('\n');
  const start = lines.findIndex((line) => line.startsWith('## ') && versionOf(line.slice(3).trim().split(/\s+/)[0] ?? '') === version);
  if (start === -1) return null;
  const rest = lines.slice(start + 1);
  const end = rest.findIndex((line) => line.startsWith('## '));
  const body = (end === -1 ? rest : rest.slice(0, end)).join('\n').trim();
  return body === '' ? null : body;
};

/** Every version the changelog has a section for, in the order they appear. */
export const versionsIn = (markdown: string): string[] =>
  markdown
    .split('\n')
    .filter((line) => line.startsWith('## '))
    .map((line) => line.slice(3).trim().split(/\s+/)[0] ?? '')
    .filter((v) => v !== '');

export interface Options {
  version: string;
  out?: string;
}

export const parseArgs = (argv: readonly string[]): Options | { help: true } => {
  const rest = [...argv];
  if (rest.includes('--help') || rest.includes('-h')) return { help: true };
  let out: string | undefined;
  const at = rest.indexOf('--out');
  if (at !== -1) {
    out = rest[at + 1];
    if (out === undefined) throw new Error('--out needs a file name');
    rest.splice(at, 2);
  }
  const version = rest[0];
  if (version === undefined || version === '') throw new Error('a version or tag is needed, e.g. v0.1.0-rc.3');
  return { version, out };
};

export const USAGE = 'usage: bun scripts/changelog.ts <version|tag> [--out <file>]';

/**
 * Where `main` writes. Injectable for one reason: the `::error::` prefix below is a GitHub Actions
 * *workflow command*, and GitHub promotes it to a red annotation on the run **whatever the exit
 * code is**. A test that exercises the failure path by calling `main` therefore painted a red X on
 * every green CI run, which is exactly as good as no annotation at all — a red mark that does not
 * mean red teaches people to ignore red marks. The test passes its own sink and asserts the
 * message instead of emitting it.
 */
export interface MainIO {
  out: (line: string) => void;
  err: (line: string) => void;
}

const CONSOLE_IO: MainIO = {
  out: (line) => console.log(line),
  err: (line) => console.error(line),
};

export function main(argv: readonly string[], io: MainIO = CONSOLE_IO): number {
  let opts: Options | { help: true };
  try {
    opts = parseArgs(argv);
  } catch (e) {
    io.err(`changelog: ${e instanceof Error ? e.message : String(e)}`);
    io.err(USAGE);
    return 2;
  }
  if ('help' in opts) {
    io.out(USAGE);
    return 0;
  }
  const markdown = readFileSync(CHANGELOG_FILE, 'utf8');
  const body = sectionFor(markdown, opts.version);
  if (body === null) {
    // ::error:: so that the failure is annotated on the workflow run rather than buried in a log,
    // matching the tag-against-VERSION check beside it. See MainIO for why it is injectable.
    io.err(`::error::CHANGELOG.md has no section for ${versionOf(opts.version)}; it has ${versionsIn(markdown).join(', ') || 'none'}`);
    return 1;
  }
  if (opts.out) writeFileSync(opts.out, `${body}\n`, 'utf8');
  else io.out(body);
  return 0;
}

if (import.meta.main) process.exit(main(process.argv.slice(2)));
