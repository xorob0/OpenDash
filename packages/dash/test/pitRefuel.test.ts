/**
 * The pit service's fuel order, read as a driver on either unit would read it (#604).
 *
 * iRacing publishes `PitSvFuel` in litres whatever the display unit, and SimHub converts its own
 * `Fuel` to gallons on a gallons profile but passes raw telemetry through as it is. Pit view drew
 * the litres beside a unit bound to `FuelUnit`, so a 40 litre order read `40.0 gal`, and band D drew
 * the same litres with no unit beside a tank in gallons. So these evaluate the NCalc the build emits
 * for both faces, under both values of `FuelUnit`, and hold the figure and the unit word together.
 *
 * The word is the other half of the ticket. The fuel page's estimate, what the laps left will burn
 * less the tank, and the pit service's order were both labelled `Refuel`, in two colours; a driver
 * moving between the fuel page, band D and pit view read two different numbers under one word.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import type { TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { fuelUnit, LITRES_TO_GALLONS, NO_VALUE, pitRefuel } from '../src/second/values.ts';
import { bandPageItems } from '../src/zones/bandPages.ts';
import { evalNcalc, Single, type Props } from './ncalcEval.ts';

const PIT_FUEL = 'DataCorePlugin.GameRawData.Telemetry.PitSvFuel';
const FUEL_UNIT = 'DataCorePlugin.GameData.FuelUnit';

/** A 40 litre order, as iRacing hands it over: an irsdk_float, which SimHub passes on as a Single. */
const order = (unit: 'Liters' | 'Gallons' | null, litres: number | null = 40): Props => ({
  ...(unit === null ? {} : { [FUEL_UNIT]: unit }),
  ...(litres === null ? {} : { [PIT_FUEL]: new Single(litres) }),
});

const textItems = (items: Iterable<{ kind: string }>): TextItem[] => [...items].filter((item): item is TextItem => item.kind === 'text');
const moduleItems = (id: string): TextItem[] =>
  textItems(walkItems(MODULES.find((m) => m.id === id)!.build({ frame: rect(0, 0, 1007, 211), density: 'zone', prefix: '' })));
const bandItems = textItems(walkItems(bandPageItems('fuel', rect(0, 420, 1920, 60), '', true)));
const pitItems = moduleItems('pitView');
const fuelItems = moduleItems('fuel');

const item = (items: readonly TextItem[], name: string): TextItem => {
  const found = items.find((it) => it.name === name);
  if (!found) throw new Error(`no item ${name}`);
  return found;
};
const textOn = (items: readonly TextItem[], name: string, props: Props): unknown => {
  const formula = item(items, name).bindings?.Text?.formula;
  if (typeof formula !== 'string') throw new Error(`no Text formula on ${name}`);
  return evalNcalc(formula, props);
};

describe('the pit service fuel helper', () => {
  test('is the litres iRacing publishes on a litres profile, and on one that names no unit', () => {
    for (const unit of ['Liters', null] as const) {
      expect({ unit, value: Number(evalNcalc(pitRefuel(), order(unit))) }).toEqual({ unit, value: Math.fround(40) });
      expect({ unit, word: evalNcalc(fuelUnit(), order(unit)) }).toEqual({ unit, word: 'L' });
    }
  });

  test('is those litres in gallons on a gallons profile, by the ratio SimHub converts the tank with', () => {
    expect(evalNcalc(pitRefuel(), order('Gallons'))).toBeCloseTo(40 * 0.264172, 6);
    expect(evalNcalc(fuelUnit(), order('Gallons'))).toBe('gal');
    // `GameManagerBase.GetFuelConvertRatio` in SimHub 9.12.6. A more exact figure would put the order
    // a digit apart from a tank of the same fuel.
    expect(LITRES_TO_GALLONS).toBe(0.264172);
  });
});

describe('pit view draws the order in the unit it names', () => {
  test('40 litres reads 40.0 L on a litres profile and 10.6 gal on a gallons one', () => {
    const read = (props: Props): unknown[] => [textOn(pitItems, 'refuel.value', props), textOn(pitItems, 'refuel.unit', props)];
    expect(read(order('Liters'))).toEqual(['40.0', 'L']);
    expect(read(order(null))).toEqual(['40.0', 'L']);
    expect(read(order('Gallons'))).toEqual(['10.6', 'gal']);
  });

  test('and says it has none where the sim publishes no order', () => {
    expect(textOn(pitItems, 'refuel.value', order('Gallons', null))).toBe(NO_VALUE);
  });
});

describe("band D's fuel page draws the order in the tank's unit", () => {
  test('40 litres reads 40.00 on a litres profile and 10.57 on a gallons one, beside a tank in the same unit', () => {
    expect(textOn(bandItems, 'refuel.value', order('Liters'))).toBe('40.00');
    expect(textOn(bandItems, 'refuel.value', order('Gallons'))).toBe('10.57');
    expect(textOn(bandItems, 'fuel.unit', order('Gallons'))).toBe('gal');
  });

  test('and the absence pit view draws, rather than an order of nothing, where the sim publishes none', () => {
    expect(textOn(bandItems, 'refuel.value', order('Liters', null))).toBe(NO_VALUE);
  });
});

describe('two quantities, two words, one colour', () => {
  test("the fuel page's estimate is To add, and Refuel is the pit service's order wherever it is drawn", () => {
    expect(item(fuelItems, 'toAdd.label').text).toBe('To add');
    expect(fuelItems.some((it) => it.text === 'Refuel')).toBe(false);
    for (const [face, items] of [
      ['pit view', pitItems],
      ['band D', bandItems],
    ] as const) {
      expect({ face, label: item(items, 'refuel.label').text }).toEqual({ face, label: 'Refuel' });
      expect({ face, reads: String(item(items, 'refuel.value').bindings?.Text?.formula).includes(PIT_FUEL) }).toEqual({ face, reads: true });
    }
  });

  test('all three are caution amber, a figure to act on, and none is the low-fuel red', () => {
    const colours = {
      toAdd: item(fuelItems, 'toAdd.value').textColor,
      pitView: item(pitItems, 'refuel.value').textColor,
      bandD: item(bandItems, 'refuel.value').textColor,
    };
    expect(colours).toEqual({ toAdd: ds.color.caution.primary, pitView: ds.color.caution.primary, bandD: ds.color.caution.primary });
  });
});
