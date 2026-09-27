/**
 * The sentences that have to be where they have to be.
 *
 * The promise is defined once in lib/site.ts and the pages that make it import it, so a page that
 * forgot would still typecheck. This is what notices. The same file keeps the site free of the em
 * dash, which the writing rules refuse, and of any host beyond the repository, the car data source
 * and SimHub itself.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { DIFFERENTIATORS, FREE_FOREVER } from '../lib/site.ts';

const site = path.resolve(import.meta.dir, '..');
const read = (rel: string): string => readFileSync(path.join(site, rel), 'utf8');

function sources(dir: string): string[] {
  const out: string[] = [];
  const walk = (d: string) => {
    for (const name of readdirSync(d)) {
      const full = path.join(d, name);
      if (statSync(full).isDirectory()) walk(full);
      else if (/\.(ts|tsx)$/.test(name) && !name.endsWith('.generated.ts')) out.push(full);
    }
  };
  if (existsSync(dir)) walk(dir);
  return out;
}

const ALL_SOURCES = ['app', 'components', 'lib'].flatMap((d) => sources(path.join(site, d)));

/**
 * Where the promise has to be made: the first screen, the download page, and the price row of the
 * comparison. Not in every lede, which is how it read as a pitch.
 */
const PROMISE_PAGES = ['app/page.tsx', 'app/download/page.tsx', 'lib/compare.ts'];

describe('the promise', () => {
  test.each(PROMISE_PAGES)('%s says free forever', (page) => {
    expect(read(page)).toContain('FREE_FOREVER');
  });

  test('is a promise and not a price', () => {
    expect(FREE_FOREVER).toContain('always will be');
  });
});

/**
 * The first screen, which is the hero and the three reasons under it, down to where the screen
 * picker starts. A visitor arrives with three questions before any picture matters: whether it runs
 * their sim, what it costs, and why they would switch. All three are answered before any scrolling,
 * and a rewrite that drops one of the answers still typechecks, so this is what notices.
 */
describe('the first screen', () => {
  const home = read('app/page.tsx');
  const first = home.slice(0, home.indexOf('id="screen"'));

  test.each(['iRacing', 'SimHub', 'Windows'])('says %s', (word) => {
    expect(first).toContain(word);
  });

  test('makes the promise', () => {
    expect(first).toContain('FREE_FOREVER');
  });

  test('names three reasons', () => {
    expect(first).toContain('DIFFERENTIATORS');
    expect(DIFFERENTIATORS.length).toBe(3);
  });
});

describe('the footer', () => {
  test('links the repository', () => {
    expect(read('components/Footer.tsx')).toContain('href={REPO_URL}');
  });

  test('is on every page', () => {
    expect(read('app/layout.tsx')).toContain('<Footer />');
  });
});

describe('the writing rules', () => {
  test('no source carries an em dash', () => {
    const offenders = ALL_SOURCES.filter((f) => /—/.test(readFileSync(f, 'utf8'))).map((f) => path.relative(site, f));
    expect(offenders).toEqual([]);
  });
});

describe('the links out', () => {
  const ALLOWED = new Set(['github.com', 'www.simhubdash.com']);

  test('go only to the repository, the car data and SimHub', () => {
    const hosts = new Set<string>();
    for (const f of ALL_SOURCES) {
      for (const m of readFileSync(f, 'utf8').matchAll(/https?:\/\/([a-z0-9.-]+)/gi)) hosts.add(m[1]!.toLowerCase());
    }
    expect([...hosts].filter((h) => !ALLOWED.has(h))).toEqual([]);
  });
});
