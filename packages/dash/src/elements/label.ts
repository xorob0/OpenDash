/**
 * label: a field label in Barlow Medium, text.label, 15 px (13 for the small variant), drawn in the
 * case it is written in. Proportional, never monospaced. The box spans the available width and is
 * top aligned so the baseline lands where the canvas line box puts it.
 *
 * The case is the caller's, and it is the case a product writes the word in: a label in sentence
 * case (`Fuel left`, `Best lap`), a unit in its symbol's (`km/h`, `kPa`), and capitals only where
 * the word is a name in capitals on its own account, a flag, an acronym or aid (`RPM`, `ABS`,
 * `DRS`) or a class. Those capitals are typed in the string rather than applied here, so that a
 * search for a capitalised literal finds every one of them. docs/design/brand.md holds the rule.
 */
import type { FontWeight, HAlign, Hex, TextItem } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { textBox } from '../design/metrics.ts';
import { roundRect } from '../design/geometry.ts';
import { ds, TRANSPARENT } from '../tokens.ts';

export interface LabelOptions {
  /** Font size; ds.size.label by default. */
  size?: number;
  color?: Hex;
  /**
   * The face the label is drawn in; Medium by default. Only a weight the package ships and
   * design/advances.ts measures can be asked for, since a run measured in one weight and drawn in
   * another is clipped by WPF without a word.
   */
  weight?: FontWeight;
  /** TextColor binding, for a label whose ink says something: a telltale lit or unlit. */
  colorBind?: Expr;
  hAlign?: HAlign;
  /** Text binding. `text` is then the design-time sample. */
  bind?: Expr;
  /** The widest string `bind` can produce. The box should be measured from it, and the fit tests are. */
  widest?: string;
  visibleBind?: Expr;
  leftBind?: Expr;
}

/** A label whose canvas line box is (y, size) at x, `width` wide. */
export function label(name: string, text: string, x: number, y: number, width: number, opts: LabelOptions = {}): TextItem {
  const fs = opts.size ?? ds.size.label;
  const box = textBox(y, fs);
  return withMoreBindings({
    kind: 'text',
    name,
    rect: roundRect({ left: x, top: box.top, width, height: box.height }),
    text,
    font: ds.font.label,
    fontWeight: opts.weight ?? 'Medium',
    fontSize: fs,
    textColor: opts.color ?? ds.color.text.label,
    hAlign: opts.hAlign ?? 'left',
    vAlign: 'top',
    backgroundColor: TRANSPARENT,
    ...(opts.widest ? { widest: opts.widest } : {}),
  }, { Text: opts.bind, TextColor: opts.colorBind, Visible: opts.visibleBind, Left: opts.leftBind });
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
 * label that does not fit its box, so a card that cannot show "Tyres °C · last stop" shows
 * "Tyres °C" rather than half of "stop".
 */
export function fitLabelForm(forms: readonly LabelForm[], width: number, fs: number = ds.size.label): LabelForm {
  const last = forms[forms.length - 1];
  if (!last) throw new Error('fitLabelForm: no forms');
  return forms.find((f) => measureText('BarlowMedium', f.widest, fs) <= width) ?? last;
}
