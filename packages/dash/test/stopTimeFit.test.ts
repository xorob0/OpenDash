/**
 * A stop's time stays inside the four cells and a point it is drawn in, however long the stop (#1170).
 *
 * The pit view's `Pit time`, the stint page's `Last stop` and band D's `Last stop` were each
 * `format(seconds, '0.0')` in {@link CHARS.consumption}'s cells, which hold `999.9` and not a tenth
 * more. Nothing bounds the seconds: SimHub starts `IsInPitSince` on the first frame its `IsInPit` is
 * true, which on iRacing and Assetto Corsa is the first frame of a session the car starts in its box,
 * so seventeen minutes there with the dash up drew `1000.0` and WPF clipped its last glyph, and
 * `LastPitStopDuration` kept it on screen after the car left. The fit tests could not see it, since
 * they measure a bound value by its sample or its `widest` and the binding is wholly the sim's. So
 * what is pinned here is the reading itself: what each field draws past a thousand seconds, that
 * every box each is built into holds it, and that the unit after it stands where the figure ends.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { ncalcEvaluator as E, type Monospace, type TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { charsOfText, textWidth } from '../src/second/drawn.ts';
import { CHARS, secondsToFit, secondsToFitDrawn, secondsToFitWidest } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { BAND_PAGES, bandPageItems } from '../src/zones/bandPages.ts';
import { widthAsDrawn } from './drawnStrings.ts';
import { moduleBoxes } from './secondScreens.test.ts';

type Frame = Record<string, unknown>;

/** Every way the stop's time is read: the decimal form, its edge, the whole form, and past the cap. */
const READS: readonly (readonly [number, string])[] = [
  [0, '0.0'],
  [24.3, '24.3'],
  [999.9, '999.9'],
  // The edge: 999.95 to a tenth is `1000.0`, so from there it is drawn whole.
  [999.94, '999.9'],
  [999.95, '1000'],
  [1000, '1000'],
  [1234.56, '1235'],
  [9999.4, '9999'],
  // Past what four cells hold the reading stays at the most they do.
  [9999.6, '9999'],
  [86400, '9999'],
];

const js = (expression: string, scope: E.Scope): unknown => {
  const result = E.evaluateBinding(expression, scope);
  if (!result.ok) throw new Error(`${expression} failed: ${result.error.message}`);
  return E.toJs(result.value);
};

const formulaOf = (item: TextItem, target: 'Text' | 'Left'): string => {
  const f = item.bindings?.[target]?.formula;
  if (typeof f !== 'string') throw new Error(`no ${target} formula on ${item.name}`);
  return f;
};

/** A frame with SimHub's own pit timer, as iRacing and Assetto Corsa publish it and the plugin leaves alone. */
const simHub = (inLane: boolean, since: number, last: number): E.Scope => ({
  properties: {
    'DataCorePlugin.GameData.IsInPitLane': inLane ? 1 : 0,
    'DataCorePlugin.GameData.IsInPitSince': E.double(since),
    'DataCorePlugin.GameData.LastPitStopDuration': E.double(last),
  } satisfies Frame,
});

/** A frame in which the player is first on the board and the tracker's last stop is `seconds` long. */
const tracker = (seconds: number): E.Scope => ({
  properties: {},
  calls: (text) => (/^driverpitlastduration\(/.test(text) ? E.fromSeconds(seconds) : /^getplayerleaderboardposition/.test(text) ? 1 : undefined),
});

/** The value and its unit in each box a module is built into. */
const moduleSites = (moduleId: string, field: string): { name: string; value: TextItem; unit: TextItem }[] => {
  const module = MODULES.find((m) => m.id === moduleId)!;
  return moduleBoxes().flatMap((box) => {
    const items = [...walkItems(module.build({ frame: box.frame, density: box.density, prefix: '' }))].filter((i): i is TextItem => i.kind === 'text');
    const value = items.find((i) => i.name === `${field}.value`);
    const unit = items.find((i) => i.name === `${field}.unit`);
    // A box that sheds the field draws neither, and the shedding table is tested on its own.
    return value === undefined || unit === undefined ? [] : [{ name: box.name, value, unit }];
  });
};

/** Band D's stint page at every face that draws it. */
const BANDS = {
  '1920x480': { left: 0, top: 420, width: 1920, height: 60, corners: true },
  '1280x400': { left: 0, top: 346, width: 1280, height: 54, corners: true },
  '850x480': { left: 0, top: 420, width: 850, height: 60, corners: false },
  '800x286': { left: 0, top: 228, width: 800, height: 58, corners: false },
  '600x686': { left: 0, top: 630, width: 600, height: 56, corners: false },
} as const;

const bandSites = (): { name: string; value: TextItem; unit: TextItem }[] =>
  Object.entries(BANDS).flatMap(([name, band]) => {
    const items = bandPageItems('stint', band, '', band.corners).filter((i): i is TextItem => i.kind === 'text');
    const value = items.find((i) => i.name === 'lastStop.value');
    const unit = items.find((i) => i.name === 'lastStop.unit');
    return value === undefined || unit === undefined ? [] : [{ name, value, unit }];
  });

describe('a stop time', () => {
  test('is drawn to a tenth under 999.95 s and whole from there, to the most four cells hold', () => {
    for (const [seconds, reads] of READS) {
      expect({ seconds, reads: js(secondsToFit(`[S]`, CHARS.consumption, 1), { properties: { S: E.double(seconds) } }) }).toEqual({ seconds, reads });
    }
  });

  test('every reading takes no more cells than the budget, and none is wider than the widest it declares', () => {
    const widest = charsOfText(secondsToFitWidest(CHARS.consumption, 1), { charWidth: 2, specialCharsWidth: 1 } as Monospace);
    expect(secondsToFitWidest(CHARS.consumption, 1)).toBe('999.9');
    for (const [, reads] of READS) {
      const cells = charsOfText(reads, { charWidth: 2, specialCharsWidth: 1 } as Monospace);
      expect({ reads, digits: cells.digits <= CHARS.consumption.digits, all: cells.digits + cells.specials <= CHARS.consumption.digits + CHARS.consumption.specials }).toEqual({ reads, digits: true, all: true });
      // `999.9` is four digit cells and a point, and no rung draws more of either.
      expect(cells.digits * 2 + cells.specials).toBeLessThanOrEqual(widest.digits * 2 + widest.specials);
    }
  });

  test('the width it says it draws is the width of what it draws, so the unit sits after the figure', () => {
    const mono = moduleSites('pitView', 'pitTime')[0]!.value.monospace!;
    const drawn = secondsToFitDrawn('[S]', CHARS.consumption, 1)(mono);
    for (const [seconds, reads] of READS) {
      expect({ seconds, width: js(drawn, { properties: { S: E.double(seconds) } }) }).toEqual({ seconds, width: textWidth(reads, mono) });
    }
  });

  test('refuses a budget with no whole digit left beside its places', () => {
    expect(() => secondsToFit('[S]', { digits: 2, specials: 1 }, 2)).toThrow();
  });
});

describe("the pit view's Pit time fits its box past a thousand seconds", () => {
  const sites = moduleSites('pitView', 'pitTime');

  test('in every box the module is built into', () => {
    expect(sites.length).toBeGreaterThan(8);
  });

  test('a car sitting in its box for 1000 s, and the stop it leaves behind, read 1000 inside the box', () => {
    for (const { name, value, unit } of sites) {
      for (const [frame, what] of [
        [simHub(true, 1000, 0), 'IsInPitSince'],
        [simHub(false, 0, 1000), 'LastPitStopDuration'],
        [simHub(true, 3 * 3600, 0), 'IsInPitSince three hours'],
      ] as const) {
        const reads = String(js(formulaOf(value, 'Text'), frame));
        expect({ name, what, reads: reads.length <= 4 }).toEqual({ name, what, reads: true });
        expect({ name, what, reads, fits: widthAsDrawn(value, reads) < value.rect.width }).toEqual({ name, what, reads, fits: true });
        // What the fit tests measure the box by holds what it draws, in the face it is drawn in.
        expect({ name, what, reads, held: widthAsDrawn(value, reads) <= widthAsDrawn(value, value.widest!) }).toEqual({ name, what, reads, held: true });
        // And the unit stands where the figure ends, inside the box rather than at the end of the budget.
        const left = Number(js(formulaOf(unit, 'Left'), frame));
        expect({ name, what, left: left <= value.rect.left + value.rect.width }).toEqual({ name, what, left: true });
      }
    }
  });

  test('an ordinary stop still reads to the tenth', () => {
    expect(js(formulaOf(sites[0]!.value, 'Text'), simHub(true, 24.3, 0))).toBe('24.3');
    expect(js(formulaOf(sites[0]!.value, 'Text'), simHub(false, 0, 28.6))).toBe('28.6');
  });
});

describe("the tracker's Last stop fits its box past a thousand seconds", () => {
  const sites = [...moduleSites('stint', 'lastStop').map((s) => ({ ...s, name: `stint ${s.name}` })), ...bandSites().map((s) => ({ ...s, name: `band D ${s.name}` }))];

  test('on the stint page and on band D', () => {
    expect(sites.some((s) => s.name.startsWith('stint'))).toBe(true);
    expect(sites.some((s) => s.name.startsWith('band D'))).toBe(true);
  });

  test('a stop of 1000 s reads 1000 inside the box, and 24.3 s still reads 24.3', () => {
    for (const { name, value } of sites) {
      expect({ name, reads: js(formulaOf(value, 'Text'), tracker(24.3)) }).toEqual({ name, reads: '24.3' });
      for (const seconds of [1000, 12000]) {
        const reads = String(js(formulaOf(value, 'Text'), tracker(seconds)));
        expect({ name, seconds, reads, fits: widthAsDrawn(value, reads) < value.rect.width }).toEqual({ name, seconds, reads, fits: true });
      }
    }
  });

  test('every one declares the widest rung, a figure to the tenth, as its widest', () => {
    for (const { name, value } of sites) expect({ name, widest: value.widest }).toEqual({ name, widest: '999.9' });
    expect(BAND_PAGES.stint!.find((f) => f.id === 'lastStop')!.numeralWidest).toBe('999.9');
  });
});

test('the pit view declares the widest rung, a figure to the tenth, as its widest too', () => {
  for (const { name, value } of moduleSites('pitView', 'pitTime')) expect({ name, widest: value.widest }).toEqual({ name, widest: '999.9' });
});

// The frame helpers above read the rect a box hands a module; keep one literal box honest as well.
test('the wide zone body holds it', () => {
  const module = MODULES.find((m) => m.id === 'pitView')!;
  const value = [...walkItems(module.build({ frame: rect(0, 0, 1007, 211), density: 'zone', prefix: '' }))].find((i): i is TextItem => i.kind === 'text' && i.name === 'pitTime.value')!;
  expect(widthAsDrawn(value, '1000')).toBeLessThan(value.rect.width);
  expect(widthAsDrawn(value, '1000.0')).toBeGreaterThan(value.rect.width);
});
