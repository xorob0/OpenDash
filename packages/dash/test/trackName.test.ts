/**
 * The track is named as a driver names it, on Assetto Corsa too (#1124).
 *
 * SimHub's `GameData.TrackName` is the circuit's name on iRacing and its folder on Assetto Corsa:
 * `ACManager.GD_TrackName` answers `TrackDesc.Track`, which is the folder under `content\tracks` in
 * lower case, `ks_red_bull_ring`. The name a driver knows is the one the track's `ui_track.json` gives,
 * which the same reader publishes raw as `Track.TrackInfo.name`. The frames below are what SimHub
 * hands a dashboard on each: AC fills both, and iRacing fills `TrackName` and has no `Track` at all.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages } from '../src/build.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { TRACK_NAME_FOR_PEOPLE } from '../src/second/values.ts';
import { expressionsIn, walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const TRACK_NAME = 'DataCorePlugin.GameData.TrackName';
const TRACK_LENGTH = 'DataCorePlugin.GameData.TrackLength';

/** Assetto Corsa at the Red Bull Ring, as SimHub 9.12.6's AC reader publishes it. */
const AC: Props = { [TRACK_NAME]: 'ks_red_bull_ring', [TRACK_NAME_FOR_PEOPLE]: 'Red Bull Ring', [TRACK_LENGTH]: 4318 };

/** iRacing at Spa, where the raw `Track` block does not exist and so reads as null. */
const IRACING: Props = { [TRACK_NAME]: 'Circuit de Spa-Francorchamps', [TRACK_NAME_FOR_PEOPLE]: null, [TRACK_LENGTH]: 7004 };

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
    expect(drawn({ ...AC, [TRACK_NAME_FOR_PEOPLE]: null })).toBe('ks_red_bull_ring · 4.3 km');
  });

  test('on iRacing it reads TrackName, exactly as it did', () => {
    expect(drawn(IRACING)).toBe('Circuit de Spa-Francorchamps · 7.0 km');
  });
});

describe('no package reads TrackName without the name for people in front of it', () => {
  // The header is the one place today, and this is what keeps a second place from reading the
  // folder on AC: any expression of any package that reads `TrackName` reads the raw name too.
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
          expect({ expression, readsName: expression.includes(`[${TRACK_NAME_FOR_PEOPLE}]`) }).toEqual({ expression, readsName: true });
        }
      }
    });
  }
});
