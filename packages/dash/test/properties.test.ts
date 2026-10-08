/**
 * The list of properties the packages read: that it reads every place an expression lives, in every
 * shipped theme, that it holds the names no scanner can see, and that asking for it twice cannot
 * give two different answers.
 *
 * Producing the list composes every package of every shipped theme, which costs a couple of seconds, so
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

  /**
   * One property for each place an expression lives that is not an item's own `Bindings`, read
   * through that place alone, and one for a theme that is not the default. Each was missing from the
   * list for as long as the scan walked only the item bindings of the default theme (#581): the
   * engine chip's border is the only reader of the warnings bitfield, the pit wall's pages are
   * enabled by their page property, the companion's forced module reaches its screens through a
   * variable, and the crest is read by the Porsche's foot alone.
   */
  const readThrough: [string, string][] = [
    ['a border colour binding', 'DataCorePlugin.GameRawData.Telemetry.EngineWarnings'],
    ["a screen's enabled expression", 'OpenDash.PitWallPage'],
    ['a dashboard variable', 'OpenDash.CompanionOpenOn'],
    ["a theme's own face", 'OpenDash.PorscheCrest'],
  ];
  for (const [place, property] of readThrough) {
    test(`it holds a property read only through ${place}`, () => {
      expect(read).toContain(property);
    });
  }

  test('it holds no dashboard variable, since SimHub resolves those inside the dashboard and no trace can carry one', () => {
    expect(read.filter((p) => p.startsWith('variable.'))).toEqual([]);
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
