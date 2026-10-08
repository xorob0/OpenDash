/**
 * The one door from the demo into the NCalc evaluator (#395, #71) and the trace format.
 *
 * Both live outside `site/` and import nothing from node, so a client component can bundle them.
 * They are reached through this file rather than by a relative path in every module, so that the
 * day they move, one line moves with them.
 */
export * as E from '../../../packages/generator/src/ncalc/index.ts';
export { frame, parseTrace, TraceError, type Trace, type TraceValue } from '../../../scripts/traceFormat.ts';
