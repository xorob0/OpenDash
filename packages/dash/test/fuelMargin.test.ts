/**
 * Fuel to the end of the race, read as a driver would read it (#387).
 *
 * The margin is one signed number standing in for a subtraction a driver was doing between corners,
 * so what is worth pinning is the number and not the shape of the formula: the fuel page carries the
 * estimated laps and the session page the laps left, and the margin has to be the difference between
 * those two or it is a third opinion. These tests therefore evaluate the NCalc the build emits, for
 * the fuel module and for band D's fuel page, and compare the margin against the two fields the same
 * frame draws.
 *
 * The two forms are checked separately because they are read in two units: laps in a lap-counted
 * session and minutes in a timed one, and which of the two is drawn follows `SessionProgress` exactly
 * as the session page's own counter does. Both are the estimated laps less the laps still to run,
 * which are the laps left less the part of the lap already run, and the timed form reads that in
 * minutes at the lap the laps left are counted at, so a timed race is measured to the end of the lap
 * its clock runs out on and not to the clock (#1024).
 *
 * The last describe here is about width rather than value, and it is the one the fit tests cannot do
 * for themselves. `textFit`, `secondScreens` and `zoneFace` measure what an item says it draws, and a
 * bound value says its sample unless it declares a `widest`: cut for `+1.4` and handed `−169.0`, the
 * margin passed every box in the build and clipped on every one of them. So the readings the
 * expression can produce are enumerated from the expression itself and held against the declaration,
 * which is what puts the real figure in front of the boxes.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { MINUS, monoWidth, cells } from '../src/design/metrics.ts';
import type { TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { ds } from '../src/tokens.ts';
import { expressionsOf, walkItems } from '../src/walk.ts';
import { CHARS, FUEL_TO_END_WIDEST, fuelToEndColour, fuelToEndText, NO_VALUE, tankIsLow } from '../src/second/values.ts';
import { BAND_PAGES, bandPageItems } from '../src/zones/bandPages.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

/** A wide zone body, which is the shape that keeps every field of the page. */
const pageItems = (id: string): TextItem[] => {
  const module = MODULES.find((m) => m.id === id)!;
  const items = [...walkItems(module.build({ frame: rect(0, 0, 1007, 211), density: 'zone', prefix: '' }))];
  return items.filter((item): item is TextItem => item.kind === 'text');
};

const formulaOf = (items: readonly TextItem[], name: string, target: 'Text' | 'TextColor' | 'Left'): string => {
  const binding = items.find((item) => item.name === name)?.bindings?.[target];
  const formula = binding?.formula;
  if (typeof formula !== 'string') throw new Error(`no ${target} formula on ${name}`);
  return formula;
};

/** A week of remaining time is what iRacing publishes for a session with no limit. */
const A_WEEK = 604800;

const fuelItems = pageItems('fuel');
const sessionItems = pageItems('session');

/**
 * What the sim publishes, in the two shapes the margin subtracts.
 *
 * The session is a race unless a case says otherwise, because that is the only session the margin
 * draws in at all: a practice or qualifying session has an end and no flag, and the reading is
 * gated on the name for that reason rather than on the length alone.
 */
const telemetry = (
  opts: {
    lapsDone?: number;
    perLap?: number;
    fuelLaps?: number;
    lapsLeft?: number;
    fuelSeconds?: number;
    sessionSeconds?: number;
    sessionLength?: number;
    bestLap?: number | null;
    lapRun?: number;
    position?: number;
    progress?: string;
    type?: string | null;
  } = {},
): Props => ({
  'DataCorePlugin.GameData.CompletedLaps': opts.lapsDone ?? 3,
  'DataCorePlugin.Computed.Fuel_LitersPerLap': opts.perLap ?? 2.84,
  'DataCorePlugin.Computed.Fuel_RemainingLaps': opts.fuelLaps ?? 13.1,
  'DataCorePlugin.GameData.RemainingLaps': opts.lapsLeft ?? 11,
  'DataCorePlugin.Computed.Fuel_RemainingTime': opts.fuelSeconds ?? 1500,
  'DataCorePlugin.GameData.SessionTimeLeft': opts.sessionSeconds ?? A_WEEK,
  ...(opts.sessionLength === undefined ? {} : { 'DataCorePlugin.GameRawData.Telemetry.SessionTimeTotal': opts.sessionLength }),
  // Two minutes, so a timed session's laps left are the time left over two minutes and the lap the
  // clock runs out on: eleven in twenty minutes, as the lap-counted default is.
  'DataCorePlugin.GameData.BestLapTime': opts.bestLap === undefined ? 120 : opts.bestLap,
  'DataCorePlugin.GameData.CurrentLapTime': opts.lapRun ?? 0,
  // At the line unless a case puts the car round the lap, so the whole lap is still to run.
  'DataCorePlugin.GameData.TrackPositionPercent': opts.position ?? 0,
  'DataCorePlugin.GameData.SessionTypeName': opts.type === undefined ? 'Race' : opts.type,
  // The car leads, so a timed race ends at its own first crossing after the clock; a car behind the
  // leader is `lapsLeft.test.ts`'s case (#1027).
  'driverisplayer(1)': true,
  ...(opts.progress === undefined ? {} : { 'OpenDash.SessionProgress': opts.progress }),
});

const marginOn = (items: readonly TextItem[], props: Props): { value: unknown; colour: unknown } => ({
  value: evalNcalc(formulaOf(items, 'toEnd.value', 'Text'), props),
  colour: evalNcalc(formulaOf(items, 'toEnd.value', 'TextColor'), props),
});

describe('the fuel margin on the fuel module', () => {
  test('a lap-counted session reads the estimate less the laps left, to a tenth of a lap', () => {
    expect(marginOn(fuelItems, telemetry({ fuelLaps: 13.1, lapsLeft: 11 }))).toEqual({ value: '+2.1', colour: ds.purpose.delta.faster });
    // The typographic minus, not the hyphen .NET's formatter writes: `signed` substitutes it.
    expect(marginOn(fuelItems, telemetry({ fuelLaps: 8.7, lapsLeft: 11 }))).toEqual({ value: '−2.3', colour: ds.purpose.delta.slower });
    // Exactly enough counts as reaching the flag, `RemainingLaps` counting the lap you are on.
    expect(marginOn(fuelItems, telemetry({ fuelLaps: 11, lapsLeft: 11 }))).toEqual({ value: '+0.0', colour: ds.purpose.delta.faster });
  });

  test('mid-lap, the part of the lap already run comes off the laps left, as it has come out of the tank', () => {
    // The last lap of thirty, begun with fuel for 1.3 laps and nine tenths of it run: 0.4 laps in the
    // tank and a tenth of a lap to go. Counted with the lap whole, this read `−0.6` in red beside a
    // Refuel asking for 0.6 laps of fuel, at the pit entry of a car that makes the flag.
    const lastLap = {
      ...telemetry({ lapsLeft: 1, fuelLaps: 0.4, perLap: 2.84, lapRun: 99, bestLap: 110, position: 0.9, progress: 'laps' }),
      'DataCorePlugin.GameData.Fuel': 0.4 * 2.84,
    };
    const refuel = evalNcalc(formulaOf(fuelItems, 'toAdd.value', 'Text'), lastLap);
    expect({ ...marginOn(fuelItems, lastLap), refuel }).toEqual({ value: '+0.3', colour: ds.purpose.delta.faster, refuel: '0.0' });
  });

  test('a timed session reads the same subtraction in whole minutes, at the lap the laps left are counted at', () => {
    // Twenty minutes at a two-minute lap is ten whole laps and the one the clock runs out on.
    const timed = { sessionSeconds: 1200, progress: 'auto' } as const;
    expect(marginOn(fuelItems, telemetry({ ...timed, fuelLaps: 13.6 }))).toEqual({ value: '+5', colour: ds.purpose.delta.faster });
    expect(marginOn(fuelItems, telemetry({ ...timed, fuelLaps: 8.6 }))).toEqual({ value: '−5', colour: ds.purpose.delta.slower });
    // A tenth of a minute is six seconds of a figure that moves by more than that every corner, so
    // the timed form is whole minutes and the field stays the width of the estimate beside it.
    expect(marginOn(fuelItems, telemetry({ ...timed, fuelLaps: 11.3 }))).toEqual({ value: '+1', colour: ds.purpose.delta.faster });
    // The fuel time is not a term: SimHub makes it from the same laps of fuel at the last three laps'
    // average, a lap the laps left are not counted at, and a margin read from it could disagree with
    // the Refuel beside it.
    for (const fuelSeconds of [0, 900, 1500, 86399]) {
      expect(marginOn(fuelItems, telemetry({ ...timed, fuelLaps: 13.6, fuelSeconds })).value).toBe('+5');
    }
  });

  test('which of the two it draws follows SessionProgress, as the session page’s counter does', () => {
    // A timed session with the setting on laps compares laps, and a lap-counted one with the setting
    // on time has no time to compare and says so, which is what the session page draws there too.
    // The timed session's laps are the time left over its best lap and the one the clock runs out on,
    // twenty minutes at two being eleven, as the lap-counted session's `RemainingLaps` is (#1008).
    // The same 2.1 laps in hand is four minutes and a fifth at two minutes a lap.
    const both = { fuelLaps: 13.1, lapsLeft: 11, fuelSeconds: 1500, sessionSeconds: 1200, bestLap: 120 } as const;
    expect(marginOn(fuelItems, telemetry({ ...both, progress: 'auto' })).value).toBe('+4');
    expect(marginOn(fuelItems, telemetry({ ...both, progress: 'laps' })).value).toBe('+2.1');
    expect(marginOn(fuelItems, telemetry({ ...both, progress: 'time' })).value).toBe('+4');
    expect(marginOn(fuelItems, telemetry({ ...both, sessionSeconds: A_WEEK, progress: 'time' })).value).toBe(NO_VALUE);
    // The unit says which, since the same box means laps on one grid and minutes on the next.
    const unit = (props: Props): unknown => evalNcalc(formulaOf(fuelItems, 'toEnd.unit', 'Text'), props);
    expect(unit(telemetry({ progress: 'laps' }))).toBe('laps');
    expect(unit(telemetry({ sessionSeconds: 1200, progress: 'auto' }))).toBe('min');
  });

  test('it says nothing before a lap has said what one costs, and nothing in a race with no end', () => {
    const absent = { value: NO_VALUE, colour: ds.color.text.primary };
    // The gate the estimate beside it reads: a completed lap and a consumption to derive from.
    expect(marginOn(fuelItems, telemetry({ lapsDone: 0 }))).toEqual(absent);
    expect(marginOn(fuelItems, telemetry({ perLap: 0 }))).toEqual(absent);
    // And a race with an end to reach. A race with no end would otherwise draw the whole of the
    // range as spare: `+13.1` laps to a flag nobody is going to wave.
    expect(marginOn(fuelItems, telemetry({ lapsLeft: 0 }))).toEqual(absent);
    expect(marginOn(fuelItems, telemetry({ sessionSeconds: A_WEEK, progress: 'time' }))).toEqual(absent);
    // A timed race with no lap timed yet has a clock but no laps to count it in, and a margin read
    // against no laps at all would be the whole range in the green.
    expect(marginOn(fuelItems, telemetry({ sessionSeconds: 1200, progress: 'auto', bestLap: null }))).toEqual(absent);
  });

  test('and nothing outside a race, where the end is real and the flag is not', () => {
    const absent = { value: NO_VALUE, colour: ds.color.text.primary };
    // A thirty-minute open practice with eight minutes of fuel: a length, so the length gate passes,
    // and `−24` MIN in the danger red for the rest of the session if nothing else asks, the clock and
    // the lap it runs out on. A low tank in practice is a low tank, which is the same reason the
    // low-fuel warning stays off this expression.
    const practice = { sessionSeconds: 1800, fuelLaps: 4 } as const;
    expect(marginOn(fuelItems, telemetry({ ...practice, type: 'Race' })).value).toBe(`${MINUS}24`);
    for (const type of ['Practice', 'Open Practice', 'Qualify', 'Open Qualify', 'Lone Qualify', 'Offline Testing', 'Warmup', '']) {
      expect({ type, ...marginOn(fuelItems, telemetry({ ...practice, type })) }).toEqual({ type, ...absent });
    }
    // Including with the setting on laps, which is the same session read the other way round.
    expect(marginOn(fuelItems, telemetry({ type: 'Practice', progress: 'laps' }))).toEqual(absent);
    // A session the sim does not name reads nothing rather than a verdict: an absence says the dash
    // cannot tell, where a red figure says the tank will not make it.
    expect(marginOn(fuelItems, telemetry({ type: null }))).toEqual(absent);
    // The comparison is on the upper-cased name, so a sim that shouts it is still a race.
    expect(marginOn(fuelItems, telemetry({ type: 'RACE' })).value).toBe('+2.1');
  });

  test('it agrees with the estimated laps and the laps left the same frame draws', () => {
    // The whole point of the field: the two terms are on the dash already, the fuel page carrying
    // the estimate and the session page the laps left, and a driver was subtracting them himself.
    // At the line, that is; mid-lap the margin is more by the part of the lap already run.
    const props = telemetry({ fuelLaps: 9.4, lapsLeft: 7, progress: 'laps' });
    const estimate = Number(evalNcalc(formulaOf(fuelItems, 'lapsLeft.value', 'Text'), props));
    const left = Number(evalNcalc(formulaOf(sessionItems, 'lapsLeft.value', 'Text'), props));
    expect({ estimate, left }).toEqual({ estimate: 9.4, left: 7 });
    expect(marginOn(fuelItems, props).value).toBe(`+${(estimate - left).toFixed(1)}`);
  });
});

/**
 * A timed race runs past its clock, and the margin runs with it (#1024).
 *
 * The leader finishes the lap the clock runs out on, and so does every car, so the end the tank has
 * to reach is the end of that lap and not the clock. The margin used to be the range's time less the
 * time left, and read a tank that ran dry on the last lap as reaching the flag, in the green.
 */
describe('a timed margin counts the lap the clock runs out on', () => {
  /**
   * SimHub's frame at the ticket's numbers: a 110 s lap, the range its laps of fuel make at it, and
   * the car as far round the lap as the time run says, unless a case puts it elsewhere.
   */
  const lastLaps = (opts: { sessionSeconds: number; fuelSeconds: number; lapRun: number; position?: number }): Props => {
    const fuelLaps = opts.fuelSeconds / 110;
    return {
      ...telemetry({
        sessionSeconds: opts.sessionSeconds,
        sessionLength: 2700,
        bestLap: 110,
        lapRun: opts.lapRun,
        position: opts.position ?? opts.lapRun / 110,
        fuelSeconds: opts.fuelSeconds,
        fuelLaps,
        perLap: 2.84,
        progress: 'auto',
      }),
      'DataCorePlugin.GameData.Fuel': fuelLaps * 2.84,
    };
  };
  const read = (props: Props): { value: unknown; colour: unknown } => ({
    value: evalNcalc(fuelToEndText(), props),
    colour: evalNcalc(fuelToEndColour(), props),
  });
  const refuel = (props: Props): unknown => evalNcalc(formulaOf(fuelItems, 'toAdd.value', 'Text'), props);

  test('a tank that lasts past the clock and not to the line reads short, in the slower colour', () => {
    // A minute on the clock and a minute and a half of fuel at the line, on a 1:50 lap: the clock
    // runs out 60 s in, the car runs the other 50 s of the lap, and the tank is dry 20 s from the
    // flag. This read `+1` in the green, the range's 90 s less the clock's 60.
    const short = lastLaps({ sessionSeconds: 60, fuelSeconds: 90, lapRun: 0 });
    expect(read(short)).toEqual({ value: `${MINUS}0`, colour: ds.purpose.delta.slower });
    // A lap's fuel and a minute more is a minute in hand at the end of that lap.
    expect(read(lastLaps({ sessionSeconds: 60, fuelSeconds: 170, lapRun: 0 }))).toEqual({ value: '+1', colour: ds.purpose.delta.faster });
  });

  test('after the clock, the margin is the range less the half lap still to run', () => {
    // The clock at nought and the car half way round its last lap, #1017 keeping the session timed.
    // The margin was the whole range less nothing, green whatever was in the tank. It is now the
    // range less the half lap still to run: 275 s of fuel less 55 s is 220 s, nearly four minutes.
    expect(read(lastLaps({ sessionSeconds: 0, fuelSeconds: 275, lapRun: 55 }))).toEqual({ value: '+4', colour: ds.purpose.delta.faster });
    // 44 s of fuel with 55 s still to run is a car that stops on its last lap, which read `+1`.
    const dry = lastLaps({ sessionSeconds: 0, fuelSeconds: 44, lapRun: 55 });
    expect({ ...read(dry), refuel: refuel(dry) }).toEqual({ value: `${MINUS}0`, colour: ds.purpose.delta.slower, refuel: '0.3' });
  });

  test('the part of the lap already run is not counted again, so a tank that makes the flag reads green', () => {
    // 100 s into a 110 s last lap with 66 s of fuel: 10 s to run and the tank makes it. Counted with
    // the lap whole, this read `−1` in red beside a Refuel of 0.8, which is a stop on the last lap.
    const home = lastLaps({ sessionSeconds: 0, fuelSeconds: 66, lapRun: 100 });
    expect({ ...read(home), refuel: refuel(home) }).toEqual({ value: '+1', colour: ds.purpose.delta.faster, refuel: '0.0' });
  });

  test('the part run is measured by distance, so a slow lap does not take too much off', () => {
    // 100 s into a lap through the pit lane, a third of the way round: the time over the best lap
    // would call it nine tenths run and the Refuel short. Two thirds of a lap is still to run, and
    // 66 s of fuel does not cover it.
    const pitLap = lastLaps({ sessionSeconds: 0, fuelSeconds: 66, lapRun: 100, position: 0.33 });
    expect({ ...read(pitLap), refuel: refuel(pitLap) }).toEqual({ value: `${MINUS}0`, colour: ds.purpose.delta.slower, refuel: '0.2' });
  });

  test('where the distance and the time disagree at the line, the lap counts whole', () => {
    // Just past the line, the lap time at nought and the distance not yet round: the lap is the one
    // just begun, and counting the distance would take nearly a lap off it.
    const justOver = lastLaps({ sessionSeconds: 0, fuelSeconds: 66, lapRun: 1, position: 0.99 });
    expect(read(justOver)).toEqual({ value: `${MINUS}1`, colour: ds.purpose.delta.slower });
    // And a game with no position, which SimHub publishes as `-1`, counts the lap whole too.
    expect(read(lastLaps({ sessionSeconds: 0, fuelSeconds: 66, lapRun: 100, position: -1 }))).toEqual({ value: `${MINUS}1`, colour: ds.purpose.delta.slower });
  });

  test('it reads short exactly where the Refuel asks for fuel, so the two never disagree', () => {
    // The margin and the Refuel are one subtraction, the laps of fuel against the laps left, read
    // two ways, so the verdict and the instruction beside it agree about whether the tank makes it
    // in every frame of a timed race: at the line, mid-lap, on a slow lap and after the clock, and
    // wherever the distance round the lap stands against the time run.
    const perLap = 2.84;
    for (const sessionSeconds of [0, 30, 60, 170, 600, 1200]) {
      for (const lapRun of [0, 40, 109, 150]) {
        for (const position of [0, 0.3, 0.9, 0.99]) {
          for (const fuelLaps of [0.3, 0.8, 1.2, 2.5, 5.9, 11.4]) {
            const props = {
              ...telemetry({ sessionSeconds, sessionLength: 2700, bestLap: 110, lapRun, position, fuelLaps, perLap, progress: 'auto' }),
              'DataCorePlugin.GameData.Fuel': fuelLaps * perLap,
            };
            const short = evalNcalc(fuelToEndColour(), props) === ds.purpose.delta.slower;
            // To the hundredth, not the tenth the field draws, so a shortfall under 0.05 is still one.
            const toAdd = Number(evalNcalc(formulaOf(fuelItems, 'toAdd.value', 'Text').replaceAll("'0.0'", "'0.00'"), props));
            expect({ sessionSeconds, lapRun, position, fuelLaps, short }).toEqual({ sessionSeconds, lapRun, position, fuelLaps, short: toAdd > 0 });
          }
        }
      }
    }
  });
});

describe('the same reading on band D', () => {
  const margin = BAND_PAGES.fuel!.find((field) => field.id === 'toEnd')!;

  test('band D draws the figure the module draws, from the one expression', () => {
    expect(margin.bind).toBe(formulaOf(fuelItems, 'toEnd.value', 'Text'));
    expect(margin.colorBind).toBe(formulaOf(fuelItems, 'toEnd.value', 'TextColor'));
  });

  test('and its absence sits in the cells the field is cut for, so no column moves at the first lap', () => {
    expect(evalNcalc(margin.bind, telemetry({ lapsDone: 0 }))).toBe(NO_VALUE);
    expect(NO_VALUE.length).toBeLessThanOrEqual(margin.chars.digits);
  });
});

describe('every reading the margin can draw is inside the width it declares', () => {
  const margin = BAND_PAGES.fuel!.find((field) => field.id === 'toEnd')!;
  const value = fuelItems.find((item) => item.name === 'toEnd.value')!;

  /**
   * Every reading the expression arrives at over the races a driver can be in.
   *
   * Enumerated from the formula rather than written out, because the point is the range of the
   * expression and not a list of strings somebody thought of: `0.0` writes a sign, the integer digits
   * and a decimal whatever the numbers are, so the widest reading belongs to the longest race and not
   * to the sample the field was drawn with.
   */
  const readings = (): Set<string> => {
    const seen = new Set<string>();
    const add = (props: Props): void => {
      seen.add(String(evalNcalc(margin.bind, props)));
    };
    // Lap-counted races from a sprint to the longest oval, at both ends of the tank.
    for (const lapsLeft of [1, 5, 11, 50, 78, 199, 367, 999]) {
      for (const fuelLaps of [0, 1.4, 13.1, 30, 47.8, 999.9]) add(telemetry({ lapsLeft, fuelLaps, progress: 'laps' }));
    }
    // Timed races from a ten-minute sprint to a day's endurance, less a second so the session is
    // still timed, at both ends of the tank, from a minute's lap to the Nordschleife's eight, at the
    // line and at the end of a lap, with the car at the line by distance or nearly round.
    for (const sessionSeconds of [0, 600, 1200, 3600, 21600, 43200, 86399]) {
      for (const fuelLaps of [0, 1.4, 13.1, 47.8, 999.9]) {
        for (const bestLap of [60, 95, 120, 480]) {
          for (const lapRun of [0, bestLap]) {
            for (const position of [0, 0.99]) add(telemetry({ sessionSeconds, sessionLength: 86400, fuelLaps, bestLap, lapRun, position, progress: 'time' }));
          }
        }
      }
    }
    // And the absence, which is a reading too.
    add(telemetry({ lapsDone: 0 }));
    return seen;
  };

  /** Width of a string in the cells the item is laid in, which is how WPF will measure it. */
  const cellWidth = (text: string, mono: { charWidth: number; specialCharsWidth: number; specialChars?: string }): number => {
    const specials = [...text].filter((c) => mono.specialChars?.includes(c) ?? false).length;
    return (text.length - specials) * mono.charWidth + specials * mono.specialCharsWidth;
  };

  test('the widest declared is really the widest, so the fit tests are measuring the right string', () => {
    const mono = value.monospace!;
    const declared = cellWidth(FUEL_TO_END_WIDEST, mono);
    for (const reading of readings()) {
      expect({ reading, inside: cellWidth(reading, mono) <= declared }).toEqual({ reading, inside: true });
    }
    // And it is a reading of the right shape: five digit cells and a decimal, the sign in one of them.
    expect(FUEL_TO_END_WIDEST).toBe(`${MINUS}999.9`);
    expect(declared).toBeLessThanOrEqual(monoWidth(mono, CHARS.margin));
  });

  test('the two drawings are cut for it and say so, which is what the fit tests read', () => {
    // The box comes from `chars` and the measurement from `widest`; a field with one and not the
    // other is either clipped or unchecked, and the margin managed both at once.
    expect({ chars: value.monospace && monoWidth(value.monospace, CHARS.margin), widest: value.widest }).toEqual({
      chars: monoWidth(cells('SemiBold', value.fontSize), CHARS.margin),
      widest: FUEL_TO_END_WIDEST,
    });
    expect({ chars: margin.chars, widest: margin.numeralWidest }).toEqual({ chars: CHARS.margin, widest: FUEL_TO_END_WIDEST });
    // Not `widest`, which on a band field means a proportional word and would take the cells and the
    // colour binding with it.
    expect(margin.widest).toBeUndefined();
  });

  test('and the longest of them fits the box every drawing gives it', () => {
    // The global fit tests do this for every face now that the item declares what it draws; this is
    // the one box this file has in hand, and it fails here first if the budget is ever narrowed.
    expect(cellWidth(FUEL_TO_END_WIDEST, value.monospace!)).toBeLessThan(value.rect.width);
  });
});

/**
 * Where the unit lands, which is the half of this reading the VM photographed as wrong.
 *
 * The margin is cut for `−999.9` and draws `−4`, so a mark placed at the end of the cells stood four
 * empty ones off its own figure -- nearer `EST. LAPS` than the number it names, which is what made
 * it read as a field of its own rather than as a unit. The gap now follows the figure, and the check
 * is a runtime one because a design-time gap proves nothing about a bound value: the item is drawn
 * with `+1.4` on it and the dash draws whatever the race hands it. #387.
 */
describe('the margin carries its unit beside the figure, not at the end of the budget', () => {
  const bandItems = [...walkItems(bandPageItems('fuel', rect(0, 420, 1920, 60), '', true))].filter((item): item is TextItem => item.kind === 'text');
  /** Five pixels on band D and six in a module's field, which is each drawing's own gap. */
  const SURFACES = [
    { name: 'the fuel module', items: fuelItems, gap: 6 },
    { name: 'band D', items: bandItems, gap: 5 },
  ] as const;

  /** One reading per length the two forms can arrive at, from the absence to the longest race. */
  const READINGS: readonly { name: string; props: Props }[] = [
    { name: 'no reading yet', props: telemetry({ lapsDone: 0 }) },
    { name: 'two laps in hand', props: telemetry({ fuelLaps: 13.1, lapsLeft: 11, progress: 'laps' }) },
    { name: 'ten laps short', props: telemetry({ fuelLaps: 1.4, lapsLeft: 11, progress: 'laps' }) },
    { name: 'a whole race short', props: telemetry({ fuelLaps: 1.4, lapsLeft: 199, progress: 'laps' }) },
    { name: 'five minutes in hand', props: telemetry({ sessionSeconds: 1200, fuelLaps: 13.6, progress: 'time' }) },
    { name: 'a day short', props: telemetry({ sessionSeconds: 86399, fuelLaps: 0, progress: 'time' }) },
  ];

  for (const surface of SURFACES) {
    test(`on ${surface.name}`, () => {
      const drawn = surface.items.find((item) => item.name === 'toEnd.value')!;
      const mono = drawn.monospace!;
      const text = formulaOf(surface.items, 'toEnd.value', 'Text');
      const left = formulaOf(surface.items, 'toEnd.unit', 'Left');
      const placed = READINGS.map((reading) => {
        const figure = String(evalNcalc(text, reading.props));
        // Rounded to the pixel: an item's own rect is snapped to whole pixels and a binding is not.
        const gap = Math.round(Number(evalNcalc(left, reading.props)) - (drawn.rect.left + cellsOf(figure, mono)));
        return { reading: reading.name, figure, gap };
      });
      for (const each of placed) expect(each).toMatchObject({ gap: surface.gap });
      // And the readings really do take the mark to four places, or the check above was one length.
      expect(new Set(placed.map((each) => each.figure.length)).size).toBeGreaterThanOrEqual(4);
    });
  }
});

/** Width of a string in the cells an item is laid in, which is how WPF will measure it. */
const cellsOf = (text: string, mono: { charWidth: number; specialCharsWidth: number; specialChars?: string }): number => {
  const specials = [...text].filter((c) => mono.specialChars?.includes(c) ?? false).length;
  return (text.length - specials) * mono.charWidth + specials * mono.specialCharsWidth;
};

describe('the fuel page goes red where every light does', () => {
  test('the level, the estimate and the bar under the level read the rig threshold', () => {
    // #791: one answer to "am I low" on a rig. The page went red under a lap of its own beside a strip
    // lit at the threshold the panel sets, so a number in the panel moved only one of them.
    const module = MODULES.find((m) => m.id === 'fuel')!;
    const items = [...walkItems(module.build({ frame: rect(0, 0, 1007, 211), density: 'zone', prefix: '' }))];
    const red = `'${ds.purpose.fuel.low}'`;
    const lowPainted = items.flatMap((item) => expressionsOf(item).filter((f) => f.includes(red)).map((f) => ({ name: item.name, f })));
    // The margin to the end is red when it is short, which is a sign and not a threshold; it is the
    // one other reading on the page that uses the colour, and is left out.
    expect(lowPainted.map((x) => x.name).sort()).toEqual(['gauge', 'lapsLeft.value', 'level.value', 'toEnd.value']);
    for (const { name, f } of lowPainted.filter((x) => x.name !== 'toEnd.value')) {
      expect({ name, threshold: f.includes(tankIsLow()) }).toEqual({ name, threshold: true });
      expect({ name, rig: f.includes('[OpenDash.LightsLowFuelLaps]') }).toEqual({ name, rig: true });
    }
  });
});
