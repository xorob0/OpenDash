/**
 * Text metrics for Barlow and Barlow Condensed as SimHub (WPF) renders them, and the conversion
 * from the design canvas's `line-height: 1` boxes to SimHub text boxes. See spec section 1.3.
 *
 * Barlow: unitsPerEm 1000, ascender 1000, descender -200, lineGap 0, capHeight 700. WPF uses a
 * 1.2 em line box with the baseline 1.0 em below the top. The canvas uses 1 em line boxes whose
 * baseline sits 0.9 em below the top. Reproducing a canvas line box (y, fs) therefore means
 * Top = y - 0.1 fs, Height = 1.2 fs, VerticalAlignment top.
 */
import type { FontWeight, Monospace } from '../generator.ts';
import { carries, type MeasuredFace } from './advances.ts';
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

/**
 * The families {@link FONT_METRICS} describes: Barlow, and Barlow Condensed under the name a package
 * installs it by (`fontFiles.ts`).
 */
const BARLOW_FAMILIES: ReadonlySet<string> = new Set(['Barlow', 'openDash Display']);

/** The segment faces the AiM's register sets its readings in (`themes/aim/register.ts`). */
const SEGMENT_FAMILIES: ReadonlySet<string> = new Set(['DSEG7 Classic', 'DSEG14 Classic']);

/**
 * How far below a box's top WPF sets the top of a segment face's cell, in em. The DSEG faces have an
 * ascent of one em, no descent and a line gap of 0.09 em, and WPF lays half the gap above the ascent,
 * so the cell starts 0.045 em down and its foot is 1.045 em down. Seen on the VM, where a ground cut
 * to the box's first em left a row of 1 to 2 px of segment feet under it (#1165).
 */
export const SEGMENT_CELL_DROP = 0.045;

/**
 * How far a figure or a capital marks above the baseline and below it, in em: the cap height and the
 * overshoot a round figure carries past it and under the baseline. Read off the bundled faces with
 * `tools/measure-font`, the 8 reaches 0.713 em in Barlow Condensed Bold and Barlow Bold and the round
 * figures dip 0.011 under the line, so a fiftieth of an em either side holds every one of them.
 */
const INK_ABOVE_BASELINE = FONT_METRICS.capHeight / FONT_METRICS.unitsPerEm + 0.02;
const INK_BELOW_BASELINE = 0.02;

/**
 * The part of a text box a run of figures or capitals can mark: from the top of the cap height and
 * its overshoot down to just under the baseline, across the whole box. A WPF box is 1.2 em tall and
 * its first 0.28 em is leading a figure never reaches, which is the room the face draws the pit banner
 * in above the gear. The AiM's segment faces mark a cell one em tall, set {@link SEGMENT_CELL_DROP} below
 * the box's top, the rest of the box being the line's slack. A face neither describes is taken to mark
 * its whole box, which is never smaller than its ink.
 */
export function inkOf(item: { rect: { left: number; top: number; width: number; height: number }; font: string; fontSize: number }): { left: number; top: number; width: number; height: number } {
  if (SEGMENT_FAMILIES.has(item.font)) {
    const top = item.rect.top + SEGMENT_CELL_DROP * item.fontSize;
    return { ...item.rect, top, height: Math.min(item.rect.top + item.rect.height, top + item.fontSize) - top };
  }
  if (!BARLOW_FAMILIES.has(item.font)) return { ...item.rect };
  const baseline = item.rect.top + WPF_BASELINE * item.fontSize;
  const top = baseline - INK_ABOVE_BASELINE * item.fontSize;
  return { left: item.rect.left, top, width: item.rect.width, height: baseline + INK_BELOW_BASELINE * item.fontSize - top };
}

export type DataWeight = 'SemiBold' | 'Bold';

/**
 * Whether the numerals are set in the label family, Barlow at normal width, rather than in the
 * condensed openDash Display. The house never does; a car theme whose display is not condensed does,
 * by pointing `font.family.data` at the label family in its overlay (#205).
 */
const DATA_IN_LABEL_FAMILY = ds.font.data === ds.font.label;

/**
 * The weight a data weight is drawn in. The label family ships in Medium and Bold alone, and a car
 * theme that draws its numerals in it draws them at 500 throughout, as the Porsche's display does, so
 * both data weights are Medium there and themselves everywhere else.
 */
/**
 * Whether the numerals are set in the seven-segment face a car theme's LCD draws (#204), which ships
 * in Regular and Bold alone: the data weights are drawn in those two there.
 */
const DATA_IN_SEGMENT_FAMILY = ds.font.data === 'DSEG7 Classic';

export const DATA_FONT_WEIGHT: Readonly<Record<DataWeight, FontWeight>> = DATA_IN_LABEL_FAMILY
  ? { SemiBold: 'Medium', Bold: 'Medium' }
  : DATA_IN_SEGMENT_FAMILY
    ? { SemiBold: 'Normal', Bold: 'Bold' }
    : { SemiBold: 'SemiBold', Bold: 'Bold' };

/** The measured face a data weight is drawn in, which is what every run set in it is measured by. */
export const DATA_FACE: Readonly<Record<DataWeight, MeasuredFace>> = DATA_IN_LABEL_FAMILY
  ? { SemiBold: 'BarlowMedium', Bold: 'BarlowMedium' }
  : DATA_IN_SEGMENT_FAMILY
    ? { SemiBold: 'DSEG7Regular', Bold: 'DSEG7Bold' }
    : { SemiBold: 'BarlowCondensedSemiBold', Bold: 'BarlowCondensedBold' };

/**
 * Whether the data faces group a whole number's thousands. They do where both carry a comma, and the
 * seven-segment face a car theme's LCD draws does not: an LCD has no comma to light, and WPF drew the
 * one in `1,350` in a face of its own, a typographic comma hanging under the segment figures (#1184).
 * The theme decides it by the face it points `font.family.data` at, so no module names a theme.
 */
const GROUPS_THOUSANDS = carries(DATA_FACE.SemiBold, ',') && carries(DATA_FACE.Bold, ',');

/** The pattern a whole number that groups its thousands is formatted in, which is the engine speed's. */
export const THOUSANDS: string = GROUPS_THOUSANDS ? '#,0' : '0';

/** A whole number written as {@link THOUSANDS} writes it, for the sample beside the binding. */
export const thousands = (n: number): string => (GROUPS_THOUSANDS ? String(n).replace(/\B(?=(\d{3})+(?!\d))/g, ',') : String(n));

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
 * The minus a signed value is drawn with: U+2212, not the hyphen-minus .NET's formatter writes,
 * which beside tabular figures reads as a dropped stroke.
 *
 * It keeps the digit cell rather than joining `SPECIAL_CHARS`. Measured from the bundled condensed
 * faces it is 0.439 em in SemiBold, 0.438 in Bold and 0.443 in Light, all of them under the 0.47
 * and 0.49 em digit cells, so a value that goes negative stays in column and no cell has to be cut
 * wider for it.
 */
export const MINUS = '−';

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
 * Wider than the digit cell, since the gear also draws N and R and the N is the widest of the set.
 * It was briefly far wider than that: WPF exposed the bundled files as one family, "Barlow", with
 * the condensed faces as a stretch of it, so a request for "Barlow Condensed" reached a face about
 * a fifth wider and the cell had to hold whichever Barlow the renderer picked. The bundled family
 * is now renamed, so the cell is the condensed face's own again.
 */
export const GEAR_CELL = ds.font.cell.gear;

/**
 * Integer monospace cells for the gear: the letter-wide cell with the Bold face's special cell.
 *
 * Rounded up, as `cells` is. This rounded to nearest while the cell was 0.68 em and had a sixth of
 * an em of headroom to absorb it; at 0.52 em the headroom is 0.006 em, so below about 83 px a cell
 * rounded down would be narrower than the "N" it has to hold.
 */
export function gearCells(fs: number): Monospace {
  return { ...cells('Bold', fs), charWidth: Math.ceil(GEAR_CELL * fs) };
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
