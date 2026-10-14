/**
 * The wind, the sim clock and the track state, read on a sim that publishes them and on one that
 * does not (#1123, #1128).
 *
 * The wind and the sim clock read iRacing's raw `WindVel` and `SessionTimeOfDay`, which SimHub's Assetto
 * Corsa, ACC and Le Mans Ultimate readers never publish, and both stood a 0 in for the missing reading:
 * on AC a driver read `0 km/h` and `00:00`, or `12:00 AM`, as if measured. The track state read
 * `GameData.TrackGripStatus`, which SimHub 9.12.6 has for no game, so it read `--` everywhere, iRacing
 * included. It reads iRacing's raw `TrackWetness` now, and hides where that is not there.
 *
 * Each field is read as a rig draws it, item by item: its `Visible`, then its `Text`, evaluated against
 * a frame of each sim. A field may hide or read `--` on AC, and may not draw a 0 for a reading it was
 * never given; on iRacing it reads what it read before.
 *
 * The Porsche's strip draws the track state in colours of its own, and a theme with colours of its own
 * cannot be drawn in a process drawing another theme's (`faces.ts`), so the default process runs this
 * file again in one drawing the Porsche's, as `themeFuelPage.test.ts` does.
 */
import { describe, expect, test } from 'bun:test';
import { readTrace, tracedScenarios } from '../../../scripts/trace.ts';
import { CLOCK_FORMAT_SETTING, FACE_SIZES, PROPERTY_PREFIX, type ClockFormat } from '../src/contract.ts';
import { rect } from '../src/design/geometry.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { PIT_WALL_HEADER, pitWallHeader } from '../src/screens/pitwallHeader.ts';
import { hasTrackState, simClock, trackGrip, windKmh } from '../src/second/values.ts';
import { drawsInThisProcess } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { porscheStrip } from '../src/themes/porsche/strip.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { BAR_FIELD_SPECS } from '../src/zones/bar.ts';
import { bandCorners, bandPageItems } from '../src/zones/bandPages.ts';
import { ZONE_FACES } from '../src/zones/index.ts';
import { drawingOf } from './moduleItems.ts';
import { bindingExpression } from './monoGlyphs.ts';
import { evalNcalc, Single, type Props } from './ncalcEval.ts';

const RAW = 'DataCorePlugin.GameRawData.Telemetry.';
const CLOCK_FORMAT = `${PROPERTY_PREFIX}.${CLOCK_FORMAT_SETTING}`;

/** Assetto Corsa: SimHub's normalised members, and not one of iRacing's raw properties. */
const ac = (format: ClockFormat): Props => ({ [CLOCK_FORMAT]: format, 'DataCorePlugin.GameData.AirTemperature': 21.5 });

/**
 * iRacing as the emulator plays it: 2.1 m/s of wind, the clock at 14:22:12 and a dry track. `WindVel`
 * is an irsdk float, and `TrackWetness` an int SimHub passes through unconverted.
 */
const iracing = (format: ClockFormat, wetness = 1): Props => ({
  ...ac(format),
  [`${RAW}WindVel`]: new Single(2.1),
  [`${RAW}SessionTimeOfDay`]: 51732.58,
  [`${RAW}TrackWetness`]: wetness,
});

const texts = (items: readonly Item[]): TextItem[] => [...walkItems(items)].filter((i): i is TextItem => i.kind === 'text');

/** The texts a rig shows of `items` under `props`, each evaluated from its bindings; an empty one draws nothing. */
function shown(items: readonly TextItem[], props: Props): string[] {
  return items.flatMap((item) => {
    const visible = bindingExpression(item, 'Visible');
    if (visible !== '' && evalNcalc(visible, props) !== true) return [];
    const text = bindingExpression(item, 'Text');
    const drawn = text === '' ? item.text : String(evalNcalc(text, props));
    return drawn === '' ? [] : [drawn];
  });
}

const BAND = rect(0, 420, 1920, 60);
const band = (page: string): TextItem[] => texts(bandPageItems(page, BAND, '', true));
const field = (items: readonly TextItem[], id: string): TextItem[] => items.filter((i) => i.name.startsWith(`${id}.`));
const header = (): TextItem[] => texts(pitWallHeader('h', { frame: rect(0, 0, 1920, PIT_WALL_HEADER.height), pageName: 'Pit wall · test', page: 1, pages: 3 }));
const group = (id: string): TextItem[] => header().filter((i) => i.name.startsWith(`h.${id}.`));
const corners = (): TextItem[] => texts(bandCorners(BAND, 'corner.'));
const trackModule = (): TextItem[] =>
  texts(drawingOf(MODULES.find((m) => m.id === 'track')!, { frame: rect(10, 20, 300, 200), density: 'zone', prefix: 'track.' })).filter((i) => i.name === 'track.state');

/** The bar's simulated-time slot as its two bindings: the digits and the meridiem after them. */
const barSimTime = (props: Props): string[] => {
  const spec = BAR_FIELD_SPECS.find((f) => f.id === 'simulatedTime')!;
  const meridiem = evalNcalc(spec.suffix!.when!, props) === true ? [String(evalNcalc(spec.suffix!.bind!, props))] : [];
  return [String(evalNcalc(spec.bind, props)), ...meridiem].filter((w) => w !== '');
};

/** The five fields #1123 names, as a driver reads each. */
const windAndClock = (props: Props): Record<string, string[]> => ({
  bandWind: shown(field(band('weather'), 'wind'), props),
  headerWind: shown(group('wind'), props),
  headerSimClock: shown(group('simClock'), props),
  barSimulatedTime: barSimTime(props),
  bandSimClock: shown(field(corners(), 'corner.sim'), props),
});

/** A reading standing in for one the sim never gave: a zero wind, or midnight on either clock. */
const ZERO = /^0( km\/h)?$|^0?0:00$|^12:00$/;

describe('the wind and the sim clock on a sim that publishes neither', () => {
  for (const format of ['24h', '12h'] as const) {
    test(`draw no 0 and no midnight on Assetto Corsa, on a ${format} rig`, () => {
      const drawn = windAndClock(ac(format));
      const zeros = Object.entries(drawn).flatMap(([id, words]) => words.filter((w) => ZERO.test(w)).map((w) => `${id}: ${w}`));
      expect(zeros).toEqual([]);
    });
  }

  test('hide the wind and write the sim clock as --, with no AM or PM after it', () => {
    expect(windAndClock(ac('12h'))).toEqual({
      bandWind: [],
      headerWind: [],
      headerSimClock: ['Sim', '--'],
      barSimulatedTime: ['--'],
      bandSimClock: ['Sim', '--'],
    });
  });

  test('keep no room for an AM or PM on a 12-hour rig, so -- stands against the edge as a 24-hour clock does', () => {
    const value = corners().find((i) => i.name === 'corner.sim.value')!;
    const left = (props: Props): unknown => evalNcalc(bindingExpression(value, 'Left'), props);
    expect(left(ac('12h'))).toBe(left(iracing('24h')));
    expect(left(iracing('12h'))).not.toBe(left(iracing('24h')));
    const bar = BAR_FIELD_SPECS.find((f) => f.id === 'simulatedTime')!;
    expect({ ac: evalNcalc(bar.suffix!.when, ac('12h')), iracing: evalNcalc(bar.suffix!.when, iracing('12h')) }).toEqual({ ac: false, iracing: true });
  });

  test('read null rather than 0 from the helpers themselves', () => {
    expect(evalNcalc(windKmh(), ac('24h'))).toBeNull();
    expect(evalNcalc(simClock().text, ac('24h'))).toBe('--');
  });
});

describe('the wind and the sim clock on iRacing', () => {
  test('read what they always read, on a 24-hour rig', () => {
    expect(windAndClock(iracing('24h'))).toEqual({
      bandWind: ['Wind', '8', 'km/h'],
      headerWind: ['8 km/h'],
      headerSimClock: ['Sim', '14:22'],
      barSimulatedTime: ['14:22'],
      bandSimClock: ['Sim', '14:22'],
    });
  });

  test('and on a 12-hour one', () => {
    expect(windAndClock(iracing('12h'))).toEqual({
      bandWind: ['Wind', '8', 'km/h'],
      headerWind: ['8 km/h'],
      headerSimClock: ['Sim', '2:22', 'PM'],
      barSimulatedTime: ['2:22', 'PM'],
      bandSimClock: ['Sim', '2:22', 'PM'],
    });
  });
});

/** Every field that draws the track state, as a driver reads each. */
const trackState = (props: Props): Record<string, string[]> => ({
  bandGrip: shown(field(band('weather'), 'grip'), props),
  bandCorner: shown(field(corners(), 'corner.trackState'), props),
  header: shown(group('track'), props),
  trackModule: shown(trackModule(), props),
});

describe('the track state', () => {
  test('is read from a property iRacing publishes, not from a GameData member no reader has', () => {
    expect(trackGrip()).toContain('[DataCorePlugin.GameRawData.Telemetry.TrackWetness]');
    expect(trackGrip()).not.toContain('TrackGripStatus');
  });

  test('reads Dry on a dry iRacing track, wherever it is drawn', () => {
    expect(trackState(iracing('24h'))).toEqual({ bandGrip: ['Grip', 'Dry'], bandCorner: ['Track', 'Dry'], header: ['Track', 'Dry'], trackModule: ['Dry'] });
  });

  test("folds iRacing's seven wetnesses onto five words, the driest first", () => {
    const words = [1, 2, 3, 4, 5, 6, 7].map((w) => evalNcalc(trackGrip(), iracing('24h', w)));
    expect(words).toEqual(['Dry', 'Damp', 'Damp', 'Wet', 'Wet', 'Very wet', 'Flooded']);
  });

  test('hides where the sim reports none, and while iRacing does not know yet', () => {
    const none = { bandGrip: [], bandCorner: [], header: [], trackModule: [] };
    expect(trackState(ac('24h'))).toEqual(none);
    expect(trackState(iracing('24h', 0))).toEqual(none);
    expect(evalNcalc(hasTrackState(), ac('24h'))).toBe(false);
  });
});

/**
 * The frames above are written by hand, so they say what the track state reads once `TrackWetness` is
 * there and not that it is. A column can be recorded and still be null in every frame (#922), so the
 * committed iRacing traces are held to carrying a wetness iRacing has settled on, which the emulator
 * writes as 1, in every frame of every scenario: then the track state the replays draw is a word.
 */
if (THEME_ID === DEFAULT_THEME_ID) {
  describe('the track state on the committed iRacing traces', () => {
    for (const scenario of tracedScenarios()) {
      test(`${scenario} carries a known wetness in every frame, so the track state draws a word`, () => {
        const trace = readTrace(scenario);
        const column = trace.columns.find((c) => c.p === `${RAW}TrackWetness`);
        const frames = column === undefined ? [] : Array.isArray(column.v) ? column.v : Array.from({ length: trace.header.frames }, () => column.v);
        const unknown = frames.flatMap((v, i) => (typeof v === 'number' && v >= 1 ? [] : [`frame ${i}: ${String(v)}`]));
        expect({ scenario, carried: column !== undefined, unknown }).toEqual({ scenario, carried: true, unknown: [] });
        const words = new Set(frames.map((v) => evalNcalc(trackGrip(), iracing('24h', v as number))));
        expect({ scenario, unknown: words.has('--') }).toEqual({ scenario, unknown: false });
      });
    }
  });
}

const PORSCHE = 'porsche';

if (drawsInThisProcess(PORSCHE)) {
  /** The strip's track outline and its word, at every size the Porsche draws. */
  const strips = (): { at: string; box: Item; value: TextItem }[] =>
    THEMES[PORSCHE]!.anatomy.sizes.map((size) => {
      const at = `${size.width}x${size.height}`;
      const layout = ZONE_FACES.find((f) => f.width === size.width && f.height === size.height)!;
      const face = FACE_SIZES.find((f) => f.width === size.width && f.height === size.height)!;
      const items = porscheStrip({ layout, face, regions: THEMES[PORSCHE]!.anatomy.regions(layout), withRevBar: true });
      const box = items.find((i) => i.name === 'strip.track.box')!;
      const value = items.find((i): i is TextItem => i.kind === 'text' && i.name === 'strip.track.value')!;
      return { at, box, value };
    });

  /** The strip as a driver reads it: whether the outline is drawn, and the word in it. */
  const strip = (box: Item, value: TextItem, props: Props): { outline: boolean; word: string[] } => {
    const visible = bindingExpression(box as TextItem, 'Visible');
    return { outline: visible === '' || evalNcalc(visible, props) === true, word: shown([value], props) };
  };

  describe("the Porsche strip's track state", () => {
    test('draws DRY in its outline on a dry iRacing track, and neither outline nor word on Assetto Corsa', () => {
      const drawn = strips().map(({ at, box, value }) => ({ at, iracing: strip(box, value, iracing('24h')), ac: strip(box, value, ac('24h')) }));
      expect(drawn).toEqual(
        THEMES[PORSCHE]!.anatomy.sizes.map((size) => ({
          at: `${size.width}x${size.height}`,
          iracing: { outline: true, word: ['DRY'] },
          ac: { outline: false, word: [] },
        })),
      );
    });
  });
}

if (THEME_ID === DEFAULT_THEME_ID && !drawsInThisProcess(PORSCHE)) {
  test(
    "the Porsche strip's track state, in a process drawing its colours",
    () => {
      const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: PORSCHE }, stdout: 'pipe', stderr: 'pipe' });
      const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
      expect({ theme: PORSCHE, exit: run.exitCode, output }).toEqual({ theme: PORSCHE, exit: 0, output: '' });
    },
    120_000,
  );
}
