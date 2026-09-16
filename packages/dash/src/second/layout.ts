/**
 * Placing rows inside a module's box. A module is a column of rows centred in its rect, which is
 * what the design canvas draws with `justify-content: center`, and a row is placed by its bottom
 * edge because the fields in it are bottom-aligned.
 *
 * A row that does not fit is not drawn: zones come in six heights (195 to 356 px) and a module
 * drops its last row rather than letting SimHub clip it.
 */
import type { Item, Rect } from '../generator.ts';
import { rect } from '../design/geometry.ts';
import { densityOf, type Density } from './density.ts';

/**
 * One row of a module: how tall it is, and how it draws itself once its bottom edge is known.
 *
 * `fill` is the row's half of rule 20. A row of fields can be drawn larger; a gauge, a trace or a
 * table cannot, and says so by leaving it undefined. `ceiling` is how far this row may grow, `lead`
 * the size of its largest value -- which is what the stack steps, a pixel at a time, so that the
 * answer is a size rather than a fraction -- `tail` how far its drawing hangs below the bottom edge
 * it is placed on, and `at` builds the row again at a factor, or returns undefined when a value at
 * that size would be wider than the box.
 */
export interface StackRow {
  height: number;
  draw(bottom: number): Item[];
  fill?: {
    ceiling: number;
    lead: number;
    tail: number;
    at(factor: number): StackRow | undefined;
  };
  /**
   * What the row declares, for a box too short to hold the stack: the ids it draws, the page's own
   * order to shed them in (most important first), and the row again without one of them.
   *
   * Without it a stack too tall loses whole trailing rows, and the page's declaration is overruled
   * by the geometry: a 249 x 158 zone took fuel's level gauge off with the average it was under,
   * although the table keeps the average and the drawing has a gauge at every shape.
   */
  shed?: {
    ids: readonly string[];
    order: readonly string[];
    without(ids: readonly string[]): StackRow | undefined;
  };
}

/** A row helper for the common case of items that do not depend on where they land. */
export const fixedRow = (height: number, draw: (bottom: number) => Item[]): StackRow => ({ height, draw });

/** The content box of a module: its rect less the density's padding. */
export function contentRect(frame: Rect, density: Density): Rect {
  const d = densityOf(density);
  return rect(frame.left + d.padX, frame.top + d.padY, Math.max(0, frame.width - 2 * d.padX), Math.max(0, frame.height - 2 * d.padY));
}

/** Total height of a stack of rows, gaps included. */
export const stackHeight = (rows: readonly StackRow[], gap: number): number =>
  rows.reduce((h, r) => h + r.height, 0) + gap * Math.max(0, rows.length - 1);

/**
 * The tail a row's last line box hangs below the bottom edge it was placed on.
 *
 * A row declares the height of its content; WPF draws a line box that runs a little below the
 * baseline row, and the box is what gets clipped. Two pixels covers it at every size the second
 * screens and the zones use, and reserving them is what keeps a stack exactly filling its frame
 * from putting its last row one pixel past the edge -- which is the whole of the overflow the zone
 * faces found on the narrow sizes.
 */
export const ROW_TAIL = 2;

/**
 * Where a stack sheds first: the least important id any of its rows declares.
 *
 * Least important is the page's own order, most important first, and an id the order does not name
 * is less important than every id it does -- the same reading `rank.ts` gives a `shedOrder`. The
 * last id of the last row that has one is never returned: a page that draws nothing is worse than a
 * page drawing one thing.
 */
function leastImportant(rows: readonly StackRow[], order: readonly string[]): { row: number; id: string } | undefined {
  const declared = rows.flatMap((row, i) => (row.shed?.ids ?? []).map((id) => ({ row: i, id })));
  if (declared.length <= 1) return undefined;
  const rank = (id: string): number => {
    const i = order.indexOf(id);
    return i === -1 ? order.length : i;
  };
  return declared.reduce((worst, next) => (rank(next.id) >= rank(worst.id) ? next : worst));
}

/**
 * The rows that fit `height`. Modules list their rows in importance order, so a short zone keeps
 * the reading that matters and drops the recap under it.
 *
 * A row that declares what it draws is shed one id at a time, in the page's own order, before any
 * row is taken off whole: that is rule 17 read the way the shedding table writes it, rather than
 * the height of a box deciding which of a page's declared fields survive. Only once nothing
 * declared can go does the stack fall back to dropping its trailing row, which is what a gauge, a
 * strip or a table -- none of which declares an id -- is dropped by.
 */
export function rowsThatFit(rows: readonly StackRow[], height: number, gap: number, shedOrder?: readonly string[]): StackRow[] {
  let kept = [...rows];
  const room = height - 2 * ROW_TAIL;
  const order = shedOrder ?? kept.find((row) => row.shed)?.shed?.order ?? kept.flatMap((row) => row.shed?.ids ?? []);
  while (stackHeight(kept, gap) > room) {
    const worst = leastImportant(kept, order);
    if (!worst) break;
    const row = kept[worst.row];
    const rebuilt = row?.shed?.without([worst.id]);
    kept = rebuilt === undefined || rebuilt.height <= 0 ? kept.filter((_, i) => i !== worst.row) : kept.map((r, i) => (i === worst.row ? rebuilt : r));
  }
  while (kept.length > 1 && stackHeight(kept, gap) > room) kept.pop();
  return kept;
}

/**
 * **Rule 20: a rank fills the box it is given.**
 *
 * Rule 17 is one half of a thought -- a page sheds its secondary rows before it shrinks its
 * numerals -- and this is the other. A page that has shed nothing and still has room should spend
 * it, because the room is on a screen a driver reads at arm's length with a corner coming.
 *
 * A stack grows until it meets an edge, and there are three: the height of the box, the width of
 * the box, and the next size up the density ramp (`growthCeiling`). The ramp is what makes this
 * filling rather than scaling -- a stack that has spent its room is the same drawing one size
 * larger, never a drawing stretched to a rectangle.
 *
 * One factor for the whole stack, so the sizes keep their order: the 46 px lap time above a 34 px
 * delta stays above it. Rows that cannot grow -- a gauge, a trace, a table -- keep their height and
 * are simply part of the budget the growing rows are measured against.
 *
 * A stack already too tall for its box is left alone. It has nothing to spend, and `rowsThatFit` is
 * about to take a row off it.
 */
function filled(rows: readonly StackRow[], height: number, step: number): readonly StackRow[] {
  if (stackHeight(rows, step) > height - 2 * ROW_TAIL) return rows;
  const fills = rows.map((row) => row.fill);
  // All of the stack grows or none of it does. A page whose sector strip is a drawing and whose lap
  // times are fields would otherwise grow the times alone until they matched the sectors above
  // them, and the hierarchy the sizes exist to express is the thing that would go.
  if (fills.some((f) => f === undefined)) return rows;
  const live = fills as NonNullable<StackRow['fill']>[];
  if (live.length === 0) return rows;
  const ceiling = Math.min(...live.map((f) => f.ceiling));
  const lead = Math.max(...live.map((f) => f.lead));
  if (lead <= 0) return rows;
  // Stepped in whole pixels of the largest value, biggest first, so what comes out is a font size a
  // person could have chosen rather than the end of a bisection.
  for (let size = Math.floor(lead * ceiling); size > lead; size--) {
    const grown = rows.map((row) => row.fill?.at(size / lead));
    if (grown.some((row) => row === undefined || row.height <= 0)) continue;
    const candidate = grown as StackRow[];
    // The room is the box less the tail at each end, because the stack is centred: a block that
    // took the whole height would sit with half the slack above it and hang the other half of its
    // last line box past the bottom edge, which WPF clips.
    const tail = Math.max(ROW_TAIL, ...candidate.map((row) => row.fill?.tail ?? ROW_TAIL));
    if (stackHeight(candidate, step) <= height - 2 * Math.ceil(tail)) return candidate;
  }
  return rows;
}

/**
 * How a stack spends the height its rows do not use.
 *
 * `centre` is one tight block in the middle of the frame. `spaceBetween` is the catalogue's other
 * answer: the wrapper takes `height: 100%` and the rows are pushed apart, the declared gap becoming
 * a minimum rather than the distance. Which of the two a page takes is a decision the catalogue
 * makes page by page and not by shape -- sectors, fuel, session, stint, speedo and car settings
 * spread at every shape they are drawn at, and the delta, the lists and the drawings centre -- so
 * the page says, and centring stays the default.
 */
export type StackJustify = 'centre' | 'spaceBetween';

export interface StackOptions {
  /** Gap between the rows, which a spread stack reads as a minimum. Defaults to the density's. */
  gap?: number;
  justify?: StackJustify;
  /** The page's declared keeps, most important first; the rows' own declaration by default. */
  shedOrder?: readonly string[];
}

/**
 * Draws rows as a column in `frame`, centred or spread, dropping what does not fit. The gap
 * defaults to the density's row gap, and a bare number is that gap.
 */
export function stack(frame: Rect, rows: readonly StackRow[], density: Density, opts: number | StackOptions = {}): Item[] {
  const { gap, justify = 'centre', shedOrder } = typeof opts === 'number' ? { gap: opts } : opts;
  const step = gap ?? densityOf(density).gapY;
  // A row of no height is a rank the page shed entirely. It is dropped rather than drawn, so the
  // rows under it move up instead of sitting below a gap with nothing above it.
  const live = rows.filter((row) => row.height > 0);
  const kept = rowsThatFit(filled(live, frame.height, step), frame.height, step, shedOrder);
  const total = stackHeight(kept, step);
  // The room a spread stack has is its box less its own tail at each end, the same reservation
  // growing makes: pushed to the bottom edge, the last row's line box would hang past it.
  const tail = Math.ceil(Math.max(ROW_TAIL, ...kept.map((row) => row.fill?.tail ?? ROW_TAIL)));
  const room = frame.height - 2 * tail;
  const spread = justify === 'spaceBetween' && kept.length > 1 && total <= room;
  const stretch = spread ? (room - total) / (kept.length - 1) : 0;
  let y = spread ? frame.top + tail : Math.round(frame.top + (frame.height - total) / 2);
  const items: Item[] = [];
  for (const row of kept) {
    items.push(...row.draw(Math.round(y) + row.height));
    y += row.height + step + stretch;
  }
  return items;
}

/** Draws rows from the top of `frame` instead of centring them; tables fill downwards. */
export function stackFromTop(frame: Rect, rows: readonly StackRow[], gap: number): Item[] {
  const kept = rowsThatFit(rows.filter((row) => row.height > 0), frame.height, gap);
  let y = frame.top;
  const items: Item[] = [];
  for (const row of kept) {
    items.push(...row.draw(y + row.height));
    y += row.height + gap;
  }
  return items;
}
