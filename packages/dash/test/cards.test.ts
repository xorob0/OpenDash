/** Every card draws inside its slot at every rung, with unique prefixed names and the catalogue label. */
import { describe, expect, test } from 'bun:test';
import { CARDS } from '../src/cards/index.ts';
import { contains, rect } from '../src/design/geometry.ts';
import { rungFor } from '../src/design/rung.ts';
import { walkItems } from '../src/walk.ts';

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
      const values = items.filter((i) => i.kind === 'text' && i.name !== `${card.id}.label` && !i.name.endsWith('denominator') && !i.name.endsWith('unit'));
      expect(values.length).toBeGreaterThan(0);
      for (const v of values) if (v.kind === 'text') expect(v.monospace).toBeDefined();
    }
  });

  test('rung L readouts sit on the canvas line boxes', () => {
    const items = CARDS[1]!.build(rect(0, 0, 255, 187), 'x.');
    const [label, value] = items;
    if (label?.kind !== 'text' || value?.kind !== 'text') throw new Error('readout');
    expect(label.rect).toEqual({ left: 16, top: 51, width: 223, height: 18 });
    expect(value.rect).toEqual({ left: 16, top: 65, width: 208, height: 77 });
    expect(value.fontSize).toBe(64);
    const grid = CARDS[10]!.build(rect(0, 0, 255, 187), 'x.');
    const rects = grid.map((i) => ('rect' in i ? [i.rect.left, i.rect.top] : null));
    expect(rects).toEqual([[16, 35], [16, 50], [136, 50], [16, 100], [136, 100]]);
    const row = CARDS[4]!.build(rect(0, 0, 255, 187), 'x.');
    const denominator = row[2];
    if (denominator?.kind !== 'text') throw new Error('denominator');
    expect(denominator.rect).toEqual({ left: 53, top: 83, width: 157, height: 55 });
    expect(denominator.fontSize).toBe(46);
    expect(denominator.monospace).toBeUndefined();
  });
});
