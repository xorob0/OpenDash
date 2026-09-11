/**
 * Small, composable builders for SimHub NCalc expressions.
 *
 * Every helper returns a string. Operands are parenthesised so that nesting never changes
 * meaning. Function names and arities were verified against SimHub 9.12's NCalcEngineBase:
 * isnull(value, default) / isnull(value), if(c, a, b), format(value, fmt[, addSign]),
 * toshorttime(timespan, decimals[, alwaysSign[, forceMinutes]]), timespantoseconds(ts),
 * secondstotimespan(s), blink(name, delayMs, enabled), round, truncate, abs, padleft, replace,
 * ucase, lcase, max, min.
 */

export type Expr = string;

/** `[DataCorePlugin.GameData.Name]`: a telemetry property normalised by SimHub. */
export const game = (name: string): Expr => `[DataCorePlugin.GameData.${name}]`;

/** `[DataCorePlugin.GameRawData.Telemetry.Name]`: a raw, sim specific telemetry field. */
export const raw = (name: string): Expr => `[DataCorePlugin.GameRawData.Telemetry.${name}]`;

/** `[DataCorePlugin.Computed.Name]`: a value SimHub computes, e.g. Fuel_RemainingLaps. */
export const computed = (name: string): Expr => `[DataCorePlugin.Computed.${name}]`;

/** `[Plugin.Name]`: any property by its full name, e.g. `prop('PersistantTrackerPlugin.SessionBestLiveDeltaSeconds')`. */
export const prop = (fullName: string): Expr => `[${fullName}]`;

/** A single quoted string literal with quotes escaped the NCalc way. */
export const str = (s: string): Expr => `'${s.replace(/\\/g, '\\\\').replace(/'/g, "\\'")}'`;

export const num = (n: number): Expr => (Number.isFinite(n) ? String(n) : '0');

const wrap = (e: Expr): Expr => `(${e})`;

export const not = (a: Expr): Expr => `!${wrap(a)}`;
export const and = (...xs: Expr[]): Expr => xs.map(wrap).join(' and ');
export const or = (...xs: Expr[]): Expr => xs.map(wrap).join(' or ');

export const eq = (a: Expr, b: Expr): Expr => `${wrap(a)} = ${wrap(b)}`;
export const ne = (a: Expr, b: Expr): Expr => `${wrap(a)} != ${wrap(b)}`;
export const gt = (a: Expr, b: Expr): Expr => `${wrap(a)} > ${wrap(b)}`;
export const ge = (a: Expr, b: Expr): Expr => `${wrap(a)} >= ${wrap(b)}`;
export const lt = (a: Expr, b: Expr): Expr => `${wrap(a)} < ${wrap(b)}`;
export const le = (a: Expr, b: Expr): Expr => `${wrap(a)} <= ${wrap(b)}`;

export const add = (...xs: Expr[]): Expr => xs.map(wrap).join(' + ');
export const sub = (a: Expr, b: Expr): Expr => `${wrap(a)} - ${wrap(b)}`;
export const mul = (...xs: Expr[]): Expr => xs.map(wrap).join(' * ');
export const div = (a: Expr, b: Expr): Expr => `${wrap(a)} / ${wrap(b)}`;
export const mod = (a: Expr, b: Expr): Expr => `${wrap(a)} % ${wrap(b)}`;

/** String concatenation. NCalc uses `+`; every operand should already be a string. */
export const concat = (...xs: Expr[]): Expr => xs.map(wrap).join(' + ');

export const iff = (cond: Expr, a: Expr, b: Expr): Expr => `if(${cond}, ${a}, ${b})`;

/** `isnull(value, fallback)`: the fallback when the property is unavailable, e.g. the plugin is absent. */
export const isnull = (value: Expr, fallback: Expr): Expr => `isnull(${value}, ${fallback})`;

/** `isnull(value)`: true when the property is unavailable. */
export const isNull = (value: Expr): Expr => `isnull(${value})`;

/** .NET format string, e.g. `fmt(x, '0.0')`. With `addSign`, positive values get a leading `+`. */
export const fmt = (value: Expr, pattern: string, addSign = false): Expr =>
  addSign ? `format(${value}, ${str(pattern)}, true)` : `format(${value}, ${str(pattern)})`;

/**
 * SimHub's lap time formatter. Output is `h:mm:ss.fff` above an hour, `m:ss.fff` with
 * `forceMinutes` (or when minutes > 0), `ss.fff` otherwise; `decimals` sets the fraction length.
 */
export const toShortTime = (timespan: Expr, decimals: number, alwaysSign = false, forceMinutes = true): Expr =>
  `toshorttime(${timespan}, ${decimals}, ${alwaysSign}, ${forceMinutes})`;

export const timespanToSeconds = (ts: Expr): Expr => `timespantoseconds(${ts})`;
export const secondsToTimespan = (s: Expr): Expr => `secondstotimespan(${s})`;

export const round = (value: Expr, decimals = 0): Expr => `round(${value}, ${decimals})`;
export const truncate = (value: Expr): Expr => `truncate(${value})`;
export const abs = (value: Expr): Expr => `abs(${value})`;
export const max = (a: Expr, b: Expr): Expr => `max(${a}, ${b})`;
export const min = (a: Expr, b: Expr): Expr => `min(${a}, ${b})`;
export const replace = (value: Expr, from: string, to: string): Expr => `replace(${value}, ${str(from)}, ${str(to)})`;
export const ucase = (value: Expr): Expr => `ucase(${value})`;
export const lcase = (value: Expr): Expr => `lcase(${value})`;
/** First `count` characters, which is how a class chip keeps a long class name inside its box. */
export const left = (value: Expr, count: number): Expr => `left(${value}, ${num(count)})`;

/** Alternates true/false every `delayMs` while `enabled` is true. `name` must be unique per blinker. */
export const blink = (name: string, delayMs: number, enabled: Expr): Expr => `blink(${str(name)}, ${delayMs}, ${enabled})`;

/**
 * Formats a number of seconds as `h:mm:ss` without relying on TimeSpan format strings,
 * whose backslash escapes are awkward inside NCalc string literals.
 */
export const hms = (seconds: Expr): Expr => {
  const s = `max(0, ${seconds})`;
  const h = truncate(div(s, '3600'));
  const m = truncate(div(mod(s, '3600'), '60'));
  const sec = truncate(mod(s, '60'));
  return concat(fmt(h, '0'), str(':'), fmt(m, '00'), str(':'), fmt(sec, '00'));
};

/** Digit count of a non negative integer expression, for laying out text of known width. */
export const digitCount = (value: Expr, maxDigits: number): Expr => {
  let e: Expr = '1';
  for (let d = 2; d <= maxDigits; d++) e = iff(ge(value, num(10 ** (d - 1))), num(d), e);
  return e;
};

/** Every `[Some.Property]` reference in an expression. */
export const referencedProperties = (expression: string): string[] => {
  const out: string[] = [];
  const re = /\[([A-Za-z0-9_.]+)\]/g;
  let m: RegExpExecArray | null;
  while ((m = re.exec(expression)) !== null) out.push(m[1] ?? "");
  return out;
};

// --- The opponent family -----------------------------------------------------------------
//
// Every per-car value is `driver<name>(leaderboardposition)`, where the argument is a 1-based
// index into SimHub's leaderboard (sorted by live position). A missing row makes every one of
// these return null, including `driveravailable`, so a row's expressions are null-guarded and a
// row's Visible is bound to `driveravailable`. The index helpers below return -1 when there is
// no such car, and `driverXxx(-1)` is null, so the two compose safely.

/** The `driver<name>(position)` functions openDash uses, as SimHub registers them (lower case). */
export type DriverFunction =
  | 'available'
  | 'isplayer'
  | 'name'
  | 'shortname'
  | 'initials'
  | 'carclass'
  | 'carnumber'
  | 'position'
  | 'classposition'
  | 'positiongain'
  | 'positiongainclass'
  | 'bestlap'
  | 'lastlap'
  | 'currentlap'
  | 'gaptoleader'
  | 'gaptoplayer'
  | 'gaptoleadercombined'
  | 'relativegaptoplayer'
  | 'iscarinpit'
  | 'iscarinpitlane'
  | 'pitcount'
  | 'pitlastduration'
  | 'lapsdonesincelastpitout'
  | 'timesincelastpitout'
  | 'iracingirating'
  | 'fronttyrecompound'
  | 'reartyrecompound';

/** `driver<name>(position)`; `position` is an expression, so it can be a `repeatindex()` sum. */
export const driver = (fn: DriverFunction, position: Expr): Expr => `driver${fn}(${position})`;

/** `driversector<which>(position, sector, includePreviousSectors)`, a TimeSpan or null. */
export const driverSector = (which: 'lastlap' | 'bestlap' | 'currentlap' | 'best', position: Expr, sector: number, includePrevious = false): Expr =>
  `driversector${which}(${position}, ${num(sector)}, ${includePrevious})`;

/** `getplayerleaderboardposition()`: the player's own 1-based leaderboard index, -1 when unknown. */
export const playerPosition = (): Expr => 'getplayerleaderboardposition()';

/** `getopponentleaderboardposition_aheadbehind(k)`: 0 is the player, -1 the car ahead on track, 1 the car behind. */
export const aheadBehind = (k: Expr): Expr => `getopponentleaderboardposition_aheadbehind(${k})`;

/** The same on the player's class only. */
export const aheadBehindInClass = (k: Expr): Expr => `getopponentleaderboardposition_aheadbehind_playerclassonly(${k})`;

/** `getopponentleaderboardposition_playerclassonly(p)`: the p-th car of the player's class. */
export const classPosition = (p: Expr): Expr => `getopponentleaderboardposition_playerclassonly(${p})`;

/**
 * `getbestlapopponentleaderboardposition(0)`: the leaderboard index of the session-best car.
 * SimHub declares it without a parameter but its delegate takes one, so the dummy 0 is required.
 */
export const bestLapPosition = (): Expr => 'getbestlapopponentleaderboardposition(0)';

/** The same within the player's class. */
export const bestLapPositionInClass = (): Expr => 'getbestlapopponentleaderboardposition_playerclassonly(0)';

/** `getbestsplittime(sector)`: the session's best individual sector time, or null. */
export const bestSplitTime = (sector: number): Expr => `getbestsplittime(${num(sector)})`;

/**
 * `repeatindex()`: which copy of a repeated layer an expression is being evaluated for, 1 for
 * the original row. `depth` addresses an outer repeated layer when layers nest.
 */
export const repeatIndex = (depth?: number): Expr => (depth === undefined ? 'repeatindex()' : `repeatindex(${num(depth)})`);

/**
 * `prop(expr)`: a property whose name is computed, e.g. the lap-history slots
 * `'PersistantTrackerPlugin.PreviousLap_' + format(repeatindex() - 1, '00')`. NCalc only.
 */
export const propByName = (nameExpression: Expr): Expr => `prop(${nameExpression})`;
