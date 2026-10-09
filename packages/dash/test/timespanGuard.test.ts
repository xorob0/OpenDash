/**
 * Every time a package converts to seconds is guarded outside the conversion (#586, after #454).
 *
 * SimHub's `timespantoseconds` answers null for anything that is not a TimeSpan, the number 0
 * included, so `timespantoseconds(isnull(t, 0))` is null whenever `t` is; and NCalc throws on a null
 * operand of a comparison or of arithmetic, which a binding draws as the empty string. The `driver*`
 * reads are null on a frame SimHub is still building and while the player is unplaced, and a
 * `GameData` time is null with no session, so the stint, the sectors and the session clock blinked
 * empty on exactly the frames they had a placeholder for. `docs/research/simhub-dash-format.md` has
 * the measurement; `secondsOf` in `second/values.ts` is the one way to write the guard.
 *
 * So this composes every package the build writes, every theme included, walks every expression a
 * binding, a border, a variable or a screen's enabled expression carries, and holds each to two
 * rules: the guard is never inside the conversion, and no conversion reaches an operator unguarded.
 * Then it evaluates every expression that converts a time against a recorded frame with every time
 * taken out of it and no opponent call answered, which is the frame the ticket describes, and asks
 * that nothing throws and that the fields it named draw their placeholders.
 */
import { describe, expect, test } from 'bun:test';
import { readTrace } from '../../../scripts/trace.ts';
import { composePackages, themesToBuild } from '../src/build.ts';
import { ncalcEvaluator as E, type Dashboard } from '../src/generator.ts';
import { NO_CLOCK, NO_TIME, NO_VALUE } from '../src/second/values.ts';
import { expressionsIn, itemsOf } from '../src/walk.ts';

const themes = themesToBuild({ themes: [], allThemes: true, touchedThemes: false }, () => {});
const packages = composePackages({ version: '0.0.0-test', themes, log: () => {} });
const dashboards: { where: string; dashboard: Dashboard }[] = packages.flatMap(({ pkg }) =>
  pkg.dashboards.map((dashboard) => ({ where: `${pkg.folderName} / ${dashboard.name}`, dashboard })),
);

/** Every expression of the build once, with the first dashboard it was found in. */
const expressions = new Map<string, string>();
for (const { where, dashboard } of dashboards) for (const e of expressionsIn(dashboard)) if (!expressions.has(e)) expressions.set(e, where);
const converting = [...expressions.keys()].filter((e) => e.includes('timespantoseconds('));

const isConversion = (node: E.Node): boolean => node.type === 'call' && node.name.toLowerCase() === 'timespantoseconds';

/**
 * Whether a node can be the null of an unguarded conversion: the conversion itself, or an `if` with
 * one in a branch it can answer. `isnull` is the guard and everything else is taken to answer a value.
 */
const mayBeNull = (node: E.Node): boolean => {
  if (isConversion(node)) return true;
  if (node.type === 'call' && node.name.toLowerCase() === 'if') return node.args.slice(1).some(mayBeNull);
  return false;
};

/**
 * The operators NCalc throws at on a null operand. The ticket names the four orderings; equality and
 * the arithmetic throw the same way (`operators.ts` in the evaluator, from NCalc 1.3.8), so a
 * difference of two unguarded sector times is the same blank field as a comparison of one.
 */
const THROWS_ON_NULL: ReadonlySet<string> = new Set(['<', '<=', '>', '>=', '=', '!=', '+', '-', '*', '/', '%']);

/** Every place in an expression where a conversion is guarded inside or reaches an operator bare. */
function unguarded(expression: string): string[] {
  const found: string[] = [];
  const visit = (node: E.Node): void => {
    if (isConversion(node) && node.type === 'call') {
      const arg = node.args[0];
      if (arg && arg.type === 'call' && arg.name.toLowerCase() === 'isnull') found.push(`the guard is inside the conversion: ${E.print(node)}`);
    }
    if (node.type === 'binary' && THROWS_ON_NULL.has(node.op)) {
      for (const side of [node.left, node.right]) if (mayBeNull(side)) found.push(`an unguarded conversion is an operand of ${node.op}: ${E.print(side)}`);
    }
    if (node.type === 'unary' && node.op === '-' && mayBeNull(node.operand)) found.push(`an unguarded conversion is negated: ${E.print(node.operand)}`);
    if (node.type === 'call') node.args.forEach(visit);
    if (node.type === 'binary') [node.left, node.right].forEach(visit);
    if (node.type === 'unary') visit(node.operand);
  };
  visit(E.parseCached(expression).root);
  return found;
}

describe('the rule, on the shapes it is written against', () => {
  test('it finds the guard inside the conversion, and a bare conversion compared or subtracted', () => {
    expect(unguarded('(timespantoseconds(isnull([T], 0))) > (0)').length).toBeGreaterThan(0);
    expect(unguarded("format(timespantoseconds(isnull([T], 0)), '0.0')")).toHaveLength(1);
    for (const op of ['>', '<', '>=', '<=', '=', '-']) expect(unguarded(`(timespantoseconds([T])) ${op} (0)`)).toHaveLength(1);
    expect(unguarded('(if([C], timespantoseconds([T]), 0)) > (0)')).toHaveLength(1);
  });

  test('it passes the guard written outside the conversion', () => {
    expect(unguarded('(isnull(timespantoseconds([T]), 0)) > (0)')).toEqual([]);
    expect(unguarded('(isnull(timespantoseconds([A]), 0)) - (isnull(timespantoseconds([B]), 0))')).toEqual([]);
    expect(unguarded('(if([C], isnull(timespantoseconds([T]), 0), 0)) > (0)')).toEqual([]);
  });
});

describe('every binding of every built package', () => {
  test('the walk finds the build, and the build converts times', () => {
    expect(themes.map((t) => t.id)).toContain('porsche');
    expect(expressions.size).toBeGreaterThan(2_000);
    expect(converting.length).toBeGreaterThan(100);
  });

  test('never writes the guard inside the conversion', () => {
    expect(converting.filter((e) => e.includes('timespantoseconds(isnull(')).map((e) => `${expressions.get(e)}\n  in ${e}`)).toEqual([]);
  });

  test('never hands an unguarded conversion to an operator', () => {
    const failures = converting.flatMap((e) => unguarded(e).map((problem) => `${expressions.get(e)}\n  ${problem}\n  in ${e}`));
    expect(failures).toEqual([]);
  });
});

describe('on a frame with no time in it', () => {
  // The race trace's middle frame with every TimeSpan column taken out, and no opponent call
  // answered: the frame SimHub is still building, or the one before the player is placed.
  const trace = readTrace('race');
  const index = Math.floor(trace.header.frames / 2);
  const properties: Record<string, E.Value> = {};
  for (const column of trace.columns) {
    const raw = Array.isArray(column.v) ? column.v[index] : column.v;
    properties[column.p] = raw === null || raw === undefined || column.t === 'timespan' ? null : column.t === 'datetime' ? E.parseDateTime(String(raw)) : E.toValue(raw);
  }
  const scope = (): E.Scope => ({ properties: (name: string) => (Object.hasOwn(properties, name) ? properties[name] : undefined), repeat: [1, 1, 1], state: new E.CallState(), now: 0, rootScreenName: '' });

  test('the trace carries the times this takes out, so the frame is not empty by accident', () => {
    const times = trace.columns.filter((c) => c.t === 'timespan').map((c) => c.p);
    expect(times).toContain('DataCorePlugin.GameData.SessionTimeLeft');
    expect(times).toContain('DataCorePlugin.GameData.LastLapTime');
  });

  test('no expression that converts a time throws', () => {
    const failures: string[] = [];
    for (const e of converting) {
      const result = E.evaluateBinding(e, scope());
      if (!result.ok) failures.push(`${expressions.get(e)}\n  ${result.error.message}`);
    }
    expect(failures).toEqual([]);
  });

  /** What every item of the build whose name ends so draws as its text, each distinct answer once. */
  const drawn = (suffix: string): unknown[] => {
    const out = new Set<unknown>();
    for (const { dashboard } of dashboards) {
      for (const item of itemsOf(dashboard)) {
        if (!item.name.endsWith(suffix)) continue;
        const text = item.bindings?.Text;
        if (!text) continue;
        const formula = typeof text.formula === 'string' ? text.formula : text.formula.expression;
        const result = E.evaluateBinding(formula, scope());
        out.add(result.ok ? result.value : `throws: ${result.error.message}`);
      }
    }
    return [...out];
  };

  test('the stint time and the last stop draw their placeholders, on the module and on band D', () => {
    expect(drawn('stint.stintTime.value')).toEqual([NO_CLOCK]);
    expect(drawn('stint.time.value')).toEqual([NO_CLOCK]);
    expect(drawn('stint.lastStop.value')).toEqual(['0.0']);
  });

  test("band D's sectors page draws the lap placeholder and the sector one", () => {
    // Every theme with its own placeholder draws that one, so the set is asked to hold the default's.
    for (const suffix of ['sectors.last.value', 'sectors.best.value']) expect(drawn(suffix)).toContain(NO_TIME);
    for (const suffix of ['sectors.s1.value', 'sectors.s2.value', 'sectors.s3.value']) expect(drawn(suffix)).toEqual([NO_VALUE]);
  });

  test('the session clock draws the unset clock', () => {
    expect(drawn('session.timeLeft.value')).toEqual([NO_CLOCK]);
    expect(drawn('race.session.left.value')).toEqual([NO_CLOCK]);
  });
});
