/**
 * The matrix's `textFit.test.ts`: the test that stops a glyph shipping that nobody can read.
 *
 * The face has one unforgiving renderer rule — WPF clips silently, so every text is measured
 * against its box. A matrix has a rule of the same kind and it is stricter, because there is no
 * room to shrink into: eight by eight, one colour per pixel, read at arm's length through a
 * diffuser and in peripheral vision. A glyph that runs off the grid does not error; it is simply
 * not there, which is the same silent wrongness textFit exists for.
 *
 * The rule that matters most here is the one nothing else can enforce: **no two pictures the
 * driver has to tell apart may be identical, and none may differ only in hue.** Yellow and waved
 * yellow, black and furled black, the limiter in the lane and out of it are pairs that mean
 * opposite things and are one careless edit apart. On a 64-pixel panel, that colour rule is the
 * only part of XOR-78 that can be checked mechanically at all.
 */
import { describe, expect, test } from 'bun:test';
import { COLUMNS, ROWS } from '../src/leds/profile.ts';
import { buildFlagBoxProfile } from '../src/leds/profile.ts';
import { contactSheet, glyphCatalogue, type Glyph } from '../src/leds/sheet.ts';
import { walkContainers } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';

/** Every colour `ds` resolves. A literal that is not one of these is not a token. */
const TOKEN_COLOURS = new Set<string>();
(function collect(node: unknown): void {
  if (typeof node === 'string') {
    if (/^#[0-9A-F]{6}$/.test(node)) TOKEN_COLOURS.add(node);
    return;
  }
  if (node !== null && typeof node === 'object') for (const v of Object.values(node)) collect(v);
})(ds);

const glyphs = glyphCatalogue();

/** The lit/unlit shape of a glyph, colours removed: what a colour-blind driver has to go on. */
const shapeOf = (glyph: Glyph): string =>
  glyph.frames.map((frame) => frame.map((row) => row.map((c) => (c === null ? '.' : '#')).join('')).join('/')).join('|');

const pictureOf = (glyph: Glyph): string => JSON.stringify(glyph.frames);

const litCells = (glyph: Glyph): number => (glyph.frames[0] ?? []).flat().filter((c) => c !== null).length;

describe('every glyph fits the panel', () => {
  test('there is a catalogue to check at all', () => {
    expect(glyphs.length).toBeGreaterThan(20);
  });

  test('exactly sixty-four cells, every one of them', () => {
    for (const glyph of glyphs) {
      for (const frame of glyph.frames) {
        expect({ glyph: glyph.name, rows: frame.length }).toEqual({ glyph: glyph.name, rows: ROWS });
        for (const row of frame) expect({ glyph: glyph.name, columns: row.length }).toEqual({ glyph: glyph.name, columns: COLUMNS });
      }
    }
  });

  test('every colour is a token, not a literal somebody typed', () => {
    for (const glyph of glyphs) {
      for (const frame of glyph.frames) {
        for (const row of frame) {
          for (const colour of row) {
            if (colour === null) continue;
            expect({ glyph: glyph.name, colour, token: TOKEN_COLOURS.has(colour) }).toEqual({ glyph: glyph.name, colour, token: true });
          }
        }
      }
    }
  });

  test('nothing is invisible at low brightness', () => {
    // A glyph too sparse to see is a glyph that does not fire, which is worse than an absent one.
    for (const glyph of glyphs) {
      expect({ glyph: glyph.name, lit: litCells(glyph) >= MIN_LIT }).toEqual({ glyph: glyph.name, lit: true });
    }
  });

  test('nothing is a white rectangle at night, except the flags that are meant to be', () => {
    // A solid field is the right picture for a racing flag and the wrong one for anything else:
    // sixty-four LEDs at full output is a lamp, not a message.
    for (const glyph of glyphs) {
      if (glyph.kind === 'flag') continue;
      expect({ glyph: glyph.name, filled: litCells(glyph) > MAX_LIT }).toEqual({ glyph: glyph.name, filled: false });
    }
  });
});

/** Four pixels of sixty-four: below this a glyph reads as a stuck LED. The standby mark is exactly this. */
export const MIN_LIT = 4;
/** Three quarters: above this a glyph is a lamp rather than a picture. */
export const MAX_LIT = 48;

describe('no two glyphs can be confused', () => {
  test('no two draw the same picture', () => {
    const seen = new Map<string, string>();
    for (const glyph of glyphs) {
      const clash = seen.get(pictureOf(glyph));
      expect({ glyph: glyph.name, clash }).toEqual({ glyph: glyph.name, clash: undefined });
      seen.set(pictureOf(glyph), glyph.name);
    }
  });

  test('no two differ only in hue, unless both are solid flags', () => {
    // XOR-78: meaning is not carried by colour alone. A racing flag is the documented exception --
    // red, yellow, white and green *are* colours, and inventing a pattern for each would be worse.
    const byShape = new Map<string, Glyph[]>();
    for (const glyph of glyphs) {
      const list = byShape.get(shapeOf(glyph)) ?? [];
      list.push(glyph);
      byShape.set(shapeOf(glyph), list);
    }
    for (const [, sharing] of byShape) {
      if (sharing.length < 2) continue;
      const names = sharing.map((g) => g.name).sort();
      const allSolidFlags = sharing.every((g) => g.kind === 'flag' && litCells(g) === ROWS * COLUMNS);
      // A gear digit is the same shape in four shift-band colours on purpose: the colour is the
      // band and the digit is the gear, so they are two facts rather than one told twice.
      const allOneGear = new Set(sharing.map((g) => g.name.replace(/ .*$/, ''))).size === 1 && sharing.every((g) => g.kind === 'gear');
      expect({ names, ok: allSolidFlags || allOneGear }).toEqual({ names, ok: true });
    }
  });
});

describe('the contact sheet', () => {
  test('renders every glyph in the catalogue', () => {
    const svg = contactSheet(glyphs);
    for (const glyph of glyphs) expect(svg).toInclude(glyph.name);
    expect(svg).toStartWith('<svg');
    expect(svg).toInclude('</svg>');
  });

  test('is the same sheet twice, so a diff of it is a real change', () => {
    expect(contactSheet(glyphs)).toBe(contactSheet(glyphCatalogue()));
  });

  test('covers what the profile actually draws', () => {
    // The sheet is worthless if it can drift from the file. Every animation in the built profile
    // has to appear on it.
    const drawn = new Set<string>();
    for (const container of walkContainers(buildFlagBoxProfile().containers)) {
      if (container.kind === 'animation' && container.description !== undefined) drawn.add(container.description.replace(/ glyph$/, ''));
    }
    const sheet = new Set(glyphs.map((g) => g.name));
    for (const name of drawn) expect({ name, onSheet: sheet.has(name) }).toEqual({ name, onSheet: true });
  });
});
