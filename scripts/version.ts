#!/usr/bin/env bun
/**
 * version: the one way the project's version changes, and the check that it changed that way.
 *
 * The number lives in VERSION, and the build, the plugin assembly (plugin/Directory.Build.props),
 * the site and the release tag all read it from there. The release notes live in CHANGELOG.md under
 * a `## <version> (<date>)` heading, and the release body is that section (scripts/changelog.ts).
 * The two were edited by hand, one after the other, in every cut. This writes both at once, and
 * `--check` fails when they disagree, which CI runs on every pull request rather than waiting for
 * a tag to find out.
 *
 * It is not cosmetic. The plugin installs its embedded dashboards only when their version is newer
 * than the installed one (Versioning.Decide), so a cut whose number does not move, or moves
 * backwards, leaves every rig on its old dashboard with nothing to say why. The command therefore
 * refuses a version that the plugin would not rank above the current one, using the plugin's own
 * ordering, which the test holds to the plugin's test cases.
 *
 *   bun run version 0.3.0-rc.8 [--date 2026-09-28]   VERSION and a new heading at the top
 *   bun run version --check                           VERSION agrees with the changelog
 *   bun run version --check --base <ref>              ...and ranks no lower than the base's
 *
 * A new heading has no notes under it, and an empty section is no section (changelog.ts), so the
 * check fails until the notes are written: a cut is not finished while its release body is empty.
 */
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { VERSION_FILE, VERSION_PATTERN } from '../packages/dash/src/build.ts';
import { CHANGELOG_FILE, sectionFor, versionOf } from './changelog.ts';

// ---------------------------------------------------------------------------------------------
// The ordering. A transcription of Versioning.VersionCompare in plugin/OpenDash/Versioning.cs,
// because "newer" has to mean what the plugin that decides whether to install means by it.
// ---------------------------------------------------------------------------------------------

interface Parsed {
  core: number[];
  /** The identifiers after `-`; null for a release. */
  preRelease: string[] | null;
}

const isDigit = (c: string): boolean => c >= '0' && c <= '9';

const INT32_MAX = 2147483647;

const parse = (version: string): Parsed => {
  let text = version.trim();
  if (text.startsWith('v') || text.startsWith('V')) text = text.slice(1);
  const plus = text.indexOf('+');
  if (plus >= 0) text = text.slice(0, plus); // build metadata carries no precedence
  let preRelease: string[] | null = null;
  const dash = text.indexOf('-');
  if (dash >= 0) {
    const rest = text.slice(dash + 1);
    if (rest.length > 0) preRelease = rest.split('.');
    text = text.slice(0, dash);
  }
  const core: number[] = [];
  for (const part of text.split('.')) {
    let digits = 0;
    while (digits < part.length && isDigit(part[digits]!)) digits++;
    // int.TryParse: a segment past Int32.MaxValue fails to parse, and the plugin reads it as 0.
    const value = digits > 0 ? Number(part.slice(0, digits)) : 0;
    core.push(value <= INT32_MAX ? value : 0);
    if (digits < part.length) break; // "0rc1": stop at the first segment that is not a number
  }
  return { core, preRelease };
};

/** Two all-digit strings by value, without overflowing: leading zeros off, then length, then text. */
const compareNumeric = (a: string, b: string): number => {
  const l = a.replace(/^0+/, '');
  const r = b.replace(/^0+/, '');
  if (l.length !== r.length) return l.length - r.length;
  return l < r ? -1 : l > r ? 1 : 0;
};

const compareOrdinal = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/** The index one past the run of digits, or of non-digits, that starts at `start`. */
const runEnd = (text: string, start: number): number => {
  const digits = isDigit(text[start]!);
  let end = start + 1;
  while (end < text.length && isDigit(text[end]!) === digits) end++;
  return end;
};

/** Digit and non-digit runs in step, so that "rc10" is newer than "rc2", as the plugin forgives. */
const compareRuns = (a: string, b: string): number => {
  let i = 0;
  let j = 0;
  while (i < a.length && j < b.length) {
    const leftEnd = runEnd(a, i);
    const rightEnd = runEnd(b, j);
    const l = a.slice(i, leftEnd);
    const r = b.slice(j, rightEnd);
    const order = isDigit(l[0]!) && isDigit(r[0]!) ? compareNumeric(l, r) : compareOrdinal(l, r);
    if (order !== 0) return order;
    i = leftEnd;
    j = rightEnd;
  }
  return a.length - i - (b.length - j); // equal so far: whichever still has characters is newer
};

const isNumeric = (s: string): boolean => s.length > 0 && /^\d+$/.test(s);

const compareIdentifier = (a: string, b: string): number => {
  const ln = isNumeric(a);
  const rn = isNumeric(b);
  if (ln && rn) return compareNumeric(a, b);
  if (ln !== rn) return ln ? -1 : 1; // numeric identifiers rank below alphanumeric ones
  return compareRuns(a, b);
};

const comparePreRelease = (a: string[] | null, b: string[] | null): number => {
  if (a === null) return b === null ? 0 : 1; // a release outranks its pre-releases
  if (b === null) return -1;
  for (let i = 0; i < Math.min(a.length, b.length); i++) {
    const order = compareIdentifier(a[i]!, b[i]!);
    if (order !== 0) return order;
  }
  return a.length - b.length;
};

/**
 * Negative when `a` is older than `b`, zero when they rank the same, positive when `a` is newer,
 * exactly as the plugin orders an embedded version against an installed one. Null or blank is the
 * oldest.
 */
export const compareVersions = (a: string | null, b: string | null): number => {
  const leftEmpty = a === null || a.trim() === '';
  const rightEmpty = b === null || b.trim() === '';
  if (leftEmpty || rightEmpty) return leftEmpty === rightEmpty ? 0 : leftEmpty ? -1 : 1;
  const left = parse(a);
  const right = parse(b);
  for (let i = 0; i < Math.max(left.core.length, right.core.length); i++) {
    const l = left.core[i] ?? 0;
    const r = right.core[i] ?? 0;
    if (l !== r) return l - r;
  }
  return comparePreRelease(left.preRelease, right.preRelease);
};

// ---------------------------------------------------------------------------------------------
// The changelog's headings.
// ---------------------------------------------------------------------------------------------

/** `## 0.3.0-rc.7 (2026-09-27)`: the version, then the day the release was cut. */
const HEADING = /^## (\S+) \((\d{4}-\d{2}-\d{2})\)$/;

export interface Heading {
  /** The line it is on, counted from 0. */
  line: number;
  text: string;
  /** The first word after `## `, which is what changelog.ts matches a tag against. */
  version: string;
  /** Null when the heading is not in the `(<date>)` shape, or its date is not a day the calendar has. */
  date: string | null;
}

/** True for a day the calendar has: 2026-02-29 and 2026-99-99 are the shape of a date, not dates. */
export const isDate = (text: string): boolean => {
  const shaped = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text);
  if (!shaped) return false;
  const [year, month, day] = [Number(shaped[1]), Number(shaped[2]), Number(shaped[3])];
  const at = new Date(Date.UTC(year, month - 1, day));
  return at.getUTCFullYear() === year && at.getUTCMonth() === month - 1 && at.getUTCDate() === day;
};

/** Every release heading, newest first as the file gives them. The same lines changelog.ts reads. */
export const headingsIn = (markdown: string): Heading[] =>
  markdown.split('\n').flatMap((text, line) => {
    if (!text.startsWith('## ')) return [];
    const version = text.slice(3).trim().split(/\s+/)[0] ?? '';
    const shaped = HEADING.exec(text.trimEnd());
    const date = shaped?.[2];
    return [{ line, text, version, date: date !== undefined && isDate(date) ? date : null }];
  });

/** A date as a heading carries it, in the local calendar, which is the day the author cut it on. */
export const today = (now: Date = new Date()): string =>
  `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;

// ---------------------------------------------------------------------------------------------
// Changing it.
// ---------------------------------------------------------------------------------------------

export class VersionError extends Error {}

export interface Bumped {
  version: string;
  markdown: string;
  /** True when the section under the heading has no notes yet, which the check will refuse. */
  empty: boolean;
}

/**
 * The changelog as it is once the project is on `next`, cut on `date`.
 *
 * A heading is added above the newest release, or, when the newest heading is already `next`,
 * re-dated with its notes kept, so that running the command again on the day a cut is finished
 * is how its date is corrected. Anything the plugin would not install over `current` is refused.
 */
export const bump = (markdown: string, current: string, requested: string, date: string): Bumped => {
  const next = versionOf(requested);
  if (!VERSION_PATTERN.test(next)) throw new VersionError(`${JSON.stringify(requested)} is not a version like 0.3.0 or 0.3.0-rc.8`);
  if (!isDate(date)) throw new VersionError(`${JSON.stringify(date)} is not a date like 2026-09-28`);

  const order = compareVersions(next, current);
  if (order < 0 || (order === 0 && next !== current)) {
    throw new VersionError(
      `${next} does not rank above ${current}. The plugin installs a dashboard only over an older one, so every rig would keep ${current}.`,
    );
  }

  const heading = `## ${next} (${date})`;
  const lines = markdown.split('\n');
  const headings = headingsIn(markdown);
  const newest = headings[0];

  if (newest?.version === next) {
    lines[newest.line] = heading;
  } else {
    const earlier = headings.find((h) => h.version === next);
    if (earlier) throw new VersionError(`CHANGELOG.md already has ${next}, below ${newest!.version}; a released version is not reused`);
    if (newest && compareVersions(next, newest.version) <= 0) {
      throw new VersionError(`CHANGELOG.md's newest release is ${newest.version}, which ${next} does not rank above`);
    }
    if (newest) lines.splice(newest.line, 0, heading, '');
    else {
      while (lines.length > 0 && lines[lines.length - 1] === '') lines.pop();
      lines.push('', heading, '');
    }
  }
  const updated = lines.join('\n');
  return { version: next, markdown: updated, empty: sectionFor(updated, next) === null };
};

// ---------------------------------------------------------------------------------------------
// Checking it.
// ---------------------------------------------------------------------------------------------

/**
 * Why VERSION and the changelog disagree, one sentence each; empty when they agree.
 *
 * They agree when VERSION holds a version, the changelog's newest heading is that version, dated,
 * with notes under it, and every heading is a dated version that ranks below the one above it.
 *
 * That holds the file to itself and no further. A branch that sets VERSION back and deletes the
 * sections above it agrees with itself; what catches it is `behind`, against the base's VERSION.
 */
export const disagreements = (versionText: string, markdown: string): string[] => {
  const problems: string[] = [];
  const version = versionText.trim();
  if (!VERSION_PATTERN.test(version)) problems.push(`VERSION must hold a version like 0.3.0, got ${JSON.stringify(version)}`);

  const headings = headingsIn(markdown);
  const newest = headings[0];
  if (!newest) {
    problems.push('CHANGELOG.md has no `## <version> (<date>)` heading');
    return problems;
  }
  if (newest.version !== version) {
    const where = headings.find((h) => h.version === version);
    problems.push(
      where
        ? `VERSION is ${version} but CHANGELOG.md's newest release is ${newest.version}; run \`bun run version <x.y.z>\` rather than editing either by hand`
        : `VERSION is ${version} but CHANGELOG.md has no section for it (its newest is ${newest.version}); run \`bun run version ${version}\` and write the notes`,
    );
  } else if (sectionFor(markdown, version) === null) {
    problems.push(`CHANGELOG.md's section for ${version} has no notes under it, and the release body would be empty`);
  }

  for (const h of headings) {
    if (h.date === null) problems.push(`CHANGELOG.md line ${h.line + 1} is not a \`## <version> (<date>)\` heading: ${JSON.stringify(h.text)}`);
    else if (!VERSION_PATTERN.test(h.version)) problems.push(`CHANGELOG.md line ${h.line + 1} names ${JSON.stringify(h.version)}, which is not a version`);
  }
  for (let i = 1; i < headings.length; i++) {
    const above = headings[i - 1]!;
    const below = headings[i]!;
    if (compareVersions(above.version, below.version) <= 0) {
      problems.push(`CHANGELOG.md lists ${above.version} above ${below.version}, which it does not rank above`);
    }
  }
  return problems;
};

/**
 * Why VERSION is behind the base it would merge into, as a sentence; null when it is not.
 *
 * The same number as the base is the usual case, since most pull requests do not cut a release.
 * A lower one is a number moving backwards, which the plugin would never install over the base's,
 * and a string that ranks the same but is spelled differently is a second name for one release.
 */
export const behind = (versionText: string, baseText: string): string | null => {
  const version = versionText.trim();
  const base = baseText.trim();
  const order = compareVersions(version, base);
  if (order > 0 || version === base) return null;
  return order < 0
    ? `VERSION is ${version}, below the base's ${base}; the plugin installs a dashboard only over an older one, so a merge would leave every rig on ${base}`
    : `VERSION is ${version}, which ranks the same as the base's ${base} without being it`;
};

// ---------------------------------------------------------------------------------------------
// The command.
// ---------------------------------------------------------------------------------------------

export const USAGE = 'usage: bun run version <x.y.z[-pre]> [--date YYYY-MM-DD]\n       bun run version --check [--base <ref>]';

export type Options = { help: true } | { check: true; base?: string } | { version: string; date?: string };

export const parseArgs = (argv: readonly string[]): Options => {
  const rest = [...argv];
  if (rest.includes('--help') || rest.includes('-h')) return { help: true };
  if (rest.includes('--check')) {
    rest.splice(rest.indexOf('--check'), 1);
    if (rest.length === 0) return { check: true };
    if (rest[0] === '--base' && rest.length === 2 && rest[1] !== '' && !rest[1]!.startsWith('-')) return { check: true, base: rest[1] };
    throw new Error('--check takes nothing else but --base <ref>');
  }
  let date: string | undefined;
  const at = rest.indexOf('--date');
  if (at !== -1) {
    date = rest[at + 1];
    if (date === undefined) throw new Error('--date needs a day, e.g. 2026-09-28');
    rest.splice(at, 2);
  }
  const unknown = rest.find((a) => a.startsWith('-'));
  if (unknown !== undefined) throw new Error(`unknown option ${unknown}`);
  if (rest.length !== 1 || rest[0] === '') throw new Error('one version is needed, e.g. 0.3.0-rc.8');
  return { version: rest[0]!, date };
};

/**
 * Where `main` reads, writes and prints. Injectable so that the test runs the command on files of
 * its own, and so that the `::error::` the check prints (a workflow command GitHub turns into a red
 * annotation whatever the exit code) never reaches a passing run's log; see changelog.ts.
 */
export interface MainIO {
  out: (line: string) => void;
  err: (line: string) => void;
  versionFile: string;
  changelogFile: string;
  now: () => Date;
  /** VERSION as it is at a git ref, for `--check --base`. */
  versionAt: (ref: string) => string;
}

/** `git show <ref>:VERSION`, failing loudly: a check that cannot read its base has not passed. */
const gitVersionAt = (ref: string): string => {
  const shown = spawnSync('git', ['show', `${ref}:VERSION`], { encoding: 'utf8' });
  if (shown.status !== 0) throw new Error(`cannot read VERSION at ${ref}: ${(shown.stderr || '').trim() || `git exited ${shown.status}`}`);
  return shown.stdout;
};

const DEFAULT_IO: MainIO = {
  out: (line) => console.log(line),
  err: (line) => console.error(line),
  versionFile: VERSION_FILE,
  changelogFile: CHANGELOG_FILE,
  now: () => new Date(),
  versionAt: gitVersionAt,
};

export function main(argv: readonly string[], io: MainIO = DEFAULT_IO): number {
  let opts: Options;
  try {
    opts = parseArgs(argv);
  } catch (e) {
    io.err(`version: ${e instanceof Error ? e.message : String(e)}`);
    io.err(USAGE);
    return 2;
  }
  if ('help' in opts) {
    io.out(USAGE);
    return 0;
  }

  const versionText = readFileSync(io.versionFile, 'utf8');
  const markdown = readFileSync(io.changelogFile, 'utf8');

  if ('check' in opts) {
    const problems = disagreements(versionText, markdown);
    if (opts.base !== undefined) {
      let baseText: string;
      try {
        baseText = io.versionAt(opts.base);
      } catch (e) {
        io.err(`::error::${e instanceof Error ? e.message : String(e)}`);
        return 1;
      }
      const regression = behind(versionText, baseText);
      if (regression !== null) problems.push(regression);
    }
    // ::error:: so that CI annotates the run with the reason rather than burying it in a log.
    for (const problem of problems) io.err(`::error::${problem}`);
    if (problems.length > 0) return 1;
    io.out(`VERSION ${versionText.trim()} agrees with CHANGELOG.md${opts.base === undefined ? '' : ` and is not behind ${opts.base}`}`);
    return 0;
  }

  let bumped: Bumped;
  try {
    bumped = bump(markdown, versionText.trim(), opts.version, opts.date ?? today(io.now()));
  } catch (e) {
    if (!(e instanceof VersionError)) throw e;
    io.err(`version: ${e.message}`);
    return 1;
  }
  // No newline after the number: VERSION has never had one, and every reader trims it anyway.
  writeFileSync(io.versionFile, bumped.version, 'utf8');
  writeFileSync(io.changelogFile, bumped.markdown, 'utf8');
  io.out(`VERSION is ${bumped.version}, and CHANGELOG.md has its heading.`);
  if (bumped.empty) io.out(`Write the notes under it: \`bun run version --check\` fails until the section says something.`);
  return 0;
}

if (import.meta.main) process.exit(main(process.argv.slice(2)));
