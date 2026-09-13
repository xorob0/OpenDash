/**
 * Every SimHub property the built packages read.
 *
 * This is the contract a telemetry trace has to satisfy: a trace that lacks one of these names
 * cannot render the dash that reads it, and nothing would say so except a blank field in a video.
 * `scripts/record.ts` records exactly this list and `scripts/trace.test.ts` holds the committed
 * traces to it, so the two cannot drift apart.
 *
 * Two sources, because a static scan of the expressions does not see everything. Most reads are
 * `[Some.Property]` and `propertiesIn` finds them. The lap history instead builds its property name
 * from the row's repeat index, which is a name no scanner can resolve, so those are written out
 * here from the same constant the module repeats over.
 */
import { composePackages } from './build.ts';
import { PREVIOUS_LAP_SLOTS } from './second/values.ts';
import { propertiesIn } from './walk.ts';

/**
 * The reads whose property name is computed at render time: `PersistantTrackerPlugin.PreviousLap_NN`
 * and its delta, one pair per row of the lap history. SimHub numbers the most recent lap 00.
 */
export const COMPUTED_PROPERTIES: readonly string[] = Array.from({ length: PREVIOUS_LAP_SLOTS }, (_, slot) => {
  const name = `PersistantTrackerPlugin.PreviousLap_${String(slot).padStart(2, '0')}`;
  return [name, `${name}_DeltaToSessionBest`];
}).flat();

/** Every property read by any binding of any package, deduplicated and sorted. */
export function propertiesRead(): string[] {
  const all = new Set<string>(COMPUTED_PROPERTIES);
  for (const { pkg } of composePackages({ log: () => {} })) {
    for (const dashboard of pkg.dashboards) for (const property of propertiesIn(dashboard)) all.add(property);
  }
  return [...all].sort();
}
