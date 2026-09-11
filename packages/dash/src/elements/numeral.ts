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
import { withBindings, type Expr } from '../bind.ts';
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
  /** Text binding. `sample` is the design-time text. */
  bind?: Expr;
  visibleBind?: Expr;
  leftBind?: Expr;
}

/** A numeral whose canvas line box is (y, fs) at x, sized for `chars`. */
export function numeral(name: string, sample: string, x: number, y: number, fs: number, chars: Chars, opts: NumeralOptions = {}): TextItem {
  const weight = opts.weight ?? 'SemiBold';
  const mono = opts.mono ?? cells(weight, fs);
  const box = textBox(y, fs);
  const budget = monoWidth(mono, chars);
  const wanted = budget + boxSlack(fs);
  // Floored, so that a box whose left rounds up still ends inside the room it was given.
  const capped = opts.maxWidth === undefined ? wanted : Math.max(budget, Math.min(wanted, Math.floor(opts.maxWidth)));
  const width = opts.width === undefined ? capped : Math.max(budget, Math.floor(opts.width));
  return {
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
    monospace: mono,
    backgroundColor: TRANSPARENT,
    ...withBindings({ Text: opts.bind, TextColor: opts.colorBind, Visible: opts.visibleBind, Left: opts.leftBind }),
  };
}
