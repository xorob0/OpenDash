#!/usr/bin/env bun
/**
 * measure-font: reads a TrueType file and prints, per character, what it advances and how far its
 * ink actually reaches.
 *
 * `packages/dash/src/design/advances.ts` has always carried advances, and its header has always
 * said they came from a tool here, which was never committed. This is that tool, and it now
 * measures one thing more, because an advance is not what WPF clips against.
 *
 * An advance is how far the pen moves. Ink is where the glyph is drawn, and the two differ: a
 * glyph with a negative right side bearing reaches past the pen's next position. Barlow Condensed
 * Bold's "6" advances 0.440 em and its bowl reaches 0.468, so a monospace cell cut to the advance
 * loses the right of the bowl. That is not a rounding error, it is the shape of the letter.
 *
 * {@link inkBreak} measures a third thing, which is where a glyph's ink *stops and starts again*:
 * the gap between the tittle of an `i` and its stem is 0.080 em in Barlow Medium, a device pixel at
 * 13 px, and a gap that small is what turns Liam Byrne into `Llam Byrne` on a rim. The name column's
 * case rule is read off it.
 *
 *   bun tools/measure-font/measure.ts packages/dash/fonts/BarlowCondensed-Bold.ttf
 *   bun tools/measure-font/measure.ts --json --chars '0123456789NR' <file.ttf>
 *
 * Only the tables needed are parsed: head for the em, cmap for the character mapping, hmtx for the
 * advances, loca and glyf for the bounding boxes. Composite glyphs report the bounding box the
 * font itself records for them, which is what a renderer uses.
 */
import { readFileSync } from 'node:fs';

export interface GlyphMetrics {
  /** Pen movement, in em. */
  advance: number;
  /** Leftmost ink, in em, from the pen origin. Negative when the glyph reaches back. */
  xMin: number;
  /** Rightmost ink, in em, from the pen origin. This is what a box has to hold. */
  xMax: number;
  /** Lowest and highest ink, in em, from the baseline. */
  yMin: number;
  yMax: number;
}

/** The subset of a TrueType file this needs. */
interface Font {
  unitsPerEm: number;
  numGlyphs: number;
  loca: number[];
  glyfOffset: number;
  glyfLength: number;
  advances: number[];
  cmap: Map<number, number>;
  nameOffset: number;
  data: DataView;
}

const tag = (view: DataView, offset: number): string =>
  String.fromCharCode(view.getUint8(offset), view.getUint8(offset + 1), view.getUint8(offset + 2), view.getUint8(offset + 3));

export function parseFont(bytes: Uint8Array): Font {
  const data = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const numTables = data.getUint16(4);
  const tables = new Map<string, { offset: number; length: number }>();
  for (let i = 0; i < numTables; i++) {
    const record = 12 + i * 16;
    tables.set(tag(data, record), { offset: data.getUint32(record + 8), length: data.getUint32(record + 12) });
  }
  const need = (name: string): { offset: number; length: number } => {
    const found = tables.get(name);
    if (!found) throw new Error(`the font has no ${name} table`);
    return found;
  };

  const head = need('head').offset;
  const unitsPerEm = data.getUint16(head + 18);
  const indexToLocFormat = data.getInt16(head + 50);

  const numGlyphs = data.getUint16(need('maxp').offset + 4);

  const locaTable = need('loca');
  const loca: number[] = [];
  for (let i = 0; i <= numGlyphs; i++) {
    loca.push(indexToLocFormat === 0 ? data.getUint16(locaTable.offset + i * 2) * 2 : data.getUint32(locaTable.offset + i * 4));
  }

  const numberOfHMetrics = data.getUint16(need('hhea').offset + 34);
  const hmtx = need('hmtx').offset;
  const advances: number[] = [];
  for (let i = 0; i < numGlyphs; i++) {
    // After numberOfHMetrics entries the advance repeats, which is how a monospaced tail is stored.
    const index = Math.min(i, numberOfHMetrics - 1);
    advances.push(data.getUint16(hmtx + index * 4));
  }

  return {
    unitsPerEm,
    numGlyphs,
    loca,
    glyfOffset: need('glyf').offset,
    glyfLength: need('glyf').length,
    advances,
    cmap: parseCmap(data, need('cmap').offset),
    nameOffset: need('name').offset,
    data,
  };
}

/** Character code to glyph id, from the first format 4 or format 12 subtable offered. */
function parseCmap(data: DataView, offset: number): Map<number, number> {
  const numTables = data.getUint16(offset + 2);
  let best = -1;
  let bestFormat = -1;
  for (let i = 0; i < numTables; i++) {
    const record = offset + 4 + i * 8;
    const subtable = offset + data.getUint32(record + 4);
    const format = data.getUint16(subtable);
    if ((format === 4 || format === 12) && format > bestFormat) {
      best = subtable;
      bestFormat = format;
    }
  }
  if (best < 0) throw new Error('the font has no format 4 or 12 character map');
  return bestFormat === 12 ? parseCmap12(data, best) : parseCmap4(data, best);
}

function parseCmap4(data: DataView, subtable: number): Map<number, number> {
  const map = new Map<number, number>();
  const segCountX2 = data.getUint16(subtable + 6);
  const segCount = segCountX2 / 2;
  const ends = subtable + 14;
  const starts = ends + segCountX2 + 2;
  const deltas = starts + segCountX2;
  const rangeOffsets = deltas + segCountX2;
  for (let s = 0; s < segCount; s++) {
    const end = data.getUint16(ends + s * 2);
    const start = data.getUint16(starts + s * 2);
    const delta = data.getInt16(deltas + s * 2);
    const rangeOffset = data.getUint16(rangeOffsets + s * 2);
    if (start > end) continue;
    for (let code = start; code <= end && code !== 0xffff; code++) {
      let glyph: number;
      if (rangeOffset === 0) {
        glyph = (code + delta) & 0xffff;
      } else {
        const at = rangeOffsets + s * 2 + rangeOffset + (code - start) * 2;
        glyph = data.getUint16(at);
        if (glyph !== 0) glyph = (glyph + delta) & 0xffff;
      }
      if (glyph !== 0) map.set(code, glyph);
    }
  }
  return map;
}

function parseCmap12(data: DataView, subtable: number): Map<number, number> {
  const map = new Map<number, number>();
  const groups = data.getUint32(subtable + 12);
  for (let g = 0; g < groups; g++) {
    const at = subtable + 16 + g * 12;
    const start = data.getUint32(at);
    const end = data.getUint32(at + 4);
    const startGlyph = data.getUint32(at + 8);
    for (let code = start; code <= end; code++) map.set(code, startGlyph + (code - start));
  }
  return map;
}

/** What one character advances and where its ink reaches, in em. */
export function measure(font: Font, char: string): GlyphMetrics | undefined {
  const glyph = font.cmap.get(char.codePointAt(0) ?? -1);
  if (glyph === undefined) return undefined;
  const advance = (font.advances[glyph] ?? 0) / font.unitsPerEm;
  const start = font.loca[glyph] ?? 0;
  const end = font.loca[glyph + 1] ?? start;
  // An empty outline, a space for instance, has no ink at all.
  if (end <= start) return { advance, xMin: 0, xMax: 0, yMin: 0, yMax: 0 };
  const at = font.glyfOffset + start;
  return {
    advance,
    xMin: font.data.getInt16(at + 2) / font.unitsPerEm,
    yMin: font.data.getInt16(at + 4) / font.unitsPerEm,
    xMax: font.data.getInt16(at + 6) / font.unitsPerEm,
    yMax: font.data.getInt16(at + 8) / font.unitsPerEm,
  };
}

export function loadFont(path: string): Font {
  return parseFont(new Uint8Array(readFileSync(path)));
}

/** Name table ids worth asking for. 1 and 2 are the legacy pair, 16 and 17 the typographic one. */
export const NAME_ID = { family: 1, subfamily: 2, full: 4, postscript: 6, typographicFamily: 16, typographicSubfamily: 17 } as const;

/**
 * A string from the name table, reading the Windows (platform 3) records, which are UTF-16BE.
 *
 * Which family a renderer uses is not a formality: WPF groups faces by the typographic family when
 * one is given and by the legacy family otherwise, which is how "Barlow Condensed" came to be
 * drawn as Barlow.
 */
export function fontName(font: Font, nameId: number): string | undefined {
  const { data } = font;
  const table = font.nameOffset;
  const count = data.getUint16(table + 2);
  const storage = table + data.getUint16(table + 4);
  for (let i = 0; i < count; i++) {
    const record = table + 6 + i * 12;
    if (data.getUint16(record) !== 3 || data.getUint16(record + 6) !== nameId) continue;
    const length = data.getUint16(record + 8);
    const at = storage + data.getUint16(record + 10);
    let out = '';
    for (let c = 0; c < length; c += 2) out += String.fromCharCode(data.getUint16(at + c));
    return out;
  }
  return undefined;
}

/** The family a renderer will file this face under: the typographic one when it has one. */
export const familyOf = (font: Font): string | undefined => fontName(font, NAME_ID.typographicFamily) ?? fontName(font, NAME_ID.family);

/**
 * The vertical span of each contour of a glyph, in em, or `undefined` for a composite.
 *
 * A bounding box says where a glyph's ink ends; this says where it stops and starts again, which is
 * the other thing a renderer can take away from a letter. Parsed here rather than inferred because
 * the gap between a tittle and its stem is a number in the outline and nothing else in the build
 * knows it.
 *
 * A composite glyph -- an accented letter, and in Barlow the full stop's relatives too -- is drawn
 * from components rather than from contours of its own, so it answers `undefined` rather than a
 * wrong list. Nothing that asks this is composite: the letters are all simple glyphs.
 */
export function contourSpans(font: Font, char: string): { yMin: number; yMax: number }[] | undefined {
  const glyph = font.cmap.get(char.codePointAt(0) ?? -1);
  if (glyph === undefined) return undefined;
  const start = font.loca[glyph] ?? 0;
  const end = font.loca[glyph + 1] ?? start;
  if (end <= start) return [];
  const { data } = font;
  let at = font.glyfOffset + start;
  const numberOfContours = data.getInt16(at);
  if (numberOfContours < 0) return undefined;
  at += 10;
  const endPoints: number[] = [];
  for (let i = 0; i < numberOfContours; i++) {
    endPoints.push(data.getUint16(at));
    at += 2;
  }
  const numPoints = (endPoints[numberOfContours - 1] ?? -1) + 1;
  at += 2 + data.getUint16(at); // the instructions, which are skipped whole

  // Flags run-length encode themselves through bit 3; bits 1 and 2 then say how each coordinate is
  // stored and bits 4 and 5 double as the sign of a byte-sized delta or as "same as the last one".
  const flags: number[] = [];
  while (flags.length < numPoints) {
    const flag = data.getUint8(at++);
    flags.push(flag);
    if (flag & 8) for (let repeat = data.getUint8(at++); repeat > 0; repeat--) flags.push(flag);
  }
  // The x coordinates come first and are skipped, this being about the vertical.
  for (let i = 0; i < numPoints; i++) {
    const flag = flags[i] ?? 0;
    if (flag & 2) at += 1;
    else if (!(flag & 16)) at += 2;
  }
  const ys: number[] = [];
  let y = 0;
  for (let i = 0; i < numPoints; i++) {
    const flag = flags[i] ?? 0;
    if (flag & 4) {
      const delta = data.getUint8(at++);
      y += flag & 32 ? delta : -delta;
    } else if (!(flag & 32)) {
      y += data.getInt16(at);
      at += 2;
    }
    ys.push(y);
  }
  const spans: { yMin: number; yMax: number }[] = [];
  let from = 0;
  for (const endPoint of endPoints) {
    const contour = ys.slice(from, endPoint + 1);
    spans.push({ yMin: Math.min(...contour) / font.unitsPerEm, yMax: Math.max(...contour) / font.unitsPerEm });
    from = endPoint + 1;
  }
  return spans;
}

/**
 * The widest horizontal band a glyph leaves empty between two pieces of its own ink, in em.
 *
 * Zero for a letter drawn in one piece, and zero for a counter as well: the spans are merged before
 * the gaps between them are measured, so the hole in an `O` -- which is inside the letter's span and
 * not above or below it -- is not a break. What is left is the disjoint glyph: the tittle of an `i`
 * over its stem, the dot of a `!` under its stroke.
 *
 * This is the number behind the name column's case rule. A break of `g` em drawn at `fs` px is
 * `g × fs` device pixels of background, and at a break under two pixels there is no pixel row the
 * gap is certain to fall inside whatever sub-pixel phase the glyph lands at, so the renderer can
 * shade both rows grey and join the two pieces into one. `i` then reads as `l`.
 */
export function inkBreak(font: Font, char: string): number | undefined {
  const spans = contourSpans(font, char);
  if (spans === undefined) return undefined;
  if (spans.length < 2) return 0;
  const sorted = [...spans].sort((a, b) => a.yMin - b.yMin);
  let widest = 0;
  let reach = sorted[0]?.yMax ?? 0;
  for (const span of sorted.slice(1)) {
    if (span.yMin > reach) widest = Math.max(widest, span.yMin - reach);
    reach = Math.max(reach, span.yMax);
  }
  return widest;
}

if (import.meta.main) {
  const args = process.argv.slice(2);
  const json = args.includes('--json');
  const charsFlag = args.indexOf('--chars');
  const chars = charsFlag >= 0 ? (args[charsFlag + 1] ?? '') : [...Array(95)].map((_, i) => String.fromCharCode(32 + i)).join('');
  const file = args.filter((a) => !a.startsWith('--') && a !== chars)[0];
  if (!file) {
    console.error('usage: bun tools/measure-font/measure.ts [--json] [--chars "0123456789NR"] <font.ttf>');
    process.exit(2);
  }
  const font = loadFont(file);
  const rows = [...chars].map((ch) => ({ ch, ...(measure(font, ch) ?? { advance: 0, xMin: 0, xMax: 0, yMin: 0, yMax: 0 }) }));
  if (json) {
    console.log(JSON.stringify(Object.fromEntries(rows.map((r) => [r.ch, { advance: r.advance, xMin: r.xMin, xMax: r.xMax }])), null, 2));
  } else {
    console.log(`${file}  ${font.unitsPerEm} units/em, ${font.numGlyphs} glyphs`);
    console.log(' ch   advance      xMin      xMax   overhang');
    for (const r of rows) {
      const overhang = r.xMax - r.advance;
      console.log(
        `  ${r.ch}   ${r.advance.toFixed(4)}   ${r.xMin.toFixed(4)}   ${r.xMax.toFixed(4)}   ${overhang > 0 ? `+${overhang.toFixed(4)}` : overhang.toFixed(4)}`,
      );
    }
  }
}
