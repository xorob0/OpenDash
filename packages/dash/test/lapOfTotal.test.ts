/**
 * The stint module's Lap field, `12 / 43`, held to the longest reading a race hands it (#921).
 *
 * A monospaced box is cut from its `chars` budget and WPF clips whatever overruns it, while the fit
 * tests measure a bound value by its `widest` and, without one, by its sample. The field carried the
 * sample alone and a budget that counted the two spaces and the slash as narrow cells, which only
 * `.`, `,` and `:` are given, so `12 / 43` passed every box and `12 / 120` lost its last digit on
 * every face that drew it. So three things are pinned here, in the order the fault ran: what the
 * binding draws through a long race, that the budget and the declaration are that reading, and that
 * every box the module is built into holds it.
 */
import { describe, expect, test } from 'bun:test';
import { monoWidth } from '../src/design/metrics.ts';
import type { TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { charsOfText, textWidth } from '../src/second/drawn.ts';
import { CHARS, LAP_OF_TOTAL_WIDEST } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';
import { moduleBoxes } from './secondScreens.test.ts';

const stint = MODULES.find((m) => m.id === 'stint')!;

/** The stint's Lap value in every box the second screens are proved against. */
const SITES = moduleBoxes().map((box) => {
  const items = [...walkItems(stint.build({ frame: box.frame, density: box.density, prefix: '' }))];
  const item = items.find((i): i is TextItem => i.kind === 'text' && i.name === 'lap.value');
  if (item === undefined) throw new Error(`no lap value in ${box.name}`);
  return { name: box.name, item };
});

const formula = (): string => {
  const f = SITES[0]!.item.bindings?.Text?.formula;
  if (typeof f !== 'string') throw new Error('the lap value has no Text formula');
  return f;
};

/** A lap-limited race: the lap you are on and the race's length. */
const lapped = (lap: number, total: number): Props => ({
  'DataCorePlugin.GameData.CurrentLap': lap,
  'DataCorePlugin.GameData.CompletedLaps': lap - 1,
  'DataCorePlugin.GameData.TotalLaps': total,
  'DataCorePlugin.GameData.RemainingLaps': total - lap + 1,
});

/**
 * A timed race as SimHub publishes it. iRacing's `TotalLaps` in a timed session is the leader's
 * completed laps rather than a length (#989), and SimHub's `RemainingLaps` is only `TotalLaps` less
 * `CompletedLaps`, never below nought (GameReaderCommon 9.12.6, `GameManagerBase`), so it predicts
 * nothing and a car on the lead lap has none.
 */
const timed = (lap: number, leader: number): Props => ({
  'DataCorePlugin.GameData.CurrentLap': lap,
  'DataCorePlugin.GameData.CompletedLaps': lap - 1,
  'DataCorePlugin.GameData.SessionTimeLeft': 1800,
  'DataCorePlugin.GameData.TotalLaps': leader,
  'DataCorePlugin.GameData.RemainingLaps': Math.max(leader - (lap - 1), 0),
});

/** An open session: no clock to speak of and no lap count, so neither `TotalLaps` nor `RemainingLaps`. */
const openSession = (lap: number): Props => ({
  'DataCorePlugin.GameData.CurrentLap': lap,
  'DataCorePlugin.GameData.CompletedLaps': lap - 1,
  'DataCorePlugin.GameData.SessionTimeLeft': 604800,
  'DataCorePlugin.GameData.TotalLaps': 0,
  'DataCorePlugin.GameData.RemainingLaps': 0,
});

const drawn = (props: Props): string => String(evalNcalc(formula(), props));

describe('the lap of the race on the stint page', () => {
  test('a long race draws three digits either side of the slash', () => {
    expect(drawn(lapped(12, 43))).toBe('12 / 43');
    expect(drawn(lapped(12, 120))).toBe('12 / 120');
    expect(drawn(lapped(142, 350))).toBe('142 / 350');
  });

  test("a race with no length draws the lap alone, never the leader's laps as its length (#989)", () => {
    // Three laps down on a leader with fourteen done: this drew `12 / 14` with half an hour to run.
    expect(drawn(timed(12, 14))).toBe('12');
    // On the lead lap, where `RemainingLaps` is nought: this drew `15 / 14`.
    expect(drawn(timed(15, 14))).toBe('15');
    // An open practice, whose `TotalLaps` is nought: never `4 / 0`.
    expect(drawn(openSession(4))).toBe('4');
    // A lap race keeps its length, clock or none.
    expect(drawn({ ...lapped(12, 30), 'DataCorePlugin.GameData.SessionTimeLeft': 604800 })).toBe('12 / 30');
    expect(drawn({ ...lapped(12, 30), 'DataCorePlugin.GameData.SessionTimeLeft': 0 })).toBe('12 / 30');
  });

  test('the budget is the declared reading, every space and the slash in a full cell', () => {
    const mono = SITES[0]!.item.monospace!;
    expect(LAP_OF_TOTAL_WIDEST).toBe('999 / 999');
    expect(charsOfText(LAP_OF_TOTAL_WIDEST, mono)).toEqual(CHARS.lapOfTotal);
    for (const s of SITES) {
      expect({ site: s.name, widest: s.item.widest }).toEqual({ site: s.name, widest: LAP_OF_TOTAL_WIDEST });
      // The canvas's sample is the short end of the range, and stays the canvas's.
      expect({ site: s.name, sample: s.item.text }).toEqual({ site: s.name, sample: '12 / 43' });
    }
  });

  test('every reading of a race up to 999 laps fits the widest in its own cells, and the widest fits the box', () => {
    const readings = new Set<string>();
    for (const total of [9, 43, 99, 100, 120, 350, 999]) {
      for (const lap of [1, 9, 12, 99, 100, 142, 999]) if (lap <= total) readings.add(drawn(lapped(lap, total)));
    }
    for (const s of SITES) {
      const mono = s.item.monospace!;
      const declared = textWidth(LAP_OF_TOTAL_WIDEST, mono);
      for (const reading of readings) {
        expect({ site: s.name, reading, inside: textWidth(reading, mono) <= declared }).toEqual({ site: s.name, reading, inside: true });
      }
      expect({ site: s.name, fits: declared <= monoWidth(mono, CHARS.lapOfTotal) && declared <= s.item.rect.width }).toEqual({ site: s.name, fits: true });
      // `12 / 120` is the reading the old budget cut, at every size it was drawn at.
      expect({ site: s.name, fits: textWidth('12 / 120', mono) <= s.item.rect.width }).toEqual({ site: s.name, fits: true });
    }
  });
});
