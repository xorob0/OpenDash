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
import { COMPUTED_PROPERTIES, propertiesRead } from '../src/properties.ts';

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
