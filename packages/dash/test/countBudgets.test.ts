/**
 * The lap counts, the incident count and the car settings, held to the longest reading their bindings
 * produce (#596).
 *
 * A monospaced box is cut from its `chars` and WPF clips whatever overruns it, while the fit tests
 * measure a bound value by its `widest` and, without one, by its sample. These fields carried their
 * samples alone, `12`, `3x` and `5`, and budgets cut to them: the position's two cells for a lap, the
 * count's three for `3x`, and for a car setting as many cells as its sample has characters. So every
 * fit test passed while lap 100 lost its last digit, `100x` its `x`, and a TC of 10 a whole digit.
 *
 * The loop is over the bindings rather than over a list of fields, so a lap count added next year is
 * held to the same reading the day it is drawn: every text item of every package of every theme, and
 * every module in every box the second screens are proved against, whose `Text` is one of these
 * readings. For each, the binding is evaluated at the long end, the item has to declare that reading
 * as its `widest`, and its box has to hold it in the face and the cells it is drawn in.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages, themesToBuild } from '../src/build.ts';
import type { TextItem } from '../src/generator.ts';
import { ncalc } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { BIAS_WIDEST, LEVEL_WIDEST, TRACKED_VALUES } from '../src/second/tracked.ts';
import { CHARS, INCIDENTS_WIDEST, LAP_TOTAL_WIDEST, LAP_WIDEST, lapsLeftText } from '../src/second/values.ts';
import { charsOfText } from '../src/second/drawn.ts';
import { itemsOf, walkItems } from '../src/walk.ts';
import { widthAsDrawn } from './drawnStrings.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';
import { moduleBoxes } from './secondScreens.test.ts';

const { fmt } = ncalc;

const themes = themesToBuild({ themes: [], allThemes: true, touchedThemes: false }, () => {});
const PACKAGES = composePackages({ version: '0.0.0-test', themes, log: () => {} });

interface Site {
  where: string;
  item: TextItem;
  formula: string;
}

const texts = (items: Iterable<{ kind: string }>): TextItem[] => [...items].filter((i): i is TextItem => i.kind === 'text');

/**
 * The reading an item's `Text` binds. The AiM theme wraps every reading it draws in a `replace` that
 * turns the house's minus into a hyphen, which is the segment face's own (`segmentText`); a count is
 * never negative, so the wrapper is taken off and the reading inside it is held to the same budget as
 * every other theme's.
 */
const formulaOf = (item: TextItem): string | undefined => {
  const f = item.bindings?.Text?.formula;
  return typeof f === 'string' ? f.replace(/^replace\((.*), '−', '-'\)$/, '$1') : undefined;
};

/** Every bound text the build draws, and every one a module draws in a box the build may not use. */
const SITES: Site[] = [
  ...PACKAGES.flatMap(({ pkg }) =>
    pkg.dashboards.flatMap((dashboard) =>
      texts(itemsOf(dashboard)).flatMap((item) => {
        const formula = formulaOf(item);
        return formula === undefined ? [] : [{ where: `${pkg.folderName} / ${dashboard.name}`, item, formula }];
      }),
    ),
  ),
  ...moduleBoxes().flatMap((box) =>
    MODULES.flatMap((module) =>
      texts(walkItems(module.build({ frame: box.frame, density: box.density, prefix: '' }))).flatMap((item) => {
        const formula = formulaOf(item);
        return formula === undefined ? [] : [{ where: `${module.id} on a ${box.name}`, item, formula }];
      }),
    ),
  ),
];

/** A lap count read straight from the sim, or from the leaderboard for the stint. */
const LAP_READ = String.raw`(?:isnull\(\[DataCorePlugin\.GameData\.(?:CurrentLap|CompletedLaps|RemainingLaps)\], 0\)|isnull\(driverlapsdonesincelastpitout\((?:[^()]|\([^()]*\))*\), 0\))`;

/** The readings this ticket is about, each with the string it draws at the long end. */
const READINGS: { name: string; pattern: RegExp; widest: string }[] = [
  { name: 'a lap count', pattern: new RegExp(String.raw`^format\(${LAP_READ}, '0'\)$`), widest: LAP_WIDEST },
  { name: 'the lap after an L', pattern: new RegExp(String.raw`^\('L'\) \+ \(format\(${LAP_READ}, '0'\)\)$`), widest: `L${LAP_WIDEST}` },
  {
    name: "the race's length after a slash",
    pattern: /^\('\/ '\) \+ \(format\((?:isnull\(\[DataCorePlugin\.GameData\.TotalLaps\], 0\)|\[DataCorePlugin\.GameData\.TotalLaps\]), '0'\)\)$/,
    widest: LAP_TOTAL_WIDEST,
  },
  // The session page's laps left, which in a timed race is predicted from the time left and is
  // `--` until a lap has been timed (#1008); at the long end of a lap race it is `RemainingLaps`.
  { name: 'the laps left', pattern: new RegExp(`^${lapsLeftText().replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}$`), widest: LAP_WIDEST },
  {
    name: 'the incident count',
    pattern: /^\(format\(isnull\(\[DataCorePlugin\.GameRawData\.Telemetry\.PlayerCarMyIncidentCount\], 0\), '0'\)\) \+ \('x'\)$/,
    widest: INCIDENTS_WIDEST,
  },
];

/** The long end of a race: lap 999 of 999, as many laps done and to come, and 999 incidents. */
const LONG_RACE: Props = {
  'DataCorePlugin.GameData.CurrentLap': 999,
  'DataCorePlugin.GameData.CompletedLaps': 999,
  'DataCorePlugin.GameData.RemainingLaps': 999,
  'DataCorePlugin.GameData.TotalLaps': 999,
  'DataCorePlugin.GameRawData.Telemetry.PlayerCarMyIncidentCount': 999,
  StintLaps: 999,
};

/** The formula with the leaderboard's stint count read as a property, which the evaluator can answer. */
const evaluable = (formula: string): string => formula.replace(/driverlapsdonesincelastpitout\((?:[^()]|\([^()]*\))*\)/g, '[StintLaps]');

/** The box holds the text, as the face and second-screen fit tests measure it: a cell is already rounded up from its glyph. */
const fits = (item: TextItem, text: string): boolean => widthAsDrawn(item, text) <= item.rect.width;

describe('the lap and incident budgets', () => {
  test('a lap count is three digits and the incident count three and its x', () => {
    expect(CHARS.lap).toEqual({ digits: 3, specials: 0 });
    expect(CHARS.incidents).toEqual({ digits: 4, specials: 0 });
    // Every glyph of each reading in a full cell, the slash and the space included.
    const mono = { charWidth: 10, specialCharsWidth: 5, specialChars: '.,:' };
    expect(charsOfText(LAP_WIDEST, mono)).toEqual(CHARS.lap);
    expect(charsOfText(INCIDENTS_WIDEST, mono)).toEqual(CHARS.incidents);
  });

  for (const reading of READINGS) {
    const sites = SITES.filter((s) => reading.pattern.test(s.formula));

    test(`${reading.name} reads ${reading.widest} at the long end, and is found where it is drawn`, () => {
      expect(sites.length).toBeGreaterThan(10);
      for (const s of sites) expect({ where: s.where, item: s.item.name, drawn: String(evalNcalc(evaluable(s.formula), LONG_RACE)) }).toEqual({ where: s.where, item: s.item.name, drawn: reading.widest });
    });

    test(`every box that draws ${reading.name} declares and holds ${reading.widest}`, () => {
      for (const s of sites) {
        const widest = s.item.widest;
        // Declared, so the fit tests measure the long end, and at least as wide as it in the item's own
        // face: a segment display declares its ghost, `888`, which is the same reading in its cells.
        expect({ where: s.where, item: s.item.name, declared: widest !== undefined && widthAsDrawn(s.item, reading.widest) <= widthAsDrawn(s.item, widest) }).toEqual({
          where: s.where,
          item: s.item.name,
          declared: true,
        });
        expect({ where: s.where, item: s.item.name, box: s.item.rect.width, fits: fits(s.item, reading.widest) }).toMatchObject({ fits: true });
      }
    });
  }

  test('the fields the ticket names are among the sites', () => {
    const names = new Set(SITES.filter((s) => READINGS.some((r) => r.pattern.test(s.formula))).map((s) => s.item.name.split('.').slice(-2).join('.')));
    for (const name of ['laps.value', 'stintLap.value', 'lap.value', 'lapsLeft.value', 'stintLaps.value', 'completed.value', 'incidents.value', 'lap.denominator']) {
      expect({ name, found: names.has(name) }).toEqual({ name, found: true });
    }
    // The bar's two ends, band D's corner and its stint page, and the companion's header.
    for (const at of [/\bLeft1\.lap\.value$/, /\bRight2\.lap\.denominator$/, /\bLeft1\.incidents\.value$/, /\.corner\.incidents\.value$/, /\bheader\.lap\.value$/, /\bstint\.laps\.value$/]) {
      const found = SITES.some((s) => at.test(s.item.name) && READINGS.some((r) => r.pattern.test(s.formula)));
      expect({ at: String(at), found }).toEqual({ at: String(at), found: true });
    }
  });
});

describe('the car settings', () => {
  /** A setting at the long end: a level of twelve, which a GT3 car's TC and ABS dials reach, and a bias of 56.5. */
  const longEnd = (pattern: string): number => (pattern === '0' ? 12 : 56.5);

  test('a level is two digits and the bias two and a place, whatever the sample', () => {
    expect(LEVEL_WIDEST).toBe('88');
    expect(BIAS_WIDEST).toBe('88.8');
    for (const value of TRACKED_VALUES) expect({ id: value.id, widest: value.widest }).toEqual({ id: value.id, widest: value.pattern === '0' ? LEVEL_WIDEST : BIAS_WIDEST });
  });

  for (const value of TRACKED_VALUES) {
    const reading = fmt(value.read, value.pattern);
    const sites = SITES.filter((s) => s.formula.includes(reading));
    const props = Object.fromEntries([...reading.matchAll(/\[([A-Za-z0-9_.]+)\]/g)].map(([, name]) => [name, longEnd(value.pattern)]));

    test(`${value.id}: every box that draws it holds what a two-digit setting draws, and the strip and the notification declare it`, () => {
      const drawn = String(evalNcalc(reading, props));
      expect(drawn).toBe(value.pattern === '0' ? '12' : '56.5');
      // The strip, the change notification, the settings page and the house themes' boxes, on every
      // face of every theme, and the legacy cards, which budget the same reading in cells of their own.
      expect(sites.length).toBeGreaterThan(10);
      for (const s of sites) {
        const widest = s.item.widest;
        if (widest !== undefined) expect({ where: s.where, item: s.item.name, inside: widthAsDrawn(s.item, drawn) <= widthAsDrawn(s.item, widest) }).toMatchObject({ inside: true });
        expect({ where: s.where, item: s.item.name, box: s.item.rect.width, fits: fits(s.item, widest ?? drawn) && fits(s.item, drawn) }).toMatchObject({ fits: true });
      }
      // The two that read the tracked value itself declare its widest, which is what their cells are
      // cut from; they were cut from the one-digit sample.
      const own = sites.filter((s) => new RegExp(String.raw`\b(strip|notice)\.${value.id}\.value$`).test(s.item.name));
      expect(own.length).toBeGreaterThan(10);
      for (const s of own) expect({ where: s.where, item: s.item.name, widest: s.item.widest }).toEqual({ where: s.where, item: s.item.name, widest: value.widest });
    });
  }
});
