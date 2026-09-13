/**
 * What reaches the release page: the section for the tag, and the refusal when there is none.
 *
 * The real CHANGELOG.md is read as well as fixtures, because the format that matters is the one
 * the file actually uses, and a heading style changed by hand would otherwise publish an empty
 * release body without anything failing.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import { CHANGELOG_FILE, main, parseArgs, sectionFor, versionOf, versionsIn } from './changelog.ts';

const SAMPLE = [
  '# Changelog',
  '',
  'Preamble that belongs to no release.',
  '',
  '## 0.2.0 (2026-09-20)',
  '',
  'The newest one.',
  '',
  '### Added',
  '',
  '- A thing.',
  '',
  '## 0.1.0-rc.3 (2026-09-12)',
  '',
  'The candidate that fixes the font.',
  '',
  '## 0.1.0-rc.2 (2026-09-11)',
  '',
  'Older still.',
  '',
].join('\n');

describe('finding the section', () => {
  test('a tag and a bare version are the same thing', () => {
    expect(versionOf('v0.1.0-rc.3')).toBe('0.1.0-rc.3');
    expect(versionOf('0.1.0-rc.3')).toBe('0.1.0-rc.3');
    expect(sectionFor(SAMPLE, 'v0.1.0-rc.3')).toBe(sectionFor(SAMPLE, '0.1.0-rc.3'));
  });

  test('the section is the body under its heading and stops at the next release', () => {
    expect(sectionFor(SAMPLE, '0.2.0')).toBe('The newest one.\n\n### Added\n\n- A thing.');
    expect(sectionFor(SAMPLE, '0.1.0-rc.3')).toBe('The candidate that fixes the font.');
  });

  test('the last section runs to the end of the file', () => {
    expect(sectionFor(SAMPLE, '0.1.0-rc.2')).toBe('Older still.');
  });

  test('the preamble is nobody\'s release notes', () => {
    expect(sectionFor(SAMPLE, '0.0.1')).toBeNull();
    expect(sectionFor(SAMPLE, 'Changelog')).toBeNull();
  });

  test('a version whose section is empty counts as missing', () => {
    expect(sectionFor('## 0.3.0 (2026-10-01)\n\n## 0.2.0 (2026-09-20)\n\nreal\n', '0.3.0')).toBeNull();
  });

  test('a version that is a prefix of another is not confused with it', () => {
    // 0.1.0 and 0.1.0-rc.3 differ by a suffix, and matching loosely would publish the candidate's
    // notes on the release, which is exactly the kind of mistake nobody notices until afterwards.
    expect(sectionFor(SAMPLE, '0.1.0')).toBeNull();
  });

  test('the versions are listed in the order the file gives them', () => {
    expect(versionsIn(SAMPLE)).toEqual(['0.2.0', '0.1.0-rc.3', '0.1.0-rc.2']);
  });
});

describe('the real changelog', () => {
  const markdown = readFileSync(CHANGELOG_FILE, 'utf8');

  test('has at least one section, in the heading shape this parses', () => {
    expect(versionsIn(markdown).length).toBeGreaterThan(0);
  });

  test('every version it names yields a non-empty body', () => {
    for (const version of versionsIn(markdown)) {
      expect({ version, body: (sectionFor(markdown, version) ?? '').length > 0 }).toMatchObject({ body: true });
    }
  });

  test('the version the project is on is one of them, which is what the release needs', () => {
    const current = readFileSync(new URL('../VERSION', import.meta.url).pathname, 'utf8').trim();
    expect(versionsIn(markdown)).toContain(current);
  });
});

describe('the command', () => {
  /**
   * Collects what `main` would have printed instead of letting it print.
   *
   * Not tidiness: the failure path emits a `::error::` workflow command, and GitHub turns that into
   * a red annotation on the run whatever the exit code is. Calling `main` for real put a red X on
   * every green CI run, which is how a red mark stops meaning anything.
   */
  const collect = (): { io: { out: (l: string) => void; err: (l: string) => void }; out: string[]; err: string[] } => {
    const out: string[] = [];
    const err: string[] = [];
    return { io: { out: (l) => out.push(l), err: (l) => err.push(l) }, out, err };
  };

  test('a tag with no section fails, rather than publishing the wrong notes', () => {
    const c = collect();
    expect(main(['v99.0.0'], c.io)).toBe(1);
    expect(c.err.join('\n')).toInclude('CHANGELOG.md has no section for 99.0.0');
  });

  test('the failure is annotated on the run, and only on a real failure', () => {
    // The annotation has to exist -- it is how a release that would ship the wrong notes announces
    // itself -- and it has to stay off every passing run.
    const failing = collect();
    main(['v99.0.0'], failing.io);
    expect(failing.err.some((l) => l.startsWith('::error::'))).toBe(true);

    const current = readFileSync(new URL('../VERSION', import.meta.url).pathname, 'utf8').trim();
    const passing = collect();
    main([current], passing.io);
    expect(passing.err.some((l) => l.startsWith('::'))).toBe(false);
  });

  test('a version that exists succeeds', () => {
    const current = readFileSync(new URL('../VERSION', import.meta.url).pathname, 'utf8').trim();
    const c = collect();
    expect(main([current], c.io)).toBe(0);
    expect(c.out.join('\n')).not.toBe('');
  });

  test('no argument at all is a usage error, not a silent empty body', () => {
    expect(main([])).toBe(2);
  });

  test('--out needs a file name', () => {
    expect(main(['0.1.0', '--out'])).toBe(2);
  });

  test('parseArgs reads the version and the output file', () => {
    expect(parseArgs(['v0.1.0-rc.3', '--out', 'body.md'])).toEqual({ version: 'v0.1.0-rc.3', out: 'body.md' });
    expect(parseArgs(['--help'])).toEqual({ help: true });
  });
});
