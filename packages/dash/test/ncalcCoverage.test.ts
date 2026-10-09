/**
 * Every expression a full build writes, held to the browser evaluator (#395, #71).
 *
 * The demo runs the built dashboards in a page, so it computes every binding with
 * `packages/generator/src/ncalc/`. An evaluator more forgiving than SimHub would hide the
 * `left([Class], 4)` class of bug ADR 0008 is about, and one less complete than the build would draw
 * a field empty that the dash fills. So this composes every package the build writes, the default
 * theme and every other theme with code, walks every expression a binding, a border, a dashboard
 * variable or a screen's enabled expression carries, and asks three things of each:
 *
 *   - it parses, in the subset `ncalc.ts` writes;
 *   - every call in it is one SimHub dispatches and the evaluator computes, at that arity;
 *   - printed back and parsed again, it is the same tree.
 *
 * Then it evaluates every one of them against a frame of a committed trace, as a renderer would,
 * repeated layers and all, and asks that nothing the evaluator does not know how to compute is
 * reached. A runtime failure is allowed there, since SimHub fails the same way (a null compared, the
 * opponent calls a version 1 trace does not carry); an unsupported construct is not.
 *
 * One more question is asked of the same walk, since it is the only place every expression is in
 * hand: whether any `max` or `min` has an Int32 literal on its left against a value that may have a
 * fraction. NCalc answers both in the left operand's type, so `max(0, x)` rounds `x` to a whole
 * number, which is what made every clock tick early and the refuel figure end in `.0` (#831).
 *
 * A failure names the package, the dashboard, the screen, the item path and the binding, and quotes
 * the expression.
 */
import { describe, expect, test } from 'bun:test';
import { readTrace } from '../../../scripts/trace.ts';
import { composePackages, themesToBuild } from '../src/build.ts';
import { ncalcEvaluator as E, type Dashboard, type Formula, type Item } from '../src/generator.ts';

interface Site {
  /** Where the expression lives, for a message a reader can find it by. */
  where: string;
  expression: string;
  /** The `repeatindex()` stack of the repeated layers around it, one entry per layer. */
  repeated: number;
}

const formulaText = (f: string | Formula): string => (typeof f === 'string' ? f : f.expression);

function sitesOf(folder: string, dashboard: Dashboard): Site[] {
  const out: Site[] = [];
  const at = (rest: string): string => `${folder} / ${dashboard.name}: ${rest}`;
  for (const v of dashboard.variables ?? []) out.push({ where: at(`variable ${v.name}`), expression: v.expression, repeated: 0 });
  for (const screen of dashboard.screens) {
    if (screen.enabledExpression) out.push({ where: at(`screen "${screen.name}" enabled expression`), expression: screen.enabledExpression, repeated: 0 });
    const visit = (items: readonly Item[], path: string, repeated: number): void => {
      for (const item of items) {
        const here = `${path} > ${item.name}`;
        for (const [target, binding] of Object.entries(item.bindings ?? {})) {
          if (!binding) continue;
          out.push({ where: at(`screen "${screen.name}"${here} .${target}`), expression: formulaText(binding.formula), repeated });
          if (typeof binding.formula !== 'string' && binding.formula.preExpression) {
            out.push({ where: at(`screen "${screen.name}"${here} .${target} (pre-expression)`), expression: binding.formula.preExpression, repeated });
          }
        }
        const border = 'border' in item ? item.border?.colorBinding : undefined;
        if (border) out.push({ where: at(`screen "${screen.name}"${here} .BorderStyle.BorderColor`), expression: formulaText(border.formula), repeated });
        if (item.kind === 'layer') {
          const repeats = (item.repetitions ?? 0) > 0 || item.bindings?.Repetitions !== undefined;
          visit(item.children, here, repeated + (repeats ? 1 : 0));
        }
      }
    };
    visit(screen.items, '', 0);
  }
  return out;
}

const themes = themesToBuild({ themes: [], allThemes: true, touchedThemes: false }, () => {});
const packages = composePackages({ version: '0.0.0-test', themes, log: () => {} });

interface DashboardSites {
  folder: string;
  dashboard: Dashboard;
  sites: Site[];
}

const dashboards: DashboardSites[] = packages.flatMap(({ pkg }) => pkg.dashboards.map((dashboard) => ({ folder: pkg.folderName, dashboard, sites: sitesOf(pkg.folderName, dashboard) })));
const sites = dashboards.flatMap((d) => d.sites);
const unique = [...new Set(sites.map((s) => s.expression))];

const describeFailure = (site: Site, reason: string): string => `${site.where}\n  ${reason}\n  in ${site.expression}`;

describe('every expression in a full build, held to the browser evaluator', () => {
  test('the walk finds the build: every theme, and tens of thousands of expressions', () => {
    expect(themes.map((t) => t.id)).toContain('porsche');
    expect(sites.length).toBeGreaterThan(10_000);
    expect(sites.some((s) => s.where.includes('BorderStyle'))).toBe(true);
    expect(sites.some((s) => s.where.includes('variable '))).toBe(true);
    expect(sites.some((s) => s.where.includes('enabled expression'))).toBe(true);
  });

  test('every expression parses in the subset ncalc.ts writes', () => {
    const failures: string[] = [];
    const seen = new Set<string>();
    for (const site of sites) {
      if (seen.has(site.expression)) continue;
      seen.add(site.expression);
      try {
        E.parse(site.expression);
      } catch (e) {
        failures.push(describeFailure(site, e instanceof Error ? e.message : String(e)));
      }
    }
    expect(failures).toEqual([]);
  });

  test('every call is one SimHub dispatches and the evaluator computes, at the arity it is written with', () => {
    const failures: string[] = [];
    const seen = new Set<string>();
    for (const site of sites) {
      if (seen.has(site.expression)) continue;
      seen.add(site.expression);
      for (const problem of E.callProblems(E.parseCached(site.expression))) {
        failures.push(describeFailure(site, `${problem.kind === 'dispatch' ? 'SimHub would not dispatch' : 'the evaluator does not compute'} ${problem.name}() with ${problem.count} argument(s) at offset ${problem.offset}: ${problem.reason}`));
      }
    }
    expect(failures).toEqual([]);
  });

  test('no max or min has an Int32 literal on its left unless what it bounds is already whole', () => {
    // Whole by construction: a truncate, or another max or min, which is checked here in its own turn.
    // Those are the two the build writes on purpose, the tacho's scale in thousands and the split
    // list's first row, both counts. Anything else wants `real` on the left, as #831 explains.
    const whole = (node: E.Node): boolean => node.type === 'call' && ['truncate', 'max', 'min'].includes(node.name.toLowerCase());
    const failures: string[] = [];
    const seen = new Set<string>();
    for (const site of sites) {
      if (seen.has(site.expression)) continue;
      seen.add(site.expression);
      for (const node of E.nodesOf(E.parseCached(site.expression).root)) {
        if (node.type !== 'call' || !['max', 'min'].includes(node.name.toLowerCase())) continue;
        const [bound, value] = node.args;
        if (bound?.type !== 'literal' || typeof bound.value !== 'object' || bound.value?.kind !== 'int' || value === undefined || whole(value)) continue;
        failures.push(describeFailure(site, `${E.print(node)} rounds its right operand to an Int32`));
      }
    }
    expect(failures).toEqual([]);
  });

  test('printed back and parsed again, every expression is the same tree', () => {
    const strip = (node: E.Node): unknown => {
      switch (node.type) {
        case 'literal':
          return { literal: node.value };
        case 'property':
          return { property: node.name };
        case 'call':
          return { call: node.name.toLowerCase(), args: node.args.map(strip) };
        case 'unary':
          return { unary: node.op, operand: strip(node.operand) };
        case 'binary':
          return { binary: node.op, left: strip(node.left), right: strip(node.right) };
      }
    };
    const failures: string[] = [];
    for (const expression of unique) {
      const tree = E.parseCached(expression).root;
      const again = E.parse(E.print(tree)).root;
      if (JSON.stringify(strip(again)) !== JSON.stringify(strip(tree))) failures.push(expression);
    }
    expect(failures).toEqual([]);
  });

  test('evaluated against a recorded frame, nothing reaches a construct the evaluator does not compute', () => {
    const trace = readTrace('race');
    const index = Math.floor(trace.header.frames / 2);
    const properties: Record<string, E.Value> = {};
    for (const column of trace.columns) {
      const raw = Array.isArray(column.v) ? column.v[index] : column.v;
      properties[column.p] =
        raw === null || raw === undefined ? null : column.t === 'timespan' ? E.parseTimeSpan(String(raw)) : column.t === 'datetime' ? E.parseDateTime(String(raw)) : E.toValue(raw);
    }
    const failures: string[] = [];
    let evaluated = 0;
    for (const { dashboard, sites: own } of dashboards) {
      const variables: Record<string, E.Value> = {};
      const state = new E.CallState();
      const scope = (repeat: readonly number[]): E.Scope => ({
        properties: (name: string) => (Object.hasOwn(properties, name) ? properties[name] : /^variable\./i.test(name) ? variables[name.slice('variable.'.length).toLowerCase()] : undefined),
        repeat,
        state,
        now: 1000,
        rootScreenName: dashboard.screens[0]?.name ?? '',
      });
      for (const site of own) {
        // The first copy of every repeated layer, and the second, which is the first that differs.
        for (const copy of site.repeated === 0 ? [0] : [1, 2]) {
          const repeat = Array.from({ length: site.repeated }, (_, i) => (i === site.repeated - 1 ? copy : 1));
          evaluated += 1;
          try {
            const result = E.evaluateBinding(site.expression, scope(repeat));
            const variable = /^.* \/ .*: variable (.*)$/.exec(site.where)?.[1];
            if (variable !== undefined) variables[variable.toLowerCase()] = result.ok ? result.value : null;
          } catch (e) {
            failures.push(describeFailure(site, e instanceof Error ? e.message : String(e)));
          }
        }
      }
    }
    expect(evaluated).toBeGreaterThan(sites.length);
    expect(failures).toEqual([]);
  });
});
