/**
 * The class and the place in it, `LMP2 · P4`, read as a driver would read it and spelled once.
 *
 * It was written out three times, on the session page, the bar and the pit wall's session panel,
 * and each copy drew `GT3 · P0` on the grid, put the class name in whole and set the run in digit
 * cells that an `M` overruns (#603). So the tests below evaluate the one helper on the states that
 * went wrong, measure what it can draw against the widest it declares, and then hold every surface
 * and every package the build composes to that helper rather than to a fourth copy of it.
 *
 * **The widest is measured in each theme's own process.** A theme's package is built in a process of
 * its own, whose overlay changes the face the numerals are set in, and a widest measured in the
 * house's Barlow Condensed held nothing in the Porsche's Barlow Medium, where `LMDH · P44` is wider
 * than the `MUST · P44` its boxes were cut from (#1058). So the default process starts this file
 * again in a process drawing each other theme, as `themeFuelPage.test.ts` does, and there the
 * declaration is measured in that theme's face and every face of that theme is held to the class
 * list. What pins the house's own surfaces runs in the default process alone.
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
import { CHIP_CHARS } from '../src/second/chip.ts';
import { CLASS_AND_PLACE_WIDEST, classAndPlace, NO_VALUE, player } from '../src/second/values.ts';
import { buildThemeFace, drawsInThisProcess } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { THEME_ID } from '../src/tokens.ts';
import { expressionsIn, walkItems } from '../src/walk.ts';
import { BAR_FIELD_SPECS } from '../src/zones/bar.ts';
import { CLASS_CUTS } from './classNames.ts';
import { widthAsDrawn } from './drawnStrings.ts';
import { bindingExpression } from './monoGlyphs.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const { num } = ncalc;

/** Whether this process draws the house's face, which is where the house's own surfaces are pinned. */
const HOUSE = THEME_ID === DEFAULT_THEME_ID;

// The face is proportional, so the widest place is not the longest-looking one: its `4` is the
// widest digit Barlow Condensed has, which is why the bar's `LMP2 · P24` was a pixel short.
const PLACES = [...Array.from({ length: 99 }, (_, i) => String(i + 1)), NO_VALUE];
// Every class name a driver meets, as the helper cuts it, so a class longer than four letters is
// measured as it is drawn. The list is `classNames.ts`'s, which the blue flag band reads as well.
const READINGS = CLASS_CUTS.flatMap((cut) => PLACES.map((place) => `${cut} · P${place}`));

/**
 * What the pair reads for the car on row 3 of the leaderboard, of the class and class place given,
 * and of the overall place, which is what says whether the sim has placed the car at all. It is the
 * class place unless a test says otherwise; null leaves the read out.
 */
const read = (carClass: string | null, place: number | null, overall: number | null = place): unknown => {
  const props: Props = {};
  if (carClass !== null) props['drivercarclass(3)'] = carClass;
  if (place !== null) props['driverclassposition(3)'] = place;
  if (overall !== null) props['driverposition(3)'] = overall;
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

  test('a class place SimHub counted before the sim placed anyone reads the placeholder (#1014)', () => {
    // SimHub never reports a class place of 0 for a listed car: it numbers every car of a class from
    // 1 in the order of the driver list before anyone is placed, and only the overall place is 0.
    expect(read('GT3', 7, 0)).toBe(`GT3 · P${NO_VALUE}`);
    expect(read('GT3', 7, null)).toBe(`GT3 · P${NO_VALUE}`);
    expect(read(null, 7, 0)).toBe(`P${NO_VALUE}`);
    // Placed overall, the class place is the one drawn, whatever the overall place is.
    expect(read('GT3', 7, 19)).toBe('GT3 · P7');
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
    expect(evalNcalc(expression, { 'getplayerleaderboardposition()': 7, 'drivercarclass(7)': 'LMP2', 'driverclassposition(7)': 2, 'driverposition(7)': 9 })).toBe('LMP2 · P2');
  });
});

describe(`the widest it declares, in ${THEME_ID}'s face`, () => {
  test('is a reading the helper draws for a real class, not a made-up bound', () => {
    expect(READINGS).toContain(CLASS_AND_PLACE_WIDEST);
  });

  if (HOUSE) {
    test("is wider than LMP2, the chip's widest until #569, which a Mustang or a Mercedes overran", () => {
      expect(CLASS_AND_PLACE_WIDEST).toBe('MUST · P44');
      for (const weight of ['SemiBold', 'Bold'] as const) {
        expect(measureText(DATA_FACE[weight], CLASS_AND_PLACE_WIDEST, 100)).toBeGreaterThan(measureText(DATA_FACE[weight], `LMP2 · P44`, 100));
      }
    });
  }

  for (const weight of ['SemiBold', 'Bold'] as const) {
    test(`holds every class at every place from 1 to 99 and the placeholder, in ${weight}, which is ${DATA_FACE[weight]} here`, () => {
      // The advances are summed in floating point, and `LMDH` and `MUST` come to the same width in
      // SemiBold by different sums, so a tie is read as a tie rather than as the last bit of a sum.
      const limit = measureText(DATA_FACE[weight], CLASS_AND_PLACE_WIDEST, 100) + 1e-9;
      const wider = READINGS.filter((text) => measureText(DATA_FACE[weight], text, 100) > limit);
      expect(wider).toEqual([]);
    });
  }
});

/**
 * Every face of every theme this process draws, as it ships: every text bound to the helper holds
 * every reading of the class list at every place, in the face and at the size it is drawn in,
 * whatever its theme declares for it. The theme's own declaration is what its box is cut from, and
 * `conformance.ts` measures the box by that declaration alone, so a declaration the class list
 * outgrows passes there with a box cut to the wrong string.
 */
describe('on every face of every theme, the class box holds every class at every place', () => {
  const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
  const helper = classAndPlace(player());
  for (const id of Object.keys(THEMES).filter(drawsInThisProcess)) {
    for (const size of THEMES[id]!.anatomy.sizes) {
      test(`${id} ${size.width}x${size.height}`, () => {
        const { main, zones } = buildThemeFace(id, size, OPTS).built;
        let found = 0;
        const over: string[] = [];
        for (const dashboard of [main, ...zones]) {
          for (const screen of dashboard.screens) {
            for (const item of walkItems(screen.items)) {
              if (item.kind !== 'text' || !bindingExpression(item, 'Text').includes(helper)) continue;
              found += 1;
              const where = `${dashboard.name} ${screen.name}: ${item.name}`;
              if (item.widest === undefined) {
                over.push(`${where} declares no widest`);
                continue;
              }
              const limit = widthAsDrawn(item, item.widest) + 1e-9;
              const wider = READINGS.filter((text) => widthAsDrawn(item, text) > limit);
              if (wider.length > 0) over.push(`${where} declares ${JSON.stringify(item.widest)} and draws ${wider.map((t) => JSON.stringify(t)).join(', ')} wider`);
              if (widthAsDrawn(item, item.widest) > item.rect.width) over.push(`${where} draws its widest past a ${item.rect.width} px box`);
            }
          }
        }
        expect(found).toBeGreaterThan(0);
        expect(over).toEqual([]);
      });
    }
  }
});

if (HOUSE) describe('every surface draws the helper, proportionally', () => {
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

if (HOUSE) {
  for (const id of Object.keys(THEMES).filter((each) => !drawsInThisProcess(each))) {
    test(
      `the ${id} theme's class and place, in a process drawing its faces`,
      () => {
        const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: id }, stdout: 'pipe', stderr: 'pipe' });
        const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
        expect({ theme: id, exit: run.exitCode, output }).toEqual({ theme: id, exit: 0, output: '' });
      },
      120_000,
    );
  }
}
