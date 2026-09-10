/** Helpers for attaching NCalc formulas to item properties without repeating the binding shape. */
import type { Bindings, BindingTarget, FormulaBinding } from './generator.ts';

/** An NCalc expression. The generator's helpers in ncalc.ts build these. */
export type Expr = string;

export const formula = (expression: Expr): FormulaBinding => ({ mode: 'formula', formula: expression });

/** A Bindings object from optional expressions; undefined when none is set, so items stay clean. */
export const bindings = (spec: Partial<Record<BindingTarget, Expr | undefined>>): Bindings | undefined => {
  const out: Bindings = {};
  let any = false;
  for (const [target, expr] of Object.entries(spec)) {
    if (expr === undefined) continue;
    out[target as BindingTarget] = formula(expr);
    any = true;
  }
  return any ? out : undefined;
};

/** Spreads a `bindings` key only when there is something to bind. */
export const withBindings = (spec: Partial<Record<BindingTarget, Expr | undefined>>): { bindings?: Bindings } => {
  const b = bindings(spec);
  return b ? { bindings: b } : {};
};
