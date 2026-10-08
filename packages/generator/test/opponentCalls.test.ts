/**
 * The canonical spelling of an opponent call, which the recorder, `callsRead()` and the browser
 * evaluator all have to agree on to the character, and the enumeration of a computed argument.
 */
import { describe, expect, test } from 'bun:test';
import { functionCalls } from '../src/ncalcFunctions.ts';
import * as ncalc from '../src/ncalc.ts';
import { callText, callTextsFor, domainOf, isOpponentCall, literalArgument, MAX_CARS, opponentCall, spellArgument } from '../src/opponentCalls.ts';

describe('the canonical spelling', () => {
  test('lower-case name, integer arguments, booleans in lower case, comma and one space', () => {
    expect(callText('DriverSectorLastLap', [3, 1, false])).toBe('driversectorlastlap(3, 1, false)');
    expect(callText('getopponentleaderboardposition_aheadbehind', [-1])).toBe('getopponentleaderboardposition_aheadbehind(-1)');
    expect(callText('getplayerleaderboardposition', [])).toBe('getplayerleaderboardposition()');
  });

  test('a whole double is spelled as the integer it is, and minus zero as zero', () => {
    expect(spellArgument(3.0)).toBe('3');
    expect(spellArgument(-0)).toBe('0');
  });

  test('a fraction, a string or a null has no spelling, so the call has no column', () => {
    expect(spellArgument(2.5)).toBeUndefined();
    expect(spellArgument('3')).toBeUndefined();
    expect(spellArgument(null)).toBeUndefined();
    expect(callText('drivername', [null])).toBeUndefined();
  });

  test('the text ncalc.ts writes for a literal call is already canonical', () => {
    for (const written of [ncalc.driverSector('lastlap', '3', 1), ncalc.bestSplitTime(2), ncalc.playerPosition(), ncalc.aheadBehind('-1')]) {
      const [call] = functionCalls(written);
      const args = call!.args.map((a) => literalArgument(a));
      expect(callText(call!.name, args)).toBe(written);
    }
  });
});

describe('which calls are opponent calls', () => {
  test('the driver families by prefix, the leaderboard lookups by name', () => {
    expect(opponentCall('drivername')).toEqual(['position']);
    expect(opponentCall('driversectorlastlap')).toEqual(['position', 'sector', 'flag']);
    expect(opponentCall('getopponentleaderboardposition_aheadbehind_playerclassonly')).toEqual(['offset']);
    expect(opponentCall('getplayerleaderboardposition')).toEqual([]);
  });

  test('drivergamespecificdata shares the prefix and is not one; nor is a property function', () => {
    expect(isOpponentCall('drivergamespecificdata')).toBe(false);
    expect(isOpponentCall('driver')).toBe(false);
    expect(isOpponentCall('format')).toBe(false);
  });
});

describe('enumerating a computed argument', () => {
  test('a literal is kept as written, brackets and a minus included', () => {
    expect(literalArgument('(-1)')).toBe(-1);
    expect(literalArgument(' ((2)) ')).toBe(2);
    expect(literalArgument('false')).toBe(false);
    expect(literalArgument('repeatindex()')).toBeUndefined();
    expect(literalArgument('(1) + (2)')).toBeUndefined();
  });

  test('a computed position is every car, a computed offset every distance either side', () => {
    expect(domainOf('position')).toHaveLength(MAX_CARS);
    expect(domainOf('offset')).toHaveLength(2 * MAX_CARS - 1);
    expect(domainOf('offset')).toContain(-(MAX_CARS - 1));
    expect(domainOf('offset')).toContain(0);
  });

  test('a call with one computed argument becomes one text per value of it', () => {
    const texts = callTextsFor('driversectorlastlap', ['getplayerleaderboardposition()', '2', 'false']);
    expect(texts).toHaveLength(MAX_CARS);
    expect(texts).toContain('driversectorlastlap(24, 2, false)');
    expect(callTextsFor('getbestsplittime', ['3'])).toEqual(['getbestsplittime(3)']);
  });

  test('a call that is not an opponent call has no texts, and a wrong arity is an error', () => {
    expect(callTextsFor('format', ['[A]', "'0'"])).toEqual([]);
    expect(() => callTextsFor('drivername', [])).toThrow();
  });
});
