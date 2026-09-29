/**
 * The delta's three references, read off what every site that draws the live delta actually draws.
 *
 * Five places draw the delta to the lap the driver chose: card 3, the delta module's number, Lap
 * times' delta, the lap pop-up's second figure and the pit wall's Lap delta panel. They share one
 * reading, `referenceDelta()`, and what is worth pinning is not that they contain it but that each of
 * them, evaluated, draws the reference the setting names and none of the other two. Every frame below
 * gives the three references different values for that reason: a site that read the wrong one would
 * draw a number that belongs to another reference, and the test would say which.
 *
 * The third reference is iRacing's own live delta to the last lap (#322), which SimHub does not
 * publish; it is read from raw telemetry and gated on iRacing's `_OK`, so the cases where it is not
 * there -- no lap yet, another sim, no telemetry at all -- are pinned as well. iRacing publishes it
 * as a float, so every frame gives it as a {@link Single}, which SimHub's `format` does not sign, and
 * what a site draws is the float's figure.
 */
import { describe, expect, test } from 'bun:test';
import type { Item, TextItem } from '../src/generator.ts';
import { delta as deltaCard } from '../src/cards/delta.ts';
import { LAP_POP_UP, popUp, POP_UP_HEIGHT, POP_UP_WIDTH } from '../src/components/popUp.ts';
import { DEFAULTS, DELTA_REFERENCES } from '../src/contract.ts';
import { measureText } from '../src/design/advances.ts';
import { ncalc } from '../src/generator.ts';
import { rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { lapDeltaPanel } from '../src/screens/pitwall.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import type { Density } from '../src/second/density.ts';
import { LAST_LAP_DELTA, REFERENCE_LABEL_WIDEST, lastLapDelta, referenceDelta, referenceLabel } from '../src/second/values.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, Single, type Props } from './ncalcEval.ts';

const SESSION = 'PersistantTrackerPlugin.SessionBestLiveDeltaSeconds';
const ALLTIME = 'PersistantTrackerPlugin.AllTimeBestLiveDeltaSeconds';
const LAST = `DataCorePlugin.GameRawData.Telemetry.${LAST_LAP_DELTA}`;
const LAST_OK = `${LAST}_OK`;
const REFERENCE = 'OpenDash.DeltaReference';

const text = (items: readonly Item[], name: string): TextItem => {
  const found = [...walkItems(items)].find((i) => i.name === name);
  if (found?.kind !== 'text') throw new Error(`${name} is not a text item`);
  return found;
};

const formula = (item: TextItem, target: 'Text' | 'TextColor'): string | undefined => {
  const b = item.bindings?.[target];
  return b && typeof b.formula === 'string' ? b.formula : undefined;
};

const moduleItems = (id: string, density: Density, width: number, height: number): Item[] => {
  const module = MODULES.find((m) => m.id === id);
  if (!module) throw new Error(`no ${id} module`);
  return [...walkItems(module.build({ frame: rect(0, 0, width, height), density, prefix: `${id}.` }))];
};

/** A site that draws the live delta: what it writes, and how it colours it where it colours it. */
interface Site {
  name: string;
  /** The card alone draws a level delta as a bare "0.00"; everywhere else keeps the sign `format` writes. */
  bareAtRest: boolean;
  text: string;
  colour?: string;
}

const site = (name: string, item: TextItem, bareAtRest = false): Site => {
  const t = formula(item, 'Text');
  if (t === undefined) throw new Error(`${name} has no Text formula`);
  return { name, bareAtRest, text: t, colour: formula(item, 'TextColor') };
};

const wide = SHAPE_ARCHETYPES.wide;
const SITES: readonly Site[] = [
  site('card 3', text(deltaCard.build(rect(0, 0, 255, 187), 'delta.'), 'delta.value'), true),
  site('delta module', text(moduleItems('delta', 'zone', wide.width, wide.height), 'delta.delta.value')),
  site('Lap times', text(moduleItems('lapTimes', 'zone', wide.width, wide.height), 'lapTimes.delta.value')),
  site('lap pop-up', text([popUp(rect(0, 0, POP_UP_WIDTH, POP_UP_HEIGHT), LAP_POP_UP)], 'popUp.lap.secondary')),
  site('pit wall', text(lapDeltaPanel('lapDelta', rect(0, 0, 640, 300)), 'lapDelta.delta.value')),
];

/** What a driver reads: the figure, and the colour it is drawn in where the site chooses one. */
const drawn = (s: Site, props: Props) => ({ text: evalNcalc(s.text, props), colour: s.colour === undefined ? undefined : evalNcalc(s.colour, props) });

/**
 * The figure a reading draws at two decimals, written independently of the evaluator's `format`:
 * a true minus, a plus for anything not negative, and the card's bare "0.00" inside its deadband,
 * which is strictly under half a hundredth. The readings used are chosen away from a half-cent, so no
 * rounding rule is being tested here; `deltaPrecision.test.ts` tests the band's edge, at both
 * precisions.
 */
const figure = (seconds: number, bareAtRest: boolean): string => {
  if (bareAtRest && Math.abs(seconds) < 0.005) return '0.00';
  return `${seconds < 0 ? '−' : '+'}${Math.abs(seconds).toFixed(2)}`;
};

/** The colour a figure should carry, read off the figure itself: level, faster or slower. */
const colourOf = (figureText: string): string => {
  if (/^[+−]?0\.0+$/.test(figureText)) return ds.purpose.delta.zero;
  return figureText.startsWith('−') ? ds.purpose.delta.faster : ds.purpose.delta.slower;
};

/**
 * A frame in which each reference reads something different, the last lap's with iRacing's own flag,
 * and as the float iRacing publishes it.
 */
const frame = (reference: string | undefined, values: { session?: number | null; alltime?: number | null; last?: number | null; ok?: boolean | null }): Props => {
  const props: Props = {};
  if (reference !== undefined) props[REFERENCE] = reference;
  if (values.session !== undefined) props[SESSION] = values.session;
  if (values.alltime !== undefined) props[ALLTIME] = values.alltime;
  if (values.last !== undefined) props[LAST] = typeof values.last === 'number' ? new Single(values.last) : values.last;
  if (values.ok !== undefined) props[LAST_OK] = values.ok;
  return props;
};

/** The readings each reference is given in turn, away from the deadband's edge and from a half-cent. */
const READINGS = [-0.21, 0.21, -0.004, 0.004, -1.23, 0.51, -12.34, 12.34, 0] as const;

describe('the delta reference (#322)', () => {
  test('the evaluator reads a raw telemetry boolean the way NCalc does', () => {
    // A raw boolean reaches a binding as true or false, and the fallback is the bare literal, so
    // an absent flag is false rather than the string 'false', which is truthy in the evaluator.
    expect(evalNcalc("if(isnull([X], false), 'a', 'b')", {})).toBe('b');
    expect(evalNcalc("if(isnull([X], false), 'a', 'b')", { X: false })).toBe('b');
    expect(evalNcalc("if(isnull([X], false), 'a', 'b')", { X: true })).toBe('a');
  });

  test('the contract offers three references and defaults to the session best', () => {
    expect(DELTA_REFERENCES).toEqual(['session', 'alltime', 'lastlap']);
    expect(DEFAULTS.DeltaReference).toBe('session');
  });

  test('every site reads the one reading the second screens share', () => {
    for (const s of SITES) expect({ site: s.name, reads: s.text.includes(referenceDelta()) }).toEqual({ site: s.name, reads: true });
  });

  // No setting, the default, the other two, and a value the plugin would never publish: an unknown
  // reference is the session best, which is what the plugin's own normaliser makes of it.
  const CASES: readonly { reference: string | undefined; reads: 'session' | 'alltime' | 'last' }[] = [
    { reference: undefined, reads: 'session' },
    { reference: 'session', reads: 'session' },
    { reference: 'alltime', reads: 'alltime' },
    { reference: 'lastlap', reads: 'last' },
    { reference: 'previous', reads: 'session' },
  ];

  for (const { reference, reads } of CASES) {
    test(`with the reference ${reference ?? 'absent'}, every site draws the ${reads} delta and colours it by what it draws`, () => {
      for (const s of SITES) {
        for (const reading of READINGS) {
          // The other two references are given readings no site could round to this one's.
          const others = reading + 3.33;
          const props = frame(reference, {
            session: reads === 'session' ? reading : others,
            alltime: reads === 'alltime' ? reading : others + 1,
            last: reads === 'last' ? reading : others + 2,
            ok: true,
          });
          // The last lap's reading is iRacing's float, so its figure is the float's.
          const expected = figure(reads === 'last' ? Math.fround(reading) : reading, s.bareAtRest);
          const got = drawn(s, props);
          expect({ site: s.name, reference, reading, text: got.text }).toEqual({ site: s.name, reference, reading, text: expected });
          if (got.colour !== undefined) expect({ site: s.name, reference, reading, colour: got.colour }).toEqual({ site: s.name, reference, reading, colour: colourOf(expected) });
        }
      }
    });
  }

  test('the last lap is read only while iRacing says it has one, and is level otherwise', () => {
    // `_OK` false is iRacing before a lap has been completed; absent is another sim, or no
    // telemetry at all. Level is what the session and all-time references draw with no lap to
    // compare against, since SimHub publishes 0 there, so the three agree about "nothing yet".
    const level = [
      { last: -0.4, ok: false },
      { last: -0.4, ok: undefined },
      { last: -0.4, ok: null },
      { last: undefined, ok: true },
      { last: null, ok: true },
      { last: undefined, ok: undefined },
    ] as const;
    for (const s of SITES) {
      expect(drawn(s, frame('lastlap', { session: 0.7, alltime: 0.9, last: -0.4, ok: true })).text).toBe(figure(Math.fround(-0.4), s.bareAtRest));
      for (const values of level) {
        const got = drawn(s, frame('lastlap', { session: 0.7, alltime: 0.9, ...values }));
        expect({ site: s.name, values, text: got.text }).toEqual({ site: s.name, values, text: figure(0, s.bareAtRest) });
        if (got.colour !== undefined) expect({ site: s.name, values, colour: got.colour }).toEqual({ site: s.name, values, colour: ds.purpose.delta.zero });
      }
    }
    expect(evalNcalc(lastLapDelta(), {})).toBe(0);
  });

  test("the last lap is signed although iRacing's float is not", () => {
    // SimHub's `format` writes its `+` only for a double, a decimal or an int, and a raw iRacing float
    // reaches it as a Single, so the reading has to be made a double before it is formatted.
    const bare = (x: number): unknown => evalNcalc(ncalc.signed(ncalc.isnull('[X]', '0'), '0.00'), { X: new Single(x) });
    expect(bare(0.21)).toBe('0.21');
    expect(bare(0)).toBe('0.00');
    expect(bare(-0.004)).toBe('0.00');
    expect(bare(-0.21)).toBe('−0.21');
    const promoted = (x: number): unknown => evalNcalc(ncalc.signed(lastLapDelta(), '0.00'), { [LAST]: new Single(x), [LAST_OK]: true });
    expect(promoted(0.21)).toBe('+0.21');
    expect(promoted(0)).toBe('+0.00');
    expect(promoted(-0.004)).toBe('−0.00');
    expect(promoted(-0.21)).toBe('−0.21');
    // By a double. NCalc reads a bare `1` as an Int32, and a Single times an Int32 is still a Single;
    // the evaluator's numbers are all doubles and cannot tell `* 1` from `* 1.0`, so the literal is
    // the one part of the formula pinned by its text.
    expect(lastLapDelta()).toContain(' * (1.0)');
  });

  test('the last lap reading reaches no site that is not asked for it', () => {
    // iRacing publishes the last-lap delta whatever the setting says; a site that leaked it into the
    // default would draw a comparison the driver never chose.
    for (const s of SITES) {
      for (const reference of [undefined, 'session', 'alltime']) {
        const got = drawn(s, frame(reference, { session: -0.21, alltime: -0.21, last: 5.55, ok: true }));
        expect({ site: s.name, reference, text: got.text }).toEqual({ site: s.name, reference, text: figure(-0.21, s.bareAtRest) });
      }
    }
  });

  test('the caption names the reference, and "vs last lap" is not the lap review\'s "vs previous"', () => {
    const caption = (reference: string | undefined): unknown => evalNcalc(referenceLabel(), frame(reference, {}));
    expect(caption(undefined)).toBe('vs session best');
    expect(caption('session')).toBe('vs session best');
    expect(caption('alltime')).toBe('vs all-time best');
    expect(caption('lastlap')).toBe('vs last lap');
    expect(caption('previous')).toBe('vs session best');
  });

  test('every caption fits the one widest the delta module and the pit wall are measured by', () => {
    const captions = DELTA_REFERENCES.map((reference) => evalNcalc(referenceLabel(), frame(reference, {})) as string);
    for (const size of [12, 13, 15]) {
      const room = measureText('BarlowMedium', REFERENCE_LABEL_WIDEST, size);
      for (const c of captions) expect({ caption: c, size, fits: measureText('BarlowMedium', c, size) <= room }).toEqual({ caption: c, size, fits: true });
    }
    // Both forms of the module's caption, beside the number and under it, and the pit wall's.
    const bound = [
      ...moduleItems('delta', 'zone', wide.width, wide.height),
      ...moduleItems('delta', 'compact', wide.width, wide.height),
      ...walkItems(lapDeltaPanel('lapDelta', rect(0, 0, 640, 300))),
    ].filter((i): i is TextItem => i.kind === 'text' && formula(i, 'Text') === referenceLabel());
    expect(bound.length).toBe(3);
    for (const item of bound) expect({ name: item.name, widest: item.widest }).toEqual({ name: item.name, widest: REFERENCE_LABEL_WIDEST });
  });

  test("Lap times says what the delta is to, and keeps the canvas's words for the driver's own bests", () => {
    const label = text(moduleItems('lapTimes', 'zone', wide.width, wide.height), 'lapTimes.delta.label');
    const bound = formula(label, 'Text');
    if (bound === undefined) throw new Error('the Lap times delta label is not bound');
    expect(label.text).toBe('Delta to your best');
    expect(label.widest).toBe('Delta to your best');
    expect(evalNcalc(bound, frame(undefined, {}))).toBe('Delta to your best');
    expect(evalNcalc(bound, frame('session', {}))).toBe('Delta to your best');
    expect(evalNcalc(bound, frame('alltime', {}))).toBe('Delta to your best');
    expect(evalNcalc(bound, frame('lastlap', {}))).toBe('Delta to last lap');
    expect(measureText('BarlowMedium', 'Delta to last lap', 13)).toBeLessThan(measureText('BarlowMedium', 'Delta to your best', 13));
  });
});
