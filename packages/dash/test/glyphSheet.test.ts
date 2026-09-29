/**
 * The glyph sheet as data: `build/flag-box-glyphs.json`, which the plugin embeds to draw the box's
 * preview and the rig page's tiles from the pictures the profile is built from (#503).
 *
 * The plugin reads it with no knowledge of how a flag looks, so what is held here is the contract it
 * reads against: the schema, every glyph of the catalogue in its order with every frame, every frame
 * the panel's size, and every lit cell a colour the design system defines.
 */
import { describe, expect, test } from 'bun:test';
import { COLUMNS, GLYPH_SHEET_SCHEMA_VERSION, glyphCatalogue, glyphSheetJson, ROWS } from '../src/leds/index.ts';
import { ds } from '../src/tokens.ts';

interface SheetGlyph {
  name: string;
  kind: string;
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
    for (const glyph of sheet.glyphs) expect(Object.keys(glyph)).toEqual(['name', 'kind', 'frames']);
  });

  test('carries the whole catalogue, in its order, every frame of it', () => {
    const catalogue = glyphCatalogue();
    expect(sheet.glyphs).toHaveLength(catalogue.length);
    expect(sheet.glyphs.map((g) => g.name)).toEqual(catalogue.map((g) => g.name));
    expect(sheet.glyphs.map((g) => g.kind)).toEqual(catalogue.map((g) => g.kind));
    // Every frame, and not only the first the SVG draws: the panel animates what the box animates.
    sheet.glyphs.forEach((g, i) => expect({ name: g.name, frames: g.frames }).toEqual({ name: g.name, frames: catalogue[i]!.frames }));
    expect(new Set(sheet.glyphs.map((g) => g.kind))).toEqual(new Set(['flag', 'pit', 'spotter', 'warning', 'gear', 'standby']));
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
