/**
 * What a module draws when the sim cannot answer it. Three modules ship this way on iRacing:
 * virtual energy is a Le Mans Ultimate feature, iRacing publishes no damage values at all, and
 * per-segment rival timing is not a SimHub property.
 *
 * They say so rather than showing zeros. A dashboard that invents a number is worse than one that
 * admits it has none, and the modules stay in the catalogue so another sim can switch them on.
 */
import type { Item, Rect } from '../generator.ts';
import { measureText } from '../design/advances.ts';
import { label } from '../elements/label.ts';
import { ds } from '../tokens.ts';
import { densityOf, type Density } from './density.ts';

/**
 * A centred line in text.dim, the module's name first and then why there is nothing to show.
 *
 * The line shrinks to the box rather than running out of it. It is one line of prose and there is
 * nothing in it to shed, so this is the third place in the repository where a value gets smaller
 * instead -- and a sentence explaining that a reading is missing, itself clipped, is the worst of
 * both.
 */
export function placeholder(name: string, text: string, frame: Rect, density: Density): Item[] {
  const d = densityOf(density);
  let size = d.label;
  while (size > 8 && measureText('BarlowMedium', text, size) > frame.width) size -= 1;
  const y = frame.top + (frame.height - size) / 2;
  return [label(`${name}.placeholder`, text, frame.left, y, frame.width, { size, color: ds.color.text.dim, hAlign: 'center' })];
}
