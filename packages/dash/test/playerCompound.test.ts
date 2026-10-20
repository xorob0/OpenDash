/**
 * The player's tyre compound reads on Assetto Corsa as well as on iRacing (#1177).
 *
 * Band D's Compound and the tyres module's chip read SimHub's `fronttyrecompound` for the player, which
 * SimHub leaves null for every AC car (docs/research/sims.md), so on AC both drew nothing although the
 * game publishes the compound in its raw `Graphics.TyreCompound`. They fall back to that now, drawn as
 * the bracketed code AC writes after the name, which is what fits the field and the chip. The frames
 * below are what SimHub hands a dashboard on each sim: AC's reader has no `fronttyrecompound` for the
 * player and an empty string in the raw field until the game writes one, and iRacing's reader has no
 * raw `Graphics` block at all, which reads as null.
 *
 * Each reading is evaluated by the generator's own evaluator, SimHub's `right` and `left` included.
 */
import { describe, expect, test } from 'bun:test';
import { ncalcEvaluator as E, type Item, type TextItem } from '../src/generator.ts';
import { measureText, widestOf } from '../src/design/advances.ts';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { AC_TYRE_COMPOUND, COMPOUND_WIDEST, NO_VALUE } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { BAND_PAGES } from '../src/zones/bandPages.ts';

type Props = Record<string, unknown>;

const PLAYER = 'getplayerleaderboardposition()';
const FRONT = 'driverfronttyrecompound(3)';

/** Assetto Corsa in the race scenario, whose emulator writes the compound as AC's Kunos cars do. */
const AC: Props = { [PLAYER]: 3, [FRONT]: null, [AC_TYRE_COMPOUND]: 'Slick Medium (M)' };

/** iRacing, whose reader fills `fronttyrecompound` and has no raw `Graphics` block. */
const IRACING: Props = { [PLAYER]: 3, [FRONT]: 'Medium', [AC_TYRE_COMPOUND]: null };

const evaluate = (formula: string, props: Props): unknown => E.toJs(E.evaluate(formula, { properties: props }));

const band = (props: Props): unknown => {
  const field = BAND_PAGES.tyres!.find((f) => f.id === 'compound');
  if (!field) throw new Error('no compound field on band D');
  return evaluate(field.bind, props);
};

const chipText = (): TextItem => {
  const module = MODULES.find((m) => m.id === 'tyres');
  if (!module) throw new Error('no tyres module');
  const { width, height } = SHAPE_ARCHETYPES.wide;
  const items: Item[] = [...walkItems(module.build({ frame: rect(0, 0, width, height), density: 'zone', prefix: 'tyres.' }))];
  const found = items.find((i) => i.kind === 'text' && String(i.bindings?.Text?.formula ?? '').includes('fronttyrecompound'));
  if (found?.kind !== 'text') throw new Error('the tyres module draws no compound chip on a wide box');
  return found;
};

const chip = (props: Props): unknown => evaluate(String(chipText().bindings?.Text?.formula), props);

describe("band D's Compound", () => {
  test("on Assetto Corsa it reads the code AC writes after the compound's name", () => {
    expect(band(AC)).toBe('M');
    expect(band({ ...AC, [AC_TYRE_COMPOUND]: 'Semislicks (SM)' })).toBe('SM');
    expect(band({ ...AC, [AC_TYRE_COMPOUND]: 'Slick SuperSoft (SS)' })).toBe('SS');
    expect(band({ ...AC, [AC_TYRE_COMPOUND]: 'Street 90s (S9)' })).toBe('S9');
  });

  test('on Assetto Corsa a compound named with no code is cut to what the field holds', () => {
    expect(band({ ...AC, [AC_TYRE_COMPOUND]: 'Hypercar road' })).toBe('Hyp');
  });

  test('on Assetto Corsa before the game writes a compound it reads as not available, not as nothing', () => {
    expect(band({ ...AC, [AC_TYRE_COMPOUND]: '' })).toBe(NO_VALUE);
  });

  test("on iRacing it reads SimHub's compound, as it did", () => {
    expect(band(IRACING)).toBe('Medium');
  });

  test('where no sim publishes one it reads as not available', () => {
    expect(band({ [PLAYER]: 3 })).toBe(NO_VALUE);
  });
});

describe("the tyres module's chip", () => {
  test('reads the same compound on either sim, and nothing where neither publishes one', () => {
    expect(chip(AC)).toBe('M');
    expect(chip(IRACING)).toBe('Medium');
    expect(chip({ ...AC, [AC_TYRE_COMPOUND]: '' })).toBe('');
  });

  test('is measured by the widest compound either sim draws', () => {
    expect(chipText().widest).toBe(COMPOUND_WIDEST);
  });
});

test("AC's widest drawn compound, three of the face's widest glyph, sits inside the declared widest", () => {
  expect(measureText('BarlowMedium', widestOf('BarlowMedium', 3), 20)).toBeLessThan(measureText('BarlowMedium', COMPOUND_WIDEST, 20));
});
