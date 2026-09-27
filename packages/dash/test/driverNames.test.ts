/**
 * What a driver is called, and that it fits.
 *
 * #385 replaced a three-letter code with four formats a driver picks between, a team-name option, and
 * an ellipsis where a name does not fit. All four of those live in one NCalc expression, and an
 * expression is the one part of this build nothing else executes: `textFit.test.ts` measures what a
 * box holds and `contract.test.ts` checks that the property is declared, and between them a formula
 * that returns the wrong string passes both. So this file evaluates the formula.
 *
 * The evaluator is SimHub's own semantics rather than an approximation of them, because that is where
 * the traps are. `StringExtensions.Left(value, startIndex, maxLength)` in WoteverCommon returns an
 * empty string rather than throwing when the value is short, which is what the ellipsis test depends
 * on; `GetShortName` splits the name on spaces and abbreviates the first word only, which is what the
 * two reordered formats depend on; and .NET's `String.Replace` replaces every occurrence, which is
 * what they are at risk from. Both were read out of the decompiled 9.12.6 assemblies.
 */
import { describe, expect, test } from 'bun:test';
import { DRIVER_NAME_FORMATS, type DriverNameFormat } from '../src/contract.ts';
import { charsThatFit, ELLIPSIS, measureText, widestGlyph, widestOf } from '../src/design/advances.ts';
import { columnWidths, MIXED_CASE_NAME_SIZE, NAME_FACE, nameColumnFloor, nameIsUpperCased, nameSizeForRow, SHORTEST_NAME_CHARS, tableRowHeight, type ColumnId } from '../src/second/table.ts';
import { driverName, ellipsised } from '../src/second/values.ts';
import { RELATIVE_COLUMNS } from '../src/modules/relative.ts';
import { fittingColumns, LEADERBOARD_COLUMNS } from '../src/modules/leaderboard.ts';
import { RACE_COLUMNS, TOWER_COLUMNS, PORTRAIT_COLUMNS } from '../src/screens/pitwall.ts';
import { walkItems } from '../src/walk.ts';
import { MODULES } from '../src/modules/index.ts';
import { buildScreenPackage, SCREEN_PACKAGES } from '../src/screens/index.ts';
import { rect } from '../src/design/geometry.ts';
import { ncalc } from '../src/generator.ts';
import type { Density } from '../src/second/density.ts';
import type { TextItem } from '../src/generator.ts';

const { repeatIndex } = ncalc;

/** One entry of the sim's field: what the three opponent functions the name reads would answer. */
interface Entry {
  name: string;
  team: string;
}

/**
 * `StringExtensions.Left` from WoteverCommon, exactly.
 *
 * The clamp is the whole point: `num` is the smallest of the length asked for, `startIndex + maxLength`
 * and what is left of the string past `startIndex`, and a result of nought or less is an empty string.
 * So `left(v, 0, n)` is the first `n` characters or the whole value, and `left(v, n, 1)` is the one
 * character past a budget of `n` or nothing at all.
 */
const Left = (value: string, startIndex: number, maxLength: number): string => {
  const n = Math.min(maxLength, Math.min(startIndex + maxLength, value.length - startIndex));
  return n <= 0 ? '' : value.substr(startIndex, n);
};

/**
 * `StringExtensions.GetShortName`: `Liam Byrne` becomes `L. Byrne`, `Liam Van Byrne` becomes
 * `L. Van Byrne`, and a name of one word comes back untouched.
 */
const GetShortName = (value: string): string => {
  const words = value.split(' ').filter((w) => w.length > 0);
  const [first, ...rest] = words;
  return words.length > 1 && first !== undefined ? `${first.substr(0, 1).toUpperCase()}. ${rest.join(' ')}` : value;
};

/** The rig settings the name expression reads. */
interface Rig {
  format: DriverNameFormat;
  team: boolean;
}

/**
 * An NCalc formula evaluated in JavaScript against one entry.
 *
 * Translated rather than pattern-matched, the way `pitwallValues.test.ts` does it: the test says what
 * a row reads and not how the generator spells it. Anything the translation does not cover reaches
 * JavaScript unchanged and throws, which is the honest outcome.
 */
function evaluate(formula: string, entry: Entry | undefined, rig: Rig): unknown {
  const js = formula
    .replace(/\bdriver([a-z]+)\(/g, "field('$1', ")
    .replace(/\[([A-Za-z0-9_.]+)\]/g, "P('$1')")
    .replace(/\brepeatindex\(\)/g, '1')
    .replace(/\bif\(/g, 'iff(')
    .replace(/\bisnull\(/g, 'nz(')
    .replace(/\bleft\(/g, 'lft(')
    .replace(/\breplace\(/g, 'rep(')
    .replace(/ = /g, ' === ')
    .replace(/ != /g, ' !== ')
    .replace(/ and /g, ' && ')
    .replace(/ or /g, ' || ');
  const helpers = {
    field: (fn: string, index: number): unknown => {
      if (index !== 1 || entry === undefined) return null;
      if (fn === 'name') return entry.name;
      if (fn === 'shortname') return GetShortName(entry.name);
      if (fn === 'teamname') return entry.team === '' ? null : entry.team;
      throw new Error(`the evaluator answers three driver functions, not ${fn}`);
    },
    P: (name: string): unknown => {
      if (name === 'OpenDash.DriverNameFormat') return rig.format;
      if (name === 'OpenDash.DriverNameTeam') return rig.team;
      throw new Error(`the evaluator publishes the two name settings, not ${name}`);
    },
    iff: (condition: unknown, then: unknown, otherwise: unknown): unknown => (condition ? then : otherwise),
    nz: (value: unknown, fallback?: unknown): unknown => (fallback === undefined ? value === null || value === undefined : (value ?? fallback)),
    lft: (value: string, startIndex: number, maxLength: number): string => Left(value, startIndex, maxLength),
    // .NET's String.Replace: every occurrence, not the first.
    rep: (value: string, from: string, to: string): string => value.split(from).join(to),
  };
  const names = Object.keys(helpers);
  return new Function(...names, `return ${js};`)(...names.map((n) => helpers[n as keyof typeof helpers]));
}

/** The name one entry draws under one rig setting, with no cut applied. */
const named = (entry: Entry | undefined, format: DriverNameFormat, team = false): unknown => evaluate(driverName(repeatIndex()), entry, { format, team });

/** And with the column's budget applied. */
const drawn = (entry: Entry | undefined, format: DriverNameFormat, chars: number, team = false): unknown =>
  evaluate(ellipsised(driverName(repeatIndex()), chars), entry, { format, team });

const entry = (name: string, team = ''): Entry => ({ name, team });

describe('the four formats', () => {
  const liam = entry('Liam Byrne');

  test('each one writes the name the panel promises it writes', () => {
    // The four labels on the Data tab are these four strings, so a change here is a change to what the
    // panel says: PanelDataTabTests pins the labels and this pins what they mean.
    expect(named(liam, 'full')).toBe('Liam Byrne');
    expect(named(liam, 'initialSurname')).toBe('L. Byrne');
    expect(named(liam, 'initialFirstName')).toBe('B. Liam');
    expect(named(liam, 'surnameFirst')).toBe('Byrne Liam');
  });

  test('a name of one word is drawn whole in all four, having nothing to reorder', () => {
    const one = entry('Verstappen');
    for (const format of DRIVER_NAME_FORMATS) expect({ format, drawn: named(one, format) }).toEqual({ format, drawn: 'Verstappen' });
  });

  test('a surname of two words stays whole and stays the surname', () => {
    // `GetShortName` abbreviates the first word only, so everything after it is the surname. The
    // failure this guards is the reordering splitting on the wrong space and drawing "Van Liam Byrne".
    const dutch = entry('Liam Van Byrne');
    expect(named(dutch, 'initialSurname')).toBe('L. Van Byrne');
    expect(named(dutch, 'initialFirstName')).toBe('V. Liam');
    expect(named(dutch, 'surnameFirst')).toBe('Van Byrne Liam');
  });

  test('a name whose first and last words are the same is not eaten by the replace', () => {
    // .NET's String.Replace takes every occurrence, so taking the surname out of the full name can
    // take more than one thing out of it. Here both words are "Byrne": the needle is " Byrne", which
    // occurs once, and the first name survives.
    const twin = entry('Byrne Byrne');
    expect(named(twin, 'surnameFirst')).toBe('Byrne Byrne');
    expect(named(twin, 'initialFirstName')).toBe('B. Byrne');
  });

  test('a row with no car draws nothing rather than a stray full stop', () => {
    for (const format of DRIVER_NAME_FORMATS) expect({ format, drawn: named(undefined, format) }).toEqual({ format, drawn: '' });
  });

  test('the formats the contract declares are the formats the expression answers to', () => {
    // A fifth value added to the contract and not to the expression would fall through to the last
    // branch and draw the surname first, silently. This is what makes that a failure.
    expect(DRIVER_NAME_FORMATS).toEqual(['full', 'initialSurname', 'initialFirstName', 'surnameFirst']);
    expect(new Set(DRIVER_NAME_FORMATS.map((f) => named(liam, f))).size).toBe(DRIVER_NAME_FORMATS.length);
  });
});

describe('the team option', () => {
  test('it names the team in place of the driver, whatever the format says', () => {
    const e = entry('Liam Byrne', 'Byrne Motorsport');
    for (const format of DRIVER_NAME_FORMATS) expect({ format, drawn: named(e, format, true) }).toEqual({ format, drawn: 'Byrne Motorsport' });
  });

  test('and keeps the driver for a car the sim publishes no team for', () => {
    // Per row, not per session: a sprint grid with one team entry in it draws that team and twenty
    // drivers rather than one name and twenty blanks.
    expect(named(entry('Liam Byrne'), 'initialSurname', true)).toBe('L. Byrne');
    expect(named(entry('Liam Byrne', ''), 'full', true)).toBe('Liam Byrne');
  });

  test('and is not reached at all while the setting is off', () => {
    expect(named(entry('Liam Byrne', 'Byrne Motorsport'), 'full')).toBe('Liam Byrne');
  });
});

describe('the ellipsis', () => {
  const long = entry('Wilhelmina Aleksandrova');

  test('a name that fits is drawn whole, with nothing appended', () => {
    expect(drawn(entry('Liam Byrne'), 'full', 10)).toBe('Liam Byrne');
    expect(drawn(entry('Liam Byrne'), 'full', 20)).toBe('Liam Byrne');
  });

  test('a name one character over is cut and closed', () => {
    expect(drawn(entry('Liam Byrne'), 'full', 9)).toBe(`Liam Byr${ELLIPSIS}`);
  });

  test('a cut that lands between two words takes the space with it', () => {
    // What the VM photographed: the narrow zone holds seven characters, so `Chloe Dubois` was cut to
    // `Chloe ` and closed to `Chloe …`. A gap and then three dots reads as a pause rather than as a
    // name that would not fit, and it spends one of the seven on nothing.
    expect(drawn(entry('Chloe Dubois'), 'full', 7)).toBe(`Chloe${ELLIPSIS}`);
    expect(drawn(entry('Marco Ricci'), 'full', 7)).toBe(`Marco${ELLIPSIS}`);
    expect(drawn(entry('Liam Byrne'), 'full', 6)).toBe(`Liam${ELLIPSIS}`);
    expect(drawn(entry('Hannah Fischer'), 'full', 8)).toBe(`Hannah${ELLIPSIS}`);
    // And the surname-first format, where the space of the same name falls somewhere else.
    expect(drawn(entry('Chloe Dubois'), 'surnameFirst', 8)).toBe(`Dubois${ELLIPSIS}`);
  });

  test('and a cut that lands inside a word keeps every letter it had room for', () => {
    // The other half: the needle is a space *and* an ellipsis, so it matches the one thing the
    // expression just built and nothing the sim sent.
    expect(drawn(entry('Chloe Dubois'), 'full', 8)).toBe(`Chloe D${ELLIPSIS}`);
    expect(drawn(entry('Hannah Fischer'), 'full', 9)).toBe(`Hannah F${ELLIPSIS}`);
    // A name with no space in it is untouched by any of it.
    expect(drawn(entry('Verstappen'), 'full', 6)).toBe(`Verst${ELLIPSIS}`);
  });

  test('what is drawn never exceeds the budget, at any budget', () => {
    for (let chars = 0; chars <= 30; chars++) {
      const text = String(drawn(long, 'full', chars));
      expect({ chars, length: text.length, over: text.length > chars }).toMatchObject({ over: false });
      // And it is the head of the name, so a driver reads the same letters they would have read.
      const body = text.endsWith(ELLIPSIS) ? text.slice(0, -1) : text;
      expect({ chars, head: long.name.startsWith(body) }).toMatchObject({ head: true });
    }
  });

  test('an empty budget draws nothing rather than a lone ellipsis', () => {
    expect(drawn(long, 'full', 0)).toBe('');
  });

  test('a twenty-five character name ends in an ellipsis at every table width the build emits', () => {
    // The ticket's own case. Every list column set at every density: what the name column holds, and
    // whether a name far longer than it comes out cut and closed rather than cut by WPF.
    const twentyFive = entry('Aleksandra Wilhelmovanova');
    expect(twentyFive.name).toHaveLength(25);
    // The two list pages at every zone width the faces and the companion hand them, and the three pit
    // wall boards at the widths their own screens are: a board is 1920 wide or 1080 in portrait, and
    // measuring its eighteen columns in a 250 px zone would be measuring a table nothing builds.
    const sets: [string, readonly ColumnId[], boolean, readonly number[]][] = [
      ['relative', RELATIVE_COLUMNS, false, [225, 250, 445, 576, 607, 745, 1247]],
      ['leaderboard', LEADERBOARD_COLUMNS, false, [225, 250, 445, 576, 607, 745, 1247]],
      ['race board', RACE_COLUMNS, true, [1888, 1920]],
      ['tower', TOWER_COLUMNS, true, [1888, 1920]],
      ['portrait board', PORTRAIT_COLUMNS, true, [1048, 1080]],
    ];
    let measured = 0;
    for (const [what, declared, board, widths] of sets) {
      for (const density of ['companion', 'zone', 'wide', 'compact', 'panel'] as Density[]) {
        for (const width of widths) {
          const rowHeight = tableRowHeight(density);
          // A list sheds a column to reach the floor under its name, which is what the modules do; a
          // board's name column is the flexible one and takes what the fixed ones leave.
          const columns = board ? declared : fittingColumns(declared, width, density, rowHeight);
          const nameWidth = columnWidths(columns, width, density, rowHeight, board)[columns.indexOf('name')] ?? 0;
          if (nameWidth <= 0) continue;
          // Taken from the row rather than written down. It was `density === 'companion' ? 15 : 13`,
          // which was the ramp before #339 raised the name to 15 from the 34 px row up: at zone density
          // that measured a 13 px name in a 138 px column, twelve characters, where the build draws 15
          // in it and cuts at ten. The one budget the build never emitted was the only one this test
          // measured, and a 15 px name clipping anywhere would have passed.
          const fs = nameSizeForRow(rowHeight, board);
          const chars = charsThatFit(NAME_FACE, fs, nameWidth);
          const text = String(drawn(twentyFive, 'full', chars));
          measured += 1;
          // Cut where the column cannot hold twenty-five characters and whole where it can, which on a
          // companion page 745 px wide it can. What must never happen is either of the other two: a name
          // ellipsised when there was room, or one drawn past the budget and clipped by WPF.
          expect({ what, density, width, chars, text, cut: text.endsWith(ELLIPSIS) }).toMatchObject({ cut: chars < twentyFive.name.length });
          // Measured in the case the row draws it in, which under 25 px is upper. Upper case is wider
          // per letter and the budget does not change for it — the count is characters of the face's
          // widest glyph, which is a W either way — so this is the assertion that says so rather than
          // leaves it to be believed.
          const asDrawn = nameIsUpperCased(fs) ? text.toUpperCase() : text;
          expect({ what, density, width, fits: measureText(NAME_FACE, asDrawn, fs) < nameWidth }).toMatchObject({ fits: true });
        }
      }
    }
    expect(measured).toBeGreaterThan(50);
  });

  test('the declared widest is wider than anything the cut can draw', () => {
    // `widest` is what the fit tests measure, so it has to be the true worst case: the budget's worth
    // of the face's widest glyph. The ellipsis is narrower than that glyph in every measured face,
    // which is what makes a cut name narrower than a whole one of the same length.
    for (const chars of [4, 8, 12, 25]) {
      for (const fs of [13, 15, 24]) {
        const widest = measureText(NAME_FACE, widestOf(NAME_FACE, chars), fs);
        const cut = measureText(NAME_FACE, widestOf(NAME_FACE, chars - 1) + ELLIPSIS, fs);
        expect({ chars, fs, within: cut <= widest }).toMatchObject({ within: true });
      }
    }
  });

  test('the ellipsis is a glyph the bundled fonts carry', () => {
    // Measured, not assumed. An unmeasured character falls back to FALLBACK_ADVANCE, which would make
    // the budget above a guess; `metrics.test.ts` holds the fallback under the widest glyph.
    expect(ELLIPSIS).toBe('…');
    expect(measureText(NAME_FACE, ELLIPSIS, 100)).toBeGreaterThan(0);
    expect(measureText(NAME_FACE, ELLIPSIS, 100)).toBeLessThan(measureText(NAME_FACE, widestGlyph(NAME_FACE).glyph, 100));
  });
});

describe('no list draws a three-letter code', () => {
  const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

  test('a name column always has room for the shortest format', () => {
    // The floor `fittingColumns` sheds a column to reach, and what `nameColumnFloor` is for.
    expect(SHORTEST_NAME_CHARS).toBe('L. Byrne'.length);
    for (const fs of [13, 15]) expect(charsThatFit(NAME_FACE, fs, nameColumnFloor(fs))).toBeGreaterThanOrEqual(SHORTEST_NAME_CHARS);
  });

  test('every name any module draws is the format expression, and never a cut to three characters', () => {
    // The code was `ucase(left(name, 0, 3))` and it was drawn by the lists and by the opponents page.
    // What replaced it reads `drivershortname` and the two settings, so both of those are the mark of
    // a name that follows the rig, and the code has neither.
    //
    // The upper-casing used to be the other half of this check, nothing about a name being shouted.
    // It is not a mark of anything any more: the whole column is shouted under 25 px, which is the
    // case rule the test below holds. What is left of the code's own signature is the *absence* of
    // the format property, which no budget of any size can imitate.
    const boxes = [rect(0, 0, 745, 276), rect(0, 0, 250, 290), rect(0, 0, 225, 290), rect(0, 0, 576, 112)];
    let names = 0;
    for (const module of MODULES) {
      for (const density of ['zone', 'compact'] as Density[]) {
        for (const frame of boxes) {
          for (const item of module.build({ frame, density, prefix: `${module.id}.` }).flatMap((i) => [...walkItems([i])])) {
            if (item.kind !== 'text') continue;
            const binding = (item as TextItem).bindings?.Text;
            const formula = binding !== undefined && binding.mode === 'formula' ? binding.formula : undefined;
            const bind = typeof formula === 'string' ? formula : formula?.expression;
            if (bind === undefined || !bind.includes('drivername(')) continue;
            names += 1;
            expect({ module: module.id, item: item.name, format: bind.includes('OpenDash.DriverNameFormat') }).toMatchObject({ format: true });
          }
        }
      }
    }
    // The relative, the leaderboard and both blocks of the opponents page, over four boxes and two
    // densities: a selection that found nothing would pass this whole test vacuously.
    expect(names).toBeGreaterThan(20);
  });

  /**
   * And the case rule, over the same walk.
   *
   * The failure it answers is a rendering one and cannot be seen from here: at 13 px WPF welded the
   * dot of a lowercase `i` to its stem and `Liam Byrne` came back off the VM as `Llam B…`. What can be
   * checked from here is that the rule was applied wherever a name is drawn and not only in the module
   * the screenshot came from — the relative, the leaderboard, the opponents page and the three pit wall
   * boards all draw a name and three separate call sites used to build one.
   */
  test('and is shouted exactly where the size cannot keep the dot of an i', () => {
    const boxes = [rect(0, 0, 745, 276), rect(0, 0, 250, 290), rect(0, 0, 225, 290), rect(0, 0, 576, 112)];
    const sizes = new Set<number>();
    for (const module of MODULES) {
      for (const density of ['zone', 'compact'] as Density[]) {
        for (const frame of boxes) {
          for (const item of module.build({ frame, density, prefix: `${module.id}.` }).flatMap((i) => [...walkItems([i])])) {
            if (item.kind !== 'text') continue;
            const text = item as TextItem;
            const binding = text.bindings?.Text;
            const formula = binding !== undefined && binding.mode === 'formula' ? binding.formula : undefined;
            const bind = typeof formula === 'string' ? formula : formula?.expression;
            if (bind === undefined || !bind.includes('drivername(')) continue;
            sizes.add(text.fontSize);
            expect({ module: module.id, item: item.name, fs: text.fontSize, shouted: bind.includes('ucase(') }).toMatchObject({
              shouted: nameIsUpperCased(text.fontSize),
            });
          }
        }
      }
    }
    // Every size a name is drawn at here is under the bound, so every one of them is shouted. The
    // assertion above is still written as the rule rather than as `true`, since a taller row would
    // earn the sim's own spelling back and nothing should have to remember to allow that.
    // 15 from the 34 px row up, 13 in the narrow zone's 28 px row, and the opponents page's own 12,
    // which comes from the density rather than from a row and is the smallest name the build draws.
    expect([...sizes].sort((a, b) => a - b)).toEqual([12, 13, 15]);
    expect(MIXED_CASE_NAME_SIZE).toBe(25);
  });

  test('and no package ships one either', () => {
    for (const def of SCREEN_PACKAGES) {
      const pkg = buildScreenPackage(def, OPTS);
      const json = JSON.stringify(pkg.dashboards);
      expect({ folder: def.folder, code: json.includes('ucase(left(isnull(drivername') }).toMatchObject({ code: false });
      expect({ folder: def.folder, names: json.includes('OpenDash.DriverNameFormat') }).toMatchObject({ names: true });
    }
  });
});
