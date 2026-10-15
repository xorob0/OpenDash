/**
 * The pit service order on a sim that publishes one and on one that does not (#564).
 *
 * Pit view and band D's `Refuel` read iRacing's raw `PitSvFlags` and `PitSvFuel`, which SimHub's
 * Assetto Corsa, ACC and Le Mans Ultimate readers never publish. Read as 0, the bits drew every
 * corner unticked, the fast repair and the tear-off off and the tyres `None`: a stop set to do
 * nothing, which on Assetto Corsa, whose stop is set in the game's own pit app, is a stop nobody set.
 *
 * So each page is read as a rig draws it, from the committed `pit` trace of each sim: every item's
 * `Visible`, its layers' included, then its `Text`. On Assetto Corsa the page says the service is
 * not published and still counts the pit time; on iRacing it draws the order it always drew.
 */
import { describe, expect, test } from 'bun:test';
import { ASSETTO_CORSA, IRACING, type Sim } from '../../../scripts/sims.ts';
import { frame, readTrace, traceDirOf } from '../../../scripts/trace.ts';
import { rect, type Rect } from '../src/design/geometry.ts';
import type { Item } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { PIT_SERVICE_MESSAGE } from '../src/modules/pitView.ts';
import type { Density } from '../src/second/density.ts';
import { NO_VALUE } from '../src/second/values.ts';
import { SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { bandPageItems } from '../src/zones/bandPages.ts';
import { drawingOf } from './moduleItems.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const pitView = MODULES.find((m) => m.id === 'pitView')!;

/** Every frame of a sim's `pit` trace, which is the scenario that stops the car in its box. */
const pitFrames = (sim: Sim): Props[] => {
  const trace = readTrace('pit', traceDirOf(sim));
  return Array.from({ length: trace.header.frames }, (_, i) => frame(trace, i));
};

const formulaOf = (item: Item, target: 'Visible' | 'Text'): string => {
  const binding = (item as { bindings?: Record<string, { formula?: unknown } | undefined> }).bindings?.[target];
  const formula = binding?.formula;
  if (formula === undefined) return '';
  return typeof formula === 'string' ? formula : (formula as { expression: string }).expression;
};

/** What a rig shows of `items` under `props`, by name: a hidden layer hides everything in it. */
function shown(items: readonly Item[], props: Props): { name: string; text?: string }[] {
  return items.flatMap((item) => {
    const visible = formulaOf(item, 'Visible');
    if (visible !== '' && evalNcalc(visible, props) !== true) return [];
    if (item.kind === 'layer') return shown(item.children, props);
    if (item.kind !== 'text') return [{ name: item.name }];
    const text = formulaOf(item, 'Text');
    return [{ name: item.name, text: text === '' ? item.text : String(evalNcalc(text, props)) }];
  });
}

/** The four shapes the catalogue draws, a zone body the faces produce, and the companion's page. */
const BOXES: { at: string; frame: Rect; density: Density }[] = [
  ...Object.entries(SHAPE_ARCHETYPES).map(([at, size]) => ({ at, frame: rect(0, 0, size.width, size.height), density: 'zone' as const })),
  { at: 'the 576 x 112 face zone', frame: rect(0, 0, 576, 112), density: 'zone' },
  { at: 'a companion page', frame: rect(0, 0, 1007, 211), density: 'companion' },
];

const page = (box: (typeof BOXES)[number]): Item[] => drawingOf(pitView, { frame: box.frame, density: box.density, prefix: 'p.' });

const SERVICE_WORDS = ['Refuel', 'Fast repair', 'Tear-off', 'Tyres', 'FL', 'FR', 'RL', 'RR'];
const REASON = PIT_SERVICE_MESSAGE.split(' · ')[1]!;

describe('pit view on Assetto Corsa, which publishes no pit service order', () => {
  const frames = pitFrames(ASSETTO_CORSA);

  test('the trace hands the page the order as null, which is what the page has to answer', () => {
    for (const props of frames) {
      expect(props['DataCorePlugin.GameRawData.Telemetry.PitSvFlags'] ?? null).toBeNull();
      expect(props['DataCorePlugin.GameRawData.Telemetry.PitSvFuel'] ?? null).toBeNull();
    }
  });

  for (const box of BOXES) {
    test(`says the service is not published and still counts the pit time, at ${box.at}`, () => {
      const items = page(box);
      for (const [i, props] of frames.entries()) {
        const drawn = shown(items, props);
        const texts = drawn.flatMap((d) => (d.text === undefined ? [] : [d.text]));
        // No option, no car, no order: nothing that reads as a stop somebody set.
        expect({ frame: i, service: texts.filter((t) => SERVICE_WORDS.includes(t)) }).toEqual({ frame: i, service: [] });
        expect({ frame: i, car: drawn.filter((d) => d.name.includes('.car.')).length }).toEqual({ frame: i, car: 0 });
        expect({ frame: i, zero: texts.filter((t) => /^0\.00?$/.test(t)) }).toEqual({ frame: i, zero: [] });
        // The notice, whole or as its reason where the box is narrow, and the pit time beside it.
        const notice = drawn.filter((d) => d.name.startsWith('p.unpublished.placeholder')).map((d) => d.text).join(' ');
        expect({ frame: i, notice: [PIT_SERVICE_MESSAGE.replace(' · ', ' '), REASON].includes(notice.replace(' · ', ' ')) }).toEqual({ frame: i, notice: true });
        expect({ frame: i, label: texts.includes('Pit time') }).toEqual({ frame: i, label: true });
        const time = drawn.find((d) => d.name === 'p.unpublished.pitTime.value')?.text;
        expect({ frame: i, time: /^\d+\.\d$/.test(time ?? '') }).toEqual({ frame: i, time: true });
      }
    });
  }
});

describe('pit view on iRacing, which does', () => {
  const frames = pitFrames(IRACING);

  for (const box of BOXES) {
    test(`draws the order it always drew and no notice, at ${box.at}`, () => {
      const items = page(box);
      for (const [i, props] of frames.entries()) {
        const drawn = shown(items, props);
        const texts = drawn.flatMap((d) => (d.text === undefined ? [] : [d.text]));
        expect({ frame: i, notice: texts.filter((t) => t.includes(REASON)) }).toEqual({ frame: i, notice: [] });
        expect({ frame: i, unpublished: drawn.filter((d) => d.name.startsWith('p.unpublished.')).length }).toEqual({ frame: i, unpublished: 0 });
        for (const word of ['Refuel', 'Fast repair', 'Tear-off', 'Pit time']) expect({ frame: i, word, drawn: texts.includes(word) }).toEqual({ frame: i, word, drawn: true });
        // The trace's order: 46.5 litres, every corner, and the tear-off without the repair (63).
        expect({ frame: i, refuel: drawn.find((d) => d.name === 'p.refuel.value')?.text }).toEqual({ frame: i, refuel: '46.5' });
      }
    });
  }
});

describe("band D's Refuel", () => {
  const field = (props: Props): string[] =>
    shown(bandPageItems('fuel', rect(0, 420, 1920, 60), '', true), props).flatMap((d) => (d.name === 'refuel.value' && d.text !== undefined ? [d.text] : []));

  test('draws no zero on Assetto Corsa, where there is no order to draw', () => {
    for (const props of pitFrames(ASSETTO_CORSA)) expect(field(props)).toEqual([NO_VALUE]);
  });

  test("and iRacing's order on iRacing", () => {
    for (const props of pitFrames(IRACING)) expect(field(props)).toEqual(['47']);
  });
});
