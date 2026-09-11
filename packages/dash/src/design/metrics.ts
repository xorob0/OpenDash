/**
 * Text metrics for Barlow and Barlow Condensed as SimHub (WPF) renders them, and the conversion
 * from the design canvas's `line-height: 1` boxes to SimHub text boxes. See spec section 1.3.
 *
 * Barlow: unitsPerEm 1000, ascender 1000, descender -200, lineGap 0, capHeight 700. WPF uses a
 * 1.2 em line box with the baseline 1.0 em below the top. The canvas uses 1 em line boxes whose
 * baseline sits 0.9 em below the top. Reproducing a canvas line box (y, fs) therefore means
 * Top = y - 0.1 fs, Height = 1.2 fs, VerticalAlignment top.
 */
import type { Monospace } from '../generator.ts';
import { ds } from '../tokens.ts';

export const FONT_METRICS = { unitsPerEm: 1000, ascender: 1000, descender: -200, lineGap: 0, capHeight: 700, xHeight: 510 } as const;

/** WPF line box height, in em. */
export const LINE_SPACING = 1.2;
/** Baseline below a WPF (top aligned, unpadded) text box top, in em. */
export const WPF_BASELINE = 1.0;
/** Baseline below a canvas `line-height: 1` box top, in em. */
export const CANVAS_BASELINE = 0.9;

/** A design-canvas line box: top edge `y`, font size (and height) `fs`. */
export interface LineBox {
  y: number;
  fs: number;
}

/**
 * Slack added to a text box, in pixels. SimHub renders text with WPF's `MaxTextWidth` and
 * `MaxTextHeight` set to the box, and WPF clips whatever does not fit: a box sized to the exact
 * text is a box whose last glyph loses its final pixels. Every box therefore gets a pixel of
 * room in each axis on top of rounding up.
 */
export const BOX_SLACK = 1;

/** Slack for a text box of font size `fs`: enough for the last glyph, never enough to notice. */
export const boxSlack = (fs: number): number => Math.max(2, Math.ceil(0.05 * fs));

/**
 * The SimHub box (top, height) that reproduces a canvas line box. The height is rounded up and
 * given its pixel of slack, since 1.2 x 46 = 55.2 rounds down to 55 and WPF would then clip the
 * line it is meant to hold.
 */
export function textBox(y: number, fs: number): { top: number; height: number } {
  return {
    top: y - (LINE_SPACING - WPF_BASELINE - (1 - CANVAS_BASELINE)) * fs,
    height: Math.ceil(LINE_SPACING * fs) + BOX_SLACK,
  };
}

/** Where a canvas line box's baseline sits. */
export const canvasBaseline = (y: number, fs: number): number => y + CANVAS_BASELINE * fs;

/** The canvas line box top that puts a run of size `fs` on a given baseline. */
export const canvasYForBaseline = (baseline: number, fs: number): number => baseline - CANVAS_BASELINE * fs;

export type DataWeight = 'SemiBold' | 'Bold';

/**
 * Monospace cell widths as a fraction of the font size, per face, from the tokens. The cell holds
 * every glyph a value can draw, not only the digits: "OFF" puts an "O" (0.467 em in SemiBold,
 * wider than the widest digit "4" at 0.459) through the same cell.
 *
 * Read rather than repeated. These were copied here once and the gear's cell then drifted from the
 * token by a sixth, which is how a "6" came to be photographed with its bowl flattened against the
 * cell edge.
 */
export const CELL: Record<DataWeight, { digit: number; special: number }> = {
  SemiBold: { ...ds.font.cell.semiBold },
  Bold: { ...ds.font.cell.bold },
};

/** Characters that get the narrow cell. SimHub's default; written explicitly so `-` stays a digit cell. */
export const SPECIAL_CHARS = ds.font.cell.specialChars;

/**
 * Integer monospace cells for a face at a font size. Rounded up, never down: SimHub draws each
 * character inside its cell, so a cell narrower than the glyph's advance clips the glyph.
 */
export function cells(weight: DataWeight, fs: number): Monospace {
  const c = CELL[weight];
  return { charWidth: Math.ceil(c.digit * fs), specialCharsWidth: Math.ceil(c.special * fs), specialChars: SPECIAL_CHARS };
}

/**
 * The gear's cell as a fraction of the font size, from the tokens.
 *
 * It is wider than the widest advance in Barlow Condensed Bold, and deliberately so. SimHub does
 * not draw the gear in Barlow Condensed at all: WPF exposes the bundled files as one family,
 * "Barlow", with the condensed faces as a stretch of it, so a request for "Barlow Condensed"
 * reaches a non-condensed face whose glyphs are about a fifth wider. The token holds a cell wide
 * enough for whichever Barlow the renderer picks; the resolution itself is XOR-84.
 */
export const GEAR_CELL = ds.font.cell.gear;

/** Integer monospace cells for the gear: the letter-wide cell with the Bold face's special cell. */
export function gearCells(fs: number): Monospace {
  return { ...cells('Bold', fs), charWidth: Math.round(GEAR_CELL * fs) };
}

/** Maximum character budget of a monospaced numeral. */
export interface Chars {
  /** Digits, signs, letters and any other character drawn in the digit cell. */
  digits: number;
  /** Characters from SPECIAL_CHARS. */
  specials: number;
}

/** Exact width of a monospaced string: digits and specials in their cells. */
export const monoWidth = (mono: Monospace, chars: Chars): number => chars.digits * mono.charWidth + chars.specials * mono.specialCharsWidth;
