/**
 * What a strip and the flag box show below the flags, in the order they take the panel.
 *
 * The flags themselves come from the generated `FLAGS`, which is `FLAG_CATALOGUE` in
 * `packages/dash/src/flags.ts` in its ranked order. The two tiers under them are stated in
 * `docs/flag-box.md` and are editorial here: the pit states and the warnings are not a catalogue
 * the build exports.
 */
export const PIT_STATES: readonly string[] = ['Speeding in the pit lane', 'Limiter on, outside the lane', 'Limiter on, in the lane'];

export const WARNINGS: readonly string[] = ['Oil temperature', 'Water temperature', 'Low fuel'];

/** The spotter is not in the ranking: it draws over whatever else the panel shows. */
export const SPOTTER = 'A car alongside is a bar down that edge, drawn over everything else.';
