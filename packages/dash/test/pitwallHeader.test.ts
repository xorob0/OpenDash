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
import { ds } from '../src/tokens.ts';

const header = (width: number, compact: boolean): Item[] =>
  pitWallHeader('h', { frame: rect(0, 0, width, PIT_WALL_HEADER.height), pageName: 'Pit wall · test', page: 1, pages: compact ? 1 : 3, compact });

/** A readout group's items are named `h.<group>.<index>`; the left-hand cluster's are not. */
const GROUP = /^h\.([a-zA-Z]+)\.\d+$/;

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

/** Where the wordmark, the page name and the page squares end. */
const leftClusterRight = (items: Item[]): number => Math.max(...boxed(items).filter((i) => i.name !== 'h.rule' && !GROUP.test(i.name)).map((i) => i.rect.left + i.rect.width));

describe('the pit wall header', () => {
  test('lays the landscape readouts out in the canvas order', () => {
    expect(readoutGroups(header(1920, false)).map((g) => g.id)).toEqual(['session', 'timeLeft', 'flag', 'incidents', 'track', 'wind', 'clocks']);
  });

  test('draws the track state as a word rather than in the digit cells', () => {
    const track = partsOf(header(1920, false), 'track');
    expect(track.map((i) => i.text)).toEqual(['TRACK', 'DRY']);
    const state = track[1]!;
    // Measured from "MODERATE", the longest grip status iRacing reports, and proportional for the
    // same reason as the wind: the "m" overruns the digit cell the numerals are built from.
    expect({ widest: state.widest, fontSize: state.fontSize, monospace: state.monospace }).toEqual({ widest: 'MODERATE', fontSize: 24, monospace: undefined });
  });

  test('reads the lap rather than the session on the portrait page, and drops the wind and the track state', () => {
    expect(readoutGroups(header(1080, true)).map((g) => g.id)).toEqual(['lap', 'timeLeft', 'flag', 'incidents', 'clocks']);
    expect(partsOf(header(1080, true), 'lap').map((i) => i.text)).toEqual(['LAP', '12', '/ 30']);
  });

  test('keeps the incident limit and the sim clock on the landscape pages and sheds both on the portrait one', () => {
    expect(partsOf(header(1920, false), 'incidents').map((i) => i.text)).toEqual(['INC', '3x', '/ 17']);
    expect(partsOf(header(1920, false), 'clocks').map((i) => i.text)).toEqual(['14:32', 'LOCAL', '15:07', 'SIM']);
    expect(partsOf(header(1080, true), 'incidents').map((i) => i.text)).toEqual(['INC', '3x']);
    expect(partsOf(header(1080, true), 'clocks').map((i) => i.text)).toEqual(['14:32']);
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
    test(page.name, () => {
      expectNoOverlap(inkSpans(page.items));
    });
  }

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
    expectNoOverlap(inkSpans(items));
  });
});
