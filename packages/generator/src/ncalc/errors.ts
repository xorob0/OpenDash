/**
 * The three ways an expression can fail, kept apart because they mean different things.
 *
 * - {@link NCalcSyntaxError}: the text is not in the subset `ncalc.ts` writes. SimHub might parse
 *   it; this evaluator will not guess.
 * - {@link NCalcRuntimeError}: SimHub fails too. `null > 0` throws in NCalc, and a binding that
 *   throws draws the empty string, so a renderer catches this one at the binding and draws nothing,
 *   which is exactly what the dashboard does.
 * - {@link NCalcUnsupportedError}: SimHub would compute something here and this evaluator does not
 *   know what. A renderer must never swallow it, because drawing nothing in its place is the silent
 *   divergence ADR 0008 warns about.
 *
 * Each names the expression and the offset in it, so a failure in a build of thirty packages can be
 * found by reading the message.
 */

/** Where in its expression a node or a token is: `[start, end)` in UTF-16 units. */
export interface Span {
  readonly start: number;
  readonly end: number;
}

const excerpt = (expression: string, at: number): string => {
  const from = Math.max(0, at - 40);
  const to = Math.min(expression.length, at + 40);
  const head = from > 0 ? '...' : '';
  const tail = to < expression.length ? '...' : '';
  return `${head}${expression.slice(from, to)}${tail}\n${' '.repeat(head.length + at - from)}^`;
};

abstract class NCalcError extends Error {
  constructor(
    readonly reason: string,
    readonly expression: string,
    readonly offset: number,
  ) {
    super(`${reason}, at offset ${offset} of ${JSON.stringify(expression.length > 200 ? `${expression.slice(0, 200)}...` : expression)}\n${excerpt(expression, offset)}`);
  }
}

export class NCalcSyntaxError extends NCalcError {
  override name = 'NCalcSyntaxError';
}

export class NCalcRuntimeError extends NCalcError {
  override name = 'NCalcRuntimeError';
}

export class NCalcUnsupportedError extends NCalcError {
  override name = 'NCalcUnsupportedError';
}

/**
 * Thrown by the pure helpers (`dotnetFormat.ts`, `timespan.ts`) that do not know which expression
 * they serve; the evaluator rethrows it with the expression and the offset of the call.
 */
export class Unsupported extends Error {
  override name = 'Unsupported';
}

/** The same for a failure SimHub would have too, such as `String.Replace` with an empty needle. */
export class Fails extends Error {
  override name = 'Fails';
}
