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
import { cells, monoWidth, type Chars } from '../src/design/metrics.ts';
import { DENSITIES, densityOf, type Density } from '../src/second/density.ts';
import { CHARS, carSector, sectorTime } from '../src/second/values.ts';

const { num, repeatIndex } = ncalc;

/** One car of the leaderboard the expressions are evaluated against. */
interface Car {
  position: number;
  gaptoleader: number | null;
  currentlap: number;
  gaptoleadercombined: string | null;
  /** The last lap's sectors, in seconds; `0` is a sector that was never set. */
  sectors: readonly number[];
}

const car = (fields: Partial<Car>): Car => ({ position: 1, gaptoleader: 0, currentlap: 1, gaptoleadercombined: null, sectors: [0, 0, 0], ...fields });

/**
 * An NCalc formula evaluated in JavaScript against a leaderboard.
 *
 * The expression is translated rather than re-read, so that a test says what a row of the board
 * reads and not how the generator happens to spell it. Only the operators these expressions use
 * are translated; anything else reaches JavaScript unchanged and throws, which is the honest
 * outcome for a formula this evaluator does not understand.
 */
function evaluate(formula: string, board: readonly Car[], row: number): unknown {
  const js = formula
    .replace(/\bdriversector[a-z]+\(/g, 'sector(')
    .replace(/\bdriver([a-z]+)\(/g, "field('$1', ")
    .replace(/\brepeatindex\(\)/g, 'ROW')
    .replace(/\bif\(/g, 'iff(')
    .replace(/\bisnull\(/g, 'nz(')
    .replace(/\btimespantoseconds\(/g, 'secs(')
    .replace(/\bformat\(/g, 'fmtOf(')
    .replace(/\breplace\(/g, 'rep(')
    .replace(/ = /g, ' === ')
    .replace(/ and /g, ' && ')
    .replace(/ or /g, ' || ');

  const helpers = {
    ROW: row,
    field: (name: keyof Car, index: number): unknown => board[index - 1]?.[name] ?? null,
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
