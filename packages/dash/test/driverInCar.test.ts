/**
 * Whether the driver is in the car, on every sim, #561.
 *
 * The car alerts, the change notifications, the AiM's engine warning and the Porsche's fuel alarm all
 * ask it, and until #561 they asked iRacing's `Telemetry.IsOnTrack` alone. On Assetto Corsa that is
 * absent, so every caller got its own default: the alerts "no", so IGNITION OFF never showed, and the
 * notifications "yes", so a brake bias moved in the setup screen raised one. The plugin answers the
 * question per sim as `DriverInCar` (ADR 0021, #1101), Assetto Corsa's from `Graphics.Status` at
 * `AC_LIVE`, and the callers now read that ahead of `IsOnTrack`.
 *
 * Each condition is evaluated over a frame as SimHub and the plugin would publish it on each sim, with
 * SimHub's `changed()` window answered by the test, since it is state a single frame cannot hold. Only
 * what main already exported is imported, so that these fail there on behaviour rather than on a
 * missing name.
 */
import { describe, expect, test } from 'bun:test';
import { changeNotificationVisible } from '../src/components/changeNotification.ts';
import { alertCondition, isFlag } from '../src/flags.ts';
import { inTheCar, sim } from '../src/second/values.ts';
import { TRACKED_VALUES } from '../src/second/tracked.ts';
import { lowFuelAlarm } from '../src/themes/porsche/register.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const T = 'DataCorePlugin.GameRawData.Telemetry';
const G = 'DataCorePlugin.GameData';
const DRIVER_IN_CAR = 'OpenDash.DriverInCar';
const IS_ON_TRACK = `${T}.IsOnTrack`;

/**
 * A `when` with every SimHub `changed()` window answered: true for the windows over `moving`, false
 * for the rest. A window over anything this test does not recognise fails it rather than guessing.
 */
const answerWindows = (expression: string, moving: string): string =>
  expression.replace(/changed\(3000, (isnull\(\[([^\]]+)\], [^()]*\)|\[([^\]]+)\])\)/g, (_all, _read, inIsnull: string | undefined, bare: string | undefined) =>
    (inIsnull ?? bare) === moving ? 'true' : 'false',
  );

const holds = (expression: string, frame: Props, moving = ''): boolean => {
  const answered = answerWindows(expression, moving);
  if (answered.includes('changed(')) throw new Error(`a window this test does not answer is left in ${answered}`);
  return evalNcalc(answered, frame) === true;
};

/** The ignition alert, IGNITION OFF over band D. */
const ignitionAlert = (): string => {
  const found = alertCondition('ignition');
  if (isFlag(found)) throw new Error('ignition is a flag');
  return found.when;
};

/** The brake bias notification, which Assetto Corsa fills from `Physics.BrakeBias`. */
const biasNotification = (): string => changeNotificationVisible('bias');
const BIAS = `${G}.BrakeBias`;

/** Out on the circuit with the engine cut: SimHub's ignition off, out of the lane, nothing else moving. */
const ENGINE_CUT: Props = {
  [`${G}.IsInPitLane`]: 0,
  [`${G}.EngineIgnitionOn`]: 0,
  [`${G}.EngineStarted`]: 0,
  [`${G}.Rpms`]: 0,
  [BIAS]: 54,
  [`${G}.LastLapTime`]: 98.4,
  [`${G}.CurrentLapTime`]: 41.2,
};

/**
 * Assetto Corsa as SimHub publishes it: no iRacing raw property, and the plugin's answer where it is
 * installed, `true` for `AC_LIVE` and `false` for the menus, the setup screen's `AC_OFF`, a replay and a
 * pause.
 */
const ac = (driverInCar: boolean | undefined, frame: Props = ENGINE_CUT): Props => (driverInCar === undefined ? { ...frame } : { ...frame, [DRIVER_IN_CAR]: driverInCar });

/** iRacing: `IsOnTrack` is there, and the plugin's answer is that same reading where it is installed. */
const iracing = (onTrack: boolean, plugin: boolean, frame: Props = ENGINE_CUT): Props => ({ ...frame, [IS_ON_TRACK]: onTrack, ...(plugin ? { [DRIVER_IN_CAR]: onTrack } : {}) });

describe('every caller asks the plugin first', () => {
  test('the car alerts, as "no" where nothing answers', () => {
    for (const id of ['ignition', 'engine', 'incident', 'pushToPass', 'headlightFlash']) {
      const found = alertCondition(id);
      if (isFlag(found)) throw new Error(`${id} is a flag`);
      expect({ id, reads: found.when.includes(sim.driverInCar(false)) }).toEqual({ id, reads: true });
    }
  });

  test('every change notification and the Porsche fuel alarm, as "yes" where nothing answers', () => {
    for (const value of TRACKED_VALUES) expect({ id: value.id, reads: changeNotificationVisible(value.id).includes(sim.driverInCar(true)) }).toEqual({ id: value.id, reads: true });
    expect(lowFuelAlarm()).toContain(sim.driverInCar(true));
  });

  test("and iRacing's reading behind it is textually what it was, #311's condition on any indirection", () => {
    expect(inTheCar(false)).toBe(`isnull([${IS_ON_TRACK}], false)`);
    expect(inTheCar(true)).toBe(`isnull([${IS_ON_TRACK}], true)`);
    expect(sim.driverInCar(false)).toBe(`isnull([${DRIVER_IN_CAR}], ${inTheCar(false)})`);
    expect(sim.driverInCar(true)).toBe(`isnull([${DRIVER_IN_CAR}], ${inTheCar(true)})`);
  });
});

describe('on Assetto Corsa', () => {
  test('the engine cut on track raises IGNITION OFF, which was silent there', () => {
    expect(holds(ignitionAlert(), ac(true))).toBe(true);
    // With the car running it does not.
    expect(holds(ignitionAlert(), ac(true, { ...ENGINE_CUT, [`${G}.EngineIgnitionOn`]: 1, [`${G}.EngineStarted`]: 1, [`${G}.Rpms`]: 900 }))).toBe(false);
  });

  test('and not in the menus, a replay or a pause, where SimHub reads every engine as off', () => {
    expect(holds(ignitionAlert(), ac(false))).toBe(false);
  });

  test('a brake bias moved in the setup screen, a replay or a pause raises no notification', () => {
    expect(holds(biasNotification(), ac(false), BIAS)).toBe(false);
  });

  test('and moved in the car it does', () => {
    expect(holds(biasNotification(), ac(true), BIAS)).toBe(true);
    expect(holds(biasNotification(), ac(true))).toBe(false);
  });

  test('the Porsche fuel alarm keeps quiet in the menus', () => {
    const low = { [`${G}.Fuel`]: 2.1, [`${G}.FuelUnit`]: 'Liters' };
    expect(evalNcalc(lowFuelAlarm(), ac(true, low))).toBe(true);
    expect(evalNcalc(lowFuelAlarm(), ac(false, low))).toBe(false);
  });

  test('without the plugin, each caller draws what it drew before', () => {
    expect(holds(ignitionAlert(), ac(undefined))).toBe(false);
    expect(holds(biasNotification(), ac(undefined), BIAS)).toBe(true);
  });
});

describe('on iRacing nothing changes', () => {
  test("with the plugin or without it, every caller reads what IsOnTrack says, as it did", () => {
    for (const onTrack of [true, false]) {
      for (const plugin of [true, false]) {
        const at = { onTrack, plugin };
        const frame = iracing(onTrack, plugin);
        expect({ ...at, ignition: holds(ignitionAlert(), frame) }).toEqual({ ...at, ignition: holds(ignitionAlert().split(sim.driverInCar(false)).join(inTheCar(false)), frame) });
        expect({ ...at, ignition: holds(ignitionAlert(), frame) }).toEqual({ ...at, ignition: onTrack });
        expect({ ...at, bias: holds(biasNotification(), frame, BIAS) }).toEqual({ ...at, bias: onTrack });
      }
    }
  });
});
