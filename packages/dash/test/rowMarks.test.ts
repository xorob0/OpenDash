/**
 * A car in the pit lane is marked on the relative's row and the leaderboard's (#388).
 *
 * The two lists are the pages a driver glances at most, and a car in the lane used to draw there as a
 * dimmer row with its gap still counting, which reads as a car on track. The pit wall's boards said it
 * in their pit column, as an inverted PIT chip; the lists have no pit column, and no room for one at
 * the narrow faces, so they say it in the gap: the gap is the one column every shape keeps, and it is
 * wide enough for the chip at every density, so the chip stands in it everywhere and no column is
 * added anywhere.
 *
 * What is pinned here is where the chip is drawn (inside the gap's own column, at every box the build
 * hands either page), what it holds (`PIT`, measured inside its box in the face it is drawn in), and
 * when it shows: a car in the lane that is not the player's, with the gap hidden for exactly as long.
 * The player's own row is left alone, the limiter banner already saying it, and a board with a pit
 * column keeps its gap and says it in that column instead.
 *
 * Off track is not marked, and `pitMarkOf` in `second/table.ts` says why: SimHub publishes nothing an
 * expression can read that says another car is off the track.
 */
import { describe, expect, test } from 'bun:test';
import { measureText } from '../src/design/advances.ts';
import { leaderboardPlan } from '../src/modules/leaderboard.ts';
import { MODULES } from '../src/modules/index.ts';
import { relativePlan } from '../src/modules/relative.ts';
import { portraitPage, racePage, towerPage } from '../src/screens/pitwall.ts';
import { columnSpans, type ListPlan } from '../src/second/table.ts';
import { walkItems } from '../src/walk.ts';
import type { Item, Rect, RectangleItem, TextItem } from '../src/generator.ts';
import type { Density } from '../src/second/density.ts';
import { evalNcalc } from './ncalcEval.ts';
import { moduleBoxes } from './secondScreens.test.ts';

const PAGES: [string, (ctx: { frame: Rect; density: Density; prefix: string; page: string }) => ListPlan][] = [
  ['relative', relativePlan],
  ['leaderboard', leaderboardPlan],
];

const flat = (items: readonly Item[]): Item[] => items.flatMap((i) => [...walkItems([i])]);

const build = (id: string, box: { frame: Rect; density: Density }): Item[] =>
  flat(MODULES.find((m) => m.id === id)!.build({ frame: box.frame, density: box.density, prefix: '' }));

const named = (items: readonly Item[], suffix: string): Item | undefined => items.find((i) => i.name.endsWith(suffix));

/** A binding's formula, or undefined where the target is not bound. */
const formulaOf = (item: Item, target: 'Visible'): string | undefined => {
  const binding = item.bindings?.[target];
  if (binding === undefined || binding.mode !== 'formula') return undefined;
  return typeof binding.formula === 'string' ? binding.formula : binding.formula.expression;
};

/**
 * Whether an item shows on a row whose car is, or is not, in the lane and the player's.
 *
 * Every per-car read in a row goes through the row's index expression, which the stubs below answer
 * with one row whatever it asks; the two reads that decide the mark answer the case. An item with no
 * Visible of its own shows.
 */
function shows(item: Item, car: { inPit: boolean; isPlayer: boolean }): boolean {
  const formula = formulaOf(item, 'Visible');
  if (formula === undefined) return true;
  const row = (): number => 3;
  return Boolean(
    evalNcalc(formula, {}, {
      repeatindex: () => 1,
      getopponentleaderboardposition_aheadbehind: row,
      getopponentleaderboardposition_aheadbehind_playerclassonly: row,
      getopponentleaderboardposition_playerclassonly: row,
      driveriscarinpitlane: () => car.inPit,
      driverisplayer: () => car.isPlayer,
    }),
  );
}

const CASES = [
  { inPit: false, isPlayer: false, marked: false },
  { inPit: true, isPlayer: false, marked: true },
  // The player's own row: the limiter banner says it, and the relative's own gap is its zero.
  { inPit: true, isPlayer: true, marked: false },
  { inPit: false, isPlayer: true, marked: false },
];

describe('a car in the pit lane gives its gap to the PIT chip', () => {
  test('and the boxes include zone B of the 850 x 480 face, in both arrangements', () => {
    // The face the yellow scenario is watched on, its two cars in the pits.
    const names = moduleBoxes().map((box) => box.name);
    expect(names).toContain('face-274x328');
    expect(names).toContain('face-274x366');
  });

  for (const box of moduleBoxes()) {
    test(`inside the gap's own column, on a ${box.name}`, () => {
      for (const [id, planOf] of PAGES) {
        const plan = planOf({ frame: box.frame, density: box.density, prefix: '', page: id });
        const gap = columnSpans(plan.columns, box.frame, box.density, plan.rowType).find((span) => span.id === 'gap')!;
        const items = build(id, box);
        const block = named(items, '.row.gap.pit.block') as RectangleItem | undefined;
        const text = named(items, '.row.gap.pit.text') as TextItem | undefined;
        expect({ id, box: box.name, block: block !== undefined, text: text !== undefined }).toEqual({ id, box: box.name, block: true, text: true });
        if (!block || !text) continue;
        // The chip's block stands in the gap column, at its right edge where the gap's figures end.
        const right = block.rect.left + block.rect.width;
        expect({ id, box: box.name, block: block.rect, gap, inside: block.rect.left >= gap.left && right <= gap.left + gap.width, flush: right === gap.left + gap.width }).toMatchObject({ inside: true, flush: true });
        // And the word inside its box, strictly, as `textFit.test.ts` asks: WPF clips at the edge.
        const width = measureText('BarlowMedium', text.text, text.fontSize);
        expect({ id, box: box.name, text: text.text, width, room: text.rect.width, fits: width < text.rect.width }).toMatchObject({ text: 'PIT', fits: true });
      }
    });
  }

  test('only while the car is in the lane and is not the player', () => {
    for (const box of moduleBoxes().filter((b) => b.name === 'face-274x328' || b.name.endsWith(' page'))) {
      for (const [id] of PAGES) {
        const items = build(id, box);
        const gap = named(items, '.row.gap')!;
        const chip = [named(items, '.row.gap.pit.block')!, named(items, '.row.gap.pit.text')!];
        for (const { marked, ...car } of CASES) {
          const at = { id, box: box.name, ...car };
          expect({ ...at, chip: chip.map((item) => shows(item, car)), gap: shows(gap, car) }).toEqual({ ...at, chip: [marked, marked], gap: !marked });
        }
      }
    }
  });
});

describe('a board with a pit column keeps its gap', () => {
  for (const [page, screen] of [['race', racePage(1920, 1080)], ['tower', towerPage(1920, 1080)], ['portrait', portraitPage(1080, 1920)]] as const) {
    test(`on the ${page} board`, () => {
      const items = flat(screen.items).filter((i) => i.name.includes(`${page}.board.`));
      expect({ page, marks: items.filter((i) => i.name.includes('.gap.pit')).map((i) => i.name) }).toEqual({ page, marks: [] });
      // The column that says it there, shown for a car in the lane, the player's own included: an
      // engineer reads the board for the player's car as much as for anyone's.
      const chip = named(items, '.row.pitChip.block')!;
      expect({ page, chip: shows(chip, { inPit: true, isPlayer: true }) }).toEqual({ page, chip: true });
    });
  }
});
