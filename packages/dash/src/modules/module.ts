/**
 * A module: one page. The companion shows one at a time, a pit wall zone embeds one, and a zone of
 * the dash face cycles through them. A module is a function of a rect, a density and a shape, so
 * the same code draws the 802 x 336 companion page and the 607 x 158 zone strip, and nothing about
 * a module knows which screen it is on.
 *
 * Modules are listed in contract.ts, which the plugin mirrors, so this file only binds a builder
 * to its catalogue entry.
 */
import type { Item, Rect } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { moduleMeta, type ModuleMeta } from '../contract.ts';
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
  type FieldSpec,
  type FieldValue,
  type LineOptions,
  type LinePlan,
} from '../second/field.ts';
import { densityOf } from '../second/density.ts';
import { fixedRow, type StackRow } from '../second/layout.ts';
import type { Density } from '../second/density.ts';
import { columnsAt, shapeOf, type Shape } from '../second/shape.ts';
import { keepsAt, keepsPart, keptAt, keptIds } from './shedding.ts';

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
   * the page: the companion and the pit wall pass nothing and list everybody, as they always have.
   */
  classOnly?: Expr;
}

/** The shape a context is drawn at, derived from its frame unless the caller named one. */
export const shapeIn = (ctx: ModuleContext): Shape => ctx.shape ?? shapeOf(ctx.frame);

export type ModuleBuilder = (ctx: ModuleContext) => Item[];

export interface Module extends ModuleMeta {
  build: ModuleBuilder;
}

export function defineModule(id: string, build: ModuleBuilder): Module {
  // A module is always its own page, even when another page builds it inside itself: the car
  // telemetry page embeds the car settings module, and the table that applies to it is the car
  // settings one.
  return { ...moduleMeta(id), build: (ctx) => build({ ...ctx, page: id }) };
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
  const { gap, lines: plan, columns, align, justify, lineGap: asked }: FieldsRowOptions = typeof opts === 'number' ? { gap: opts } : opts;
  const shape = shapeIn(ctx);
  const lineGap = asked ?? Math.round(densityOf(ctx.density).gapY / 2);
  const kept = keptAt(specs, ctx.page, shape);
  if (kept.length === 0) return fixedRow(0, () => []);
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
  const linesOf = (at: readonly FieldSpec[]): FieldSpec[][] => planLines(at, ctx.frame.width, ctx.density, { plan, columns: columnCount, gap });
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
        order: keepsAt(ctx.page, shape) ?? at.map((spec) => spec.id ?? spec.name),
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
export const pageColumns = <T extends string>(all: readonly T[], ctx: ModuleContext): T[] => keptIds(all, ctx.page, shapeIn(ctx));

/** Whether this page keeps a part that is neither a field nor a column, at the shape it is drawn at. */
export const pageKeeps = (id: string, ctx: ModuleContext): boolean => keepsPart(id, ctx.page, shapeIn(ctx));

/** A stack row of a fixed height drawn by the caller, e.g. a gauge or a strip. */
export const blockRow = fixedRow;
