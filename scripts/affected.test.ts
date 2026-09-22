/**
 * What `bun run affected` decides without building anything: how its arguments are read, how two
 * builds are turned into a verdict, and what the report says. Building the base beside the branch
 * is a sequence of git and filesystem steps and is proved by running it.
 */
import { describe, expect, test } from 'bun:test';
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { COMMENT_MARKER, compare, digest, parseArgs, report, type Digest } from './affected.ts';

const digestOf = (entries: Record<string, string>): Digest => new Map(Object.entries(entries));

describe('reading the arguments', () => {
  test('nothing compares against main', () => {
    expect(parseArgs([])).toEqual({ base: 'origin/main', keep: false });
  });

  test('the base may be any ref', () => {
    expect(parseArgs(['--base', 'origin/release'])).toMatchObject({ base: 'origin/release' });
  });

  test('it may be written with an equals sign', () => {
    expect(parseArgs(['--base=abc1234'])).toMatchObject({ base: 'abc1234' });
  });

  test('the switch is read', () => {
    expect(parseArgs(['--keep'])).toMatchObject({ keep: true });
  });

  test('help wins over everything else', () => {
    expect(parseArgs(['--base', 'main', '--help'])).toEqual({ help: true });
  });
});

describe('comparing two builds', () => {
  test('a package whose bytes are the same did not move', () => {
    const one = digestOf({ OpenDash: 'aaa' });
    expect(compare(one, one)).toMatchObject({ changed: [], added: [], removed: [], unchanged: ['OpenDash'] });
  });

  test('different bytes under the same name are a change', () => {
    const comparison = compare(digestOf({ OpenDash: 'aaa' }), digestOf({ OpenDash: 'bbb' }));
    expect(comparison).toMatchObject({ changed: ['OpenDash'], added: [], removed: [], unchanged: [] });
  });

  test('a name the base does not have is new, not changed', () => {
    const comparison = compare(digestOf({ OpenDash: 'aaa' }), digestOf({ OpenDash: 'aaa', 'OpenDash 800 round': 'ccc' }));
    expect(comparison).toMatchObject({ added: ['OpenDash 800 round'], changed: [], unchanged: ['OpenDash'] });
  });

  test('a name the branch no longer builds is removed', () => {
    const comparison = compare(digestOf({ OpenDash: 'aaa', 'OpenDash 480 round': 'bbb' }), digestOf({ OpenDash: 'aaa' }));
    expect(comparison).toMatchObject({ removed: ['OpenDash 480 round'], changed: [], added: [] });
  });

  test('one card reaching many faces is many changed packages', () => {
    const before = digestOf({ OpenDash: 'a', 'OpenDash 800x480': 'b', 'OpenDash Pit wall': 'c' });
    const after = digestOf({ OpenDash: 'a2', 'OpenDash 800x480': 'b2', 'OpenDash Pit wall': 'c' });
    expect(compare(before, after)).toMatchObject({ changed: ['OpenDash', 'OpenDash 800x480'], unchanged: ['OpenDash Pit wall'] });
  });

  test('the lists are sorted, so the same change reads the same twice', () => {
    const before = digestOf({ b: '1', a: '1', c: '1' });
    const after = digestOf({ b: '2', a: '2', c: '2' });
    expect(compare(before, after).changed).toEqual(['a', 'b', 'c']);
  });
});

describe('what the report says', () => {
  const nothing = { added: [], removed: [], changed: [], unchanged: ['OpenDash'] };

  test('a branch that moves no package output says nothing at all', () => {
    expect(report(nothing, 'abc1234567890')).toBe('');
  });

  test('a report carries the marker that lets CI find its own comment', () => {
    expect(report({ ...nothing, changed: ['OpenDash'] }, 'abc1234567890')).toContain(COMMENT_MARKER);
  });

  test('every package that moved is named', () => {
    const body = report({ added: ['OpenDash 800 round'], removed: ['OpenDash 480 round'], changed: ['OpenDash'], unchanged: [] }, 'abc1234567890');
    expect(body).toContain('`OpenDash`');
    expect(body).toContain('`OpenDash 800 round` (new)');
    expect(body).toContain('`OpenDash 480 round` (removed)');
  });

  test('the heading counts everything that moved', () => {
    expect(report({ added: ['b'], removed: ['c'], changed: ['a'], unchanged: [] }, 'abc1234567890')).toContain('3 packages to look at');
  });

  test('one package is not pluralised', () => {
    expect(report({ ...nothing, changed: ['OpenDash'] }, 'abc1234567890')).toContain('1 package to look at');
  });

  test('the capture command offers exactly what moved', () => {
    const body = report({ added: ['OpenDash 800 round'], removed: [], changed: ['OpenDash'], unchanged: ['OpenDash Pit wall'] }, 'abc1234567890');
    expect(body).toContain("bun run shots --packages 'OpenDash,OpenDash 800 round'");
    expect(body).not.toContain('OpenDash Pit wall');
  });

  test('a package the branch removed is not offered to the VM, which could not open it', () => {
    const body = report({ added: [], removed: ['OpenDash 480 round'], changed: [], unchanged: [] }, 'abc1234567890');
    expect(body).toContain('`OpenDash 480 round` (removed)');
    expect(body).not.toContain('bun run shots');
  });

  test('the base is named, so a stale comment can be recognised', () => {
    expect(report({ ...nothing, changed: ['OpenDash'] }, 'abc1234567890fff')).toContain('abc123456789');
  });
});

describe('reading a build from disk', () => {
  /** A build directory holding the manifest and the zips it names, with the given contents. */
  const buildDir = (packages: Record<string, string>): string => {
    const dir = mkdtempSync(path.join(tmpdir(), 'affected-'));
    const entries = Object.keys(packages).map((folder) => ({ folder, file: `${folder}.simhubdash` }));
    writeFileSync(path.join(dir, 'manifest.json'), JSON.stringify({ packages: entries }));
    for (const [folder, contents] of Object.entries(packages)) writeFileSync(path.join(dir, `${folder}.simhubdash`), contents);
    return dir;
  };

  test('every package the manifest names is hashed', () => {
    const digests = digest(buildDir({ OpenDash: 'one', 'OpenDash 800x480': 'two' }));
    expect([...digests.keys()].sort()).toEqual(['OpenDash', 'OpenDash 800x480']);
  });

  test('the same bytes hash the same and different bytes do not', () => {
    const same = digest(buildDir({ a: 'identical', b: 'identical', c: 'other' }));
    expect(same.get('a')).toBe(same.get('b'));
    expect(same.get('a')).not.toBe(same.get('c'));
  });

  test('a zip the manifest names but the build did not write is an error, not a silent absence', () => {
    const dir = mkdtempSync(path.join(tmpdir(), 'affected-'));
    writeFileSync(path.join(dir, 'manifest.json'), JSON.stringify({ packages: [{ folder: 'OpenDash', file: 'OpenDash.simhubdash' }] }));
    expect(() => digest(dir)).toThrow();
  });

  test('a directory with no manifest is an error', () => {
    const dir = mkdtempSync(path.join(tmpdir(), 'affected-'));
    mkdirSync(path.join(dir, 'empty'));
    expect(() => digest(dir)).toThrow();
  });
});
