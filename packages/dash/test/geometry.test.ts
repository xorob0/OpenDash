/** Slot geometry of the 1920 x 480 layout and the rect helpers behind it. */
import { describe, expect, test } from 'bun:test';
import { gearSpeed } from '../src/components/gearSpeed.ts';
import { contains, grid, gridRules, inset, overlaps, rect, right, snapEdges } from '../src/design/geometry.ts';
import { gearCells } from '../src/design/metrics.ts';
import { layout1920x480 } from '../src/layouts/1920x480.ts';
import { LAYOUTS } from '../src/layouts/index.ts';

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
      expect(overlaps(s, hero.column)).toBe(false);
      expect(overlaps(s, hero.flagStrip)).toBe(false);
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
    expect(hero.revBar).toEqual({ left: 24, top: 12, width: 1872, height: 40, gap: 8 });
    expect(hero.column).toEqual({ left: 769, top: 65, width: 382, height: 375 });
    expect(hero.pitLimiter).toEqual({ left: 824, top: 75, width: 272, height: 36 });
    expect(hero.flagStrip).toEqual({ left: 0, top: 440, width: 1920, height: 40 });
    expect(layout1920x480.rung).toBe('L');
    expect(LAYOUTS).toContain(layout1920x480);
  });

  test('gear and speed block is centred in the hero column, the gear in a cell that holds "N"', () => {
    const { column } = layout1920x480.hero;
    const [gear, speed, speedUnit] = gearSpeed(column);
    if (gear?.kind !== 'text' || speed?.kind !== 'text' || speedUnit?.kind !== 'text') throw new Error('gearSpeed returns three text items');
    // Barlow Condensed Bold "N" advances 0.514 em = 133.6 px at 260; the face's digit cell (127) clips it.
    expect(gear.rect).toEqual({ left: 801, top: 97, width: 135, height: 312 });
    expect(gear.monospace).toEqual(gearCells(260));
    expect(gear.rect.width).toBeGreaterThanOrEqual(Math.ceil(0.514 * 260));
    expect(speed.rect).toEqual({ left: 960, top: 173, width: 159, height: 139 });
    expect(speedUnit.rect).toEqual({ left: 960, top: 306, width: 159, height: 16 });
    // 135 + 24 + 159 = 318 in a 382 column: 32 px either side.
    expect(speed.rect.left - right(gear.rect)).toBe(24);
    expect(gear.rect.left - column.left).toBe(32);
    expect(right(column) - right(speed.rect)).toBe(32);
    for (const item of [gear, speed, speedUnit]) expect(contains(column, item.rect)).toBe(true);
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
