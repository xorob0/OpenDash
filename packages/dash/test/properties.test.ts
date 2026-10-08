/**
 * The list of properties the packages read: that it holds the names no scanner can see, and that
 * asking for it twice cannot give two different answers.
 *
 * Producing the list composes all twenty-two packages, which costs more than a second, so
 * `propertiesRead` answers from a held scan. That is the kind of saving that pays for itself once
 * and then quietly breaks: a caller that sorts or splices what it got back would hand the next
 * caller a list that is no longer the contract, and the callers here are the recorder and the test
 * that holds the committed traces to it, so the failure would read as a trace gone stale rather
 * than as a list gone wrong. Hence the second test.
 */
import { describe, expect, test } from 'bun:test';
import { MAX_CARS } from '../src/generator.ts';
import { COMPUTED_PROPERTIES, callsRead, propertiesRead } from '../src/properties.ts';

describe('the properties the packages read', () => {
  const read = propertiesRead();

  test('it holds the lap-history names, which are built from a row index and no scan can see', () => {
    for (const property of COMPUTED_PROPERTIES) expect(read).toContain(property);
  });

  test('it is sorted, deduplicated, and not empty', () => {
    expect(read.length).toBeGreaterThan(0);
    expect(read).toEqual([...new Set(read)].sort());
  });

  test('a caller that rewrites what it got back does not change what the next one gets', () => {
    const mine = propertiesRead();
    mine.length = 0;
    mine.push('Not.A.Property');
    expect(propertiesRead()).toEqual(read);
  });
});

describe('the opponent calls the packages make', () => {
  const calls = callsRead();

  test('it is sorted, deduplicated, and not empty', () => {
    expect(calls.length).toBeGreaterThan(0);
    expect(calls).toEqual([...new Set(calls)].sort());
  });

  test('every text is in the canonical spelling: lower case, integer or boolean arguments, comma and space', () => {
    for (const call of calls) expect(call).toMatch(/^[a-z_]+\((-?\d+|true|false)?(, (-?\d+|true|false))*\)$/);
  });

  test('a row read by its repeat index is enumerated over every car, and the player position is asked once', () => {
    for (let p = 1; p <= MAX_CARS; p++) expect(calls).toContain(`drivername(${p})`);
    expect(calls).toContain('getplayerleaderboardposition()');
    expect(calls).toContain(`getopponentleaderboardposition_aheadbehind(-${MAX_CARS - 1})`);
  });

  test('a sector written as a literal is kept, so the sector strip asks for its three and no fourth', () => {
    expect(calls).toContain('driversectorlastlap(1, 3, false)');
    expect(calls.some((c) => /^driversectorlastlap\(\d+, 4,/.test(c))).toBe(false);
  });

  test('a caller that rewrites what it got back does not change what the next one gets', () => {
    const mine = callsRead();
    mine.length = 0;
    expect(callsRead()).toEqual(calls);
  });
});
