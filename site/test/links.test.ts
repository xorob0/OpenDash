/**
 * Every internal link points at a page that exists and an anchor that page declares.
 *
 * `lib/routes.ts` lists the routes and their anchors; this reads every `href` in a page or a
 * component and holds it against that list. A section renamed without its links following fails
 * here rather than scrolling a reader to the top of the wrong page.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { REDIRECTS, ROUTE_LIST, STATIC_PREFIXES, isInternalLink, routeFor } from '../lib/routes.ts';

const site = path.resolve(import.meta.dir, '..');

function sources(dir: string): string[] {
  const out: string[] = [];
  const walk = (d: string) => {
    for (const name of readdirSync(d)) {
      const full = path.join(d, name);
      if (statSync(full).isDirectory()) walk(full);
      else if (name.endsWith('.tsx')) out.push(full);
    }
  };
  walk(dir);
  return out;
}

/** The page a file under app/ renders, for its `#anchor` links. */
const pageOf = (file: string): string | undefined => {
  const rel = path.relative(path.join(site, 'app'), file);
  if (!rel.endsWith('page.tsx')) return undefined;
  const dir = path.dirname(rel);
  return dir === '.' ? '/' : `/${dir}`;
};

/** Every literal href in a file: `href="/x"`, `href={'/x'}` and the prefix of a template `href={`/x/${y}`}`. */
function hrefs(source: string): string[] {
  const out: string[] = [];
  for (const m of source.matchAll(/href=(?:"([^"]+)"|\{'([^']+)'\}|\{`([^`$]+)(?:\$\{[^`]*)?`\})/g)) out.push((m[1] ?? m[2] ?? m[3])!);
  return out;
}

describe('the routes', () => {
  test.each(ROUTE_LIST.map((r) => [r.path] as const))('%s has a page', (p) => {
    const file = path.join(site, 'app', p === '/' ? '' : p, 'page.tsx');
    expect({ path: p, exists: existsSync(file) }).toEqual({ path: p, exists: true });
  });

  test('every redirect lands on a route and a declared anchor', () => {
    for (const r of REDIRECTS) expect({ from: r.source, ok: isInternalLink(r.destination) }).toEqual({ from: r.source, ok: true });
  });
});

describe('the links', () => {
  const files = [...sources(path.join(site, 'app')), ...sources(path.join(site, 'components'))];

  test.each(files.map((f) => [path.relative(site, f), f] as const))('%s', (_rel, file) => {
    const page = pageOf(file);
    const bad: string[] = [];
    for (const href of hrefs(readFileSync(file, 'utf8'))) {
      if (/^https?:/.test(href) || href.startsWith('mailto:')) continue;
      if (href.startsWith('#')) {
        if (page === undefined) continue;
        if (!routeFor(page)?.anchors.includes(href.slice(1))) bad.push(href);
        continue;
      }
      if (STATIC_PREFIXES.some((p) => href.startsWith(p))) continue;
      if (!isInternalLink(href)) bad.push(href);
    }
    expect(bad).toEqual([]);
  });
});
