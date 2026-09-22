/** Helpers for attaching NCalc formulas to item properties without repeating the binding shape. */
import type { Bindings, BindingTarget, FormulaBinding, Item } from './generator.ts';

/** An NCalc expression. The generator's helpers in ncalc.ts build these. */
export type Expr = string;

/** The expressions a caller wants bound, by target. An undefined entry binds nothing. */
export type BindingSpec = Partial<Record<BindingTarget, Expr | undefined>>;

export const formula = (expression: Expr): FormulaBinding => ({ mode: 'formula', formula: expression });

/** A Bindings object from optional expressions; undefined when none is set, so items stay clean. */
export const bindings = (spec: BindingSpec): Bindings | undefined => {
  const out: Bindings = {};
  let any = false;
  for (const [target, expr] of Object.entries(spec)) {
    if (expr === undefined) continue;
    out[target as BindingTarget] = formula(expr);
    any = true;
  }
  return any ? out : undefined;
};

/**
 * `item` with `spec` folded into whatever bindings it already carries.
 *
 * This is the only way to bind an item, because the shape it replaces was a trap. The old
 * `withBindings` returned a whole `bindings` object, so `{ ...label(...), ...withBindings({ Visible: x }) }`
 * read as "and also bind Visible" while in fact replacing every binding the label had built for
 * itself. It was harmless for exactly as long as the element it was spread over happened to have
 * none of its own, and #226 records the day that stopped being true: a `Left` binding a rank had
 * just computed was thrown away, nothing failed, and the strip was simply wrong on the VM.
 *
 * A target bound on both sides throws rather than picking a winner. Two places binding one
 * property is the ambiguity this helper exists to end, and last-write-wins would keep the silence
 * and merely move it somewhere harder to see; a throw fires while the package is being built, names
 * the item and the target, and so reaches a developer rather than a driver.
 */
export const withMoreBindings = <T extends Item>(item: T, spec: BindingSpec): T => {
  const added = bindings(spec);
  if (added === undefined) return { ...item };
  const existing = item.bindings;
  if (existing === undefined) return { ...item, bindings: added };
  for (const target of Object.keys(added) as BindingTarget[]) {
    if (existing[target] !== undefined) {
      throw new Error(`${item.name}: ${target} is bound in two places; one of them has to stop binding it.`);
    }
  }
  return { ...item, bindings: { ...existing, ...added } };
};
