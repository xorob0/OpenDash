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

export interface ModuleContext {
  /** The box the module draws into, padding already removed. */
  frame: Rect;
  density: Density;
  /** Item name prefix, unique within the screen. */
  prefix: string;
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
  return { ...moduleMeta(id), build };
}

/** A field of this module, its item name prefixed so it is unique on the screen. */
export const fld = (ctx: ModuleContext, id: string, label: string, value: FieldValue, extra: Partial<FieldSpec> = {}): FieldSpec => ({
  name: `${ctx.prefix}${id}`,
  label,
  value,
  ...extra,
});

/**
 * A stack row of bottom-aligned fields. The row shrinks its gaps to fit and then wraps: the same
 * three lap times are one line on a companion page and two on a portrait one, with no variant of
 * the module written for either.
 */
export function fieldsRow(specs: readonly FieldSpec[], ctx: ModuleContext, gap?: number): StackRow {
  const lineGap = Math.round(densityOf(ctx.density).gapY / 2);
  const lines = wrapFields(specs, ctx.frame.width, ctx.density, gap);
  return fixedRow(fieldBlockHeight(lines, ctx.density, lineGap), (bottom) =>
    drawFieldBlock(lines, ctx.frame.left, bottom, ctx.frame.width, ctx.density, { gap, lineGap }),
  );
}

/** A stack row of a fixed height drawn by the caller, e.g. a gauge or a strip. */
export const blockRow = fixedRow;
