/**
 * Evaluates a parsed expression against a {@link Scope}, the way SimHub's NCalc would.
 *
 * Three outcomes, as `errors.ts` describes: a {@link Value}; an {@link NCalcRuntimeError} where
 * SimHub fails as well, which a renderer draws as the empty string; or an
 * {@link NCalcUnsupportedError} where the evaluator does not know what SimHub would do, which a
 * renderer must let through. {@link evaluateBinding} is the boundary that tells the first two apart
 * from the third.
 */
import { Fails, NCalcRuntimeError, NCalcUnsupportedError, Unsupported } from './errors.ts';
import { dispatchProblem, implementationOf, supportProblem, type CallContext } from './functions.ts';
import { arithmetic, comparison, negate, plus, toBoolean, type ArithmeticOperator, type ComparisonOperator } from './operators.ts';
import { parseCached, type Node, type Parsed } from './parse.ts';
import { readProperty, type Scope } from './scope.ts';
import type { Value } from './values.ts';

class Evaluation {
  constructor(
    private readonly parsed: Parsed,
    private readonly scope: Scope,
  ) {}

  /** Rethrows a helper's failure with the expression and the offset of the node it came from. */
  private guard<T>(node: Node, f: () => T): T {
    try {
      return f();
    } catch (e) {
      if (e instanceof Fails) throw new NCalcRuntimeError(e.message, this.parsed.source, node.span.start);
      if (e instanceof Unsupported) throw new NCalcUnsupportedError(e.message, this.parsed.source, node.span.start);
      if (e instanceof RangeError) throw new NCalcRuntimeError(e.message, this.parsed.source, node.span.start);
      throw e;
    }
  }

  run(node: Node): Value {
    switch (node.type) {
      case 'literal':
        return node.value;
      case 'property':
        return readProperty(this.scope, node.name);
      case 'unary': {
        const v = this.run(node.operand);
        return this.guard(node, () => (node.op === '!' ? !toBoolean(v) : negate(v)));
      }
      case 'binary': {
        if (node.op === 'and' || node.op === 'or') {
          // Short-circuit, left to right, as NCalc's `&&` and `||` do (#762).
          const left = this.guard(node.left, () => toBoolean(this.run(node.left)));
          if (node.op === 'and' ? !left : left) return left;
          return this.guard(node.right, () => toBoolean(this.run(node.right)));
        }
        const left = this.run(node.left);
        const right = this.run(node.right);
        return this.guard(node, () => {
          if (node.op === '+') return plus(left, right);
          if (node.op === '-' || node.op === '*' || node.op === '/' || node.op === '%') return arithmetic(node.op as ArithmeticOperator, left, right);
          return comparison(node.op as ComparisonOperator, left, right);
        });
      }
      case 'call': {
        const count = node.args.length;
        const notDispatched = dispatchProblem(node.name, count);
        if (notDispatched) throw new NCalcUnsupportedError(`${notDispatched}, so SimHub would draw nothing here`, this.parsed.source, node.nameSpan.start);
        const unsupported = supportProblem(node.name, count);
        if (unsupported) throw new NCalcUnsupportedError(unsupported, this.parsed.source, node.nameSpan.start);
        const impl = implementationOf(node.name);
        const values: (Value | undefined)[] = new Array(count);
        const done: boolean[] = new Array(count).fill(false);
        const context: CallContext = {
          name: node.name.toLowerCase(),
          count,
          scope: this.scope,
          arg: (i) => {
            if (!done[i]) {
              values[i] = this.run(node.args[i]!);
              done[i] = true;
            }
            return values[i] as Value;
          },
          source: (i) => {
            const a = node.args[i]!;
            return this.parsed.source.slice(a.span.start, a.span.end);
          },
        };
        return this.guard(node, () => impl(context));
      }
    }
  }
}

/** Evaluates an expression, given as text or already parsed. Throws as described above. */
export function evaluate(expression: string | Parsed, scope: Scope): Value {
  const parsed = typeof expression === 'string' ? parseCached(expression) : expression;
  return new Evaluation(parsed, scope).run(parsed.root);
}

/** A binding's outcome: its value, or the runtime failure SimHub would have drawn as nothing. */
export type BindingResult = { readonly ok: true; readonly value: Value } | { readonly ok: false; readonly error: NCalcRuntimeError };

/**
 * Evaluates a binding the way SimHub applies one: a runtime failure is caught and reported as an
 * empty result, as the dash draws the empty string for a throwing binding. A syntax error and an
 * unsupported construct are not caught: those are faults in the build or in this evaluator, and
 * drawing nothing for them would hide exactly what this evaluator exists to show.
 */
export function evaluateBinding(expression: string | Parsed, scope: Scope): BindingResult {
  try {
    return { ok: true, value: evaluate(expression, scope) };
  } catch (e) {
    if (e instanceof NCalcRuntimeError) return { ok: false, error: e };
    throw e;
  }
}
