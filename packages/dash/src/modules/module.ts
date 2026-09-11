/**
 * A module: one page of the companion, and one page a pit wall zone can show. A module is a
 * function of a rect and a density, so the same code draws the 802 x 356 companion page and the
 * 607 x 212 zone panel; nothing about a module knows which screen it is on.
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

export interface ModuleContext {
  /** The box the module draws into, padding already removed. */
  frame: Rect;
  density: Density;
  /** Item name prefix, unique within the screen. */
  prefix: string;
}

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
