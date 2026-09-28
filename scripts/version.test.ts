/**
 * The version command: the ordering it refuses by, the edit it makes, and the check CI runs.
 *
 * The ordering is held to the plugin's own test cases, read out of VersioningTests.cs, because a
 * command that thought 0.3.0-rc.10 was older than rc.9 would refuse the one cut the plugin would
 * install, and one that thought the opposite would allow a cut it would not. The real VERSION and
 * CHANGELOG.md are checked as well, so that `bun run check` fails where CI does.
 */
import { describe, expect, test } from 'bun:test';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { VERSION_FILE } from '../packages/dash/src/build.ts';
import { CHANGELOG_FILE, sectionFor } from './changelog.ts';
import { behind, bump, compareVersions, disagreements, headingsIn, isDate, main, parseArgs, today, type MainIO } from './version.ts';

const SAMPLE = [
  '# Changelog',
  '',
  'Preamble that belongs to no release.',
  '',
  '## 0.3.0-rc.7 (2026-09-27)',
  '',
  'The newest one.',
  '',
  '## 0.3.0-rc.6 (2026-09-22)',
  '',
  'Older.',
  '',
].join('\n');

describe('the ordering is the plugin\'s', () => {
  const cases = (): [string | null, string | null, number][] => {
    const cs = readFileSync(path.resolve(import.meta.dir, '../plugin/OpenDash.Tests/VersioningTests.cs'), 'utf8');
    const arg = String.raw`(null|"[^"]*")`;
    const row = new RegExp(String.raw`\[InlineData\(${arg}, ${arg}, (-?1|0)\)\]`, 'g');
    const value = (s: string): string | null => (s === 'null' ? null : JSON.parse(s));
    return [...cs.matchAll(row)].map((m) => [value(m[1]!), value(m[2]!), Number(m[3])]);
  };

  test('the plugin\'s test file still has the cases this reads', () => {
    // A rename of the C# theories would otherwise leave the loop below checking nothing.
    expect(cases().length).toBeGreaterThanOrEqual(30);
  });

  test('every VersionCompare case the plugin tests orders the same way here', () => {
    for (const [a, b, sign] of cases()) {
      expect({ a, b, sign: Math.sign(compareVersions(a, b)) }).toEqual({ a, b, sign });
    }
  });

  test('a two-digit candidate outranks a one-digit one, which is the cut that comes after rc.9', () => {
    expect(compareVersions('0.3.0-rc.10', '0.3.0-rc.9')).toBeGreaterThan(0);
    expect(compareVersions('0.3.0', '0.3.0-rc.10')).toBeGreaterThan(0);
  });

  test('a core segment past Int32.MaxValue is 0, as int.TryParse leaves it in the plugin', () => {
    expect(compareVersions('99999999999.0.0', '1.0.0')).toBeLessThan(0);
    expect(compareVersions('2147483648.0.0', '0.0.0')).toBe(0);
    expect(compareVersions('2147483647.0.0', '1.0.0')).toBeGreaterThan(0);
  });
});

describe('the edit', () => {
  test('a newer version gets a dated heading above the newest release, and nothing else moves', () => {
    const { version, markdown, empty } = bump(SAMPLE, '0.3.0-rc.7', '0.3.0-rc.8', '2026-09-28');
    expect(version).toBe('0.3.0-rc.8');
    expect(markdown).toBe(SAMPLE.replace('## 0.3.0-rc.7', '## 0.3.0-rc.8 (2026-09-28)\n\n## 0.3.0-rc.7'));
    expect(empty).toBe(true);
  });

  test('a tag is taken as its version', () => {
    expect(bump(SAMPLE, '0.3.0-rc.7', 'v0.3.0', '2026-10-01').version).toBe('0.3.0');
  });

  test('the same version again re-dates its heading and keeps its notes', () => {
    const { markdown, empty } = bump(SAMPLE, '0.3.0-rc.7', '0.3.0-rc.7', '2026-09-29');
    expect(markdown).toBe(SAMPLE.replace('## 0.3.0-rc.7 (2026-09-27)', '## 0.3.0-rc.7 (2026-09-29)'));
    expect(empty).toBe(false);
  });

  test('an older version is refused, because the plugin would not install it', () => {
    expect(() => bump(SAMPLE, '0.3.0-rc.7', '0.3.0-rc.6', '2026-09-28')).toThrow('does not rank above 0.3.0-rc.7');
    expect(() => bump(SAMPLE, '0.3.0-rc.7', '0.2.0', '2026-09-28')).toThrow('every rig would keep 0.3.0-rc.7');
  });

  test('a version that only differs by build metadata is not newer', () => {
    expect(() => bump(SAMPLE.replace(/0\.3\.0-rc\.7/g, '0.3.0'), '0.3.0', '0.3.0+b1', '2026-09-28')).toThrow('does not rank above');
  });

  test('something that is not a version is refused before anything is written', () => {
    expect(() => bump(SAMPLE, '0.3.0-rc.7', '0.4', '2026-09-28')).toThrow('is not a version');
    expect(() => bump(SAMPLE, '0.3.0-rc.7', '0.4.0', 'Monday')).toThrow('is not a date');
    expect(() => bump(SAMPLE, '0.3.0-rc.7', '0.4.0', '2026-99-99')).toThrow('is not a date');
    expect(() => bump(SAMPLE, '0.3.0-rc.7', '0.4.0', '2026-02-29')).toThrow('is not a date');
  });

  test('a date is a day the calendar has, not only the shape of one', () => {
    expect(isDate('2026-09-28')).toBe(true);
    expect(isDate('2028-02-29')).toBe(true);
    expect(isDate('2026-02-29')).toBe(false);
    expect(isDate('2026-13-01')).toBe(false);
    expect(isDate('2026-9-28')).toBe(false);
  });

  test('a changelog ahead of VERSION is refused rather than given a second newest heading', () => {
    expect(() => bump(SAMPLE, '0.3.0-rc.5', '0.3.0-rc.6', '2026-09-28')).toThrow('already has 0.3.0-rc.6');
    expect(() => bump(SAMPLE, '0.3.0-rc.5', '0.3.0-rc.6.1', '2026-09-28')).toThrow("newest release is 0.3.0-rc.7, which 0.3.0-rc.6.1 does not rank above");
  });

  test('a changelog with no release yet gets its first heading after the preamble', () => {
    const { markdown } = bump('# Changelog\n\nPreamble.\n', '0.0.1', '0.1.0', '2026-09-10');
    expect(markdown).toBe('# Changelog\n\nPreamble.\n\n## 0.1.0 (2026-09-10)\n');
  });

  test('the date is the local day', () => {
    expect(today(new Date(2026, 8, 3, 23, 59))).toBe('2026-09-03');
  });
});

describe('the check', () => {
  test('a changelog whose newest heading is VERSION, with notes, agrees', () => {
    expect(disagreements('0.3.0-rc.7', SAMPLE)).toEqual([]);
    expect(disagreements('0.3.0-rc.7\n', SAMPLE)).toEqual([]);
  });

  test('VERSION moved by hand, with no heading, disagrees', () => {
    expect(disagreements('0.3.0-rc.8', SAMPLE)).toEqual([
      'VERSION is 0.3.0-rc.8 but CHANGELOG.md has no section for it (its newest is 0.3.0-rc.7); run `bun run version 0.3.0-rc.8` and write the notes',
    ]);
  });

  test('a heading added by hand, with VERSION left behind, disagrees', () => {
    const ahead = bump(SAMPLE, '0.3.0-rc.7', '0.3.0-rc.8', '2026-09-28').markdown.replace('(2026-09-28)\n', '(2026-09-28)\n\nNotes.\n');
    expect(disagreements('0.3.0-rc.7', ahead)).toEqual([
      "VERSION is 0.3.0-rc.7 but CHANGELOG.md's newest release is 0.3.0-rc.8; run `bun run version <x.y.z>` rather than editing either by hand",
    ]);
  });

  test('a heading with nothing under it disagrees, because the release body would be empty', () => {
    const bare = bump(SAMPLE, '0.3.0-rc.7', '0.3.0-rc.8', '2026-09-28').markdown;
    expect(disagreements('0.3.0-rc.8', bare)).toEqual(["CHANGELOG.md's section for 0.3.0-rc.8 has no notes under it, and the release body would be empty"]);
  });

  test('a number that moved backwards disagrees', () => {
    const backwards = SAMPLE.replace('## 0.3.0-rc.7', '## 0.3.0-rc.5');
    expect(disagreements('0.3.0-rc.5', backwards)).toEqual(['CHANGELOG.md lists 0.3.0-rc.5 above 0.3.0-rc.6, which it does not rank above']);
  });

  test('an undated heading or a VERSION that is not a version disagrees', () => {
    expect(disagreements('0.3.0-rc.7', SAMPLE.replace(' (2026-09-22)', ''))).toEqual([
      'CHANGELOG.md line 9 is not a `## <version> (<date>)` heading: "## 0.3.0-rc.6"',
    ]);
    expect(disagreements('next', SAMPLE)[0]).toBe('VERSION must hold a version like 0.3.0, got "next"');
    expect(disagreements('0.3.0-rc.7', SAMPLE.replace('(2026-09-22)', '(2026-99-99)'))).toEqual([
      'CHANGELOG.md line 9 is not a `## <version> (<date>)` heading: "## 0.3.0-rc.6 (2026-99-99)"',
    ]);
  });

  test('a branch that set the number back and deleted the newer section agrees with itself, so only the base catches it', () => {
    const rolledBack = SAMPLE.replace('## 0.3.0-rc.7 (2026-09-27)\n\nThe newest one.\n\n', '');
    expect(disagreements('0.3.0-rc.6', rolledBack)).toEqual([]);
    expect(behind('0.3.0-rc.6', '0.3.0-rc.7\n')).toBe(
      "VERSION is 0.3.0-rc.6, below the base's 0.3.0-rc.7; the plugin installs a dashboard only over an older one, so a merge would leave every rig on 0.3.0-rc.7",
    );
  });

  test('against the base, the same number or a newer one is not behind', () => {
    expect(behind('0.3.0-rc.7', '0.3.0-rc.7')).toBeNull();
    expect(behind('0.3.0-rc.8\n', '0.3.0-rc.7')).toBeNull();
    expect(behind('0.3.0', '0.3.0-rc.10')).toBeNull();
    expect(behind('0.3.0+b1', '0.3.0')).toBe("VERSION is 0.3.0+b1, which ranks the same as the base's 0.3.0 without being it");
  });

  test('a changelog with no release at all disagrees', () => {
    expect(disagreements('0.1.0', '# Changelog\n')).toEqual(['CHANGELOG.md has no `## <version> (<date>)` heading']);
  });
});

describe('the real tree', () => {
  test('VERSION agrees with CHANGELOG.md, which is what CI checks on a pull request', () => {
    expect(disagreements(readFileSync(VERSION_FILE, 'utf8'), readFileSync(CHANGELOG_FILE, 'utf8'))).toEqual([]);
  });

  test('every heading the changelog has is one the check can read', () => {
    const markdown = readFileSync(CHANGELOG_FILE, 'utf8');
    for (const h of headingsIn(markdown)) expect({ version: h.version, section: sectionFor(markdown, h.version) !== null, date: h.date !== null }).toMatchObject({ section: true, date: true });
  });
});

describe('the command', () => {
  const tree = (version: string, changelog: string, base: Record<string, string> = {}) => {
    const dir = mkdtempSync(path.join(tmpdir(), 'version-'));
    const out: string[] = [];
    const err: string[] = [];
    const io: MainIO = {
      out: (l) => out.push(l),
      err: (l) => err.push(l),
      versionFile: path.join(dir, 'VERSION'),
      changelogFile: path.join(dir, 'CHANGELOG.md'),
      now: () => new Date(2026, 8, 28, 12),
      versionAt: (ref) => {
        const at = base[ref];
        if (at === undefined) throw new Error(`cannot read VERSION at ${ref}`);
        return at;
      },
    };
    writeFileSync(io.versionFile, version);
    writeFileSync(io.changelogFile, changelog);
    return { io, out, err, version: () => readFileSync(io.versionFile, 'utf8'), changelog: () => readFileSync(io.changelogFile, 'utf8') };
  };

  test('writes VERSION and the heading together, dated today, and says the notes are still owed', () => {
    const t = tree('0.3.0-rc.7', SAMPLE);
    expect(main(['0.3.0-rc.8'], t.io)).toBe(0);
    expect(t.version()).toBe('0.3.0-rc.8');
    expect(t.changelog()).toStartWith('# Changelog\n\nPreamble that belongs to no release.\n\n## 0.3.0-rc.8 (2026-09-28)\n\n## 0.3.0-rc.7');
    expect(t.out.join('\n')).toInclude('Write the notes under it');

    // Until they are written, the check refuses the cut.
    expect(main(['--check'], t.io)).toBe(1);
    writeFileSync(t.io.changelogFile, t.changelog().replace('(2026-09-28)\n', '(2026-09-28)\n\nThe notes.\n'));
    expect(main(['--check'], t.io)).toBe(0);
  });

  test('--date sets the day of the cut', () => {
    const t = tree('0.3.0-rc.7', SAMPLE);
    expect(main(['0.3.0', '--date', '2026-10-02'], t.io)).toBe(0);
    expect(t.changelog()).toInclude('## 0.3.0 (2026-10-02)');
  });

  test('a refused version writes nothing', () => {
    const t = tree('0.3.0-rc.7', SAMPLE);
    expect(main(['0.3.0-rc.6'], t.io)).toBe(1);
    expect(t.version()).toBe('0.3.0-rc.7');
    expect(t.changelog()).toBe(SAMPLE);
    expect(t.err.join('\n')).toInclude('does not rank above');
  });

  test('the check annotates the run on a failure, and only on a failure', () => {
    const failing = tree('0.3.0-rc.8', SAMPLE);
    expect(main(['--check'], failing.io)).toBe(1);
    expect(failing.err.every((l) => l.startsWith('::error::'))).toBe(true);
    expect(failing.err.length).toBe(1);

    const passing = tree('0.3.0-rc.7', SAMPLE);
    expect(main(['--check'], passing.io)).toBe(0);
    expect(passing.err).toEqual([]);
    expect(passing.out).toEqual(['VERSION 0.3.0-rc.7 agrees with CHANGELOG.md']);
  });

  test('--base fails the check when VERSION is behind the base, and says so in the annotation', () => {
    const rolledBack = SAMPLE.replace('## 0.3.0-rc.7 (2026-09-27)\n\nThe newest one.\n\n', '');
    const t = tree('0.3.0-rc.6', rolledBack, { main: '0.3.0-rc.7' });
    expect(main(['--check'], t.io)).toBe(0);
    expect(main(['--check', '--base', 'main'], t.io)).toBe(1);
    expect(t.err).toEqual([
      "::error::VERSION is 0.3.0-rc.6, below the base's 0.3.0-rc.7; the plugin installs a dashboard only over an older one, so a merge would leave every rig on 0.3.0-rc.7",
    ]);
  });

  test('--base passes a branch level with or ahead of the base', () => {
    const t = tree('0.3.0-rc.7', SAMPLE, { main: '0.3.0-rc.7', old: '0.3.0-rc.6' });
    expect(main(['--check', '--base', 'main'], t.io)).toBe(0);
    expect(main(['--check', '--base', 'old'], t.io)).toBe(0);
    expect(t.out.at(-1)).toBe('VERSION 0.3.0-rc.7 agrees with CHANGELOG.md and is not behind old');
  });

  test('a base that cannot be read fails the check rather than passing it', () => {
    const t = tree('0.3.0-rc.7', SAMPLE);
    expect(main(['--check', '--base', 'nowhere'], t.io)).toBe(1);
    expect(t.err).toEqual(['::error::cannot read VERSION at nowhere']);
  });

  test('arguments', () => {
    expect(parseArgs(['0.3.0'])).toEqual({ version: '0.3.0', date: undefined });
    expect(parseArgs(['--date', '2026-09-28', '0.3.0'])).toEqual({ version: '0.3.0', date: '2026-09-28' });
    expect(parseArgs(['--check'])).toEqual({ check: true });
    expect(parseArgs(['--check', '--base', 'abc123'])).toEqual({ check: true, base: 'abc123' });
    expect(() => parseArgs(['--check', '--base'])).toThrow('--check takes nothing else but --base <ref>');
    expect(parseArgs(['-h'])).toEqual({ help: true });
    expect(() => parseArgs([])).toThrow('one version is needed');
    expect(() => parseArgs(['0.3.0', '0.4.0'])).toThrow('one version is needed');
    expect(() => parseArgs(['--check', '0.3.0'])).toThrow('--check takes nothing else but --base <ref>');
    expect(() => parseArgs(['0.3.0', '--date'])).toThrow('--date needs a day');
    expect(() => parseArgs(['--force', '0.3.0'])).toThrow('unknown option --force');
  });

  test('a usage error is exit 2 and touches nothing', () => {
    const t = tree('0.3.0-rc.7', SAMPLE);
    expect(main([], t.io)).toBe(2);
    expect(t.version()).toBe('0.3.0-rc.7');
  });
});
