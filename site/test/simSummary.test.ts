/**
 * The Pages page summarises docs/sim-support.md rather than rendering it (#1191): a supported sim's
 * column becomes the readings that do not simply work, grouped by section, each with the first
 * sentence of its note and none of the bookkeeping.
 */
import { describe, expect, test } from 'bun:test';
import { readSimSupport, SUPPORTED_SIMS } from '../../scripts/simSupport.ts';
import { note, summarise } from '../lib/sims.ts';

const sections = readSimSupport();

describe('a note', () => {
  test('is the first sentence after the mark, without the ticket', () => {
    expect(note({ mark: 'Partly', text: '**Partly.** Air and track; the wind and the track state hide (#1123). More follows.' })).toBe(
      'Air and track; the wind and the track state hide.',
    );
    expect(note({ mark: 'Absent', text: '**Absent.** Reads `--` (#1123, #567).' })).toBe('Reads `--`.');
    expect(note({ mark: 'Partly', text: '**Partly.** The chequer ([flag-box.md](flag-box.md)).' })).toBe('The chequer.');
    expect(note({ mark: 'Works', text: '**Works** on the cars that have them.' })).toBe('on the cars that have them.');
  });

  test('is empty for a cell that is only its mark', () => {
    expect(note({ mark: 'Absent', text: '**Absent.**' })).toBe('');
  });
});

describe('a supported sim', () => {
  test('lists every reading of its column that is not Works, under its section, and counts the rest', () => {
    for (const sim of SUPPORTED_SIMS) {
      const summary = summarise(sections, sim);
      const rows = sections.flatMap((s) => s.rows);
      expect(summary.total).toBe(rows.length);
      expect(summary.works).toBe(rows.filter((r) => r.cells[sim].mark === 'Works').length);
      expect(summary.groups.flatMap((g) => g.items).length).toBe(summary.total - summary.works);
      for (const group of summary.groups) {
        expect(sections.map((s) => s.title)).toContain(group.title);
        expect(group.items.length).toBeGreaterThan(0);
        for (const item of group.items) {
          expect(item.mark).not.toBe('Works');
          expect(item.mark).not.toBe('Untested');
          expect(item.note).not.toMatch(/#\d+/);
          expect(item.note).not.toMatch(/\]\(/);
        }
      }
    }
  });

  test('on iRacing is a handful; on Assetto Corsa it is the column the drive rests on', () => {
    expect(summarise(sections, 'iRacing').groups.flatMap((g) => g.items).length).toBeLessThan(10);
    expect(summarise(sections, 'Assetto Corsa').groups.length).toBeGreaterThan(5);
  });
});
