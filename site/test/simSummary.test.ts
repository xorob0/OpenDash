/**
 * The Pages page summarises docs/sim-support.md rather than rendering it (#1191): a row per part of
 * the dash, and per supported sim how many of that part's readings are marked Works.
 */
import { describe, expect, test } from 'bun:test';
import { readSimSupport, SUPPORTED_SIMS } from '../../scripts/simSupport.ts';
import { countByPart } from '../lib/sims.ts';

const sections = readSimSupport();
const parts = countByPart(sections, SUPPORTED_SIMS);

describe('the parts', () => {
  test('are the document sections, in its order, each the size of its table', () => {
    expect(parts.map((p) => p.title)).toEqual(sections.map((s) => s.title));
    expect(parts.map((p) => p.total)).toEqual(sections.map((s) => s.rows.length));
  });

  test('count the Works cells of each supported sim and nothing else', () => {
    for (const [i, section] of sections.entries()) {
      for (const sim of SUPPORTED_SIMS) {
        const works = section.rows.filter((r) => r.cells[sim].mark === 'Works').length;
        expect(parts[i]?.works[sim]).toBe(works);
        expect(works).toBeLessThanOrEqual(parts[i]?.total ?? 0);
      }
    }
  });

  test('say iRacing works nearly everywhere and Assetto Corsa in part', () => {
    const whole = (sim: (typeof SUPPORTED_SIMS)[number]) => parts.filter((p) => p.works[sim] === p.total).length;
    expect(whole('iRacing')).toBeGreaterThan(parts.length / 2);
    expect(whole('Assetto Corsa')).toBeLessThan(whole('iRacing'));
    expect(parts.every((p) => p.works['Assetto Corsa'] > 0)).toBe(true);
  });
});
