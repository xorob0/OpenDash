/**
 * The pit wall header strip, which all four pages draw from one builder: which readouts it lays
 * out and in what order, the amber the incident count is owed, the wind's unit folded into its
 * value, and that no text of the strip is drawn over another, on every page as it ships. The 1080
 * px portrait page is the narrowest strip and therefore where a collision appears first, which is
 * where the session name once drew over the page name.
 *
 * It lives in its own file rather than in secondScreens.test.ts because that file measures every
 * text of every package and is read and edited by everybody; a header assertion buried in it is a
 * header assertion nobody finds.
 */
import { describe, expect, test } from 'bun:test';
import { measureText, type MeasuredFace } from '../src/design/advances.ts';
import { rect } from '../src/design/geometry.ts';
import type { Rect } from '../src/design/geometry.ts';
import type { Item, TextItem } from '../src/generator.ts';
import { PIT_WALL_SIZES, portraitPage, racePage, telemetryPage, towerPage } from '../src/screens/pitwall.ts';
import { PIT_WALL_HEADER, pitWallHeader } from '../src/screens/pitwallHeader.ts';
import { CLOCK_FORMATS, CLOCK_FORMAT_SETTING, PROPERTY_PREFIX, type ClockFormat } from '../src/contract.ts';
import { ncalc } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';
import { bindingExpression } from './monoGlyphs.ts';
import { evalNcalc } from './ncalcEval.ts';

const header = (width: number, compact: boolean): Item[] =>
  pitWallHeader('h', { frame: rect(0, 0, width, PIT_WALL_HEADER.height), pageName: 'Pit wall · test', page: 1, pages: compact ? 1 : 3, compact });

/**
 * A readout group's items are named `h.<group>.<index>`; the left-hand cluster's are not.
 *
 * A mark is `h.<group>.<index>.mark`, the text that stands in for a value in one state -- the `∞` of
 * a session with no clock. It belongs to its group and shares its value's box, so it is matched here
 * rather than left to fall into the left cluster, which is where it first arrived and where it made
 * the cluster look 195 px wider than the wordmark it is.
 */
const GROUP = /^h\.([a-zA-Z]+)\.\d+(?:\.mark)?$/;

const boxed = (items: Item[]): { name: string; rect: Rect }[] => items.flatMap((i) => (i.kind === 'layer' ? [] : [{ name: i.name, rect: i.rect }]));

const partsOf = (items: Item[], id: string): TextItem[] => items.filter((i): i is TextItem => i.kind === 'text' && i.name.startsWith(`h.${id}.`));

/** The readout groups as the eye meets them, left to right, each one the span of its parts. */
function readoutGroups(items: Item[]): { id: string; left: number; right: number }[] {
  const spans = new Map<string, { left: number; right: number }>();
  for (const { name, rect: r } of boxed(items)) {
    const id = GROUP.exec(name)?.[1];
    if (id === undefined) continue;
    const span = spans.get(id);
    spans.set(id, { left: Math.min(span?.left ?? r.left, r.left), right: Math.max(span?.right ?? 0, r.left + r.width) });
  }
  return [...spans].map(([id, span]) => ({ id, ...span })).sort((a, b) => a.left - b.left);
}

const CLOCK_FORMAT = `${PROPERTY_PREFIX}.${CLOCK_FORMAT_SETTING}`;

/** Whether an expression asks which clock format the rig is on. */
const readsClockFormat = (expression: string): boolean => ncalc.referencedProperties(expression).includes(CLOCK_FORMAT);

/**
 * The header as a rig draws it with its clocks in `format`: a part the format hides left out, and a
 * part the format moves put where its `Left` sends it. #324.
 *
 * Only what the clock format decides is decided here. A part hidden for another reason -- the lap
 * total of an untimed session, the incident limit of a session with none -- is kept, because the
 * question is whether the strip has room for everything it may draw at once, and it may draw those.
 */
function underClockFormat(items: Item[], format: ClockFormat): Item[] {
  const props = { [CLOCK_FORMAT]: format };
  return items.flatMap((item): Item[] => {
    if (item.kind === 'layer') return [item];
    const visible = item.kind === 'text' ? bindingExpression(item, 'Visible') : '';
    if (visible !== '' && readsClockFormat(visible) && evalNcalc(visible, props) !== true) return [];
    const left = item.kind === 'text' ? bindingExpression(item, 'Left') : '';
    if (left === '' || !readsClockFormat(left)) return [item];
    return [{ ...item, rect: { ...item.rect, left: Number(evalNcalc(left, props)) } }];
  });
}

/** Where the wordmark, the page name and the page squares end. */
const leftClusterRight = (items: Item[]): number => Math.max(...boxed(items).filter((i) => i.name !== 'h.rule' && !GROUP.test(i.name)).map((i) => i.rect.left + i.rect.width));

describe('the pit wall header', () => {
  test('lays the landscape readouts out in the canvas order', () => {
    expect(readoutGroups(header(1920, false)).map((g) => g.id)).toEqual(['session', 'timeLeft', 'incidents', 'track', 'wind', 'simClock', 'localClock']);
  });

  test('draws the track state as a word rather than in the digit cells', () => {
    const track = partsOf(header(1920, false), 'track');
    expect(track.map((i) => i.text)).toEqual(['Track', 'Dry']);
    const state = track[1]!;
    // Measured from "MODERATE", the longest grip status iRacing reports, and proportional for the
    // same reason as the wind: the "m" overruns the digit cell the numerals are built from.
    expect({ widest: state.widest, fontSize: state.fontSize, monospace: state.monospace }).toEqual({ widest: 'Moderate', fontSize: 24, monospace: undefined });
  });

  test('reads the lap rather than the session on the portrait page, and drops the wind and the track state', () => {
    expect(readoutGroups(header(1080, true)).map((g) => g.id)).toEqual(['lap', 'timeLeft', 'incidents', 'localClock']);
    expect(partsOf(header(1080, true), 'lap').map((i) => i.text)).toEqual(['Lap', '12', '/ 30']);
  });

  test('keeps the incident limit and the sim clock on the landscape pages and sheds both on the portrait one', () => {
    expect(partsOf(header(1920, false), 'incidents').map((i) => i.text)).toEqual(['Inc', '3x', '/ 17']);
    expect(partsOf(header(1080, true), 'incidents').map((i) => i.text)).toEqual(['Inc', '3x']);
    expect(partsOf(header(1080, true), 'simClock')).toEqual([]);
  });

  test('names each clock in front of its own value, and never behind it', () => {
    // The strip read "14:32 LOCAL 15:07 SIM" and was reported from a rig as not saying which clock
    // was the real one; every other group on it names itself first, and now these two do as well.
    // The `PM` after each is the meridiem a twelve-hour clock writes, drawn only on one (#324).
    expect(partsOf(header(1920, false), 'simClock').map((i) => i.text)).toEqual(['Sim', '15:07', 'PM']);
    expect(partsOf(header(1920, false), 'localClock').map((i) => i.text)).toEqual(['Local', '14:32', 'PM']);
    expect(partsOf(header(1080, true), 'localClock').map((i) => i.text)).toEqual(['Local', '14:32', 'PM']);
  });

  test('writes a twelve-hour clock with its meridiem after it, and only a twelve-hour one', () => {
    for (const items of [header(1920, false), header(1080, true)]) {
      for (const id of ['simClock', 'localClock']) {
        const parts = partsOf(items, id);
        if (parts.length === 0) continue;
        const [, value, word] = parts;
        // The digits in the cells both formats share, right aligned so that `9:05` stands against its
        // `PM` rather than a cell short of it.
        expect({ id, chars: value!.monospace !== undefined, hAlign: value!.hAlign }).toEqual({ id, chars: true, hAlign: 'right' });
        // The word in the label face, because `M` fits no cell, and measured by the wider of the two.
        expect({ id, widest: word!.widest, monospace: word!.monospace }).toEqual({ id, widest: 'AM', monospace: undefined });
        for (const format of CLOCK_FORMATS) {
          const drawn = underClockFormat(items, format).some((i) => i.name === word!.name);
          expect({ id, format, drawn }).toEqual({ id, format, drawn: format === '12h' });
        }
      }
    }
  });

  test('ends against the padding under either clock format, with no hole where a word is not', () => {
    // Laid out for the twelve-hour clock, which is the wider, and moved back to the edge on a
    // twenty-four-hour one, so a rig that never opens the setting has no hole where the word is not.
    for (const [width, compact] of [
      [1920, false],
      [1080, true],
    ] as const) {
      for (const format of CLOCK_FORMATS) {
        const groups = readoutGroups(underClockFormat(header(width, compact), format));
        const last = groups[groups.length - 1]!;
        // Within the few pixels a value's box is short of the room its group gives it.
        const edge = width - PIT_WALL_HEADER.padX;
        expect({ width, format, right: last.right, flush: last.right <= edge && last.right >= edge - 4 }).toMatchObject({ flush: true });
        for (let i = 1; i < groups.length; i++) {
          const gap = groups[i]!.left - groups[i - 1]!.right;
          expect({ width, format, after: groups[i]!.id, gap: gap >= PIT_WALL_HEADER.groupGap && gap <= PIT_WALL_HEADER.groupGap + 4 }).toMatchObject({ gap: true });
        }
      }
    }
  });

  test('draws no flag on the strip', () => {
    // It was six normalised properties and a 24 px block, it did not light on a rig, and the flag
    // is now the page's own -- a band or the full screen, chosen in the plugin like the companion's.
    for (const items of [header(1920, false), header(1080, true)]) expect(partsOf(items, 'flag')).toEqual([]);
  });

  test('draws the incident count in amber', () => {
    expect(ds.purpose.alert.incident).toBe('#FFB300');
    for (const items of [header(1920, false), header(1080, true)]) {
      const count = partsOf(items, 'incidents')[1];
      expect({ text: count?.text, color: count?.textColor }).toEqual({ text: '3x', color: ds.purpose.alert.incident });
    }
  });

  test('draws the wind as one run with its unit inline', () => {
    const wind = partsOf(header(1920, false), 'wind');
    expect(wind.map((i) => i.text)).toEqual(['12 km/h']);
    const run = wind[0]!;
    expect({ widest: run.widest, fontSize: run.fontSize }).toEqual({ widest: '188 km/h', fontSize: 24 });
    expect(run.text.endsWith('km/h')).toBe(true);
    // Proportional, not cells: "m" is one of the glyphs font.cell.excluded names as overrunning
    // the digit cell, so a monospaced "12 km/h" would be drawn with its unit clipped.
    expect(run.monospace).toBeUndefined();
  });

  test('leaves a gap between every group of the portrait header at 1080', () => {
    const items = header(1080, true);
    const boxes = [{ id: 'left cluster', left: 0, right: leftClusterRight(items) }, ...readoutGroups(items)];
    for (let i = 1; i < boxes.length; i++) {
      const before = boxes[i - 1]!;
      const after = boxes[i]!;
      expect({ before: before.id, after: after.id, gap: after.left - before.right, clear: after.left > before.right }).toMatchObject({ clear: true });
    }
    expect(boxes[boxes.length - 1]!.right).toBeLessThanOrEqual(1080 - PIT_WALL_HEADER.padX);
  });
});

/** Which measured face an item draws in, as textFit.test.ts reads it off the same two fields. */
const faceOf = (item: TextItem): MeasuredFace => {
  if (item.font === 'Barlow') return item.fontWeight === 'Bold' ? 'BarlowBold' : 'BarlowMedium';
  if (item.fontWeight === 'Bold') return 'BarlowCondensedBold';
  if (item.fontWeight === 'Light') return 'BarlowCondensedLight';
  return 'BarlowCondensedSemiBold';
};

/**
 * What the item's ink spans when it draws the longest thing it can draw.
 *
 * Ink and not the box, because two boxes touching is not two texts touching: the wordmark's halves
 * are given boxes a quarter wider than their letters, so that a SimHub install missing Barlow
 * Condensed Light or Bold synthesises the face into room it has, and those boxes overlap each other
 * and the page name by design.
 */
function ink(item: TextItem): { left: number; right: number } {
  const text = item.widest ?? item.text;
  const mono = item.monospace;
  const width = mono
    ? [...text].reduce((sum, c) => sum + ((mono.specialChars?.includes(c) ?? false) ? mono.specialCharsWidth : mono.charWidth), 0)
    : measureText(faceOf(item), text, item.fontSize);
  const left = item.hAlign === 'right' ? item.rect.left + item.rect.width - width : item.rect.left;
  return { left, right: left + width };
}

/** The four pages as they ship, so that the page names and the frames are the real ones. */
const PAGES = PIT_WALL_SIZES.flatMap((size) =>
  size.portrait ? [portraitPage(size.width, size.height)] : [racePage(size.width, size.height), towerPage(size.width, size.height), telemetryPage(size.width, size.height)],
);

const headerTexts = (items: Item[]): TextItem[] => items.filter((i): i is TextItem => i.kind === 'text' && i.name.includes('header.'));

/** Every text of a header as the eye meets it, with what its ink spans. */
const inkSpans = (items: Item[]): { name: string; left: number; right: number }[] =>
  headerTexts(items)
    .map((i) => ({ name: i.name, ...ink(i) }))
    .sort((a, b) => a.left - b.left);

function expectNoOverlap(spans: { name: string; left: number; right: number }[]): void {
  expect(spans.length).toBeGreaterThan(0);
  for (let i = 1; i < spans.length; i++) {
    const before = spans[i - 1]!;
    const after = spans[i]!;
    expect({ before: before.name, after: after.name, clear: after.left >= before.right }).toMatchObject({ clear: true });
  }
}

describe('a pit wall header never draws one text over another', () => {
  for (const page of PAGES) {
    for (const format of CLOCK_FORMATS) {
      test(`${page.name} with its clocks at ${format}`, () => {
        // Marks are held against their own values below rather than counted as neighbours: a mark and
        // the value it replaces are drawn in the same place on purpose and never in the same frame.
        // The clocks are held in each format, since a twelve-hour clock's word takes a room a
        // twenty-four-hour one gives back.
        expectNoOverlap(inkSpans(underClockFormat(page.items, format)).filter((span) => !span.name.endsWith('.mark')));
      });
    }
  }

  test('a mark is drawn inside the box of the value it stands in for, so it needs no room of its own', () => {
    // Which is what lets the overlap proof above run over the values alone. `∞` cannot be laid in a
    // digit cell -- it advances a third wider than one -- so it is a proportional run beside the
    // clock, and the room it is allowed is the clock's own (#439).
    let marks = 0;
    for (const page of PAGES) {
      const texts = headerTexts(page.items);
      for (const item of texts.filter((i) => i.name.endsWith('.mark'))) {
        marks += 1;
        const value = texts.find((i) => i.name === item.name.slice(0, -'.mark'.length));
        if (value === undefined) throw new Error(`${item.name} stands in for nothing`);
        const span = ink(item);
        const room = { left: value.rect.left, right: value.rect.left + value.rect.width };
        expect({ mark: item.name, text: item.text, inside: span.left >= room.left && span.right <= room.right }).toMatchObject({ text: '∞', inside: true });
        // And on the value's own line, at the value's own size, so the two read as one reading.
        expect({ mark: item.name, top: item.rect.top, fontSize: item.fontSize }).toMatchObject({ top: value.rect.top, fontSize: value.fontSize });
      }
    }
    expect(marks).toBe(PAGES.length);
  });

  test('drops the page name rather than let a readout draw over it', () => {
    // The left cluster is laid out against what the readouts left, and the name is the only text on
    // the strip the builder can decline to draw, the squares saying which page this is anyway.
    const items = pitWallHeader('long.header', {
      frame: rect(0, 0, 1080, PIT_WALL_HEADER.height),
      pageName: 'Pit wall · portrait, under a name nobody would write on a 1080 px strip',
      page: 1,
      pages: 3,
      compact: true,
    });
    expect(items.some((i) => i.name === 'long.header.page')).toBe(false);
    expect(items.some((i) => i.name === 'long.header.square1')).toBe(true);
    for (const format of CLOCK_FORMATS) expectNoOverlap(inkSpans(underClockFormat(items, format)).filter((span) => !span.name.endsWith('.mark')));
  });
});
