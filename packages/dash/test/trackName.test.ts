/**
 * The track is named as a driver names it, on iRacing and on Assetto Corsa (#1124).
 *
 * SimHub's `GameData.TrackName` is a code on both. On iRacing `IRacingManager.GD_TrackName` answers the
 * session's `WeekendInfo.TrackName`, `spa gp`, which is what every recorded trace holds; the name is
 * `WeekendInfo.TrackDisplayShortName` beside it, `Spa`. On Assetto Corsa `ACManager.GD_TrackName`
 * answers `TrackDesc.Track`, the folder under `content\tracks` in lower case, `ks_red_bull_ring`; the
 * name is the one the track's `ui_track.json` gives, which the same reader publishes raw as
 * `Track.TrackInfo.name`. The frames below are what SimHub hands a dashboard on each: each sim fills
 * its own raw name and `TrackName`, and the other sim's raw block does not exist and reads as null.
 */
import { describe, expect, test } from 'bun:test';
import { SIMS } from '../../../scripts/sims.ts';
import { frame, readTrace, traceDirOf, tracedScenarios } from '../../../scripts/trace.ts';
import { composePackages } from '../src/build.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { measureText } from '../src/design/advances.ts';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { AC_TRACK_NAME, IRACING_TRACK_NAME } from '../src/second/values.ts';
import { expressionsIn, itemsOf, walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const TRACK_NAME = 'DataCorePlugin.GameData.TrackName';
const TRACK_LENGTH = 'DataCorePlugin.GameData.TrackLength';

/** Assetto Corsa at the Red Bull Ring, as SimHub 9.12.6's AC reader publishes it. */
const AC: Props = { [TRACK_NAME]: 'ks_red_bull_ring', [AC_TRACK_NAME]: 'Red Bull Ring', [IRACING_TRACK_NAME]: null, [TRACK_LENGTH]: 4318 };

/** iRacing at Spa, as the emulator's session info gives it and the recorded traces hold it. */
const IRACING: Props = { [TRACK_NAME]: 'spa gp', [AC_TRACK_NAME]: null, [IRACING_TRACK_NAME]: 'Spa', [TRACK_LENGTH]: 7004 };

const header = (): TextItem => {
  const module = MODULES.find((m) => m.id === 'track');
  if (!module) throw new Error('no track module');
  const { width, height } = SHAPE_ARCHETYPES.wide;
  const items: Item[] = [...walkItems(module.build({ frame: rect(0, 0, width, height), density: 'zone', prefix: 'track.' }))];
  const found = items.find((i) => i.name === 'track.title');
  if (found?.kind !== 'text') throw new Error('track.title is not a text item');
  return found;
};

const drawn = (props: Props): unknown => evalNcalc(String(header().bindings?.Text?.formula), props);

/** A text item's `Text` formula, or the empty string where it has none. */
const formulaOf = (item: Item): string => String(item.bindings?.Text?.formula ?? '');

describe("the track module's header", () => {
  test("on Assetto Corsa it names the track from its ui_track.json, not its folder", () => {
    expect(drawn(AC)).toBe('Red Bull Ring · 4.3 km');
  });

  test('on Assetto Corsa a track with no ui_track.json falls back to the folder, rather than to nothing', () => {
    expect(drawn({ ...AC, [AC_TRACK_NAME]: null })).toBe('ks_red_bull_ring · 4.3 km');
  });

  test("on iRacing it names the track from the session's short display name, not iRacing's code for it", () => {
    expect(drawn(IRACING)).toBe('Spa · 7.0 km');
  });

  test("on iRacing a session that gives no display name falls back to TrackName, rather than to nothing", () => {
    expect(drawn({ ...IRACING, [IRACING_TRACK_NAME]: null })).toBe('spa gp · 7.0 km');
  });

  test('on a sim that fills neither raw name it reads TrackName, as it always did', () => {
    expect(drawn({ [TRACK_NAME]: 'Monza', [TRACK_LENGTH]: 5793 })).toBe('Monza · 5.8 km');
  });
});

describe("the track module's header, replayed from every committed trace", () => {
  // The frames above are written by hand; these are the files the preview, the video and the goldens
  // replay. Each sim's traces must carry both raw names, or a replay draws the code a driver never
  // sees: an AC trace without `Track.TrackInfo.name` reads `spa`, an iRacing one without
  // `TrackDisplayShortName` reads `spa gp`. Every scenario of both emulators is at Spa.
  for (const sim of SIMS) {
    const dir = traceDirOf(sim);
    for (const scenario of tracedScenarios(dir)) {
      test(`${sim.id} ${scenario}`, () => {
        const trace = readTrace(scenario, dir);
        for (const name of [AC_TRACK_NAME, IRACING_TRACK_NAME]) {
          expect({ scenario, name, carried: trace.columns.some((c) => c.p === name) }).toEqual({ scenario, name, carried: true });
        }
        expect(drawn(frame(trace, 0))).toBe('Spa · 7.0 km');
      });
    }
  }
});

describe("no package reads TrackName without each sim's name for people in front of it", () => {
  // The header is the one place today, and this is what keeps a second place from reading the
  // folder on AC or the code on iRacing: any expression of any package that reads `TrackName` reads
  // both raw names too.
  const packages = composePackages({ version: '0.0.0-test', log: () => {} }, true);

  const reading = (expressions: readonly string[]): string[] => expressions.filter((e) => e.includes(`[${TRACK_NAME}]`));

  test('the build composes the packages it is asked about, and some of them draw the track', () => {
    expect(packages.length).toBeGreaterThan(10);
    expect(packages.some(({ pkg }) => pkg.dashboards.some((d) => reading(expressionsIn(d)).length > 0))).toBe(true);
  });

  for (const { pkg } of packages) {
    test(pkg.folderName, () => {
      for (const dashboard of pkg.dashboards) {
        for (const expression of reading(expressionsIn(dashboard))) {
          const readsNames = [AC_TRACK_NAME, IRACING_TRACK_NAME].every((name) => expression.includes(`[${name}]`));
          expect({ expression, readsNames }).toEqual({ expression, readsNames: true });
        }
      }
    });
  }
});

describe("the track module's header fits its box whatever the track is called (#1150)", () => {
  // The name is an open string, so the line is measured by what it draws on frames whose names are
  // long rather than by its sample. Every box the packages place the module in, with each sim's raw
  // name filled in turn, since the two reach the line through different reads.
  const packages = composePackages({ version: '0.0.0-test', log: () => {} }, true);
  const headers = new Map<string, TextItem>();
  for (const { pkg } of packages) {
    for (const dashboard of pkg.dashboards) {
      for (const item of itemsOf(dashboard)) {
        if (item.kind !== 'text' || !formulaOf(item).includes(`[${AC_TRACK_NAME}]`)) continue;
        headers.set(`${item.rect.width}x${item.fontSize}`, item);
      }
    }
  }

  const SIXTY = 'Autodromo Internazionale del Mugello Grand Prix Circuit 2024';
  const NAMES = ['Spa', 'Red Bull Ring', 'Nürburgring Combined', 'Nordschleife - Tourist', 'Watkins Glen International', SIXTY, 'W'.repeat(60)];
  const LENGTHS = [7004, 25378, 250000];
  const frames: Props[] = NAMES.flatMap((name) =>
    LENGTHS.flatMap((length) => [
      { ...AC, [AC_TRACK_NAME]: name, [TRACK_LENGTH]: length },
      { ...IRACING, [IRACING_TRACK_NAME]: name, [TRACK_LENGTH]: length },
    ]),
  );

  test('the packages place the header, the smallest boxes among them', () => {
    expect(SIXTY.length).toBe(60);
    expect(headers.size).toBeGreaterThan(5);
    expect(Math.min(...[...headers.values()].map((h) => h.rect.width))).toBeLessThan(200);
  });

  test('every string the header draws is inside its box and inside its widest', () => {
    for (const item of headers.values()) {
      const limit = measureText('BarlowMedium', item.widest ?? '', item.fontSize);
      for (const props of frames) {
        const text = String(evalNcalc(formulaOf(item), props));
        const width = measureText('BarlowMedium', text, item.fontSize);
        expect({ box: item.rect.width, text, fits: width < item.rect.width, withinWidest: width <= limit }).toEqual({ box: item.rect.width, text, fits: true, withinWidest: true });
      }
    }
  });

  test('a short name keeps its length in every box, and a long one is cut rather than drawn past it', () => {
    for (const item of headers.values()) {
      expect(evalNcalc(formulaOf(item), IRACING)).toBe('Spa · 7.0 km');
      // The companion page's header holds all sixty; every narrower one cuts and says it has.
      const cut = String(evalNcalc(formulaOf(item), { ...AC, [AC_TRACK_NAME]: SIXTY }));
      const whole = cut === SIXTY || cut.startsWith(`${SIXTY} · `);
      expect({ box: item.rect.width, cut, readable: whole || (cut.endsWith('…') && SIXTY.startsWith(cut.slice(0, -1))) }).toMatchObject({ readable: true });
      if (item.rect.width < 300) expect(whole).toBe(false);
    }
  });

  test('the length gives way before the name does', () => {
    // On the narrowest header Red Bull Ring fits whole only without its length, and is drawn so
    // rather than cut to make room for it; on the widest it is drawn with it, as the canvas writes it.
    const byWidth = [...headers.values()].sort((a, b) => a.rect.width - b.rect.width);
    const narrowest = formulaOf(byWidth[0]!);
    const widest = formulaOf(byWidth[byWidth.length - 1]!);
    expect(evalNcalc(narrowest, AC)).toBe('Red Bull Ring');
    expect(evalNcalc(widest, AC)).toBe('Red Bull Ring · 4.3 km');
    // A reading past any circuit is held to three figures rather than let a fourth past the box.
    expect(evalNcalc(widest, { ...AC, [TRACK_LENGTH]: 250000 })).toBe('Red Bull Ring · 99.9 km');
  });
});
