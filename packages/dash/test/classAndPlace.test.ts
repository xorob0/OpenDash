/**
 * The class and the place in it, `LMP2 · P4`, read as a driver would read it and spelled once.
 *
 * It was written out three times, on the session page, the bar and the pit wall's session panel,
 * and each copy drew `GT3 · P0` on the grid, put the class name in whole and set the run in digit
 * cells that an `M` overruns (#603). So the tests below evaluate the one helper on the states that
 * went wrong, measure what it can draw against the widest it declares, and then hold every surface
 * and every package the build composes to that helper rather than to a fourth copy of it.
 */
import { describe, expect, test } from 'bun:test';
import { composePackages } from '../src/build.ts';
import { measureText } from '../src/design/advances.ts';
import { rect } from '../src/design/geometry.ts';
import { DATA_FACE } from '../src/design/metrics.ts';
import { ncalc } from '../src/generator.ts';
import type { TextItem } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { sessionPanel } from '../src/screens/pitwall.ts';
import { CHIP_CHARS, CHIP_WIDEST } from '../src/second/chip.ts';
import { CLASS_AND_PLACE_WIDEST, classAndPlace, NO_VALUE, player } from '../src/second/values.ts';
import { expressionsIn, walkItems } from '../src/walk.ts';
import { BAR_FIELD_SPECS } from '../src/zones/bar.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const { num } = ncalc;

/** What the pair reads for the car on row 3 of the leaderboard, of the class and place given. */
const read = (carClass: string | null, place: number | null): unknown => {
  const props: Props = {};
  if (carClass !== null) props['drivercarclass(3)'] = carClass;
  if (place !== null) props['driverclassposition(3)'] = place;
  return evalNcalc(classAndPlace(num(3)), props);
};

describe('the helper', () => {
  test('a placed car reads its class and its place', () => {
    expect(read('GT3', 4)).toBe('GT3 · P4');
    expect(read('LMP2', 12)).toBe('LMP2 · P12');
  });

  test('a car the sim has not placed reads the placeholder, not P0', () => {
    // The grid before the green flag: the car is on the leaderboard with a class place of zero,
    // and the composite drew `GT3 · P0` there while every other position on the face drew `--`.
    expect(read('GT3', 0)).toBe(`GT3 · P${NO_VALUE}`);
    expect(read('GT3', null)).toBe(`GT3 · P${NO_VALUE}`);
  });

  test(`the class is cut to the ${CHIP_CHARS} characters a chip holds, in capitals, as the chip cuts it`, () => {
    expect(read('Ferrari 296 GT3', 4)).toBe('FERR · P4');
    expect(read('gt3', 4)).toBe('GT3 · P4');
    expect(String(read('Hypercar', 1)).split(' · ')[0]).toHaveLength(CHIP_CHARS);
  });

  test('a car with no class name reads its place alone, not a separator with nothing before it', () => {
    expect(read('', 3)).toBe('P3');
    expect(read(null, 3)).toBe('P3');
    expect(read(null, 0)).toBe(`P${NO_VALUE}`);
  });

  test('the class and the place are read from the one car the helper is handed', () => {
    // Any car's pair can be drawn, and the two halves cannot come from two different entries.
    const expression = classAndPlace(player());
    expect(evalNcalc(expression, { 'getplayerleaderboardposition()': 7, 'drivercarclass(7)': 'LMP2', 'driverclassposition(7)': 2 })).toBe('LMP2 · P2');
  });
});

/**
 * Class names a driver meets, as a sim reports them: iRacing's multiclass classes, the single-make
 * series that race as a class named after the car, and the classes Assetto Corsa and ACC report.
 * The helper keeps the first four letters of whichever it is handed, so this list, and not the
 * chip's `LMP2`, is what the widest has to hold. A class found on a rig that the list lacks belongs
 * here, and the test then says whether the declaration still holds it.
 */
const CLASS_NAMES = [
  // Multiclass classes.
  'GTP', 'LMP2', 'LMP3', 'LMDh', 'Hypercar', 'GTE', 'GTD', 'GT3', 'GT3 Class', 'GT4', 'GT4 Class', 'TCR', 'IMSA',
  // Single-make classes, named after the car or its series.
  'Porsche 911 Cup', 'PCup', 'Mustang', 'Ford GT', 'Mercedes W13', 'Mercedes-AMG', 'McLaren MP4-30', 'BMW M4 GT4',
  'BMW M Hybrid V8', 'Lamborghini', 'Ferrari 296 GT3', 'Aston Martin', 'Audi RS 3 LMS', 'Cadillac V-Series.R',
  'Acura ARX-06', 'Toyota GR86', 'Mazda MX-5 Cup', 'Global Mazda MX-5 Cup', 'MX-5 Cup', 'Chevrolet Camaro',
  'Supercars', 'Formula Vee', 'Formula Renault', 'Super Formula', 'Dallara IR18', 'Dallara P217', 'Skip Barber',
  'Ray FF1600', 'Williams FW31', 'Lotus 79', 'Radical SR8', 'Ligier JS P320', 'Porsche 963', 'Kia Optima',
  'Renault Clio', 'Volkswagen Jetta', 'VW Beetle', 'Mini Cooper', 'NASCAR Cup', 'Xfinity', 'ARCA Menards',
  'Legends', 'Late Model', 'Street Stock', 'Modified', 'Sprint Car', 'Silver Crown', 'Midget', 'Pro Mazda',
  'Indy Pro 2000', 'USF 2000', 'HPD',
  // Assetto Corsa's and ACC's classes.
  'Race', 'Street', 'Drift', 'Vintage', 'Touring', 'Prototype', 'GT2', 'CUP', 'ST', 'CHL', 'TCX',
];

describe('the widest it declares', () => {
  // The cut the helper makes, so a class longer than four letters is measured as it is drawn.
  const cuts = [...new Set(CLASS_NAMES.map((name) => name.slice(0, CHIP_CHARS).toUpperCase()))];
  // The face is proportional, so the widest place is not the longest-looking one: its `4` is the
  // widest digit Barlow Condensed has, which is why the bar's `LMP2 · P24` was a pixel short.
  const places = [...Array.from({ length: 99 }, (_, i) => String(i + 1)), NO_VALUE];
  const readings = cuts.flatMap((cut) => places.map((place) => `${cut} · P${place}`));

  test('is a reading the helper draws for a real class, not a made-up bound', () => {
    expect(readings).toContain(CLASS_AND_PLACE_WIDEST);
  });

  test("is wider than the chip's LMP2, which a Mustang or a Mercedes overran", () => {
    for (const weight of ['SemiBold', 'Bold'] as const) {
      expect(measureText(DATA_FACE[weight], `MUST · P44`, 100)).toBeGreaterThan(measureText(DATA_FACE[weight], `${CHIP_WIDEST} · P44`, 100));
    }
  });

  for (const weight of ['SemiBold', 'Bold'] as const) {
    test(`holds every class at every place from 1 to 99 and the placeholder, in ${weight}`, () => {
      // The advances are summed in floating point, and `LMDH` and `MUST` come to the same width in
      // SemiBold by different sums, so a tie is read as a tie rather than as the last bit of a sum.
      const limit = measureText(DATA_FACE[weight], CLASS_AND_PLACE_WIDEST, 100) + 1e-9;
      const wider = readings.filter((text) => measureText(DATA_FACE[weight], text, 100) > limit);
      expect(wider).toEqual([]);
    });
  }
});

describe('every surface draws the helper, proportionally', () => {
  const expected = classAndPlace(player());

  /** The class value as it is drawn: bound to the helper, set in advances rather than cells, measured from its widest. */
  const drawnAsTheHelper = (item: TextItem | undefined) => {
    expect(item).toBeDefined();
    const formula = item!.bindings?.Text?.formula;
    expect({ formula, monospace: item!.monospace, widest: item!.widest }).toEqual({ formula: expected, monospace: undefined, widest: CLASS_AND_PLACE_WIDEST });
    // The box is cut from the declaration, so the widest reading fits it at the size it is drawn.
    expect(item!.rect.width).toBeGreaterThanOrEqual(measureText(DATA_FACE.SemiBold, CLASS_AND_PLACE_WIDEST, item!.fontSize));
  };

  const textNamed = (items: Iterable<{ kind: string; name: string }>, name: string): TextItem | undefined =>
    [...items].find((i): i is TextItem => i.kind === 'text' && i.name === name);

  test('the session page', () => {
    const items = MODULES.find((m) => m.id === 'session')!.build({ frame: rect(0, 0, 769, 358), density: 'companion', prefix: '' });
    drawnAsTheHelper(textNamed(walkItems(items), 'class.value'));
  });

  test("the pit wall's session panel", () => {
    drawnAsTheHelper(textNamed(walkItems(sessionPanel('session', rect(0, 0, 600, 160))), 'session.class.value'));
  });

  test('the bar', () => {
    const spec = BAR_FIELD_SPECS.find((f) => f.id === 'classPosition')!;
    expect({ bind: spec.bind, widest: spec.widest }).toEqual({ bind: expected, widest: CLASS_AND_PLACE_WIDEST });
  });

  test("and nothing the build emits spells ' · P' outside it", () => {
    // Every binding of every package, faces and second screens alike: the separator and the P are
    // the helper's, and an expression that carries them and is not the helper is a fourth copy.
    const stray = new Set<string>();
    let found = 0;
    for (const composed of composePackages({ version: '0.0.0-test', log: () => {} }, true)) {
      for (const dashboard of composed.pkg.dashboards) {
        for (const expression of expressionsIn(dashboard)) {
          if (!expression.includes("' · P'")) continue;
          found += 1;
          if (!expression.includes(expected)) stray.add(expression);
        }
      }
    }
    expect(found).toBeGreaterThan(0);
    expect([...stray]).toEqual([]);
  });
});
