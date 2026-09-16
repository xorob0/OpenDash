/**
 * The car settings strip, when the game does not publish one of its settings.
 *
 * iRacing omits `dcTractionControl` and `dcABS` on a car without the controls, so the strip has to
 * close over the cells that are not there rather than leave two holes of about fifty pixels in the
 * middle of the bar. Nothing in the repository evaluates a binding (XOR-20), so this file carries
 * the smallest evaluator that settles the question: the `Left` expressions the strip emits contain
 * nothing but numbers, the four arithmetic operators and `if(!(isnull(p)), a, b)`, and substituting
 * a presence for every `p` leaves plain arithmetic.
 *
 * That is deliberately not a general NCalc evaluator. It refuses anything it does not recognise, so
 * an expression that grew a function this does not know fails the test rather than being guessed
 * at.
 *
 * The cell widths are never read from the items, because a label and a numeral are each laid out
 * inside the cell rather than filling it. What the assertions use instead is the pitch, the
 * distance from one cell's left edge to the next in the layout where everything is present, which
 * is the cell's width and the gap after it and is exactly what closing has to remove.
 */
import { describe, expect, test } from 'bun:test';
import type { Item, TextItem } from '../src/generator.ts';
import { walkItems } from '../src/walk.ts';
import { STRIP_CELLS, ZONE_FACES, buildZoneFace } from '../src/zones/index.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

const stripItems = (folder: string): Item[] => {
  const face = ZONE_FACES.find((f) => f.folder === folder)!;
  return [...walkItems(buildZoneFace(face, OPTS).main.screens[0]!.items)].filter((i) => i.name.startsWith('bar.strip.'));
};

/** The strip's value items of one face, in drawing order. */
const stripValues = (folder: string): TextItem[] =>
  stripItems(folder).filter((i): i is TextItem => i.kind === 'text' && i.name.endsWith('.value'));

const cellIdOf = (item: { name: string }): string => item.name.slice('bar.strip.'.length).replace(/\.(value|label)$/, '');

/** Index of the matching close paren for the open paren at `open`. */
const matching = (s: string, open: number): number => {
  let depth = 0;
  for (let i = open; i < s.length; i += 1) {
    if (s[i] === '(') depth += 1;
    else if (s[i] === ')') {
      depth -= 1;
      if (depth === 0) return i;
    }
  }
  throw new Error(`unbalanced parentheses in ${s}`);
};

/** Top-level commas of an argument list, ignoring commas inside nested parentheses. */
const splitArgs = (s: string): string[] => {
  const out: string[] = [];
  let depth = 0;
  let start = 0;
  for (let i = 0; i < s.length; i += 1) {
    if (s[i] === '(') depth += 1;
    else if (s[i] === ')') depth -= 1;
    else if (s[i] === ',' && depth === 0) {
      out.push(s.slice(start, i));
      start = i + 1;
    }
  }
  out.push(s.slice(start));
  return out.map((a) => a.trim());
};

/**
 * The value of a strip `Left` expression given which settings the game does not publish.
 *
 * `absent` holds the cell expressions that are null. Every `if(...)` is resolved by asking whether
 * its condition mentions one of them, which is sound here because the only condition the strip
 * emits is `!(isnull(e))` over one cell expression, and a bracketed property name cannot be a
 * prefix of another.
 */
const evaluate = (expression: string, absent: readonly string[]): number => {
  let s = expression;
  for (let guard = 0; guard < 100; guard += 1) {
    const at = s.indexOf('if(');
    if (at === -1) break;
    const close = matching(s, at + 2);
    const args = splitArgs(s.slice(at + 3, close));
    if (args.length !== 3) throw new Error(`if() with ${args.length} arguments in ${expression}`);
    const [condition, whenTrue, whenFalse] = args as [string, string, string];
    s = s.slice(0, at) + `(${absent.some((e) => condition.includes(e)) ? whenFalse : whenTrue})` + s.slice(close + 1);
  }
  if (!/^[\d\s().+\-*/]+$/.test(s)) throw new Error(`the expression holds something this cannot evaluate: ${s}`);
  return Number(new Function(`return ${s};`)());
};

const leftOf = (item: Item): string => String(item.bindings?.Left?.formula);
/**
 * The property that says the car has the setting, which is what the strip asks about.
 *
 * For five of the seven it is the value's own property. For TC and ABS it is the raw iRacing field
 * behind the normalised reading, because SimHub reports 0 rather than null for a car with no such
 * control, and for the brake bias it is the reading before the isnull default is wrapped round it.
 */
const exprOf = (id: string): string => {
  const cell = STRIP_CELLS.find((c) => c.id === id)!;
  return cell.present ?? cell.expr;
};

describe('the strip closes over what the game does not publish', () => {
  const FOLDER = 'openDash';
  const values = stripValues(FOLDER);
  const ids = values.map(cellIdOf);
  /** Where each cell sits when every setting is published. */
  const whole = values.map((v) => evaluate(leftOf(v), []));
  /** Cell width plus the gap after it, which is what an absent cell has to give back. */
  const pitch = whole.slice(1).map((left, i) => left - whole[i]!);

  test('every cell binds its left edge, and its label moves with its value', () => {
    const items = stripItems(FOLDER);
    expect(items.length).toBe(values.length * 2);
    for (const item of items) expect({ item: item.name, bound: item.bindings?.Left !== undefined }).toMatchObject({ bound: true });
    for (const value of values) {
      const label = items.find((i) => i.name === `bar.strip.${cellIdOf(value)}.label`)!;
      expect(leftOf(label)).toBe(leftOf(value));
    }
  });

  test('with every setting published, the binding gives back the laid-out position', () => {
    // Which is what makes the static rect meaningful: the scene graph as written is the
    // all-present case, and the binding only ever moves a cell when something is missing.
    for (const [i, value] of values.entries()) {
      expect({ cell: ids[i], bound: whole[i] }).toMatchObject({ bound: value.rect.left });
    }
  });

  // Each case keeps the last cell, so that the closed strip and the full strip end on the same
  // cell and its width cancels out of the comparison of their centres.
  const cases: { name: string; absent: string[] }[] = [
    { name: 'TC and ABS, which is the quali scenario', absent: ['tc', 'abs'] },
    { name: 'the first cell', absent: ['slip'] },
    { name: 'three scattered cells', absent: ['slip', 'cut', 'map'] },
    { name: 'all but two', absent: ['slip', 'tc', 'cut', 'abs', 'map'] },
  ];

  for (const { name, absent } of cases) {
    describe(`without ${name}`, () => {
      const missing = absent.map(exprOf);
      const kept = ids.map((id, i) => ({ id, i })).filter(({ id }) => !absent.includes(id));
      const lefts = kept.map(({ i }) => evaluate(leftOf(values[i]!), missing));

      test('no hole is left where an absent cell would have been', () => {
        // Each survivor sits its own pitch after the one before it, which is the spacing it had
        // when nothing was missing. Without closing, the difference would carry the absent cells'
        // pitches as well.
        const steps = lefts.slice(1).map((left, k) => left - lefts[k]!);
        expect({ absent, steps }).toMatchObject({ steps: kept.slice(0, -1).map(({ i }) => pitch[i]!) });
      });

      test('and what is left is still centred between the two ends', () => {
        // The centre of a run is (first + last + width of last) / 2, and the last cell is the same
        // one in both layouts, so equal centres is exactly equal sums of the two edges.
        expect(lefts[0]! + lefts[lefts.length - 1]!).toBe(whole[0]! + whole[whole.length - 1]!);
      });

      test('every absent cell is hidden rather than drawn empty', () => {
        for (const id of absent) {
          const value = values.find((v) => cellIdOf(v) === id)!;
          expect({ id, visible: String(value.bindings?.Visible?.formula) }).toMatchObject({ visible: expect.stringContaining(exprOf(id)) });
        }
      });
    });
  }

  test('a narrow face closes its shorter strip the same way', () => {
    // 850 by 480 keeps two cells, so the arithmetic is a different one and worth its own case.
    const narrow = stripValues('openDash 850x480');
    expect(narrow.length).toBeGreaterThan(0);
    expect(narrow.length).toBeLessThan(values.length);
    const full = narrow.map((v) => evaluate(leftOf(v), []));
    for (const [i, value] of narrow.entries()) expect({ cell: cellIdOf(value), left: full[i] }).toMatchObject({ left: value.rect.left });
    const withoutFirst = narrow.slice(1).map((v) => evaluate(leftOf(v), [exprOf(cellIdOf(narrow[0]!))]));
    expect(withoutFirst[0]! + withoutFirst[withoutFirst.length - 1]!).toBe(full[0]! + full[full.length - 1]!);
  });
});

/**
 * The columns, and which of them each face has the width for.
 *
 * Both are read off the artboards: the column is a fixed width per face rather than a cell
 * measured from its own reading, and what a narrow face keeps is what is left once the two ends
 * have been laid out for the widest entry the catalogue holds. The cells a face keeps are
 * tabulated in `docs/design/zones.md` section 3 and are what changes first when a field of an end
 * grows, so they are pinned here rather than recomputed.
 */
describe('the strip is a rank of equal columns', () => {
  const KEPT: Record<string, string[]> = {
    openDash: ['slip', 'tc', 'cut', 'bias', 'abs', 'map', 'diff'],
    'openDash 1280x480': ['slip', 'tc', 'cut', 'bias', 'abs', 'map', 'diff'],
    'openDash 1280x400': ['slip', 'tc', 'cut', 'bias', 'abs', 'map', 'diff'],
    'openDash 1280x720': ['slip', 'tc', 'cut', 'bias', 'abs', 'map', 'diff'],
    'openDash 850x480': ['slip', 'tc', 'bias', 'abs'],
    'openDash 800x480': ['tc', 'bias', 'abs'],
    'openDash 600x686': ['slip', 'tc', 'cut', 'bias', 'abs'],
    'openDash 800x286': [],
  };

  for (const face of ZONE_FACES) {
    const scale = face.bar;
    test(`${face.folder} draws ${KEPT[face.folder]!.length} of the seven`, () => {
      expect(stripValues(face.folder).map(cellIdOf)).toEqual(KEPT[face.folder]!);
    });
    if (!scale || stripValues(face.folder).length === 0) continue;

    test(`${face.folder} draws them ${scale.stripCell} wide, centred, ${scale.gap} apart`, () => {
      const column = 2 * Math.ceil(scale.stripCell / 2);
      const values = stripValues(face.folder);
      for (const value of values) {
        // The column is a floor rather than a width: "Bias 50.5" is the one reading wider than the
        // column it is given, and it takes the pixels it needs rather than losing its last digit.
        expect({ cell: cellIdOf(value), width: value.rect.width, atLeast: value.rect.width >= column, centred: value.hAlign }).toMatchObject({ atLeast: true, centred: 'center' });
      }
      const ordered = [...values].sort((a, b) => a.rect.left - b.rect.left);
      for (const [i, value] of ordered.slice(1).entries()) {
        const before = ordered[i]!;
        expect({ cell: cellIdOf(value), pitch: value.rect.left - before.rect.left }).toMatchObject({ pitch: before.rect.width + scale.gap });
      }
    });
  }
});

/**
 * The strip is a row of values, drawn as the bar's end fields are.
 *
 * Both of these were silently wrong for as long as the strip measured its cells for a label-sized
 * numeral and drew one: a cell wide enough for "BIAS" but holding "54.5" at the value's size
 * overlapped the cell beside it, and the whole suite stayed green, because the only fit check that
 * sees the bar compares the strip against the two ends and never a cell against its neighbour.
 */
describe('the strip is drawn in the bar value type', () => {
  for (const face of ZONE_FACES.filter((f) => stripValues(f.folder).length > 0)) {
    describe(face.folder, () => {
      const values = stripValues(face.folder);

      test('a cell value is the same type, ink and line as a bar end field', () => {
        const end = [...walkItems(buildZoneFace(face, OPTS).main.screens[0]!.items)].find(
          (i): i is TextItem => i.kind === 'text' && /^bar\.(Left|Right)1\..*\.value$/.test(i.name),
        )!;
        for (const value of values) {
          expect({
            cell: cellIdOf(value),
            size: value.fontSize,
            color: value.textColor,
            top: value.rect.top,
            height: value.rect.height,
          }).toMatchObject({ size: end.fontSize, color: end.textColor, top: end.rect.top, height: end.rect.height });
        }
      });

      test('no cell is drawn over the one beside it', () => {
        // Measured on the boxes rather than the pitch: a cell measured for a smaller numeral than
        // it draws keeps its pitch and grows its box, which is exactly the failure this is for.
        const ordered = [...values].sort((a, b) => a.rect.left - b.rect.left);
        for (const [i, value] of ordered.slice(1).entries()) {
          const before = ordered[i]!;
          const clear = value.rect.left >= before.rect.left + before.rect.width;
          expect({ cell: cellIdOf(value), after: cellIdOf(before), left: value.rect.left, ends: before.rect.left + before.rect.width, clear }).toMatchObject({ clear: true });
        }
      });

      test('every cell sits on a whole pixel, closed as well as full', () => {
        // `rank` centres on (width - total) / 2 in the static rect and again in the `Left` binding,
        // which nothing rounds. Even cell widths are what keep the two agreeing, and they have to
        // agree for every subset the closing can produce rather than only the all-present one.
        const ids = values.map(cellIdOf);
        for (let mask = 0; mask < 1 << ids.length; mask += 1) {
          const absent = ids.filter((_, i) => (mask & (1 << i)) !== 0);
          if (absent.length === ids.length) continue;
          const missing = absent.map(exprOf);
          for (const value of values) {
            if (absent.includes(cellIdOf(value))) continue;
            const left = evaluate(leftOf(value), missing);
            expect({ absent, cell: cellIdOf(value), left, whole: Number.isInteger(left) }).toMatchObject({ whole: true });
          }
        }
      });
    });
  }
});
