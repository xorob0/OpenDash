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

/** The unrounded SimHub box (top, height) that reproduces a canvas line box. */
export function textBox(y: number, fs: number): { top: number; height: number } {
  return { top: y - (LINE_SPACING - WPF_BASELINE - (1 - CANVAS_BASELINE)) * fs, height: LINE_SPACING * fs };
}

/** Where a canvas line box's baseline sits. */
export const canvasBaseline = (y: number, fs: number): number => y + CANVAS_BASELINE * fs;

/** The canvas line box top that puts a run of size `fs` on a given baseline. */
export const canvasYForBaseline = (baseline: number, fs: number): number => baseline - CANVAS_BASELINE * fs;

export type DataWeight = 'SemiBold' | 'Bold';

/** Monospace cell widths as a fraction of the font size, per face, measured from the TTFs. */
export const CELL: Record<DataWeight, { digit: number; special: number }> = {
  SemiBold: { digit: 0.46, special: 0.26 },
  Bold: { digit: 0.49, special: 0.28 },
};

/** Characters that get the narrow cell. SimHub's default; written explicitly so `-` stays a digit cell. */
export const SPECIAL_CHARS = '.,:';

/** Integer monospace cells for a face at a font size. */
export function cells(weight: DataWeight, fs: number): Monospace {
  const c = CELL[weight];
  return { charWidth: Math.round(c.digit * fs), specialCharsWidth: Math.round(c.special * fs), specialChars: SPECIAL_CHARS };
}

/**
 * The gear's cell as a fraction of the font size. SimHub reports "N" and "R" as well as digits,
 * and Barlow Condensed Bold "N" advances 0.514 em (measured from the TTF), wider than any digit
 * ("4", the widest, is 0.484), so the gear cannot use the face's digit cell: at 260 px that is
 * 127 px and "N" (134 px) clips. 0.52 em is 135 px at 260.
 */
export const GEAR_CELL = 0.52;

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
