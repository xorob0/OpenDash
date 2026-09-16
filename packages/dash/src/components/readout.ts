/**
 * readout: a label with a numeral under it, the block centred vertically in the slot.
 * Block height = label + 5 + value; the label sits at the block top and the value 20 below (at L).
 */
import type { Hex, Item, Rect } from '../generator.ts';
import type { Expr } from '../bind.ts';
import type { Chars, DataWeight } from '../design/metrics.ts';
import type { RungSpec } from '../design/rung.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { ds } from '../tokens.ts';
import { cardFrame, centredTop } from './frame.ts';

/**
 * Gap between a readout's label row and its value row, the same 5 px the canvas draws between the
 * two rows of a second-screen field. Off the `space` scale, which goes 4 then 8.
 */
export const READOUT_GAP = 5;

export interface LabelSpec {
  /** Literal text (upper-cased) or, with `bind`, the design-time sample. */
  text: string;
  bind?: Expr;
}

export interface ValueSpec {
  sample: string;
  bind: Expr;
  /** Character budget: the box is exactly this many cells wide. */
  chars: Chars;
  color?: Hex;
  colorBind?: Expr;
  weight?: DataWeight;
}

export interface ReadoutGeometry {
  x: number;
  innerWidth: number;
  /** Canvas line box tops. */
  labelY: number;
  valueY: number;
  valueFs: number;
}

export function readoutGeometry(slot: Rect, rung: RungSpec): ReadoutGeometry {
  const f = cardFrame(slot, rung);
  const labelFs = ds.size.label;
  const gap = READOUT_GAP;
  const top = centredTop(slot, labelFs + gap + rung.value);
  return { x: f.x, innerWidth: f.innerWidth, labelY: top, valueY: top + labelFs + gap, valueFs: rung.value };
}

export function readout(slot: Rect, rung: RungSpec, prefix: string, lbl: LabelSpec, value: ValueSpec): Item[] {
  const g = readoutGeometry(slot, rung);
  return [
    label(`${prefix}label`, lbl.text, g.x, g.labelY, g.innerWidth, { bind: lbl.bind }),
    numeral(`${prefix}value`, value.sample, g.x, g.valueY, g.valueFs, value.chars, {
      maxWidth: slot.left + slot.width - g.x,
      bind: value.bind,
      color: value.color,
      colorBind: value.colorBind,
      weight: value.weight,
    }),
  ];
}
