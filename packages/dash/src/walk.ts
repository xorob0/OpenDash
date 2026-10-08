/**
 * Walks a dashboard model: every item including layer children, and every expression bound on them.
 *
 * "Every expression" is the whole of what SimHub evaluates for a dashboard, which is more than the
 * bindings written on its items. A border's colour is bound inside the item's `BorderStyle`; a
 * screen's `enabledExpression` decides whether the screen takes part in navigation; a dashboard's
 * variables are expressions of their own, read back as `[variable.<name>]`. All four read properties,
 * and a scan that reads only the item bindings misses the engine chip's border, the companion's
 * module switches and the pit wall's page (#581): the trace recorder asked SimHub for none of them,
 * and the replays drew those fields from nothing.
 */
import type { Binding, Dashboard, Item } from './generator.ts';
import { VARIABLE_PREFIX, bindingsOf, ncalc } from './generator.ts';

export function* walkItems(items: readonly Item[]): Generator<Item> {
  for (const item of items) {
    yield item;
    if (item.kind === 'layer') yield* walkItems(item.children);
  }
}

/** Every item of every screen, layers flattened. */
export const itemsOf = (dashboard: Dashboard): Item[] => dashboard.screens.flatMap((s) => [...walkItems(s.items)]);

/** The formula of a binding, and the pre-expression SimHub evaluates before it when there is one. */
const formulasOf = ({ formula }: Binding): string[] => {
  if (typeof formula === 'string') return [formula];
  return formula.preExpression ? [formula.expression, formula.preExpression] : [formula.expression];
};

/** Every formula expression bound on an item, its border's colour included. */
export const expressionsOf = (item: Item): string[] => bindingsOf(item).flatMap(({ binding }) => formulasOf(binding));

/**
 * The expressions a dashboard carries outside its items: its variables, in the order SimHub
 * evaluates them, and then each screen's enabled expression.
 */
export const dashboardExpressionsOf = (dashboard: Dashboard): string[] => [
  ...(dashboard.variables ?? []).map((v) => v.expression),
  ...dashboard.screens.flatMap((s) => (s.enabledExpression ? [s.enabledExpression] : [])),
];

/** Every expression SimHub evaluates for the dashboard: its own, then every binding of every item. */
export const expressionsIn = (dashboard: Dashboard): string[] => [...dashboardExpressionsOf(dashboard), ...itemsOf(dashboard).flatMap(expressionsOf)];

/**
 * Whether a `[...]` reference names one of the dashboard's own variables rather than a property.
 * SimHub resolves those inside the dashboard, so no plugin publishes them and no trace can carry one.
 */
export const isVariableRead = (reference: string): boolean => reference.startsWith(VARIABLE_PREFIX);

/** Every `[Plugin.Property]` referenced anywhere in the dashboard, deduplicated; the dashboard's own variables are not properties and are left out. */
export const propertiesIn = (dashboard: Dashboard): string[] => [
  ...new Set(expressionsIn(dashboard).flatMap(ncalc.referencedProperties).filter((p) => !isVariableRead(p))),
];
