/**
 * The fuel readings hold still at the widest their boxes take, whatever range the sim hands them
 * (#1015, #1066).
 *
 * SimHub's range is the tank over the per-lap figure, and a settled per-lap figure a hair above zero
 * makes it anything at all: on the VM 9.97 L at 0.002 L a lap made `Fuel_RemainingLaps` 4985. Band D
 * drew the estimate as `4985.` with its tenth clipped, the AiM's foot drew it as `985.0` with its
 * leading digit dropped, the margin beside them read `+4974.0` in cells cut for `−999.9`, and the fuel
 * and stint modules drew a range of a hundred hours as `250:00:00`, seven digits in six cells. Each of
 * them is capped now, at the reading its field declares as its widest, and this file evaluates the
 * bindings the build emits at those ranges and measures what they draw against the boxes they are
 * drawn in.
 *
 * `widest.test.ts` holds every declared widest to what its binding can write; what it cannot do is
 * read a number, which is the half of these readings that grew, so the evaluation is here.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { MINUS } from '../src/design/metrics.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { CLOCK_WIDEST, FUEL_LAPS_LEFT_WIDEST, FUEL_TO_END_WIDEST } from '../src/second/values.ts';
import { THEME_DRAWINGS } from '../src/themes/drawings.ts';
import { drawsInThisProcess } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV } from '../src/themes/index.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { BAND_PAGES, bandPageItems } from '../src/zones/bandPages.ts';
import { widthAsDrawn } from './drawnStrings.ts';
import { evalTyped, type Props } from './ncalcEval.ts';

/** The range of the ticket: fuel for five thousand laps. */
const FIVE_THOUSAND_LAPS = 5000;

/** A hundred hours and more of fuel: 100 L at 0.01 L a lap on a 90 s lap. */
const NINE_HUNDRED_THOUSAND_SECONDS = 900_000;

/**
 * A race with a settled per-lap figure, the range `fuelLaps` laps or `fuelSeconds` seconds, eleven
 * laps or twenty minutes still to run, the car at the line.
 */
const telemetry = (opts: { fuelLaps?: number; fuelSeconds?: number; timed?: boolean } = {}): Props => ({
  'DataCorePlugin.GameData.CompletedLaps': 3,
  'DataCorePlugin.Computed.Fuel_LitersPerLap': 0.002,
  'DataCorePlugin.Computed.Fuel_RemainingLaps': opts.fuelLaps ?? 13.1,
  'DataCorePlugin.GameData.RemainingLaps': 11,
  'DataCorePlugin.Computed.Fuel_RemainingTime': opts.fuelSeconds ?? 1500,
  'DataCorePlugin.GameData.SessionTimeLeft': opts.timed ? 1200 : 604800,
  'DataCorePlugin.GameData.BestLapTime': 120,
  'DataCorePlugin.GameData.CurrentLapTime': 0,
  'DataCorePlugin.GameData.TrackPositionPercent': 0,
  'DataCorePlugin.GameData.SessionTypeName': 'Race',
  'driverisplayer(1)': true,
  'OpenDash.SessionProgress': opts.timed ? 'time' : 'laps',
});

const textsOf = (items: Item | readonly Item[]): TextItem[] => {
  const list: readonly Item[] = Array.isArray(items) ? (items as readonly Item[]) : [items as Item];
  return [...walkItems(list)].filter((item): item is TextItem => item.kind === 'text');
};

const named = (items: readonly TextItem[], name: string): TextItem => {
  const item = items.find((each) => each.name === name);
  if (item === undefined) throw new Error(`no ${name} among ${items.map((each) => each.name).join(', ')}`);
  return item;
};

const textAt = (item: TextItem, props: Props): string => {
  const formula = item.bindings?.Text?.formula;
  if (typeof formula !== 'string') throw new Error(`no Text formula on ${item.name}`);
  return String(evalTyped(formula, props));
};

/** A module at a wide zone body, which keeps every field it has. */
const moduleTexts = (id: string): TextItem[] => textsOf(MODULES.find((m) => m.id === id)!.build({ frame: rect(0, 0, 1007, 211), density: 'zone', prefix: '' }));

/** Digit cells of a reading, which is what a right-aligned row of segment cells has room for. */
const digitsOf = (text: string): number => [...text].filter((c) => /[0-9]/.test(c)).length;

if (THEME_ID === DEFAULT_THEME_ID) {
  describe('band D\'s Est. laps and margin at a range of five thousand laps', () => {
    const items = textsOf(bandPageItems('fuel', rect(0, 420, 1920, 60), '', true));
    const laps = named(items, 'laps.value');
    const margin = named(items, 'toEnd.value');

    test('the estimate reads its widest, and the margin the widest of its lap form', () => {
      expect(textAt(laps, telemetry({ fuelLaps: FIVE_THOUSAND_LAPS }))).toBe(FUEL_LAPS_LEFT_WIDEST);
      expect(textAt(margin, telemetry({ fuelLaps: FIVE_THOUSAND_LAPS }))).toBe('+999.9');
      // Short of the cap they read what the sim says, to the tenth.
      expect(textAt(laps, telemetry({ fuelLaps: 13.1 }))).toBe('13.1');
      expect(textAt(laps, telemetry({ fuelLaps: 999.9 }))).toBe('999.9');
      expect(textAt(margin, telemetry({ fuelLaps: 13.1 }))).toBe('+2.1');
    });

    test('a timed margin holds at what its box takes as well', () => {
      // Five thousand two-minute laps less the twenty-two minutes still to run is four digits of
      // minutes, which the box holds; ten times the range is the cap.
      expect(textAt(margin, telemetry({ fuelLaps: FIVE_THOUSAND_LAPS, timed: true }))).toBe('+9978');
      expect(textAt(margin, telemetry({ fuelLaps: 10 * FIVE_THOUSAND_LAPS, timed: true }))).toBe('+9999');
      expect(textAt(margin, telemetry({ fuelLaps: 0.5, timed: true }))).toBe(`${MINUS}21`);
    });

    test('both fields declare what they draw, and draw it inside their boxes', () => {
      expect(laps.widest).toBe(FUEL_LAPS_LEFT_WIDEST);
      expect(margin.widest).toBe(FUEL_TO_END_WIDEST);
      for (const [fuelLaps, timed] of [
        [FIVE_THOUSAND_LAPS, false],
        [10 * FIVE_THOUSAND_LAPS, true],
      ] as const) {
        const props = telemetry({ fuelLaps, timed });
        for (const [item, widest] of [
          [laps, FUEL_LAPS_LEFT_WIDEST],
          [margin, FUEL_TO_END_WIDEST],
        ] as const) {
          const reading = textAt(item, props);
          expect({ name: item.name, reading, inside: widthAsDrawn(item, reading) <= widthAsDrawn(item, widest) }).toEqual({ name: item.name, reading, inside: true });
          expect({ name: item.name, reading, fits: widthAsDrawn(item, reading) <= item.rect.width }).toEqual({ name: item.name, reading, fits: true });
        }
      }
      expect(BAND_PAGES.fuel!.find((f) => f.id === 'laps')!.numeralWidest).toBe(FUEL_LAPS_LEFT_WIDEST);
    });
  });

  describe('the fuel module\'s Est. laps at a range of five thousand laps', () => {
    test('reads its widest, which it declares and which fits its box', () => {
      const laps = named(moduleTexts('fuel'), 'lapsLeft.value');
      const reading = textAt(laps, telemetry({ fuelLaps: FIVE_THOUSAND_LAPS }));
      expect(reading).toBe(FUEL_LAPS_LEFT_WIDEST);
      expect(laps.widest).toBe(FUEL_LAPS_LEFT_WIDEST);
      expect(widthAsDrawn(laps, reading)).toBeLessThanOrEqual(laps.rect.width);
    });
  });

  describe('the fuel time on the fuel and stint modules at a range of a hundred hours', () => {
    for (const [module, field] of [
      ['fuel', 'time'],
      ['stint', 'fuelTime'],
    ] as const) {
      test(`the ${module} module's ${field} holds at ${CLOCK_WIDEST}, which it declares and which fits its box`, () => {
        const time = named(moduleTexts(module), `${field}.value`);
        expect(time.widest).toBe(CLOCK_WIDEST);
        const reading = textAt(time, telemetry({ fuelSeconds: NINE_HUNDRED_THOUSAND_SECONDS }));
        expect(reading).toBe(CLOCK_WIDEST);
        expect(widthAsDrawn(time, reading)).toBeLessThanOrEqual(widthAsDrawn(time, CLOCK_WIDEST));
        expect(widthAsDrawn(time, reading)).toBeLessThanOrEqual(time.rect.width);
        // Short of the cap the clock counts as it always did, hours on into two digits.
        expect(textAt(time, telemetry({ fuelSeconds: 1900 }))).toBe('0:31:40');
        expect(textAt(time, telemetry({ fuelSeconds: 359_999 }))).toBe(CLOCK_WIDEST);
        expect(textAt(time, telemetry({ fuelSeconds: 1e9 }))).toBe(CLOCK_WIDEST);
      });
    }
  });
}

/** The AiM's foot at 800 x 480, which drew 4985.0 as `985.0`. */
const AIM = 'aim';

if (THEME_ID === AIM) {
  describe('the AiM\'s 800x480 foot at a range of five thousand laps', () => {
    test('Est. laps and the margin keep every digit, inside their ghosts', () => {
      const drawing = THEME_DRAWINGS[AIM]!;
      const items = textsOf(drawing.bandPage!('fuel', { left: 0, top: 0, width: 800, height: 60 }, 'fuel.'));
      const props = telemetry({ fuelLaps: FIVE_THOUSAND_LAPS });
      for (const [id, expected] of [
        ['laps', FUEL_LAPS_LEFT_WIDEST],
        ['toEnd', '+999.9'],
      ] as const) {
        const value = named(items, `fuel.${id}.value`);
        const ghost = named(items, `fuel.${id}.ghost`);
        const reading = textAt(value, props);
        expect({ id, reading }).toEqual({ id, reading: expected });
        // A right-aligned row of segment cells drops whatever is left of its first cell, so the
        // digits have to be no more than the ghost's.
        expect({ id, reading, fits: digitsOf(reading) <= digitsOf(ghost.text) }).toEqual({ id, reading, fits: true });
        expect({ id, reading, fits: widthAsDrawn(value, reading) <= value.rect.width }).toEqual({ id, reading, fits: true });
      }
    });
  });
} else if (THEME_ID === DEFAULT_THEME_ID && !drawsInThisProcess(AIM)) {
  test(
    'the AiM\'s foot, in a process drawing its colours',
    () => {
      const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: AIM }, stdout: 'pipe', stderr: 'pipe' });
      const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
      expect({ exit: run.exitCode, output }).toEqual({ exit: 0, output: '' });
    },
    120_000,
  );
}
