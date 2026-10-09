/**
 * readoutRow: a readout plus a follower on the value's baseline, a denominator one rung down or
 * a 13 px unit. The follower's Left is bound to the value's digit count when the value's length
 * varies, so "3 / 24" and "12 / 24" keep the same gap.
 */
import type { Item, Monospace, Rect } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { boxSlack, canvasBaseline, canvasYForBaseline, cells, DATA_FACE, monoWidth, type Chars } from '../design/metrics.ts';
import type { RungSpec } from '../design/rung.ts';
import { denominator } from '../elements/denominator.ts';
import { unit } from '../elements/unit.ts';
import { ds } from '../tokens.ts';
import { readout, readoutGeometry, type LabelSpec, type ValueSpec } from './readout.ts';

export interface FollowerContext {
  /** Left edge of the value. */
  x: number;
  /** The value's monospace cells. */
  mono: Monospace;
  /** Gap between value and follower. */
  gap: number;
}

export interface FollowerSpec {
  kind: 'denominator' | 'unit';
  sample: string;
  bind?: Expr;
  /** Design-time position: the follower sits after this many value characters. */
  after: Chars;
  /** The most value characters the follower can follow; sizes the follower's box. Defaults to the value's budget. */
  maxAfter?: Chars;
  /** Runtime Left expression, typically `x + digitCount * charWidth + gap`. */
  leftBind?: (ctx: FollowerContext) => Expr;
  visibleBind?: Expr;
  /** The longest text `bind` can produce, which the fit tests measure the follower's box by. */
  widest?: string;
}

export function readoutRow(slot: Rect, rung: RungSpec, prefix: string, lbl: LabelSpec, value: ValueSpec, follower: FollowerSpec): Item[] {
  const items = readout(slot, rung, prefix, lbl, value);
  const g = readoutGeometry(slot, rung);
  const mono = cells(value.weight ?? 'SemiBold', g.valueFs);
  const gap = ds.space[2];
  const staticLeft = g.x + monoWidth(mono, follower.after) + gap;
  const maxLeft = g.x + monoWidth(mono, follower.maxAfter ?? value.chars) + gap;
  // What is left of the card's inner width, or as much of its padding as the follower's longest
  // reading needs, as the value's own box may take: the box is transparent and the padding is never
  // drawn into, while a box cut at the inner edge cut the `/ 120` of a long race on a 480 round. #596.
  const inner = g.x + g.innerWidth - maxLeft;
  const needs = follower.widest === undefined || follower.kind !== 'denominator' ? 0 : Math.ceil(measureText(DATA_FACE.SemiBold, follower.widest, rung.denominator)) + boxSlack(rung.denominator);
  const width = Math.max(0, Math.min(Math.max(inner, needs), slot.left + slot.width - maxLeft));
  const baseline = canvasBaseline(g.valueY, g.valueFs);
  const leftBind = follower.leftBind?.({ x: g.x, mono, gap });
  const opts = { bind: follower.bind, visibleBind: follower.visibleBind, leftBind };
  if (follower.kind === 'denominator') {
    const fs = rung.denominator;
    items.push(denominator(`${prefix}denominator`, follower.sample, staticLeft, canvasYForBaseline(baseline, fs), fs, width, { ...opts, widest: follower.widest }));
  } else {
    const fs = ds.size.labelSm;
    items.push(unit(`${prefix}unit`, follower.sample, staticLeft, canvasYForBaseline(baseline, fs), width, opts));
  }
  return items;
}
