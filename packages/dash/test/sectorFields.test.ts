/**
 * The row of sectors the pit wall's lap delta panel draws under its delta.
 *
 * Six fields where the box has room, three where it does not, and one colour across each pair. The
 * pair is the point: a sector and its delta are two readings of one fact, so a driver who saw them
 * disagree would have to work out which of the two to believe.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { sectorColour, sectorFields } from '../src/second/sectors.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { DELTA_WIDEST, hasTime, sectorLast } from '../src/second/values.ts';
import { ds } from '../src/tokens.ts';
import type { TextItem } from '../src/generator.ts';
import { walkItems } from '../src/walk.ts';
import { widthAsDrawn } from './drawnStrings.ts';
import { evalNcalc } from './ncalcEval.ts';

const row = (width: number): TextItem[] =>
  sectorFields('p.', rect(0, 0, width, 60), 'wide', 24).filter((i): i is TextItem => i.kind === 'text');

const labels = (width: number): string[] =>
  row(width)
    .filter((i) => i.name.endsWith('.label'))
    .map((i) => i.text);

describe('the sectors under a lap delta', () => {
  test('are the three times and then the three deltas, in that order', () => {
    // The sheet groups them rather than interleaving them, so the times read as a set and the
    // deltas as another; interleaved, a driver comparing two sectors reads across a delta.
    expect(labels(1248)).toEqual(['S1', 'S2', 'S3', 'Δ S1', 'Δ S2', 'Δ S3']);
  });

  test('are packed from the left edge at one pitch, not spread across thirds', () => {
    const lefts = row(1248)
      .filter((i) => i.name.endsWith('.label'))
      .map((i) => i.rect.left);
    expect(lefts[0]).toBe(0);
    const pitches = new Set(lefts.slice(1).map((left, i) => left - lefts[i]!));
    expect(pitches.size).toBe(1);
  });

  test('share one colour between a sector and its delta', () => {
    for (const sector of [1, 2, 3]) {
      const time = row(1248).find((i) => i.name === `p.s${sector}.value`);
      const delta = row(1248).find((i) => i.name === `p.delta${sector}.value`);
      const formula = (i?: TextItem): string => String(i?.bindings?.TextColor?.formula ?? '');
      expect(formula(time)).toBe(sectorColour(sector));
      expect(formula(delta)).toBe(formula(time));
    }
    // And that colour is the three the design names, purple for a session best over green and red.
    expect(sectorColour(1)).toContain(ds.purpose.lap.sessionBest);
    expect(sectorColour(1)).toContain(ds.purpose.delta.faster);
    expect(sectorColour(1)).toContain(ds.purpose.delta.slower);
  });

  test('fall back to three times carrying their deltas in their labels when six will not fit', () => {
    // The older form, and the one a zone was always given: a bound label reading "S1 · −0.29".
    const narrow = labels(240);
    expect(narrow).toHaveLength(3);
    expect(narrow.every((l) => !l.startsWith('Δ'))).toBe(true);
    const bound = row(240).find((i) => i.name === 'p.s1.label');
    expect(String(bound?.bindings?.Text?.formula ?? '')).toContain('S1');
  });

  test('never draw past the frame they are given, at any width', () => {
    for (const width of [1248, 800, 560, 400, 300, 240, 180]) {
      for (const item of row(width)) {
        expect({ width, name: item.name, inside: item.rect.left >= 0 && item.rect.left + item.rect.width <= width }).toEqual({
          width,
          name: item.name,
          inside: true,
        });
      }
    }
  });
});

/**
 * #886: a sector delta is cut for `±99.99`, and the sector of the in-lap that holds the pit stall is
 * a stop's length off your best of it. Drawn to two places regardless, its `+123.45` was drawn
 * `+123.4`, in the field and in the label of a narrow zone alike.
 */
describe('a sector delta past a hundred seconds', () => {
  /** A frame in which sector one is `seconds` off its best, with a best of half a minute. */
  const frame = (seconds: number) => ({
    'DataCorePlugin.GameData.Sector1LastLapTime': 30 + seconds,
    'DataCorePlugin.GameData.Sector1BestTime': 30,
  });
  const READINGS: readonly [number, string][] = [
    [-0.29, '−0.29'],
    [12.34, '+12.34'],
    [123.47, '+123.5'],
    [1234.56, '+1235'],
  ];

  test('gives up a place in the field rather than its last digit', () => {
    const delta = row(1248).find((i) => i.name === 'p.delta1.value')!;
    expect(delta.widest).toBe(DELTA_WIDEST);
    const formula = String(delta.bindings?.Text?.formula);
    for (const [seconds, text] of READINGS) {
      const drawn = evalNcalc(formula, frame(seconds));
      expect({ seconds, drawn, fits: widthAsDrawn(delta, String(drawn)) <= widthAsDrawn(delta, DELTA_WIDEST) }).toEqual({ seconds, drawn: text, fits: true });
    }
  });

  test('and in the label of the narrow form, which is measured from `S1 · +99.99`', () => {
    const label = row(240).find((i) => i.name === 'p.s1.label')!;
    const formula = String(label.bindings?.Text?.formula);
    for (const [seconds, text] of READINGS) {
      const drawn = evalNcalc(formula, frame(seconds));
      expect({ seconds, drawn, fits: widthAsDrawn(label, String(drawn)) <= widthAsDrawn(label, label.widest!) }).toEqual({ seconds, drawn: `S1 · ${text}`, fits: true });
    }
  });
});

/**
 * #614: before a sector is timed, `sectorDelta` is nought less nought, and the delta page and the pit
 * wall's panel drew it `+0.00`, a sector driven exactly to your best, on every out-lap. The label of
 * a narrow zone already said nothing there; the fields now say so too.
 */
describe('a sector delta with nothing to compare', () => {
  const LAST = 'DataCorePlugin.GameData.Sector1LastLapTime';
  const BEST = 'DataCorePlugin.GameData.Sector1BestTime';
  /** No time yet, a time with no best to hold it against, and SimHub's zero for a sector not yet run. */
  const EMPTY: readonly Record<string, number>[] = [{}, { [LAST]: 30.12 }, { [LAST]: 0, [BEST]: 30 }, { [LAST]: 0, [BEST]: 0 }];

  /** The sector-one delta of the delta page, in a zone, and of the pit wall's panel, wide enough for six. */
  const deltas = (): TextItem[] => {
    const page = MODULES.find((m) => m.id === 'delta')!;
    const items = [...walkItems(page.build({ frame: rect(0, 0, SHAPE_ARCHETYPES.wide.width, SHAPE_ARCHETYPES.wide.height), density: 'zone', prefix: 'delta.' }))];
    const field = items.find((i): i is TextItem => i.kind === 'text' && i.name === 'delta.s1.value');
    const panel = row(1248).find((i) => i.name === 'p.delta1.value');
    if (field === undefined || panel === undefined) throw new Error('no sector-one delta to test');
    return [field, panel];
  };

  test('draws the placeholder, not a signed zero', () => {
    for (const item of deltas()) {
      const formula = String(item.bindings?.Text?.formula);
      for (const props of EMPTY) expect({ item: item.name, props, drawn: evalNcalc(formula, props) }).toEqual({ item: item.name, props, drawn: '--' });
      // And the figure once there is one.
      expect({ item: item.name, drawn: evalNcalc(formula, { [LAST]: 29.83, [BEST]: 30.12 }) }).toEqual({ item: item.name, drawn: '−0.29' });
      expect({ item: item.name, drawn: evalNcalc(formula, { [LAST]: 30.12, [BEST]: 30.12 }) }).toEqual({ item: item.name, drawn: '+0.00' });
    }
  });

  test('draws it in the sector colour, which is dim while the sector has no time', () => {
    // Read off the formula rather than evaluated, since the purple branch asks SimHub for the
    // session's best split, which the evaluator does not answer.
    expect(sectorColour(1).startsWith(`if(!(${hasTime(sectorLast(1))}), '${ds.color.text.dim}'`)).toBe(true);
    for (const item of deltas()) expect({ item: item.name, colour: item.bindings?.TextColor?.formula }).toEqual({ item: item.name, colour: sectorColour(1) });
  });

  test('and the label of the narrow form is the bare sector number until there is a delta', () => {
    const label = row(240).find((i) => i.name === 'p.s1.label')!;
    const formula = String(label.bindings?.Text?.formula);
    for (const props of EMPTY) expect({ props, drawn: evalNcalc(formula, props) }).toEqual({ props, drawn: 'S1' });
    expect(evalNcalc(formula, { [LAST]: 29.83, [BEST]: 30.12 })).toBe('S1 · −0.29');
  });
});
