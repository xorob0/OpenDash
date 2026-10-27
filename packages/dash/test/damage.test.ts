/**
 * Module 20 draws the car's damage where the sim reports it and says so where it does not (#582).
 *
 * The page is built both ways and the plugin's `DamageReported` decides which half is on the
 * screen, so these tests evaluate the bindings the build wrote rather than reading the items: what a
 * driver on Assetto Corsa sees after a hit, and what a driver on iRacing sees, whose SimHub reader
 * fills the same members with zeros.
 */
import { describe, expect, test } from 'bun:test';
import type { Item, TextItem } from '../src/generator.ts';
import { rect } from '../src/design/geometry.ts';
import { DAMAGE_HEAVY, DAMAGE_MESSAGE, DAMAGE_PANELS, DAMAGE_WORDS, damage } from '../src/modules/damage.ts';
import { densityForBox } from '../src/second/density.ts';
import { zoneFrame } from '../src/second/header.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { insideBox } from './conformance.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const GAME = 'DataCorePlugin.GameData.';
const REPORTED = 'OpenDash.DamageReported';

const items = damage.build({ frame: rect(0, 0, 600, 300), density: 'companion', prefix: 'damage.' });
const all = items.flatMap((i) => [...walkItems([i])]);
const named = (name: string): Item => {
  const found = all.find((i) => i.name === name);
  if (found === undefined) throw new Error(`no item ${name}`);
  return found;
};
const bound = (item: Item, target: 'Visible' | 'Text' | 'TextColor' | 'BackgroundColor', props: Props): unknown => {
  const binding = item.bindings?.[target];
  if (binding === undefined || binding.mode !== 'formula') throw new Error(`${item.name} has no ${target} formula`);
  return evalNcalc(binding.formula as string, props);
};

/** What the damage page shows a driver: which half is on screen, and each panel's word and paint. */
const read = (props: Props) => {
  const drawing = named('damage.reported');
  const notice = named('damage.placeholder') as TextItem;
  return {
    drawing: bound(drawing, 'Visible', props),
    notice: bound(notice, 'Visible', props),
    panels: DAMAGE_PANELS.map(({ id, panel }) => ({
      id,
      word: bound(named(`damage.${id}.value`), 'Text', props),
      colour: bound(named(`damage.car.${panel}`), 'BackgroundColor', props),
    })),
  };
};

describe('the damage page reads the sim it is on', () => {
  test('a sim that reports damage gets the car, with the hit panel coloured and named', () => {
    // The AC emulator's race scenario: the front member at 12.5 after a nudge, the rest untouched.
    const shown = read({ [REPORTED]: true, [`${GAME}CarDamage1`]: 12.5, [`${GAME}CarDamage2`]: 0, [`${GAME}CarDamage3`]: 0, [`${GAME}CarDamage4`]: 0 });
    expect(shown.drawing).toBe(true);
    expect(shown.notice).toBe(false);
    expect(shown.panels).toEqual([
      { id: 'front', word: DAMAGE_WORDS.light, colour: ds.color.caution.primary },
      { id: 'rear', word: DAMAGE_WORDS.none, colour: ds.purpose.illustration.dim },
      { id: 'left', word: DAMAGE_WORDS.none, colour: ds.purpose.illustration.dim },
      { id: 'right', word: DAMAGE_WORDS.none, colour: ds.purpose.illustration.dim },
    ]);
  });

  test('past the heavy threshold the panel turns to danger', () => {
    const shown = read({ [REPORTED]: true, [`${GAME}CarDamage4`]: DAMAGE_HEAVY + 1 });
    expect(shown.panels.find((p) => p.id === 'right')).toEqual({ id: 'right', word: DAMAGE_WORDS.heavy, colour: ds.color.danger.primary });
  });

  test('a sim that reports none, or no plugin at all, gets the notice and never a car painted from zeros', () => {
    for (const props of [{ [REPORTED]: false, [`${GAME}CarDamage1`]: 0 }, {}] as Props[]) {
      const shown = read(props);
      expect(shown.drawing).toBe(false);
      expect(shown.notice).toBe(true);
    }
    expect((named('damage.placeholder') as TextItem).text).toBe(DAMAGE_MESSAGE);
  });

  test('each word is drawn in the colour of its panel', () => {
    const props = { [REPORTED]: true, [`${GAME}CarDamage1`]: DAMAGE_HEAVY + 10, [`${GAME}CarDamage2`]: 1 };
    expect(bound(named('damage.front.value'), 'TextColor', props)).toBe(ds.color.danger.primary);
    expect(bound(named('damage.rear.value'), 'TextColor', props)).toBe(ds.color.caution.primary);
    expect(bound(named('damage.left.value'), 'TextColor', props)).toBe(ds.color.text.primary);
  });
});

describe('the words beside the car stay in the frame once the stack has grown them (#1167)', () => {
  // The face's 274 x 366 zone is a one-column shape, so the four words are centred by default. The
  // grid is chosen at the size the words fit before growing, two columns of 63 px there, and `stack`
  // then grows them to the point that a word no longer fits its cell and the rank wraps to one per
  // line. Each line used to be placed as a 63 px cell centred in the frame while its value drew 110
  // px wide, so the value ended seven pixels past the frame's right edge.
  const size = { width: 274, height: 366 };
  const density = densityForBox(size);
  const { body } = zoneFrame('probe', { frame: rect(0, 0, size.width, size.height), title: 'PROBE', counter: { kind: 'reserved', widest: '21 / 21' } }, density, 'face');
  const drawn = damage.build({ frame: body, density, prefix: 'damage.' }).flatMap((i) => [...walkItems([i])]);
  const values = DAMAGE_PANELS.map(({ id }) => drawn.find((i) => i.name === `damage.${id}.value`) as TextItem);

  test('the stack grew the words past the grid they were fitted in, which is the case that drew outside', () => {
    expect(values.every((v) => v !== undefined)).toBe(true);
    // One word under another, each on a line of its own: the grid of two has become one column. Where
    // each one starts is the centring's business, and a word with a wider `widest` would start
    // further left without the layout being wrong, so only the lines are counted.
    expect(new Set(values.map((v) => v.rect.top)).size).toBe(4);
  });

  test('every item is inside the frame', () => {
    const out = drawn
      .filter((i): i is Exclude<Item, { kind: 'layer' }> => i.kind !== 'layer')
      .filter((i) => !insideBox(i, body))
      .map((i) => `${i.name} at ${i.rect.left} + ${i.rect.width}`);
    expect(out).toEqual([]);
  });
});
