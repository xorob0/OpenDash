/**
 * One best, one name (#1030).
 *
 * A face can draw two bests side by side, and they are two laps. Lap times' `Session best` is the
 * field's best lap, or the class's, and its `Your best` is the player's own best lap of the session,
 * which is SimHub's `BestLapTime`. SimHub's lap tracker calls the second its session best too: its
 * live delta and its per-lap `_DeltaToSessionBest` are measured from `BestLapTime`. So every reading
 * measured from that lap has to say so, or a driver reads a gap to the session best that is really a
 * gap to their own best, which on the VM was six tenths away from the session best drawn beside it.
 *
 * The frame below is the VM's green scenario as the ticket found it: the field's best 1:37.268, the
 * player's 1:37.905 and a last lap of 1:38.000. The lap review's first delta and the lap history's
 * delta are `+0.10` and `+0.095` there, which is the gap to your best and not the `+0.73` to the
 * session best, and so their captions name your best.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages } from '../src/build.ts';
import { MODULE_CATALOGUE } from '../src/contract.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { referenceLabel } from '../src/second/values.ts';
import { itemsOf, walkItems } from '../src/walk.ts';
import { ZONE_FACES, faceItems } from '../src/zones/index.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const FIELD_BEST = 97.268;
const YOUR_BEST = 97.905;
const LAST_LAP = 98.0;

/** A gap as the lap review draws it, written independently of `signedToFit`. */
const gap = (seconds: number): string => `${seconds < 0 ? '−' : '+'}${Math.abs(seconds).toFixed(2)}`;

const GREEN: Props = {
  'DataCorePlugin.GameData.BestLapOpponent.BestLapTime': FIELD_BEST,
  'DataCorePlugin.GameData.BestLapTime': YOUR_BEST,
  'DataCorePlugin.GameData.LastLapTime': LAST_LAP,
  'PersistantTrackerPlugin.PreviousLap_00': LAST_LAP,
  // What SimHub 9.12.6 writes for the slot: its time less `BestLapTime`, a difference of two
  // TimeSpans and so exact to the tick, which the VM drew `+0.10`.
  'PersistantTrackerPlugin.PreviousLap_00_DeltaToSessionBest': 0.095,
  'PersistantTrackerPlugin.PreviousLap_00_IsCurrentSession': 1,
};

const textIn = (items: readonly Item[], name: string): TextItem => {
  const found = [...walkItems(items)].find((i) => i.name === name);
  if (found?.kind !== 'text') throw new Error(`${name} is not a text item`);
  return found;
};

/** What a text draws in a frame: its binding evaluated where it has one, and its text where it does not. */
const drawn = (item: TextItem, props: Props): unknown => {
  const formula = item.bindings?.Text?.formula;
  return typeof formula === 'string' ? evalNcalc(formula, props) : item.text;
};

const moduleItems = (id: string): Item[] => {
  const module = MODULES.find((m) => m.id === id);
  if (!module) throw new Error(`no ${id} module`);
  const { width, height } = SHAPE_ARCHETYPES.wide;
  return [...walkItems(module.build({ frame: rect(0, 0, width, height), density: 'zone', prefix: `${id}.` }))];
};

describe('the names Lap times gives the two bests', () => {
  const lapTimes = moduleItems('lapTimes');
  const reads = (name: string): string => String(textIn(lapTimes, name).bindings?.Text?.formula);

  test("the session best is the field's lap and your best is your own", () => {
    expect(textIn(lapTimes, 'lapTimes.sessionBest.label').text).toBe('Session best');
    expect(reads('lapTimes.sessionBest.value')).toContain('[DataCorePlugin.GameData.BestLapOpponent.BestLapTime]');
    expect(textIn(lapTimes, 'lapTimes.yourBest.label').text).toBe('Your best');
    expect(reads('lapTimes.yourBest.value')).toContain('[DataCorePlugin.GameData.BestLapTime]');
    expect(reads('lapTimes.yourBest.value')).not.toContain('BestLapOpponent');
  });
});

describe('a delta measured from your best says so', () => {
  test("the lap review's first delta is the gap to your best, under that name", () => {
    const items = faceItems(ZONE_FACES[0]!);
    const review = [...walkItems(items)].find((i) => i.name === 'lapReview');
    if (!review) throw new Error('no lap review on the reference face');
    const parts = [...walkItems([review])];
    expect(drawn(textIn(parts, 'lapReview.vsBest.label'), GREEN)).toBe('vs your best');
    // `+0.10` is 1:38.000 less 1:37.905. The gap to the session best on the same face is `+0.73`.
    const value = drawn(textIn(parts, 'lapReview.vsBest.value'), GREEN);
    expect(value).toBe('+0.10');
    expect(value).toBe(gap(0.095));
    expect(value).not.toBe(gap(LAST_LAP - FIELD_BEST));
  });

  test("the lap history's delta column is headed by your best, and its first row is that gap", () => {
    const items = moduleItems('lapHistory');
    expect(textIn(items, 'lapHistory.head.delta').text).toBe('Δ your best');
    const row = textIn(items, 'lapHistory.row.delta');
    // Row one is slot zero, the lap just finished.
    const formula = String(row.bindings?.Text?.formula).replace(/\brepeatindex\(\)/g, '1');
    expect(evalNcalc(formula, GREEN)).toBe('+0.095');
  });

  test('the catalogue describes the lap history by the same best, which is what the panel shows', () => {
    const description = MODULE_CATALOGUE.find((m) => m.id === 'lapHistory')?.description;
    expect(description).toBe('Your last laps with the delta to your best.');
  });

  test("the live delta's default reference is your best, which is the lap SimHub's session best is", () => {
    expect(evalNcalc(referenceLabel(), {})).toBe('vs your best');
    expect(evalNcalc(referenceLabel(), { 'OpenDash.DeltaReference': 'session' })).toBe('vs your best');
  });
});

describe('no package captions a reading "vs session best"', () => {
  // SimHub publishes no live delta and no per-lap delta to the field's best, so a caption of that
  // form is always a reading of the player's own best under the field's name.
  const packages = composePackages({ version: '0.0.0-test', log: () => {} }, true);

  test('the build composes the faces, the second screens and the pit wall it is asked about', () => {
    expect(packages.length).toBeGreaterThan(10);
  });

  for (const { pkg } of packages) {
    test(pkg.folderName, () => {
      for (const dashboard of pkg.dashboards) {
        for (const item of itemsOf(dashboard)) {
          if (item.kind !== 'text') continue;
          const said = [item.text, item.widest ?? '', String(item.bindings?.Text?.formula ?? '')].join(' ').toLowerCase();
          expect({ item: item.name, says: said.includes('vs session best') }).toEqual({ item: item.name, says: false });
        }
      }
    });
  }
});
