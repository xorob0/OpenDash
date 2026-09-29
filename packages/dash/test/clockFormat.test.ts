/**
 * The clock format, #324: both clocks of the day in either format, and every surface that draws one
 * holding the format it is given.
 *
 * The reading is checked by what a driver would see -- the expressions are evaluated rather than
 * compared as text -- and the surfaces by where a rig draws them: each item's `Left`, `Visible` and
 * `Text` bindings evaluated under the format, since a box is cut at build time and the format is read
 * at runtime, and the design-time rect of a word that is only drawn on one of the two says nothing on
 * its own. The pit wall header is held in `pitwallHeader.test.ts`, beside the rest of the strip.
 *
 * The ticket's trap is the one asserted most: a twelve-hour clock needs room for `AM` and `PM` that a
 * twenty-four-hour clock does not, and a box sized for the shorter clips the longer without a word
 * from WPF. So every surface is measured with the word, and every surface is also asked to give the
 * room back when the word is not there, which is what keeps a rig that never opens the setting on the
 * face it had.
 */
import { describe, expect, test } from 'bun:test';
import { CLOCK_FORMATS, CLOCK_FORMAT_SETTING, DEFAULTS, PROPERTY_PREFIX, BAR_FIELDS, barFieldSettingName, type ClockFormat } from '../src/contract.ts';
import { measureText } from '../src/design/advances.ts';
import { cells } from '../src/design/metrics.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { idleItems } from '../src/idle.ts';
import { textWidth } from '../src/second/drawn.ts';
import { CHARS, localClock, meridiemWidest, simClock, type TimeOfDay } from '../src/second/values.ts';
import { rect } from '../src/design/geometry.ts';
import { ZONE_FACES, bandCorners, faceItems, sizeOf } from '../src/zones/index.ts';
import { bindingExpression, faceOf } from './monoGlyphs.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const CLOCK_FORMAT = `${PROPERTY_PREFIX}.${CLOCK_FORMAT_SETTING}`;
const NOW = 'DataCorePlugin.CurrentDateTime';
const SIM = 'DataCorePlugin.GameRawData.Telemetry.SessionTimeOfDay';

/** A moment of the day, for each clock: the wall clock as a date, the sim's as seconds of day. */
const at = (hour: number, minute: number): Props => ({
  [NOW]: new Date(2026, 8, 29, hour, minute, 41),
  // A few seconds past the minute, as a real session clock always is, so a clock rounding its seconds
  // up instead of down would read the next minute and fail.
  [SIM]: hour * 3600 + minute * 60 + 41.7,
});

/** What each moment reads on each clock, in each format. */
const MOMENTS: { hour: number; minute: number; h24: string; h12: string; meridiem: 'AM' | 'PM' }[] = [
  { hour: 0, minute: 5, h24: '00:05', h12: '12:05', meridiem: 'AM' },
  { hour: 1, minute: 0, h24: '01:00', h12: '1:00', meridiem: 'AM' },
  { hour: 9, minute: 5, h24: '09:05', h12: '9:05', meridiem: 'AM' },
  { hour: 10, minute: 59, h24: '10:59', h12: '10:59', meridiem: 'AM' },
  { hour: 11, minute: 59, h24: '11:59', h12: '11:59', meridiem: 'AM' },
  { hour: 12, minute: 0, h24: '12:00', h12: '12:00', meridiem: 'PM' },
  { hour: 13, minute: 7, h24: '13:07', h12: '1:07', meridiem: 'PM' },
  { hour: 21, minute: 45, h24: '21:45', h12: '9:45', meridiem: 'PM' },
  { hour: 23, minute: 59, h24: '23:59', h12: '11:59', meridiem: 'PM' },
];

const CLOCKS: { name: string; clock: TimeOfDay }[] = [
  { name: 'wall', clock: localClock() },
  { name: 'sim', clock: simClock() },
];

describe('the reading', () => {
  test('the setting is two formats, and twenty-four hours is what a rig without the plugin draws', () => {
    expect([...CLOCK_FORMATS]).toEqual(['24h', '12h']);
    expect(DEFAULTS.ClockFormat).toBe('24h');
  });

  for (const { name, clock } of CLOCKS) {
    test(`the ${name} clock reads each moment in either format, and names its half of the day`, () => {
      for (const m of MOMENTS) {
        for (const format of CLOCK_FORMATS) {
          const props = { ...at(m.hour, m.minute), [CLOCK_FORMAT]: format };
          expect({ at: m.h24, format, text: evalNcalc(clock.text, props) }).toEqual({ at: m.h24, format, text: format === '12h' ? m.h12 : m.h24 });
          // Whatever the format, so that a surface only has to decide whether to draw it.
          expect({ at: m.h24, format, meridiem: evalNcalc(clock.meridiem, props) }).toEqual({ at: m.h24, format, meridiem: m.meridiem });
        }
        // No plugin, no setting: the clock every package drew before there was a choice.
        expect({ at: m.h24, text: evalNcalc(clock.text, at(m.hour, m.minute)) }).toEqual({ at: m.h24, text: m.h24 });
      }
    });

    test(`the ${name} clock says how wide it draws, so a word after it can stand against the figure`, () => {
      const mono = cells('SemiBold', 34);
      for (const m of MOMENTS) {
        for (const format of CLOCK_FORMATS) {
          const props = { ...at(m.hour, m.minute), [CLOCK_FORMAT]: format };
          const text = String(evalNcalc(clock.text, props));
          expect({ at: text, drawn: evalNcalc(clock.drawn(mono), props) }).toEqual({ at: text, drawn: textWidth(text, mono) });
        }
      }
    });
  }

  test('the sim clock takes its hour round the day, so a session that reaches midnight reads 00:00', () => {
    const { text, meridiem } = simClock();
    const props = { [SIM]: 86400 + 60, [CLOCK_FORMAT]: '24h' };
    expect(evalNcalc(text, props)).toBe('00:01');
    expect(evalNcalc(text, { ...props, [CLOCK_FORMAT]: '12h' })).toBe('12:01');
    expect(evalNcalc(meridiem, props)).toBe('AM');
  });

  test('both formats fit one budget of digits, which is why only the word needs room of its own', () => {
    const mono = cells('SemiBold', 34);
    for (const text of ['23:59', '12:59', '9:05']) {
      expect({ text, fits: textWidth(text, mono) <= textWidth('00:00', mono) }).toEqual({ text, fits: true });
    }
    expect(CHARS.timeOfDay).toEqual({ digits: 4, specials: 1 });
  });

  test('the word is measured by whichever of the two is wider in the face it is drawn in', () => {
    for (const face of ['BarlowMedium', 'BarlowCondensedSemiBold'] as const) {
      const widest = meridiemWidest(face);
      for (const word of ['AM', 'PM']) expect({ face, word, holds: measureText(face, word, 24) <= measureText(face, widest, 24) }).toEqual({ face, word, holds: true });
    }
  });
});

/** An item as a rig draws it under `props`: whether it is shown, where its box starts, and what it says. */
function asDrawn(item: TextItem, props: Props): { shown: boolean; left: number; text: string } {
  const visible = bindingExpression(item, 'Visible');
  const left = bindingExpression(item, 'Left');
  const text = bindingExpression(item, 'Text');
  return {
    shown: visible === '' || evalNcalc(visible, props) === true,
    left: left === '' ? item.rect.left : Number(evalNcalc(left, props)),
    text: text === '' ? item.text : String(evalNcalc(text, props)),
  };
}

/** Where an item's letters start and end when it draws `text` from `left`, by its alignment. */
function ink(item: TextItem, left: number, text: string): { start: number; end: number } {
  const width = item.monospace ? textWidth(text, item.monospace) : measureText(faceOf(item), text, item.fontSize);
  const start = item.hAlign === 'right' ? left + item.rect.width - width : item.hAlign === 'center' ? left + (item.rect.width - width) / 2 : left;
  return { start, end: start + width };
}

const texts = (items: readonly Item[]): TextItem[] => items.filter((i): i is TextItem => i.kind === 'text');

const named = (items: readonly TextItem[], name: string): TextItem => {
  const item = items.find((i) => i.name === name);
  if (item === undefined) throw new Error(`no item named ${name}`);
  return item;
};

/**
 * A value and the word after it, under each format and at each moment: the word is drawn on a
 * twelve-hour clock and on nothing else, and when it is drawn it stands `gap` after the figure.
 */
function holdsTheWord(where: string, value: TextItem, word: TextItem, props: Props, gap: { exactly: number } | { atLeast: number }): void {
  for (const m of MOMENTS) {
    for (const format of CLOCK_FORMATS) {
      const moment = { ...props, ...at(m.hour, m.minute), [CLOCK_FORMAT]: format };
      const v = asDrawn(value, moment);
      const w = asDrawn(word, moment);
      expect({ where, format, at: m.h24, value: v.shown, word: w.shown }).toEqual({ where, format, at: m.h24, value: true, word: format === '12h' });
      if (format !== '12h') continue;
      expect({ where, at: m.h24, text: v.text, word: w.text }).toEqual({ where, at: m.h24, text: m.h12, word: m.meridiem });
      const between = ink(word, w.left, w.text).start - ink(value, v.left, v.text).end;
      const placed = 'exactly' in gap ? Math.abs(between - gap.exactly) <= 0.5 : between >= gap.atLeast - 0.5;
      expect({ where, at: m.h12, between, placed }).toMatchObject({ placed: true });
    }
  }
}

describe('the bar', () => {
  /** The gap the bar sets between a value and what follows it. */
  const FOLLOWER_GAP = 6;

  for (const face of ZONE_FACES) {
    if (!face.zones.bar || !face.bar) continue;
    const frame = face.zones.bar;
    const items = texts(faceItems(face)).filter((i) => i.name.startsWith('bar.'));
    const slots = face.barFieldsPerEnd === 2 ? ['Left1', 'Left2', 'Right1', 'Right2'] : ['Left1', 'Right1'];

    for (const id of ['clock', 'simulatedTime']) {
      test(`${face.folder} ${id}: measured for the word, drawn with it only on a twelve-hour clock, and flush without it`, () => {
        const number = BAR_FIELDS.find((f) => f.id === id)!.number;
        for (const slot of slots) {
          const shown = { [`${PROPERTY_PREFIX}.${barFieldSettingName(sizeOf(face), slot as 'Left1')}`]: number };
          const value = named(items, `bar.${slot}.${id}.value`);
          const word = named(items, `bar.${slot}.${id}.unit`);
          const label = named(items, `bar.${slot}.${id}.label`);
          const right = slot.startsWith('Right');
          // A left-hand word follows the figure at the bar's gap exactly; a right-hand one stands
          // against the padding, so its gap is the bar's plus whatever the narrower word leaves.
          holdsTheWord(`${face.folder} ${slot}`, value, word, shown, right ? { atLeast: FOLLOWER_GAP } : { exactly: FOLLOWER_GAP });

          for (const m of MOMENTS) {
            for (const format of CLOCK_FORMATS) {
              const moment = { ...shown, ...at(m.hour, m.minute), [CLOCK_FORMAT]: format };
              const v = asDrawn(value, moment);
              const inkOf = ink(value, v.left, v.text);
              if (right) {
                // The field ends at the padding in both formats: with the word, the word ends there;
                // without it, the digits move up and end there themselves.
                const edge = label.rect.left + label.rect.width;
                const last = format === '12h' ? ink(word, asDrawn(word, moment).left, m.meridiem).end : inkOf.end;
                expect({ where: `${face.folder} ${slot} ${id}`, format, at: m.h24, end: Math.round(last), edge }).toMatchObject({ end: edge });
                expect({ where: `${face.folder} ${slot} ${id}`, format, inside: inkOf.start >= label.rect.left }).toMatchObject({ inside: true });
              } else {
                expect({ where: `${face.folder} ${slot} ${id}`, format, start: inkOf.start }).toMatchObject({ start: label.rect.left });
                const end = format === '12h' ? ink(word, asDrawn(word, moment).left, m.meridiem).end : inkOf.end;
                expect({ where: `${face.folder} ${slot} ${id}`, format, inside: end <= label.rect.left + label.rect.width + 0.5 }).toMatchObject({ inside: true });
              }
            }
          }
          expect(frame.left <= label.rect.left && label.rect.left + label.rect.width <= frame.left + frame.width).toBe(true);
        }
      });
    }
  }
});

describe("band D's corner", () => {
  /** Band D's gap between a value and its unit. */
  const UNIT_GAP = 5;
  const BANDS = [rect(0, 420, 1920, 60), rect(0, 420, 1280, 60), rect(0, 346, 1280, 54), rect(0, 660, 1280, 60)];

  for (const band of BANDS) {
    test(`${band.width} x ${band.height}: each clock ends against its field's edge in either format`, () => {
      const items = texts(bandCorners(band, 'corner.'));
      for (const id of ['clock', 'sim']) {
        const value = named(items, `corner.${id}.value`);
        const word = named(items, `corner.${id}.unit`);
        const label = named(items, `corner.${id}.label`);
        holdsTheWord(`${band.width} ${id}`, value, word, {}, { atLeast: UNIT_GAP });
        const edge = label.rect.left + label.rect.width;
        for (const m of MOMENTS) {
          for (const format of CLOCK_FORMATS) {
            const moment = { ...at(m.hour, m.minute), [CLOCK_FORMAT]: format };
            const v = asDrawn(value, moment);
            const last = format === '12h' ? ink(word, asDrawn(word, moment).left, m.meridiem).end : ink(value, v.left, v.text).end;
            expect({ where: `${band.width} ${id}`, format, at: m.h24, end: Math.round(last), edge }).toMatchObject({ end: edge });
            expect({ where: `${band.width} ${id}`, format, inside: ink(value, v.left, v.text).start >= label.rect.left }).toMatchObject({ inside: true });
          }
        }
      }
    });
  }
});

describe('the idle screen', () => {
  const frames = [rect(0, 0, 1920, 480), rect(0, 0, 800, 286), rect(0, 0, 480, 480)];

  for (const frame of frames) {
    test(`${frame.width} x ${frame.height}: the digits stay centred where the twenty-four-hour clock is, and the word hangs after them`, () => {
      const items = texts(idleItems({ frame }));
      const value = named(items, 'idle.clock');
      const word = named(items, 'idle.clock.unit');
      holdsTheWord(`idle ${frame.width}`, value, word, {}, { exactly: 6 });
      // The centre the digits keep: the twenty-four-hour clock's, which is the design-time place.
      const centre = (inkOf: { start: number; end: number }): number => (inkOf.start + inkOf.end) / 2;
      const home = centre(ink(value, value.rect.left, value.text));
      for (const m of MOMENTS) {
        for (const format of CLOCK_FORMATS) {
          const v = asDrawn(value, { ...at(m.hour, m.minute), [CLOCK_FORMAT]: format });
          expect({ frame: frame.width, format, at: m.h24, centre: centre(ink(value, v.left, v.text)) }).toMatchObject({ centre: home });
        }
      }
    });
  }
});
