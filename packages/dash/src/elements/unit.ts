/**
 * unit: the small label that follows a value or sits under the speed.
 *
 * The size is the density's, not a constant. It used to be `ds.size.labelSm` whatever the caller
 * was working in, which is fine while every density's small label is 13 px and wrong the moment
 * one is not: a compact zone measures a follower at 12 and drew it at 13, so "km/h" was measured
 * into a 29 px box and drawn 30 px wide. A box measured at one size and drawn at another is the
 * same fault as a box measured in the wrong weight, and WPF clips both the same way.
 */
import type { TextItem } from '../generator.ts';
import { label, type LabelOptions } from './label.ts';
import { ds } from '../tokens.ts';

export type UnitOptions = LabelOptions;

export function unit(name: string, text: string, x: number, y: number, width: number, opts: UnitOptions = {}): TextItem {
  return label(name, text, x, y, width, { size: ds.size.labelSm, color: ds.color.text.secondary, ...opts });
}
