/**
 * The Porsche's low-fuel alarm, read on every sim the release claims (#1126).
 *
 * The alarm and its reading read iRacing's raw `FuelLevel`, which SimHub's Assetto Corsa reader, like
 * its ACC and Le Mans Ultimate ones, never publishes, so a driver on AC who ran the tank down to two
 * litres got no alarm. They read SimHub's `Fuel` now, which every reader fills from its own litres and
 * converts to gallons on a gallons profile, turned back into litres since the car's ten is in litres.
 *
 * On iRacing SimHub's `Fuel` is `FuelLevel` (`IRacingManager.GD_Fuel` in 9.12.6), so the alarm there
 * is pinned against the condition it used to be, tank by tank, in both units.
 *
 * The condition draws nothing and runs in any process. The reading is drawn by the theme, whose colours
 * are its own, so it is read in a process drawing the Porsche's, as `themeFuelPage.test.ts` does.
 */
import { describe, expect, test } from 'bun:test';
import { FACE_SIZES } from '../src/contract.ts';
import { ncalc, type LayerItem, type TextItem } from '../src/generator.ts';
import { inTheCar, LITRES_TO_GALLONS, sim } from '../src/second/values.ts';
import { THEME_DRAWINGS } from '../src/themes/drawings.ts';
import { drawsInThisProcess } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { lowFuelAlarm } from '../src/themes/porsche/register.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES } from '../src/zones/index.ts';
import { evalNcalc, Single, type Props } from './ncalcEval.ts';

const FUEL = 'DataCorePlugin.GameData.Fuel';
const FUEL_UNIT = 'DataCorePlugin.GameData.FuelUnit';
const FUEL_LEVEL = 'DataCorePlugin.GameRawData.Telemetry.FuelLevel';
const IS_ON_TRACK = 'DataCorePlugin.GameRawData.Telemetry.IsOnTrack';

type Unit = 'Liters' | 'Gallons';

/** SimHub's `Fuel` for a tank of `litres`, as `GameManagerBase` converts it for the profile. */
const converted = (litres: number, unit: Unit): number => (unit === 'Gallons' ? litres * LITRES_TO_GALLONS : litres);

/** Assetto Corsa: `Physics.Fuel` in litres becomes `Fuel`, and no iRacing raw property is there at all. */
const ac = (litres: number, unit: Unit = 'Liters'): Props => ({ [FUEL]: converted(litres, unit), [FUEL_UNIT]: unit });

/** iRacing in the car: `FuelLevel` is an irsdk_float, and `Fuel` is that float widened, then converted. */
const iracing = (litres: number, unit: Unit = 'Liters', onTrack = true): Props => ({
  [FUEL]: converted(Math.fround(litres), unit),
  [FUEL_UNIT]: unit,
  [FUEL_LEVEL]: new Single(litres),
  [IS_ON_TRACK]: onTrack,
});

const raised = (props: Props): unknown => evalNcalc(lowFuelAlarm(), props);

describe("the Porsche's low-fuel alarm", () => {
  test('is raised on Assetto Corsa with 2.1 litres in the tank, as the emulator pins it in the yellow scenario', () => {
    expect(raised(ac(2.1))).toBe(true);
  });

  test('is clear on Assetto Corsa with 30 litres', () => {
    expect(raised(ac(30))).toBe(false);
  });

  test('is clear on Assetto Corsa in the garage, where the plugin says the driver is not in the car (#561)', () => {
    expect(raised({ ...ac(2.1), 'OpenDash.DriverInCar': false })).toBe(false);
    expect(raised({ ...ac(2.1), 'OpenDash.DriverInCar': true })).toBe(true);
  });

  test('is raised on iRacing with 8 litres, and clear with 30', () => {
    expect(raised(iracing(8))).toBe(true);
    expect(raised(iracing(30))).toBe(false);
  });

  test('counts litres on a gallons profile, where SimHub has converted the tank', () => {
    // 30 litres is 7.9 gallons, which a threshold read in the profile's unit would call low.
    expect(raised(ac(30, 'Gallons'))).toBe(false);
    expect(raised(ac(2.1, 'Gallons'))).toBe(true);
    expect(raised(iracing(30, 'Gallons'))).toBe(false);
    expect(raised(iracing(8, 'Gallons'))).toBe(true);
  });

  test('reads no iRacing raw property', () => {
    expect(lowFuelAlarm()).not.toContain('GameRawData.Telemetry.FuelLevel');
    // The one raw read left is `inTheCar`'s, the plugin's fallback, which reads as in the car where
    // nothing says.
    expect(lowFuelAlarm()).toContain(sim.driverInCar());
    expect(lowFuelAlarm().replace(inTheCar(), '')).not.toContain('GameRawData');
  });

  test('is clear with no sim running, when SimHub publishes no tank', () => {
    expect(raised({})).toBe(false);
  });

  test("on iRacing is what it was when it read FuelLevel, tank by tank in both units, in the car and out of it", () => {
    const { and, isNull, lt, not, num, raw } = ncalc;
    const before = and(inTheCar(), not(isNull(raw('FuelLevel'))), lt(raw('FuelLevel'), num(10)));
    for (const unit of ['Liters', 'Gallons'] as const) {
      for (const onTrack of [true, false]) {
        for (let tenths = 0; tenths <= 200; tenths++) {
          const frame = iracing(tenths / 10, unit, onTrack);
          const at = { unit, onTrack, litres: tenths / 10 };
          expect({ ...at, raised: raised(frame) }).toEqual({ ...at, raised: evalNcalc(before, frame) });
        }
      }
    }
  });
});

const PORSCHE = 'porsche';

if (drawsInThisProcess(PORSCHE)) {
  /** The alarm's layer and its reading, as the theme draws them on each face it ships. */
  const alarms = (): { at: string; layer: LayerItem; reading: TextItem }[] =>
    THEMES[PORSCHE]!.anatomy.sizes.map((size) => {
      const at = `${size.width}x${size.height}`;
      const layout = ZONE_FACES.find((f) => f.width === size.width && f.height === size.height)!;
      const face = FACE_SIZES.find((f) => f.width === size.width && f.height === size.height)!;
      const items = THEME_DRAWINGS[PORSCHE]!.takeovers!({ layout, face, regions: THEMES[PORSCHE]!.anatomy.regions(layout), withRevBar: true });
      const layer = items.find((item): item is LayerItem => item.kind === 'layer' && item.name === 'lowFuel')!;
      const reading = [...walkItems([layer])].find((item): item is TextItem => item.kind === 'text' && item.name === 'lowFuel.reading')!;
      return { at, layer, reading };
    });

  describe("the Porsche's low-fuel alarm as the theme draws it", () => {
    test('is shown on the alarm condition, and its reading is the tank in litres on Assetto Corsa and on iRacing', () => {
      for (const { at, layer, reading } of alarms()) {
        const text = (props: Props): unknown => evalNcalc(String(reading.bindings?.Text?.formula), props);
        expect({ at, visible: layer.bindings?.Visible?.formula }).toEqual({ at, visible: lowFuelAlarm() });
        expect({ at, ac: text(ac(2.1)), acGallons: text(ac(2.1, 'Gallons')) }).toEqual({ at, ac: 'Fuel level 2.1', acGallons: 'Fuel level 2.1' });
        expect({ at, iracing: text(iracing(8.6)), iracingGallons: text(iracing(8.6, 'Gallons')) }).toEqual({
          at,
          iracing: 'Fuel level 8.6',
          iracingGallons: 'Fuel level 8.6',
        });
      }
    });
  });
}

if (THEME_ID === DEFAULT_THEME_ID && !drawsInThisProcess(PORSCHE)) {
  test(
    "the Porsche's low-fuel alarm as the theme draws it, in a process drawing its colours",
    () => {
      const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: PORSCHE }, stdout: 'pipe', stderr: 'pipe' });
      const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
      expect({ theme: PORSCHE, exit: run.exitCode, output }).toEqual({ theme: PORSCHE, exit: 0, output: '' });
    },
    120_000,
  );
}
