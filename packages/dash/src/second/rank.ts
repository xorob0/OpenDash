/**
 * A rank: one row of fields, and what it does when one of them is not there.
 *
 * Rule 17 settled that a page sheds its secondary ranks before it shrinks its numerals, and the
 * shedding order itself is data: `shedding.ts` carries the table the catalogue draws. What is left
 * is the mechanism, and it has two halves that pull in opposite directions.
 *
 * **A field the sim does not publish is removed, and the rank closes over the hole.** Band D says
 * it plainly: nothing is spread to fill, the rank is packed and centred in what the corners leave.
 * A strip drawing an empty box for a setting iRacing has no property for is worse than a narrower
 * strip.
 *
 * **A telltale that is unlit keeps its place and is drawn dim.** A lamp coming on should be a
 * change of colour and not of layout: one that vanished and returned would move every lamp beside
 * it at the moment the driver most needs to read them.
 *
 * Both rules are deliberate and they contradict each other, which is why the choice is a mode of
 * one component rather than twenty-one judgements. A rank whose members can never go missing
 * carries no bindings at all, so the two modes cost nothing where neither applies.
 */
import type { Hex, Item } from '../generator.ts';
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { ds } from '../tokens.ts';

const { add, div, iff, num, str, sub } = ncalc;

/** What a rank does with a member that is not there. */
export type WhenMissing = 'close' | 'dim';

/** Where a member of a rank was placed, and the bindings that say what happens when it is missing. */
export interface Placed {
  /** Where the member is drawn with every member present; what DashStudio shows. */
  x: number;
  width: number;
  /**
   * `Left` for an item `dx` into the member, once the rank has closed over what is missing.
   * Undefined when nothing in the rank can go missing, which is the common case.
   */
  leftAt(dx?: number): Expr | undefined;
  /** `Visible`: false when the member is not there and the rank closes over it. */
  visibleBind?: Expr;
  /** True while the member is there; what a member drawn dim picks its ink with. */
  litBind?: Expr;
}

/** One member of a rank: how wide it is, whether it can go missing, and how it draws itself. */
export interface RankMember {
  id: string;
  width: number;
  /**
   * True while the sim publishes the field, or while the telltale is lit. Undefined means the
   * member is always there.
   */
  present?: Expr;
  draw(at: Placed): Item[];
}

export interface RankOptions {
  left: number;
  width: number;
  gap: number;
  when: WhenMissing;
  /**
   * Ids most important first, for a rank whose shedding order is not its drawing order. The bar's
   * strip is the worked example: it draws slip, TC, cut, bias, ABS, map, diff and keeps bias, TC
   * and ABS, because that is what a driver would choose.
   */
  shedOrder?: readonly string[];
  /** Packed and centred by default, which is what band D asks for. */
  align?: 'centre' | 'left';
  /**
   * How few members the rank may shed to, one by default.
   *
   * A page keeps one whatever the width, because a page that draws nothing is worse than a page
   * drawing one thing badly. The bar's strip passes zero: it is one of three blocks on a line, and
   * a cell that will not fit is a cell drawn over the field beside it -- which is what the first
   * photograph of the 850 face showed, BIAS sitting on POSITION.
   */
  atLeast?: number;
}

/** Width a set of members takes, drawn side by side. */
export const rankWidth = (members: readonly RankMember[], gap: number): number =>
  members.reduce((sum, m) => sum + m.width, 0) + gap * Math.max(0, members.length - 1);

/**
 * The members that fit `width`, least important dropped first, never fewer than `atLeast`.
 *
 * One by default, because a rank that sheds its last member draws nothing at all; a single member
 * too wide for its box is the case a caller has to answer some other way. A caller with room to
 * draw nothing says so with `atLeast: 0`.
 */
export function rankThatFits(members: readonly RankMember[], width: number, gap: number, shedOrder?: readonly string[], atLeast = 1): RankMember[] {
  // Most important first. An id the shedding order does not name is less important than every id
  // it does, and ties among those break on the drawing order, so the tail still goes first.
  const importance = (m: RankMember): number => {
    const i = shedOrder?.indexOf(m.id) ?? -1;
    return i === -1 ? (shedOrder?.length ?? 0) + members.indexOf(m) : i;
  };
  const kept = [...members];
  while (kept.length > Math.max(0, atLeast) && rankWidth(kept, gap) > width) {
    const worst = kept.reduce((a, b) => (importance(b) > importance(a) ? b : a));
    kept.splice(kept.indexOf(worst), 1);
  }
  return kept;
}

/**
 * Where each member sits, and the `Left` expression that closes the rank over the ones that are
 * not there.
 *
 * The expression is built with the fixed members folded into constants, so a rank with one
 * optional member carries one `if` per position rather than one per member. A rank with none
 * carries no expression at all and the items come out exactly as they did before there was a mode.
 */
function placements(members: readonly RankMember[], opts: RankOptions): { x: number; leftBind?: Expr }[] {
  const { gap, left, width, when } = opts;
  const centred = (opts.align ?? 'centre') === 'centre';
  const total = rankWidth(members, gap);
  const startX = left + (centred ? Math.max(0, (width - total) / 2) : 0);

  let cursor = startX;
  const xs = members.map((m) => {
    const x = cursor;
    cursor += m.width + gap;
    return x;
  });

  // Only the closing mode moves anything: a dimmed member keeps its place by definition.
  if (when !== 'close' || !members.some((m) => m.present)) return xs.map((x) => ({ x }));

  /** `w + gap` when the member is there, 0 when it is not; a constant when it is never missing. */
  const slot = (m: RankMember): { fixed: number; term?: Expr } =>
    m.present ? { fixed: 0, term: iff(m.present, num(m.width + gap), num(0)) } : { fixed: m.width + gap, term: undefined };

  const slots = members.map(slot);
  // The rank's own width, less the trailing gap the last member does not need.
  const totalExpr = add(num(slots.reduce((sum, s) => sum + s.fixed, 0) - gap), ...slots.flatMap((s) => (s.term ? [s.term] : [])));

  return members.map((m, i) => {
    const before = slots.slice(0, i);
    const offset = add(num(before.reduce((sum, s) => sum + s.fixed, 0)), ...before.flatMap((s) => (s.term ? [s.term] : [])));
    // A rank laid out from the left only moves a member that has an optional one before it; a
    // centred one moves every member, because the centre itself moves. A member nothing can move
    // is left with no binding rather than one that always evaluates to where it already is.
    if (!centred && !before.some((s) => s.term)) return { x: xs[i] ?? left };
    const base = centred ? add(num(left), div(sub(num(width), totalExpr), num(2)), offset) : add(num(left), offset);
    return { x: xs[i] ?? left, leftBind: base };
  });
}

/**
 * A rank drawn from `left`, packed and centred in `width`.
 *
 * Returns the members that survived as well as the items, because what a rank kept is the thing a
 * test wants to assert and a caller sometimes wants to report.
 */
export function rank(members: readonly RankMember[], opts: RankOptions): { items: Item[]; kept: RankMember[] } {
  const kept = rankThatFits(members, opts.width, opts.gap, opts.shedOrder, opts.atLeast);
  const placed = placements(kept, opts);
  const items = kept.flatMap((m, i) => {
    const at = placed[i] ?? { x: opts.left };
    return m.draw({
      x: at.x,
      width: m.width,
      leftAt: (dx = 0) => (at.leftBind === undefined ? undefined : dx === 0 ? at.leftBind : add(at.leftBind, num(dx))),
      ...(opts.when === 'close' && m.present ? { visibleBind: m.present } : {}),
      ...(opts.when === 'dim' && m.present ? { litBind: m.present } : {}),
    });
  });
  return { items, kept };
}

/**
 * The ink a member drawn dim uses: its own colour while it is there, the dim ink while it is not.
 *
 * `undefined` for a member that can never be missing, so the item keeps its plain colour rather
 * than a binding that is always true.
 */
export const dimUnless = (lit: Expr | undefined, colour: Hex): Expr | undefined =>
  lit === undefined ? undefined : iff(lit, str(colour), str(ds.color.text.dim));
