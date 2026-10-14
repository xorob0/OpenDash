/**
 * What the emulators' scenarios hold, read from the files as each emulator merges them: the state a
 * capture of one shows, where that state is easy to get wrong by a value inherited from `race.json`
 * or by a driver switched off around it (#1138). The emulators play them; this reads them, so the
 * merge rules below are the emulators' own, from `Scenario.cs` beside each.
 */
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { POP_UP_SECONDS } from '../packages/dash/src/components/popUp.ts';
import { scenarios } from './emulator.ts';
import { ASSETTO_CORSA, IRACING, SIMS, type Sim } from './sims.ts';

type Json = Record<string, unknown>;
interface DriverSpec extends Json {
  type?: string;
  enabled?: boolean;
}
interface TimelineEvent extends Json {
  at?: number;
  driver?: DriverSpec | DriverSpec[];
}

const repoRoot = path.resolve(import.meta.dir, '..');
const file = (sim: Sim, name: string): string => path.join(repoRoot, sim.sourceDir, 'scenarios', name.endsWith('.json') ? name : `${name}.json`);

/**
 * A scenario with its `extends` chain merged underneath, as the emulator merges it: AC's `values` and
 * `pin` key by key and iRacing's `variables` one after another, every other key replaced whole. So
 * a child's `timeline` is the whole of its timeline, and its `drivers` are race.json's unless it names
 * its own.
 */
function merged(sim: Sim, name: string): Json {
  const own = Bun.JSONC.parse(readFileSync(file(sim, name), 'utf8')) as Json;
  const base = typeof own.extends === 'string' ? merged(sim, own.extends) : {};
  const result: Json = { ...base };
  for (const [key, value] of Object.entries(own)) {
    if (key === 'extends') continue;
    if (sim === ASSETTO_CORSA && (key === 'values' || key === 'pin')) result[key] = { ...((base[key] as Json) ?? {}), ...(value as Json) };
    else if (sim === IRACING && (key === 'variables' || key === 'exclude')) result[key] = [...((base[key] as unknown[]) ?? []), ...(value as unknown[])];
    else result[key] = value;
  }
  return result;
}

const timeline = (s: Json): TimelineEvent[] => [...((s.timeline as TimelineEvent[]) ?? [])].sort((a, b) => (a.at ?? 0) - (b.at ?? 0));
const driversOf = (e: TimelineEvent): DriverSpec[] => (e.driver === undefined ? [] : Array.isArray(e.driver) ? e.driver : [e.driver]);

/** Whether the timeline switches the driver off at the start and never on again. */
function stoppedThroughout(s: Json, type: string): boolean {
  const changes = timeline(s).flatMap((e) => driversOf(e).filter((d) => d.type === type && d.enabled !== undefined).map((d) => ({ at: e.at ?? 0, on: d.enabled })));
  return changes.length > 0 && changes[0]!.at === 0 && changes.every((c) => c.on === false);
}

/** The player's current lap time at the start, in seconds: the emulator's initial value of it. */
function currentLapSeconds(sim: Sim, s: Json): number | undefined {
  if (sim === ASSETTO_CORSA) {
    const value = ((s.pin as Json)?.['Graphics.iCurrentTime'] ?? (s.values as Json)?.['Graphics.iCurrentTime']) as number | undefined;
    return value === undefined ? undefined : value / 1000;
  }
  const variables = ((s.variables as Json[]) ?? []).filter((v) => v.name === 'LapCurrentLapTime');
  return variables.at(-1)?.value as number | undefined;
}

/**
 * The cars of the field in the pits once the timeline has run its course: each one the field marks
 * `"pit": true`, which no timeline can take out, and the last `pitCars` the timeline gives it. AC's
 * are named, iRacing's are car indices.
 */
function carsInThePits(s: Json): (string | number)[] {
  const field = ((s.drivers as DriverSpec[]) ?? []).find((d) => d.type === 'field');
  if (!field) return [];
  const cars = (field.cars as Json[]) ?? [];
  const id = (car: Json): string | number => (car.name ?? car.idx) as string | number;
  let pitCars = (field.pitCars as (string | number)[]) ?? [];
  for (const e of timeline(s)) for (const d of driversOf(e)) if (d.type === 'field' && Array.isArray(d.pitCars)) pitCars = d.pitCars as (string | number)[];
  return [...cars.filter((c) => c.pit === true).map(id), ...pitCars];
}

describe('a scenario that stops the lap clock', () => {
  // The lap pop-up is out while the current lap time is under its window, which a running clock
  // passes three seconds after the line. A stopped clock under it holds the pop-up over the gear for
  // the whole run, as Assetto Corsa's pit did at 300 ms (#1138).
  for (const sim of SIMS) {
    for (const name of scenarios(sim)) {
      const s = merged(sim, name);
      if (!stoppedThroughout(s, 'lapTimer')) continue;
      test(`${sim.id} ${name} holds it past the lap pop-up's ${POP_UP_SECONDS} s`, () => {
        expect(currentLapSeconds(sim, s)).toBeGreaterThan(POP_UP_SECONDS);
      });
    }
  }

  test('is found on both sims, so the check above is not of nothing', () => {
    expect(scenarios(ASSETTO_CORSA).filter((n) => stoppedThroughout(merged(ASSETTO_CORSA, n), 'lapTimer'))).toContain('pit');
    expect(scenarios(IRACING).filter((n) => stoppedThroughout(merged(IRACING, n), 'lapTimer'))).toContain('pit');
  });
});

describe('the other cars in the pits', () => {
  // The count the scenarios' comments and READMEs give, which is what a capture is checked against
  // on the VM. race.json parks one car in its box for the whole race on both sims, Diego Ramos on
  // Assetto Corsa and car 8 on iRacing, and every scenario that extends it inherits him (#1138).
  test.each([
    [ASSETTO_CORSA, 'race', ['Diego Ramos']],
    [ASSETTO_CORSA, 'yellow', ['Diego Ramos', 'Tom Hughes', 'Nina Berg']],
    [ASSETTO_CORSA, 'pit', ['Diego Ramos', 'Sophie Laurent', 'Hanna Kowalska', 'Omar Haddad']],
    [IRACING, 'race', [8]],
    [IRACING, 'yellow', [8, 4, 17]],
    [IRACING, 'pit', [8, 4, 9, 17]],
  ] as const)('%#: %o %s', (sim, name, cars) => {
    expect(carsInThePits(merged(sim, name))).toEqual([...cars]);
  });
});
