/**
 * A lap of ten minutes fits every box a lap time is drawn in (#883).
 *
 * `toshorttime` writes the minutes unpadded, so `1:42.905` is six digits and a Nordschleife lap in
 * the MX-5 Cup, `10:30.123`, is seven. Every lap-time box was cut for six and declared no `widest`,
 * so the fit tests measured the sample and passed while the lap pop-up and the lap review drew
 * `10:30.12` and a sliver of the last digit. The fix is in the reading rather than the boxes: a lap
 * of ten minutes is drawn to hundredths, `lapReading` in `second/values.ts` says why.
 *
 * The fit tests cannot see this for themselves, since what a lap time draws is the sim's and not a
 * string in the expression. So the bindings are evaluated, as the browser demo evaluates them, with
 * every lap the expression reads set to a ten-minute lap, and what comes back is held against the
 * box and against the `widest` the item declares, which is what the fit tests measure the box by.
 * The loop is every text of every package every theme builds, and every module in every box the
 * second screens are proved against, so a lap time added tomorrow is measured the day it is added.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages, themesToBuild } from '../src/build.ts';
import { ncalcEvaluator as E, type TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { itemsOf, walkItems } from '../src/walk.ts';
import { CHARS, LAP_TIME_WIDEST, lapTime, lapTimeWidest, noTime } from '../src/second/values.ts';
import { SPECIAL_CHARS } from '../src/design/metrics.ts';
import { measureText } from '../src/design/advances.ts';
import { widthAsDrawn } from './drawnStrings.ts';
import { bindingExpression, faceOf } from './monoGlyphs.ts';
import { moduleBoxes } from './secondScreens.test.ts';

/** The lap reads every lap-time binding makes: the player's, the tracker's, the five-lap history's, the session best's. */
const LAP_PROPERTY = /(LapTime|PreviousLap_\d\d|ClassBestLap)$/;

/**
 * A frame in which every lap is `seconds` long and every car is on the board. Everything else is
 * null, which is what the guards around a lap time were written for.
 */
const frame = (seconds: number): E.Scope => ({
  properties: (name) => (LAP_PROPERTY.test(name) ? E.fromSeconds(seconds) : undefined),
  calls: (text) => (/^driver(last|best)lap\(/.test(text) ? E.fromSeconds(seconds) : /^getopponentleaderboardposition/.test(text) ? 2 : undefined),
  repeat: [1, 1, 1],
});

const drawn = (expression: string, seconds: number): string => {
  const result = E.evaluateBinding(expression, frame(seconds));
  if (!result.ok) throw new Error(`${expression} failed at ${seconds} s: ${result.error.message}`);
  return E.valueToString(result.value);
};

/** The cells a reading takes, as a monospaced box counts them. */
const cellsOf = (text: string): { digits: number; specials: number } => {
  const specials = [...text].filter((c) => SPECIAL_CHARS.includes(c)).length;
  return { digits: text.length - specials, specials };
};

describe('a lap time', () => {
  const at = (seconds: number, decimals = 3): string => drawn(lapTime('[DataCorePlugin.GameData.LastLapTime]', decimals), seconds);

  test('is m:ss.fff under ten minutes', () => {
    expect(at(102.905)).toBe('1:42.905');
    expect(at(599.999)).toBe('9:59.999');
  });

  test('and is drawn to hundredths from ten minutes, which is the digit the box has room for', () => {
    expect(at(600)).toBe('10:00.00');
    expect(at(630.123)).toBe('10:30.12');
    expect(at(659.999)).toBe('10:59.99');
  });

  test('to one place, it keeps its place, which a two-digit minute already fits', () => {
    expect(at(102.3, 1)).toBe('1:42.3');
    expect(at(630.1, 1)).toBe('10:30.1');
  });

  test('never set, it is the placeholder', () => {
    expect(at(0)).toBe(noTime());
    expect(drawn(lapTime('[Nothing.Here]'), 630.123)).toBe(noTime());
  });

  test('every reading is inside the budget, and the widest is the budget', () => {
    for (const reading of [at(102.905), at(599.999), at(630.123), at(3599.999)]) {
      const { digits, specials } = cellsOf(reading);
      expect({ reading, fits: digits <= CHARS.lapTime.digits && specials <= CHARS.lapTime.specials }).toEqual({ reading, fits: true });
    }
    expect(cellsOf(LAP_TIME_WIDEST)).toEqual(CHARS.lapTime);
    expect(cellsOf(lapTimeWidest(1))).toEqual({ digits: CHARS.lapTime.digits - 1, specials: CHARS.lapTime.specials });
  });
});

/**
 * Ten minutes and a half, the Nordschleife in the MX-5 Cup; the last lap a single minute digit draws;
 * and a lap either side of ten minutes in the digit Barlow draws widest, which is what a proportional
 * reading has to be measured against.
 */
const LAPS = [630.123, 599.999, 644.444, 284.444];

/** What an item that draws a lap time does with these laps, if it draws one at all. */
function readings(item: TextItem): { lap: number; text: string; width: number }[] {
  const expression = bindingExpression(item, 'Text');
  if (!expression.includes('toshorttime(')) return [];
  return LAPS.map((lap) => {
    const text = drawn(expression, lap);
    return { lap, text, width: widthAsDrawn(item, text) };
  });
}

/** The readings of an item that its box would clip, or that its declared `widest` does not hold. */
function problems(item: TextItem): object[] {
  const found = readings(item);
  if (found.length === 0) return [];
  if (item.widest === undefined) return [{ item: item.name, declares: 'no widest', drawn: found.map((r) => r.text) }];
  // `lapTimeWidest` is all 4s because that is the widest digit of the faces a lap is drawn in, and
  // in a proportional face that is a claim about the face.
  const face = faceOf(item);
  const widestDigit = [...'0123456789'].reduce((w, d) => (measureText(face, d, 100) > measureText(face, w, 100) ? d : w), '4');
  if (!item.monospace && widestDigit !== '4') return [{ item: item.name, face, widestDigit }];
  const limit = widthAsDrawn(item, item.widest);
  return found.filter((r) => r.width > item.rect.width || r.width > limit).map((r) => ({ item: item.name, drawn: r.text, width: r.width, box: item.rect.width, widest: item.widest }));
}

describe('a lap of ten minutes fits every box a lap time is drawn in', () => {
  const themes = themesToBuild({ themes: [], allThemes: true, touchedThemes: false }, () => {});
  const packages = composePackages({ version: '0.0.0-test', themes, log: () => {} });

  test('there is something to check, on the faces, the cards and the second screens', () => {
    const drawing = packages.flatMap(({ pkg }) => pkg.dashboards.flatMap((d) => itemsOf(d).filter((i): i is TextItem => i.kind === 'text' && readings(i).length > 0)));
    const names = new Set(drawing.map((i) => i.name.replace(/^.*\.(?=[^.]+\.value$)/, '')));
    expect(drawing.length).toBeGreaterThan(100);
    for (const name of ['lastLap.value', 'bestLap.value', 'last.value', 'sessionBest.value', 'yourBest.value']) expect(names).toContain(name);
    expect(drawing.some((i) => i.name.endsWith('popUp.lap.value'))).toBe(true);
    expect(drawing.some((i) => i.name.endsWith('lapReview.lap.value'))).toBe(true);
  });

  for (const { pkg } of packages) {
    test(pkg.folderName, () => {
      for (const dashboard of pkg.dashboards) {
        for (const item of itemsOf(dashboard)) {
          if (item.kind !== 'text') continue;
          expect({ dashboard: dashboard.name, problems: problems(item) }).toEqual({ dashboard: dashboard.name, problems: [] });
        }
      }
    });
  }

  for (const box of moduleBoxes()) {
    test(`every module on a ${box.name}`, () => {
      for (const module of MODULES) {
        for (const item of walkItems(module.build({ frame: box.frame, density: box.density, prefix: '' }))) {
          if (item.kind !== 'text') continue;
          expect({ module: module.id, problems: problems(item) }).toEqual({ module: module.id, problems: [] });
        }
      }
    });
  }
});
