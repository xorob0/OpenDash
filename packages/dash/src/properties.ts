/**
 * Every SimHub property the built packages read.
 *
 * This is the contract a telemetry trace has to satisfy: a trace that lacks one of these names
 * cannot render the dash that reads it, and nothing would say so except a blank field in a video.
 * `scripts/record.ts` records exactly this list and `scripts/trace.test.ts` holds the committed
 * traces to it, so the two cannot drift apart.
 *
 * Two sources, because a static scan of the expressions does not see everything. Most reads are
 * `[Some.Property]` and `propertiesIn` finds them, in every expression a dashboard carries: the item
 * bindings, the border colours, the screens' enabled expressions and the variables (#581). The lap
 * history instead builds its property name from the row's repeat index, which is a name no scanner
 * can resolve, so those are written out here from the same constant the module repeats over.
 *
 * Every theme, not only the default one. A theme changes the register and never the contract
 * (ADR 0016), but a theme's face is free to read a property the house face does not, as the
 * Porsche's does for its fuel figure and its crest, and a trace that cannot render the Porsche is as
 * blind as one that cannot render the engine chip. The shipped set is what `bun run package` builds,
 * which is every theme with code, so that is what the traces are held to.
 */
import { composePackages, themesToBuild } from './build.ts';
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

/** Every theme a release ships: the default and every other one that has code, as `--all-themes` picks them. */
const shippedThemes = () => themesToBuild({ themes: [], allThemes: true, touchedThemes: false }, () => {});

/** The scan itself: compose every package of every shipped theme, and collect what its expressions read. */
function scanPackages(): string[] {
  const all = new Set<string>(COMPUTED_PROPERTIES);
  for (const { pkg } of composePackages({ log: () => {}, themes: shippedThemes() })) {
    for (const dashboard of pkg.dashboards) for (const property of propertiesIn(dashboard)) all.add(property);
  }
  return [...all].sort();
}

/**
 * Held from the first answer, because composing every package to produce it costs well over a
 * second, a theme with colours of its own composing in a Bun process of its own on top, and the
 * answer cannot change inside a process: the layouts, the zone faces, the themes and the screen
 * packages are module constants, and the scan is a pure function of them.
 *
 * What made this worth caching is that the callers ask more than once. `scripts/record.test.ts`
 * asks three times in one file, which on a loaded CI runner once took a single test past Bun's
 * five-second timeout and failed a build that had nothing wrong with it.
 */
let scanned: string[] | undefined;

/**
 * Every property read by any expression of any package of any shipped theme, deduplicated and sorted.
 *
 * A copy each time, so that a caller sorting or splicing the list in place cannot hand the next
 * caller a different answer.
 */
export function propertiesRead(): string[] {
  scanned ??= scanPackages();
  return [...scanned];
}
