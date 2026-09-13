#!/usr/bin/env bun
/**
 * affected: which packages a change reaches, computed from the built output.
 *
 * By ADR 0002 the `.djson` is build output, so the TypeScript a reviewer reads sits one remove
 * from the thing that actually changed, and a card is shared: an edit to one of them reaches every
 * face that card appears on. The files a diff touches therefore answer the wrong question. This
 * builds the branch and the commit it forked from, and compares the packages the two builds wrote.
 *
 * The comparison is a byte comparison, which is only meaningful because the packages are
 * reproducible: `writePackage` sorts the zip entries and stamps the same fixed timestamp on every
 * one, so two builds of one tree give identical bytes. See packages/generator/src/package.ts.
 *
 * By ADR 0008 there is no preview renderer, so nothing here draws a dashboard. What this produces
 * is the list a reviewer needs in order to know which faces to look at, and the `bun run shots`
 * command that captures exactly those on the VM.
 *
 * The report goes to stdout and progress goes to stderr, so that CI can capture one without the
 * other. Nothing on stdout means no package output moved.
 */
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { copyFileSync, existsSync, mkdirSync, readFileSync, rmSync, symlinkSync } from 'node:fs';
import path from 'node:path';

const repoRoot = path.resolve(import.meta.dir, '..');

/** Where the base checkout and the two builds go. Under build/, which is gitignored. */
export const WORK_DIR = path.join(repoRoot, 'build', 'affected');
/** The build's own statement of what it produced; read rather than globbed, so a missing zip is an error. */
const MANIFEST_FILE = 'manifest.json';
/**
 * Identifies the comment this report owns, so that CI edits its own comment rather than adding
 * one per push. Invisible when GitHub renders the body.
 */
export const COMMENT_MARKER = '<!-- opendash:affected -->';

export const USAGE = `affected: which packages a change reaches, computed from the built output.

  bun run affected [--base <ref>] [--keep]

  --base   what to compare against; default origin/main. The comparison is against the merge
           base, so a branch is judged by what it changed rather than by how far main has moved.
  --keep   leave build/affected in place, to look at the two builds by hand
  --help   print this text

It writes the report to stdout and prints nothing at all when no package output moved.
`;

export interface AffectedOptions {
  base: string;
  keep: boolean;
}

/** One package as the build left it: the folder SimHub imports it under, and the bytes of its zip. */
export type Digest = ReadonlyMap<string, string>;

export interface Comparison {
  added: readonly string[];
  removed: readonly string[];
  changed: readonly string[];
  unchanged: readonly string[];
}

interface ManifestEntry {
  folder: string;
  file: string;
}

export function parseArgs(argv: readonly string[]): AffectedOptions | { help: true } {
  if (argv.includes('--help') || argv.includes('-h')) return { help: true };
  const flagValue = (name: string): string | undefined => {
    const index = argv.indexOf(`--${name}`);
    if (index >= 0 && argv[index + 1] && !argv[index + 1]!.startsWith('--')) return argv[index + 1];
    return argv.find((a) => a.startsWith(`--${name}=`))?.split('=').slice(1).join('=');
  };
  return { base: flagValue('base') ?? 'origin/main', keep: argv.includes('--keep') };
}

const progress = (line: string): void => console.error(line);

/** Runs a command and throws with its stderr, which is the only part of a failure worth reading. */
function run(argv: readonly string[], cwd: string): string {
  const r = spawnSync(argv[0]!, argv.slice(1), { cwd, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  if (r.error) throw r.error;
  if (r.status !== 0) throw new Error(`${argv.join(' ')} failed in ${cwd}:\n${(r.stderr || r.stdout || '').trim()}`);
  return r.stdout;
}

/**
 * Reads a build's output into folder name and hash.
 *
 * The hash covers the `.simhubdash` rather than the unzipped folder because the zip is what SimHub
 * imports and what a release ships, and it already contains every file the folder holds.
 */
export function digest(outDir: string): Digest {
  const manifest = JSON.parse(readFileSync(path.join(outDir, MANIFEST_FILE), 'utf8')) as { packages: ManifestEntry[] };
  const digests = new Map<string, string>();
  for (const entry of manifest.packages) {
    const bytes = readFileSync(path.join(outDir, entry.file));
    digests.set(entry.folder, createHash('sha256').update(bytes).digest('hex'));
  }
  return digests;
}

/** What moved between two builds. Sorted, so that a report of the same change reads the same twice. */
export function compare(base: Digest, head: Digest): Comparison {
  const added: string[] = [];
  const changed: string[] = [];
  const unchanged: string[] = [];
  for (const [folder, hash] of head) {
    const before = base.get(folder);
    if (before === undefined) added.push(folder);
    else if (before === hash) unchanged.push(folder);
    else changed.push(folder);
  }
  const removed = [...base.keys()].filter((folder) => !head.has(folder));
  const sorted = (names: string[]): string[] => names.sort((a, b) => a.localeCompare(b));
  return { added: sorted(added), removed: sorted(removed), changed: sorted(changed), unchanged: sorted(unchanged) };
}

/**
 * Puts the two modules the build imports by name within reach of a fresh checkout, so that the base
 * can be built without an install.
 *
 * The `@opendash/generator` link is deliberately relative, and copying it wrong is the one way this
 * whole comparison can quietly lie. Pointed at the working tree's generator, the base build would
 * use the branch's serialiser, a change confined to the generator would produce identical bytes on
 * both sides, and the report would say that nothing moved. Relative, it resolves inside the base
 * checkout and each side is built by its own code. `fflate` is third-party and identical either
 * way, so that one is shared.
 */
function linkDependencies(tree: string): void {
  const fflate = path.join(repoRoot, 'packages', 'generator', 'node_modules', 'fflate');
  if (!existsSync(fflate)) throw new Error(`${fflate} is missing; run \`bun install\` before comparing`);

  const generatorModules = path.join(tree, 'packages', 'generator', 'node_modules');
  mkdirSync(generatorModules, { recursive: true });
  symlinkSync(fflate, path.join(generatorModules, 'fflate'), 'dir');

  const dashScope = path.join(tree, 'packages', 'dash', 'node_modules', '@opendash');
  mkdirSync(dashScope, { recursive: true });
  symlinkSync(path.join('..', '..', '..', 'generator'), path.join(dashScope, 'generator'), 'dir');
}

/**
 * Checks the base out beside the branch and builds it.
 *
 * VERSION is taken from the branch rather than from the base, because the version string reaches
 * every package's metadata: left alone, a release commit would report all twenty-two packages as
 * changed and say nothing about what a reviewer should look at.
 */
function buildBase(sha: string, outDir: string): Digest {
  const tree = path.join(WORK_DIR, 'base-tree');
  // A run killed before its cleanup leaves the checkout registered although the directory is gone,
  // and git then refuses to add the same path again. Pruning first makes a second run work.
  run(['git', 'worktree', 'prune'], repoRoot);
  run(['git', 'worktree', 'add', '--detach', tree, sha], repoRoot);
  try {
    linkDependencies(tree);
    copyFileSync(path.join(repoRoot, 'VERSION'), path.join(tree, 'VERSION'));
    run(['bun', 'packages/dash/src/build.ts', '--out', outDir], tree);
    return digest(outDir);
  } finally {
    run(['git', 'worktree', 'remove', '--force', tree], repoRoot);
  }
}

/** The report body, or an empty string when no package output moved. */
export function report(comparison: Comparison, baseSha: string): string {
  const { added, changed, removed } = comparison;
  const moved = added.length + changed.length + removed.length;
  if (moved === 0) return '';

  const lines = [
    COMMENT_MARKER,
    `### ${moved} package${moved === 1 ? '' : 's'} to look at`,
    '',
    ...changed.map((folder) => `- \`${folder}\``),
    ...added.map((folder) => `- \`${folder}\` (new)`),
    ...removed.map((folder) => `- \`${folder}\` (removed)`),
    '',
    'This list is computed from the packages the build wrote rather than from the files the diff',
    'touches, because a card is shared and an edit to one of them reaches every face it appears on.',
    `Compared against \`${baseSha.slice(0, 12)}\`.`,
  ];

  // Capturing something the build no longer produces is not possible, so a removal is reported
  // without being offered to the VM.
  const capturable = [...changed, ...added];
  if (capturable.length > 0) {
    lines.push(
      '',
      // Not a markdown link: a relative path in a pull request comment resolves against the
      // repository root rather than against a tree, and lands on a 404.
      'By ADR 0008 (`docs/decisions/0008-how-a-pull-request-renders-a-dash.md`) there is no preview',
      'renderer and CI draws nothing: the capture is native and the author takes it.',
      '',
      '```bash',
      `bun run shots --packages '${capturable.join(',')}'`,
      '```',
    );
  }
  return `${lines.join('\n')}\n`;
}

export function affected(opts: AffectedOptions): Comparison {
  const baseSha = run(['git', 'merge-base', 'HEAD', opts.base], repoRoot).trim();
  if (baseSha === '') throw new Error(`no merge base between HEAD and ${opts.base}`);

  rmSync(WORK_DIR, { recursive: true, force: true });
  mkdirSync(WORK_DIR, { recursive: true });
  try {
    progress(`building ${baseSha.slice(0, 12)}`);
    const base = buildBase(baseSha, path.join(WORK_DIR, 'base'));
    progress('building the branch');
    const headOut = path.join(WORK_DIR, 'head');
    run(['bun', 'packages/dash/src/build.ts', '--out', headOut], repoRoot);
    const head = digest(headOut);

    const comparison = compare(base, head);
    const { changed, added, removed, unchanged } = comparison;
    progress(`${changed.length} changed, ${added.length} new, ${removed.length} removed, ${unchanged.length} untouched`);
    process.stdout.write(report(comparison, baseSha));
    return comparison;
  } finally {
    if (!opts.keep) rmSync(WORK_DIR, { recursive: true, force: true });
  }
}

if (import.meta.main) {
  const opts = parseArgs(process.argv.slice(2));
  if ('help' in opts) console.log(USAGE);
  else affected(opts);
}
