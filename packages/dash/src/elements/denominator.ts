/**
 * denominator: "/ 24" after a value, one rung down, text.secondary. Proportional rather than
 * monospaced: it is the follower and nothing follows it, so jitter does not matter.
 */
import type { TextItem } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { textBox } from '../design/metrics.ts';
import { roundRect } from '../design/geometry.ts';
import { ds, TRANSPARENT } from '../tokens.ts';

export interface DenominatorOptions {
  bind?: Expr;
  visibleBind?: Expr;
  leftBind?: Expr;
}

export function denominator(name: string, sample: string, x: number, y: number, fs: number, width: number, opts: DenominatorOptions = {}): TextItem {
  const box = textBox(y, fs);
  return {
    kind: 'text',
    name,
    rect: roundRect({ left: x, top: box.top, width, height: box.height }),
    text: sample,
    font: ds.font.data,
    fontWeight: 'SemiBold',
    fontSize: fs,
    textColor: ds.color.text.secondary,
    hAlign: 'left',
    vAlign: 'top',
    backgroundColor: TRANSPARENT,
    ...withBindings({ Text: opts.bind, Visible: opts.visibleBind, Left: opts.leftBind }),
  };
}
