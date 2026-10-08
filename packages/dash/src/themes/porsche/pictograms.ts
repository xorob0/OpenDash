/**
 * The car's pictograms, the ISO 2575 symbols its display shows: the low beam of the top strip, and
 * down the telltale column the position lamps, the brake system warning, the general warning
 * triangle and the tyre pressure warning. The 1280 x 720 face's column of eight adds the fuel pump,
 * the charge lamp, the oil can and the limiter's dial (#713).
 *
 * Each is a picture from `design/assets.ts`, one file per state, because SimHub's format has no
 * vector item and an image carries no tint (docs/research/simhub-dash-format.md): a lit pictogram is
 * its lit file shown over its dim one, each hidden by the other's condition, so that it is lit or dim
 * as a whole and a pictogram with no reading is its dim file alone, bound to nothing.
 */
import type { ImageItem, Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { assetBox, assetNamed, imageOf } from '../../design/assets.ts';
import { roundRect } from '../../design/geometry.ts';

/** When a pictogram is lit, and which of its files it is lit in. */
export interface Lit {
  on: Expr;
  state: 'lit' | 'ink';
}

export type Pictogram = 'headlight' | 'lights' | 'warning' | 'hazard' | 'tyre' | 'fuel' | 'battery' | 'oil' | 'limiter';

/** One file of a pictogram in `box`, kept to its own proportions. */
export function picture(name: string, pictogram: Pictogram, state: 'dim' | 'lit' | 'ink', box: Rect, visible?: Expr): ImageItem {
  const asset = assetNamed(`porsche-${pictogram}-${state}`);
  if (!asset) throw new Error(`porsche: no pictogram porsche-${pictogram}-${state} in design/assets.ts`);
  return withMoreBindings({ kind: 'image', name: `${name}.${state}`, image: asset.name, rect: roundRect(assetBox(box, imageOf(asset))) } satisfies ImageItem, { Visible: visible });
}

/** A pictogram in `box`, kept to its own proportions, dim unless `lit` says otherwise. */
export function pictogram(name: string, which: Pictogram, box: Rect, lit?: Lit): Item[] {
  if (!lit) return [picture(name, which, 'dim', box)];
  return [picture(name, which, 'dim', box, ncalc.not(lit.on)), picture(name, which, lit.state, box, lit.on)];
}
