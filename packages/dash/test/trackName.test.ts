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
import { composePackages } from '../src/build.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { AC_TRACK_NAME, IRACING_TRACK_NAME } from '../src/second/values.ts';
import { expressionsIn, walkItems } from '../src/walk.ts';
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
