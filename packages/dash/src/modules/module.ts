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
import { moduleMeta, type ModuleMeta } from '../contract.ts';
import { drawFieldBlock, fieldBlockHeight, wrapFields, type FieldSpec, type FieldValue } from '../second/field.ts';
import { densityOf } from '../second/density.ts';
import { fixedRow, type StackRow } from '../second/layout.ts';
import type { Density } from '../second/density.ts';
import { shapeOf, type Shape } from '../second/shape.ts';
import { keepsPart, keptAt, keptIds } from './shedding.ts';

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
}

/** The shape a context is drawn at, derived from its frame unless the caller named one. */
export const shapeIn = (ctx: ModuleContext): Shape => ctx.shape ?? shapeOf(ctx.frame);

export type ModuleBuilder = (ctx: ModuleContext) => Item[];

export interface Module extends ModuleMeta {
  build: ModuleBuilder;
}

export function defineModule(id: string, build: ModuleBuilder): Module {
  return { ...moduleMeta(id), build: (ctx) => build({ ...ctx, page: ctx.page ?? id }) };
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
 * A stack row of bottom-aligned fields. The row shrinks its gaps to fit and then wraps: the same
 * three lap times are one line on a companion page and two on a portrait one, with no variant of
 * the module written for either.
 *
 * What it does **before** any of that is shed by the page's declared order: the fields this page
 * keeps at this shape, from `shedding.ts`, and no others. A row left with nothing draws nothing and
 * takes no height, so the rank below it moves up rather than sitting under a gap.
 */
export function fieldsRow(specs: readonly FieldSpec[], ctx: ModuleContext, gap?: number): StackRow {
  const lineGap = Math.round(densityOf(ctx.density).gapY / 2);
  const kept = keptAt(specs, ctx.page, shapeIn(ctx));
  if (kept.length === 0) return fixedRow(0, () => []);
  const lines = wrapFields(kept, ctx.frame.width, ctx.density, gap);
  return fixedRow(fieldBlockHeight(lines, ctx.density, lineGap), (bottom) =>
    drawFieldBlock(lines, ctx.frame.left, bottom, ctx.frame.width, ctx.density, { gap, lineGap }),
  );
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
