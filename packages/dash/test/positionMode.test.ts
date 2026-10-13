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
import { DEFAULTS, secondScreen, setting } from '../src/contract.ts';
import { rect } from '../src/design/geometry.ts';
import { BAND_PAGES } from '../src/zones/bandPages.ts';
import { MODULES } from '../src/modules/index.ts';
import { racePage } from '../src/screens/pitwall.ts';
import { sectorColour } from '../src/second/sectors.ts';
import { carIsSessionBest, carPosition, fieldSize, player, rowsInClass, sessionBestLap, sessionBestRow } from '../src/second/values.ts';
import { ds } from '../src/tokens.ts';
import { BAR_FIELD_SPECS } from '../src/zones/bar.ts';
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
const PLAYER_CLASS = GRID[PLAYER_ROW - 1]!;

/** The leaderboard rows of a class, in order. */
const rowsOfClass = (klass: string): number[] => GRID.flatMap((c, i) => (c === klass ? [i + 1] : []));

/** The leaderboard rows of the player's own class, in order: 1, 3 and 6. */
const CLASS_ROWS = rowsOfClass(PLAYER_CLASS);

const onGrid = (row: number): boolean => row >= 1 && row <= GRID.length;

/** The position of a row within its own class, counting from one. */
const classPositionOf = (row: number): number => GRID.slice(0, row).filter((c) => c === GRID[row - 1]).length;

/**
 * Where each row started, overall, which is what a places-gained reading counts from.
 *
 * Chosen so that the player's own two readings differ. Row 3 started sixth overall and runs third,
 * three places gained; within its own class it started last of the three and runs second, one place
 * gained. Were the grid's starting order its running order, or the player's class contiguous, the
 * assertion below would pass on a ± column reading either field.
 */
const START = [2, 1, 6, 4, 5, 3] as const;

/** The place a row started in within its own class: its class ordered by where each of them started. */
const classStartOf = (row: number): number => [...rowsOfClass(GRID[row - 1]!)].sort((a, b) => START[a - 1]! - START[b - 1]!).indexOf(row) + 1;

/**
 * Each row's best lap, in seconds, chosen so that the field's fastest and the player's class's
 * fastest are different cars: an LMP2 on row 2 holds the session best, and the fastest GT3 is row 6,
 * last of its class on the road. A session best that read the field would put the purple on row 2,
 * which a board of GT3s does not draw at all.
 */
const BEST = [99.1, 97.2, 98.9, 100.4, 97.6, 98.4] as const;

/** The leaderboard row holding the fastest lap among some rows, 1-based. */
const fastestOf = (rows: readonly number[]): number => rows.reduce((a, b) => (BEST[b - 1]! < BEST[a - 1]! ? b : a));

const FIELD_BEST_ROW = fastestOf(GRID.map((_, i) => i + 1));
const CLASS_BEST_ROW = fastestOf(CLASS_ROWS);

/**
 * The best time through sector 1, of the field and of the player's class, in seconds. A TimeSpan in
 * SimHub; the evaluator's `timespantoseconds` is the identity, so a number stands in for one.
 */
const BEST_S1 = { field: 27.9, class: 28.4 } as const;

/** What the plugin publishes for one run of the evaluator. */
interface Settings {
  /** Absent is a package drawn with no plugin attached, which falls back to the contract's default. */
  positionMode: 'overall' | 'class' | undefined;
  /** The pit wall's own class filter, which is a zone filter of the kind a face carries per zone. */
  screenFilter?: boolean;
  /**
   * The leaderboard index (0-based, as SimHub publishes it) of the field's and of the class's best
   * lap. Unset, they are the ones {@link BEST} makes; a single-class race sets them equal.
   */
  bestIndex?: { field: number; class: number };
  /** The player's last sector 1, in seconds. */
  lastS1?: number;
  /**
   * Whether the plugin is attached to publish the class best. It is unless a test says otherwise,
   * and without it the class reading falls back on the row.
   */
  plugin?: boolean;
  /**
   * A frame the dashboard renders while SimHub is still building the next one: the published
   * properties hold the last finished frame, and every `driver*` function, which looks in the frame
   * being built, finds no leaderboard and answers nothing. #454.
   */
  midBuild?: boolean;
}

/** SimHub's leaderboard functions, answered from {@link GRID}. A row that is not there is null. */
function scopeFor(settings: Settings, repeatIndex: number): Record<string, unknown> {
  const bestIndex = settings.bestIndex ?? { field: FIELD_BEST_ROW - 1, class: CLASS_BEST_ROW - 1 };
  const properties: Record<string, unknown> = {
    'OpenDash.PositionMode': settings.positionMode,
    'DataCorePlugin.GameData.OpponentsCount': GRID.length,
    'DataCorePlugin.GameData.PlayerClassOpponentsCount': CLASS_ROWS.length,
    ...(settings.screenFilter === undefined ? {} : { 'OpenDash.PitWallClassOnly': settings.screenFilter }),
    'DataCorePlugin.GameData.BestLapOpponentPosition': bestIndex.field,
    'DataCorePlugin.GameData.BestLapOpponentSameClassPosition': bestIndex.class,
    // The two times, published from the frame SimHub finished: SimHub's own copy of the field's
    // best car, and the plugin's copy of the class's, which SimHub does not publish. Null for nobody.
    'DataCorePlugin.GameData.BestLapOpponent.BestLapTime': BEST[bestIndex.field] ?? null,
    ...(settings.plugin === false ? {} : { 'OpenDash.ClassBestLap': BEST[bestIndex.class] ?? null }),
    'DataCorePlugin.GameData.Sector1LastLapTime': settings.lastS1 ?? null,
    // The player's own best of the sector, slower than either session best so that only the
    // purple branch can tell the two references apart.
    'DataCorePlugin.GameData.Sector1BestTime': 29.0,
  };
  const classRowAt = (place: number): number => CLASS_ROWS[place - 1] ?? -1;
  return {
    P: (name: string): unknown => properties[name],
    // By arity rather than by whether a fallback came back undefined: `isnull(x, driverbestlap(-1))`
    // is the two-argument form even on a frame where its fallback finds no car.
    nz: (...args: unknown[]): unknown => (args.length < 2 ? args[0] === undefined || args[0] === null : (args[0] ?? args[1])),
    IF: (condition: unknown, whenTrue: unknown, whenFalse: unknown): unknown => (condition ? whenTrue : whenFalse),
    FMT: (value: number, pattern: string): string => {
      if (pattern !== '0') throw new Error(`the evaluator knows one format pattern, not ${pattern}`);
      return String(Math.round(value));
    },
    abs: Math.abs,
    repeatindex: (): number => repeatIndex,
    getplayerleaderboardposition: (): number => PLAYER_ROW,
    driverposition: (row: number): number | undefined => (onGrid(row) ? row : undefined),
    driverclassposition: (row: number): number | undefined => (onGrid(row) ? classPositionOf(row) : undefined),
    drivercarclass: (row: number): string | undefined => (onGrid(row) ? GRID[row - 1] : undefined),
    // SimHub's three-argument `left(value, start, count)`, and `ucase`: the cut a class chip makes.
    left: (value: unknown, start: number, count: number): string => String(value).slice(start, start + count),
    ucase: (value: unknown): string => String(value).toUpperCase(),
    driverpositiongain: (row: number): number | undefined => (onGrid(row) ? START[row - 1]! - row : undefined),
    driverpositiongainclass: (row: number): number | undefined => (onGrid(row) ? classStartOf(row) - classPositionOf(row) : undefined),
    driveravailable: (row: number): boolean | undefined => (onGrid(row) ? true : undefined),
    driverbestlap: (row: number): number | undefined => (onGrid(row) && !settings.midBuild ? BEST[row - 1] : undefined),
    driveriscarinpitlane: (row: number): boolean | undefined => (onGrid(row) ? false : undefined),
    driverisplayer: (row: number): boolean | undefined => (onGrid(row) ? row === PLAYER_ROW : undefined),
    timespantoseconds: (value: number): number => value,
    getbestsplittime: (sector: number): number | null => (sector === 1 ? BEST_S1.field : null),
    getbestsplittime_playerclassonly: (sector: number): number | null => (sector === 1 ? BEST_S1.class : null),
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
  const known = ['nz(', 'if(', 'format(', 'abs(', 'left(', 'ucase(', 'repeatindex(', 'driverposition(', 'driverclassposition(', 'drivercarclass(', 'driverpositiongain(', 'driverpositiongainclass(',
    'driveravailable(', 'getplayerleaderboardposition(', 'driverbestlap(', 'driveriscarinpitlane(', 'driverisplayer(', 'timespantoseconds(',
    'getbestsplittime(', 'getbestsplittime_playerclassonly(',
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

  test('the rank column counts the places the position column counts', () => {
    // A ± is the movement of a place, so the field it counts is the field the place beside it is
    // counted in. The player has gained three overall since the start and one within its own class,
    // that class having started in a different order; a cell reading the first beside a column
    // showing the second draws P2 with three places gained against it, which is this ticket a
    // column to the right. The race board is the one page that heads the column.
    //
    // Counting in class, this field of three classes draws no count at all. SimHub's class count
    // starts from a class place it numbered before the sim placed the car, and nothing it publishes
    // says where a car first stood in its class (#1022), so the cell draws neither the class count
    // nor the overall one in its place.
    const items = flat(racePage(1920, 1080).items);
    const count = items.find((i) => i.name.endsWith('.row.rank.count'))!;
    const marks = items.filter((i) => /\.row\.rank\.(up|down|flat)$/.test(i.name));
    const drawn = (settings: Settings, row: number): unknown => evaluate(formulaOf(count, 'Text'), settings, row);
    const shown = (settings: Settings, row: number): unknown[] => [count, ...marks].map((i) => evaluate(formulaOf(i, 'Visible'), settings, row));
    expect({
      overall: drawn({ positionMode: 'overall' }, PLAYER_ROW),
      inClass: drawn({ positionMode: 'class' }, classPositionOf(PLAYER_ROW)),
    }).toEqual({ overall: '3', inClass: '' });
    expect(shown({ positionMode: 'class' }, classPositionOf(PLAYER_ROW))).toEqual([false, false, false, false]);
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

describe('the rig-wide mode is one property', () => {
  test('read by one expression, which the position of every row goes through', () => {
    // `cards/position.ts` draws the player's own place and the field it is out of, which is one car
    // and not a list: there are no rows to filter, so class mode there is the readout it always was,
    // and it keeps a predicate of its own that the card snapshots pin. What is asserted here is the
    // list side: one property, read through one expression, which is what every row's place goes
    // through.
    expect(setting.positionMode()).toContain('OpenDash.PositionMode');
    expect(carPosition(player())).toContain(setting.positionMode());
  });
});

describe('the session best is the fastest car of the field the rig counts in (#433)', () => {
  const OVERALL: Settings = { positionMode: 'overall' };
  const CLASS: Settings = { positionMode: 'class' };

  test('the grid this block argues from holds the two bests in different classes', () => {
    // Were the field's fastest car a GT3, every assertion below would pass on the build that read
    // the whole field.
    expect({ field: GRID[FIELD_BEST_ROW - 1], klass: GRID[CLASS_BEST_ROW - 1] }).toEqual({ field: 'LMP2', klass: PLAYER_CLASS });
  });

  test("counting overall, the row and its lap are the whole field's, exactly as before", () => {
    expect({ row: evaluate(sessionBestRow(), OVERALL), lap: evaluate(sessionBestLap(), OVERALL) }).toEqual({ row: FIELD_BEST_ROW, lap: BEST[FIELD_BEST_ROW - 1] });
  });

  test("counting in class, they are the fastest car of the player's own class", () => {
    expect({ row: evaluate(sessionBestRow(), CLASS), lap: evaluate(sessionBestLap(), CLASS) }).toEqual({ row: CLASS_BEST_ROW, lap: BEST[CLASS_BEST_ROW - 1] });
  });

  test('nobody yet is nobody in either mode, not the first row', () => {
    for (const positionMode of ['overall', 'class'] as const) {
      expect({ positionMode, row: evaluate(sessionBestRow(), { positionMode, bestIndex: { field: -1, class: -1 } }) }).toEqual({ positionMode, row: -1 });
    }
  });

  test('a single-class race reads the same under either setting', () => {
    // One class, so SimHub's two properties name the same car.
    const bestIndex = { field: 4, class: 4 };
    expect(evaluate(sessionBestLap(), { positionMode: 'class', bestIndex })).toBe(evaluate(sessionBestLap(), { positionMode: 'overall', bestIndex }));
  });

  test('a frame drawn while SimHub builds the next one still draws the session best, in either mode (#454)', () => {
    // What blinked: the time was looked up with driverbestlap(), which finds no car on such a frame.
    for (const positionMode of ['overall', 'class'] as const) {
      const steady = evaluate(sessionBestLap(), { positionMode });
      expect({ positionMode, lap: evaluate(sessionBestLap(), { positionMode, midBuild: true }) }).toEqual({ positionMode, lap: steady });
    }
    expect(evaluate(sessionBestLap(), { positionMode: 'overall', midBuild: true })).toBe(BEST[FIELD_BEST_ROW - 1]);
    expect(evaluate(sessionBestLap(), { positionMode: 'class', midBuild: true })).toBe(BEST[CLASS_BEST_ROW - 1]);
  });

  test('with no plugin attached the class best falls back on the row, and nobody yet is nothing', () => {
    expect(evaluate(sessionBestLap(), { positionMode: 'class', plugin: false })).toBe(BEST[CLASS_BEST_ROW - 1]);
    for (const positionMode of ['overall', 'class'] as const) {
      // Nothing at all, whichever way the evaluator spells it; `lapTime()` draws its placeholder for either.
      expect({ positionMode, lap: evaluate(sessionBestLap(), { positionMode, bestIndex: { field: -1, class: -1 } }) ?? null }).toEqual({ positionMode, lap: null });
    }
  });

  test('every field labelled for the session best reads the one row', () => {
    // The switch sits at the row so that no field can be left reading the other one: the Lap times
    // module, the Sectors module and the pit wall's track panel all draw `sessionBestLap()`.
    const fields = [
      ...flat(build('lapTimes', PAGE.width, PAGE.height)),
      ...flat(build('sectors', PAGE.width, PAGE.height)),
      ...flat(racePage(1920, 1080).items),
    ].filter((i) => i.kind === 'text' && /(^|\.)sessionBest\.value$/.test(i.name));
    expect(fields.length).toBeGreaterThanOrEqual(3);
    for (const field of fields) expect({ name: field.name, reads: formulaOf(field, 'Text').includes(sessionBestLap()) }).toEqual({ name: field.name, reads: true });
  });

  /** The rows of a leaderboard whose `Best` cell is drawn purple, by the position each draws. */
  function purpleRows(items: readonly Item[], settings: Settings): number[] {
    const all = flat(items);
    const stamped = all.find((i) => i.kind === 'layer' && i.name.endsWith('.rows'));
    if (!stamped || stamped.kind !== 'layer') throw new Error('no stamped rows layer');
    const row = stamped.children[0]!;
    const cells = flat([row]);
    const best = cells.find((i) => i.kind === 'text' && i.name.endsWith('.row.best'));
    if (!best) throw new Error(`no Best column among ${cells.map((c) => c.name).join(', ')}`);
    const pos = cells.find((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.row.pos'))!;
    const purple: number[] = [];
    for (let i = 1; i <= (stamped.repetitions ?? 0) + 1; i++) {
      if (!evaluate(formulaOf(row, 'Visible'), settings, i)) continue;
      const colour = best.bindings?.['TextColor'];
      if (!colour || typeof colour !== 'object' || !('formula' in colour)) throw new Error('the Best cell has no colour binding');
      if (evaluate(String(colour.formula), settings, i) === ds.purpose.lap.sessionBest) purple.push(placeIn(String(evaluate(formulaOf(pos, 'Text'), settings, i)))!);
    }
    return purple;
  }

  test("a leaderboard counting overall paints the field's fastest car", () => {
    expect(purpleRows(build('leaderboard', PAGE.width, PAGE.height), OVERALL)).toEqual([FIELD_BEST_ROW]);
  });

  test('a leaderboard counting in class has a purple row, on the fastest car of that class', () => {
    // Before #433 this was empty: the purple fell on the LMP2, which a GT3 board does not draw.
    expect(purpleRows(build('leaderboard', PAGE.width, PAGE.height), CLASS)).toEqual([classPositionOf(CLASS_BEST_ROW)]);
  });

  test("a board filtered to one class by its own zone, the rig counting overall, paints that class's fastest car", () => {
    // The rows are the player's class by their overall places, so the purple has to be that class's
    // best too: the field's would fall on the LMP2, which this board does not draw, and leave it
    // with no purple row at all.
    const board = build('leaderboard', PAGE.width, PAGE.height, secondScreen.classOnly());
    const filtered: Settings = { positionMode: 'overall', screenFilter: true };
    expect(column(board, filtered)).toEqual(CLASS_ROWS);
    expect(purpleRows(board, filtered)).toEqual([CLASS_BEST_ROW]);
  });

  test('the same board with its zone filter off is the whole field again, and so is its purple', () => {
    const board = build('leaderboard', PAGE.width, PAGE.height, secondScreen.classOnly());
    expect(purpleRows(board, { positionMode: 'overall', screenFilter: false })).toEqual([FIELD_BEST_ROW]);
  });

  test('carIsSessionBest answers for exactly one row in each mode', () => {
    const rows = GRID.map((_, i) => i + 1);
    const holders = (settings: Settings): number[] => rows.filter((r) => evaluate(carIsSessionBest(String(r)), settings));
    expect({ overall: holders(OVERALL), inClass: holders(CLASS) }).toEqual({ overall: [FIELD_BEST_ROW], inClass: [CLASS_BEST_ROW] });
  });

  test("a sector equal to the class's best split is purple counting in class, and is not the field's", () => {
    const colour = (settings: Settings): unknown => evaluate(sectorColour(1), settings);
    expect(colour({ ...CLASS, lastS1: BEST_S1.class })).toBe(ds.purpose.lap.sessionBest);
    expect(colour({ ...OVERALL, lastS1: BEST_S1.class })).not.toBe(ds.purpose.lap.sessionBest);
    expect(colour({ ...OVERALL, lastS1: BEST_S1.field })).toBe(ds.purpose.lap.sessionBest);
  });
});

describe('a rig that never chose counts in class', () => {
  test('the fallback a package draws with no plugin is the class, place and count alike', () => {
    // #432: second of class in a multiclass race read P16, because the default counted the whole
    // field. The plugin's default is held to this one by contract.test.ts; this is what it draws.
    expect(DEFAULTS.PositionMode).toBe('class');
    const unset = { positionMode: undefined };
    expect({ place: evaluate(carPosition(player()), unset), of: evaluate(fieldSize(), unset) }).toEqual({
      place: classPositionOf(PLAYER_ROW),
      of: CLASS_ROWS.length,
    });
  });
});

describe("the bar's two position cells cannot contradict each other", () => {
  const cell = (id: string): { bind: Expr; of?: Expr } => {
    const spec = BAR_FIELD_SPECS.find((f) => f.id === id)!;
    return { bind: spec.bind, ...(spec.denominator ? { of: spec.denominator.bind } : {}) };
  };

  for (const positionMode of ['overall', 'class', undefined] as const) {
    test(`position reads the place the rig counts and the field it counts it in, counting ${positionMode ?? 'by default'}`, () => {
      // The bar's position cell bound Position and OpponentsCount whatever the mode said, so a rig
      // counting in class read 3 / 6 in the bar beside 2 / 3 on every other surface. It now reads the
      // same expressions the session module and the pit wall do.
      const settings = { positionMode };
      const { bind, of } = cell('position');
      const inClass = positionMode !== 'overall';
      expect({ place: evaluate(bind, settings), of: evaluate(of!, settings) }).toEqual({
        place: String(inClass ? classPositionOf(PLAYER_ROW) : PLAYER_ROW),
        of: `/ ${inClass ? CLASS_ROWS.length : GRID.length}`,
      });
    });

    test(`class position is the class whatever the rig counts, counting ${positionMode ?? 'by default'}`, () => {
      // The one cell that is always the class: what it adds is the class name, and with the rig
      // counting overall it is where the class place still reads. With the rig counting in class the
      // two cells say the same place, which is agreement rather than contradiction.
      expect(evaluate(cell('classPosition').bind, { positionMode })).toBe(`${PLAYER_CLASS} · P${classPositionOf(PLAYER_ROW)}`);
    });
  }
});
