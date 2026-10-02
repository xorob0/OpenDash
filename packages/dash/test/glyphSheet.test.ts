/**
 * The glyph sheet as data: `build/flag-box-glyphs.json`, which the plugin embeds to draw the box's
 * preview and the rig page's tiles from the pictures the profile is built from (#791).
 *
 * The plugin reads it with no knowledge of how a flag looks, so what is held here is the contract it
 * reads against: the schema, every glyph of the catalogue in its order with every frame and how long
 * the box holds it, every frame the panel's size, and every lit cell a colour the design system
 * defines.
 */
import { describe, expect, test } from 'bun:test';
import { FLAG_CATALOGUE } from '../src/flags.ts';
import { COLUMNS, flagFrames, GLYPH_SHEET_SCHEMA_VERSION, glyphCatalogue, glyphSheetJson, ROWS } from '../src/leds/index.ts';
import { ds } from '../src/tokens.ts';

interface SheetGlyph {
  name: string;
  kind: string;
  durationsMs: number[];
  frames: (string | null)[][][];
}
interface Sheet {
  schemaVersion: number;
  rows: number;
  columns: number;
  glyphs: SheetGlyph[];
}

/** Every `#RRGGBB` the token file defines, wherever in it. */
const tokenHexes = (): Set<string> => {
  const found = new Set<string>();
  const visit = (value: unknown): void => {
    if (typeof value === 'string' && /^#[0-9A-Fa-f]{6}$/.test(value)) found.add(value.toUpperCase());
    else if (value !== null && typeof value === 'object') for (const v of Object.values(value)) visit(v);
  };
  visit(ds);
  return found;
};

describe('the glyph sheet', () => {
  const text = glyphSheetJson();
  const sheet = JSON.parse(text) as Sheet;

  test('is the schema the plugin reads', () => {
    expect(Object.keys(sheet)).toEqual(['schemaVersion', 'rows', 'columns', 'glyphs']);
    expect(sheet.schemaVersion).toBe(GLYPH_SHEET_SCHEMA_VERSION);
    expect(sheet.schemaVersion).toBe(1);
    expect([sheet.rows, sheet.columns]).toEqual([ROWS, COLUMNS]);
    expect([sheet.rows, sheet.columns]).toEqual([8, 8]);
    for (const glyph of sheet.glyphs) expect(Object.keys(glyph)).toEqual(['name', 'kind', 'durationsMs', 'frames']);
  });

  test('carries the whole catalogue, in its order, every frame of it', () => {
    const catalogue = glyphCatalogue();
    expect(sheet.glyphs).toHaveLength(catalogue.length);
    expect(sheet.glyphs.map((g) => g.name)).toEqual(catalogue.map((g) => g.name));
    expect(sheet.glyphs.map((g) => g.kind)).toEqual(catalogue.map((g) => g.kind));
    // Every frame, and not only the first the SVG draws, with how long each is held: a preview that
    // plays the frames at their durations plays what the box plays.
    sheet.glyphs.forEach((g, i) =>
      expect({ name: g.name, frames: g.frames, durationsMs: g.durationsMs }).toEqual({
        name: g.name,
        frames: catalogue[i]!.frames,
        durationsMs: catalogue[i]!.durationsMs,
      }),
    );
    expect(new Set(sheet.glyphs.map((g) => g.kind))).toEqual(new Set(['flag', 'pit', 'spotter', 'warning', 'gear', 'standby']));
  });

  test('names each glyph as the plugin will look it up, literally, per kind', () => {
    // The name is a key the panel reads the sheet by, so it is pinned as written rather than compared
    // with the catalogue that writes it: the comparison above stays green whatever the catalogue calls
    // a glyph. A flag is its condition id; every other kind is its label, spaces and all. There is no
    // "gear-3" and no "spotter-left", which is what a lookup written from a guess would ask for.
    const byKind = (kind: string): string[] => sheet.glyphs.filter((g) => g.kind === kind).map((g) => g.name);
    expect(byKind('flag')).toEqual([
      'red',
      'disqualify',
      'furled',
      'black',
      'meatball',
      'caution',
      'yellowWaving',
      'yellow',
      'debris',
      'blue',
      'white',
      'green',
      'startSet',
      'startReady',
      'chequered',
    ]);
    expect(byKind('pit')).toEqual(['Pit speeding', 'Pit limiterOutOfLane', 'Pit limiterInLane']);
    expect(byKind('spotter')).toEqual([
      'Spotter carBoth',
      'Spotter carLeft',
      'Spotter carRight',
      'Spotter carBothGrowing',
      'Spotter carLeftGrowing',
      'Spotter carRightGrowing',
    ]);
    expect(byKind('warning')).toEqual(['Warning oilHot', 'Warning waterHot', 'Warning lowFuel']);
    const gears = byKind('gear');
    expect(gears).toHaveLength(44);
    for (const name of ['Gear R redline', 'Gear N stage2', 'Gear 3 stage1', 'Gear 9 rest']) expect(gears).toContain(name);
    expect(byKind('standby')).toEqual(['Standby']);
    const names = sheet.glyphs.map((g) => g.name);
    expect(new Set(names).size).toBe(names.length);
    for (const guess of ['gear-3', 'spotter-left']) expect(names).not.toContain(guess);
  });

  test("holds each frame as long as the box does, one duration to a frame", () => {
    for (const glyph of sheet.glyphs) {
      expect({ name: glyph.name, durations: glyph.durationsMs.length }).toEqual({ name: glyph.name, durations: glyph.frames.length });
      for (const ms of glyph.durationsMs) expect({ name: glyph.name, positive: Number.isInteger(ms) && ms > 0 }).toEqual({ name: glyph.name, positive: true });
    }
    // Against the profile's own frames, not the catalogue that copies them: the flags are the glyphs
    // that do not play at one rate.
    for (const condition of FLAG_CATALOGUE) {
      const frames = flagFrames(condition.id);
      if (frames === undefined) continue;
      const glyph = sheet.glyphs.find((g) => g.name === condition.id);
      expect({ name: condition.id, durationsMs: glyph?.durationsMs }).toEqual({ name: condition.id, durationsMs: frames.map((f) => f.durationMs) });
    }
    // Red and the meatball grow, then hold: their first frame is a dot, and the picture is the frame
    // held longest. A tile painted from frames[0] would draw the red flag as four LEDs.
    const lit = (frame: (string | null)[][]): number => frame.flat().filter((c) => c !== null).length;
    const still = (g: SheetGlyph): (string | null)[][] => g.frames[g.durationsMs.indexOf(Math.max(...g.durationsMs))]!;
    const red = sheet.glyphs.find((g) => g.name === 'red')!;
    expect(red.durationsMs).toEqual([100, 100, 100, 20000]);
    expect(lit(red.frames[0]!)).toBe(4);
    expect(lit(still(red))).toBe(64);
    expect(sheet.glyphs.find((g) => g.name === 'meatball')!.durationsMs).toEqual([100, 100, 100, 20000]);
  });

  test('every frame is the panel, eight rows of eight, and an unlit cell is kept as null', () => {
    for (const glyph of sheet.glyphs) {
      expect({ name: glyph.name, frames: glyph.frames.length > 0 }).toEqual({ name: glyph.name, frames: true });
      for (const frame of glyph.frames) {
        expect({ name: glyph.name, rows: frame.length }).toEqual({ name: glyph.name, rows: 8 });
        for (const row of frame) expect({ name: glyph.name, columns: row.length }).toEqual({ name: glyph.name, columns: 8 });
      }
    }
    // A picture is mostly dark, and the dark is part of it: nulls are written, not left out.
    expect(text).toContain('null');
  });

  test('every lit cell is a colour the tokens define', () => {
    const allowed = tokenHexes();
    const used = new Set(sheet.glyphs.flatMap((g) => g.frames.flat(2)).filter((c): c is string => c !== null));
    for (const colour of used) {
      expect({ colour, form: /^#[0-9A-F]{6}$/.test(colour) }).toEqual({ colour, form: true });
      expect({ colour, token: allowed.has(colour) }).toEqual({ colour, token: true });
    }
  });

  test('is deterministic, one row to a line, and ends with a newline', () => {
    expect(glyphSheetJson()).toBe(text);
    expect(text.endsWith('}\n')).toBe(true);
    // A diff of the file is a diff of pictures: a row of a frame is one line of it.
    expect(text).toContain('\n          [null,null,null,null,null,null,null,null]');
  });
});
