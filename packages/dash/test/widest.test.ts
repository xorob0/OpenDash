/**
 * Every `widest` is at least as wide as anything its own binding can draw.
 *
 * The fit tests measure a bound item's box against its `widest`, and until now nothing measured the
 * `widest` against the binding. It is a declaration written beside the expression by the same hand
 * on the same day, and it drifts the way such declarations do: #384's branch declared `TYRES °F`
 * for a label that also draws `TYRES °C`, and `kPa` for a unit that also draws `bar`, and both
 * passed every fit test because the box held what was declared. Three of those in two days is a
 * check that is missing, not three mistakes.
 *
 * The check reads the binding as `drawnStrings.ts` reads it: the strings some output is certain
 * to be at least as wide as. Where the whole output is known, that is the output; where a sim value
 * is spliced in, it is the written part around the splice. Either way a floor wider than `widest`
 * means a box that WPF will clip, so that is the assertion, and it is the whole of it: a `widest`
 * generous beyond what is drawn wastes a pixel and clips nothing, and a binding whose output is
 * wholly the sim's has no floor to hold it to. The loop is every text item of every package the
 * build composes, faces and second screens alike, and then every module in every box the second
 * screens are proved against, since a field's `widest` follows the box it is fitted to.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages } from '../src/build.ts';
import { MODULES } from '../src/modules/index.ts';
import { expressionsIn, itemsOf, walkItems } from '../src/walk.ts';
import type { TextItem } from '../src/generator.ts';
import { measureText, type MeasuredFace } from '../src/design/advances.ts';
import { SESSION_NAMES, SESSION_NAME_WIDEST, sessionType } from '../src/second/values.ts';
import { certainlyDrawn, drawnFloors, parseNcalc, readsVocabulary, widthAsDrawn } from './drawnStrings.ts';
import { moduleBoxes } from './secondScreens.test.ts';

const PACKAGES = composePackages({ version: '0.0.0-test', log: () => {} }, true);

const texts = (items: Iterable<{ kind: string }>): TextItem[] => [...items].filter((i): i is TextItem => i.kind === 'text');

/** The floors of an item that its declared `widest` does not hold, which should be none. */
function wider(item: TextItem): { text: string; width: number }[] {
  const limit = widthAsDrawn(item, item.widest!);
  return certainlyDrawn(item)
    .map((text) => ({ text, width: widthAsDrawn(item, text) }))
    .filter((f) => f.width > limit);
}

describe('the reading of a binding', () => {
  test('a composite is read whole, which is what a literal-by-literal reading missed', () => {
    // #384's tyre temps card: no literal here is wider than `TYRES °F`, and the string drawn is.
    const label = "('TYRES ') + (if([TemperatureUnit] = 'Fahrenheit', '°F', if([TemperatureUnit] = 'Kelvin', 'K', '°C'))) + (' · LAST STOP')";
    expect(drawnFloors(label).sort()).toEqual(['TYRES K · LAST STOP', 'TYRES °C · LAST STOP', 'TYRES °F · LAST STOP']);
  });

  test('a case function is applied, since KPA and BAR are not kPa and bar', () => {
    // A unit wrapped in `ucase`, which is how `unit()` drew #384's tyre pressure unit before #422.
    expect(drawnFloors("ucase(if([Unit] = 'Psi', 'psi', if([Unit] = 'Bar', 'bar', 'kPa')))").sort()).toEqual(['BAR', 'KPA', 'PSI']);
    expect(drawnFloors("lcase('KM/H')")).toEqual(['km/h']);
    expect(drawnFloors("tcase('liam byrne')")).toEqual(['Liam Byrne']);
  });

  test('what the sim sends is unknowable, and what is written around it is a floor', () => {
    // The hash is certain and the number is not, so `#` is what a `widest` has to hold at least.
    expect(drawnFloors("('#') + (drivercarnumber(1))")).toEqual(['#']);
    expect(drawnFloors("isnull([DataCorePlugin.GameData.TrackGripStatus], '--')")).toEqual(['--']);
    // Wholly the sim's: nothing to hold a `widest` to, and nothing invented to hold it to either.
    expect(drawnFloors("format([DataCorePlugin.GameData.SpeedKmh], '0')")).toEqual([]);
    expect(drawnFloors('[DataCorePlugin.GameData.Gear]')).toEqual([]);
  });

  test('a property whose words are known draws one of them, and the session type is one', () => {
    // A free property has no floor, which is how the session page came to measure the session's
    // name by `Race` and draw `Lone Qualify` as `Lone Qua` with every test passing (#1029).
    expect(drawnFloors(sessionType()).sort()).toEqual([...SESSION_NAMES].sort());
    expect(drawnFloors(`ucase(${sessionType()})`)).toContain('OFFLINE TESTING');
    expect(drawnFloors("('Session: ') + ([DataCorePlugin.GameData.SessionTypeName])")).toContain('Session: Offline Testing');
  });

  test('a number is not a string, so a sum contributes no digit', () => {
    // `([Laps]) + (1)` is 13 on the screen and never a `1`, and a `1` measured as a floor would be
    // wrong only when it was wider than what was drawn, which is exactly when it would matter.
    expect(drawnFloors('([DataCorePlugin.GameData.CompletedLaps]) + (1)')).toEqual([]);
    expect(drawnFloors("('P') + ([Position] + 1)")).toEqual(['P']);
  });

  test('a cut acts on a whole string and gives up on a partial one', () => {
    expect(drawnFloors("left('MODERATE', 0, 4)")).toEqual(['MODE']);
    expect(drawnFloors("ucase(left(isnull([CarClass], ''), 0, 4))")).toEqual([]);
    // `ellipsised`: the name is the sim's and the ellipsis is certain.
    expect(drawnFloors("if((left([Name], 1, 8)) = (''), [Name], (left([Name], 0, 7)) + ('…'))")).toEqual(['…']);
    expect(drawnFloors("replace('a-b', '-', '−')")).toEqual(['a−b']);
    expect(drawnFloors("replace(format([X], '0.0', true), '-', '−')")).toEqual([]);
  });

  test('a binding it cannot read is an error, not a pass', () => {
    expect(() => drawnFloors("if([X] = 'a', 'b'")).toThrow();
    expect(() => drawnFloors("'unterminated")).toThrow();
    expect(() => drawnFloors('[X] ~ 1')).toThrow();
  });

  test('and it reads every expression the build emits, not only the ones a widest sits beside', () => {
    // The check below only reads a `Text` binding that declares a `widest`, so a shape of
    // expression the reader does not know would surface the day somebody declared one beside it,
    // in their change rather than in the reader's. Proved against the whole corpus instead: every
    // binding of every item, `Visible` and `Left` included.
    let read = 0;
    for (const composed of PACKAGES) {
      for (const dashboard of composed.pkg.dashboards) {
        for (const expression of expressionsIn(dashboard)) {
          expect(() => parseNcalc(expression)).not.toThrow();
          read += 1;
        }
      }
    }
    expect(read).toBeGreaterThan(1000);
  });
});

describe('every widest holds what its binding can draw', () => {
  /** The items that declare a `widest`, package by package. */
  const declared = PACKAGES.map((composed) => ({
    folder: composed.pkg.folderName,
    items: composed.pkg.dashboards.flatMap((dashboard) => texts(itemsOf(dashboard)).filter((item) => item.widest !== undefined).map((item) => ({ dashboard: dashboard.name, item }))),
  }));

  test('there is something to check', () => {
    // Over the build rather than per package: a round face is a hero and nothing bound with a
    // `widest`, and that is the face and not a hole in the loop.
    expect(declared.flatMap((p) => p.items).length).toBeGreaterThan(100);
  });

  for (const { folder, items } of declared) {
    test(folder, () => {
      for (const { dashboard, item } of items) {
        expect({ dashboard, item: item.name, widest: item.widest, wider: wider(item) }).toMatchObject({ wider: [] });
      }
    });
  }

  // The packages place each module in the boxes the pages happened to have room for; the boxes
  // the second screens are proved against are every shape a module can be handed.
  for (const box of moduleBoxes()) {
    test(`every module on a ${box.name}`, () => {
      for (const module of MODULES) {
        for (const item of texts(walkItems(module.build({ frame: box.frame, density: box.density, prefix: '' })))) {
          if (item.widest === undefined) continue;
          expect({ module: module.id, item: item.name, widest: item.widest, wider: wider(item) }).toMatchObject({ wider: [] });
        }
      }
    });
  }
});

describe('a property whose words are known is measured by the widest of them', () => {
  test('the session name declared widest is the widest session name in every face, as written and in capitals', () => {
    const faces: MeasuredFace[] = ['BarlowMedium', 'BarlowBold', 'BarlowCondensedSemiBold', 'BarlowCondensedBold', 'BarlowCondensedLight', 'DSEG7Regular', 'DSEG7Bold', 'DSEG14Regular'];
    expect(SESSION_NAMES).toContain(SESSION_NAME_WIDEST);
    for (const face of faces) {
      for (const cased of [(t: string) => t, (t: string) => t.toUpperCase()]) {
        const widest = measureText(face, cased(SESSION_NAME_WIDEST), 100);
        for (const name of SESSION_NAMES) {
          expect({ face, name: cased(name), fits: measureText(face, cased(name), 100) <= widest }).toMatchObject({ fits: true });
        }
      }
    }
  });

  // A bound item with no `widest` is measured by its sample, and the loop above passes it over; one
  // that reads a property whose words are known has a widest to declare, and declaring it is what
  // puts it in that loop. The session page's Session field declared none and was cut for `Race`.
  const reading = PACKAGES.flatMap((composed) =>
    composed.pkg.dashboards.flatMap((dashboard) => texts(itemsOf(dashboard)).filter(readsVocabulary).map((item) => ({ folder: composed.pkg.folderName, dashboard: dashboard.name, item }))),
  );

  test('there is something to check', () => {
    expect(reading.length).toBeGreaterThan(0);
  });

  test('every package', () => {
    for (const { folder, dashboard, item } of reading) {
      expect({ folder, dashboard, item: item.name, declares: item.widest !== undefined }).toMatchObject({ declares: true });
    }
  });

  for (const box of moduleBoxes()) {
    test(`every module on a ${box.name}`, () => {
      for (const module of MODULES) {
        for (const item of texts(walkItems(module.build({ frame: box.frame, density: box.density, prefix: '' }))).filter(readsVocabulary)) {
          expect({ module: module.id, item: item.name, declares: item.widest !== undefined }).toMatchObject({ declares: true });
        }
      }
    });
  }
});
