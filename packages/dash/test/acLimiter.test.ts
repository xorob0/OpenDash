/**
 * The limiter on Assetto Corsa, where SimHub's `PitLimiterOn` is the pit lane (#1122, #558).
 *
 * SimHub's AC reader answers `GameData.PitLimiterOn` with `GD_IsInPitLane`, so read alone it lit every
 * limiter reading in the lane whether the driver had engaged the limiter or not, kept them dark on track
 * with the limiter on, and never let "Engage limiter" be raised. The plugin publishes the raw
 * `Physics.PitLimiterOn` as `OpenDash.PitLimiterOn` there (ADR 0021), and every reading goes through
 * `pitLimiterOn` in `second/values.ts`, which takes it and falls back to SimHub's member.
 *
 * Each surface is evaluated as it ships, against two AC frames that tell the two apart: in the lane with
 * SimHub's member on and the raw limiter off, where every limiter reading is dark and the reminder is out;
 * and on track with the raw limiter on and SimHub's member off, where every one of them is lit and the
 * reminder is "Disengage limiter". Then iRacing's frames, with no plugin answer at all, where SimHub's
 * member is the limiter bit and is read exactly as before. The Porsche draws its own limiter pictogram,
 * so it is built in a process drawing its colours, as `engineWarnings.test.ts` builds it.
 */
import { describe, expect, test } from 'bun:test';
import { PIT_ALERTS, pitAlertVisible } from '../src/components/pitAlerts.ts';
import type { Item, RectangleItem } from '../src/generator.ts';
import { ALL_EFFECTS, PIT_EFFECTS } from '../src/leds/effects.ts';
import { pitStates } from '../src/leds/states.ts';
import { buildThemeFace, drawsInThisProcess } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { ds, THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { bandPageItems } from '../src/zones/bandPages.ts';
import { TELLTALE_PAGE, TELLTALES } from '../src/zones/telltales.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

/**
 * Assetto Corsa with the plugin: SimHub's `PitLimiterOn` is the lane, the raw limiter is `rawLimiter`, and
 * the plugin publishes the raw limiter and that every car has one.
 */
const ac = (lane: boolean, rawLimiter: boolean): Props => ({
  'DataCorePlugin.GameData.IsInPitLane': lane ? 1 : 0,
  'DataCorePlugin.GameData.PitLimiterOn': lane ? 1 : 0,
  'DataCorePlugin.GameRawData.Physics.PitLimiterOn': rawLimiter ? 1 : 0,
  'OpenDash.PitLimiterOn': rawLimiter,
  'OpenDash.CarHasPitLimiter': true,
});

/** iRacing with no plugin answer: SimHub's `PitLimiterOn` is the limiter bit, and the car has the toggle. */
const iracing = (lane: boolean, limiter: boolean): Props => ({
  'DataCorePlugin.GameData.IsInPitLane': lane ? 1 : 0,
  'DataCorePlugin.GameData.PitLimiterOn': limiter ? 1 : 0,
  'DataCorePlugin.GameRawData.Telemetry.dcPitSpeedLimiterToggle': 1,
});

/** In the lane with the limiter left off, SimHub's member on because it is the lane. */
const AC_LANE_OFF = ac(true, false);
/** On track with the limiter on, SimHub's member off because it is not the lane. */
const AC_TRACK_ON = ac(false, true);

const holds = (expression: string, props: Props): boolean => Boolean(evalNcalc(expression, props));

/** Which of the pit family is out: the one rectangle shows one band at a time. */
const pitAlertOut = (props: Props): string | undefined => PIT_ALERTS.find((alert) => holds(pitAlertVisible(alert.id), props))?.id;
/** Which of the flag box's pit states are raised. */
const boxRaised = (props: Props): string[] => pitStates().filter((state) => holds(state.raised, props)).map((state) => state.id);

const formulaOf = (item: Item, binding: 'Visible' | 'Text'): string => {
  const bound = item.bindings?.[binding];
  if (bound?.mode !== 'formula' || typeof bound.formula !== 'string') throw new Error(`${item.name} has no ${binding} formula`);
  return bound.formula;
};

if (THEME_ID === DEFAULT_THEME_ID) {
  describe('the limiter on Assetto Corsa', () => {
    const lamp = TELLTALES.find((each) => each.id === 'limiter')!;
    const strip = PIT_EFFECTS.find((effect) => effect.id === 'pit.limiter')!;
    const drawn = ALL_EFFECTS().find((effect) => effect.id === 'pit.limiter')!;

    test("in the lane with the limiter off, band D's limiter lamp, the strip and the flag box are dark", () => {
      expect({
        lamp: holds(lamp.on!, AC_LANE_OFF),
        strip: holds(strip.when, AC_LANE_OFF),
        blink: holds(strip.blinkWhen!, AC_LANE_OFF),
        drawn: holds(drawn.when, AC_LANE_OFF),
        box: boxRaised(AC_LANE_OFF),
      }).toEqual({ lamp: false, strip: false, blink: false, drawn: false, box: [] });
    });

    test("and band D's pit family asks for the limiter", () => {
      expect(pitAlertOut(AC_LANE_OFF)).toBe('engage');
    });

    test('on track with the limiter on, every one of them is lit, and the pit family says to disengage it', () => {
      expect({
        lamp: holds(lamp.on!, AC_TRACK_ON),
        strip: holds(strip.when, AC_TRACK_ON),
        blink: holds(strip.blinkWhen!, AC_TRACK_ON),
        drawn: holds(drawn.when, AC_TRACK_ON),
        box: boxRaised(AC_TRACK_ON),
        alert: pitAlertOut(AC_TRACK_ON),
      }).toEqual({ lamp: true, strip: true, blink: true, drawn: true, box: ['limiterOutOfLane'], alert: 'disengage' });
    });

    test('in the lane with the limiter on, the limiter is what is shown', () => {
      const props = ac(true, true);
      expect({ lamp: holds(lamp.on!, props), strip: holds(strip.when, props), box: boxRaised(props), alert: pitAlertOut(props) }).toEqual({
        lamp: true,
        strip: true,
        box: ['limiterInLane'],
        alert: 'limiter',
      });
    });

    test('the lamp as the band draws it takes the same answer, in its edge', () => {
      const band = { left: 0, top: 420, width: 1280, height: 60 };
      const chip = [...walkItems(bandPageItems(TELLTALE_PAGE, band, '', true))].find((item): item is RectangleItem => item.kind === 'rect' && item.name === 'limiter.chip')!;
      const edge = chip.border!.colorBinding!.formula as string;
      expect({ lane: evalNcalc(edge, AC_LANE_OFF), track: evalNcalc(edge, AC_TRACK_ON) }).toEqual({
        lane: ds.color.surface.raised,
        track: ds.purpose.telltale.neutral,
      });
    });

    test('the strip names the property it reads', () => {
      expect(strip.source).toContain('OpenDash.PitLimiterOn');
    });
  });

  describe('the limiter on iRacing, with no plugin answer', () => {
    const lamp = TELLTALES.find((each) => each.id === 'limiter')!;
    const strip = PIT_EFFECTS.find((effect) => effect.id === 'pit.limiter')!;

    test("is SimHub's limiter bit, read as before", () => {
      for (const lane of [false, true]) {
        for (const limiter of [false, true]) {
          const props = iracing(lane, limiter);
          expect({ lane, limiter, lamp: holds(lamp.on!, props), strip: holds(strip.when, props) }).toEqual({ lane, limiter, lamp: limiter, strip: limiter });
        }
      }
      expect({
        engage: pitAlertOut(iracing(true, false)),
        limiter: pitAlertOut(iracing(true, true)),
        disengage: pitAlertOut(iracing(false, true)),
        clear: pitAlertOut(iracing(false, false)),
      }).toEqual({ engage: 'engage', limiter: 'limiter', disengage: 'disengage', clear: undefined });
    });

    test('and a car without a limiter still gets no reminder', () => {
      const props = { ...iracing(true, false), 'DataCorePlugin.GameRawData.Telemetry.dcPitSpeedLimiterToggle': null };
      expect(pitAlertOut(props)).toBeUndefined();
    });
  });
}

/** Every item of every screen of a theme's every face whose name `wanted` accepts. */
function shippedItems(id: string, wanted: (name: string) => boolean): Item[] {
  return THEMES[id]!.anatomy.sizes.flatMap((size) => {
    const { built } = buildThemeFace(id, size, OPTS);
    return [built.main, ...built.zones].flatMap((dashboard) => dashboard.screens.flatMap((screen) => [...walkItems(screen.items)].filter((item) => wanted(item.name))));
  });
}

if (THEME_ID === 'porsche') {
  describe("the Porsche's limiter pictogram", () => {
    test('is dim in the lane with the limiter off on Assetto Corsa, and lit on track with it on', () => {
      const lit = shippedItems('porsche', (name) => name.endsWith('telltales.limiter.lit'));
      const dim = shippedItems('porsche', (name) => name.endsWith('telltales.limiter.dim'));
      expect(lit.length).toBeGreaterThan(0);
      expect(dim.length).toBe(lit.length);
      for (const item of lit) {
        const visible = formulaOf(item, 'Visible');
        expect({ lane: holds(visible, AC_LANE_OFF), track: holds(visible, AC_TRACK_ON) }).toEqual({ lane: false, track: true });
      }
      for (const item of dim) {
        const visible = formulaOf(item, 'Visible');
        expect({ lane: holds(visible, AC_LANE_OFF), track: holds(visible, AC_TRACK_ON) }).toEqual({ lane: true, track: false });
      }
    });
  });
}

if (THEME_ID === DEFAULT_THEME_ID && !drawsInThisProcess('porsche')) {
  test(
    "the Porsche's limiter, in a process drawing its colours",
    () => {
      const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: 'porsche' }, stdout: 'pipe', stderr: 'pipe' });
      const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
      expect({ exit: run.exitCode, output }).toEqual({ exit: 0, output: '' });
    },
    120_000,
  );
}
