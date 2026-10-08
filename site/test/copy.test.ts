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
import { counted } from '../lib/counts.ts';
import { DIFFERENTIATORS, FREE_FOREVER, NO_OTHER_ROUTE, ONLY_WAY_IN, PLUGIN_ZIP, THEMES_FREE, releaseWord } from '../lib/site.ts';
import { SAMPLE_COUNTS } from './sampleCounts.ts';

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

  test.each(PROMISE_PAGES)('%s says the car themes are free too', (page) => {
    expect(read(page)).toContain('THEMES_FREE');
  });

  test('is a promise and not a price', () => {
    expect(FREE_FOREVER).toContain('always will be');
    expect(THEMES_FREE).toContain('free too');
  });
});

/**
 * A count in the copy is read from the build, never typed (#560). A digit followed by one of the
 * nouns the site counts is what a retyped number looks like, and the sources are refused one. A
 * sentence that needs the number interpolates it from a `Counts`, through `counted()` where the
 * build may be absent.
 */
describe('the counts', () => {
  const NOUNS = 'dashboards?|LED profiles?|pages?|strip shapes?|faces?|screens?|glyphs?|flags?|companions?|pit walls?|themes?|sizes?';
  const TYPED = new RegExp(`(?<![×x$] )\\b\\d+ (${NOUNS})\\b`, 'g');

  test('no source types a count', () => {
    const offenders: string[] = [];
    for (const f of ALL_SOURCES) {
      // Comments explain the rule with examples of what it refuses, so they are not read.
      const text = readFileSync(f, 'utf8')
        .replace(/\/\*[\s\S]*?\*\//g, '')
        .replace(/^\s*\/\/.*$/gm, '');
      for (const m of text.matchAll(TYPED)) {
        // A competitor's cell is a fact about them, read from their pages, and carries no count of ours.
        const line = text.slice(text.lastIndexOf('\n', m.index) + 1, text.indexOf('\n', m.index));
        if (/\b(lovely|dnr): cell\(/.test(line)) continue;
        // A size in a caption, `480 face` after a width, is a size and not a count.
        if (/^\d+ (face|companion)$/.test(m[0]) && /\b(\d+ × |x)\d+ /.test(line)) continue;
        offenders.push(`${path.relative(site, f)}: ${m[0]}`);
      }
    }
    expect(offenders).toEqual([]);
  });

  test('a count the build has not made says every rather than zero', () => {
    expect(counted(14, 'dashboard')).toBe('14 dashboards');
    expect(counted(1, 'theme')).toBe('1 theme');
    expect(counted(0, 'dashboard')).toBe('every dashboard');
    expect(counted(0, 'LED profile')).toBe('every LED profile');
  });

  test('the three reasons read their numbers from the counts', () => {
    const bodies = DIFFERENTIATORS.map((d) => d.body(SAMPLE_COUNTS));
    expect(bodies.some((b) => b.includes(`${SAMPLE_COUNTS.stripShapes} strip shapes`))).toBe(true);
    expect(bodies.some((b) => b.includes(`${SAMPLE_COUNTS.pages} pages`))).toBe(true);
  });
});

/** One word for what a version is, on the home page and in the survey: never alpha here and beta there. */
describe('the status word', () => {
  test('a suffix is a candidate, and none is a release', () => {
    expect(releaseWord('0.3.0-rc.12')).toBe('Release candidate');
    expect(releaseWord('1.0.0')).toBe('Release');
  });

  test('no source says alpha or beta of the product', () => {
    const offenders: string[] = [];
    for (const f of ALL_SOURCES) {
      const lines = readFileSync(f, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '').split('\n');
      for (const line of lines) {
        if (/^\s*\/\//.test(line)) continue;
        // A competitor's cell may say their product is in beta; that is their word about theirs.
        if (/\b(lovely|dnr): cell\(/.test(line)) continue;
        if (/\b(alpha|beta)\b/i.test(line)) offenders.push(`${path.relative(site, f)}: ${line.trim()}`);
      }
    }
    expect(offenders).toEqual([]);
  });
});

/** Every picture says which build took it (#552): the two components that draw one call the note. */
describe('the pictures say where they came from', () => {
  test.each(['components/Capture.tsx', 'components/Clip.tsx'])('%s prints the provenance note', (file) => {
    expect(read(file)).toContain('provenanceNote(');
  });
});

/**
 * The first screen, which is the home page's first `<section>` and nothing after it. A visitor
 * arrives with three questions before any picture matters: whether it runs their sim, what it costs,
 * and why they would switch. All three are answered in that one section, so the three reasons have
 * to share it with the hero rather than open a section of their own below the fold. Reading only as
 * far as the section's closing tag is what notices when they move back out.
 */
describe('the first screen', () => {
  const home = read('app/page.tsx');
  const opens = home.indexOf('<section');
  const first = home.slice(opens, home.indexOf('</section>', opens));

  test('is one section, and it ends before the screen picker', () => {
    expect(first.slice('<section'.length)).not.toContain('<section');
    expect(first).not.toContain('id="screen"');
  });

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

/**
 * The plugin is the only way in (docs/scope.md, #438). The site used to offer a second route, one
 * `.simhubdash` per screen to double-click, on the install page, the download page and under the
 * picker; this is what notices if any of it comes back, or if the line moves to a page that is not
 * the one a reader looking for another route lands on.
 */
describe('the way in', () => {
  test.each(['app/install/page.tsx', 'app/download/page.tsx'])('%s makes the line', (page) => {
    expect(read(page)).toContain('ONLY_WAY_IN');
  });

  test('the install page says what the line costs', () => {
    expect(read('app/install/page.tsx')).toContain('NO_OTHER_ROUTE');
    expect(NO_OTHER_ROUTE).toContain('DLL');
    expect(ONLY_WAY_IN).toContain('only way in');
  });

  test('the one download is the plugin zip', () => {
    expect(PLUGIN_ZIP).toBe('OpenDash-plugin.zip');
    const offenders: string[] = [];
    for (const f of ALL_SOURCES) {
      for (const m of readFileSync(f, 'utf8').matchAll(/\/downloads\/(\$\{[^}]+\}|[^'"`\s]+)/g)) {
        if (m[1] !== '${PLUGIN_ZIP}' && m[1] !== PLUGIN_ZIP) offenders.push(`${path.relative(site, f)}: ${m[0]}`);
      }
    }
    expect(offenders).toEqual([]);
  });

  test('no source offers a dashboard or a profile as a file of its own', () => {
    const offenders = ALL_SOURCES.filter((f) => /\.simhubdash(?!\.com)|\.ledsprofile\b|by hand|double-click/i.test(readFileSync(f, 'utf8'))).map((f) => path.relative(site, f));
    expect(offenders).toEqual([]);
  });

  test('no reason to switch claims a file runs without the plugin', () => {
    for (const d of DIFFERENTIATORS) expect({ id: d.id, claims: /without the plugin|on its own/i.test(d.body(SAMPLE_COUNTS)) }).toEqual({ id: d.id, claims: false });
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

/**
 * Where a companion's paging is bound, which the plugin's pane and plugin/INSTALL.md also say (#435).
 * SimHub pages a companion, so the button is bound in the Controls and events of the device it runs
 * on, and that binding is the device's: the button paging the phone does not page the dash. A driver
 * looks here before the plugin is open, so the place has to be here and not only the action. The
 * page is read as text with its markup and its line breaks folded away, since a phrase may cross a
 * `<code>` or a wrap.
 */
describe('the install page on paging a companion', () => {
  const text = read('app/install/page.tsx')
    .replace(/<[^>]+>/g, '')
    .replace(/\{' '\}/g, ' ')
    .replace(/\s+/g, ' ');
  const guide = readFileSync(path.join(site, '..', 'plugin', 'INSTALL.md'), 'utf8').replace(/\*\*/g, '').replace(/\s+/g, ' ');

  test.each([
    'Tap the left or right half of the screen to change module.',
    'open the device or window the companion runs on in SimHub, go to its',
    'Controls and events',
    'NextScreen, with PreviousScreen to go back.',
    'Those bindings belong to that device, so the button that pages',
    'does not page your dash.',
  ])('says, as the guide does, %p', (phrase) => {
    expect(text.toLowerCase()).toContain(phrase.toLowerCase());
    expect(guide.toLowerCase()).toContain(phrase.toLowerCase());
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
