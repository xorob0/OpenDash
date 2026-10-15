/**
 * Which sims OpenDash supports is said in five places, and they say one thing (#102, #479).
 *
 * docs/scope.md's product table is the record, docs/sim-support.md's columns are the evidence and
 * `SUPPORTED_SIMS` in scripts/simSupport.ts is the list its test holds the columns to; the README, the
 * install guide in the plugin's zip and the site's claim are what a driver reads. Before #102 the site
 * said "Tested on iRacing only" in a sentence nothing held to the scope, so a claim that moved in one
 * place could stand still in the others. This reads each of them and holds them to the list.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { readSimSupport, SIMS, SUPPORTED_SIMS } from '../../scripts/simSupport.ts';
import { rows } from '../lib/compare.ts';
import { CLAIMED_SIM, SUPPORTED_SIMS as SITE_SUPPORTED } from '../lib/site.ts';
import { SAMPLE_COUNTS, SAMPLE_LISTED } from './sampleCounts.ts';

const repoRoot = path.resolve(import.meta.dir, '..', '..');
const read = (file: string): string => readFileSync(path.join(repoRoot, file), 'utf8').replace(/\s+/g, ' ');

/** `iRacing and Assetto Corsa`, as prose joins two. */
const joined = SUPPORTED_SIMS.join(' and ');
/** The sims the table has a column for and 1.0 does not claim. */
const unclaimed = SIMS.filter((sim) => !SUPPORTED_SIMS.includes(sim));

describe('the claim about sims', () => {
  test('is two sims, and the table has a column for each and for the two it does not claim', () => {
    expect([...SUPPORTED_SIMS]).toEqual(['iRacing', 'Assetto Corsa']);
    expect(unclaimed).toEqual(['ACC', 'Le Mans Ultimate']);
    expect(readSimSupport().length).toBeGreaterThan(0);
  });

  test("docs/scope.md's product table names the supported sims, and its product section calls the others untested", () => {
    const scope = read('docs/scope.md');
    expect(scope).toContain(`| Supported sims | ${SUPPORTED_SIMS.join(', ')} |`);
    const product = scope.slice(scope.indexOf('## Product'), scope.indexOf('## What ships'));
    expect(product).toContain('Assetto Corsa Competizione and Le Mans Ultimate may work');
    expect(product).toContain('untested and not supported');
    expect(product).toContain('sim-support.md');
  });

  test('scope.md no longer refuses other sims', () => {
    expect(read('docs/scope.md')).not.toContain('Sims other than iRacing, as a supported claim');
  });

  test('the README and the install guide say the same', () => {
    const readme = read('README.md');
    expect(readme).toContain(`**Supported: ${joined}.**`);
    expect(readme).toContain('**May work, untested and not supported: Assetto Corsa Competizione and Le Mans Ultimate.**');
    expect(readme).toContain('docs/sim-support.md');
    const guide = read('plugin/INSTALL.md');
    expect(guide).toContain(`OpenDash supports ${joined}. Assetto Corsa Competizione and Le Mans Ultimate may work, but they are untested and not supported.`);
  });

  test('the site says the same, on the first screen, in the titles and in the comparison', () => {
    expect(SITE_SUPPORTED).toBe(joined);
    expect(CLAIMED_SIM).toBe(`Supported on ${joined}. ACC and Le Mans Ultimate may work, but they are untested and not supported.`);
    const cell = rows(SAMPLE_COUNTS, SAMPLE_LISTED).find((r) => r.id === 'sims')?.cells.opendash.text ?? '';
    expect(cell).toStartWith(`Supported on ${joined}.`);
    for (const sim of unclaimed) expect(cell).toContain(sim);
    expect(cell).toContain('untested');
  });

  test('and renders the table on the Pages page, under the anchor the home page links to', () => {
    const pages = readFileSync(path.join(import.meta.dir, '..', 'app', 'pages', 'page.tsx'), 'utf8');
    expect(pages).toContain('id="sims"');
    expect(pages).toContain('<SimTable');
    expect(readFileSync(path.join(import.meta.dir, '..', 'app', 'page.tsx'), 'utf8')).toContain('href="/pages#sims"');
  });
});
