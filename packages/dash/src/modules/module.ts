/**
 * A module: one page. The companion shows one at a time, a pit wall zone embeds one, and a zone of
 * the dash face cycles through them. A module is a function of a rect, a density and a shape, so
 * the same code draws the 802 x 356 companion page and the 607 x 158 zone strip, and nothing about
 * a module knows which screen it is on.
 *
 * Modules are listed in contract.ts, which the plugin mirrors, so this file only binds a builder
 * to its catalogue entry.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { moduleMeta, type ModuleMeta } from '../contract.ts';
import { placeholder } from '../second/placeholder.ts';
import { inSession } from '../second/values.ts';
import {
  drawFieldBlock,
  fieldBlockHeight,
  fieldWidth,
  fieldsTail,
  growthCeiling,
  leadSize,
  planLines,
  raggedness,
  scaleFields,
  takingTurns,
  type FieldSpec,
  type FieldValue,
  type LineOptions,
  type LinePlan,
} from '../second/field.ts';
import { densityOf, isFace } from '../second/density.ts';
import { fixedRow, type StackRow } from '../second/layout.ts';
import type { Density } from '../second/density.ts';
import { columnsAt, promotesLead, shapeOf, type Shape } from '../second/shape.ts';
import { archetypeFor, keepsAt, keepsPart, keptAt, keptIds, type Archetype } from './shedding.ts';
import { moduleRegister, withHouseLayout } from '../themes/moduleRegister.ts';

export interface ModuleContext {
  /** The box the module draws into, padding already removed. */
  frame: Rect;
  density: Density;
  /** Item name prefix, unique within the screen. */
  prefix: string;
  /**
   * Which page this is, which is how a rank finds its shedding order. `defineModule` fills it in,
   * so a module never passes its own id and a caller never has to know one.
   */
  page?: string;
  /**
   * The shape of the frame: a width band and a height band. Derived from the frame when a caller
   * does not pass one, which is every caller today, so adding it moves nothing.
   *
   * Density says how large the type is -- a companion page and a pit wall zone are different
   * instruments. Shape says how much fits, which is a different question and the one rule 17 is
   * about. A page at `wide` takes one rank; the same page at `tall narrow` stacks one column.
   */
  shape?: Shape;
  /**
   * When true, a page that lists other cars lists the player's own class rather than the whole
   * field. An expression rather than a flag, because the answer is a plugin setting a driver
   * changes mid-session, and because it is a property of the zone showing the page rather than of
   * the page. A face asks it per zone, because a face's four zones are four rectangles of one
   * dashboard. A pit wall asks it once for the whole screen, its zones being widgets pointed at one
   * zone dashboard per rectangle, so two of them are literally the same file and a per-zone answer
   * could not reach one without reaching the other. The companion passes nothing and lists
   * everybody.
   */
  classOnly?: Expr;
  /**
   * False drops a page's own title line and gives the drawing the whole frame. A zone of the dash
   * face already names the page it is showing, so repeating the name inside the frame costs a line
   * the drawing wants; the companion and the pit wall pass nothing and keep their titles.
   */
  title?: boolean;
  /**
   * False says the caller draws the session notice itself, so the module returns its drawing bare
   * (#406).
   *
   * For a caller that embeds a module in a box of its own alongside readings of its own: the pit
   * wall's Track panel puts the track map in half its body and a column of session readings in the
   * other half, and a notice over the map alone leaves the sentence "go into a session" sitting
   * beside a session best lap time. So the panel gates its whole body and says it once, and the
   * module it embeds stops saying it a second time in a corner. Every other caller passes nothing
   * and the module answers for itself, which is the point of the declaration being in the catalogue.
   */
  notice?: boolean;
}

/** The shape a context is drawn at, derived from its frame unless the caller named one. */
export const shapeIn = (ctx: ModuleContext): Shape => ctx.shape ?? shapeOf(ctx.frame);

/**
 * The catalogue drawing this context takes, which is what every declaration is read at.
 *
 * Its shape's answer for twenty of the twenty-two pages, and for the two that ask, the drawing the
 * canvas points at for a box that short. See `archetypeFor`.
 */
export const drawnAt = (ctx: ModuleContext): Archetype => archetypeFor(ctx.page, shapeIn(ctx), ctx.frame);

/**
 * The size a page's lead rank is drawn at: the density's `big`, or on a face whose zone is tall, the
 * next name up the ramp. **Rule 17's other lever** (#330).
 *
 * The catalogue's `tall` drawings promote their first rank and leave the rank under it where it
 * was: lap times 46 to 88 over the same 34, session and stint 34 to 76 over the same 34. Rule 20
 * cannot draw that, since it grows every size on a page by one factor and so keeps whatever ratio
 * the page started from; a tall box affording the lead a size more is what `promotesLead` has always
 * said and what rule 17 names as the height's second job. One name up is 64 over 34 in a zone, 1.88,
 * which is the catalogue's lap times to within a few per cent, and rule 20 then grows the page into
 * the box the promotion leaves.
 *
 * A face only. The companion and the pit wall are drawn on artboards of their own screens, which do
 * not promote, and the portrait companion's lap times already lead with a hero of their own drawing.
 */
export const leadRankSize = (ctx: ModuleContext): number => {
  const d = densityOf(ctx.density);
  return isFace(ctx.density) && promotesLead(shapeIn(ctx)) ? d.hero : d.big;
};

export type ModuleBuilder = (ctx: ModuleContext) => Item[];

export interface Module extends ModuleMeta {
  build: ModuleBuilder;
}

/** The reason half of a session notice: an instruction, since the reader can act on it. */
export const SESSION_REASON = 'Go into a session';

/**
 * `Leaderboard · Go into a session`: what a page says in place of its empty table.
 *
 * Anything with a name, because band D's pages are named the same way and are not modules. The name
 * half is the one `placeholder` drops in a box too short for both, which is why the reason half is
 * an instruction that stands on its own.
 *
 * A page whose name is already a word of the instruction gets the instruction alone. Module 11 is
 * called Session, and "Session · Go into a session" is the stutter voice.md's governing principle
 * refuses: the header above the box has said the name, so the notice has nothing to add by saying it
 * again. Every other page keeps both halves, since "lap times" is not in the sentence.
 *
 * Each half is written as a sentence of its own, capital first, because each is drawn alone: the
 * placeholder sheds the name where the box is short and keeps the instruction.
 */
export const sessionNotice = (page: { name: string }): string => {
  // Compared without case, which is a question about the words and not a change to what is drawn.
  const said = SESSION_REASON.toLowerCase().split(' ').includes(page.name.toLowerCase());
  return said ? SESSION_REASON : `${page.name} · ${SESSION_REASON}`;
};

/**
 * The name of the group a module's content is gathered in while it waits for a session.
 *
 * Exported for the test that checks every gated module has exactly one, and so that nothing else
 * has to know how the name is spelled.
 */
export const sessionGroupName = (prefix: string): string => `${prefix}inSession`;

/**
 * Some drawing and its notice, in the same rectangle, one of them on the screen at a time.
 *
 * The one arrangement, so that the three places that need it -- a module, one of band D's own
 * pages, and a pit wall panel that embeds a module -- compose it the same way rather than each
 * spelling the gate out. `notice` is the sentence, since a band keeps its page's name where a module
 * in a zone drops it, and a panel names itself.
 *
 * The content goes into one group whose `Visible` is the session test, rather than having the test
 * folded into every item, for two reasons. A group whose Visible is false leaves its children's
 * bindings unevaluated, so an empty leaderboard's forty rows cost nothing while the notice is
 * showing; and an item that already binds Visible for a reason of its own -- a row on a car that
 * does not exist, a field the sim does not publish -- keeps that binding untouched, where folding
 * would have had to read and rewrite it.
 *
 * The first of those is the load-bearing one and it is verified rather than assumed:
 * `EditorModel.ApplyItemBindings` in SimHub 9.12.6 evaluates an item's Visible, and returns on a
 * `DrawableItem` that came out invisible *before* it walks a `Layer`'s children. `Layer` derives
 * from `ContainerItemBase` and so from `DrawableItem`, so the return applies to it. That is why the
 * readings inside need no extra guard on their text, which is the trap energy's header records: an
 * invisible *leaf* does keep evaluating its other bindings, and formatting a null once a frame puts
 * an error in SimHub's log once a frame.
 *
 * The notice is `placeholder`'s, in the rectangle the caller gives and gated the other way, so the
 * two are never on the screen together and the overlap exists only in the editor, exactly as
 * energy's does.
 */
export function withSessionGate(prefix: string, notice: string, frame: Rect, density: Density, items: Item[]): Item[] {
  const test = inSession();
  return [
    withMoreBindings({ kind: 'layer', name: sessionGroupName(prefix), children: items }, { Visible: test }),
    ...placeholder(prefix, notice, frame, density).map((item) => withMoreBindings(item, { Visible: ncalc.not(test) })),
  ];
}

export function defineModule(id: string, build: ModuleBuilder): Module {
  const meta = moduleMeta(id);
  // A module is always its own page, even when another page builds it inside itself: the pit wall's
  // track panel embeds the track module, and the table that applies to it is the track one.
  const page: ModuleBuilder = (ctx) => (moduleRegister()?.houseLayout?.has(id) ? withHouseLayout(() => build({ ...ctx, page: id })) : build({ ...ctx, page: id }));
  // A module that needs a session says so while there is none (#406). The catalogue's declaration
  // is what decides it, so a module added later answers the question by existing -- unless the
  // caller embedding it says it is drawing the notice itself, which is `notice: false`.
  const gated: ModuleBuilder = (ctx) =>
    ctx.notice === false ? page(ctx) : withSessionGate(ctx.prefix, sessionNotice(meta), ctx.frame, ctx.density, page(ctx));
  return { ...meta, build: meta.needsSession ? gated : page };
}

/** A field of this module, its item name prefixed so it is unique on the screen. */
export const fld = (ctx: ModuleContext, id: string, label: string, value: FieldValue, extra: Partial<FieldSpec> = {}): FieldSpec => ({
  name: `${ctx.prefix}${id}`,
  id,
  label,
  value,
  ...extra,
});

/**
 * How a rank of fields is laid out, for a page that wants something other than the greedy wrap.
 *
 * `lines` is the plan: `perLine` puts one field on each line, `grid` lays `columns` equal cells --
 * `columnsAt` of the shape when the page does not name a count, which is where the shape model
 * reaches the layout rather than only the shedding. `align: 'top'` shares the top edge of the line
 * instead of the baseline of its largest field, and `justify` overrides the shape's own answer,
 * which centres a rank in a zone narrow enough for one column and draws from the left otherwise.
 */
export interface FieldsRowOptions {
  gap?: number;
  lineGap?: number;
  lines?: LinePlan;
  columns?: number;
  align?: 'baseline' | 'top';
  justify?: 'left' | 'centre';
  /**
   * Pairs of fields, by id, that are never shown together: each is shown exactly when the other is
   * not. A line too narrow for the two side by side draws them in one place rather than wrapping
   * onto a line that is always empty; see `takingTurns`.
   */
  turns?: readonly (readonly [string, string])[];
}

/**
 * The fields with each pair that takes turns drawn as one place, where both of the pair are kept.
 * A pair this shape keeps only one of is that one field on its own.
 */
function inTurns(specs: readonly FieldSpec[], turns: readonly (readonly [string, string])[]): FieldSpec[] {
  const idOf = (spec: FieldSpec): string => spec.id ?? spec.name;
  let out = [...specs];
  for (const [a, b] of turns) {
    const first = out.find((spec) => idOf(spec) === a);
    const second = out.find((spec) => idOf(spec) === b);
    if (first === undefined || second === undefined) continue;
    out = out.flatMap((spec) => (spec === first ? [takingTurns(first, second)] : spec === second ? [] : [spec]));
  }
  return out;
}

/**
 * A stack row of bottom-aligned fields. The row shrinks its gaps to fit and then wraps: the same
 * three lap times are one line on a companion page and two on a portrait one, with no variant of
 * the module written for either.
 *
 * What it does **before** any of that is shed by the page's declared order: the fields this page
 * keeps at this shape, from `shedding.ts`, and no others. A row left with nothing draws nothing and
 * takes no height, so the rank below it moves up rather than sitting under a gap. It carries that
 * declaration with it, so that a stack too tall for its box sheds by the table rather than by
 * dropping whichever row happens to be last.
 *
 * A bare number is the gap between the fields of a line, which is what most pages pass.
 */
export function fieldsRow(specs: readonly FieldSpec[], ctx: ModuleContext, opts: number | FieldsRowOptions = {}): StackRow {
  const { gap, lines: plan, columns, align, justify, lineGap: asked, turns = [] }: FieldsRowOptions = typeof opts === 'number' ? { gap: opts } : opts;
  const shape = shapeIn(ctx);
  const lineGap = asked ?? Math.round(densityOf(ctx.density).gapY / 2);
  const kept = keptAt(specs, ctx.page, drawnAt(ctx));
  if (kept.length === 0) return fixedRow(0, () => []);
  const register = moduleRegister();
  if (register) return register.fieldsRow(kept, ctx, keepsAt(ctx.page, drawnAt(ctx)) ?? kept.map((spec) => spec.id ?? spec.name));
  const shapeColumns = columnsAt(shape);
  const columnCount = columns ?? shapeColumns;
  // A zone narrow enough for one column centres what is in it; a wider one draws from its left
  // edge. `rank` already carries the centring, including the bindings that re-centre the line when
  // the sim does not publish one of its fields.
  const line: LineOptions & { lineGap: number } = {
    gap,
    lineGap,
    align,
    justify: justify ?? (shapeColumns === 1 ? 'centre' : 'left'),
    ...(plan === 'grid' ? { columns: columnCount } : {}),
  };
  // Two fields that take turns are drawn side by side wherever the line has room for both, and the
  // rank closes over whichever is hidden. Only a line that would wrap them apart draws them in one
  // place, so the pair costs nothing where it already fitted.
  const linesOf = (at: readonly FieldSpec[]): FieldSpec[][] => {
    const apart = planLines(at, ctx.frame.width, ctx.density, { plan, columns: columnCount, gap });
    if (turns.length === 0) return apart;
    const together = planLines(inTurns(at, turns), ctx.frame.width, ctx.density, { plan, columns: columnCount, gap });
    return together.length < apart.length ? together : apart;
  };
  const rowOf = (at: readonly FieldSpec[], evenness: number): StackRow => {
    const lines = linesOf(at);
    return {
      height: fieldBlockHeight(lines, ctx.density, lineGap),
      draw: (bottom) => drawFieldBlock(lines, ctx.frame.left, bottom, ctx.frame.width, ctx.density, line),
      fill: {
        ceiling: growthCeiling(at, ctx.density),
        lead: leadSize(at),
        tail: fieldsTail(at, ctx.density),
        at: (factor) => {
          const grown = scaleFields(at, factor);
          // A field wider than the whole box is where growing stops. `wrapFields` would give it a
          // line of its own and `fieldRowFitted` would draw it from the left edge and off the right
          // one, which is the clip this whole file exists to avoid.
          if (grown.some((spec) => fieldWidth(spec, ctx.density) > ctx.frame.width)) return undefined;
          // And a rank may not grow itself into a worse shape than it started in. Growing may turn
          // two lines of two and one into three of one, which is the narrow zone stacking itself
          // and is the point; it may not turn one line of three into two and one, which is the
          // companion's lap times and looks like a bug.
          if (raggedness(linesOf(grown)) > evenness) return undefined;
          return rowOf(grown, evenness);
        },
      },
      shed: {
        ids: at.map((spec) => spec.id ?? spec.name),
        order: keepsAt(ctx.page, drawnAt(ctx)) ?? at.map((spec) => spec.id ?? spec.name),
        without: (ids) => {
          const left = at.filter((spec) => !ids.includes(spec.id ?? spec.name));
          return left.length === 0 ? undefined : rowOf(left, raggedness(linesOf(left)));
        },
      },
    };
  };

  return rowOf(kept, raggedness(linesOf(kept)));
}

/**
 * The columns this page keeps at this shape, in the order the table draws them.
 *
 * The same declaration a rank of fields reads, for the pages that are lists. A narrow zone loses
 * columns before it loses rows: relative at `tall narrow` is position, code and gap, and eight
 * rows rather than six. What fits is still checked afterwards, because a declared column set is a
 * design decision and a box is a fact.
 */
export const pageColumns = <T extends string>(all: readonly T[], ctx: ModuleContext): T[] => keptIds(all, ctx.page, drawnAt(ctx));

/** Whether this page keeps a part that is neither a field nor a column, at the shape it is drawn at. */
export const pageKeeps = (id: string, ctx: ModuleContext): boolean => keepsPart(id, ctx.page, drawnAt(ctx));

/** A stack row of a fixed height drawn by the caller, e.g. a gauge or a strip. */
export const blockRow = fixedRow;
