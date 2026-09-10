/**
 * readoutRow: a readout plus a follower on the value's baseline, a denominator one rung down or
 * a 13 px unit. The follower's Left is bound to the value's digit count when the value's length
 * varies, so "3 / 24" and "12 / 24" keep the same gap.
 */
import type { Item, Monospace, Rect } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { canvasBaseline, canvasYForBaseline, cells, monoWidth, type Chars } from '../design/metrics.ts';
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
}

export function readoutRow(slot: Rect, rung: RungSpec, prefix: string, lbl: LabelSpec, value: ValueSpec, follower: FollowerSpec): Item[] {
  const items = readout(slot, rung, prefix, lbl, value);
  const g = readoutGeometry(slot, rung);
  const mono = cells(value.weight ?? 'SemiBold', g.valueFs);
  const gap = ds.space[2];
  const staticLeft = g.x + monoWidth(mono, follower.after) + gap;
  const maxLeft = g.x + monoWidth(mono, follower.maxAfter ?? value.chars) + gap;
  const width = Math.max(0, g.x + g.innerWidth - maxLeft);
  const baseline = canvasBaseline(g.valueY, g.valueFs);
  const leftBind = follower.leftBind?.({ x: g.x, mono, gap });
  const opts = { bind: follower.bind, visibleBind: follower.visibleBind, leftBind };
  if (follower.kind === 'denominator') {
    const fs = rung.denominator;
    items.push(denominator(`${prefix}denominator`, follower.sample, staticLeft, canvasYForBaseline(baseline, fs), fs, width, opts));
  } else {
    const fs = ds.size.labelSm;
    items.push(unit(`${prefix}unit`, follower.sample, staticLeft, canvasYForBaseline(baseline, fs), width, opts));
  }
  return items;
}
