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
import { ncalc } from '../generator.ts';
import { label, type LabelOptions } from './label.ts';
import { ds } from '../tokens.ts';

export type UnitOptions = LabelOptions;

export function unit(name: string, text: string, x: number, y: number, width: number, opts: UnitOptions = {}): TextItem {
  // Resolved key by key rather than spread over the defaults. A caller that builds its options
  // object unconditionally — `{ color: follower.color }` in `second/field.ts` — passes the key with
  // the value `undefined`, the spread overwrote text.secondary with it, and `label` then fell back
  // to text.label: every unit following a value was drawn #5A6069 where the canvas draws #8A9099.
  //
  // The case is not the caller's. The canvas writes `s`, `psi` and `km/h` in its markup and sets
  // `.lbl-sm` to `text-transform: uppercase`, so what it draws is `S`, `PSI` and `KM/H`; reading
  // the markup rather than the rendering is what once drew the stint's last stop in a case the
  // sheet does not have. A bound unit goes through SimHub's own `ucase`, because the sim sends the
  // spelling and the style is ours, and `widest` is upper-cased with it or the box is measured
  // from a narrower string than the one drawn and WPF clips the difference.
  return label(name, text, x, y, width, {
    ...opts,
    size: opts.size ?? ds.size.labelSm,
    color: opts.color ?? ds.color.text.secondary,
    ...(opts.bind ? { bind: ncalc.ucase(opts.bind) } : {}),
    ...(opts.widest ? { widest: opts.widest.toUpperCase() } : {}),
  });
}
