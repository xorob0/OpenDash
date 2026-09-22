/**
 * `PositionMode` on a page that lists other cars: that the numbers in a position column and the
 * order of the rows are answers to the same question.
 *
 * The defect #212 reports is not a wrong number but two questions answered from different fields.
 * `class` made `tablePosition` read `driverclassposition` and left the rows in overall order, so a
 * multi-class leaderboard drew three cars called P1 in an order that was not the order of any of
 * the numbers. Nothing measurable on one row catches that; it is a property of the column read
 * downwards, which is why this file evaluates the formulas the build writes rather than matching
 * them against a string.
 *
 * The evaluator below is the same trick `expressions.test.ts` uses on the dial: an NCalc formula
 * translated into JavaScript, with SimHub's leaderboard functions answered from a grid written
 * here. It is a subset and deliberately so -- `if`, `isnull`, `format`, the comparisons and the
 * opponent family are all these bindings contain -- and anything outside it throws rather than
 * evaluating to something plausible.
 */
import { describe, expect, test } from 'bun:test';
import { secondScreen, setting } from '../src/contract.ts';
import { rect } from '../src/design/geometry.ts';
import { BAND_PAGES } from '../src/zones/bandPages.ts';
import { MODULES } from '../src/modules/index.ts';
import { carPosition, player, rowsInClass } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import type { Expr } from '../src/bind.ts';
import type { Item, TextItem } from '../src/generator.ts';

/**
 * A six-car multi-class grid in leaderboard order, the player third overall and second in GT3.
 *
 * Three classes, and the player's own is neither first nor contiguous: GT3 holds overall 1, 3 and
 * 6, so a list filtered to it and a list numbered by it disagree about every row but the first.
 * Track order is leaderboard order, everyone on the same lap, which is what lets a relative's
 * window be checked against the positions it draws.
 */
const GRID = ['GT3', 'LMP2', 'GT3', 'LMP3', 'LMP2', 'GT3'] as const;
const PLAYER_ROW = 3;
const PLAYER_CLASS = GRID[PLAYER_ROW - 1];

/** The leaderboard rows of the player's own class, in order: 1, 3 and 6. */
const CLASS_ROWS = GRID.map((c, i) => (c === PLAYER_CLASS ? i + 1 : 0)).filter((row) => row > 0);

const onGrid = (row: number): boolean => row >= 1 && row <= GRID.length;

/** The position of a row within its own class, counting from one. */
const classPositionOf = (row: number): number => GRID.slice(0, row).filter((c) => c === GRID[row - 1]).length;

/** What the plugin publishes for one run of the evaluator. */
interface Settings {
  positionMode: 'overall' | 'class';
  /** The pit wall's own class filter, which is a zone filter of the kind a face carries per zone. */
  screenFilter?: boolean;
}

/** SimHub's leaderboard functions, answered from {@link GRID}. A row that is not there is null. */
function scopeFor(settings: Settings, repeatIndex: number): Record<string, unknown> {
  const properties: Record<string, unknown> = {
    'OpenDash.PositionMode': settings.positionMode,
    ...(settings.screenFilter === undefined ? {} : { 'OpenDash.PitWallClassOnly': settings.screenFilter }),
  };
  const classRowAt = (place: number): number => CLASS_ROWS[place - 1] ?? -1;
  return {
    P: (name: string): unknown => properties[name],
    nz: (value: unknown, fallback?: unknown): unknown => (fallback === undefined ? value === undefined || value === null : (value ?? fallback)),
    IF: (condition: unknown, whenTrue: unknown, whenFalse: unknown): unknown => (condition ? whenTrue : whenFalse),
    FMT: (value: number, pattern: string): string => {
      if (pattern !== '0') throw new Error(`the evaluator knows one format pattern, not ${pattern}`);
      return String(Math.round(value));
    },
    repeatindex: (): number => repeatIndex,
    getplayerleaderboardposition: (): number => PLAYER_ROW,
    driverposition: (row: number): number | undefined => (onGrid(row) ? row : undefined),
    driverclassposition: (row: number): number | undefined => (onGrid(row) ? classPositionOf(row) : undefined),
    driveravailable: (row: number): boolean | undefined => (onGrid(row) ? true : undefined),
    getopponentleaderboardposition_playerclassonly: classRowAt,
    getopponentleaderboardposition_aheadbehind: (k: number): number => (onGrid(PLAYER_ROW + k) ? PLAYER_ROW + k : -1),
    getopponentleaderboardposition_aheadbehind_playerclassonly: (k: number): number => classRowAt(classPositionOf(PLAYER_ROW) + k),
  };
}

/** The NCalc a binding carries, as JavaScript. Every operator these formulas use and no others. */
function toJavaScript(formula: string): string {
  const js = formula
    .replace(/\[([A-Za-z0-9_.]+)\]/g, "P('$1')")
    .replace(/\bisnull\(/g, 'nz(')
    .replace(/\bif\(/g, 'IF(')
    .replace(/\bformat\(/g, 'FMT(')
    .replace(/ != /g, ' !== ')
    .replace(/ = /g, ' === ')
    .replace(/ and /g, ' && ')
    .replace(/ or /g, ' || ');
  const unknown = js.replace(/'[^']*'/g, '').match(/\b[a-z][a-z0-9_]*\(/g) ?? [];
  const known = ['nz(', 'if(', 'format(', 'repeatindex(', 'driverposition(', 'driverclassposition(', 'driveravailable(', 'getplayerleaderboardposition(',
    'getopponentleaderboardposition_playerclassonly(', 'getopponentleaderboardposition_aheadbehind(', 'getopponentleaderboardposition_aheadbehind_playerclassonly('];
  for (const call of unknown) if (!known.includes(call)) throw new Error(`the evaluator does not know ${call})`);
  return js;
}

/** One formula, evaluated for one row of a repeated layer. */
function evaluate(formula: Expr, settings: Settings, repeatIndex = 1): unknown {
  const scope = scopeFor(settings, repeatIndex);
  const body = `return (${toJavaScript(formula)});`;
  return new Function(...Object.keys(scope), body)(...Object.values(scope));
}

const build = (id: string, width: number, height: number, classOnly?: Expr): Item[] =>
  MODULES.find((m) => m.id === id)!.build({ frame: rect(0, 0, width, height), density: 'companion', prefix: '', ...(classOnly ? { classOnly } : {}) });

const flat = (items: readonly Item[]): Item[] => items.flatMap((i) => [...walkItems([i])]);

const formulaOf = (item: Item, target: 'Text' | 'Visible'): Expr => {
  const binding = item.bindings?.[target];
  if (!binding || typeof binding !== 'object' || !('formula' in binding)) throw new Error(`${item.name} has no ${target} formula`);
  return String(binding.formula);
};

/** The digits of a position as a row draws it: `P4` is 4, and a row the sim has not placed is absent. */
const placeIn = (text: string): number | undefined => {
  const match = /P(\d+)/.exec(text);
  return match ? Number(match[1]) : undefined;
};

/** The companion's page box, which is the widest list the build draws and the one with no zone filter. */
const PAGE = { width: 802, height: 356 };

/**
 * The position each visible row of a list draws, top to bottom.
 *
 * A row whose car is not there is hidden by SimHub rather than drawn empty, so the column is read
 * through the same `Visible` the file carries: a class of three in a field of six leaves four rows
 * of a seven-row page unstamped, and counting them as positions would be counting the blanks.
 */
function column(items: readonly Item[], settings: Settings): number[] {
  const all = flat(items);
  const stamped = all.find((i) => i.kind === 'layer' && i.name.endsWith('.rows'));
  if (!stamped || stamped.kind !== 'layer') throw new Error('no stamped rows layer');
  const row = stamped.children[0]!;
  const cell = flat([row]).find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.row.pos'))!;
  const places: number[] = [];
  for (let i = 1; i <= (stamped.repetitions ?? 0) + 1; i++) {
    if (!evaluate(formulaOf(row, 'Visible'), settings, i)) continue;
    const place = placeIn(String(evaluate(formulaOf(cell, 'Text'), settings, i)));
    if (place !== undefined) places.push(place);
  }
  return places;
}

/** Whether a run of numbers counts up one at a time, which is what a leaderboard's column does. */
const consecutive = (places: readonly number[]): boolean => places.length > 1 && places.every((p, i) => i === 0 || p === places[i - 1]! + 1);

describe('a list numbers the field it is drawn from', () => {
  test('the grid this file argues from is genuinely multi-class', () => {
    // Three classes, the player's holding three of the six rows and not the first three: were the
    // grid single-class or the player's class contiguous from the top, every assertion below would
    // pass on the broken build as well.
    expect({ classes: new Set(GRID).size, classRows: CLASS_ROWS, player: PLAYER_ROW }).toEqual({ classes: 3, classRows: [1, 3, 6], player: 3 });
  });

  for (const page of ['leaderboard', 'relative'] as const) {
    test(`${page}: counting overall lists the whole field in overall order`, () => {
      const places = column(build(page, PAGE.width, PAGE.height), { positionMode: 'overall' });
      expect({ page, places, consecutive: consecutive(places) }).toMatchObject({ consecutive: true });
      expect(places).toContain(PLAYER_ROW);
    });

    test(`${page}: counting in class lists the player's class, and the numbers are that list's own`, () => {
      const places = column(build(page, PAGE.width, PAGE.height), { positionMode: 'class' });
      // The whole of the defect: the numbers run 1, 2, 3 because the rows do. Before #212 this page
      // drew the overall field and this column read 1, 1, 2, 1, 2, 3.
      expect({ page, places }).toMatchObject({ places: [1, 2, 3] });
      expect(consecutive(places)).toBe(true);
    });
  }

  test('a leaderboard counting in class draws only the cars it is counting', () => {
    const overall = column(build('leaderboard', PAGE.width, PAGE.height), { positionMode: 'overall' });
    const inClass = column(build('leaderboard', PAGE.width, PAGE.height), { positionMode: 'class' });
    expect({ overall: overall.length, inClass: inClass.length }).toEqual({ overall: GRID.length, inClass: CLASS_ROWS.length });
  });

  test("a zone's own filter still counts overall, which is the other question and is deliberate", () => {
    // One class numbered by its overall places is a legitimate thing to want on a multi-class grid,
    // and is what a zone filter with the rig left on `overall` means. The column is therefore the
    // overall positions of the player's class -- 1, 3, 6 -- which climbs without being consecutive.
    const places = column(build('leaderboard', PAGE.width, PAGE.height, secondScreen.classOnly()), { positionMode: 'overall', screenFilter: true });
    expect(places).toEqual(CLASS_ROWS);
    expect(consecutive(places)).toBe(false);
  });

  test('a filter and the rig agreeing is the same list as either of them alone', () => {
    const both = column(build('leaderboard', PAGE.width, PAGE.height, secondScreen.classOnly()), { positionMode: 'class', screenFilter: true });
    expect(both).toEqual([1, 2, 3]);
  });

  test('the gap column counts from the leader of the list, asking the rows their own question', () => {
    // A cell measured against a car above the row has to be measured against a car the list draws.
    // A class board whose gap counted from the race leader reads `+1L` on every row of a class that
    // is a lap down on the race and `Lead` on none of them, which is the column saying nothing at
    // all; `pitwallValues.test.ts` reads the two values row by row against a board built for it.
    // What belongs here is that the page asks the one question, so that the rows and the number
    // measured against them cannot come apart.
    const gap = flat(build('leaderboard', PAGE.width, PAGE.height)).find((i) => i.name === 'table.row.gap')!;
    const formula = formulaOf(gap, 'Text');
    expect(formula).toStartWith(`if(${rowsInClass()}, `);
    expect(formula).toContain('getopponentleaderboardposition_playerclassonly(1)');
  });
});

describe('the pages that list two cars straddle the player', () => {
  /** The player's own position, which is what a heading beside it has to agree with. */
  const own = (settings: Settings): number => Number(evaluate(carPosition(player()), settings));

  for (const positionMode of ['overall', 'class'] as const) {
    test(`the opponents page heads its two blocks P${'{own-1}'} and P${'{own+1}'}, counting ${positionMode}`, () => {
      const items = flat(build('opponents', PAGE.width, PAGE.height));
      const heading = (side: string): number => {
        const item = items.find((i) => i.name === `${side}.heading`)!;
        return placeIn(String(evaluate(formulaOf(item, 'Text'), { positionMode })))!;
      };
      expect({ positionMode, ahead: heading('ahead'), behind: heading('behind') }).toEqual({ positionMode, ahead: own({ positionMode }) - 1, behind: own({ positionMode }) + 1 });
    });

    test(`band D's relative reads three consecutive places, counting ${positionMode}`, () => {
      const places = BAND_PAGES.relative!.map((field) => placeIn(String(evaluate(field.labelBind!, { positionMode }))));
      expect({ positionMode, places }).toMatchObject({ places: [own({ positionMode }) - 1, own({ positionMode }), own({ positionMode }) + 1] });
    });
  }
});

describe('the reading a card draws answers the same setting', () => {
  test('the rig-wide mode is one property, read by one expression', () => {
    // `cards/position.ts` draws the player's own place and the field it is out of, which is one car
    // and not a list: there are no rows to filter, so class mode there is the readout it always was.
    // What matters is that it is the same question, asked of the same property.
    expect(setting.positionMode()).toContain('OpenDash.PositionMode');
    expect(carPosition(player())).toContain(setting.positionMode());
  });
});
