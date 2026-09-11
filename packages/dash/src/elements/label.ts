/**
 * label: an uppercase field label in Barlow Medium, text.label, 15 px (13 for the small variant).
 * Proportional, never monospaced. The box spans the available width and is top aligned so the
 * baseline lands where the canvas line box puts it.
 */
import type { HAlign, Hex, TextItem } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { textBox } from '../design/metrics.ts';
import { roundRect } from '../design/geometry.ts';
import { ds, TRANSPARENT } from '../tokens.ts';

export interface LabelOptions {
  /** Font size; ds.size.label by default. */
  size?: number;
  color?: Hex;
  hAlign?: HAlign;
  /** Text binding. `text` is then the design-time sample and is not upper-cased. */
  bind?: Expr;
  visibleBind?: Expr;
  leftBind?: Expr;
}

/** A label whose canvas line box is (y, size) at x, `width` wide. */
export function label(name: string, text: string, x: number, y: number, width: number, opts: LabelOptions = {}): TextItem {
  const fs = opts.size ?? ds.size.label;
  const box = textBox(y, fs);
  return {
    kind: 'text',
    name,
    rect: roundRect({ left: x, top: box.top, width, height: box.height }),
    text: opts.bind ? text : text.toUpperCase(),
    font: ds.font.label,
    fontWeight: 'Medium',
    fontSize: fs,
    textColor: opts.color ?? ds.color.text.label,
    hAlign: opts.hAlign ?? 'left',
    vAlign: 'top',
    backgroundColor: TRANSPARENT,
    ...withBindings({ Text: opts.bind, Visible: opts.visibleBind, Left: opts.leftBind }),
  };
}

/**
 * One way of writing a label: the design-time `sample`, the `widest` string the binding can
 * produce (the longest unit, say), and the binding itself.
 */
export interface LabelForm {
  sample: string;
  /** The longest text the form can render; what it is measured by. */
  widest: string;
  bind?: Expr;
}

/**
 * The first form whose widest rendering fits `width`, else the shortest form. SimHub clips a
 * label that does not fit its box, so a card that cannot show "TYRES °C · LAST STOP" shows
 * "TYRES °C" rather than half of "STOP".
 */
export function fitLabelForm(forms: readonly LabelForm[], width: number, fs: number = ds.size.label): LabelForm {
  const last = forms[forms.length - 1];
  if (!last) throw new Error('fitLabelForm: no forms');
  return forms.find((f) => measureText('BarlowMedium', f.widest, fs) <= width) ?? last;
}
