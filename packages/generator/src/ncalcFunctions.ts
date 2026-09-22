/**
 * Every function a SimHub NCalc expression may name, and how many arguments each one takes.
 *
 * This exists because `left([Class], 4)` shipped. The expression was well formed, the item was
 * present, the geometry was right and every test passed, and the chip drew an empty block on both
 * leaderboards for months. SimHub's `left` takes three arguments — value, start index, length —
 * so the dispatch below never matched, no delegate was attached, and the evaluation produced
 * nothing. SimHub reports none of that to a dashboard; it simply draws the empty string.
 *
 * So a name on its own is not enough to check. `left` is a real SimHub function, and calling it
 * with two arguments is still a silent blank. Arity is the half of the check that catches this.
 *
 * Read from SimHub 9.12.6 by decompiling `SimHub.Plugins.dll`:
 *
 *   - `NCalcEngineBase.EvaluateFunction` dispatches on `name` and `parameterCount`; every
 *     `name == "x" && parameterCount == n` branch is one row below.
 *   - `NCalcEngineMethodsRegistry.AddMethod(name, ...)` registers the generic methods, which the
 *     same method falls through to. Their arity is the delegate's, and they are listed with
 *     `variadic: true` where the registry accepts a nullable trailing parameter.
 *   - `driver<name>(position)` and `driversector<name>(position, sector, includePrevious)` are
 *     matched by prefix against `OpponentsDataProviders` and `OpponentSectorDataProviders`, so
 *     they are checked by prefix and arity rather than by an enumerated list.
 *   - Anything the dispatch does not claim falls through to NCalc's own function table, which is
 *     the last group below.
 *
 * When SimHub is upgraded, this file is re-derived rather than edited by hand.
 */

/** How many arguments a function accepts: an exact count, a set, or a minimum. */
export type Arity = { exactly: number } | { oneOf: readonly number[] } | { atLeast: number };

export interface NCalcFunction {
  readonly name: string;
  readonly arity: Arity;
  /** What it is for, when the name does not say. */
  readonly note?: string;
}

/**
 * The note on the two functions that can throw, which is a worse failure than a wrong arity.
 *
 * Both are `IndexToPosition(lastData?.NewData?.BestLapOpponent...Position).Value`. The property
 * behind the `?.` chain is a plain `int` defaulting to -1, so the only way the argument is null is
 * the chain itself -- a frame on which SimHub's `NewData` reference is momentarily absent, which is
 * a race a dashboard evaluating on its own thread observes while the data is being rewritten. Then
 * `IndexToPosition` returns null and `.Value` throws, and a throwing expression draws the empty
 * string exactly as a mis-dispatched one does. A field goes blank for a frame and comes back.
 *
 * `BestLapOpponentPosition` and `BestLapOpponentSameClassPosition` are public properties of
 * `StatusDataBase`, so `second/values.ts` reads them under `GameData` instead. These stay listed
 * because they exist and the list is a record of what SimHub has, not of what OpenDash calls;
 * `expressions.test.ts` is what keeps a package from calling them.
 */
const THROWS =
  'takes a dummy argument, and throws on a frame where SimHub has no data: read GameData.BestLapOpponentPosition instead';

const exactly = (name: string, n: number, note?: string): NCalcFunction => ({ name, arity: { exactly: n }, ...(note ? { note } : {}) });
const oneOf = (name: string, ns: readonly number[], note?: string): NCalcFunction => ({ name, arity: { oneOf: ns }, ...(note ? { note } : {}) });
const atLeast = (name: string, n: number, note?: string): NCalcFunction => ({ name, arity: { atLeast: n }, ...(note ? { note } : {}) });

/** The branches of `NCalcEngineBase.EvaluateFunction`, in the order they are tested. */
const ENGINE: readonly NCalcFunction[] = [
  exactly('bestsectortime', 2),
  exactly('blink', 3, 'name, delayMs, enabled'),
  oneOf('changed', [2, 3]),
  exactly('compareversions', 2),
  exactly('currentlapgetsectortime', 2),
  exactly('downloadstring', 2),
  exactly('downloadstringasync', 2),
  atLeast('format', 2, 'value, .NET format string, and an optional addSign'),
  exactly('getrpmtickvalue', 1),
  atLeast('getvalue', 2),
  oneOf('isdecreasing', [2, 3]),
  oneOf('isincreasing', [2, 3]),
  oneOf('isnull', [1, 2], 'one argument tests, two substitutes'),
  exactly('isplayer', 1),
  exactly('lastlapgetsectortime', 2),
  exactly('lcase', 1),
  exactly('left', 3, 'value, start index, length — not value and length'),
  exactly('maximum', 2),
  exactly('minimum', 2),
  exactly('padleft', 2),
  oneOf('progress', [3, 4]),
  exactly('prop', 1, 'a property whose name is itself an expression'),
  exactly('rand', 1),
  exactly('readtextfile', 1),
  exactly('replace', 3),
  exactly('right', 3, 'value, start index, length'),
  oneOf('scroll', [3, 4]),
  exactly('secondstotimespan', 1),
  exactly('sessionbestlapgetsectortime', 2),
  exactly('setvalue', 2),
  exactly('tcase', 1),
  exactly('timesincelastevent', 1),
  exactly('timespantoseconds', 1),
  atLeast('toshorttime', 2, 'timespan, decimals, and optional alwaysSign and forceMinutes'),
  exactly('triggeraction', 1),
  exactly('ucase', 1),
];

/** `NCalcEngineMethodsRegistry.AddMethod` — the generic methods the dispatch falls through to. */
const REGISTRY: readonly NCalcFunction[] = [
  exactly('activescreenname', 0),
  exactly('createthreecolorsgradient', 7),
  exactly('createtwocolorsgradient', 5),
  exactly('dashboarddirectory', 0),
  exactly('dashboardheight', 0),
  exactly('dashboardwidth', 0),
  exactly('drivergamespecificdata', 2),
  exactly('getbestlapopponentleaderboardposition', 1, THROWS),
  exactly('getbestlapopponentleaderboardposition_playerclassonly', 1, THROWS),
  exactly('getbestsplitleaderboardposition', 1),
  exactly('getbestsplitleaderboardposition_playerclassonly', 1),
  exactly('getbestsplittime', 1),
  exactly('getbestsplittime_playerclassonly', 1),
  exactly('getcontrolleraxis', 2),
  exactly('getcontrollerbuttonstate', 2),
  exactly('getcontrollername', 1),
  exactly('getcontrollerpid', 1),
  exactly('getdeviceaccessorycolor', 1),
  exactly('getdevicebuttoncolor', 1),
  exactly('getdevicehardwareledcolor', 1),
  exactly('getdeviceinstanceid', 0),
  exactly('getdevicetelemetryledcolor', 1),
  exactly('getdevicetypeid', 0),
  exactly('getdevicetypename', 0),
  exactly('gethidmessage', 2),
  exactly('gethidmessagebyte', 3),
  exactly('getleaderboardcarclasscolor', 1),
  exactly('getleaderboardcarclasscount', 0),
  exactly('getleaderboardcarclassname', 1),
  exactly('getleaderboardcarclassopponentscount', 1),
  exactly('getleaderboardcarclasstextcolor', 1),
  exactly('getopponentleaderboardposition_aheadbehind', 1),
  exactly('getopponentleaderboardposition_aheadbehind_playerclassonly', 1),
  exactly('getopponentleaderboardposition_playerclassonly', 1),
  exactly('getplayerleaderboardposition', 0),
  exactly('isbuttonpressed', 0),
  exactly('loopingtimer', 1),
  exactly('mapthreecolors', 7),
  exactly('maptwocolors', 5),
  oneOf('repeatindex', [0, 1], 'the depth argument is optional'),
  exactly('rootdashboardscreenname', 0),
  exactly('writehid', 2),
];

/**
 * NCalc's own table, which SimHub does not intercept. `if` is NCalc's ternary and `in` its
 * membership test; the rest are the maths library. These are the ones a reader is most likely to
 * assume SimHub added and it did not.
 */
const BUILTIN: readonly NCalcFunction[] = [
  exactly('abs', 1),
  exactly('acos', 1),
  exactly('asin', 1),
  exactly('atan', 1),
  exactly('ceiling', 1),
  exactly('cos', 1),
  exactly('exp', 1),
  exactly('floor', 1),
  exactly('ieeeremainder', 2),
  exactly('if', 3),
  atLeast('in', 2),
  oneOf('log', [1, 2]),
  exactly('log10', 1),
  exactly('max', 2),
  exactly('min', 2),
  exactly('pow', 2),
  oneOf('round', [1, 2]),
  exactly('sign', 1),
  exactly('sin', 1),
  exactly('sqrt', 1),
  exactly('tan', 1),
  exactly('truncate', 1),
];

/** Every function by its lower-cased name. NCalc matching is case-insensitive. */
export const NCALC_FUNCTIONS: ReadonlyMap<string, NCalcFunction> = new Map(
  [...ENGINE, ...REGISTRY, ...BUILTIN].map((f) => [f.name, f] as const),
);

/** `driver<name>(position)`, resolved against SimHub's opponent providers at evaluation time. */
export const DRIVER_PREFIX = 'driver';
/** `driversector<name>(position, sector, includePrevious)`. Tested before `driver`, being longer. */
export const DRIVER_SECTOR_PREFIX = 'driversector';

export const arityAccepts = (arity: Arity, count: number): boolean =>
  'exactly' in arity ? count === arity.exactly : 'oneOf' in arity ? arity.oneOf.includes(count) : count >= arity.atLeast;

export const arityText = (arity: Arity): string =>
  'exactly' in arity ? `${arity.exactly}` : 'oneOf' in arity ? arity.oneOf.join(' or ') : `${arity.atLeast} or more`;

/**
 * NCalc words that are operators or literals rather than functions, and which are routinely
 * followed by a parenthesised operand: `(a) and (b)` looks exactly like a call to `and` to a
 * scanner that only checks for a name and a bracket.
 *
 * `in` is deliberately absent: NCalc really does spell its membership test `in(value, a, b)`.
 */
const OPERATOR_WORDS = new Set(['and', 'or', 'not', 'true', 'false', 'null']);

export interface FunctionCall {
  name: string;
  /** As written, for the message. */
  raw: string;
  argumentCount: number;
  /** Index in the expression where the name starts. */
  at: number;
}

/**
 * Every function call in an expression, with how many arguments each was given.
 *
 * Hand-written rather than a parser because the only things that can hide a comma or a bracket
 * from a scan are a string literal and a nested call, and both are cheap to track. Property
 * references are `[Name]` and cannot contain either.
 */
export function functionCalls(expression: string): FunctionCall[] {
  const calls: FunctionCall[] = [];
  let i = 0;
  while (i < expression.length) {
    const ch = expression[i]!;
    // A string literal: skip it whole, so a comma inside one is not counted as a separator.
    if (ch === "'") {
      i += 1;
      while (i < expression.length) {
        if (expression[i] === '\\') i += 2;
        else if (expression[i] === "'") {
          i += 1;
          break;
        } else i += 1;
      }
      continue;
    }
    // A property reference: skip it whole.
    if (ch === '[') {
      const close = expression.indexOf(']', i);
      i = close < 0 ? expression.length : close + 1;
      continue;
    }
    if (!/[A-Za-z_]/.test(ch)) {
      i += 1;
      continue;
    }
    let end = i;
    while (end < expression.length && /[A-Za-z0-9_]/.test(expression[end]!)) end += 1;
    const name = expression.slice(i, end);
    let after = end;
    while (after < expression.length && expression[after] === ' ') after += 1;
    if (expression[after] !== '(' || OPERATOR_WORDS.has(name.toLowerCase())) {
      i = end;
      continue;
    }
    // Walk the argument list, counting commas at depth one only.
    let depth = 0;
    let commas = 0;
    let seenArgument = false;
    let j = after;
    for (; j < expression.length; j++) {
      const c = expression[j]!;
      if (c === "'") {
        j += 1;
        while (j < expression.length) {
          if (expression[j] === '\\') j += 2;
          else if (expression[j] === "'") break;
          else j += 1;
        }
        seenArgument = true;
        continue;
      }
      if (c === '[') {
        const close = expression.indexOf(']', j);
        j = close < 0 ? expression.length : close;
        seenArgument = true;
        continue;
      }
      if (c === '(') {
        depth += 1;
        continue;
      }
      if (c === ')') {
        depth -= 1;
        if (depth === 0) break;
        continue;
      }
      if (c === ',' && depth === 1) {
        commas += 1;
        continue;
      }
      if (!/\s/.test(c)) seenArgument = true;
    }
    calls.push({ name, raw: expression.slice(i, Math.min(j + 1, expression.length)), argumentCount: seenArgument ? commas + 1 : 0, at: i });
    i = end;
  }
  return calls;
}

export interface UnknownFunction {
  call: FunctionCall;
  reason: 'unknown' | 'arity';
  message: string;
}

/**
 * The calls in an expression SimHub would not dispatch. An unknown name and a known name with the
 * wrong number of arguments fail the same way at runtime — no delegate, no result, an empty
 * string drawn — so they are reported together.
 */
export function unknownFunctions(expression: string): UnknownFunction[] {
  const out: UnknownFunction[] = [];
  for (const call of functionCalls(expression)) {
    const name = call.name.toLowerCase();
    if (name.startsWith(DRIVER_SECTOR_PREFIX)) {
      if (call.argumentCount !== 3) {
        out.push({ call, reason: 'arity', message: `${call.name} takes 3 arguments (position, sector, includePrevious), given ${call.argumentCount}` });
      }
      continue;
    }
    if (name.startsWith(DRIVER_PREFIX) && name !== 'drivergamespecificdata') {
      if (call.argumentCount !== 1) {
        out.push({ call, reason: 'arity', message: `${call.name} takes 1 argument (the leaderboard position), given ${call.argumentCount}` });
      }
      continue;
    }
    const fn = NCALC_FUNCTIONS.get(name);
    if (!fn) {
      out.push({ call, reason: 'unknown', message: `${call.name}() is not a function SimHub implements` });
      continue;
    }
    if (!arityAccepts(fn.arity, call.argumentCount)) {
      out.push({
        call,
        reason: 'arity',
        message: `${call.name}() takes ${arityText(fn.arity)} argument${'exactly' in fn.arity && fn.arity.exactly === 1 ? '' : 's'}, given ${call.argumentCount}${fn.note ? ` — ${fn.note}` : ''}`,
      });
    }
  }
  return out;
}
