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
 * The two forms are checked separately because they are two quantities: laps against `RemainingLaps`
 * in a lap-counted session, minutes against `SessionTimeLeft` in a timed one, and which of the two
 * is drawn follows `SessionProgress` exactly as the session page's own counter does.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import type { TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { NO_VALUE } from '../src/second/values.ts';
import { BAND_PAGES } from '../src/zones/bandPages.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

/** A wide zone body, which is the shape that keeps every field of the page. */
const pageItems = (id: string): TextItem[] => {
  const module = MODULES.find((m) => m.id === id)!;
  const items = [...walkItems(module.build({ frame: rect(0, 0, 1007, 211), density: 'zone', prefix: '' }))];
  return items.filter((item): item is TextItem => item.kind === 'text');
};

const formulaOf = (items: readonly TextItem[], name: string, target: 'Text' | 'TextColor'): string => {
  const binding = items.find((item) => item.name === name)?.bindings?.[target];
  const formula = binding?.formula;
  if (typeof formula !== 'string') throw new Error(`no ${target} formula on ${name}`);
  return formula;
};

/** A week of remaining time is what iRacing publishes for a session with no limit. */
const A_WEEK = 604800;

const fuelItems = pageItems('fuel');
const sessionItems = pageItems('session');

/** What the sim publishes, in the two shapes the margin subtracts. */
const telemetry = (
  opts: { lapsDone?: number; perLap?: number; fuelLaps?: number; lapsLeft?: number; fuelSeconds?: number; sessionSeconds?: number; progress?: string } = {},
): Props => ({
  'DataCorePlugin.GameData.CompletedLaps': opts.lapsDone ?? 3,
  'DataCorePlugin.Computed.Fuel_LitersPerLap': opts.perLap ?? 2.84,
  'DataCorePlugin.Computed.Fuel_RemainingLaps': opts.fuelLaps ?? 13.1,
  'DataCorePlugin.GameData.RemainingLaps': opts.lapsLeft ?? 11,
  'DataCorePlugin.Computed.Fuel_RemainingTime': opts.fuelSeconds ?? 1500,
  'DataCorePlugin.GameData.SessionTimeLeft': opts.sessionSeconds ?? A_WEEK,
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

  test('a timed session reads the fuel time less the time left, in whole minutes', () => {
    const timed = { sessionSeconds: 1200, progress: 'auto' } as const;
    expect(marginOn(fuelItems, telemetry({ ...timed, fuelSeconds: 1500 }))).toEqual({ value: '+5', colour: ds.purpose.delta.faster });
    expect(marginOn(fuelItems, telemetry({ ...timed, fuelSeconds: 900 }))).toEqual({ value: '−5', colour: ds.purpose.delta.slower });
    // A tenth of a minute is six seconds of a figure that moves by more than that every corner, so
    // the timed form is whole minutes and the field stays the width of the estimate beside it.
    expect(marginOn(fuelItems, telemetry({ ...timed, fuelSeconds: 1230 }))).toEqual({ value: '+1', colour: ds.purpose.delta.faster });
  });

  test('which of the two it draws follows SessionProgress, as the session page’s counter does', () => {
    // A timed session with the setting on laps compares laps, and a lap-counted one with the setting
    // on time has no time to compare and says so, which is what the session page draws there too.
    const both = { fuelLaps: 13.1, lapsLeft: 11, fuelSeconds: 1500, sessionSeconds: 1200 } as const;
    expect(marginOn(fuelItems, telemetry({ ...both, progress: 'auto' })).value).toBe('+5');
    expect(marginOn(fuelItems, telemetry({ ...both, progress: 'laps' })).value).toBe('+2.1');
    expect(marginOn(fuelItems, telemetry({ ...both, progress: 'time' })).value).toBe('+5');
    expect(marginOn(fuelItems, telemetry({ ...both, sessionSeconds: A_WEEK, progress: 'time' })).value).toBe(NO_VALUE);
    // The unit says which, since the same box means laps on one grid and minutes on the next.
    const unit = (props: Props): unknown => evalNcalc(formulaOf(fuelItems, 'toEnd.unit', 'Text'), props);
    expect(unit(telemetry({ progress: 'laps' }))).toBe('LAPS');
    expect(unit(telemetry({ sessionSeconds: 1200, progress: 'auto' }))).toBe('MIN');
  });

  test('it says nothing before a lap has said what one costs, and nothing in a race with no end', () => {
    const absent = { value: NO_VALUE, colour: ds.color.text.primary };
    // The gate the estimate beside it reads: a completed lap and a consumption to derive from.
    expect(marginOn(fuelItems, telemetry({ lapsDone: 0 }))).toEqual(absent);
    expect(marginOn(fuelItems, telemetry({ perLap: 0 }))).toEqual(absent);
    // And a race with an end to reach. A practice session would otherwise draw the whole of the
    // range as spare: `+13.1` laps to a flag nobody is going to wave.
    expect(marginOn(fuelItems, telemetry({ lapsLeft: 0 }))).toEqual(absent);
    expect(marginOn(fuelItems, telemetry({ sessionSeconds: A_WEEK, progress: 'time' }))).toEqual(absent);
  });

  test('it agrees with the estimated laps and the laps left the same frame draws', () => {
    // The whole point of the field: the two terms are on the dash already, the fuel page carrying
    // the estimate and the session page the laps left, and a driver was subtracting them himself.
    const props = telemetry({ fuelLaps: 9.4, lapsLeft: 7, progress: 'laps' });
    const estimate = Number(evalNcalc(formulaOf(fuelItems, 'lapsLeft.value', 'Text'), props));
    const left = Number(evalNcalc(formulaOf(sessionItems, 'lapsLeft.value', 'Text'), props));
    expect({ estimate, left }).toEqual({ estimate: 9.4, left: 7 });
    expect(marginOn(fuelItems, props).value).toBe(`+${(estimate - left).toFixed(1)}`);
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
