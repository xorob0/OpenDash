/**
 * numeral: a value in Barlow Condensed SemiBold (Bold for the gear), text.primary unless told
 * otherwise, drawn in monospace cells so that a ticking value never jitters. The box is exactly
 * `chars` cells wide, which is what lets a follower be positioned from a digit count.
 */
import type { HAlign, Hex, Monospace, TextItem } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { cells, monoWidth, textBox, type Chars, type DataWeight } from '../design/metrics.ts';
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
  return {
    kind: 'text',
    name,
    rect: roundRect({ left: x, top: box.top, width: monoWidth(mono, chars), height: box.height }),
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
