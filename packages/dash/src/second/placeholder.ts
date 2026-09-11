/**
 * What a module draws when the sim cannot answer it. Three modules ship this way on iRacing:
 * virtual energy is a Le Mans Ultimate feature, iRacing publishes no damage values at all, and
 * per-segment rival timing is not a SimHub property.
 *
 * They say so rather than showing zeros. A dashboard that invents a number is worse than one that
 * admits it has none, and the modules stay in the catalogue so another sim can switch them on.
 */
import type { Item, Rect } from '../generator.ts';
import { label } from '../elements/label.ts';
import { ds } from '../tokens.ts';
import { densityOf, type Density } from './density.ts';

/** A centred line in text.dim, the module's name first and then why there is nothing to show. */
export function placeholder(name: string, text: string, frame: Rect, density: Density): Item[] {
  const d = densityOf(density);
  const y = frame.top + (frame.height - d.label) / 2;
  return [label(`${name}.placeholder`, text, frame.left, y, frame.width, { size: d.label, color: ds.color.text.dim, hAlign: 'center' })];
}
