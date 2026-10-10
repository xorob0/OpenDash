/** Every card draws inside its slot at every rung, with unique prefixed names and the catalogue label. */
import { describe, expect, test } from 'bun:test';
import { CARDS } from '../src/cards/index.ts';
import { contains, rect } from '../src/design/geometry.ts';
import { rungFor } from '../src/design/rung.ts';
import type { TextItem } from '../src/generator.ts';
import { NO_VALUE } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const slots = [rect(0, 0, 255, 187), rect(513, 253, 255, 187), rect(0, 0, 223, 156), rect(0, 0, 140, 104)];

describe('cards', () => {
  for (const card of CARDS) {
    test(`${card.number} ${card.id} fits every slot`, () => {
      for (const slot of slots) {
        const items = card.build(slot, `${card.id}.`);
        expect(items.length).toBeGreaterThanOrEqual(2);
        const flat = [...walkItems(items)];
        for (const item of flat) {
          expect(item.name.startsWith(`${card.id}.`)).toBe(true);
          if ('rect' in item) {
            expect({ rung: rungFor(slot.width, slot.height), item: item.name, rect: item.rect, inside: contains(slot, item.rect) }).toMatchObject({ inside: true });
            for (const v of Object.values(item.rect)) expect(Number.isInteger(v)).toBe(true);
          }
        }
        expect(new Set(flat.map((i) => i.name)).size).toBe(flat.length);
      }
    });
  }

  test('labels match the catalogue and values are monospaced', () => {
    for (const card of CARDS) {
      const items = card.build(rect(0, 0, 255, 187), `${card.id}.`);
      const label = items.find((i) => i.name === `${card.id}.label`);
      if (label?.kind !== 'text') throw new Error(`${card.id} has no label`);
      expect(label.text).toBe(card.label);
      expect(label.fontSize).toBe(15);
      expect(label.textColor).toBe('#5A6069');
      // A mark is the one text of a card that is not laid in cells and cannot be: `∞` advances a
      // third wider than the digit cell, so the session card's untimed mark is a proportional run
      // beside its clock rather than a string the clock is bound to (#439).
      const drawn = items.filter((i) => i.kind === 'text' && i.name !== `${card.id}.label` && !i.name.endsWith('denominator') && !i.name.endsWith('unit'));
      const values = drawn.filter((i) => !i.name.endsWith('.mark'));
      expect(values.length).toBeGreaterThan(0);
      for (const v of values) if (v.kind === 'text') expect(v.monospace).toBeDefined();
      for (const m of drawn.filter((i) => i.name.endsWith('.mark'))) if (m.kind === 'text') expect({ name: m.name, mono: m.monospace, widest: m.widest }).toMatchObject({ mono: undefined, widest: m.text });
    }
  });

  test('rung L readouts sit on the canvas line boxes', () => {
    const items = CARDS[1]!.build(rect(0, 0, 255, 187), 'x.');
    const [label, value] = items;
    if (label?.kind !== 'text' || value?.kind !== 'text') throw new Error('readout');
    expect(label.rect).toEqual({ left: 16, top: 50, width: 223, height: 19 });
    // 220 px of cells plus the slack that keeps WPF from clipping the last glyph.
    expect(value.rect).toEqual({ left: 16, top: 65, width: 224, height: 78 });
    expect(value.fontSize).toBe(64);
    const grid = CARDS[10]!.build(rect(0, 0, 255, 187), 'x.');
    const placed = (items: typeof grid, name: string) => {
      const item = items.find((i) => i.name === name);
      if (item?.kind !== 'text') throw new Error(`${name} is not a text item`);
      return [item.rect.left, item.rect.top, item.fontSize];
    };
    const corners = ['x.fl', 'x.fr', 'x.rl', 'x.rr'];
    expect(['x.label', ...corners].map((n) => placed(grid, n))).toEqual([[16, 30, 15], [16, 49, 34], [137, 49, 34], [16, 104, 34], [137, 104, 34]]);
    // At rung L the tread left follows the temperature on a second line, its per-cent sign after
    // the cells; the shorter slots keep the plain numerals and drop both.
    expect(['x.fl.sub', 'x.fl.subunit'].map((n) => placed(grid, n))).toEqual([[16, 87, 13], [37, 87, 13]]);
    for (const shorter of [rect(0, 0, 223, 156), rect(0, 0, 140, 104)]) {
      expect(CARDS[10]!.build(shorter, 'x.').map((i) => i.name)).toEqual(['x.label', ...corners]);
    }
    const row = CARDS[4]!.build(rect(0, 0, 255, 187), 'x.');
    const denominator = row[2];
    if (denominator?.kind !== 'text') throw new Error('denominator');
    expect(denominator.rect).toEqual({ left: 55, top: 83, width: 153, height: 57 });
    expect(denominator.fontSize).toBe(46);
    expect(denominator.monospace).toBeUndefined();
  });
});

/**
 * The position card read as a driver reads it, which is the card's own value and where its
 * denominator lands, evaluated rather than matched.
 */
describe('the position card', () => {
  const slot = rect(0, 0, 255, 187);
  const items = CARDS.find((c) => c.id === 'position')!.build(slot, 'x.');
  const text = (name: string): TextItem => {
    const item = items.find((i) => i.name === name);
    if (item?.kind !== 'text') throw new Error(`${name} is not a text item`);
    return item;
  };
  const value = text('x.value');
  const denominator = text('x.denominator');
  /**
   * The player's place as SimHub reports it, overall and in class alike, and through the published
   * property and the leaderboard alike, so the frame holds whichever of them the card asks whether
   * it is placed; null where it reports none.
   */
  const read = (place: number | null): { value: unknown; left: unknown } => {
    const props: Props = { 'getplayerleaderboardposition()': 5 };
    if (place !== null) Object.assign(props, { 'DataCorePlugin.GameData.Position': place, 'driverposition(5)': place, 'driverclassposition(5)': place });
    return {
      value: evalNcalc(String(value.bindings?.Text?.formula ?? ''), props),
      left: evalNcalc(String(denominator.bindings?.Left?.formula ?? ''), props),
    };
  };

  test('a placed car reads its place, with the count one gap after its last digit', () => {
    const four = read(4);
    const twelve = read(12);
    expect(four.value).toBe('4');
    expect(twelve.value).toBe('12');
    expect(Number(twelve.left) - Number(four.left)).toBe(value.monospace!.charWidth);
  });

  test('a car the sim has not placed reads the placeholder, not 0 (#931)', () => {
    // The grid before the green flag and a practice session before anyone has a time: SimHub
    // reports a zero, and the card drew `0 / 24` where every other position reads `--`.
    expect(read(0).value).toBe(NO_VALUE);
    expect(read(null).value).toBe(NO_VALUE);
    // The placeholder is two digit cells, so the count stands where it does after a two-digit place.
    expect(read(0).left).toBe(read(12).left);
    expect(read(null).left).toBe(read(12).left);
  });
});
