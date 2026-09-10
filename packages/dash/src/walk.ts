/** Walks a dashboard model: every item including layer children, and every expression bound on them. */
import type { Dashboard, Item } from './generator.ts';
import { ncalc } from './generator.ts';

export function* walkItems(items: readonly Item[]): Generator<Item> {
  for (const item of items) {
    yield item;
    if (item.kind === 'layer') yield* walkItems(item.children);
  }
}

/** Every item of every screen, layers flattened. */
export const itemsOf = (dashboard: Dashboard): Item[] => dashboard.screens.flatMap((s) => [...walkItems(s.items)]);

/** Every formula expression bound on an item. */
export function expressionsOf(item: Item): string[] {
  const out: string[] = [];
  for (const binding of Object.values(item.bindings ?? {})) {
    if (!binding) continue;
    const f = binding.formula;
    if (typeof f === 'string') out.push(f);
    else {
      out.push(f.expression);
      if (f.preExpression) out.push(f.preExpression);
    }
  }
  return out;
}

export const expressionsIn = (dashboard: Dashboard): string[] => itemsOf(dashboard).flatMap(expressionsOf);

/** Every `[Plugin.Property]` referenced anywhere in the dashboard, deduplicated. */
export const propertiesIn = (dashboard: Dashboard): string[] => [...new Set(expressionsIn(dashboard).flatMap(ncalc.referencedProperties))];
