/**
 * The NCalc subset `ncalc.ts` writes, parsed and evaluated in TypeScript, with no node import, so
 * that a browser page can run a built dashboard's bindings (#395, #71).
 *
 * `parse` turns an expression into a tree whose every node knows where it came from; `evaluate`
 * computes it against a {@link Scope} with NCalc's own types and semantics; `evaluateBinding` is the
 * boundary at which SimHub's own failures become an empty field and the evaluator's gaps do not.
 */
export { NCalcRuntimeError, NCalcSyntaxError, NCalcUnsupportedError, type Span } from './errors.ts';
export { nodesOf, parse, parseCached, print, type BinaryOperator, type Node, type Parsed, type UnaryOperator } from './parse.ts';
export { evaluate, evaluateBinding, type BindingResult } from './evaluate.ts';
export { CallState, readProperty, type Scope, type Source } from './scope.ts';
export { dispatchProblem, supportProblem, SUPPORTED_FUNCTIONS } from './functions.ts';
export { dateToString, formatDate, formatNumber, numberToString, valueToString } from './dotnetFormat.ts';
export { fromSeconds, parseDateTime, parseTimeSpan, timeSpanToString, toShortTime, totalSeconds } from './timespan.ts';
export {
  date,
  decimal,
  double,
  int,
  isDate,
  isNumber,
  isTimeSpan,
  single,
  timespan,
  toJs,
  toValue,
  TICKS_PER_SECOND,
  type DateValue,
  type NumberKind,
  type NumberValue,
  type TimeSpanValue,
  type Value,
} from './values.ts';

import { nodesOf, type Parsed } from './parse.ts';
import { dispatchProblem, supportProblem } from './functions.ts';

/** A call in a parsed expression that SimHub would not dispatch or this evaluator would not compute. */
export interface CallProblem {
  readonly name: string;
  readonly count: number;
  readonly offset: number;
  readonly reason: string;
  readonly kind: 'dispatch' | 'unsupported';
}

/** Every call in an expression that would fail before it computed anything, without evaluating it. */
export function callProblems(parsed: Parsed): CallProblem[] {
  const out: CallProblem[] = [];
  for (const node of nodesOf(parsed.root)) {
    if (node.type !== 'call') continue;
    const count = node.args.length;
    const dispatch = dispatchProblem(node.name, count);
    if (dispatch) {
      out.push({ name: node.name, count, offset: node.nameSpan.start, reason: dispatch, kind: 'dispatch' });
      continue;
    }
    const unsupported = supportProblem(node.name, count);
    if (unsupported) out.push({ name: node.name, count, offset: node.nameSpan.start, reason: unsupported, kind: 'unsupported' });
  }
  return out;
}
