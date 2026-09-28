/**
 * mark: what a reading draws in place of its value, where the mark is a glyph no cell can hold.
 *
 * Rule 19 says a value is laid in cells and only what fits a cell may be drawn, and a mark is where
 * that rule and a reading pull against each other: `∞` says in one glyph that a session has no
 * clock, and it advances a third wider than the digit cell the clock beside it is laid in, so a
 * monospaced item bound to it would have WPF clip the mark exactly as it clipped the week of time
 * left the mark replaces. The same answer the pit wall header gives "MODERATE" and "12 km/h", and
 * the one `numeral` names when it refuses a banned glyph: the mark is a proportional run.
 *
 * So a marked reading is two items in one place, and the switch between them is one expression:
 * {@link marked} shows the mark, {@link unmarked} shows the value, and neither can be true with the
 * other. They are two items rather than one bound both ways because monospace is a property of the
 * item and not of the string: there is no binding that makes a cell wide for one reading and narrow
 * for the next.
 */
import type { Expr } from '../bind.ts';
import { ncalc, type HAlign, type Hex, type TextItem } from '../generator.ts';
import type { DataWeight } from '../design/metrics.ts';
import { numeral } from './numeral.ts';

const { and, not } = ncalc;

/** A mark and the state it is drawn in. */
export interface Mark {
  /** What is drawn in the value's place; measured in the advances, not in cells. */
  text: string;
  /** When it is drawn, which is when the value it replaces is not. */
  when: Expr;
}

export interface MarkOptions {
  weight?: DataWeight;
  color?: Hex;
  hAlign?: HAlign;
  /**
   * The box the mark shares with the value it stands in for, so that a right-aligned pair lands on
   * the same edge. Without it the mark takes the width of its own glyph.
   */
  width?: number;
  maxWidth?: number;
}

/** When the mark is drawn: its own state, inside whatever makes the reading visible at all. */
export const marked = (mark: Mark, visible?: Expr): Expr => (visible === undefined ? mark.when : and(visible, mark.when));

/**
 * When the value is drawn: whatever makes the reading visible, minus the mark's state.
 *
 * `undefined` for a reading that carries no mark, which is what leaves an unmarked value's Visible
 * binding exactly as it was.
 */
export const unmarked = (mark: Mark | undefined, visible?: Expr): Expr | undefined =>
  mark === undefined ? visible : visible === undefined ? not(mark.when) : and(visible, not(mark.when));

/**
 * The mark's item: the value's own place, size and face, set proportionally and measured from the
 * mark itself.
 *
 * The budget is zero cells because a proportional numeral is measured from its `widest` and never
 * from a budget; passing the value's own would measure the mark against a box cut for a clock.
 */
export function mark(name: string, spec: Mark, x: number, y: number, fs: number, visible: Expr | undefined, opts: MarkOptions = {}): TextItem {
  return numeral(name, spec.text, x, y, fs, { digits: 0, specials: 0 }, {
    proportional: true,
    widest: spec.text,
    weight: opts.weight,
    color: opts.color,
    hAlign: opts.hAlign,
    width: opts.width,
    maxWidth: opts.maxWidth,
    visibleBind: marked(spec, visible),
  });
}
