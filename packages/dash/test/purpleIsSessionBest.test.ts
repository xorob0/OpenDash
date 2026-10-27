/**
 * #1038 and #1042: purple is the session best and nothing else.
 *
 * The brand gives `color.best.primary` to the session best, and Lap times draws the field's best as
 * `Session best` in purple and the driver's own as `Your best` in the neutral colour. Band D's Sectors
 * page, the Last lap card and the lap history each drew the driver's own best, `BestLapTime`, in that
 * purple, so a driver five tenths off the field who set a personal best saw two purple bests on one
 * face, and only one of them was the session best.
 *
 * Every surface is evaluated against the frame the tickets were found on, the VM's green scenario:
 * the field's best 1:37.268, the player's best and last lap 1:37.905. The colour is read off what a
 * driver would see, not off the formula's spelling.
 */
import { describe, expect, test } from 'bun:test';
import { lastLap } from '../src/cards/lastLap.ts';
import { rect } from '../src/design/geometry.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { ncalcEvaluator as E } from '../src/generator.ts';
import { lapHistory } from '../src/modules/lapHistory.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { BAND_PAGES } from '../src/zones/bandPages.ts';

const PURPLE = ds.purpose.lap.sessionBest;
const FIELD_BEST = 97.268;
const YOUR_BEST = 97.905;

/** The green scenario's frame, with the last lap and the newest previous lap both `last`. */
const frame = (last: number, fieldBest: number = FIELD_BEST, yourBest: number = YOUR_BEST): Record<string, E.Value> => ({
  'OpenDash.PositionMode': 'field',
  'DataCorePlugin.GameData.BestLapOpponent.BestLapTime': E.fromSeconds(fieldBest),
  'DataCorePlugin.GameData.BestLapTime': E.fromSeconds(yourBest),
  'DataCorePlugin.GameData.LastLapTime': E.fromSeconds(last),
  'PersistantTrackerPlugin.PreviousLap_00': E.fromSeconds(last),
  // SimHub's per-slot delta is the lap less the player's own best (#1030).
  'PersistantTrackerPlugin.PreviousLap_00_DeltaToSessionBest': E.double(last - yourBest),
  'PersistantTrackerPlugin.PreviousLap_00_IsCurrentSession': E.int(1),
});

/** What a formula answers in a frame, row one being the repeat index. */
const at = (formula: string, properties: Record<string, E.Value>): unknown =>
  E.toJs(
    E.evaluate(formula, {
      properties: (name: string) => (Object.hasOwn(properties, name) ? properties[name] : undefined),
      repeat: [1],
      state: new E.CallState(),
      now: 0,
      rootScreenName: '',
    }),
  );

const texts = (items: readonly Item[]): TextItem[] => [...walkItems(items)].filter((i): i is TextItem => i.kind === 'text');

const formulaOf = (item: TextItem, target: 'Text' | 'TextColor'): string => {
  const b = item.bindings?.[target];
  if (!b || b.mode !== 'formula' || typeof b.formula !== 'string') throw new Error(`${item.name} has no ${target} formula`);
  return b.formula;
};

/** The text an item draws in a frame and the colour it draws it in. */
const drawn = (item: TextItem, properties: Record<string, E.Value>): { text: unknown; colour: unknown } => ({
  text: at(formulaOf(item, 'Text'), properties),
  colour: item.bindings?.TextColor ? at(formulaOf(item, 'TextColor'), properties) : item.textColor,
});

describe("band D's Sectors best is the session best its purple says (#1038)", () => {
  const best = BAND_PAGES.sectors!.find((f) => f.id === 'best')!;
  const colourOf = (): string | undefined => best.color;

  test('it draws the field’s 1:37.268 in purple, not the player’s 1:37.905', () => {
    const text = at(best.bind, frame(YOUR_BEST));
    expect({ text, colour: colourOf() }).toEqual({ text: '1:37.268', colour: PURPLE });
  });

  test('a purple field only ever draws the session best', () => {
    // The field is purple whatever it draws, so what it draws must be the session best on any frame.
    for (const [fieldBest, yourBest, expected] of [
      [FIELD_BEST, YOUR_BEST, '1:37.268'],
      [YOUR_BEST, YOUR_BEST, '1:37.905'],
      [95.5, 101.25, '1:35.500'],
    ] as const) {
      const text = at(best.bind, frame(yourBest, fieldBest, yourBest));
      expect({ fieldBest, purple: colourOf() === PURPLE, text }).toEqual({ fieldBest, purple: true, text: expected });
    }
  });
});

describe('the Last lap card is purple only on the session best (#1042)', () => {
  const value = texts(lastLap.build(rect(0, 0, 255, 187), 'lastLap.')).find((i) => i.name === 'lastLap.value')!;

  test('a personal best a field’s best faster than it is drawn in the nominal colour', () => {
    expect(drawn(value, frame(YOUR_BEST))).toEqual({ text: '1:37.905', colour: ds.purpose.lap.nominal });
  });

  test('the session best is purple', () => {
    expect(drawn(value, frame(FIELD_BEST, FIELD_BEST, FIELD_BEST))).toEqual({ text: '1:37.268', colour: PURPLE });
  });
});

describe('a lap history row is purple only on the session best (#1042)', () => {
  const items = lapHistory.build({ frame: rect(0, 0, 600, 280), density: 'zone', prefix: '', page: 'lapHistory' });
  const time = texts(items).find((i) => i.name === 'row.time')!;
  const delta = texts(items).find((i) => i.name === 'row.delta')!;

  test('a personal best a field’s best faster than it is drawn in the row’s ordinary colours', () => {
    const f = frame(YOUR_BEST);
    expect(drawn(time, f)).toEqual({ text: '1:37.905', colour: ds.color.text.primary });
    expect(drawn(delta, f)).toEqual({ text: '+0.000', colour: ds.color.text.secondary });
  });

  test('the session best is purple, time and delta', () => {
    const f = frame(FIELD_BEST, FIELD_BEST, FIELD_BEST);
    expect(drawn(time, f)).toEqual({ text: '1:37.268', colour: PURPLE });
    expect(drawn(delta, f).colour).toBe(PURPLE);
  });

  test('and before anybody has a time, nothing is purple', () => {
    const f: Record<string, E.Value> = { 'OpenDash.PositionMode': 'field', 'PersistantTrackerPlugin.PreviousLap_00_IsCurrentSession': E.int(1) };
    expect(drawn(time, f).colour).not.toBe(PURPLE);
    expect(drawn(delta, f).colour).not.toBe(PURPLE);
  });
});
