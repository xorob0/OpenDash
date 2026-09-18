/**
 * bar: a level gauge at the one height the element sheet gives it, four pixels, with the track in
 * surface.raised and the fill in text.primary.
 *
 * Those two colours are already what `levelGauge` defaults to, so what this adds is the name the
 * sheet uses and the height it fixes: a page asks for a bar rather than for a gauge it has to
 * decide two colours and a height for. `levelGauge` keeps its own signature, since a gauge that
 * has to be another height (a tyre's wear column) or another pair of colours (the fuel bar under a
 * lap of fuel) still needs it.
 *
 * It is the one element not built from nothing: the gauge item lives in `second/` because the
 * second screens drew the first one, and moving it there would be a change to a file this has no
 * business touching.
 */
import type { LinearGaugeItem } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { levelGauge, type LevelGaugeOptions } from '../second/gauge.ts';

/** The bar's height, which the canvas fixes rather than putting on a ramp. */
export const BAR_HEIGHT = 4;

/** A horizontal bar `width` wide at (x, y), filling from the left. */
export function bar(name: string, x: number, y: number, width: number, valueBind: Expr, opts: LevelGaugeOptions = {}): LinearGaugeItem {
  return levelGauge(name, rect(x, y, width, BAR_HEIGHT), valueBind, opts);
}
