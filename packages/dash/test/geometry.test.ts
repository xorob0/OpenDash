/** Slot geometry of the 1920 x 480 layout and the rect helpers behind it. */
import { describe, expect, test } from 'bun:test';
import { gear } from '../src/components/gear.ts';
import { contains, grid, gridRules, inset, overlaps, rect, right, snapEdges } from '../src/design/geometry.ts';
import { gearCells } from '../src/design/metrics.ts';
import { layout1920x480 } from '../src/layouts/1920x480.ts';
import { LAYOUTS, rungOf } from '../src/layouts/index.ts';

const canvas = rect(0, 0, 1920, 480);

describe('1920x480 slots', () => {
  test('12 slots of 255 x 187, none overlapping, all inside the canvas', () => {
    const slots = layout1920x480.slots;
    expect(slots).toHaveLength(12);
    for (const s of slots) {
      expect(s.width).toBe(255);
      expect(s.height).toBe(187);
      expect(contains(canvas, s)).toBe(true);
    }
    for (let i = 0; i < slots.length; i++) for (let j = i + 1; j < slots.length; j++) expect(overlaps(slots[i]!, slots[j]!)).toBe(false);
  });

  test('slot order is left grid row-major then right grid row-major', () => {
    const lefts = layout1920x480.slots.map((s) => [s.left, s.top]);
    expect(lefts).toEqual([
      [1, 65], [257, 65], [513, 65], [1, 253], [257, 253], [513, 253],
      [1152, 65], [1408, 65], [1664, 65], [1152, 253], [1408, 253], [1664, 253],
    ]);
  });

  test('slots stay clear of the hero column, the header and the flag strip', () => {
    const { hero } = layout1920x480;
    for (const s of layout1920x480.slots) {
      expect(overlaps(s, hero.gear.rect)).toBe(false);
      if (hero.flags.kind !== 'flagStrip') throw new Error('1920x480 has a flag strip');
      expect(overlaps(s, hero.flags.rect)).toBe(false);
      expect(s.top).toBeGreaterThanOrEqual(65);
      expect(s.top + s.height).toBeLessThanOrEqual(440);
    }
  });

  test('rules sit between the slots and around the hero', () => {
    const rules = Object.fromEntries(layout1920x480.rules.map((r) => [r.name, [r.rect.left, r.rect.top, r.rect.width, r.rect.height]]));
    expect(rules['rule.header']).toEqual([0, 64, 1920, 1]);
    expect(rules['rule.left.01']).toEqual([256, 65, 1, 375]);
    expect(rules['rule.left.02']).toEqual([512, 65, 1, 375]);
    expect(rules['rule.left.03']).toEqual([1, 252, 767, 1]);
    expect(rules['rule.hero.left']).toEqual([768, 65, 1, 375]);
    expect(rules['rule.hero.right']).toEqual([1151, 65, 1, 375]);
    expect(rules['rule.right.01']).toEqual([1407, 65, 1, 375]);
    expect(rules['rule.right.02']).toEqual([1663, 65, 1, 375]);
    expect(rules['rule.right.03']).toEqual([1152, 252, 767, 1]);
    for (const r of layout1920x480.rules) for (const s of layout1920x480.slots) expect(overlaps(r.rect, s)).toBe(false);
  });

  test('hero geometry matches the canvas', () => {
    const { hero } = layout1920x480;
    expect(hero.rev).toEqual({ kind: 'revBar', left: 24, top: 12, width: 1872, height: 40, gap: 8 });
    expect(hero.gear).toEqual({ rect: { left: 769, top: 65, width: 382, height: 375 } });
    expect(hero.pitLimiter).toEqual({ left: 824, top: 75, width: 272, height: 36 });
    expect(hero.flags).toEqual({ kind: 'flagStrip', rect: { left: 0, top: 440, width: 1920, height: 40 } });
    expect(layout1920x480.shape).toBe('rect');
    // The zone face took the plain "openDash" in XOR-118, so the card reference face says so.
    expect(layout1920x480.folder).toBe('openDash slots 1920x480');
    expect(layout1920x480.description).toBe('1920 x 480, 12 slots');
    expect(rungOf(layout1920x480)).toBe('L');
    expect(LAYOUTS).toContain(layout1920x480);
  });

  test('the gear is centred alone in the hero column, in a cell that holds "N"', () => {
    const column = layout1920x480.hero.gear.rect;
    const items = gear(column);
    expect(items).toHaveLength(1);
    const [gearItem] = items;
    if (gearItem?.kind !== 'text') throw new Error('gear returns one text item');
    // Barlow Condensed Bold "N" advances 0.514 em = 133.6 px at 260; the face's digit cell (127) clips it.
    expect(gearItem.monospace?.charWidth ?? 0).toBeGreaterThanOrEqual(Math.ceil(0.514 * 260));
    expect(gearItem.monospace).toEqual(gearCells(260));
    // The glyph cell, not the box, is what is centred: the box carries transparent slack to its right.
    const cell = gearItem.monospace?.charWidth ?? 0;
    const leftAir = gearItem.rect.left - column.left;
    const rightAir = right(column) - (gearItem.rect.left + cell);
    expect(Math.abs(leftAir - rightAir)).toBeLessThanOrEqual(1);
    expect(contains(column, gearItem.rect)).toBe(true);
  });
});

describe('geometry helpers', () => {
  test('snapEdges keeps every gap exact and ends at left + width', () => {
    const spans = snapEdges(24, 1872, 15, 8);
    expect(spans).toHaveLength(15);
    expect(spans[0]!.left).toBe(24);
    expect(spans[14]!.left + spans[14]!.width).toBe(24 + 1872);
    for (let k = 1; k < spans.length; k++) expect(spans[k]!.left - (spans[k - 1]!.left + spans[k - 1]!.width)).toBe(8);
    for (const s of spans) expect(Number.isInteger(s.left) && Number.isInteger(s.width)).toBe(true);
    expect(snapEdges(0, 100, 0, 5)).toEqual([]);
  });

  test('grid and gridRules agree', () => {
    const cells = grid({ left: 1, top: 65 }, 3, 2, { width: 255, height: 187 }, 1);
    const rules = gridRules({ left: 1, top: 65 }, 3, 2, { width: 255, height: 187 }, 1);
    expect(cells).toHaveLength(6);
    expect(rules).toHaveLength(3);
    for (const c of cells) for (const r of rules) expect(overlaps(c, r)).toBe(false);
  });

  test('inset and contains', () => {
    const inner = inset(rect(0, 0, 255, 187), 12, 16);
    expect(inner).toEqual({ left: 16, top: 12, width: 223, height: 163 });
    expect(contains(rect(0, 0, 255, 187), inner)).toBe(true);
    expect(contains(inner, rect(0, 0, 255, 187))).toBe(false);
  });
});
