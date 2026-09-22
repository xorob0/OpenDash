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
  currentlap: number;
  gaptoleadercombined: string | null;
  /** Whether this car is in the player's own class, which is what the class-only lookup answers from. */
  ours: boolean;
  /** The last lap's sectors, in seconds; `0` is a sector that was never set. */
  sectors: readonly number[];
}

const car = (fields: Partial<Car>): Car =>
  ({ position: 1, classposition: 1, gaptoleader: 0, currentlap: 1, gaptoleadercombined: null, ours: false, sectors: [0, 0, 0], ...fields });

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
 */
function evaluate(formula: string, board: readonly Car[], row: number, positionMode: 'overall' | 'class' = 'class'): unknown {
  const js = formula
    .replace(/\bdriversector[a-z]+\(/g, 'sector(')
    .replace(/\bdriver([a-z]+)\(/g, "field('$1', ")
    .replace(/\bgetopponentleaderboardposition_playerclassonly\(/g, 'classRow(')
    .replace(/\[([A-Za-z0-9_.]+)\]/g, "P('$1')")
    .replace(/\brepeatindex\(\)/g, 'ROW')
    .replace(/\bif\(/g, 'iff(')
    .replace(/\bisnull\(/g, 'nz(')
    .replace(/\btimespantoseconds\(/g, 'secs(')
    .replace(/\bformat\(/g, 'fmtOf(')
    .replace(/\breplace\(/g, 'rep(')
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
      if (name !== 'OpenDash.PositionMode') throw new Error(`the evaluator publishes one property, not ${name}`);
      return positionMode;
    },
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
  const BOARD: readonly Car[] = [
    car({ position: 1, gaptoleader: 0, currentlap: 24, gaptoleadercombined: '' }),
    car({ position: 2, gaptoleader: 2.64, currentlap: 24, gaptoleadercombined: '+2.6' }),
    car({ position: 3, gaptoleader: 142.35, currentlap: 23, gaptoleadercombined: '+1L' }),
    car({ position: 4, gaptoleader: null, currentlap: 23, gaptoleadercombined: null }),
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

  test('the combined string is reached only once the lapped test has said so', () => {
    // It is one string for both cases and the dash has no say in its sign or its decimals, so a
    // row that is not lapped must never fall through to it.
    const formula = carRaceGap(repeatIndex());
    const lapped = formula.indexOf('drivercurrentlap');
    expect(lapped).toBeGreaterThan(-1);
    expect(formula.indexOf('drivergaptoleadercombined')).toBeGreaterThan(lapped);
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
    car({ position: 1, classposition: 1, gaptoleader: 0, currentlap: 24, gaptoleadercombined: '' }),
    car({ position: 2, classposition: 2, gaptoleader: 2.64, currentlap: 24, gaptoleadercombined: '+2.6' }),
    car({ position: 3, classposition: 1, gaptoleader: 130, currentlap: 23, gaptoleadercombined: '+1L', ours: true }),
    car({ position: 4, classposition: 3, gaptoleader: 132, currentlap: 23, gaptoleadercombined: '+1L' }),
    car({ position: 5, classposition: 2, gaptoleader: 134.2, currentlap: 23, gaptoleadercombined: '+1L', ours: true }),
    car({ position: 6, classposition: 3, gaptoleader: 140, currentlap: 23, gaptoleadercombined: '+1L', ours: true }),
    car({ position: 7, classposition: 4, gaptoleader: 260, currentlap: 22, gaptoleadercombined: '+2L', ours: true }),
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
      car({ position: 1, classposition: 1, gaptoleader: 0, currentlap: 24, ours: true }),
      car({ position: 2, classposition: 1, gaptoleader: 3.2, currentlap: 24 }),
      car({ position: 3, classposition: 2, gaptoleader: 8.4, currentlap: 24, ours: true }),
    ];
    const lead = (positionMode: 'overall' | 'class'): unknown => evaluate(carClassRaceGap(repeatIndex()), LEADING, 1, positionMode);
    expect({ overall: lead('overall'), inClass: lead('class') }).toEqual({ overall: 'Lead', inClass: 'Lead' });
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
    // Its own lap against the class leader's, and not against the race leader's: the class leader
    // is itself a lap down on the race, so the two questions give different answers here.
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
