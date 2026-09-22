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

/**
 * The first site's files, exempt until each is replaced. An entry is removed in the commit that
 * rewrites the file, so the list shrinks to nothing and the rules then hold everywhere.
 */
const LEGACY = [
  'app/download/',
  'components/Shot.tsx',
  'components/SectionHead.tsx',
  'components/Reveal.tsx',
  'components/Changelog.tsx',
];

const isLegacy = (rel: string): boolean => LEGACY.some((l) => rel.startsWith(l));

const ALL_SOURCES = ['app', 'components', 'lib']
  .flatMap((d) => sources(path.join(site, d)))
  .filter((f) => !isLegacy(path.relative(site, f)));

/** The pages that have to make the promise, in the order they are rebuilt. */
const PROMISE_PAGES = ['app/page.tsx', 'app/compare/page.tsx', 'app/download/page.tsx'];

describe('the promise', () => {
  test.each(PROMISE_PAGES.filter((p) => existsSync(path.join(site, p)) && !isLegacy(p)))('%s says free forever', (page) => {
    expect(read(page)).toContain('FREE_FOREVER');
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
