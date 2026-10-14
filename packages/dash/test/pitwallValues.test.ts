/**
 * What the pit wall's values read, and whether the budget they are measured by holds them.
 *
 * The two halves belong together. A value is drawn in a monospace cell cut from `CHARS`, so a
 * format and a budget that disagree are not two faults but one: the sector cell was budgeted for
 * five digits and one separator while its formatter could emit six and two, and WPF answered by
 * cutting the last glyph off every Nordschleife sector. The tests below therefore evaluate the
 * expressions rather than spelling them a second time, and measure the widest thing each one can
 * produce against the budget it is drawn in.
 */
import { describe, expect, test } from 'bun:test';
import { ncalc } from '../src/generator.ts';
import { cells, monoWidth, MINUS, type Chars } from '../src/design/metrics.ts';
import { DENSITIES, densityOf, type Density } from '../src/second/density.ts';
import { CHARS, carClassInterval, carClassRaceGap, carInterval, carRaceGap, carSector, sectorTime } from '../src/second/values.ts';

const { num, repeatIndex } = ncalc;

/** One car of the leaderboard the expressions are evaluated against. */
interface Car {
  position: number;
  classposition: number;
  gaptoleader: number | null;
  /** SimHub's whole laps behind the race leader by distance, null on the leader's own row. */
  lapstoleader: number | null;
  /** The same behind the first car of the player's class. */
  lapstoclassleader: number | null;
  /** SimHub's laps begun and the fraction of the lap run, which is distance; null where a case gives none. */
  currentlaphighprecision: number | null;
  /** Whether this car is in the player's own class, which is what the class-only lookup answers from. */
  ours: boolean;
  /** The last lap's sectors, in seconds; `0` is a sector that was never set. */
  sectors: readonly number[];
}

const car = (fields: Partial<Car>): Car =>
  ({ position: 1, classposition: 1, gaptoleader: 0, lapstoleader: null, lapstoclassleader: null, currentlaphighprecision: null, ours: false, sectors: [0, 0, 0], ...fields });

/**
 * An NCalc formula evaluated in JavaScript against a leaderboard.
 *
 * The expression is translated rather than re-read, so that a test says what a row of the board
 * reads and not how the generator happens to spell it. Only the operators these expressions use
 * are translated; anything else reaches JavaScript unchanged and throws, which is the honest
 * outcome for a formula this evaluator does not understand.
 *
 * `positionMode` is the rig's own setting, which one cell of a class board reads: the word on the
 * row the Gap column counts from is a claim about a place and follows the places the board draws.
 * The default is the mode such a board is normally drawn under, and the cases that turn on the
 * other one say so.
 *
 * The session is a race, whose gaps are measured on the road. Outside one the two columns measure
 * best laps instead, which `gapOutsideRace.test.ts` evaluates (#1040).
 */
function evaluate(
  formula: string,
  board: readonly Car[],
  row: number,
  positionMode: 'overall' | 'class' = 'class',
  live?: Readonly<Record<string, number>>,
): unknown {
  const js = formula
    .replace(/\bdriversector[a-z]+\(/g, 'sector(')
    .replace(/\bdriver([a-z]+)\(/g, "field('$1', ")
    .replace(/\bgetopponentleaderboardposition_playerclassonly\(/g, 'classRow(')
    .replace(/\[([A-Za-z0-9_.]+)\]/g, "P('$1')")
    .replace(/\bprop\(/g, 'PROP(')
    .replace(/\brepeatindex\(\)/g, 'ROW')
    .replace(/\bif\(/g, 'iff(')
    .replace(/\bisnull\(/g, 'nz(')
    .replace(/\btimespantoseconds\(/g, 'secs(')
    .replace(/\bformat\(/g, 'fmtOf(')
    .replace(/\breplace\(/g, 'rep(')
    .replace(/\bin\(/g, 'isIn(')
    .replace(/\btruncate\(/g, 'Math.trunc(')
    .replace(/\bucase\(/g, 'upper(')
    .replace(/ = /g, ' === ')
    .replace(/ and /g, ' && ')
    .replace(/ or /g, ' || ');

  /** The leaderboard rows of the player's own class, in order, which is what the class lookup returns. */
  const ourRows = board.flatMap((c, i) => (c.ours ? [i + 1] : []));

  const helpers = {
    ROW: row,
    field: (name: keyof Car, index: number): unknown => board[index - 1]?.[name] ?? null,
    classRow: (place: number): number => ourRows[place - 1] ?? -1,
    P: (name: string): string => {
      if (name === 'DataCorePlugin.GameData.SessionTypeName') return 'Race';
      if (name !== 'OpenDash.PositionMode') throw new Error(`the evaluator publishes two properties, not ${name}`);
      return positionMode;
    },
    // The plugin's live places and rows, by the name the formula builds; none unless the case publishes some.
    PROP: (name: string): unknown => live?.[name] ?? null,
    isIn: (value: unknown, ...options: unknown[]): boolean => options.includes(value),
    upper: (value: unknown): string => String(value).toUpperCase(),
    sector: (index: number, sector: number): number | null => board[index - 1]?.sectors[sector - 1] ?? null,
    // .NET's `format(x, '0.0', true)`: the pattern's decimals, and a sign on a positive value too.
    // A null formats to nothing, as NCalc's does. NCalc's `if` takes only the branch it needs and
    // a JavaScript call takes both, so the branch this evaluator does not want has to be harmless.
    fmtOf: (value: number | null, pattern: string, addSign = false): string =>
      value === null ? '' : `${addSign && value >= 0 ? '+' : ''}${value.toFixed((pattern.split('.')[1] ?? '').length)}`,
    iff: (condition: unknown, then: unknown, otherwise: unknown): unknown => (condition ? then : otherwise),
    nz: (value: unknown, fallback?: unknown): unknown => (fallback === undefined ? value === null || value === undefined : (value ?? fallback)),
    secs: (value: unknown): number => Number(value),
    rep: (value: string, from: string, to: string): string => value.split(from).join(to),
  };
  const names = Object.keys(helpers);
  return new Function(...names, `return ${js};`)(...names.map((n) => helpers[n as keyof typeof helpers]));
}

/** Whether a string fits a budget: its own cells against the cells the budget buys. */
function fits(text: string, chars: Chars): boolean {
  const specials = [...text].filter((c) => '.,:'.includes(c)).length;
  return text.length - specials <= chars.digits && specials <= chars.specials;
}

describe('the Gap column', () => {
  // SimHub gives the first car of its leaderboard no gap unless it is the player's (#1041), so the
  // boards below leave it null, as SimHub publishes it.
  const BOARD: readonly Car[] = [
    car({ position: 1, gaptoleader: null }),
    car({ position: 2, gaptoleader: 2.64, lapstoleader: 0 }),
    car({ position: 3, gaptoleader: 142.35, lapstoleader: 1 }),
    car({ position: 4, gaptoleader: null, lapstoleader: 1 }),
  ];
  const gapOf = (row: number): unknown => evaluate(carRaceGap(repeatIndex()), BOARD, row);

  test('the leader reads Lead', () => {
    expect(gapOf(1)).toBe('Lead');
  });

  test('a car on the lead lap reads seconds, signed, to one decimal', () => {
    expect(gapOf(2)).toBe('+2.6');
  });

  test('a lapped car keeps the lap count, which seconds cannot say', () => {
    expect(gapOf(3)).toBe('+1L');
  });

  test('a row the sim gives no gap for reads the no-value placeholder', () => {
    expect(gapOf(4)).toBe('--');
  });

  test("neither column reads SimHub's combined strings or the lap counters (#1023)", () => {
    // The combined string is one string for both cases, and the dash has no say in its decimals or
    // its words: two decimals and `+1 lap`. The lap counters differ between two cars on one lap for
    // the seconds after the first of them crosses the line. Lapped is asked of the laps by distance.
    for (const formula of [carRaceGap(repeatIndex()), carClassRaceGap(repeatIndex())]) {
      expect(formula).not.toContain('combined');
      expect(formula).not.toContain('drivercurrentlap');
    }
    expect(carRaceGap(repeatIndex())).toContain('driverlapstoleader');
    expect(carClassRaceGap(repeatIndex())).toContain('driverlapstoclassleader');
  });

  test('the leader on the road reads Lead and the gaps are measured to it (#1063)', () => {
    // Row 2 has passed row 1 since the line: SimHub still has row 1 as its leader, and measures row 2
    // half a second ahead of it. The plugin has the order on the road as rows 2, 1, 3, 4. SimHub's
    // laps to the leader are counted from row 1, which leads nothing and has none of its own, and
    // row 1 has no gap either, not being the player's: its gap to itself is 0, so it reads +0.5.
    const PASSED: readonly Car[] = [
      car({ position: 1, gaptoleader: null }),
      car({ position: 2, gaptoleader: -0.5, lapstoleader: 0 }),
      car({ position: 3, gaptoleader: 142.35, lapstoleader: 1 }),
      car({ position: 4, gaptoleader: null, lapstoleader: 1 }),
    ];
    const live = {
      'OpenDash.LiveOrder1': 2, 'OpenDash.LiveOrder2': 1, 'OpenDash.LiveOrder3': 3, 'OpenDash.LiveOrder4': 4,
      'OpenDash.LivePosition1': 2, 'OpenDash.LivePosition2': 1, 'OpenDash.LivePosition3': 3, 'OpenDash.LivePosition4': 4,
    };
    const gap = (row: number): unknown => evaluate(carRaceGap(repeatIndex()), PASSED, row, 'overall', live);
    const interval = (row: number): unknown => evaluate(carInterval(repeatIndex()), PASSED, row, 'overall', live);
    expect([gap(2), gap(1), gap(3), gap(4)]).toEqual(['Lead', '+0.5', '+1L', '--']);
    expect([interval(2), interval(1)]).toEqual(['', '+0.5']);
  });

  test('the widest gap a lead-lap car can show fits the budget it is drawn in', () => {
    // A car not yet lapped is less than one lap behind, and no lap of any track the sims model
    // runs to a thousand seconds, so three digits of seconds is the ceiling.
    expect(fits('+999.9', CHARS.gap)).toBe(true);
    expect(fits(`${MINUS}999.9`, CHARS.gap)).toBe(true);
  });
});

/**
 * The same two columns on a board filtered to the player's own class, which is what a rig counting
 * in class draws everywhere and what the pit wall's own filter has always drawn.
 *
 * A column is measured against a car above it, so the car it is measured against has to be one the
 * list draws. The board below is the case that makes both readings visible: the player's class runs
 * a lap behind the overall leader and is interleaved with another, so a gap taken to the race
 * leader lands every row of it in the lapped branch, and an interval taken between leaderboard
 * neighbours is measured against a car of another class.
 */
describe('the Gap and Int columns of a list drawn from one class', () => {
  const BOARD: readonly Car[] = [
    car({ position: 1, classposition: 1, gaptoleader: null }),
    car({ position: 2, classposition: 2, gaptoleader: 2.64, lapstoleader: 0 }),
    car({ position: 3, classposition: 1, gaptoleader: 130, lapstoleader: 1, ours: true }),
    car({ position: 4, classposition: 3, gaptoleader: 132, lapstoleader: 1, lapstoclassleader: 0 }),
    car({ position: 5, classposition: 2, gaptoleader: 134.2, lapstoleader: 1, lapstoclassleader: 0, ours: true }),
    car({ position: 6, classposition: 3, gaptoleader: 140, lapstoleader: 1, lapstoclassleader: 0, ours: true }),
    car({ position: 7, classposition: 4, gaptoleader: 260, lapstoleader: 2, lapstoclassleader: 1, ours: true }),
  ];
  /** The leaderboard rows such a list draws, top to bottom. */
  const ROWS = [3, 5, 6, 7];
  const gapOf = (row: number, positionMode: 'overall' | 'class' = 'class'): unknown => evaluate(carClassRaceGap(repeatIndex()), BOARD, row, positionMode);
  const intOf = (row: number): unknown => evaluate(carClassInterval(repeatIndex()), BOARD, row);

  test('the leader of the list reads Lead, whatever its place in the race', () => {
    expect({ gap: gapOf(3), position: BOARD[2]!.position }).toEqual({ gap: 'Lead', position: 3 });
  });

  test('numbered overall, that row claims no lead it does not hold', () => {
    // The other half of the setting, which a zone's own filter reaches: one class listed by its
    // overall places. The row the column counts from is headed P3 there, and a cell beside it
    // reading Lead is one row of a board making two claims about a place that disagree. Nothing
    // is left to measure against the row itself, so the cell is empty, as the Int cell is.
    expect({ gap: gapOf(3, 'overall'), position: BOARD[2]!.position }).toEqual({ gap: '', position: 3 });
  });

  test('and every other row of it is measured exactly as before, the rows not having moved', () => {
    // The word follows the numbering; the figure follows the rows. A filter is what put these cars
    // on the board, so the car they are measured against is the one at the top of it either way.
    expect([gapOf(5, 'overall'), gapOf(6, 'overall'), gapOf(7, 'overall')]).toEqual([gapOf(5), gapOf(6), gapOf(7)]);
    expect([gapOf(5, 'overall'), gapOf(6, 'overall')]).toEqual(['+4.2', '+10.0']);
  });

  test('a class that leads the race keeps the word under either setting', () => {
    const LEADING: readonly Car[] = [
      car({ position: 1, classposition: 1, gaptoleader: null, ours: true }),
      car({ position: 2, classposition: 1, gaptoleader: 3.2, lapstoleader: 0 }),
      car({ position: 3, classposition: 2, gaptoleader: 8.4, lapstoleader: 0, lapstoclassleader: 0, ours: true }),
    ];
    const lead = (positionMode: 'overall' | 'class'): unknown => evaluate(carClassRaceGap(repeatIndex()), LEADING, 1, positionMode);
    expect({ overall: lead('overall'), inClass: lead('class') }).toEqual({ overall: 'Lead', inClass: 'Lead' });
  });

  test('no row claims the lead before the sim has placed anyone, under either setting (#1014)', () => {
    // The grid, or a practice before anyone has a time: every overall place is 0, and SimHub still
    // numbers the class places from 1 in the order of the driver list, so the first car of the class
    // has a class place of 1 and nothing else to say. Its place reads `--`, and the word beside it
    // waits for the same answer.
    const GRID: readonly Car[] = [
      car({ position: 0, classposition: 1, gaptoleader: null, ours: true }),
      car({ position: 0, classposition: 1, gaptoleader: null }),
      car({ position: 0, classposition: 2, gaptoleader: null, ours: true }),
    ];
    const first = (positionMode: 'overall' | 'class'): unknown => evaluate(carClassRaceGap(repeatIndex()), GRID, 1, positionMode);
    expect({ overall: first('overall'), inClass: first('class') }).toEqual({ overall: '', inClass: '' });
  });

  test('measured to the race leader the same column says nothing at all', () => {
    // The defect this reading exists for, and the reason it is not the one beside it: the class is
    // a lap down on the race, so every row falls into the lapped branch, no row reads Lead and the
    // column carries no gap within the class anywhere down it.
    expect(ROWS.map((row) => evaluate(carRaceGap(repeatIndex()), BOARD, row))).toEqual(['+1L', '+1L', '+1L', '+2L']);
  });

  test("a car on the class leader's lap reads the seconds to that car", () => {
    expect([gapOf(5), gapOf(6)]).toEqual(['+4.2', '+10.0']);
  });

  test('a car a lap down on the class leader reads the lap, not the seconds', () => {
    // Its laps to the class leader, and not to the race leader: the class leader is itself a lap
    // down on the race, so the two questions give different answers here.
    expect(gapOf(7)).toBe('+1L');
  });

  test('the interval is to the row above on the list, not to the car above on the leaderboard', () => {
    // Row 5 is the second of its class and the fifth of the board, and the car between the two is
    // of another class. The leaderboard reading is what a class board used to draw in this cell.
    expect({ list: intOf(5), leaderboard: evaluate(carInterval(repeatIndex()), BOARD, 5) }).toEqual({ list: '+4.2', leaderboard: '+2.2' });
  });

  test('the two columns add up, which is why they are one decision', () => {
    // Gap to the leader of the list, plus the interval to the row above, is the row above's gap.
    expect({ int: intOf(6), gap: gapOf(6), above: gapOf(5) }).toEqual({ int: '+5.8', gap: '+10.0', above: '+4.2' });
    expect(intOf(3)).toBe('');
  });

  test('the class leader on the road reads Lead and both columns are measured to it (#1063)', () => {
    // Row 5 has passed row 3 for the class lead since the line: SimHub still numbers row 3 first in
    // the class and counts its laps to the class leader from it, and the plugin has the class on the
    // road as rows 5, 3, 6, 7.
    const PASSED = BOARD.map((c, i) => (i === 4 ? { ...c, gaptoleader: 129.8 } : c));
    const live = {
      'OpenDash.LiveClassOrder1': 5, 'OpenDash.LiveClassOrder2': 3, 'OpenDash.LiveClassOrder3': 6, 'OpenDash.LiveClassOrder4': 7,
      'OpenDash.LiveClassPosition5': 1, 'OpenDash.LiveClassPosition3': 2, 'OpenDash.LiveClassPosition6': 3, 'OpenDash.LiveClassPosition7': 4,
    };
    const gap = (row: number): unknown => evaluate(carClassRaceGap(repeatIndex()), PASSED, row, 'class', live);
    const int = (row: number): unknown => evaluate(carClassInterval(repeatIndex()), PASSED, row, 'class', live);
    expect([gap(5), gap(3), gap(6)]).toEqual(['Lead', '+0.2', '+10.2']);
    expect([int(5), int(3), int(6)]).toEqual(['', '+0.2', '+10.0']);
  });

  test('a class leader passed for the race lead reads its seconds to the car that passed it (#1063)', () => {
    // A single class, so SimHub's first car is the class leader too, and it is not the player's, so
    // SimHub gives it no gap. Row 2 has passed it since the line and is half a second ahead of it.
    const PASSED: readonly Car[] = [
      car({ position: 1, classposition: 1, gaptoleader: null, ours: true }),
      car({ position: 2, classposition: 2, gaptoleader: -0.5, lapstoleader: 0, lapstoclassleader: 0, ours: true }),
      car({ position: 3, classposition: 3, gaptoleader: 8.4, lapstoleader: 0, lapstoclassleader: 0, ours: true }),
    ];
    const live = {
      'OpenDash.LiveClassOrder1': 2, 'OpenDash.LiveClassOrder2': 1, 'OpenDash.LiveClassOrder3': 3,
      'OpenDash.LiveClassPosition2': 1, 'OpenDash.LiveClassPosition1': 2, 'OpenDash.LiveClassPosition3': 3,
    };
    const gap = (row: number): unknown => evaluate(carClassRaceGap(repeatIndex()), PASSED, row, 'class', live);
    const int = (row: number): unknown => evaluate(carClassInterval(repeatIndex()), PASSED, row, 'class', live);
    expect([gap(2), gap(1), gap(3)]).toEqual(['Lead', '+0.5', '+8.9']);
    expect([int(2), int(1), int(3)]).toEqual(['', '+0.5', '+8.4']);
  });
});

/**
 * The Int column beside a car on another lap than the car in front of it (#1077).
 *
 * Int is the difference of two gaps to the leader, and a gap to the leader carries the whole laps a
 * car is down, so the interval of a car laps behind the car in front was those laps in seconds: a
 * car five laps down in the pits read `+484.6` beside a Gap of `+5L` on the pit wall. Whether two
 * cars are on one lap is a question of distance, as it is for the Gap column, and the distance is
 * SimHub's `currentlaphighprecision`, the laps begun and the fraction of the lap run.
 *
 * The board is one class of five in a race. The leader has just crossed the line to start its
 * twenty-first lap; the second car is on its lap, 12.3 s down and still on its twentieth; the third
 * is in the pits five laps down; the fourth is 12.4 s behind the third on the road and on its lap;
 * the fifth is a lap down on the fourth.
 */
describe('the Int column of a car on another lap than the car in front (#1077)', () => {
  const BOARD: readonly Car[] = [
    car({ position: 1, classposition: 1, gaptoleader: null, currentlaphighprecision: 20.02, ours: true }),
    car({ position: 2, classposition: 2, gaptoleader: 12.31, lapstoleader: 0, lapstoclassleader: 0, currentlaphighprecision: 19.88, ours: true }),
    car({ position: 3, classposition: 3, gaptoleader: 496.9, lapstoleader: 5, lapstoclassleader: 5, currentlaphighprecision: 14.81, ours: true }),
    car({ position: 4, classposition: 4, gaptoleader: 509.3, lapstoleader: 5, lapstoclassleader: 5, currentlaphighprecision: 14.66, ours: true }),
    car({ position: 5, classposition: 5, gaptoleader: 640.2, lapstoleader: 6, lapstoclassleader: 6, currentlaphighprecision: 13.62, ours: true }),
  ];
  const ROWS = [1, 2, 3, 4, 5];
  const overall = (row: number): unknown => evaluate(carInterval(repeatIndex()), BOARD, row, 'overall');
  const inClass = (row: number): unknown => evaluate(carClassInterval(repeatIndex()), BOARD, row);

  test('a lapped car behind a car on the lead lap reads the laps between them, as Gap does', () => {
    expect({ int: overall(3), gap: evaluate(carRaceGap(repeatIndex()), BOARD, 3, 'overall') }).toEqual({ int: '+5L', gap: '+5L' });
  });

  test('a lapped car on the lap of the car in front reads the seconds between them', () => {
    // Five laps down on the leader and on the third car's lap, so the interval is the time on the road.
    expect(overall(4)).toBe('+12.4');
  });

  test('a car a lap behind the car in front by distance reads the lap', () => {
    expect(overall(5)).toBe('+1L');
  });

  test('a car on the lap of the car in front by distance reads seconds, whatever the lap counters say', () => {
    // The leader has crossed the line and the second car has not: their counters differ by one and
    // they are 0.14 of a lap apart, which is the moment the counters got wrong for Gap (#1023).
    expect(overall(2)).toBe('+12.3');
  });

  test('the class list reads every row the way the overall list does', () => {
    expect(ROWS.map(inClass)).toEqual(ROWS.map(overall));
    expect(ROWS.map(inClass)).toEqual(['', '+12.3', '+5L', '+12.4', '+1L']);
  });

  test('where SimHub gives no distance the column reads seconds, which is always true', () => {
    const blind = BOARD.map((c) => ({ ...c, currentlaphighprecision: null }));
    expect(evaluate(carInterval(repeatIndex()), blind, 3, 'overall')).toBe('+484.6');
    expect(evaluate(carClassInterval(repeatIndex()), blind, 3)).toBe('+484.6');
  });

  test('both readings ask distance and not the lap counters', () => {
    for (const formula of [carInterval(repeatIndex()), carClassInterval(repeatIndex())]) {
      expect(formula).toContain('drivercurrentlaphighprecision(');
      expect(formula).not.toMatch(/drivercurrentlap\(/);
    }
  });

  test('the widest lap count the column can draw fits the budget the gap is drawn in', () => {
    expect(fits('+999L', CHARS.gap)).toBe(true);
  });
});

describe('the sector cells', () => {
  const sectorOf = (seconds: number): unknown => evaluate(carSector(repeatIndex(), 1), [car({ sectors: [seconds, 0, 0] })], 1);

  test('a sector under a minute reads two decimals, as the sheet draws it', () => {
    expect(sectorOf(28.4123)).toBe('28.41');
  });

  test('a sector over a minute reads seconds rather than growing a colon', () => {
    // `m:ss.ff` would want a second separator cell, and the budget is the width of every box a
    // sector is drawn in: buying that cell costs the 1280 x 60 sectors band its Best field.
    expect(sectorOf(142.3456)).toBe('142.35');
  });

  test('a sector that was never set reads the no-value placeholder', () => {
    expect(sectorOf(0)).toBe('--');
  });

  test('the widest sector the formatter can emit fills its budget exactly', () => {
    expect(fits('999.99', CHARS.sector)).toBe(true);
    // The other half of the same decision: a third decimal would want a sixth digit cell, so the
    // decimals and the budget cannot be changed one without the other.
    expect(fits('999.999', CHARS.sector)).toBe(false);
    expect(sectorTime(num(0))).toContain("'0.00'");
  });

  test('and it fits the cell at every size the second screens draw it in', () => {
    for (const density of Object.keys(DENSITIES) as Density[]) {
      const d = densityOf(density);
      for (const fs of [d.hero, d.big, d.mid, d.small, d.tiny]) {
        const mono = cells('SemiBold', fs);
        const drawn = 5 * mono.charWidth + mono.specialCharsWidth;
        expect({ density, fs, drawn, cell: monoWidth(mono, CHARS.sector) }).toMatchObject({ drawn: monoWidth(mono, CHARS.sector) });
      }
    }
  });
});
