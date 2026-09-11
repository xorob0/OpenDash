/**
 * A class chip: the car class as a word on a raised block, never as a colour. Identity on the
 * second screens is labelled, because a colour-coded class is unreadable to a fifth of drivers
 * and iRacing's own class colours clash with the state colours this design reserves.
 *
 * The player's own class is inverted (light block, dark text), which is also how a PIT marker is
 * drawn. The text is cut to four characters by NCalc rather than by the renderer, so a long class
 * name shortens instead of being clipped mid-letter.
 */
import type { Hex, Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect, roundRect } from '../design/geometry.ts';
import { textBox } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { ds } from '../tokens.ts';
import { densityOf, type Density } from './density.ts';

const { left, ucase, iff, str, isnull } = ncalc;

/** Characters a class chip shows. "LMP2" and "GTP" fit; a longer class name is cut to this. */
export const CHIP_CHARS = 4;

/** The widest four letters a class name realistically has, which is what a chip is sized by. */
export const CHIP_WIDEST = 'LMP2';

/**
 * A tyre compound chip shows one letter, since that is what a compound is: H, M, S, W. The column
 * is drawn one letter wide, so the binding has to be cut to one as well; "MEDI" in a box built for
 * "M" is the same clipping fault by another route.
 */
export const COMPOUND_CHARS = 1;
/** "W" is the widest capital a compound uses, and what a compound chip is measured by. */
export const COMPOUND_WIDEST = 'W';

export interface ChipOptions {
  /** Live text; `text` is then the design-time sample. */
  bind?: Expr;
  /** True (or an expression) draws the inverted chip: light block, dark text. */
  inverted?: boolean;
  invertedBind?: Expr;
  visibleBind?: Expr;
  /** Override the measured width, e.g. to line a chip up with a table column. */
  width?: number;
  /**
   * The widest string `bind` can draw. `chipText` cuts to `CHIP_CHARS`, so that is the default for
   * a bound chip; a caller that knows its binding is narrower may say so and get a narrower box.
   */
  widest?: string;
}

/** Width a chip takes at a density: padding, four characters, padding. */
export function chipWidth(density: Density, widest: string = CHIP_WIDEST): number {
  const d = densityOf(density);
  return Math.ceil(2 * d.chipPadding + measureText('BarlowMedium', widest, d.labelSm));
}

/** Cuts a name to what the chip can hold, upper-cased. */
export const chipText = (expr: Expr, chars: number = CHIP_CHARS): Expr => ucase(left(isnull(expr, str('')), chars));

/**
 * A chip whose top edge is `top`. The block is the chip's height; the text sits on the canvas
 * line box that centres it in the block.
 */
export function chip(name: string, text: string, x: number, top: number, density: Density, opts: ChipOptions = {}): Item[] {
  const d = densityOf(density);
  const width = opts.width ?? chipWidth(density);
  const box = rect(x, top, width, d.chipHeight);
  const fill: Hex = opts.inverted ? ds.color.text.primary : ds.color.surface.raised;
  const ink: Hex = opts.inverted ? ds.color.surface.base : ds.color.text.secondary;
  const textY = top + (d.chipHeight - d.labelSm) / 2;
  const fillBind = opts.invertedBind ? iff(opts.invertedBind, str(ds.color.text.primary), str(ds.color.surface.raised)) : undefined;
  const inkBind = opts.invertedBind ? iff(opts.invertedBind, str(ds.color.surface.base), str(ds.color.text.secondary)) : undefined;
  const block = band(`${name}.block`, box, fill, { visibleBind: opts.visibleBind });
  return [
    { ...block, ...withBindings({ Visible: opts.visibleBind, BackgroundColor: fillBind }) },
    {
      ...label(`${name}.text`, text, x + d.chipPadding, textY, width - 2 * d.chipPadding, {
        size: d.labelSm,
        color: ink,
        hAlign: 'center',
        bind: opts.bind,
        widest: opts.bind ? (opts.widest ?? CHIP_WIDEST) : undefined,
        visibleBind: opts.visibleBind,
      }),
      ...withBindings({ Text: opts.bind, Visible: opts.visibleBind, TextColor: inkBind }),
    },
  ];
}

/** The rect a chip occupies, for a caller that lays a row out by rects. */
export const chipRect = (x: number, top: number, density: Density, width?: number): Rect =>
  roundRect(rect(x, top, width ?? chipWidth(density), densityOf(density).chipHeight));

/** The chip's text box, exposed so a test can check what it holds. */
export const chipTextBox = (top: number, density: Density) => textBox(top + (densityOf(density).chipHeight - densityOf(density).labelSm) / 2, densityOf(density).labelSm);
