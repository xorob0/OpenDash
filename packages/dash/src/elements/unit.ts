/** unit: the small label (13 px, text.secondary) that follows a value or sits under the speed. */
import type { TextItem } from '../generator.ts';
import { label, type LabelOptions } from './label.ts';
import { ds } from '../tokens.ts';

export type UnitOptions = Omit<LabelOptions, 'size'>;

export function unit(name: string, text: string, x: number, y: number, width: number, opts: UnitOptions = {}): TextItem {
  return label(name, text, x, y, width, { size: ds.size.labelSm, color: ds.color.text.secondary, ...opts });
}
