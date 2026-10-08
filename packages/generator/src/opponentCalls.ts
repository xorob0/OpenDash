/**
 * The opponent calls, and the one way each is spelled when it has to be looked up by its text.
 *
 * `driverposition(3)`, `getopponentleaderboardposition_aheadbehind(-1)` and the rest are functions
 * over SimHub's own leaderboard, which a recording of property values cannot see (#257). What a
 * recorder can do instead is ask SimHub for the value of each call it might be asked, frame by
 * frame, and keep the answers as columns named by the call. The browser evaluator (#395) then
 * answers `driverposition(repeatindex() + 1)` by working out the argument, spelling the call the
 * same way, and reading that column.
 *
 * So the spelling is a contract between three programs, the recorder in C#, `callsRead()` in the
 * dash package and the evaluator here, and this file is where it is written once:
 *
 *   - the function name in lower case, as SimHub registers it;
 *   - each argument as NCalc would read it back: an integer in invariant digits with a leading `-`
 *     when negative, and a boolean as `true` or `false` in lower case (C#'s `bool.ToString()` says
 *     `True`, so the recorder has to lower it);
 *   - the arguments joined by a comma and one space, inside one pair of brackets, nothing else.
 *
 * An argument that is not an integer or a boolean has no spelling, and a call whose argument has
 * none has no column: the evaluator reads that as null, which is what SimHub answers for a
 * position that names no car.
 */

/** How many cars the recorder enumerates. iRacing's fields are larger, but no page draws past 24. */
export const MAX_CARS = 24;

/** The three sectors SimHub splits a lap into. */
export const SECTORS = [1, 2, 3] as const;

/**
 * What an argument of an opponent call means, which is what decides the values it is enumerated
 * over when the expression computes it rather than writing it down.
 *
 * - `position`: a 1-based leaderboard index, 1..{@link MAX_CARS}.
 * - `offset`: a signed distance from the player on track, -(MAX_CARS - 1)..(MAX_CARS - 1), 0 being
 *   the player.
 * - `sector`: a sector number, {@link SECTORS}.
 * - `flag`: a boolean, false and true.
 * - `dummy`: an argument SimHub's delegate takes and ignores; only the written 0 is recorded.
 */
export type ArgumentKind = 'position' | 'offset' | 'sector' | 'flag' | 'dummy';

/** The registry functions that answer from the leaderboard, by lower-cased name. */
const NAMED: ReadonlyMap<string, readonly ArgumentKind[]> = new Map<string, readonly ArgumentKind[]>([
  ['getplayerleaderboardposition', []],
  ['getopponentleaderboardposition_aheadbehind', ['offset']],
  ['getopponentleaderboardposition_aheadbehind_playerclassonly', ['offset']],
  ['getopponentleaderboardposition_playerclassonly', ['position']],
  ['getbestsplittime', ['sector']],
  ['getbestsplittime_playerclassonly', ['sector']],
  ['getbestsplitleaderboardposition', ['sector']],
  ['getbestsplitleaderboardposition_playerclassonly', ['sector']],
  ['getbestlapopponentleaderboardposition', ['dummy']],
  ['getbestlapopponentleaderboardposition_playerclassonly', ['dummy']],
]);

/**
 * The argument kinds of an opponent call, or undefined when `name` is not one.
 *
 * `driver<name>(position)` and `driversector<name>(position, sector, includePrevious)` are matched by
 * prefix, as SimHub matches them against its providers; `drivergamespecificdata` shares the prefix
 * and is not one of them.
 */
export function opponentCall(name: string): readonly ArgumentKind[] | undefined {
  const n = name.toLowerCase();
  const named = NAMED.get(n);
  if (named) return named;
  if (n.startsWith('driversector') && n.length > 'driversector'.length) return ['position', 'sector', 'flag'];
  if (n.startsWith('driver') && n.length > 'driver'.length && n !== 'drivergamespecificdata') return ['position'];
  return undefined;
}

/** Whether `name` is answered from SimHub's leaderboard rather than from a property. */
export const isOpponentCall = (name: string): boolean => opponentCall(name) !== undefined;

/** An argument value as the evaluator holds it once computed. */
export type CallArgument = number | boolean;

/**
 * One argument in the canonical spelling, or undefined when it has none: a number that is not a
 * whole one, or anything that is not a number or a boolean.
 */
export function spellArgument(value: unknown): string | undefined {
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (typeof value === 'number' && Number.isInteger(value)) return Object.is(value, -0) ? '0' : String(value);
  return undefined;
}

/**
 * The canonical text of a call: what the recorder names its column and what the evaluator looks up.
 * Undefined when an argument has no spelling, which the evaluator reads as null.
 */
export function callText(name: string, args: readonly unknown[]): string | undefined {
  const spelled: string[] = [];
  for (const a of args) {
    const s = spellArgument(a);
    if (s === undefined) return undefined;
    spelled.push(s);
  }
  return `${name.toLowerCase()}(${spelled.join(', ')})`;
}

/** The values an argument of this kind is enumerated over when the expression computes it. */
export function domainOf(kind: ArgumentKind): readonly CallArgument[] {
  switch (kind) {
    case 'position':
      return Array.from({ length: MAX_CARS }, (_, i) => i + 1);
    case 'offset':
      return Array.from({ length: 2 * MAX_CARS - 1 }, (_, i) => i - (MAX_CARS - 1));
    case 'sector':
      return SECTORS;
    case 'flag':
      return [false, true];
    case 'dummy':
      return [0];
  }
}

/**
 * An argument's source text as a literal, when it is one: an integer, optionally negative and
 * optionally in brackets as `ncalc.ts` writes every operand, or `true` or `false`. Anything else is
 * computed, and undefined here.
 */
export function literalArgument(source: string): CallArgument | undefined {
  let s = source.trim();
  while (s.startsWith('(') && s.endsWith(')') && balanced(s.slice(1, -1))) s = s.slice(1, -1).trim();
  if (/^-?\s*\d+$/.test(s)) return Number(s.replace(/\s+/g, ''));
  const lower = s.toLowerCase();
  if (lower === 'true') return true;
  if (lower === 'false') return false;
  return undefined;
}

const balanced = (s: string): boolean => {
  let depth = 0;
  for (const c of s) {
    if (c === '(') depth += 1;
    else if (c === ')' && --depth < 0) return false;
  }
  return depth === 0;
};

/**
 * Every canonical call text a written call could be evaluated as: each literal argument as written,
 * each computed one over its kind's domain. `driversectorlastlap(getplayerleaderboardposition(), 2,
 * false)` is 24 texts, one per position; `getbestsplittime(1)` is one.
 */
export function callTextsFor(name: string, argumentSources: readonly string[]): string[] {
  const kinds = opponentCall(name);
  if (!kinds) return [];
  if (kinds.length !== argumentSources.length) {
    throw new Error(`${name}() takes ${kinds.length} argument${kinds.length === 1 ? '' : 's'} as an opponent call, given ${argumentSources.length}`);
  }
  let combos: CallArgument[][] = [[]];
  kinds.forEach((kind, i) => {
    const literal = literalArgument(argumentSources[i]!);
    const values = literal === undefined ? domainOf(kind) : [literal];
    combos = combos.flatMap((c) => values.map((v) => [...c, v]));
  });
  return combos.map((args) => callText(name, args)!);
}
