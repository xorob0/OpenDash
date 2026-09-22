/**
 * numeral: a value in Barlow Condensed SemiBold (Bold for the gear), text.primary unless told
 * otherwise, drawn in monospace cells so that a ticking value never jitters.
 *
 * The character budget `chars` is what a follower is positioned from, but the box is drawn a
 * little wider than the budget: SimHub gives WPF the box as `MaxTextWidth` and WPF clips what
 * does not fit, so a box sized to the exact text loses the last glyph's final pixels. The slack
 * is invisible (the box is transparent and the text is left aligned) and never moves a
 * follower, which is positioned from the cells.
 */
import type { HAlign, Hex, Monospace, TextItem } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { measureText, type MeasuredFace } from '../design/advances.ts';
import { boxSlack, cells, monoWidth, textBox, type Chars, type DataWeight } from '../design/metrics.ts';
import { roundRect } from '../design/geometry.ts';
import { ds, TRANSPARENT } from '../tokens.ts';

export interface NumeralOptions {
  weight?: DataWeight;
  /** Cells to draw in instead of the face's at this size; the gear's letter-wide cell. */
  mono?: Monospace;
  /** Static colour; text.primary by default. */
  color?: Hex;
  /** TextColor binding. */
  colorBind?: Expr;
  hAlign?: HAlign;
  /** Widest the box may be, typically the card's inner width; the slack is capped by it. */
  maxWidth?: number;
  /**
   * The box's exact width, for a value centred or right-aligned in a column it does not fill.
   *
   * `maxWidth` is a cap and cannot do this: it leaves the box at the width of the cells, so
   * `hAlign: 'center'` centres the value inside its own glyphs and the column's centre is never
   * involved. Zone A's speed sat hard against the left edge of a 380 px column for exactly that
   * reason.
   */
  width?: number;
  /**
   * Set in the face's own advances rather than in cells, for the one kind of value that is not a
   * number: the class and the position after it, drawn as "GT3 · P4" in a single run. A number
   * keeps its cells, because a proportional one jitters as its digits change.
   */
  proportional?: boolean;
  /** The widest string the binding can produce; a proportional value is measured from it. */
  widest?: string;
  /** Text binding. `sample` is the design-time text. */
  bind?: Expr;
  visibleBind?: Expr;
  leftBind?: Expr;
}

/** The measured face a data weight is set in, for a value drawn proportionally. */
const DATA_FACE: Record<DataWeight, MeasuredFace> = { SemiBold: 'BarlowCondensedSemiBold', Bold: 'BarlowCondensedBold' };

/** A numeral whose canvas line box is (y, fs) at x, sized for `chars`. */
export function numeral(name: string, sample: string, x: number, y: number, fs: number, chars: Chars, opts: NumeralOptions = {}): TextItem {
  const weight = opts.weight ?? 'SemiBold';
  const mono = opts.proportional ? undefined : (opts.mono ?? cells(weight, fs));
  // The cell is cut for digits and `font.cell.excluded` is the set whose ink overruns it in every
  // condensed face, at any size, so no cell a value is drawn in can hold one. WPF says nothing when
  // it clips, and a clipped `#` reads as the wrong font rather than as too wide a glyph, which is
  // how thirty-one of them shipped; failing the build is the only place it can be noticed.
  if (mono) {
    const banned = [...sample, ...(opts.widest ?? '')].find((ch) => ds.font.cell.excluded.has(ch));
    if (banned !== undefined) {
      throw new Error(`numeral ${name}: ${JSON.stringify(banned)} cannot be drawn in a monospace cell (font.cell.excluded); split it into a proportional label beside the value`);
    }
  }
  const box = textBox(y, fs);
  const budget = mono === undefined ? Math.ceil(measureText(DATA_FACE[weight], opts.widest ?? sample, fs)) : monoWidth(mono, chars);
  const wanted = budget + boxSlack(fs);
  // Floored, so that a box whose left rounds up still ends inside the room it was given.
  const capped = opts.maxWidth === undefined ? wanted : Math.max(budget, Math.min(wanted, Math.floor(opts.maxWidth)));
  const width = opts.width === undefined ? capped : Math.max(budget, Math.floor(opts.width));
  return withMoreBindings({
    kind: 'text',
    name,
    rect: roundRect({ left: x, top: box.top, width, height: box.height }),
    text: sample,
    font: ds.font.data,
    fontWeight: weight,
    fontSize: fs,
    textColor: opts.color ?? ds.color.text.primary,
    hAlign: opts.hAlign ?? 'left',
    vAlign: 'top',
    ...(mono ? { monospace: mono } : {}),
    ...(opts.widest ? { widest: opts.widest } : {}),
    backgroundColor: TRANSPARENT,
  }, { Text: opts.bind, TextColor: opts.colorBind, Visible: opts.visibleBind, Left: opts.leftBind });
}
