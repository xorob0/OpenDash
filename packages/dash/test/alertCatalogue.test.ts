/**
 * The alert catalogue, #109: one ordered list of the flags and the car alerts, and what each car
 * alert's own condition says.
 *
 * `alertBand.test.ts` holds the ranking, with every car alert stood in for by one truth value. This
 * file is the other half: that the conditions standing behind those values are the ones the ticket
 * asks for. Each is evaluated against a frame of telemetry with `evalNcalc`, and the one thing that
 * evaluator does not cover, SimHub's `changed()` window, is answered by the test, since the window is
 * state SimHub keeps between frames and a single frame cannot hold it.
 */
import { describe, expect, test } from 'bun:test';
import { changeNotificationVisible } from '../src/components/changeNotification.ts';
import { PIT_ALERTS } from '../src/components/pitAlerts.ts';
import { ALERT_CATALOGUE, ALERT_EVENT_MS, alertCondition, FLAG_CATALOGUE, isFlag, type CarAlert } from '../src/flags.ts';
import { engineStalled, ignitionOff, inTheCar, isInPitLane } from '../src/second/values.ts';
import { TRACKED_VALUES } from '../src/second/tracked.ts';
import { ncalc } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const T = 'DataCorePlugin.GameRawData.Telemetry';
const G = 'DataCorePlugin.GameData';
const LIMIT = 'DataCorePlugin.GameRawData.SessionData.WeekendInfo.WeekendOptions.IncidentLimit';

const carAlert = (id: string): CarAlert => {
  const found = alertCondition(id);
  if (isFlag(found)) throw new Error(`${id} is a flag`);
  return found;
};

/**
 * A car alert's condition on one frame, with SimHub's window answered as `moved`: whether the value
 * the window watches has changed within the last three seconds. Everything else is evaluated.
 */
function raised(id: string, frame: Props, moved = false): boolean {
  const when = carAlert(id).when.replace(/changed\(3000, (?:isnull\(\[[^\]]+\], 0\)|\[[^\]]+\])\)/g, moved ? 'true' : 'false');
  if (when.includes('changed(')) throw new Error(`a window this test does not answer is left in ${when}`);
  return Boolean(evalNcalc(when, frame));
}

/** Driving, out on the circuit, with the car running: the frame every case starts from. */
const DRIVING: Props = {
  [`${T}.IsOnTrack`]: true,
  [`${G}.IsInPitLane`]: 0,
  [`${G}.EngineIgnitionOn`]: 1,
  [`${T}.EngineWarnings`]: 0,
  [`${T}.PlayerCarMyIncidentCount`]: 0,
  [`${G}.PushToPassActive`]: 0,
  [`${T}.dcHeadlightFlash`]: false,
};
const IN_THE_GARAGE: Props = { [`${T}.IsOnTrack`]: false };
const IN_THE_LANE: Props = { [`${G}.IsInPitLane`]: 1 };

describe('one ordered list', () => {
  test('ranked in the canvas alert catalogue’s own numbering, with the departures written down', () => {
    // The order, and the canvas number each entry carries in PagesAndAlerts. Asserted as the list
    // rather than refreshed from the code, because it is what a driver is told first.
    expect(ALERT_CATALOGUE.map((c) => c.id)).toEqual([
      'ignition', // 2, above the stall as the pit family has it
      'engine', // 1
      'red', // 3
      'disqualify', // 4
      'furled', // 5
      'black', // 6
      'meatball', // 18, kept here: a flag calling this car in
      'caution', // 7 SafetyCar
      'yellowWaving', // where 9 DoubleYellow and 10 YellowSector would be
      'yellow', // 11
      'debris', // 19, with the flags that mean slow down
      'incident', // 15
      'blue', // 20
      'white', // 16, below the blue: see the rule in flags.ts
      'green', // 21
      'startSet', // 22
      'startReady', // 22
      'chequered', // 23
      'pushToPass', // 24
      'headlightFlash', // 25
    ]);
  });

  test('the lights read the flag half of it, in the same order, rather than a second list', () => {
    expect(FLAG_CATALOGUE).toEqual(ALERT_CATALOGUE.filter(isFlag));
    expect(FLAG_CATALOGUE).toHaveLength(15);
    expect(ALERT_CATALOGUE.filter((c) => !isFlag(c)).map((c) => c.id)).toEqual(['ignition', 'engine', 'incident', 'pushToPass', 'headlightFlash']);
  });

  test('every car alert is drawn in a token colour, and the two neutral ones are the ones in white', () => {
    const alerts = ALERT_CATALOGUE.filter((c): c is CarAlert => !isFlag(c));
    const colour = (c: CarAlert): string => (c.band.shape === 'chequer' ? '' : c.band.colour);
    expect(Object.fromEntries(alerts.map((c) => [c.id, colour(c)]))).toEqual({
      ignition: ds.purpose.alert.power,
      engine: ds.purpose.alert.power,
      incident: ds.purpose.alert.incident,
      pushToPass: ds.purpose.alert.p2p,
      headlightFlash: ds.purpose.alert.p2p,
    });
    expect(alerts.filter((c) => c.neutral).map((c) => c.id)).toEqual(['pushToPass', 'headlightFlash']);
  });
});

describe('nothing is raised with nobody in the car', () => {
  // SimHub reads iRacing's ignition as off whenever there is no voltage, which is whenever the
  // driver is standing in the garage, so every car alert reads #312's one test of being in the car.
  for (const alert of ALERT_CATALOGUE.filter((c): c is CarAlert => !isFlag(c))) {
    test(alert.id, () => {
      expect(alert.when).toContain(inTheCar());
      const everything: Props = {
        ...DRIVING,
        [`${G}.EngineIgnitionOn`]: 0,
        [`${T}.EngineWarnings`]: 8,
        [`${T}.PlayerCarMyIncidentCount`]: 4,
        [`${G}.PushToPassActive`]: 1,
        [`${T}.dcHeadlightFlash`]: true,
      };
      expect(raised(alert.id, { ...everything, ...IN_THE_GARAGE }, true)).toBe(false);
    });
  }

  test('and the test is the one the change notification reads, kept in one place', () => {
    expect(changeNotificationVisible(TRACKED_VALUES[0]!.id)).toContain(inTheCar());
  });
});

describe('the ignition and the stalled engine, out on the circuit', () => {
  test('the ignition off is raised on the circuit and nowhere else', () => {
    expect(raised('ignition', { ...DRIVING, [`${G}.EngineIgnitionOn`]: 0 })).toBe(true);
    expect(raised('ignition', DRIVING)).toBe(false);
    expect(raised('ignition', { ...DRIVING, [`${G}.EngineIgnitionOn`]: 0, ...IN_THE_LANE })).toBe(false);
    // A sim that publishes no ignition is not a car switched off.
    expect(raised('ignition', { ...DRIVING, [`${G}.EngineIgnitionOn`]: null })).toBe(false);
  });

  test('the stall is bit 8 of EngineWarnings, whatever else is set beside it', () => {
    expect(raised('engine', { ...DRIVING, [`${T}.EngineWarnings`]: 8 })).toBe(true);
    // 16 is the pit limiter and 24 is the two together: the stall is read out of the word, not off it.
    expect(raised('engine', { ...DRIVING, [`${T}.EngineWarnings`]: 16 })).toBe(false);
    expect(raised('engine', { ...DRIVING, [`${T}.EngineWarnings`]: 24 })).toBe(true);
    expect(raised('engine', { ...DRIVING, [`${T}.EngineWarnings`]: 1 + 2 + 4 + 16 + 32 + 64 })).toBe(false);
    expect(raised('engine', { ...DRIVING, [`${T}.EngineWarnings`]: 8, ...IN_THE_LANE })).toBe(false);
    expect(raised('engine', { ...DRIVING, [`${T}.EngineWarnings`]: null })).toBe(false);
  });

  test('the pit family says the same two things in the lane, from the same two readings', () => {
    // One condition drawn twice on one face would be two answers to one question, so the lane is
    // the pit family's and the circuit is the catalogue's, and they read the same predicates.
    const pit = (id: string): string => PIT_ALERTS.find((a) => a.id === id)!.when;
    expect(pit('ignition')).toBe(ncalc.and(isInPitLane(), ignitionOff()));
    expect(pit('engine')).toBe(ncalc.and(isInPitLane(), engineStalled()));
    expect(carAlert('ignition').when).toContain(ignitionOff());
    expect(carAlert('engine').when).toContain(engineStalled());
    expect(carAlert('ignition').when).toContain(ncalc.not(isInPitLane()));
    expect(carAlert('engine').when).toContain(ncalc.not(isInPitLane()));
    // And in the same order: the switch the driver can move first.
    expect(PIT_ALERTS.map((a) => a.id).filter((id) => id === 'ignition' || id === 'engine')).toEqual(['ignition', 'engine']);
  });
});

describe('the incident, an event and not a state', () => {
  const taken = { ...DRIVING, [`${T}.PlayerCarMyIncidentCount`]: 4 };

  test('is raised for the window after the count moves, and not after it', () => {
    expect(ALERT_EVENT_MS).toBe(ds.indicator.alert.durationMs);
    expect(raised('incident', taken, true)).toBe(true);
    expect(raised('incident', taken, false)).toBe(false);
  });

  test('is not raised by a count falling back to zero, which is a new session', () => {
    expect(raised('incident', DRIVING, true)).toBe(false);
    expect(raised('incident', { ...DRIVING, [`${T}.PlayerCarMyIncidentCount`]: null }, true)).toBe(false);
  });

  test('asks its window before anything else, or a session’s first incident would go untold', () => {
    // SimHub's NCalc stops an `and` at the first false, and `changed()` answers false the first time
    // it is asked. Behind "the count is above zero" it would first be asked on the first incident.
    expect(carAlert('incident').when.startsWith(`(changed(${ALERT_EVENT_MS}, `)).toBe(true);
  });

  test('writes the running count, and the limit only where the session has one', () => {
    const spec = carAlert('incident').band;
    if (spec.shape === 'chequer' || !spec.run) throw new Error('the incident writes a run');
    const text = (limit: unknown): unknown => evalNcalc(spec.run!.bind, { ...taken, [LIMIT]: limit });
    expect(text('17')).toBe('INCIDENT · 4x / 17');
    expect(text('unlimited')).toBe('INCIDENT · 4x');
    expect(text(null)).toBe('INCIDENT · 4x');
    expect(spec.run.sample).toBe('INCIDENT · 4x / 17');
  });
});

describe('the two that are the driver’s own hand', () => {
  test('push to pass is raised while it is in use', () => {
    // NCalc compares a boolean with a number in the boolean's type, so SimHub's `true` is `= 1`;
    // JavaScript's === does not, so this frame publishes the number the comparison arrives at.
    expect(raised('pushToPass', { ...DRIVING, [`${G}.PushToPassActive`]: 1 })).toBe(true);
    expect(raised('pushToPass', DRIVING)).toBe(false);
    expect(raised('pushToPass', { ...DRIVING, [`${G}.PushToPassActive`]: null })).toBe(false);
  });

  test('the headlight flash is raised for the window after the control moves, on a car that has one', () => {
    expect(raised('headlightFlash', DRIVING, true)).toBe(true);
    expect(raised('headlightFlash', DRIVING, false)).toBe(false);
    expect(raised('headlightFlash', { ...DRIVING, [`${T}.dcHeadlightFlash`]: null }, true)).toBe(false);
    expect(carAlert('headlightFlash').when.startsWith(`(changed(${ALERT_EVENT_MS}, `)).toBe(true);
  });
});
