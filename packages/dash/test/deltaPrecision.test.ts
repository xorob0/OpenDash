/**
 * The live delta to hundredths or to thousandths, read off what the five surfaces that draw it
 * actually draw (#322).
 *
 * `OpenDash.DeltaPrecision` chooses between two literal formats, since `format` takes only a literal
 * pattern, and every box that draws the delta is cut once for the longer of the two, since a box cannot
 * change its cells at runtime. So three things are worth pinning, and none of them is the shape of a
 * formula. Each surface draws the reference the driver chose, to the places the driver chose, whatever
 * the other setting says. The colour says what the figure says: a figure drawn as a zero is level, and
 * a figure drawn as anything else is faster or slower, at both precisions and on both sides of the
 * band's edge. And every reading a binding can draw fits the `widest` its item declares, in the item's
 * own cells, which is what the fit tests measure the box by.
 *
 * The expected figures here are written with JavaScript's `toFixed`, which is also what the evaluator
 * formats with. .NET rounds a decimal half away from zero where `toFixed` rounds the binary double, so
 * the two part where a decimal half is stored just under it: 1.2345 to three places is `1.234` here
 * and `1.235` on the dash, and 9.9995 is `9.999` here and `10.000` on the dash. That is a rounding of
 * the last place, not a choice of precision, and the fit test below measures the .NET roundings too.
 */
import { describe, expect, test } from 'bun:test';
import type { Item, Rect, TextItem } from '../src/generator.ts';
import { composePackages } from '../src/build.ts';
import { delta as deltaCard } from '../src/cards/delta.ts';
import { LAP_POP_UP, popUp, POP_UP_HEIGHT, POP_UP_WIDTH } from '../src/components/popUp.ts';
import { DEFAULTS, DELTA_PRECISIONS, DELTA_REFERENCES } from '../src/contract.ts';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { COMPANION_SIZES, companionGeometry } from '../src/screens/companion.ts';
import { lapDeltaPanel } from '../src/screens/pitwall.ts';
import type { Density } from '../src/second/density.ts';
import { charsOfText, textWidth } from '../src/second/drawn.ts';
import { contentRect } from '../src/second/layout.ts';
import { CHARS, LAST_LAP_DELTA, REFERENCE_DELTA_WIDEST, referenceDelta, referenceDeltaText } from '../src/second/values.ts';
import { ds } from '../src/tokens.ts';
import { expressionsOf, itemsOf, walkItems } from '../src/walk.ts';
import { evalNcalc, Single, type Props } from './ncalcEval.ts';

const SESSION = 'PersistantTrackerPlugin.SessionBestLiveDeltaSeconds';
const ALLTIME = 'PersistantTrackerPlugin.AllTimeBestLiveDeltaSeconds';
const LAST = `DataCorePlugin.GameRawData.Telemetry.${LAST_LAP_DELTA}`;
const LAST_OK = `${LAST}_OK`;
const REFERENCE = 'OpenDash.DeltaReference';
const PRECISION = 'OpenDash.DeltaPrecision';

const texts = (items: Iterable<Item>): TextItem[] => [...items].filter((i): i is TextItem => i.kind === 'text');

const named = (items: readonly Item[], name: string): TextItem => {
  const found = texts(walkItems(items)).find((i) => i.name === name);
  if (found === undefined) throw new Error(`no text item ${name}`);
  return found;
};

const formula = (item: TextItem, target: 'Text' | 'TextColor' | 'Left'): string | undefined => {
  const b = item.bindings?.[target];
  return b && typeof b.formula === 'string' ? b.formula : undefined;
};

const moduleItems = (id: string, frame: Rect, density: Density): Item[] => {
  const module = MODULES.find((m) => m.id === id);
  if (!module) throw new Error(`no ${id} module`);
  return [...walkItems(module.build({ frame, density, prefix: `${id}.` }))];
};

/** One surface that draws the live delta, built into one box it is drawn in. */
interface Site {
  name: string;
  item: TextItem;
}

/**
 * The five surfaces, each in the boxes it is really drawn in: the card at the three rungs, the delta
 * page and Lap times on both companions, a zone, a panel and the narrowest compact zone, the lap pop-up
 * and the pit wall's panel. A reading that fits one box and not another is found here rather than on a
 * rim.
 */
const companionBox = (i: number): Rect => contentRect(companionGeometry(COMPANION_SIZES[i]!).module, 'companion');
const MODULE_BOXES: readonly { name: string; frame: Rect; density: Density }[] = [
  { name: 'companion', frame: companionBox(0), density: 'companion' },
  { name: 'portrait companion', frame: companionBox(1), density: 'companion' },
  { name: 'zone', frame: rect(0, 0, 445, 282), density: 'zone' },
  { name: 'panel', frame: rect(0, 0, 607, 196), density: 'panel' },
  { name: 'compact zone', frame: rect(0, 0, 225, 290), density: 'compact' },
];
const SITES: readonly Site[] = [
  ...[rect(0, 0, 255, 187), rect(0, 0, 223, 156), rect(0, 0, 140, 104)].map((slot) => ({
    name: `card 3 in ${slot.width}x${slot.height}`,
    item: named(deltaCard.build(slot, 'delta.'), 'delta.value'),
  })),
  ...MODULE_BOXES.flatMap((box) => [
    { name: `the delta page, ${box.name}`, item: named(moduleItems('delta', box.frame, box.density), 'delta.delta.value') },
    { name: `Lap times, ${box.name}`, item: named(moduleItems('lapTimes', box.frame, box.density), 'lapTimes.delta.value') },
  ]),
  { name: 'the lap pop-up', item: named([popUp(rect(0, 0, POP_UP_WIDTH, POP_UP_HEIGHT), LAP_POP_UP)], 'popUp.lap.secondary') },
  { name: 'the pit wall', item: named(lapDeltaPanel('lapDelta', rect(0, 0, 640, 300)), 'lapDelta.delta.value') },
];

const textOf = (item: TextItem): string => {
  const t = formula(item, 'Text');
  if (t === undefined) throw new Error(`${item.name} has no Text formula`);
  return t;
};

/** What a driver reads: the figure, and its colour where the surface chooses one. */
const drawn = (s: Site, props: Props): { text: string; colour: unknown } => {
  const colour = formula(s.item, 'TextColor');
  return { text: String(evalNcalc(textOf(s.item), props)), colour: colour === undefined ? undefined : evalNcalc(colour, props) };
};

/** The places a precision draws, and what an unknown value is taken for: the default. */
const placesOf = (precision: string | undefined): number => (precision === 'thousandths' ? 3 : 2);

/**
 * The places a reading is drawn to: the precision's, except that a delta of 100 s or more is drawn to
 * two, since a third place would take it past the six cells.
 */
const placesDrawn = (places: number, seconds: number): number => (places === 3 && Math.abs(seconds) >= 99.9995 ? 2 : places);

/**
 * The figure a reading draws, written independently of the binding: a true minus, a plus for anything
 * not negative, and a bare zero inside the band, which is half a unit of the last place. Every surface
 * draws the bare zero, card 3 no longer alone (#614).
 */
const figure = (seconds: number, places: number): string => {
  if (Math.abs(seconds) < 0.5 * 10 ** -places) return `0.${'0'.repeat(places)}`;
  return `${seconds < 0 ? '−' : '+'}${Math.abs(seconds).toFixed(placesDrawn(places, seconds))}`;
};

/** The colour a figure should carry, read off the figure itself: level, faster or slower. */
const colourOf = (text: string): string => {
  if (/^[+−]?0\.0+$/.test(text)) return ds.purpose.delta.zero;
  return text.startsWith('−') ? ds.purpose.delta.faster : ds.purpose.delta.slower;
};

/**
 * A frame in which the three references read different things, so a site reading the wrong one shows.
 * The last lap's is iRacing's float, which is how it reaches a binding.
 */
const frame = (reference: string | undefined, precision: string | undefined, reading: number): Props => {
  const reads = reference === 'alltime' ? 'alltime' : reference === 'lastlap' ? 'last' : 'session';
  const other = reading + 3.333;
  const props: Props = {
    [SESSION]: reads === 'session' ? reading : other,
    [ALLTIME]: reads === 'alltime' ? reading : other + 1,
    [LAST]: new Single(reads === 'last' ? reading : other + 2),
    [LAST_OK]: true,
  };
  if (reference !== undefined) props[REFERENCE] = reference;
  if (precision !== undefined) props[PRECISION] = precision;
  return props;
};

/** The reading a reference draws: the last lap's is the float iRacing publishes. */
const asRead = (reference: string | undefined, reading: number): number => (reference === 'lastlap' ? Math.fround(reading) : reading);

/**
 * The readings, from level to past the longest the budget is cut for, and both sides of both edges of
 * the band: 0.0005 is the edge at thousandths and 0.005 at hundredths, and the readings just inside
 * each are the ones a band on the wrong side of its edge would colour wrongly. 99.9994 is the last
 * reading drawn to three places, and 123.456 is a long stop's, drawn to two at either precision.
 */
const MAGNITUDES = [0, 0.0004, 0.00049, 0.0005, 0.004, 0.0049, 0.005, 0.21, 0.214, 1.2345, 9.9995, 9.9996, 12.345, 99.999, 99.9994, 123.456] as const;
const READINGS: readonly number[] = [...new Set(MAGNITUDES.flatMap((m) => [m, -m]))];

const REFERENCES: readonly (string | undefined)[] = [undefined, ...DELTA_REFERENCES, 'previous'];
const PRECISIONS: readonly (string | undefined)[] = [undefined, ...DELTA_PRECISIONS, 'millis'];

describe('the delta precision (#322)', () => {
  test('the contract offers two precisions and draws hundredths without the plugin', () => {
    expect(DELTA_PRECISIONS).toEqual(['hundredths', 'thousandths']);
    expect(DEFAULTS.DeltaPrecision).toBe('hundredths');
    // No plugin at all: no reference, no precision, and every surface draws the session best to two
    // places, which is what every package drew before either setting existed.
    for (const s of SITES) {
      const props: Props = { [SESSION]: -0.214, [ALLTIME]: 0.5, [LAST]: new Single(0.7), [LAST_OK]: true };
      expect({ site: s.name, text: drawn(s, props).text }).toEqual({ site: s.name, text: '−0.21' });
    }
  });

  test('every surface reads the one reading and the one format', () => {
    for (const s of SITES) {
      const text = textOf(s.item);
      expect({ site: s.name, reads: text.includes(referenceDelta()) }).toEqual({ site: s.name, reads: true });
      expect({ site: s.name, precision: text.includes(`[${PRECISION}]`) }).toEqual({ site: s.name, precision: true });
    }
  });

  for (const precision of PRECISIONS) {
    test(`at ${precision ?? 'no'} precision, every surface draws the reference named to ${placesOf(precision)} places, and colours it by what it draws`, () => {
      for (const s of SITES) {
        for (const reference of REFERENCES) {
          for (const reading of READINGS) {
            const got = drawn(s, frame(reference, precision, reading));
            const expected = figure(asRead(reference, reading), placesOf(precision));
            const at = { site: s.name, reference, precision, reading };
            expect({ ...at, text: got.text }).toEqual({ ...at, text: expected });
            if (got.colour !== undefined) expect({ ...at, colour: got.colour }).toEqual({ ...at, colour: colourOf(got.text) });
          }
        }
      }
    });
  }

  test('the band is half a unit of the last place drawn, and its edge is drawn as a digit', () => {
    const card = SITES[0]!;
    const page = SITES.find((s) => s.name.startsWith('the delta page'))!;
    const at = (s: Site, precision: string, reading: number) => drawn(s, frame('session', precision, reading));
    // Hundredths: four thousandths is level, and five is a hundredth, since .NET rounds a half away
    // from zero; a band that took 0.005 in would draw "+0.01" in the resting white.
    expect(at(page, 'hundredths', 0.004)).toEqual({ text: '0.00', colour: ds.purpose.delta.zero });
    expect(at(page, 'hundredths', 0.005)).toEqual({ text: '+0.01', colour: ds.purpose.delta.slower });
    expect(at(page, 'hundredths', -0.005)).toEqual({ text: '−0.01', colour: ds.purpose.delta.faster });
    expect(at(card, 'hundredths', 0.004)).toEqual({ text: '0.00', colour: ds.purpose.delta.zero });
    expect(at(card, 'hundredths', 0.005)).toEqual({ text: '+0.01', colour: ds.purpose.delta.slower });
    // Thousandths: the band narrows with the figure, so four thousandths is a reading and is coloured.
    expect(at(page, 'thousandths', 0.004)).toEqual({ text: '+0.004', colour: ds.purpose.delta.slower });
    expect(at(page, 'thousandths', 0.0004)).toEqual({ text: '0.000', colour: ds.purpose.delta.zero });
    expect(at(page, 'thousandths', -0.0005)).toEqual({ text: '−0.001', colour: ds.purpose.delta.faster });
    expect(at(card, 'thousandths', 0.0004)).toEqual({ text: '0.000', colour: ds.purpose.delta.zero });
    expect(at(card, 'thousandths', -0.004)).toEqual({ text: '−0.004', colour: ds.purpose.delta.faster });
  });

  test('a level delta is the bare zero on every surface, at both precisions and on both sides of zero (#614)', () => {
    // Card 3 drew "0.00" off a branch of its own and the other four drew "+0.00", or "−0.00" a
    // thousandth under, so one reading looked like two. The branch is the shared text's now, and the
    // card draws that text and nothing around it.
    expect(textOf(SITES[0]!.item)).toBe(referenceDeltaText(referenceDelta()));
    const level = { hundredths: [0, 0.004, -0.004, 0.0049, -0.0049], thousandths: [0, 0.0004, -0.0004, 0.00049, -0.00049] } as const;
    for (const s of SITES) {
      for (const precision of DELTA_PRECISIONS) {
        for (const reading of level[precision]) {
          const got = drawn(s, frame('session', precision, reading));
          const at = { site: s.name, precision, reading };
          expect({ ...at, text: got.text }).toEqual({ ...at, text: `0.${'0'.repeat(placesOf(precision))}` });
          if (got.colour !== undefined) expect({ ...at, colour: got.colour }).toEqual({ ...at, colour: ds.purpose.delta.zero });
        }
      }
    }
  });

  test('a delta of a hundred seconds or more is drawn to hundredths at either precision', () => {
    // A long stop takes the delta there, and a third place would need a seventh cell: `+100.000`.
    for (const s of SITES) {
      const at = (reading: number) => drawn(s, frame('session', 'thousandths', reading)).text;
      expect({ site: s.name, text: at(99.9994) }).toEqual({ site: s.name, text: '+99.999' });
      expect({ site: s.name, text: at(99.9995) }).toEqual({ site: s.name, text: '+100.00' });
      expect({ site: s.name, text: at(-123.456) }).toEqual({ site: s.name, text: '−123.46' });
      expect({ site: s.name, text: at(999.99) }).toEqual({ site: s.name, text: '+999.99' });
    }
  });

  test('with no lap to compare against, every surface is level at either precision', () => {
    for (const s of SITES) {
      for (const precision of DELTA_PRECISIONS) {
        const places = placesOf(precision);
        const got = drawn(s, { [REFERENCE]: 'lastlap', [PRECISION]: precision, [SESSION]: 0.7, [LAST]: new Single(-0.4), [LAST_OK]: false });
        expect({ site: s.name, precision, text: got.text }).toEqual({ site: s.name, precision, text: figure(0, places) });
        if (got.colour !== undefined) expect({ site: s.name, precision, colour: got.colour }).toEqual({ site: s.name, precision, colour: ds.purpose.delta.zero });
      }
    }
  });

  test('the budget is cut for the widest reading and every box declares it', () => {
    expect(REFERENCE_DELTA_WIDEST).toBe('−12.345');
    expect(charsOfText(REFERENCE_DELTA_WIDEST, SITES[0]!.item.monospace!)).toEqual(CHARS.referenceDelta);
    // The sector deltas, the lap review and the lap history keep the two-place budget.
    expect(CHARS.delta).toEqual({ digits: 5, specials: 1 });
    for (const s of SITES) {
      expect({ site: s.name, widest: s.item.widest }).toEqual({ site: s.name, widest: REFERENCE_DELTA_WIDEST });
      // The canvas's sample is the short end of the range, and stays the canvas's.
      expect({ site: s.name, sample: s.item.text }).toEqual({ site: s.name, sample: '−0.21' });
    }
  });

  test('every reading any surface can draw fits its widest in its own cells, and the widest fits the box', () => {
    // The .NET roundings the evaluator does not make: a half in decimal that is under a half in binary.
    const dotnet = ['+10.000', '−10.000', '+1.235', '−1.235'];
    // And the longest a long stop draws, which is to two places at either precision: every reading
    // under 1000 s fits.
    const longest = ['+100.00', '−123.46', '+999.99', '−999.99'];
    for (const s of SITES) {
      const mono = s.item.monospace;
      if (mono === undefined) throw new Error(`${s.name} is not monospaced`);
      // No wider than the box: a monospaced cell is already wider than the glyph set in it, which is
      // the rule secondScreens.test.ts holds a field to, and textFit.test.ts holds the cards to a
      // strict one on every face that ships.
      const room = textWidth(REFERENCE_DELTA_WIDEST, mono);
      expect({ site: s.name, fits: room <= s.item.rect.width }).toEqual({ site: s.name, fits: true });
      const drawnTexts = new Set([...dotnet, ...longest]);
      for (const precision of PRECISIONS) for (const reading of READINGS) drawnTexts.add(drawn(s, frame('session', precision, reading)).text);
      for (const text of drawnTexts) {
        expect({ site: s.name, text, fits: textWidth(text, mono) <= room }).toEqual({ site: s.name, text, fits: true });
      }
    }
  });

  test("the delta page's caption follows the figure it draws, at either precision and against every reference", () => {
    for (const box of MODULE_BOXES.filter((b) => b.density !== 'compact' && b.name !== 'portrait companion')) {
      const items = moduleItems('delta', box.frame, box.density);
      const value = named(items, 'delta.delta.value');
      const caption = texts(items).find((i) => i.name.startsWith('delta.delta.') && i !== value && formula(i, 'Left') !== undefined);
      if (caption === undefined) throw new Error(`the delta page in the ${box.name} box draws no caption beside the figure`);
      const left = formula(caption, 'Left')!;
      // Every reference, the last lap's float among them: a figure `format` wrote without its sign
      // would end a cell before the caption placed for it.
      for (const reference of DELTA_REFERENCES) {
        for (const precision of DELTA_PRECISIONS) {
          // Not at a half of the last place. The caption is placed by `round` and the figure written
          // by `format`, and at a half the two can round opposite ways, in the evaluator and on the
          // dash alike; where that carries a digit, as 9.9995 does to `10.000`, the caption would sit a
          // cell off for as long as the delta read exactly that, which is a frame at most.
          const half = (reading: number): boolean => {
            const seconds = Math.abs(asRead(reference, reading));
            return Math.abs(((seconds * 10 ** placesDrawn(placesOf(precision), seconds)) % 1) - 0.5) < 1e-6;
          };
          for (const reading of READINGS.filter((r) => !half(r))) {
            const props = frame(reference, precision, reading);
            const figureText = String(evalNcalc(textOf(value), props));
            // Rounded to the pixel, since a rect is laid on whole pixels and a binding is not.
            const gap = Math.round(Number(evalNcalc(left, props)) - (value.rect.left + textWidth(figureText, value.monospace!)));
            const at = { box: box.name, reference, precision, reading };
            expect({ ...at, gap }).toEqual({ ...at, gap: 10 });
          }
        }
      }
    }
  });

  test('the portrait companion draws the caption under the figure, which a three-place box no longer leaves room beside', () => {
    const items = moduleItems('delta', companionBox(1), 'companion');
    const value = named(items, 'delta.delta.value');
    const label = named(items, 'delta.delta.label');
    expect(label.rect.top).toBeGreaterThan(value.rect.top);
    // And the landscape companion still draws it beside.
    expect(texts(moduleItems('delta', companionBox(0), 'companion')).some((i) => i.name === 'delta.delta.label')).toBe(false);
  });

  test('only the live delta to the reference follows the precision', () => {
    // The sector deltas, the lap review's two and the lap history's column are other comparisons and
    // keep their own formats; an item that reads the precision is one that draws the reference's delta
    // or places something after it, and an item that writes the reference's delta reads the precision.
    const packages = composePackages({ version: '0.0.0-test', log: () => {} }, true);
    const readers = new Set<string>();
    for (const { pkg } of packages) {
      for (const dashboard of pkg.dashboards) {
        for (const item of itemsOf(dashboard)) {
          const precision = expressionsOf(item).filter((e) => e.includes(`[${PRECISION}]`));
          for (const e of precision) expect({ item: item.name, readsTheDelta: e.includes(referenceDelta()) }).toEqual({ item: item.name, readsTheDelta: true });
          if (precision.length > 0) readers.add(item.name);
          const text = item.kind === 'text' ? formula(item, 'Text') : undefined;
          if (text?.includes(referenceDelta())) expect({ item: item.name, precision: text.includes(`[${PRECISION}]`) }).toEqual({ item: item.name, precision: true });
        }
      }
    }
    // By name, the five and the caption after the delta page's figure, wherever a screen draws them.
    const kinds = new Set([...readers].map((name) => /(delta\.value|delta\.delta\.value|delta\.delta\.unit|lapTimes\.delta\.value|popUp\.lap\.secondary)$/.exec(name)?.[1] ?? name));
    expect([...kinds].sort()).toEqual(['delta.delta.unit', 'delta.delta.value', 'delta.value', 'lapTimes.delta.value', 'popUp.lap.secondary']);
  });
});
