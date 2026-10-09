/**
 * The telemetry the second screens read, as NCalc expressions. Every property name here was
 * checked against the decompiled SimHub 9.12.6 readers (see docs/research); where iRacing carries
 * nothing the expression is absent rather than invented, and the module that wanted it says so.
 *
 * Guarding is the theme. A lap time that has never been set is `00:00:00`, not null, so a time is
 * only shown when its seconds are above zero. Every per-car value is null for a row that does not
 * exist, so every one of them is wrapped.
 */
import { ncalc } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { ELLIPSIS, measureText, type MeasuredFace } from '../design/advances.ts';
import { MINUS, type Chars } from '../design/metrics.ts';
import type { Mark } from '../elements/mark.ts';
import { CLASS_BEST_LAP, flagBox, propertyName, setting } from '../contract.ts';
import { CHIP_WIDEST, chipText } from './chip.ts';
import { drawnAfter, drawnEither, drawnFigure, drawnText, type DrawnFigure } from './drawn.ts';
import { rpms } from '../shift.ts';
import { ds as dsTokens } from '../tokens.ts';

const {
  game,
  raw,
  computed,
  prop,
  str,
  num,
  iff,
  eq,
  gt,
  lt,
  ge,
  le,
  and,
  concat,
  fmt,
  signed,
  isnull,
  toShortTime,
  timespanToSeconds,
  secondsToTimespan,
  driver,
  playerPosition,
  aheadBehind,
  aheadBehindInClass,
  classPosition,
  bestLapPosition,
  repeatIndex,
  propByName,
  truncate,
  mod,
  div,
  add,
  sub,
  mul,
  max,
  min,
  abs,
  ucase,
  left,
  hms,
  ne,
  isIn,
  not,
  isNull,
} = ncalc;

/** What a value shows when the sim has not given one. */
export const NO_VALUE = '--';

/**
 * What a lap time shows when it has never been set: a minus in place of every digit of a time of
 * the same shape, so the placeholder occupies the cells the time will and the column does not move
 * when the first lap lands. `1:42.905` is `−:−−.−−−`, and a one-decimal `1:42.3` is `−:−−.−`.
 *
 * One spelling, taking the decimals it stands in for. There were two, nine characters here and
 * eight in the cards, and neither matched the other or the time it replaced.
 */
export const noTime = (decimals = 3): string => `${MINUS}:${MINUS}${MINUS}.${MINUS.repeat(decimals)}`;

/** The three-decimal form, which is what a lap time is drawn to unless it asks for fewer. */
export const NO_TIME = noTime();

/** Character budgets of the values the second screens draw. */
export const CHARS = {
  /** `1:42.905` */
  lapTime: { digits: 6, specials: 2 } as Chars,
  /**
   * `-0.21`: a signed delta in five digit cells and a point, the sign taking one of the cells. The
   * sector deltas and the lap review's two draw it to two places, which leaves two whole digits; the
   * lap history's column draws it to three, `+0.594`, which leaves one. A reading with more whole
   * digits than that gives up places to keep them, through {@link signedToFit}, so the in-lap's
   * `+123.45` is drawn `+123.5` rather than cut. The live delta to the reference has a budget of
   * its own, {@link CHARS.referenceDelta}.
   */
  delta: { digits: 5, specials: 1 } as Chars,
  /**
   * `−12.345`: the live delta to the reference, at the most places the precision setting draws it to.
   *
   * Its own budget rather than {@link CHARS.delta} widened, for the reason {@link CHARS.margin} gives:
   * the sector deltas share that one, and a sixth cell on each of three sectors wraps the compact
   * sector rank of the delta page onto a second line. The sign, two whole digits and three decimals,
   * whichever precision is chosen: a box cannot change its cells at runtime (SimHub marks `CharWidth`
   * `[NoBinding]`), so the one box is cut for the longer of the two readings and a two-place delta
   * sits left in it, as every other value sits in the cells cut for its longest reading. #322.
   */
  referenceDelta: { digits: 6, specials: 1 } as Chars,
  /** `-5.886` */
  relativeGap: { digits: 6, specials: 1 } as Chars,
  /** `+1L` or `+12.6` */
  gap: { digits: 6, specials: 1 } as Chars,
  /** `28.41`, and `142.35` on a track whose sectors run over a minute. */
  sector: { digits: 5, specials: 1 } as Chars,
  /** `24` */
  position: { digits: 2, specials: 0 } as Chars,
  /** `123`, bare: the hash belongs to the label. */
  carNumber: { digits: 4, specials: 0 } as Chars,
  /** `0:42:15` */
  clock: { digits: 6, specials: 2 } as Chars,
  /** `08:46` */
  minutesClock: { digits: 4, specials: 1 } as Chars,
  /**
   * `14:32`, and `12:59` on a twelve-hour clock: the same four digits and a colon, so one budget
   * holds either format. The `AM` or `PM` after a twelve-hour clock is a word beside the value and
   * not a cell of it, `M` being a glyph no cell holds; see {@link TimeOfDay}. Its own budget rather
   * than {@link CHARS.minutesClock}'s, which is a duration and reads `08:46` for the same cells, so
   * that widening one cannot quietly widen the other.
   */
  timeOfDay: { digits: 4, specials: 1 } as Chars,
  /** `299` */
  speed: { digits: 3, specials: 0 } as Chars,
  /** `12,450` */
  rpm: { digits: 6, specials: 1 } as Chars,
  /** `38.4` */
  fuel: { digits: 4, specials: 1 } as Chars,
  /** `2.84` */
  consumption: { digits: 4, specials: 1 } as Chars,
  /**
   * A signed margin: `−169.0` laps, `−1389` min.
   *
   * Five digit cells rather than the estimate's four, because the sign takes one of them and the
   * figure behind it is a whole race: the margin on the first lap of a 200-lap race is the tank's
   * range less 199 laps, which is three integer digits and a decimal on top of the minus. It is the
   * budget {@link CHARS.delta} already carries for the same reason, kept separate so that widening
   * one signed reading cannot quietly widen the other.
   */
  margin: { digits: 5, specials: 1 } as Chars,
  /** `104` */
  temperature: { digits: 3, specials: 0 } as Chars,
  /** `28.6` */
  pressure: { digits: 4, specials: 1 } as Chars,
  /** `999` */
  count: { digits: 3, specials: 0 } as Chars,
  /** `2.4k` */
  rating: { digits: 4, specials: 1 } as Chars,
  /** `100` per cent, and the input readouts. */
  percent: { digits: 3, specials: 0 } as Chars,
  /** `54.2` brake bias, `3` assist levels. */
  setting: { digits: 4, specials: 1 } as Chars,
  /** `GT3 · P12` and `GT3 · #12`: a class or car name, a separator and a number. */
  classPosition: { digits: 10, specials: 0 } as Chars,
  /** A short word: `Race`, `Dry`, `M`. */
  word: { digits: 8, specials: 0 } as Chars,
  /** `12 / 43`, and `142 / 350` in an endurance race: the two spaces and the slash are the specials. */
  lapOfTotal: { digits: 6, specials: 3 } as Chars,
};

/**
 * True when a TimeSpan holds a real lap time rather than the unset `00:00:00` or nothing at all.
 *
 * **The guard goes outside the conversion.** SimHub's `timespantoseconds` answers null for anything
 * that is not a TimeSpan, the number `0` included, so `timespantoseconds(isnull(ts, 0))` is null
 * whenever `ts` is, and NCalc's `null > 0` throws (`ArgumentNullException`, measured against the
 * NCalc.dll SimHub 9.12.6 ships). A throwing expression draws the empty string, so every lap time
 * with no value behind it drew nothing where it should have drawn {@link noTime}, and a value that
 * came and went drew a field that vanished and came back. #454.
 */
export const hasTime = (ts: Expr): Expr => gt(secondsOf(ts), num(0));

/**
 * A TimeSpan as seconds, or nought when there is no TimeSpan behind it.
 *
 * The one place a time is converted for arithmetic or a comparison, because every comparison and
 * every operator throws on the null `timespantoseconds` answers for a missing time (see
 * {@link hasTime}), and a binding that throws draws nothing at all. The `driver*` reads are null on
 * a frame SimHub is still building and while the player is unplaced, and a `GameData` time is null
 * with no session, so a guard written inside the conversion instead, `timespantoseconds(isnull(t,
 * 0))`, blanked the stint, the sectors and the session clock on exactly the frames they had a
 * placeholder for. `test/timespanGuard.test.ts` holds every binding of every package to this. #586.
 */
export const secondsOf = (ts: Expr): Expr => isnull(timespanToSeconds(ts), num(0));

/** A lap time as `m:ss.fff`, or the placeholder of the same shape when it was never set. */
export const lapTime = (ts: Expr, decimals = 3): Expr => iff(hasTime(ts), toShortTime(ts, decimals, false, true), str(noTime(decimals)));

/**
 * A sector time as seconds to two decimals, `28.41`, or `--` when it was never set.
 *
 * Two decimals rather than three, because every sample drawn beside one of these -- the table's
 * `28.41`, the sectors module's, the zone Sectors page's -- writes two, and the third decimal was
 * the one digit by which the bound text disagreed with the sheet it was measured from.
 *
 * Seconds rather than `toShortTime`, which is the same change made to the same end. `toShortTime`
 * turns a sector of a minute or more into `m:ss.ff`, which wants a second special cell, and
 * {@link CHARS.sector} is the width of every box a sector is drawn in: budgeting for that form
 * widens the three sector fields far enough that the 1280 x 60 sectors band sheds its Best field,
 * which is a real column lost on every lap of every track to buy the Nordschleife a colon. So the
 * form that fits the budget is the one that is drawn, and it is the form `zones/bandPages.ts`
 * already writes its sectors in. Five digits hold up to `999.99`, which no sector of a lap reaches.
 */
export const sectorTime = (ts: Expr, decimals = 2): Expr =>
  iff(hasTime(ts), fmt(secondsOf(ts), `0.${'0'.repeat(decimals)}`), str(NO_VALUE));

/** Seconds as `h:mm:ss`, or `-:--:--` when there is nothing to count. */
export const clock = (seconds: Expr): Expr => iff(gt(seconds, num(0)), hms(seconds), str('-:--:--'));

/**
 * Seconds as `mm:ss`, or `--:--` when there is nothing to count. Built the way `hms` is, minus the
 * hour term, because band D is 60 px tall and an hour that reads `0:` whenever a tank lasts under
 * one is a cell spent saying nothing. A range over 99 minutes overruns the four digits it is
 * budgeted, which no tank the sims model reaches.
 */
export const minutesClock = (seconds: Expr): Expr => {
  const s = max(num(0), seconds);
  const mmss = concat(fmt(truncate(div(s, num(60))), '00'), str(':'), fmt(truncate(mod(s, num(60))), '00'));
  return iff(gt(seconds, num(0)), mmss, str('--:--'));
};

/** An iRating as `2.4k`, or `--` when the sim does not report one. */
export const ratingK = (value: Expr): Expr => iff(gt(isnull(value, num(0)), num(0)), concat(fmt(div(value, num(1000)), '0.0'), str('k')), str(NO_VALUE));

// --- The player's own car ------------------------------------------------------------------

/** The player's 1-based leaderboard index, which every per-car read of your own car goes through. */
export const player = (): Expr => playerPosition();

/** The car ahead on track (-1) or behind (1), taken from the whole track whoever is on it. */
export const neighbour = (offset: number): Expr => aheadBehind(num(offset));

/**
 * Whether the rig counts positions in class rather than overall, which is `PositionMode`.
 *
 * One reading, because four drawings ask it: the position a row shows, the field that position is
 * shown out of, which cars a list is drawn from, and which neighbour a relative reads.
 */
export const classMode = (): Expr => eq(setting.positionMode(), str('class'));

/**
 * Whether a list draws the player's own class rather than the whole field.
 *
 * Two settings answer it and either one on its own is enough. A zone carries its own filter, which
 * is one rectangle of a face or one pit wall answering for itself; `PositionMode` is the rig's
 * answer, and it filters the rows it numbers rather than only numbering them. A column of class
 * positions over the whole field is three cars called P1 in an order that is not the order of any
 * of the numbers, which is not a leaderboard.
 *
 * The converse remains two questions, deliberately: a zone filtered to one class while the rig
 * counts overall lists that class by its overall places, which is a legitimate thing to want on a
 * multi-class grid and is what the zone setting is for.
 */
export const rowsInClass = (zoneFilter?: Expr): Expr => (zoneFilter === undefined ? classMode() : ncalc.or(zoneFilter, classMode()));

/**
 * The car ahead (-1) or behind (1) on track, in the player's class wherever the list it belongs to
 * is filtered to that class.
 *
 * {@link neighbour} asks the same of the whole track and is what a blue flag is about, a faster
 * class arriving being the commonest reason for one. This is what a drawing reads when its subject
 * is the cars a driver is actually racing.
 */
export const listNeighbour = (offset: number, zoneFilter?: Expr): Expr =>
  iff(rowsInClass(zoneFilter), aheadBehindInClass(num(offset)), aheadBehind(num(offset)));

/**
 * Where a split list's window opens: the first leaderboard row drawn under the limit line.
 *
 * The `topRows` at the head of the field are kept whatever the player does, so the window opens
 * directly under them for as long as the player is still inside it, and follows the player down
 * once the player is past it, the player sitting in the middle of its `rows` as in a relative
 * table. The max is also the guard: `getplayerleaderboardposition()` answers -1 before the sim has
 * placed the player, and a window pinned against the kept rows is the plain list the table drew
 * before there was a split.
 *
 * The min is the other half of the sentence the canvas writes, "when the player sits below the row
 * budget": a field the rows can all hold is not cut at all, and the last car of a longer one is the
 * last row rather than the window running off the end into blank rows with cars hidden behind the
 * line.
 */
const splitWindowTop = (topRows: number, rows: number): Expr =>
  max(num(topRows + 1), min(sub(playerPosition(), num(Math.ceil(rows / 2) - 1)), sub(opponentCount(), num(rows - 1))));

/** The row a table's Nth line shows, given its mode. */
export const rowIndex = {
  /** The leaderboard in order. */
  full: (): Expr => repeatIndex(),
  /** The player's class only, in leaderboard order. */
  inClass: (): Expr => classPosition(repeatIndex()),
  /** Relative to the player on track, the player on row `centre`. */
  relative: (centre: number): Expr => aheadBehind(sub(repeatIndex(), num(centre))),
  /** The same, counting only the player's own class. */
  relativeInClass: (centre: number): Expr => aheadBehindInClass(sub(repeatIndex(), num(centre))),
  /**
   * A split list's second block: the leaderboard again, counting from where the window opens.
   *
   * Still the overall leaderboard, and deliberately: `inClass` would restart the numbering at the
   * player's own class while the rows above the limit line draw overall positions, so the two
   * blocks of one list would be counting different fields.
   */
  split: (topRows: number, rows: number): Expr => sub(add(splitWindowTop(topRows, rows), repeatIndex()), num(1)),
};

/** How many cars a split list leaves out: the run between the rows it keeps and the window. */
export const splitHiddenCars = (topRows: number, rows: number): Expr => sub(splitWindowTop(topRows, rows), num(topRows + 1));

/**
 * The leaderboard index of the car holding the session's best lap, or -1 while there is none.
 *
 * **Read as a property, never through `getbestlapopponentleaderboardposition()`.** That function is
 * `IndexToPosition(lastData?.NewData?.BestLapOpponentPosition).Value`, and the `?.` chain makes the
 * argument `int?` although the property behind it is a plain `int` defaulting to -1. So on any frame
 * where SimHub's `NewData` reference is momentarily null -- between the ticks it swaps them on, which
 * is a race a dashboard evaluating on its own thread can and does observe -- `IndexToPosition` hands
 * back null and `.Value` throws.
 *
 * A throwing NCalc expression does not fail loudly in SimHub. It draws **the empty string**, which is
 * the same trap `left([Class], 4)` fell into and the reason `ncalcFunctions.ts` exists. Every field
 * whose text went through this function therefore blanked on those frames and came back on the next:
 * reported from a rig as the session's best time appearing on the second lap and then "blinking a
 * lot", steady again once the car stopped and the data stopped being rewritten under it.
 *
 * `BestLapOpponentPosition` is a public property of `StatusDataBase`, so SimHub publishes it under
 * `GameData` like any other, and reading it cannot throw. `IndexToPosition`'s own arithmetic is the
 * `+ 1`, which applies to a real index and not to the -1 that means "nobody yet".
 *
 * **Which field "the session" is follows `PositionMode`**, as every list already does (#433). The
 * fastest car of the whole field is an LMP2 time handed to a GT3 driver, which is a reference
 * nobody in that car can act on; counting in class, the row is the fastest car of the player's own
 * class instead. SimHub keeps that one as a property too, `BestLapOpponentSameClassPosition`, set
 * from `Opponents.IndexOf` exactly as its overall twin is, so it is the same leaderboard index with
 * the same -1 and takes the same `+ 1`. Its function form,
 * `getbestlapopponentleaderboardposition_playerclassonly`, is the same `?.` chain and throws the
 * same way, so it is not called either.
 *
 * The switch is here rather than at each field so that every reader of the row follows it without
 * being told: the Lap times, Sectors and pit wall fields, and the purple on a leaderboard's `Best`
 * column through {@link carIsSessionBest}, which on a board filtered to one class would otherwise
 * paint the overall fastest car and therefore nobody at all.
 *
 * A list asks a wider question than the rig does, which is why the condition can be passed. A zone
 * filtered to the player's class draws only that class whatever `PositionMode` says, so its purple
 * has to be the fastest car of that class or it falls on a car the list does not draw; the table
 * hands in the same condition its rows are chosen by, a plain `true` or `false` where the rows
 * never change field, and every other reader takes the rig's.
 */
export const sessionBestRow = (inClass: Expr | boolean = classMode()): Expr => {
  const inClassIndex = isnull(game('BestLapOpponentSameClassPosition'), num(-1));
  const fieldIndex = isnull(game('BestLapOpponentPosition'), num(-1));
  const index = typeof inClass === 'boolean' ? (inClass ? inClassIndex : fieldIndex) : iff(inClass, inClassIndex, fieldIndex);
  return iff(ge(index, num(0)), add(index, num(1)), num(-1));
};

/**
 * The session's best lap: of the whole field, or of the player's class when the rig counts in class.
 *
 * **Never `driverbestlap()` of {@link sessionBestRow}**, which is what this was when a rig reported
 * the session best flashing on joining a session and through the first timed lap (#454). The row
 * comes from a published property, and SimHub publishes a property from the last frame it finished.
 * `driverbestlap()` instead reads `lastData.NewData`, the frame SimHub is building, and SimHub builds
 * each frame in place: at the start of every tick `NewData` is a fresh object with no leaderboard,
 * which it fills in over the course of the tick. A dashboard renders on its own thread, so a frame it
 * draws mid-build finds no car at the row and gets null, which {@link lapTime} used to draw as an
 * empty field. It is the same race the property read above already took the row out of. The race is
 * read from the decompiled 9.12.6; the emulator on the test VM did not produce it, so what a rig
 * sees is argued from SimHub's code rather than filmed.
 *
 * So the time is read from a published property as well. For the field SimHub has one:
 * `BestLapOpponent` is declared under `GameData` like any other member, because the empty frame
 * SimHub declares its properties from carries a blank car there, and the read is null-safe the whole
 * way down. For the class it has none: `BestLapSameClassOpponent` is null on that frame, so the plugin
 * copies that car's time out of the finished frame as {@link CLASS_BEST_LAP}. Without the plugin the
 * class reading falls back on the row, which is right on every frame SimHub is not mid-build.
 */
export const sessionBestLap = (): Expr =>
  iff(classMode(), isnull(prop(propertyName(CLASS_BEST_LAP)), driver('bestlap', sessionBestRow(true))), game('BestLapOpponent.BestLapTime'));

/**
 * The session's best time through one sector, of the field or of the player's own class as
 * `PositionMode` says, for the same reason {@link sessionBestRow} follows it: a GT3 driver could
 * never draw a sector purple while an LMP2 was on track. A TimeSpan, or null before anyone has set
 * one.
 *
 * Both are functions rather than properties, SimHub publishing the splits as a list and not under
 * `GameData`; both are null-safe the whole way down and return a `TimeSpan?`, so neither can throw.
 */
export const sessionBestSplit = (sector: number): Expr => iff(classMode(), ncalc.bestSplitTimeInClass(sector), ncalc.bestSplitTime(sector));

// --- Per-car reads, each guarded for a row that is not there --------------------------------

export const carAvailable = (idx: Expr): Expr => isnull(driver('available', idx), 'false');
export const carIsPlayer = (idx: Expr): Expr => isnull(driver('isplayer', idx), 'false');
export const carInPit = (idx: Expr): Expr => isnull(driver('iscarinpitlane', idx), 'false');
export const carName = (idx: Expr): Expr => isnull(driver('name', idx), str(''));
/**
 * A car number, drawn bare.
 *
 * It used to carry its own `#`, and `#` is one of the glyphs that overruns a monospace cell cut
 * for digits, so WPF clipped it in all thirty-one places this reaches. The hash is a label now:
 * a column header on a table, a field label everywhere else. Rule 19 -- only what fits a cell may
 * be drawn in one -- and a hash never did.
 */
/**
 * A car's race number, or nothing at all before the sim has one.
 *
 * **Guarded against a negative**, which is what iRacing hands back for a car it has not placed yet.
 * A field of AI on the grid drew `#-1` down the number column until their first lap; the `isnull`
 * caught an absent number and had nothing to say about a present nonsense one. A number is a label
 * rather than a quantity, so the honest answer for "no number yet" is an empty cell: the column
 * keeps its width and fills in as the sim learns, where `-1` reads as a fact about the car.
 */
export const carNumber = (idx: Expr): Expr => {
  const value = isnull(driver('carnumber', idx), num(-1));
  return iff(ge(value, num(0)), fmt(value, '0'), str(''));
};

/** The entry's team, as the sim publishes it, or nothing where it publishes none. */
export const carTeamName = (idx: Expr): Expr => isnull(driver('teamname', idx), str(''));

/**
 * `L. Byrne`: SimHub's own short form of the name, which is `StringExtensions.GetShortName` in
 * WoteverCommon and comes back unchanged for a name of one word.
 */
const carShortName = (idx: Expr): Expr => isnull(driver('shortname', idx), str(''));

/**
 * The surname, taken back out of the short name.
 *
 * `GetShortName` writes the first letter of the first word, a full stop and a space, then the rest of
 * the words joined back: `Liam Byrne` is `L. Byrne` and `Liam Van Byrne` is `L. Van Byrne`. So taking
 * `L. ` out of it leaves the surname, and the needle is built from the name rather than written down
 * because the initial is whatever the driver's is. There is no `indexof`, `substring` or `length` in
 * SimHub's NCalc to find a space with, so this is the only handle a formula has on where a name
 * divides.
 *
 * A one-word name comes out of `GetShortName` untouched, and the needle is then `V. ` against
 * `Verstappen`, which does not occur: the surname is the whole name and nothing is cut. That is why
 * the needle is the initial and the stop rather than `left(short, 3)`, which would have left
 * `stappen`.
 */
const surname = (idx: Expr): Expr => {
  const short = carShortName(idx);
  return ncalc.replaceWith(short, concat(left(short, 1), str('. ')), str(''));
};

/**
 * The first name: the full name with the space and the surname taken off the end of it.
 *
 * A one-word name has the whole name as its surname, so the needle carries a leading space that does
 * not occur and the first name is the whole name too. {@link driverName} does not reach here in that
 * case, but a helper that only works under its caller's guard is one edit from being wrong.
 */
const firstName = (idx: Expr): Expr => ncalc.replaceWith(carName(idx), concat(str(' '), surname(idx)), str(''));

/**
 * Whether the name divides at all, read off SimHub's short form: its second character is the full
 * stop of an abbreviated first name, and a one-word name has a letter there.
 *
 * Cheaper than comparing the short form against the full name, which is the same question asked with
 * one opponent lookup more — and every lookup in here is made on every row on every frame.
 */
const hasSurname = (idx: Expr): Expr => eq(left(carShortName(idx), 1, 1), str('.'));

/**
 * A driver's name in the format the rig asks for, or the entry's team where it asks for that.
 *
 * The four formats are `contract.ts`'s `DriverNameFormat` and the panel offers them by example. The
 * team is a separate setting and replaces the name rather than reformatting it — a team is not a
 * person and has no surname to abbreviate — and it falls back to the driver per row wherever the sim
 * publishes no team for that car.
 *
 * NCalc evaluates the branch it discards as well as the one it keeps, so every format is computed on
 * every frame for every row whatever the setting says. That is the cost of the setting being a
 * setting: a formula is written once, into one row template the repeated layer stamps, and cannot be
 * specialised at build time for a value a driver changes mid-session. It is also why the two reordered
 * formats share the first name between them rather than each building one: the shape is
 * `<head> + firstName` with the head chosen, which costs two opponent lookups per row per frame less
 * than the same thing written as two whole names.
 *
 * **Not title-cased**, although #385 asked for it. SimHub's `tcase` is `TextInfo.ToTitleCase`, which
 * lower-cases the rest of every word it capitalises unless the word is entirely upper case: it turns
 * `McDonald` into `Mcdonald` and leaves `LIAM BYRNE` shouting, which is the case it would have been
 * reached for. Nor is a bracketed prefix stripped, for the reason {@link surname} gives — finding the
 * closing bracket needs an index into the string and no SimHub function returns one.
 */
export function driverName(idx: Expr): Expr {
  const full = carName(idx);
  const head = iff(setting.driverNameFormatIs('initialFirstName'), concat(left(surname(idx), 1), str('. ')), concat(surname(idx), str(' ')));
  const named = iff(
    setting.driverNameFormatIs('full'),
    full,
    iff(setting.driverNameFormatIs('initialSurname'), carShortName(idx), iff(hasSurname(idx), concat(head, firstName(idx)), full)),
  );
  const team = carTeamName(idx);
  return iff(and(setting.driverNameTeam(), ne(team, str(''))), team, named);
}

/**
 * A text cut to `chars` characters, closed with an ellipsis where it was cut.
 *
 * SimHub's text items have no ellipsis mode: WPF is handed the box as `MaxTextWidth` and clips
 * whatever overruns it, which is how the site's own opponents page came to show `Emma Larse`. So the
 * cut is made in the expression.
 *
 * The test is one character rather than a comparison of two strings. `left(value, 1, chars)` asks for
 * the one character just past the budget, and `StringExtensions.Left` returns an empty string rather
 * than throwing when the value does not reach that far, so an empty answer is exactly "it fits". That
 * matters because `value` here is a whole driver name, thirteen opponent lookups deep, and every
 * mention of it is another thirteen evaluated on every row on every frame: the obvious spelling,
 * `if(left(value, chars) != value, ...)`, names it four times where this one names it three.
 *
 * The cut keeps `chars - 1` characters and the ellipsis takes the last, so what is drawn is never
 * wider than `chars` of the face's widest glyph — the ellipsis being the narrower of the two in every
 * face `advances.ts` measures — which is what the caller's `widest` declares.
 *
 * **The space the cut lands on goes with it.** A cut between two words kept the space and then put the
 * ellipsis after it, and the relative on the VM drew `Chloe …` and `Marco …`: a gap and then three
 * dots reads as a pause rather than as a name that was too long, and it spends a whole character of a
 * seven-character column on nothing. The remedy is a `replace` of the space and the ellipsis by the
 * ellipsis, because SimHub's NCalc has no `trim` — `ncalcFunctions.ts` is the whole of the table it
 * dispatches on, read out of 9.12.6, and the string functions in it are `left`, `right`, `replace`,
 * `padleft` and the three case changes. The alternative, asking whether the last kept character is a
 * space and cutting one more where it is, gives the same answer and names `value` five times where
 * this names it three: `value` is a driver name thirteen opponent lookups deep, evaluated per row per
 * frame, so the two extra mentions are twenty-six more lookups a row. The needle can only match what
 * this line just built, a driver name carrying an ellipsis of its own not being a thing SimHub sends,
 * which is the check every caller of `replace` owes.
 */
export const ellipsised = (value: Expr, chars: number): Expr =>
  chars <= 0
    ? str('')
    : iff(eq(left(value, 1, chars), str('')), value, ncalc.replace(concat(left(value, chars - 1), str(ELLIPSIS)), ` ${ELLIPSIS}`, ELLIPSIS));

export const carClass = (idx: Expr): Expr => driver('carclass', idx);

/** The position a table shows, overall or in class per the plugin's PositionMode. */
/**
 * A car's position, overall or in class, or 0 while the sim has not placed it.
 *
 * Zero is kept as the *number* because callers do arithmetic on it and compare it. What changed is
 * that nothing draws a zero any more: {@link hasPosition} is the guard, and every drawing that
 * prints a position asks it first. A grid of AI before the green flag has no positions at all, and
 * a column of `P0` is a row of wrong answers where an empty cell is an honest one.
 */
export const carPosition = (idx: Expr): Expr => iff(classMode(), isnull(driver('classposition', idx), num(0)), isnull(driver('position', idx), num(0)));

/** True once the sim has actually placed this car. Positions count from one, so zero is "not yet". */
export const hasPosition = (idx: Expr): Expr => gt(carPosition(idx), num(0));

/** A position as digits, or `--` before the sim has placed the car: `4`, and `--` on the grid. */
export const positionDigits = (idx: Expr): Expr => iff(hasPosition(idx), fmt(carPosition(idx), '0'), str(NO_VALUE));

/**
 * How wide {@link positionDigits} draws, for the "/ 24" that follows it on three surfaces.
 *
 * Beside the expression it measures rather than at each of the three, so the two cannot disagree
 * about which branch is on the screen: a denominator placed for `--` beside a value reading `12`
 * is the fault {@link DrawnWidth} exists to prevent, and it would be a fault nobody could see in
 * the page that declared it.
 */
export const positionDrawn = (idx: Expr): DrawnFigure =>
  drawnEither(hasPosition(idx), drawnFigure({ value: carPosition(idx), digits: CHARS.position.digits }), drawnText(NO_VALUE));

/** A position with the P the drawings prefix it with, or `P--` before the sim has placed the car. */
export const positionLabelled = (idx: Expr): Expr => concat(str('P'), positionDigits(idx));

/** How wide {@link positionLabelled} draws: the P, and then the digits or the placeholder. */
export const positionLabelledDrawn = (idx: Expr): DrawnFigure => drawnAfter('P', positionDrawn(idx));

/**
 * Places gained since the start, signed; 0 when the sim does not track it.
 *
 * Counted in the field the position beside it is counted in, which is {@link carPosition}'s own
 * question and is why the two read the same setting: a triangle counting the whole race next to a
 * number counting one class is the pair of cells #212 is about, a place and the movement of that
 * place answered from different fields. A car that started eighth overall and third in class and
 * now runs fifth and first drew `P1` with three places gained beside it.
 *
 * SimHub publishes the twin rather than leaving it to be worked out: `PositionGainClass` is
 * "driver's position gains in his own class since the start of the race/connection", registered
 * beside `PositionGain` among the opponent providers of SimHub 9.12.6.
 */
export const carRankChange = (idx: Expr): Expr =>
  iff(classMode(), isnull(driver('positiongainclass', idx), num(0)), isnull(driver('positiongain', idx), num(0)));

/**
 * The gap to the leader: `Lead` on the leader's own row, `+2.6` on a car on the lead lap, and
 * SimHub's own `+1L` once a car is a lap or more down.
 *
 * The seconds are formatted here rather than taken from `gaptoleadercombined`, which is one string
 * for both cases and whose sign and decimals the dash has no say in: the column is drawn beside
 * the interval, which is `signed(..., '0.0')`, and two neighbouring columns of the same quantity
 * reading to different precisions is what the sheet is measured against. `signed` writes the
 * typographic minus, so the value is not formatted again on top of it.
 *
 * Lapped is asked of the laps rather than inferred from the string, since the string is the answer
 * and not the question. The leader is leaderboard row 1, the board being sorted by live position;
 * where either lap is missing the difference is null, the test is false, and the row falls back to
 * seconds, which is the reading that is always true.
 *
 * This is the reading of a list drawn from the whole field. A list drawn from one class measures to
 * the leader of that class instead, which is {@link carClassRaceGap}.
 */
export const carRaceGap = (idx: Expr): Expr => {
  const gap = driver('gaptoleader', idx);
  const lapsDown = sub(driver('currentlap', num(1)), driver('currentlap', idx));
  return iff(
    eq(isnull(driver('position', idx), num(0)), num(1)),
    str('Lead'),
    iff(
      ncalc.isNull(gap),
      str(NO_VALUE),
      iff(gt(lapsDown, num(0)), isnull(driver('gaptoleadercombined', idx), str(NO_VALUE)), signed(gap, '0.0')),
    ),
  );
};

/**
 * The same gap on a list drawn from the player's own class: `Lead` on the row it counts from, the
 * seconds to that car on a classmate sharing its lap, and `+1L` on one that does not.
 *
 * The reference is the leader of the list rather than the leader of the race, because a column
 * measured to a car that is not on it says nothing a reader can use. Where the player's class runs
 * a lap behind the overall leader, every row of a class board measured the other way reads `+1L`
 * and no row of it reads `Lead`, which is a column carrying no intra-class gap at all.
 *
 * SimHub does publish the class leader's own figures: `gaptoclassleader`, `lapstoclassleader` and
 * `gaptoclassleadercombined` are registered beside the overall ones among the opponent providers of
 * 9.12.6, and docs/research/simhub-dash-format.md records them. This column nevertheless builds the
 * value as the difference of the two gaps to the overall leader, exactly as {@link carInterval}
 * takes one between two rows, with the lap count from `currentlap`: that is the arithmetic the pit
 * wall values test can evaluate against its model of a field today, and it is proved there against
 * the leader of the list. Reading the three providers instead is the simplification to make, for
 * this column and {@link carRaceGap}'s lapped case together, once that test's evaluator carries them.
 *
 * The word on the row the column counts from is the one thing here that follows the numbering
 * rather than the rows. `Lead` is a claim about a place, and a zone filtered to one class while
 * the rig counts overall draws that class by its overall places: the top row of such a list reads
 * `P2` where the class is a lap down on another, and `Lead` beside it is two cells of one row
 * making claims that contradict each other. {@link carPosition} is the place the row draws, so the
 * word is shown when that place is the first and the cell is left empty otherwise, the row having
 * nothing to measure against itself, which is what {@link carInterval} already draws in the cell
 * beside it. A class leading the race keeps the word under either setting.
 */
export const carClassRaceGap = (idx: Expr): Expr => {
  const here = driver('gaptoleader', idx);
  const lead = driver('gaptoleader', classPosition(num(1)));
  const lapsDown = sub(driver('currentlap', classPosition(num(1))), driver('currentlap', idx));
  return iff(
    eq(isnull(driver('classposition', idx), num(0)), num(1)),
    iff(eq(carPosition(idx), num(1)), str('Lead'), str('')),
    iff(
      ncalc.or(ncalc.isNull(here), ncalc.isNull(lead)),
      str(NO_VALUE),
      iff(gt(lapsDown, num(0)), concat(str('+'), fmt(lapsDown, '0'), str('L')), signed(sub(here, lead), '0.0')),
    ),
  );
};

/**
 * The gap to the player on track, signed, three decimals: a car ahead reads `−5.886` and a car
 * behind `+0.722`. The minus is the typographic one, which `signed` substitutes for the hyphen
 * .NET's formatter writes.
 *
 * Three places in {@link CHARS.relativeGap}'s six cells leave two whole digits, and a car half a lap
 * away is a hundred seconds off on a lap of three and a half minutes, which is Le Mans and the
 * Nordschleife with a thin field. Past that the gap gives up places rather than its last digit,
 * `−104.31`, through {@link signedToFit}, as the deltas do (#886).
 */
export const carRelativeGap = (idx: Expr): Expr =>
  iff(ncalc.isNull(driver('relativegaptoplayer', idx)), str(NO_VALUE), signedToFit(driver('relativegaptoplayer', idx), CHARS.relativeGap, 3));

/**
 * The widest reading {@link carRelativeGap} draws, which is what a box drawing it declares: the true
 * minus and five digits around a point, the whole of {@link CHARS.relativeGap}. Past a hundred
 * seconds the gap draws `−104.32` in the same six cells, so this is the length of every reading.
 */
export const RELATIVE_GAP_WIDEST = `${MINUS}99.999`;

/**
 * The interval to the car in front on the leaderboard: the difference of the two gaps to the
 * leader. The leader's own row is empty, there being nothing in front of it.
 *
 * The car in front is the row above on the list this is drawn in, so a list of one class takes
 * {@link carClassInterval} instead. The two are one decision with the gap column beside them: Int
 * is the difference of two neighbouring Gaps, and a board counting the one from the class leader
 * while the other counted between leaderboard neighbours would draw two columns that do not add up.
 */
export const carInterval = (idx: Expr): Expr => {
  const ahead = driver('gaptoleader', sub(idx, num(1)));
  const here = driver('gaptoleader', idx);
  return iff(and(gt(idx, num(1)), ncalc.not(ncalc.isNull(ahead)), ncalc.not(ncalc.isNull(here))), signed(sub(here, ahead), '0.0'), str(''));
};

/**
 * The interval to the car one place ahead in the player's own class, which on a list drawn from
 * that class is the row above.
 *
 * The row above is found through the class place rather than through the row number, `idx` being
 * the only thing a cell is given: one off a car's own `classposition` and back through SimHub's
 * class-only lookup is the car the list draws above it, whatever leaderboard rows fall between the
 * two.
 */
export const carClassInterval = (idx: Expr): Expr => {
  const place = isnull(driver('classposition', idx), num(0));
  const ahead = driver('gaptoleader', classPosition(sub(place, num(1))));
  const here = driver('gaptoleader', idx);
  return iff(and(gt(place, num(1)), ncalc.not(ncalc.isNull(ahead)), ncalc.not(ncalc.isNull(here))), signed(sub(here, ahead), '0.0'), str(''));
};

export const carLastLap = (idx: Expr): Expr => lapTime(driver('lastlap', idx));
export const carBestLap = (idx: Expr): Expr => lapTime(driver('bestlap', idx));
/**
 * Whether a car holds the session best, which is the fastest car of the field its list draws: the
 * player's class where `inClass` holds and the whole field otherwise, the rig's `PositionMode` when
 * nothing is passed (see {@link sessionBestRow}).
 */
export const carIsSessionBest = (idx: Expr, inClass?: Expr | boolean): Expr => {
  const row = sessionBestRow(inClass);
  return and(eq(idx, row), gt(row, num(0)));
};
export const carSector = (idx: Expr, sector: number): Expr => sectorTime(ncalc.driverSector('lastlap', idx, sector, false));
export const carStintLaps = (idx: Expr): Expr => fmt(isnull(driver('lapsdonesincelastpitout', idx), num(0)), '0');
export const carPitCount = (idx: Expr): Expr => fmt(isnull(driver('pitcount', idx), num(0)), '0');
export const carCompound = (idx: Expr): Expr => isnull(driver('fronttyrecompound', idx), str(''));
/**
 * A driver's iRating, which is the only rating SimHub publishes per car.
 *
 * The canvas draws two further marks beside it, the licence class and the safety rating, and
 * iRacing knows both; SimHub's `driver*` family carries neither, and nothing else here has been
 * verified to. The badge in `elements/badge.ts` is therefore drawn against the tokens and bound to
 * nothing, and `purpose.rating`'s gain and loss wait on a change to the rating that no reader
 * publishes and that a dashboard, keeping no history, cannot work out for itself.
 */
export const carRating = (idx: Expr): Expr => ratingK(driver('iracingirating', idx));

/**
 * The car immediately behind on track, which is the one a blue flag is about.
 *
 * On track and not on the leaderboard: a blue flag is thrown for the car that is about to arrive,
 * and the car a place behind on the timing screen may be a lap away.
 *
 * Whoever is on the track, moreover, and not whoever is in the player's class: the car about to
 * arrive is most often a faster class, so this stays on {@link neighbour} while the lists moved to
 * {@link listNeighbour}. The position it prints still follows `PositionMode`, as every position
 * OpenDash draws does.
 */
export const carBehind = (): Expr => neighbour(1);

/**
 * The class of the car behind, cut to the four characters a chip holds, or the empty string when
 * there is nothing behind.
 *
 * The cut is `chipText`'s and not a second one: the class names are the same names the leaderboard
 * draws, and a band that wrote `Ferrari 296 GT3` where the chip writes `FERR` would be two
 * spellings of one fact. Empty rather than a placeholder, because the caller drops the separator
 * with it rather than writing a dot before nothing.
 */
export const carBehindClass = (): Expr => iff(carAvailable(carBehind()), chipText(carClass(carBehind())), str(''));

/**
 * `P4 LMP2`: the position of the car behind and its class, or the empty string when there is
 * nothing behind. The position honours PositionMode, as every position OpenDash draws does.
 */
export const carBehindPositionClass = (): Expr =>
  iff(carAvailable(carBehind()), concat(str('P'), fmt(carPosition(carBehind()), '0'), str(' '), chipText(carClass(carBehind()))), str(''));

/** The widest `carBehindPositionClass` can draw: a two-digit place and the widest chip. */
export const WIDEST_BEHIND_POSITION_CLASS = `P99 ${CHIP_WIDEST}`;

// --- Session, car and environment -----------------------------------------------------------

export const currentLap = (): Expr => isnull(game('CurrentLap'), num(0));
/** Laps completed, which is one behind the lap in progress for as long as a lap is in progress. */
export const completedLaps = (): Expr => isnull(game('CompletedLaps'), num(0));
export const totalLaps = (): Expr => isnull(game('TotalLaps'), num(0));
/**
 * The session's time left in seconds, nought with no session. Guarded here rather than at each
 * caller, so that {@link isTimedSession}, {@link sessionClock} and the fuel margin read a session
 * that is not there as untimed instead of throwing on it. #586.
 */
export const sessionTimeLeft = (): Expr => secondsOf(game('SessionTimeLeft'));
/**
 * The longest session clock taken at its word: a day, and a day inclusive.
 *
 * iRacing reports a week of time left -- 604800 s, `Irsdk.UnlimitedTime` -- for a session that has
 * no clock, and nothing SimHub publishes says "untimed" in words, so the sentinel has to be read as
 * a threshold rather than matched. A day is where the threshold sits, and **exactly** a day is on
 * the timed side of it, because the 24-hour races are the longest real clocks anybody drives:
 * Daytona, Le Mans and the Nürburgring are 86400 s exactly, and `SessionTimeRemain` equals
 * `SessionTimeTotal` until the clock starts, so a boundary that excluded 86400 would have a 24-hour
 * race open by announcing it has no end -- in the one class of race where time left is the whole
 * point. `24:00:00` is six digit cells and two separators, which is {@link CHARS.clock} exactly, so
 * including the boundary costs no width anywhere.
 *
 * What the threshold still gets wrong is the other direction: a real clock longer than a day -- a
 * 25-hour race, a multi-day league session -- reads the mark for its whole length. That is the
 * trade this constant has always made, and no property distinguishes the two cases.
 */
export const UNTIMED_SECONDS = 86400;
export const isTimedSession = (): Expr => and(gt(sessionTimeLeft(), num(0)), le(sessionTimeLeft(), num(UNTIMED_SECONDS)));

/**
 * A session the sim has given, which has no clock: iRacing's week of `SessionTimeLeft` in a
 * lap-limited race. The other half of {@link isTimedSession}'s window, and the state the two used
 * to be folded into. Strictly above the threshold, so a 24-hour race draws its clock; see
 * {@link UNTIMED_SECONDS} for which side of the line each length of race falls on.
 */
export const isUntimedSession = (): Expr => gt(sessionTimeLeft(), num(UNTIMED_SECONDS));

/** What a session clock reads where there is no session at all: a clock of the same shape, unset. */
export const NO_CLOCK = `-:--:--`;

/**
 * The mark a session clock reads where the session has no clock at all, U+221E.
 *
 * It is not drawn through a monospace cell and cannot be. Rule 19: the cell is cut for the widest
 * ink a digit draws, 0.47 em in SemiBold and 0.49 in Bold, and this glyph advances 0.651 and 0.658
 * em in those faces -- a third over the cell in both, which is why it cannot be a cell, the same
 * reason the eight characters `font.cell.excluded` names cannot. It is not the widest thing the
 * cells have refused and no claim here needs it to be: four of those eight -- `%`, `@`, `W` and `m`
 * -- advance further than it in both faces.
 * So the clock and the mark are two items in one place, the clock monospaced and the mark
 * proportional, and {@link untimedMark} is the pair's switch. See `elements/mark.ts`.
 */
export const UNTIMED_MARK = '∞';

/**
 * The session clock every surface draws: how long is left while the session is timed, and the unset
 * clock when there is no session.
 *
 * One spelling of a rule that had five, four of which agreed. `zones/bar.ts` was the fifth and drew
 * `clock(sessionTimeLeft())` bare, so a lap race's week of time left reached the bar's six-digit
 * budget as `168:00:00` and WPF took the last glyph off it -- every iRacing lap race, on the default
 * field of the default slot (#439).
 *
 * The untimed session is *not* this expression's business: it draws {@link UNTIMED_MARK} instead,
 * from the item beside this one, and this clock is hidden while that mark is shown. Folding the two
 * states into one placeholder is what the four agreeing surfaces did, and it tells a driver in a
 * thirty-lap race that the dash has no reading where it has one.
 *
 * `hms` and not {@link clock}: `clock`'s own guard is the lower half of `isTimedSession`'s window, so
 * writing both nests the same comparison twice in a binding SimHub evaluates every frame.
 */
export const sessionClock = (): Expr => iff(isTimedSession(), hms(sessionTimeLeft()), str(NO_CLOCK));

/** The mark that replaces the session clock where the session has no clock. */
export const untimedMark = (): Mark => ({ text: UNTIMED_MARK, when: isUntimedSession() });

/**
 * Whether a page shows how much time is left rather than which lap it is, per `SessionProgress`.
 *
 * `time` and `laps` force the answer and `auto` takes it from the session. It lives here rather
 * than beside either drawing, because the card and the module both read it and a setting answered
 * twice is how the two came to disagree over what `auto` means: iRacing's `TotalLaps` is the
 * leader's completed laps in a timed session, so the mode never keys off a lap count.
 */
export const showsTimeLeft = (): Expr => {
  const mode = setting.sessionProgress();
  return ncalc.or(eq(mode, str('time')), and(eq(mode, str('auto')), isTimedSession()));
};
export const opponentCount = (): Expr => isnull(game('OpponentsCount'), num(0));
export const classOpponentCount = (): Expr => isnull(game('PlayerClassOpponentsCount'), num(0));

/** The field size a position is shown out of, per PositionMode. */
export const fieldSize = (): Expr => iff(classMode(), classOpponentCount(), opponentCount());

export const speed = (): Expr => isnull(game('SpeedLocal'), num(0));

/**
 * The unit words the faces draw.
 *
 * SimHub publishes a unit as the name of its enum member, `KMH`, `Liters`, `Kpa`, so a follower
 * bound straight to one reads `kmh` beside a speed and `Liters` beside a fuel load, in a box cut
 * for `km/h` and `L`. The written form is what the canvas draws and what the box was measured
 * against, so the enum is mapped here rather than in each follower.
 */
export const speedUnit = (): Expr => iff(eq(isnull(game('SpeedLocalUnit'), str('KMH')), str('MPH')), str('mph'), str('km/h'));
// Engine speed has one body, `rpms` in `shift.ts`, because the speedo prints it directly above a
// rev bar that reads the same value: two spellings of one expression is how the redline came to
// disagree with the bar it sits on. ADR 0014.
export const rpm = rpms;
// The redline a readout prints is not a value of its own: it is the RPM the rev bar's top band
// lights at, and it lives in `shift.ts` as `redlineRpm` so that the number and the bar cannot
// disagree. ADR 0014. This file used to carry a second body for it, reading SimHub's number
// while the bar beside it read the car's.
export const fuelUnit = (): Expr => iff(eq(isnull(game('FuelUnit'), str('Liters')), str('Gallons')), str('gal'), str('L'));
export const fuel = (): Expr => isnull(game('Fuel'), num(0));
export const fuelPercent = (): Expr => isnull(game('FuelPercent'), num(0));
export const fuelPerLap = (): Expr => isnull(computed('Fuel_LitersPerLap'), num(0));
export const fuelLapsLeft = (): Expr => isnull(computed('Fuel_RemainingLaps'), num(0));
/** The fuel range in seconds, nought when SimHub has none, guarded here for every caller as {@link sessionTimeLeft} is. */
export const fuelTimeLeft = (): Expr => secondsOf(computed('Fuel_RemainingTime'));
export const fuelLastLap = (): Expr => isnull(computed('Fuel_LastLapConsumption'), num(0));
export const fuelThisLap = (): Expr => isnull(computed('Fuel_CurrentLapConsumption'), num(-1));
export const lapsLeft = (): Expr => isnull(game('RemainingLaps'), num(0));

/** Fuel to add: what the laps left will burn, less what is in the tank; never negative. */
export const fuelToAdd = (): Expr => max(num(0), sub(mul(lapsLeft(), fuelPerLap()), fuel()));

/**
 * Whether a fuel figure derived from a lap's consumption means anything yet.
 *
 * **A lap has to have been completed, not merely begun.** SimHub publishes `Fuel_LitersPerLap`
 * before the first crossing by extrapolating the partial lap, so on the out lap it is a number that
 * changes every frame -- large under braking, small on a straight -- and every figure derived from
 * it moves with it: the estimated laps, the refuel and the average itself. Reported from a rig as
 * "really jumpy for the first lap"; the reading is not wrong so much as not yet a reading.
 *
 * So the gate is a completed lap. After one, `Fuel_LitersPerLap` is an average over laps that have
 * finished and it moves only at a crossing, which is the "updated per lap" a driver expects. Before
 * one, the fields draw {@link NO_VALUE} rather than a figure that will not sit still.
 *
 * The estimate is still required to be positive as well. A car whose sim computes no consumption at
 * all has completed laps and nothing to show for them, and that is the same empty box.
 */
export const fuelIsSettled = (): Expr => and(gt(completedLaps(), num(0)), gt(fuelPerLap(), num(0)));

/**
 * Whether the last completed lap's consumption is a reading.
 *
 * Two conditions rather than one, since SimHub publishes `Fuel_LastLapConsumption` as zero in two
 * separate situations: before the first crossing, which is what {@link fuelIsSettled} answers, and
 * after a lap that included a refuelling stop, on which the difference between the two crossings is
 * not a consumption at all. Both of them are an absence, whereas `0.000` drawn on a face reads as a
 * broken sensor. The band's fuel page and the fuel module draw this one property, so they ask this
 * one question of it rather than each guarding it in a way of its own. #382.
 */
export const fuelLastLapIsSettled = (): Expr => and(fuelIsSettled(), gt(fuelLastLap(), num(0)));

/**
 * The fuel range in seconds, or nothing to count until a lap has said what one costs.
 *
 * The gate sits on the seconds rather than around the drawing, which is what keeps one field to one
 * absence. A fuel time is drawn through {@link clock} on the modules and through
 * {@link minutesClock} on band D, and each of those already writes a placeholder of the shape the
 * time has when there is nothing to count; a gate wrapped around the drawing instead would answer
 * `--` before a lap and `-:--:--` after the tank had run dry, two shapes for one absence in one
 * box, and the shorter of the two would move the column the time sits in.
 */
export const settledFuelTimeLeft = (): Expr => iff(fuelIsSettled(), fuelTimeLeft(), num(0));

/** So the timed margin below reads as minutes rather than as a division by a bare 60. */
const SECONDS_PER_MINUTE = 60;

/**
 * Whether there is a race to measure the tank against: a length to run, in a session that is a race.
 *
 * Two questions, and the first one alone is not enough. A length, because a session with no end has
 * no flag for the tank to reach and the subtraction would draw the whole of the range as spare, and
 * the length asked for is the one the page is counting down -- the time in a timed session and the
 * laps remaining in a lap-counted one -- since those are the two terms of the subtraction and either
 * may be missing while the other is there.
 *
 * And a race, because a practice or qualifying session that *does* have a length has an end nobody
 * waves a flag at. A thirty-minute open practice with eight minutes of fuel in the tank read `−22`
 * MIN in the danger red, from the first completed lap to the end of the session, which is exactly the
 * false alarm {@link tankIsLow} refuses this expression for: a low tank in practice is a low tank and
 * nothing more, and the driver is not short of fuel for anything. `Race` is the one session name
 * OpenDash matches with certainty and `lapReviewWanted` in `components/lapReview.ts` already compares
 * it this way, through {@link sessionType} rather than a second spelling of the property. A session
 * whose name the sim does not give reads nothing here, which is the right way to be wrong: an absence
 * says the dash cannot tell, where a red figure says the tank will not make it.
 *
 * `Refuel` is the counter-precedent and stays as it is: it reads the race's remaining laps in
 * practice too, but it is drawn in caution amber as an instruction to the crew rather than as a
 * verdict, so a figure of no use in practice is not a figure that alarms there.
 */
const raceHasAnEnd = (): Expr => and(eq(ucase(sessionType()), str('RACE')), iff(showsTimeLeft(), isTimedSession(), gt(lapsLeft(), num(0))));

/**
 * Whether there is a margin to draw: a lap has said what one costs, and the race has an end.
 *
 * {@link fuelIsSettled} is the gate every other estimate on the fuel page reads, and this is that
 * gate with the session's own added, so the margin appears at the same crossing as the estimate it
 * is derived from rather than a frame before or after it.
 */
export const fuelToEndIsSettled = (): Expr => and(fuelIsSettled(), raceHasAnEnd());

/** The lap-counted form: the range in the tank less the laps the session still has to run. */
const fuelToEndLaps = (): Expr => sub(fuelLapsLeft(), lapsLeft());
/** The timed form, in minutes: the range in the tank less the time the session still has to run. */
const fuelToEndMinutes = (): Expr => div(sub(fuelTimeLeft(), sessionTimeLeft()), num(SECONDS_PER_MINUTE));

/**
 * Fuel to the end of the race, signed: how much more than the rest of the race the tank holds.
 *
 * The one fuel question in a race is whether the tank makes it, and the fuel page had every term of
 * that subtraction on it -- the range, the estimated laps -- while the laps left sat on the session
 * page, so the driver did the arithmetic mid-corner. [ADR 0009](../../../../docs/decisions/0009-does-the-plugin-compute.md)
 * allows a subtraction of two published values in an expression, and this is that subtraction. #387.
 *
 * Two quantities rather than one, chosen the way the session page chooses which counter to draw: a
 * timed session compares `Fuel_RemainingTime` with `SessionTimeLeft` and reads in minutes, a
 * lap-counted one compares `Fuel_RemainingLaps` with `RemainingLaps` and reads in laps. The switch
 * is {@link showsTimeLeft}, so the margin counts in whatever the face beside it is counting in and
 * the driver is never asked to notice that the unit changed for a reason of its own. The unit is
 * drawn beside the number by {@link fuelToEndUnit}, because a bare signed figure that means laps on
 * one grid and minutes on the next is a reading nobody can act on.
 */
export const fuelToEnd = (): Expr => iff(showsTimeLeft(), fuelToEndMinutes(), fuelToEndLaps());

/**
 * The margin as text: `+1.4` laps, `−3` minutes, or {@link NO_VALUE} until there is one.
 *
 * Through {@link signed}, which writes the typographic minus and keeps the `+`: the sign is the
 * whole of what the reading says, so a margin drawn without one would be the estimate again.
 *
 * A tenth of a lap and a whole minute, which is the precision each of the two earns rather than one
 * pattern applied to both. A tenth of a lap is a reading -- half a lap in hand is a stop and a lap
 * and a half is not -- where a tenth of a minute is six seconds of a figure that moves by more than
 * that every corner, and nobody plans a race on it. A box cut for the tenth of a minute as well
 * would be the widest on the page for a digit no driver reads.
 *
 * The width is {@link CHARS.margin} and not the estimate's {@link CHARS.consumption}, which is a
 * correction: `0.0` always writes a sign, the integer digits and a decimal, so the margin on lap one
 * of a 200-lap race is `−169.0`, six cells where the estimate beside it is cut for five, and WPF
 * takes the last glyph off every reading of the first half of any long race. {@link FUEL_TO_END_WIDEST}
 * is what the two boxes carrying it are measured by, since the sample is the narrow end of the range.
 */
export const fuelToEndText = (): Expr =>
  iff(fuelToEndIsSettled(), iff(showsTimeLeft(), signed(fuelToEndMinutes(), '0'), signed(fuelToEndLaps(), '0.0')), str(NO_VALUE));

/**
 * How wide {@link fuelToEndText} draws, for the `laps` or `min` that follows it.
 *
 * Three branches, as the text has three: the absence, the whole minutes and the tenths of a lap.
 * Built from the same two expressions the text is built from, so that the unit cannot be placed for
 * one reading while the figure is the other.
 */
export const fuelToEndDrawn = (): DrawnFigure =>
  drawnEither(
    fuelToEndIsSettled(),
    drawnEither(
      showsTimeLeft(),
      drawnFigure({ value: fuelToEndMinutes(), digits: CHARS.margin.digits - 1, signed: true }),
      drawnFigure({ value: fuelToEndLaps(), digits: CHARS.margin.digits - 2, decimals: 1, signed: true }),
    ),
    drawnText(NO_VALUE),
  );

/**
 * The widest reading the margin can draw, which is what its box is measured against.
 *
 * The lap form, because the decimal costs a cell the minutes do not spend: `−999.9` laps is five
 * digit cells and a special where the longest timed reading, a full day's `−1439` min, is five digit
 * cells and none. Both are inside {@link CHARS.margin}; a `widest` is declared because a monospaced
 * box is cut from its budget and the fit tests measure what the item says it draws, so a field left
 * with `+1.4` on it is a field measured at three cells for a reading that takes six.
 */
export const FUEL_TO_END_WIDEST = `${MINUS}999.9`;

/** What the margin is counted in, which is the unit of whichever term the page is counting down. */
export const fuelToEndUnit = (): Expr => iff(showsTimeLeft(), str('min'), str('laps'));

/** The wider of the two spellings, which every box carrying the unit is measured by. */
export const FUEL_TO_END_UNIT_WIDEST = 'laps';

/**
 * Good while the tank reaches the flag, danger once it does not, and the plain text until it says.
 *
 * The delta greens and reds rather than the fuel page's own amber, because the reading is a verdict
 * on the plan and not a level in a tank: `purpose.fuel.low` is the colour of the tank and the bar
 * under it, and a second field in it would read as a second alarm about the same tank. There is no
 * middle band, unlike {@link deltaColour}: a margin of a tenth of a lap is not a lap in hand, and
 * the question this answers has two answers.
 *
 * Zero counts as reaching the flag, since `RemainingLaps` counts the lap you are on, and the
 * absence is drawn in the primary text: a green `--` claims the tank makes it before anything knows.
 */
export const fuelToEndColour = (): Expr =>
  iff(
    ncalc.not(fuelToEndIsSettled()),
    str(dsTokens.color.text.primary),
    iff(lt(fuelToEnd(), num(0)), str(dsTokens.purpose.delta.slower), str(dsTokens.purpose.delta.faster)),
  );

/**
 * How long the race is expected to run, in laps.
 *
 * `TotalLaps` where the session has one, which is every lap-limited race and no timed one. For a
 * timed race the length is a prediction, and the one SimHub already makes is `RemainingLaps` -- laps
 * done plus laps to come. Adding rather than reading a second property keeps the two forms in one
 * expression and means the number always agrees with the remaining-laps figure beside it.
 */
export const estimatedRaceLaps = (): Expr => iff(gt(totalLaps(), num(0)), totalLaps(), add(completedLaps(), lapsLeft()));

/**
 * `12 / 43`: the lap you are on, out of the race's estimated length.
 *
 * The lap you are *on* and not the lap you have finished, because that is the number a driver says
 * out loud. SimHub's `CurrentLap` is already 1 on the opening lap, so it needs no adjusting.
 *
 * `--` until there is a length to count against. A timed race has none until the sim has an average
 * lap to divide by, and a total of zero drawn as "12 / 0" is worse than saying nothing.
 */
export const lapOfTotal = (): Expr =>
  iff(gt(estimatedRaceLaps(), num(0)), concat(fmt(currentLap(), '0'), str(' / '), fmt(estimatedRaceLaps(), '0')), str(NO_VALUE));

/**
 * The tank under the threshold the driver set, which is the sentence three separate drawings had
 * each written for themselves: band D's fuel telltale, the strip's low-fuel state and the hero's
 * fuel pop-up. ADR 0009 puts a derivation that has reached three items behind one name, and this is
 * that name.
 *
 * The remaining laps default high here and to zero in {@link fuelLapsLeft} above, which is not an
 * oversight: a figure the sim does not compute is drawn as nothing, whereas a warning raised on a
 * missing figure would come on in every car that has no such reading. The threshold itself is
 * `LightsLowFuelLaps` with the box's deprecated name behind it, so one number answers "am I low"
 * for every light and every face.
 *
 * **A tank is only low once the sim knows what a lap costs**, which is {@link fuelIsSettled}.
 * SimHub derives `Fuel_RemainingLaps` from `Fuel_LitersPerLap`, and with no lap yet run it publishes
 * zero rather than null -- so "0.0 laps remaining" is not an empty tank, it is a sim that has not
 * been asked to compute one.
 * Without the consumption gate the warning is on at every idle screen, on the band's telltale, in
 * the fuel pop-up, on the flag box and on every strip at once, which is exactly what a driver
 * sitting in the menus saw in 0.3.0-rc.1. The gate is the one the fuel module already draws its
 * "est. laps" behind, so the number and the warning about it now agree about when there is one.
 *
 * **It stays on the driver's threshold and does not read {@link fuelToEnd}.** #387 raised the
 * question and this is the answer: a warning that came on because the tank will not reach the flag
 * would be a second rule behind one lamp, silent in a practice session where a low tank is still a
 * low tank, and loud on the first lap of a long race where a car is by definition short of fuel for
 * the whole of it. The threshold is a number the driver chose, in laps, and it means the same thing
 * in every session. The margin answers a different question and answers it as a reading on the fuel
 * page and on band D, which is where a strategy is read rather than warned about -- and it draws
 * nothing outside a race for the first of those two reasons, so the pair now agree about practice
 * rather than only this one being right about it.
 */
export const tankIsLow = (): Expr => and(fuelIsSettled(), lt(isnull(computed('Fuel_RemainingLaps'), num(999)), flagBox.lowFuelLaps()));

/**
 * Whether the car is switched on. SimHub normalises it from the sim, so this is one of the few
 * conditions that reaches a package through `GameData` rather than through iRacing's own telemetry.
 *
 * It is written here, with the rest of the telemetry, rather than beside the one drawing that reads
 * it today. That drawing is the flag box, whose answer is a dim standby mark and not a dark panel
 * so that a car switched off is not mistaken for a profile that failed to load; the reasoning is in
 * docs/design/flag-box.md and it is about the box rather than about the property.
 */
export const ignitionOn = (): Expr => game('EngineIgnitionOn');

/**
 * The ignition is off.
 *
 * SimHub's iRacing reader has no ignition of iRacing's to pass through, and derives one: it is on
 * while `Voltage` has been above zero within the last five frames (`IRacingManager` in the decompiled
 * 9.12.6 ICarsReader.dll). That is why anything drawing this on track also needs {@link inTheCar}: a
 * driver standing in the garage has no voltage, and so, to SimHub, an ignition switched off. A reader
 * that overrides nothing gets `GameManagerBase`'s guess instead, `Rpms > 300`, which is an engine at
 * idle rather than a switch.
 *
 * The property is a non-nullable `int` and is never absent while a game runs; the `isnull` default of
 * 1 is for the frames before any game has, when there is no `NewData` to read it from.
 */
export const ignitionOff = (): Expr => eq(isnull(ignitionOn(), num(1)), num(0));

/**
 * The engine is not running: SimHub's `EngineStarted` at 0, which on iRacing is the ignition on and
 * the stalled bit of `EngineWarnings` clear, held over five frames.
 *
 * SimHub's reading rather than the bit, although the bit is iRacing's own, because of what SimHub
 * leaves out. `IRacingManager` ignores the stalled bit on an electric car, on a car whose idle RPM
 * is 0, and on one whose model name says Hybrid -- which is a guess about names, but a guess somebody
 * made because a hybrid raises the bit while it runs. The alert catalogue ranks this above a red
 * flag, and ENGINE OFF over a flag on a car that is driving is the worst false alarm the face could
 * give, so the reading that has already been corrected for it is the one to take.
 *
 * It is also true with the ignition off, which is SimHub's own definition; the pit family and the
 * catalogue both rank the ignition above it, so the one that names the switch is what shows. Here
 * rather than beside a drawing because both read it and have to agree about what an engine that has
 * stopped is.
 */
export const engineStopped = (): Expr => eq(isnull(game('EngineStarted'), num(1)), num(0));

/**
 * The driver is in the car: iRacing's `IsOnTrack`, which is "car on track physics running with
 * player in car". This is the one definition of it, which #312 asks for, so that a second sim is a
 * change to this line.
 *
 * It is the stronger of the two questions #312 tells apart, and {@link inSession} is the weaker: a
 * driver in the garage is in a session and not in the car. Everything that raises itself on a change
 * or on a car state reads it, because in the garage and in the menus those are noise -- a setting
 * that moves while a setup loads, and an ignition that SimHub reads as off whenever there is no
 * voltage, which is whenever nobody is driving.
 *
 * Read as a boolean and not compared with a number. A raw telemetry boolean reaches a binding as
 * `true` or `false`, where `GameData`'s booleans arrive as 1 and 0 -- the committed traces show both
 * forms side by side -- so `= 1` would never hold and the gate would silence everything behind it.
 * It is the same reading `carAvailable` uses for the leaderboard's rows.
 *
 * **What a sim that does not say gets is the caller's choice**, because the two callers want opposite
 * answers. A change notification is harmless if it fires in a sim with no `IsOnTrack`, so it passes
 * `true` and keeps showing there. The alert catalogue does not: its car alerts outrank every flag, and
 * off iRacing SimHub's ignition is `Rpms > 300`, so a sim that published no `IsOnTrack` would put
 * IGNITION OFF over the band whenever the engine idled. It passes `false`, and is iRacing's, as the
 * flags already are.
 */
export const inTheCar = (ifUnknown = true): Expr => isnull(raw('IsOnTrack'), ifUnknown ? 'true' : 'false');

export const throttle = (): Expr => isnull(game('Throttle'), num(0));
export const brake = (): Expr => isnull(game('Brake'), num(0));
export const clutch = (): Expr => isnull(game('Clutch'), num(0));
export const steering = (): Expr => isnull(raw('SteeringWheelAngle'), num(0));
/** Radians of wheel angle either side of centre a full-lock reading is drawn to. */
export const STEERING_RANGE = 3.5;
export const brakeBias = (): Expr => isnull(game('BrakeBias'), num(0));
export const tcLevel = (): Expr => game('TCLevel');
export const absLevel = (): Expr => game('ABSLevel');
export const fuelMixture = (): Expr => raw('dcFuelMixture');
export const antiRollFront = (): Expr => raw('dcAntiRollFront');
export const antiRollRear = (): Expr => raw('dcAntiRollRear');

export const airTemperature = (): Expr => isnull(game('AirTemperature'), num(0));
export const roadTemperature = (): Expr => isnull(game('RoadTemperature'), num(0));
export const trackName = (): Expr => isnull(game('TrackName'), str(''));
export const trackLengthKm = (): Expr => div(isnull(game('TrackLength'), num(0)), num(1000));
/** The widest word the grip status takes, which is what a field measures its box against. */
export const GRIP_WIDEST = 'Moderate';
/** The track's grip, in the words the sim writes it in; `--` where the sim reports none. */
export const trackGrip = (): Expr => isnull(game('TrackGripStatus'), str(NO_VALUE));
export const sessionType = (): Expr => isnull(game('SessionTypeName'), str(''));

/**
 * Whether SimHub is connected to a running game.
 *
 * `DataCorePlugin.GameRunning` is an `AttachedProperty<int>` and not a bool -- 1 or 0, written on
 * every `DataUpdate` from `data.GameRunning` -- so it is compared rather than used as a term.
 * Verified in the decompiled 9.12.6 `DataCorePlugin.DeclareProperties`, which attaches it under
 * that exact name, and `DataCorePlugin.DataUpdate`, which sets it.
 */
export const gameRunning = (): Expr => gt(isnull(prop('DataCorePlugin.GameRunning'), num(0)), num(0));

/**
 * Whether there is a session to draw: the game is running and has named one.
 *
 * This is the one definition of being in a session, and every module that needs one cites it
 * (#406). It is deliberately the weaker of the two questions #312 tells apart: a driver in the
 * garage is in a session and gets the timing screens, since the leaderboard and the relative are
 * filled from the session before the car is on track. Being *in the car* is the other question,
 * which the alerts need and this one does not answer.
 *
 * **Both halves are needed, and the game half is the one that is easy to drop.** `GameData.*` is
 * declared once over a single `StatusDataBase` that `DataCorePlugin.DataUpdate` only reassigns
 * `if (data.GameRunning)`, so after a game quits the whole block keeps the last session's values:
 * `SessionTypeName` alone would read `Race` on a desktop with nothing running. Tested the other way
 * round, `GameRunning` alone is true through the few seconds iRacing spends loading a session, when
 * the name is still empty and there is nothing to draw.
 *
 * Read from iRacing alone, and kept here so that a second sim is a change to one line -- the
 * per-game mapping block #312 opened was closed on 2026-09-22.
 *
 * **It is false with no game running too, but that state belongs to the idle screen now (#763).**
 * The racing face, the companion's module screens and all four pit wall pages declare `idle: false`
 * and every package ends with an idle screen of its own, so a rig with nothing running shows the
 * wordmark and a clock rather than a page of notices. The two constructors still setting `idle: true`
 * -- `pageScreen` and `cardScreens` -- build the dashboards a widget embeds, whose screen is chosen by
 * a bound `InitialScreenIndex` rather than by SimHub's idle selection.
 *
 * So the state this is for is the narrow one it was written for: the game running and no session
 * named. `tools/irsdk-emulator/scenarios/nosession.json` exists because it is the only way to reach
 * it -- stopping the emulator makes `gameRunning()` false and reaches the idle screen instead. See the
 * "no session yet" section of docs/second-screens.md.
 */
export const inSession = (): Expr => and(gameRunning(), ne(sessionType(), str('')));
export const carModel = (): Expr => isnull(game('CarModel'), str(''));
export const playerClass = (): Expr => isnull(game('CarClass'), str(''));

/** Wind speed in km/h, from the raw metres per second iRacing publishes. */
export const windKmh = (): Expr => mul(isnull(raw('WindVel'), num(0)), num(3.6));
/** Wind direction in degrees, from radians. */
export const windDegrees = (): Expr => mul(isnull(raw('WindDir'), num(0)), num(180 / Math.PI));
/** The sim's own clock, as seconds of day. */
export const simTimeOfDay = (): Expr => isnull(raw('SessionTimeOfDay'), num(0));

/** Whether the rig writes its clocks to twelve hours, which is `ClockFormat`. #324. */
export const twelveHour = (): Expr => setting.clockFormatIs('12h');

/** The two words a twelve-hour clock follows its digits with, in the case they are drawn in. */
export const MERIDIEMS = ['AM', 'PM'] as const;

/**
 * Whichever of {@link MERIDIEMS} is wider in `face`, which is what a box for the word is measured by.
 *
 * Measured rather than chosen, because the answer is the face's: the two share an `M` and differ
 * by an `A` against a `P`, and which of those is wider is a fact of the font a label and a value
 * do not have to agree on.
 */
export const meridiemWidest = (face: MeasuredFace): string =>
  MERIDIEMS.reduce((a, b) => (measureText(face, b, 100) > measureText(face, a, 100) ? b : a));

/**
 * A clock of the day as a surface draws it: the digits, the word after them, and how wide the
 * digits really are. #324.
 *
 * `text` is `14:32` on a twenty-four-hour clock and `2:32` on a twelve-hour one -- `HH:mm` against
 * `h:mm`, the twelve-hour hour carrying no leading zero because that is how it is read -- and either
 * fits {@link CHARS.timeOfDay}. `meridiem` is `AM` or `PM` whatever the setting, and a surface draws
 * it only while {@link twelveHour} holds: a word beside the value rather than part of it, because
 * `M` is one of the glyphs rule 19 keeps out of a cell. `drawn` is where a word after the digits
 * goes, since a twelve-hour hour is one digit or two and `9:05` placed as if it were `12:05` would
 * stand its `PM` a cell off the figure -- the fault #387 removed from every other follower.
 */
export interface TimeOfDay {
  text: Expr;
  meridiem: Expr;
  drawn: DrawnFigure;
}

/** The hours `from` to `to` as `HH` and `hh` write them, as the literals `in` compares against. */
const writtenHours = (from: number, to: number): Expr[] => Array.from({ length: to - from + 1 }, (_, i) => str(String(from + i).padStart(2, '0')));

/** How wide a clock's digits are: `14:32` always, and `12:05` or `9:05` on a twelve-hour clock. */
const timeOfDayDrawn = (twoDigitHour: Expr): DrawnFigure =>
  drawnEither(twelveHour(), drawnEither(twoDigitHour, drawnText('12:00'), drawnText('9:00')), drawnText('00:00'));

/**
 * The wall clock, from SimHub's own date property.
 *
 * The hour is asked of the formatted text rather than of a number, there being no function that
 * takes a number out of a date: `HH` for the afternoon and `hh` for a two-digit twelve-hour hour,
 * each matched against its written hours by `in`, which compares two strings as strings. SimHub sets
 * its culture to en-US at startup (`SimHubWPF.exe`, decompiled from 9.12.6), so the separator a
 * format writes is always the colon; `tt` would name the meridiem as well, and is not used, because
 * a literal is a word the fit tests can read and a culture's designator is not.
 */
export const localClock = (): TimeOfDay => {
  const now = prop('DataCorePlugin.CurrentDateTime');
  return {
    text: iff(twelveHour(), fmt(now, 'h:mm'), fmt(now, 'HH:mm')),
    meridiem: iff(isIn(fmt(now, 'HH'), ...writtenHours(12, 23)), str('PM'), str('AM')),
    drawn: timeOfDayDrawn(isIn(fmt(now, 'hh'), ...writtenHours(10, 12))),
  };
};

/**
 * The sim's clock, from its seconds of day.
 *
 * Arithmetic rather than a format, the time of day being a number of seconds: the hour is taken
 * modulo 24, so a session clock that reaches midnight reads `00:00` rather than `24:00`, and the
 * twelve-hour hour is that hour moved onto 1 to 12, midnight and noon both reading 12.
 */
export const simClock = (): TimeOfDay => {
  const seconds = simTimeOfDay();
  const hour = mod(truncate(div(seconds, num(3600))), num(24));
  const hour12 = add(mod(add(hour, num(11)), num(12)), num(1));
  const minutes = fmt(truncate(div(mod(seconds, num(3600)), num(60))), '00');
  return {
    text: iff(twelveHour(), concat(fmt(hour12, '0'), str(':'), minutes), concat(fmt(hour, '00'), str(':'), minutes)),
    meridiem: iff(ge(hour, num(12)), str('PM'), str('AM')),
    drawn: timeOfDayDrawn(ge(hour12, num(10))),
  };
};

/** Incidents taken, which iRacing publishes raw and other sims do not publish at all. */
export const incidents = (): Expr => raw('PlayerCarMyIncidentCount');
/** The incident limit, a string in iRacing's session YAML ("unlimited" or a number). */
export const incidentLimit = (): Expr => prop('DataCorePlugin.GameRawData.SessionData.WeekendInfo.WeekendOptions.IncidentLimit');
/** The session has a limit to count incidents against: one is published and it is not "unlimited". */
export const hasIncidentLimit = (): Expr => and(not(isNull(incidentLimit())), ne(incidentLimit(), str('unlimited')));

// --- Tyres ------------------------------------------------------------------------------------

export const CORNERS = ['FrontLeft', 'FrontRight', 'RearLeft', 'RearRight'] as const;
export type Corner = (typeof CORNERS)[number];

/** Which iRacing pit-service flag belongs to a corner, for the "changed at the next stop" tick. */
export const CORNER_PIT_FLAGS: Record<Corner, string> = {
  FrontLeft: 'dpLFTireChange',
  FrontRight: 'dpRFTireChange',
  RearLeft: 'dpLRTireChange',
  RearRight: 'dpRRTireChange',
};

export const tyreTemperature = (corner: Corner): Expr => isnull(game(`TyreTemperature${corner}`), num(0));
export const tyrePressure = (corner: Corner): Expr => isnull(game(`TyrePressure${corner}`), num(0));
/** Wear is reported as the percentage of tread left. */
export const tyreWear = (corner: Corner): Expr => isnull(game(`TyreWear${corner}`), num(0));

/** iRacing names a corner side first in its raw telemetry, `LF` where SimHub says `FrontLeft`. */
const CORNER_PREFIX: Record<Corner, string> = { FrontLeft: 'LF', FrontRight: 'RF', RearLeft: 'LR', RearRight: 'RR' };

/**
 * The worst of a tyre's three tread sections, as a percentage.
 *
 * SimHub's own `TyreWear` is one figure for the corner, and a tyre is done when its most worn
 * section is, not when its average is: a tyre worn on the inside shoulder and untouched elsewhere
 * reads healthy as one figure. iRacing publishes the three sections as fractions of tread left, so
 * the minimum of them is the figure the face draws; where they are absent, which is every sim but
 * iRacing, the single figure is what there is.
 */
export const tyreWearMin = (corner: Corner): Expr => {
  const section = (across: 'L' | 'M' | 'R'): Expr => raw(`${CORNER_PREFIX[corner]}wear${across}`);
  return iff(ncalc.isNull(section('M')), tyreWear(corner), mul(min(section('L'), min(section('M'), section('R'))), num(100)));
};

export const tyreChangeScheduled = (corner: Corner): Expr => gt(isnull(raw(CORNER_PIT_FLAGS[corner]), num(0)), num(0));
export const temperatureUnit = (): Expr => isnull(game('TemperatureUnit'), str('Celcius'));
/**
 * `°C`, `°F` or `K`, which is the temperature unit as a reading draws it rather than as SimHub
 * spells it: `TemperatureUnit` publishes the enum name, `Celcius` included with its own spelling,
 * and a driver reads the mark and not the enum. Kelvin takes no degree sign.
 *
 * Every temperature SimHub publishes is already converted to whichever of the three the driver has
 * set, so a page drawing `TyreTemperature*` follows this and a page reading iRacing's raw telemetry
 * does not: `LFtempCM` is degrees Celsius whatever the setting says, which is why band D's tyre
 * page writes its own `°C` rather than asking here.
 */
export const temperatureMark = (): Expr => {
  const unit = temperatureUnit();
  return iff(eq(unit, str('Fahrenheit')), str('°F'), iff(eq(unit, str('Kelvin')), str('K'), str('°C')));
};
/** `Psi`, `Kpa` or `Bar`, written the way the tyre pages draw it. */
export const pressureUnit = (): Expr => {
  const unit = isnull(game('TyrePressureUnit'), str('Psi'));
  return iff(eq(unit, str('Kpa')), str('kPa'), iff(eq(unit, str('Bar')), str('bar'), str('psi')));
};

// --- Pit service (iRacing's raw black box) ------------------------------------------------------

/**
 * The PitSvFlags bit field. iRacing's public header gives LF 1, RF 2, LR 4, RR 8, fuel 16,
 * tear-off 32, fast repair 64; NCalc has no bitwise operators, so a bit is read by dividing and
 * taking the remainder.
 */
export const PIT_SERVICE_BITS = { FrontLeft: 1, FrontRight: 2, RearLeft: 4, RearRight: 8, fuel: 16, tearOff: 32, fastRepair: 64 } as const;

/** One bit of the field as a number, 1 or 0, which is what a count of corners is added from. */
const pitServiceBit = (bit: number): Expr => mod(truncate(div(isnull(raw('PitSvFlags'), num(0)), num(bit))), num(2));

export const pitServiceFlag = (bit: number): Expr => gt(pitServiceBit(bit), num(0));
export const pitRefuelLitres = (): Expr => raw('PitSvFuel');
export const isInPitLane = (): Expr => gt(isnull(game('IsInPitLane'), num(0)), num(0));
export const inPitSeconds = (): Expr => isnull(game('IsInPitSince'), num(0));
export const lastPitDuration = (): Expr => isnull(game('LastPitStopDuration'), num(0));

/**
 * The words the catalogue writes beside `Tyres`, longest first is not the order: this is the set,
 * and what a box is measured by is whichever of them is widest.
 *
 * Four bits give sixteen selections and the drawing names six, so the ladder below answers the
 * pairs a driver asks for by name and calls the ten that are left `Some`. Which corners those are
 * is what the four corner toggles drawn beside the summary still say.
 */
export const TYRE_SELECTIONS: readonly string[] = ['None', 'All', 'Fronts', 'Rears', 'Lefts', 'Rights', 'Some'];

/** Which corners the next stop changes, as the one word the catalogue writes beside `Tyres`. */
export const pitTyreSelection = (): Expr => {
  const fronts = add(pitServiceBit(PIT_SERVICE_BITS.FrontLeft), pitServiceBit(PIT_SERVICE_BITS.FrontRight));
  const rears = add(pitServiceBit(PIT_SERVICE_BITS.RearLeft), pitServiceBit(PIT_SERVICE_BITS.RearRight));
  const lefts = add(pitServiceBit(PIT_SERVICE_BITS.FrontLeft), pitServiceBit(PIT_SERVICE_BITS.RearLeft));
  const rights = add(pitServiceBit(PIT_SERVICE_BITS.FrontRight), pitServiceBit(PIT_SERVICE_BITS.RearRight));
  const both = (pair: Expr, other: Expr): Expr => and(eq(pair, num(2)), eq(other, num(0)));
  return iff(
    eq(add(fronts, rears), num(0)),
    str('None'),
    iff(
      eq(add(fronts, rears), num(4)),
      str('All'),
      iff(
        both(fronts, rears),
        str('Fronts'),
        iff(both(rears, fronts), str('Rears'), iff(both(lefts, rights), str('Lefts'), iff(both(rights, lefts), str('Rights'), str('Some')))),
      ),
    ),
  );
};

/**
 * How far through the stop the crew is, as a percentage.
 *
 * SimHub publishes no service progress at all, so this is an estimate and not a reading: the
 * seconds since the car entered the box over the duration of the last stop, which is the only
 * figure the game gives for how long a stop takes. It reads nothing outside the lane and before a
 * first stop has been timed, which is honest about a number that is not there rather than showing
 * a quantity from another page.
 */
export const pitServiceProgress = (): Expr =>
  iff(and(isInPitLane(), gt(lastPitDuration(), num(0))), min(num(100), mul(num(100), div(inPitSeconds(), lastPitDuration()))), num(0));

// --- Lap history and deltas ---------------------------------------------------------------------

export const PREVIOUS_LAP_SLOTS = 10;
/** `PersistantTrackerPlugin.PreviousLap_NN`, addressed by the row's repeat index. */
export const previousLap = (slotExpr: Expr): Expr => propByName(concat(str('PersistantTrackerPlugin.PreviousLap_'), fmt(slotExpr, '00')));
/** The same lap's delta to the session best, in seconds. */
export const previousLapDelta = (slotExpr: Expr): Expr =>
  propByName(concat(str('PersistantTrackerPlugin.PreviousLap_'), fmt(slotExpr, '00'), str('_DeltaToSessionBest')));

/**
 * A signed delta to `places` decimals where `chars` holds it, and to fewer where it does not:
 * `+1.03`, `+123.5` and `+1234` in {@link CHARS.delta}'s five cells at two places.
 *
 * A box cannot change its cells at runtime, so a delta whose whole part grows past what its cells
 * were cut for loses its last digit to WPF instead, and `+123.45` in five cells is drawn `+123.4`,
 * a figure the sim never published. The lap that carried a stop, a tow or a repair is a minute or
 * three off the session best and off the lap before it, and its sectors with it, so the deltas that
 * are cut for an ordinary lap meet that reading once a stint, on the lap they are most looked at
 * (#886). Each place given up buys a whole digit, which is the trade
 * {@link referenceDeltaText} already makes past 100 s: a figure rounded to the tenth is still the
 * figure, and a hundredth of a lap two minutes off is not what anybody is reading it for.
 *
 * The sign takes a digit cell and the point a narrow one, so `places` decimals leave
 * `chars.digits - 1 - places` whole digits. Each edge is half a unit of the last place short of the
 * next power of ten, under which .NET still rounds to the shorter figure: 99.995 to two places is
 * `100.00`, a sixth digit. Past the last edge the figure is whole seconds, which five cells hold to
 * 9999 s, the better part of three hours. The choice is an `if` around literal patterns for the
 * reason {@link referenceDeltaText} gives.
 */
export const signedToFit = (seconds: Expr, chars: Chars, places: number): Expr => {
  const patternOf = (p: number): string => (p === 0 ? '0' : `0.${'0'.repeat(p)}`);
  let text: Expr = signed(seconds, '0');
  for (let p = 1; p <= places; p++) {
    const whole = chars.digits - 1 - p;
    if (whole < 1) throw new Error(`signedToFit: ${chars.digits} digit cells hold no whole digit beside a sign and ${p} places`);
    text = iff(lt(abs(seconds), num(Number((10 ** whole - 0.5 * 10 ** -p).toFixed(p + 1)))), signed(seconds, patternOf(p)), text);
  }
  return text;
};

/**
 * The widest reading {@link signedToFit} draws in {@link CHARS.delta} at two places, which is what a
 * box drawing one declares as its `widest`: the true minus and four digits around a point. The
 * samples stay the canvas's `+1.03` and `−0.21`, the short end of the range.
 */
export const DELTA_WIDEST = `${MINUS}99.99`;

/**
 * The same budget for the lap history's column, which draws {@link signedToFit} to three places:
 * `−9.999` is as long as {@link DELTA_WIDEST} in every face, since both are five digit cells and a
 * point, but it has the column's own shape. The AiM theme ghosts a reading's widest cell for cell,
 * and a ghost of `888.88` behind `+0.594` puts the point a cell off its ghost on every row.
 */
export const HISTORY_DELTA_WIDEST = `${MINUS}9.999`;

/** How many laps the rolling average covers, and the number the field is named after. */
export const AVERAGE_LAPS = 5;

/**
 * The mean of the last five laps, `m:ss.mmm`, or the no-data placeholder until there are five.
 *
 * The plugin computes nothing (ADR 0009), so the average is the expression: five lap-history
 * slots read as seconds, summed and divided. A slot that has not been driven yet holds `00:00:00`
 * rather than null, which would average in as a nought and read as a lap two seconds quicker than
 * anything on the track, so every one of the five is required to hold a time before any of them
 * is shown.
 */
export const average5 = (): Expr => {
  // From slot zero, which is the lap just completed. Starting at one averaged laps two to six and
  // left the newest out, so the number moved a lap late; `modules/lapHistory.ts` reads the same
  // slots and says so where it draws row one.
  const slots = Array.from({ length: AVERAGE_LAPS }, (_, i) => previousLap(num(i)));
  const seconds = slots.map((slot) => secondsOf(slot));
  return iff(
    and(...slots.map((slot) => hasTime(slot))),
    toShortTime(secondsToTimespan(div(add(...seconds), num(AVERAGE_LAPS))), 3, false, true),
    str(NO_TIME),
  );
};

export const bestLap = (): Expr => game('BestLapTime');
export const lastLap = (): Expr => game('LastLapTime');
export const estimatedLap = (): Expr => prop('PersistantTrackerPlugin.EstimatedLapTime');
export const sessionBestDelta = (): Expr => isnull(prop('PersistantTrackerPlugin.SessionBestLiveDeltaSeconds'), num(0));
export const allTimeBestDelta = (): Expr => isnull(prop('PersistantTrackerPlugin.AllTimeBestLiveDeltaSeconds'), num(0));

/** The raw telemetry field that carries iRacing's live delta to the last lap, spelt as iRacing spells it. */
export const LAST_LAP_DELTA = 'LapDeltaToSessionLastlLap';

/**
 * The live delta to the lap before this one, which is iRacing's own reading and not SimHub's.
 *
 * SimHub's lap tracker publishes a live delta to the session best and to the all-time best and to
 * nothing else; its `*LastLapDelta` properties are the finished lap's, written once at the line, and
 * say nothing while the next lap runs. iRacing publishes the running comparison itself, as
 * `LapDeltaToSessionLastlLap` (the second `l` is iRacing's), with `_OK` saying whether it has a lap
 * to compare against. Reading it here is what ADR 0009 asks for: a published property read in the
 * expression, where computing it would need the previous lap kept by distance, which is memory
 * between frames.
 *
 * Gated on `_OK` and zero without it, which is what the other two references draw when they have no
 * lap to compare against: SimHub publishes 0 there. The flag's fallback is the bare literal `false`
 * and not the string, because a raw telemetry boolean arrives as `true` or `false` (see `inTheCar`
 * in `components/changeNotification.ts`), so a sim that publishes neither field, or no telemetry at
 * all, reads a level delta rather than an error.
 *
 * Multiplied by `1.0` to make it a double. iRacing publishes the reading as an irsdk_float, and it
 * reaches the binding as a boxed System.Single: SimHub's raw telemetry passes it through unchanged,
 * and so do `if` and `isnull`. SimHub's `format(v, pattern, true)` writes its `+` only for a double,
 * a decimal or an int, so without the promotion a slower or level last-lap delta would lose its sign,
 * and the delta page's caption, placed by {@link referenceDeltaDrawn} with the sign's cell counted,
 * would stand a cell right of the figure. The literal is `1.0` and not `num(1)`: NCalc reads `1` as
 * an Int32, and a Single times an Int32 is still a Single, where a Single times the double `1.0` is a
 * double. The colour never needed it, since `abs` and `<` promote the value themselves.
 */
export const lastLapDelta = (): Expr =>
  iff(isnull(raw(`${LAST_LAP_DELTA}_OK`), 'false'), mul(isnull(raw(LAST_LAP_DELTA), num(0)), '1.0'), num(0));

/**
 * The delta every screen draws, per the plugin's DeltaReference: the one reading behind the card, the
 * delta module, Lap times, the lap pop-up and the pit wall, so no two of them can compare against
 * different laps. An unknown value reads the session best, which is the default.
 */
export const referenceDelta = (): Expr =>
  iff(setting.deltaReferenceIs('alltime'), allTimeBestDelta(), iff(setting.deltaReferenceIs('lastlap'), lastLapDelta(), sessionBestDelta()));

/**
 * The label that says which reference the delta is against.
 *
 * "vs last lap" rather than "vs previous": the lap review already uses "vs previous" for a finished
 * lap against the one before it, which is a different comparison drawn at a different moment.
 */
export const referenceLabel = (): Expr =>
  iff(setting.deltaReferenceIs('alltime'), str('vs all-time best'), iff(setting.deltaReferenceIs('lastlap'), str('vs last lap'), str('vs session best')));

/**
 * The longest caption {@link referenceLabel} can produce, which is what a box that draws it is
 * measured by. Written once here, beside the binding, because the delta module and the pit wall
 * both measure by it and a copy in each is how the two would come to disagree with the binding.
 */
export const REFERENCE_LABEL_WIDEST = 'vs all-time best';

/** Whether the live delta is drawn to thousandths rather than to the default hundredths. */
const inThousandths = (): Expr => setting.deltaPrecisionIs('thousandths');

/**
 * Whether this reading is drawn to three places: thousandths chosen, and a figure short enough to hold
 * them. A delta of 100 s or more (a long stop, or the lap after one against the last lap) would need a
 * seventh cell at three places, so it is drawn to hundredths, which `+100.00` fits. The edge is the
 * half under which .NET still rounds to two whole digits: 99.9995 to three places is `100.000`.
 */
const drawnToThousandths = (seconds: Expr): Expr => and(inThousandths(), lt(abs(seconds), num(99.9995)));

/**
 * The live delta to the reference as it is drawn: signed, with a true minus, to the places the
 * precision setting asks for, except that a delta of 100 s or more is drawn to hundredths at either
 * setting, since three places would take it past the box ({@link drawnToThousandths}). A level delta,
 * inside {@link referenceDeltaBand}, is the bare `0.00` the canvas draws, or `0.000` at thousandths.
 *
 * One helper for the five surfaces that draw it -- card 3, the delta page, Lap times, the lap pop-up
 * and the pit wall's Lap delta panel -- so that no two of them can draw one reading to different
 * places. The sector deltas, the lap review's two deltas and the lap history's column are other
 * comparisons with formats of their own and do not come through here.
 *
 * The level branch is here and not on the card, where it used to be: card 3 drew `0.00` and the four
 * other surfaces drew `+0.00`, or `−0.00` for a reading a thousandth under, so one reading looked
 * like two (#614). A forced sign on a figure the band calls level is a plus or a minus drawn in the
 * resting white, which is a direction the colour has just said there is not. The band is the one
 * {@link referenceDeltaColour} reads, so the figure and its colour cannot disagree about it.
 *
 * The choice is an `if` around two formats rather than one format with a bound pattern. `signed`
 * writes its pattern as a string literal, and a pattern that was itself an expression would be
 * emitted as the text of that expression; `format` has never been verified with anything but a
 * literal pattern either. #322.
 */
export const referenceDeltaText = (seconds: Expr): Expr =>
  iff(
    referenceDeltaLevel(seconds),
    iff(inThousandths(), str('0.000'), str('0.00')),
    iff(drawnToThousandths(seconds), signed(seconds, '0.000'), signed(seconds, '0.00')),
  );

/**
 * The longest reading {@link referenceDeltaText} is budgeted for, which is what every box that draws
 * it declares as its `widest`: two whole digits and three places, the true minus taking a digit
 * cell. The same six cells hold three whole digits and two places, which is how a delta of 100 s or
 * more is drawn, so every reading under 1000 s fits. The samples stay the canvas's `−0.21`, which is
 * the short end of the range; this is what the fit tests measure instead, since a monospaced box is
 * measured by what it declares rather than by its budget.
 */
export const REFERENCE_DELTA_WIDEST = `${MINUS}12.345`;

/**
 * Half a unit of the last place the live delta is drawn to: 0.005 at hundredths, 0.0005 at
 * thousandths.
 *
 * The band inside which a delta is level exists because the number drawn does not carry anything
 * finer. At hundredths a delta of four thousandths is drawn `+0.00`, and colouring it as slower is a
 * claim the figure does not make; at thousandths the same delta is drawn `+0.004`, and the figure
 * does make it, so the band narrows with the precision. A band that stayed at 0.005 would draw
 * `+0.004` in the resting white, which is a thousandths setting showing a thousandth and then
 * disowning it.
 */
export const referenceDeltaBand = (): Expr => iff(inThousandths(), num(0.0005), num(0.005));

/**
 * Whether the live delta is level: inside {@link referenceDeltaBand}, which is exactly the readings
 * {@link referenceDeltaText} draws as a zero.
 *
 * Strictly inside. .NET rounds a half away from zero, so 0.005 at hundredths is drawn `+0.01` and a
 * band that took it in would colour a `+0.01` as level. The lap history's column, drawn to three
 * places, calls a lap the session best by the same strict edge, `< 0.0005`.
 */
export const referenceDeltaLevel = (seconds: Expr): Expr => lt(abs(seconds), referenceDeltaBand());

/**
 * The colour of the live delta: level inside the band, then faster below zero and slower above it.
 *
 * One predicate with the text rather than {@link deltaColour}'s own band, so the figure and its colour
 * cannot disagree at either precision: a figure drawn as a zero is white and a figure drawn as
 * anything else is green or red. {@link referenceDeltaText} draws its bare `0.00` off the same
 * predicate.
 */
export const referenceDeltaColour = (seconds: Expr): Expr =>
  iff(referenceDeltaLevel(seconds), str(dsColour.zero), iff(lt(seconds, num(0)), str(dsColour.faster), str(dsColour.slower)));

/**
 * How wide the live delta really draws, for the caption that follows it: the places the setting asks
 * for, and as many whole digits as the budget leaves once the sign and those places are taken. A
 * caption placed at the end of the three-place budget would stand a cell off a two-place figure.
 *
 * The whole digits are counted per precision rather than written as two, because the six cells hold
 * three whole digits at hundredths: a long stop in the pits can take the delta past a hundred
 * seconds, `+100.00` fits the box, and a caption placed for two digits would sit on its last one.
 * A delta that long is drawn to hundredths at either setting, so the caption asks the same question
 * of the reading that {@link referenceDeltaText} does. So does a level delta, which is drawn with no
 * sign and so a cell shorter than a signed zero: a caption placed for the sign would stand a cell off
 * the `0.00` it follows.
 */
export const referenceDeltaDrawn = (seconds: Expr): DrawnFigure => {
  const figure = (decimals: number): DrawnFigure =>
    drawnFigure({ value: seconds, digits: CHARS.referenceDelta.digits - 1 - decimals, decimals, signed: true });
  return drawnEither(
    referenceDeltaLevel(seconds),
    drawnEither(inThousandths(), drawnText('0.000'), drawnText('0.00')),
    drawnEither(drawnToThousandths(seconds), figure(3), figure(2)),
  );
};

export const sectorLast = (sector: number): Expr => game(`Sector${sector}LastLapTime`);
export const sectorBest = (sector: number): Expr => game(`Sector${sector}BestTime`);

/** True when a sector of the last lap beat your own best of that sector. */
export const sectorImproved = (sector: number): Expr =>
  and(hasTime(sectorLast(sector)), hasTime(sectorBest(sector)), lt(secondsOf(sectorLast(sector)), secondsOf(sectorBest(sector))));

/** The signed difference between a sector of the last lap and your best of that sector. */
export const sectorDelta = (sector: number): Expr =>
  sub(secondsOf(sectorLast(sector)), secondsOf(sectorBest(sector)));

/** Absolute seconds, for a gain-or-loss bar that only knows how far it is from zero. */
export const magnitude = (expr: Expr): Expr => abs(expr);

/**
 * Deltas this close to zero are drawn as neither faster nor slower, as on the dash's delta card.
 *
 * The canvas states a two-colour rule for the lap review's own deltas -- red when slower, green when
 * faster -- and the code has three states here, the third being `purpose.delta.zero` inside this
 * band. The three are kept, and for a finished lap as well as for a live one. A lap that came in
 * four thousandths off the session best is drawn `+0.00`, which is not a lap that was slower, and
 * colouring it as though it were is a claim the number does not carry. The difference from the
 * canvas is recorded here rather than resolved by a second rule.
 *
 * The lap review's two deltas are what {@link deltaColour} colours. The live delta to the reference
 * has a band of its own, {@link referenceDeltaBand}, which narrows when the delta is drawn to
 * thousandths, and the lap history's column and the sectors colour by rules of their own. What the
 * reference's band shares with this one is the edge: half a unit of the last place drawn, and
 * strictly inside it.
 */
export const DELTA_DEADBAND = 0.005;

/**
 * Green when faster, red when slower, white within the deadband.
 *
 * Strictly within. .NET rounds a half away from zero, so 0.005 to two places is `+0.01` and −0.005
 * is `−0.01`, and a band that took its own edge in coloured those figures as level. Every figure
 * drawn as a zero is white and every other is green or red, which is {@link referenceDeltaLevel}'s
 * rule at hundredths (#614).
 */
export const deltaColour = (seconds: Expr): Expr =>
  iff(le(seconds, num(-DELTA_DEADBAND)), str(dsColour.faster), iff(ge(seconds, num(DELTA_DEADBAND)), str(dsColour.slower), str(dsColour.zero)));

/** The delta colours, named so the expression above reads as a sentence. */
const dsColour = { faster: dsTokens.purpose.delta.faster, slower: dsTokens.purpose.delta.slower, zero: dsTokens.purpose.delta.zero };
