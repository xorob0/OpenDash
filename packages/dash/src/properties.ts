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
import { callTextsFor, functionCalls, isOpponentCall } from './generator.ts';
import { PREVIOUS_LAP_SLOTS } from './second/values.ts';
import { expressionsIn, propertiesIn } from './walk.ts';

/**
 * The reads whose property name is computed at render time: `PersistantTrackerPlugin.PreviousLap_NN`,
 * its delta and whether it was driven in this session, one set per row of the lap history. SimHub
 * numbers the most recent lap 00.
 */
export const COMPUTED_PROPERTIES: readonly string[] = Array.from({ length: PREVIOUS_LAP_SLOTS }, (_, slot) => {
  const name = `PersistantTrackerPlugin.PreviousLap_${String(slot).padStart(2, '0')}`;
  return [name, `${name}_DeltaToSessionBest`, `${name}_IsCurrentSession`];
}).flat();

/** Every theme a release ships: the default and every other one that has code, as `--all-themes` picks them. */
const shippedThemes = () => themesToBuild({ themes: [], allThemes: true, touchedThemes: false }, () => {});

/** What one composition of every package of every shipped theme yields: the properties read and the opponent calls made. */
interface Scan {
  properties: string[];
  calls: string[];
}

/** The scan itself: compose every package of every shipped theme, and collect what its expressions read and call. */
function scanPackages(): Scan {
  const properties = new Set<string>(COMPUTED_PROPERTIES);
  const calls = new Set<string>();
  for (const { pkg } of composePackages({ log: () => {}, themes: shippedThemes() })) {
    for (const dashboard of pkg.dashboards) {
      for (const property of propertiesIn(dashboard)) properties.add(property);
      for (const expression of new Set(expressionsIn(dashboard))) {
        for (const call of functionCalls(expression)) {
          if (isOpponentCall(call.name)) for (const text of callTextsFor(call.name, call.args)) calls.add(text);
        }
      }
    }
  }
  return { properties: [...properties].sort(), calls: [...calls].sort() };
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
let scanned: Scan | undefined;

/**
 * Every property read by any expression of any package of any shipped theme, deduplicated and sorted.
 *
 * A copy each time, so that a caller sorting or splicing the list in place cannot hand the next
 * caller a different answer.
 */
export function propertiesRead(): string[] {
  scanned ??= scanPackages();
  return [...scanned.properties];
}

/**
 * Every opponent call any expression of any package of any shipped theme can make, as the canonical text a recorder names
 * its column by and the browser evaluator looks it up by (`opponentCalls.ts` in the generator).
 *
 * The other half of what a trace has to carry (#257). A call's argument is usually computed, as in
 * `drivername(repeatindex() + 1)`, so a scan cannot know which car it will ask for; each computed
 * argument is enumerated over its kind's domain instead, every position from 1 to 24 for a
 * `driver*` call, every offset from -23 to 23 for an ahead-and-behind lookup, and a literal argument
 * is kept as written. Deduplicated and sorted, and a copy each time.
 */
export function callsRead(): string[] {
  scanned ??= scanPackages();
  return [...scanned.calls];
}
