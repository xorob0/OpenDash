/**
 * One body per reading (#619). ADR 0009 and ADR 0014 put a reading behind one name so that two
 * drawings cannot disagree about it, and these are the readings that had more than one body: pit
 * speeding, the forward gears, the assist levels, the engine speed and the temperature unit.
 *
 * Each describe block asserts that every reader is built from the one helper, by identity of the
 * expression rather than by a substring of it, and then drives the helper with the telemetry a rig
 * would hand it, because "the strip and the box read the same thing" is worth little if the thing
 * they both read is wrong. Pit speeding was exactly that: the box's body compared metres per second
 * against a property SimHub never publishes, so it never lit, while the strip's lit.
 */
import { describe, expect, test } from 'bun:test';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { ncalc, type TextItem } from '../src/generator.ts';
import { PIT_EFFECTS, SPOTTER_EFFECTS } from '../src/leds/effects.ts';
import { GEARS } from '../src/leds/gear.ts';
import { pitStates, spotterStates } from '../src/leds/states.ts';
import { gearGhosts } from '../src/components/gear.ts';
import { assistValue, hasAssist } from '../src/cards/assist.ts';
import { settingCells } from '../src/modules/carSettings.ts';
import { rect } from '../src/design/geometry.ts';
import { racePage, TELEMETRY_TRACES } from '../src/screens/pitwall.ts';
import { isInPitLane, pitLimiterOn, pitSpeeding, PIT_SPEEDING_MARGIN, spotterCar } from '../src/second/values.ts';
import { FORWARD_GEARS } from '../src/shift.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const { and, not } = ncalc;

const GAME = 'DataCorePlugin.GameData.';

describe('pit speeding has one body, read by the strip and the box alike', () => {
  // The catalogue's own rows, before `ALL_EFFECTS` puts each behind the panel's switch for it.
  const effect = (id: string) => PIT_EFFECTS.find((e) => e.id === id)!;
  const state = (id: string) => pitStates().find((s) => s.id === id)!;

  test('the strip effect, its blink and the box picture are the same expression', () => {
    expect(effect('pit.speeding').when).toBe(pitSpeeding());
    expect(effect('pit.speeding').blinkWhen).toBe(pitSpeeding());
    expect(state('speeding').raised).toBe(pitSpeeding());
    // The lane colour gives way to speeding on the same definition, so the two never overlap.
    expect(effect('pit.lane').when).toBe(and(isInPitLane(), not(pitSpeeding())));
  });

  test('neither reads the metres-per-second twin SimHub does not publish', () => {
    // `PitLimiterSpeedMs` carries `[DoNotExpose]` (StatusDataBase.cs), and DataCorePlugin skips
    // every member that does, so a formula reading it reads null on every rig.
    expect(pitSpeeding()).not.toContain('PitLimiterSpeedMs');
    expect(pitSpeeding()).toContain(`[${GAME}PitLimiterSpeed]`);
    expect(pitSpeeding()).toContain(`[${GAME}SpeedLocal]`);
  });

  /**
   * A rig in the pit lane of a 60 km/h track. SimHub publishes the limit and the speed in the unit
   * the driver set, both through `KmhToLocalSpeedUnit`, and publishes no `PitLimiterSpeedMs`.
   */
  const KMH_PER_MPH = 1.609344;
  const rig = (unit: 'KMH' | 'MPH', kmh: number, extra: Props = {}): Props => {
    const local = (v: number): number => (unit === 'MPH' ? v / KMH_PER_MPH : v);
    return {
      [`${GAME}IsInPitLane`]: 1,
      [`${GAME}SpeedLocalUnit`]: unit,
      [`${GAME}PitLimiterSpeed`]: local(60),
      [`${GAME}SpeedLocal`]: local(kmh),
      [`${GAME}SpeedKmh`]: kmh,
      ...extra,
    };
  };
  const lit = (props: Props) => ({ strip: evalNcalc(effect('pit.speeding').when, props), box: evalNcalc(state('speeding').raised, props) });

  for (const unit of ['KMH', 'MPH'] as const) {
    test(`on a rig set to ${unit}, both light over the limit and neither at it`, () => {
      expect(lit(rig(unit, 0))).toEqual({ strip: false, box: false });
      expect(lit(rig(unit, 60))).toEqual({ strip: false, box: false });
      // Within the margin, in the driver's own unit: the limiter settling a hair over its figure.
      expect(lit(rig(unit, 60.5))).toEqual({ strip: false, box: false });
      expect(lit(rig(unit, 66))).toEqual({ strip: true, box: true });
      // Out of the lane the limit is nobody's business.
      expect(lit(rig(unit, 120, { [`${GAME}IsInPitLane`]: 0 }))).toEqual({ strip: false, box: false });
    });
  }

  test('a track whose limit the sim does not say is never speeding, at any speed', () => {
    const props = rig('KMH', 200);
    delete props[`${GAME}PitLimiterSpeed`];
    expect(lit(props)).toEqual({ strip: false, box: false });
    expect(lit({ ...rig('KMH', 200), [`${GAME}PitLimiterSpeed`]: 0 })).toEqual({ strip: false, box: false });
  });

  test('the margin is one unit of whatever unit the driver set', () => {
    expect(PIT_SPEEDING_MARGIN).toBe(1);
    const at = (unit: 'KMH' | 'MPH', local: number) => lit({ ...rig(unit, 0), [`${GAME}SpeedLocal`]: local });
    expect(at('KMH', 61)).toEqual({ strip: false, box: false });
    expect(at('KMH', 61.1)).toEqual({ strip: true, box: true });
    expect(at('MPH', 60 / KMH_PER_MPH + 1.1)).toEqual({ strip: true, box: true });
  });

  test('the box reads the limiter, the lane and the spotter through the null-safe helpers', () => {
    expect(state('limiterOutOfLane').raised).toBe(and(pitLimiterOn(), not(isInPitLane())));
    expect(state('limiterInLane').raised).toBe(and(pitLimiterOn(), isInPitLane()));
    const left = spotterStates(1).find((s) => s.id === 'carLeft')!.raised;
    expect(left).toContain(spotterCar('Left'));
    expect(effect('pit.limiter').when).toBe(pitLimiterOn());
    expect(SPOTTER_EFFECTS.find((e) => e.side === 'left')?.when).toBe(spotterCar('Left'));
    expect(SPOTTER_EFFECTS.find((e) => e.side === 'right')?.when).toBe(spotterCar('Right'));
  });
});

describe('there is one list of forward gears', () => {
  test('the box draws reverse, neutral and every gear of it', () => {
    expect(GEARS).toEqual(['R', 'N', ...FORWARD_GEARS]);
  });

  test('the pit wall trace plots every gear of it, and the last one at the top of its axis', () => {
    const gear = TELEMETRY_TRACES.find((t) => t.id === 'gear')!.series()[0]!;
    const plotted = (g: string): unknown => evalNcalc(String(gear.bind), { [`${GAME}Gear`]: g });
    for (const g of FORWARD_GEARS) expect({ g, plotted: plotted(g) }).toEqual({ g, plotted: Number(g) });
    expect(plotted('R')).toBe(-1);
    expect(plotted('N')).toBe(0);
    expect(gear.max).toBe(Number(FORWARD_GEARS[FORWARD_GEARS.length - 1]));
  });

  test('the ghosts name the gear either side for every gear of it, and nothing past either end', () => {
    const items = gearGhosts(rect(0, 0, 600, 300), 120, { gap: 8 }, 'g') as TextItem[];
    const text = (side: 'below' | 'above', g: string): unknown =>
      evalNcalc(String(items.find((i) => i.name === `g.${side}`)!.bindings!.Text!.formula), { [`${GAME}Gear`]: g });
    FORWARD_GEARS.forEach((g, i) => {
      expect({ g, below: text('below', g), above: text('above', g) }).toEqual({ g, below: FORWARD_GEARS[i - 1] ?? '', above: FORWARD_GEARS[i + 1] ?? '' });
    });
  });
});

describe('the pit wall assist cells read what the cards read', () => {
  const items = [...walkItems(racePage(1920, 1080).items)].filter((i): i is TextItem => i.kind === 'text');
  for (const [assist, sample] of [['tc', '3'], ['abs', '2']] as const) {
    test(`${assist} binds the text and the colour of assistValue`, () => {
      const cell = items.find((i) => i.name === `race.track.${assist}.value`)!;
      const card = assistValue(assist, sample);
      expect(cell.bindings?.Text?.formula).toBe(card.bind);
      expect(cell.bindings?.TextColor?.formula).toBe(card.colorBind);
      // ...so a car with the system switched off reads OFF there as it does on the card, not 0.
      const off = { 'DataCorePlugin.GameRawData.Telemetry.dcTractionControl': 0, 'DataCorePlugin.GameRawData.Telemetry.dcABS': 0, [`${GAME}TCLevel`]: 0, [`${GAME}ABSLevel`]: 0 };
      expect(evalNcalc(String(cell.bindings!.Text!.formula), off)).toBe('OFF');
    });

    test(`the car settings page's ${assist} cell binds the same, and hides by the same test`, () => {
      const field = settingCells({ frame: rect(0, 0, 600, 300), density: 'panel', prefix: 's.' }, 24).find((f) => f.id === assist)!;
      expect(field.value.bind).toBe(assistValue(assist, sample).bind);
      expect(field.value.colorBind).toBe(assistValue(assist, sample).colorBind);
      expect(field.visibleBind).toBe(hasAssist(assist));
    });
  }
});

describe('no reader outside the one body spells these properties again', () => {
  /**
   * Each property, and the files allowed to read it with `game(...)`. `contract.ts` keeps its own
   * guarded read of the temperature unit because `second/values.ts` imports the contract, and the
   * default it picks by unit is evaluated where the contract is built.
   */
  const OWNERS: Record<string, readonly string[]> = {
    PitLimiterSpeed: ['second/values.ts'],
    PitLimiterOn: ['second/values.ts'],
    IsInPitLane: ['second/values.ts'],
    SpotterCar: ['second/values.ts'],
    TemperatureUnit: ['second/values.ts', 'contract.ts'],
    Rpms: ['shift.ts'],
    TCLevel: ['second/values.ts'],
    ABSLevel: ['second/values.ts'],
  };
  const SRC = join(import.meta.dir, '../src');
  const files = (dir: string): string[] =>
    readdirSync(dir).flatMap((name) => {
      const path = join(dir, name);
      return statSync(path).isDirectory() ? files(path) : path.endsWith('.ts') ? [path] : [];
    });

  for (const [property, owners] of Object.entries(OWNERS)) {
    test(`${property} is read in ${owners.join(' and ')} alone`, () => {
      const read = new RegExp(`game\\(\\s*['"\`]${property}`);
      const readers = files(SRC)
        .filter((path) => read.test(readFileSync(path, 'utf8')))
        .map((path) => relative(SRC, path));
      expect(readers.sort()).toEqual([...owners].sort());
    });
  }
});

