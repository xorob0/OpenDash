/**
 * A car with no class name draws no class chip (#1013).
 *
 * The Class column drew its chip with no Visible of its own, so a car whose `CarClassShortName` was
 * empty drew the chip's grey box with nothing in it, and a session whose cars carry no class drew one
 * on every row of every leaderboard and pit wall board. What is pinned here is that the chip, box and
 * text, carries a Visible that hides it for a car whose class is empty or null and shows it for a car
 * with a class, on every board and list the build draws the column on.
 */
import { describe, expect, test } from 'bun:test';
import type { Item } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { portraitPage, racePage, towerPage } from '../src/screens/pitwall.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc } from './ncalcEval.ts';
import { moduleBoxes } from './secondScreens.test.ts';

const flat = (items: readonly Item[]): Item[] => items.flatMap((i) => [...walkItems([i])]);

/** Every class chip, block and text, the build draws: the pit wall boards and the leaderboard at every box. */
function chips(): { where: string; item: Item }[] {
  const found: { where: string; item: Item }[] = [];
  const collect = (where: string, items: readonly Item[]): void => {
    for (const item of flat(items)) if (item.name.endsWith('.class.block') || item.name.endsWith('.class.text')) found.push({ where, item });
  };
  for (const [page, screen] of [['race', racePage(1920, 1080)], ['tower', towerPage(1920, 1080)], ['portrait', portraitPage(1080, 1920)]] as const) {
    collect(`${page} board`, screen.items);
  }
  const leaderboard = MODULES.find((m) => m.id === 'leaderboard')!;
  for (const box of moduleBoxes()) collect(`leaderboard ${box.name}`, leaderboard.build({ frame: box.frame, density: box.density, prefix: '' }));
  return found;
}

/** Whether a chip item shows for the car on its row, of the class given; null is a class SimHub does not send. */
function shows(item: Item, carClass: string | null): boolean {
  const binding = item.bindings?.Visible;
  if (binding === undefined || binding.mode !== 'formula') return true;
  const formula = typeof binding.formula === 'string' ? binding.formula : binding.formula.expression;
  const row = (): number => 3;
  return Boolean(
    evalNcalc(formula, {}, {
      repeatindex: () => 1,
      getopponentleaderboardposition: row,
      getopponentleaderboardposition_playerclassonly: row,
      getopponentleaderboardposition_aheadbehind: row,
      getopponentleaderboardposition_aheadbehind_playerclassonly: row,
      drivercarclass: () => carClass,
    }),
  );
}

describe('the class chip', () => {
  const found = chips();

  test('is drawn on the three pit wall boards and on the leaderboard', () => {
    const where = new Set(found.map((c) => c.where.split(' ')[0]));
    expect([...where].sort()).toEqual(['leaderboard', 'portrait', 'race', 'tower']);
  });

  test('is hidden, box and text, for a car whose class name is empty', () => {
    for (const { where, item } of found) {
      expect({ where, name: item.name, empty: shows(item, ''), none: shows(item, null) }).toEqual({ where, name: item.name, empty: false, none: false });
    }
  });

  test('is shown for a car with a class', () => {
    for (const { where, item } of found) {
      expect({ where, name: item.name, gt3: shows(item, 'GT3'), long: shows(item, 'Ferrari 296 GT3') }).toEqual({ where, name: item.name, gt3: true, long: true });
    }
  });
});
