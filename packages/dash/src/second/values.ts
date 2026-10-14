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
import { ELLIPSIS, measureText, widestFigure, type MeasuredFace } from '../design/advances.ts';
import { DATA_FACE, MINUS, type Chars, type DataWeight } from '../design/metrics.ts';
import type { Mark } from '../elements/mark.ts';
import { CAR_HAS_PIT_LIMITER, CLASS_BEST_LAP, DAMAGE_REPORTED, DRIVER_IN_CAR, flagBox, LIVE_CLASS_ORDER, LIVE_CLASS_POSITION, LIVE_ORDER, LIVE_POSITION, PIT_LIMITER_ON, propertyName, setting } from '../contract.ts';
import { chipText } from './chip.ts';
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
  real,
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
  ceiling,
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

/**
 * The places a lap of ten minutes or more is drawn to, which is one fewer than a shorter lap's.
 *
 * `toshorttime` does not pad the minutes, so a lap that reaches ten minutes gains a digit: the
 * Nordschleife's 24h and Tourist layouts lap in ten to eleven minutes in the MX-5 Cup, the Legends
 * and the Cross Car, the three cars the AiM theme was built for, and `10:30.123` is seven digits in
 * a box cut for the six of `1:42.905`. WPF clips at the box, so the lap pop-up, the lap review and
 * every other lap-time box drew `10:30.12` and a sliver of the last digit (#883).
 *
 * The lap drops its thousandth rather than every box gaining a cell, which is the trade
 * {@link sectorTime} makes for the same track. A seventh cell is wider than a rung L card's slot at
 * 64 px, and on the faces and the pit wall it costs columns on every lap of every track: the
 * 800 x 286 lap-times zone sheds Your best, the pit wall boards draw their Last and Best wider than
 * the artboard heads them, the leaderboard gives its lap columns up sooner. A thousandth of a ten
 * minute lap is noise, and `toshorttime` truncates as the clip did, so what is lost is what was
 * already not drawn, and what is gained is the digit that was cut. An hour is not a lap.
 */
export const LONG_LAP_DECIMALS = 2;

/** Ten minutes, in the seconds `timespantoseconds` reads a lap in: the first lap with two minute digits. */
export const LONG_LAP_SECONDS = 600;

/**
 * The widest reading a lap time draws at `decimals` places, which every box that draws one declares
 * as its `widest` so that the fit tests measure the budget and not the sample.
 *
 * Every digit is a 4, the widest digit of Barlow and Barlow Condensed in every weight a lap time is
 * drawn in (Light alone draws its 0 wider, and draws no lap), so the one string is the widest a
 * proportional reading can be as well as the cell count of a monospaced one.
 *
 * At three places it is in the shape of a lap under ten minutes, `4:44.444`. A ten-minute lap,
 * `10:30.12`, takes the same cells and draws no wider, but the shape is not only a width: the AiM
 * theme draws its LCD ghost from the `widest`, and a ghost of `88:88.88` behind `1:42.905` puts the
 * lit colon and point a cell away from the unlit ones on every ordinary lap. The ten-minute lap is
 * the rare one, so it is the one drawn over a ghost of the other shape. At fewer places the ten
 * minute form keeps its places and has the one more cell, so it is the widest, `44:44.4`.
 */
export const lapTimeWidest = (decimals = 3): string =>
  decimals > LONG_LAP_DECIMALS ? `4:44.${'4'.repeat(decimals)}` : `44:44.${'4'.repeat(decimals)}`;

/** The three-decimal form, which is what a lap time is drawn to unless it asks for fewer. */
export const LAP_TIME_WIDEST = lapTimeWidest();

/** Character budgets of the values the second screens draw. */
export const CHARS = {
  /** `1:42.905`, and `10:42.90`: a lap of ten minutes is drawn to {@link LONG_LAP_DECIMALS} places to fit. */
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
  /**
   * `08:46`, and `100:00` once a tank lasts a hundred minutes: {@link minutesClock} counts on in
   * minutes rather than turning into an hour, so the budget is three minute digits and two of
   * seconds, and {@link MINUTES_CLOCK_WIDEST} is what a box holding one is measured by. #899.
   */
  minutesClock: { digits: 5, specials: 1 } as Chars,
  /**
   * `14:32`, and `12:59` on a twelve-hour clock: the same four digits and a colon, so one budget
   * holds either format. The `AM` or `PM` after a twelve-hour clock is a word beside the value and
   * not a cell of it, `M` being a glyph no cell holds; see {@link TimeOfDay}. Its own budget rather
   * than {@link CHARS.minutesClock}'s, which is a duration and reads `08:46` in the same cells, so
   * that widening one cannot quietly widen the other: the duration has since had a third minute
   * digit for a tank that lasts `100:00`, and the time of day stayed at four.
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
  /**
   * `142`: a lap count, whether the lap you are on, the laps done, the laps left or the laps of a
   * stint. Three digits, because a hundred-lap oval and a day-long race both run past 99, and the
   * position's two cells, which these fields used to borrow, cut the last digit of lap 100. #596.
   */
  lap: { digits: 3, specials: 0 } as Chars,
  /**
   * `999x`: the incident count and the `x` after it, which takes a full cell of its own. The count's
   * three cells left the `x` nowhere to go, and band D's corner, which was cut from them, clipped
   * from the hundredth incident of a long race. #596.
   */
  incidents: { digits: 4, specials: 0 } as Chars,
  /** `2.4k` */
  rating: { digits: 4, specials: 1 } as Chars,
  /** `100` per cent, and the input readouts. */
  percent: { digits: 3, specials: 0 } as Chars,
  /** `54.2` brake bias, `3` assist levels. */
  setting: { digits: 4, specials: 1 } as Chars,
  /**
   * `LMP2 · P12`, as it would be counted in cells. Nothing measures it by them: the run holds
   * letters a cell cannot, so every surface sets it proportionally and measures it from
   * {@link CLASS_AND_PLACE_WIDEST}, and this is only the budget a field has to carry.
   */
  classPosition: { digits: 10, specials: 0 } as Chars,
  /**
   * `Offline Testing`: a session's name, in the cells it would take. The name is a word and is drawn
   * proportionally, so this is not what its box is cut from; {@link SESSION_NAME_WIDEST} is.
   */
  sessionName: { digits: 15, specials: 0 } as Chars,
  /**
   * `12 / 43`, and `142 / 350` in an endurance race: nine digit cells and no special.
   *
   * The two spaces and the slash take the full cell, since the narrow one is given only to `.,:`,
   * so the reading is as many cells as it has characters. Three digits either side of the slash is
   * every oval race of a hundred laps and more and every day-long race. The budget used to count the
   * spaces and the slash as specials, which at 46 px is a box of 168 px for a `12 / 120` of 176, and
   * WPF cut the last digit of the total from the first lap of any race that long. #921.
   */
  lapOfTotal: { digits: 9, specials: 0 } as Chars,
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

/**
 * A lap time as `toshorttime` writes it, `m:ss.fff`, and to {@link LONG_LAP_DECIMALS} places once
 * the lap reaches ten minutes, `10:42.90`, so that every lap fits the cells cut for `1:42.905`.
 *
 * With no placeholder of its own: a lap that may be unset goes through {@link lapTime}, which reads
 * this only once {@link hasTime} has said there is a time to read. The ten-minute test still reads
 * through {@link secondsOf}, so that it cannot throw on a missing time whichever branch a caller
 * takes, which is the rule `test/timespanGuard.test.ts` holds every binding to. #586.
 */
export const lapReading = (ts: Expr, decimals = 3): Expr => {
  const long = Math.min(decimals, LONG_LAP_DECIMALS);
  if (long === decimals) return toShortTime(ts, decimals, false, true);
  return iff(lt(secondsOf(ts), num(LONG_LAP_SECONDS)), toShortTime(ts, decimals, false, true), toShortTime(ts, long, false, true));
};

/** A lap time as {@link lapReading} draws it, or the placeholder of the same shape when it was never set. */
export const lapTime = (ts: Expr, decimals = 3): Expr => iff(hasTime(ts), lapReading(ts, decimals), str(noTime(decimals)));

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

/** The longest reading {@link minutesClock} draws, and so what a box holding one is measured by. */
export const MINUTES_CLOCK_WIDEST = '999:59';

/** {@link MINUTES_CLOCK_WIDEST} in seconds, the most {@link minutesClock} counts. */
const MINUTES_CLOCK_CAP = 999 * 60 + 59;

/**
 * Seconds as `mm:ss`, or `--:--` when there is nothing to count. Built the way `hms` is, minus the
 * hour term, because band D is 60 px tall and an hour that reads `0:` whenever a tank lasts under
 * one is a cell spent saying nothing.
 *
 * Past 99 minutes the minutes count on, `100:00`, rather than turning into an hour. The budget used
 * to be four digits on the reasoning that no tank lasts that long, and a full tank in a car that
 * sips fuel does, at a short circuit or at the start of an endurance stint: `100:00` in a box cut
 * for `08:46` was clipped by WPF, on the band's default page, at the moment a driver reads it.
 * Rolling into `1:40` instead would have fitted and read as a minute and forty seconds, which is the
 * one misreading a fuel time cannot allow, so the box has a third minute digit instead. That cell
 * costs no house face a field. On its own it cost the Porsche's and the AiM's feet at 850 and 800
 * wide between one and three from the tail, and {@link bandFuelQuantity} wins that room back. The
 * seconds are capped at {@link MINUTES_CLOCK_WIDEST}, over sixteen hours, which no tank lasts but a
 * per-lap figure a hair above zero can ask for, so that even that reading stays inside the cells
 * it is drawn in. The floor and the cap are doubles for the reason {@link hms} gives. #899.
 */
export const minutesClock = (seconds: Expr): Expr => {
  const s = min(real(MINUTES_CLOCK_CAP), max(real(0), seconds));
  const mmss = concat(fmt(truncate(div(s, num(60))), '00'), str(':'), fmt(truncate(mod(s, num(60))), '00'));
  return iff(gt(seconds, num(0)), mmss, str('--:--'));
};

/** An iRating as `2.4k`, or `--` when the sim does not report one. */
export const ratingK = (value: Expr): Expr => iff(gt(isnull(value, num(0)), num(0)), concat(fmt(div(value, num(1000)), '0.0'), str('k')), str(NO_VALUE));

/**
 * The widest {@link ratingK} a proportional box in `face` has to hold: two whole thousands, since a
 * few hundred drivers are rated over 10 000 and none near 100 000, which is also what
 * {@link CHARS.rating}'s four digit cells hold.
 *
 * Measured rather than written as a sample, because a sample is a rating somebody holds and the box
 * has to hold everybody's: the opponents page was cut to `iR 3.1k` and clipped the `k` of every
 * driver rated ten thousand or more, and of a driver rated `4.0k` as well. #902.
 */
export const ratingWidest = (face: MeasuredFace): string => widestFigure(face, '99.9k');

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
 * table. The player's place is the live one, the window being a run of live places that
 * {@link rowIndex.split} turns into rows. The max is also the guard: the place is 0 before the sim has
 * placed the player, and a window pinned against the kept rows is the plain list the table drew
 * before there was a split.
 *
 * The min is the other half of the sentence the canvas writes, "when the player sits below the row
 * budget": a field the rows can all hold is not cut at all, and the last car of a longer one is the
 * last row rather than the window running off the end into blank rows with cars hidden behind the
 * line.
 */
const splitWindowTop = (topRows: number, rows: number): Expr =>
  max(num(topRows + 1), min(sub(carOverallPosition(player()), num(Math.ceil(rows / 2) - 1)), sub(opponentCount(), num(rows - 1))));

/** The row a table's Nth line shows, given its mode. */
export const rowIndex = {
  /** The leaderboard in order, as it stands on the road where the plugin publishes the order (#1063). */
  full: (): Expr => liveRow(repeatIndex()),
  /** The player's class only, in the same order. */
  inClass: (): Expr => liveClassRow(repeatIndex()),
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
  split: (topRows: number, rows: number): Expr => liveRow(sub(add(splitWindowTop(topRows, rows), repeatIndex()), num(1))),
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

/**
 * The place of the car on leaderboard row `idx` over the whole field, as it stands on the road where
 * the plugin publishes it and the sim's own place otherwise, or 0 while nobody has placed it.
 *
 * iRacing moves a car's place only when it crosses the start/finish line, and SimHub's `driverposition`
 * is that place, so a pass in the first corner reached the dash a lap later (#1063). The plugin works the
 * place out from the distance every car has covered and publishes it per leaderboard row;
 * `LIVE_POSITION` in `contract.ts` says when it publishes nothing, and then this is the sim's place,
 * which is also what a package with no plugin draws.
 */
export const carOverallPosition = (idx: Expr): Expr => isnull(setting.live(LIVE_POSITION, idx, driver('position', idx)), num(0));

/**
 * The same in the car's own class: the live class place, else `driverclassposition`, else 0. SimHub
 * numbers the class place of every listed car whether or not the sim has placed it, so it is drawn
 * only where {@link hasPosition} holds (#1014).
 */
export const carClassPosition = (idx: Expr): Expr => isnull(setting.live(LIVE_CLASS_POSITION, idx, driver('classposition', idx)), num(0));

/**
 * The leaderboard row of the car in place `place` over the whole field, as it stands on the road.
 *
 * SimHub's leaderboard is sorted by the sim's place, so with no live order the row is the place itself,
 * which is what every list drew before the plugin published one. A place past the field is a row the
 * plugin does not fill and the fallback runs past the leaderboard too, so `driveravailable` still hides it.
 */
export const liveRow = (place: Expr): Expr => setting.live(LIVE_ORDER, place, place);

/**
 * The leaderboard row of the car in place `place` of the player's own class, as it stands on the road,
 * or SimHub's `getopponentleaderboardposition_playerclassonly`, which counts the sim's order.
 */
export const liveClassRow = (place: Expr): Expr => setting.live(LIVE_CLASS_ORDER, place, classPosition(place));

/**
 * A car's position, overall or in class per the plugin's PositionMode.
 *
 * It is a number whether or not the sim has placed the car, and it is never drawn without asking
 * {@link hasPosition} first: every drawing that prints a position goes through that guard. A grid
 * of AI before the green flag has no positions at all, and a column of `P0` or of places SimHub
 * counted out of the driver list is a row of wrong answers where an empty cell is an honest one.
 * Overall the number is 0 until the sim places the car; in class it is never 0, for the reason
 * {@link hasPosition} gives.
 *
 * Live, through {@link carOverallPosition} and {@link carClassPosition}: every list is drawn in the
 * live order as well ({@link liveRow}), so a column of these counts down one at a time whichever of the
 * two the plugin is publishing.
 */
export const carPosition = (idx: Expr): Expr => iff(classMode(), carClassPosition(idx), carOverallPosition(idx));

/**
 * True once the sim has actually placed this car, which only its overall position says, in either
 * mode. Positions count from one, so an overall zero is "not yet".
 *
 * The class place cannot answer it, because SimHub never reports a class place of 0 for a car on
 * the leaderboard. `GameManagerBase` in 9.12.6 numbers `PositionInClass` itself, from 1 within each
 * class over every opponent, in the order of their overall `Position` with a 0 sorted last, then
 * their live position, then their best lap. So before iRacing has placed anyone, every car still
 * has a class place, which is its order in the driver list within its class, and a guard asking
 * that place drew `GT3 · P12`, `12 / 12` and a relative of P1 to P12 on a grid where the overall
 * reading drew `--` (#1014). A car the sim has placed overall has a class place that means
 * something, the placed cars being sorted ahead of the rest.
 *
 * The sim's place answers it rather than {@link carOverallPosition}, and the two agree: the plugin
 * publishes a live place only for a car the sim has placed (#1063).
 */
export const hasPosition = (idx: Expr): Expr => gt(isnull(driver('position', idx), num(0)), num(0));

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
 * A car's place in its own class: {@link carClassPosition}, so it follows the race on the road as every
 * other place does. SimHub numbers it whether or not the sim has placed the car, so it is drawn only
 * where {@link hasPosition} holds.
 *
 * Always the class, whatever `PositionMode` says, which is what sets it apart from
 * {@link carPosition}: it is the place that stands beside a class name, and with the rig counting
 * overall it is the one reading left that says where a car stands among its own.
 */
export const classPlace = (idx: Expr): Expr => carClassPosition(idx);

/**
 * `LMP2 · P4`: a car's class and its place in that class, or `LMP2 · P--` before the sim has placed
 * it. Every surface that draws the pair binds this, and no other binding spells `' · P'`.
 *
 * It was written out three times, as the player's `CarClass` and a formatted `classposition`, and
 * each copy had the same three faults. The grid read `GT3 · P0`, where every other position on the
 * face reads `--` until {@link hasPosition} holds. The class went in whole, so a box measured for
 * four letters drew `Ferrari 296` into itself and WPF cut it wherever it ran out. And two of the
 * three drew it in digit cells, where the `M` of LMP2 and of BMW overruns its cell
 * (`font.cell.excluded`).
 *
 * So the class is cut by {@link chipText}, the cut the leaderboard's chip makes, because a header
 * reading one spelling of a class beside a chip reading another would be two answers to one
 * question; the place is drawn `--` until there is one, under the guard {@link positionDigits} asks;
 * and the run is set proportionally by every caller and measured from {@link CLASS_AND_PLACE_WIDEST}.
 * The guard is the overall position and not the class place's own `> 0`, which never failed: SimHub
 * numbers every listed car's class place from 1 before anyone is placed, and the pair read
 * `GT3 · P12` on a grid (#1014).
 *
 * Both halves are read from the same leaderboard entry, so the class and the place cannot belong to
 * two different cars. It takes the car's index for that reason, and any car's pair can be drawn
 * from it.
 *
 * A car with no class name reads its place alone, `P4`, rather than a separator with nothing before
 * it. The evaluator reads ` · P4` for an empty or null class, and an entry can carry one: the
 * emulator's pace car does, and a session whose cars carry no class may.
 */
export const classAndPlace = (idx: Expr): Expr => {
  const cut = chipText(carClass(idx));
  const place = iff(hasPosition(idx), fmt(classPlace(idx), '0'), str(NO_VALUE));
  return iff(eq(cut, str('')), concat(str('P'), place), concat(cut, str(' · P'), place));
};

/**
 * The readings that can be the widest {@link classAndPlace} draws, one for each data face a theme
 * sets it in: the widest four capitals a real class name is cut to, and the widest of the
 * ninety-nine places and the placeholder.
 *
 * `MUST`, from Mustang, and not the chip's `LMP2`: the cut keeps whatever four letters a class starts
 * with, and four letters with an `M` among them are wider than LMP2's, so a Mustang, a Mercedes or a
 * McLaren in a big field drew past a box measured from LMP2 and lost the end of its last digit. In
 * the house's Barlow Condensed, `LMDH` draws exactly as wide in SemiBold and is a hair narrower in
 * Bold. In Barlow Medium, which the Porsche sets its numerals in, the order turns: `LMDH` is the
 * widest, then `LAMB`, `MERC` and `MCLA`, and `MUST` is narrower than all four, so the Porsche's
 * faces were cut from a reading a Hypercar's `LMDH · P44` drew a pixel past (#1058). `44` and not
 * `99` or `24` in both, because both faces are proportional and the `4` is the widest digit each has.
 */
const CLASS_AND_PLACE_CANDIDATES = ['MUST · P44', 'LMDH · P44'] as const;

/**
 * Of a few readings, the one at least as wide as every other in both data weights of this process's
 * face, a tie going to the first. None being so is an error rather than a guess, since a declaration
 * narrower than another reading in either weight is the very defect a declaration is there to prevent.
 */
function widestInDataFace(readings: readonly string[]): string {
  const width = (weight: DataWeight, text: string): number => measureText(DATA_FACE[weight], text, 100);
  // The advances are summed in floating point, and `LMDH` and `MUST` come to the same width in the
  // house's SemiBold by different sums, so a tie is read as a tie rather than as the last bit of a sum.
  const holds = (text: string): boolean => readings.every((other) => (['SemiBold', 'Bold'] as const).every((w) => width(w, other) <= width(w, text) + 1e-9));
  const widest = readings.find(holds);
  if (widest === undefined) throw new Error(`none of ${JSON.stringify(readings)} is the widest in both of ${JSON.stringify(DATA_FACE)}`);
  return widest;
}

/**
 * The widest {@link classAndPlace} draws, in the data face of the theme this process builds: `MUST ·
 * P44` in the house's face and the AiM's, `LMDH · P44` in the Porsche's.
 *
 * Read from {@link CLASS_AND_PLACE_CANDIDATES} in the face {@link DATA_FACE} names rather than
 * written once, because a theme's package is built in a process of its own whose overlay changes the
 * face, and a widest measured in the house's face is a promise about the house's face only.
 * `classAndPlace.test.ts` holds a list of class names from iRacing, Assetto Corsa and ACC and
 * measures every cut of them at every place against this, in SemiBold and in Bold, in the process of
 * every theme; a class some theme's face draws wider than every candidate is found there, and its
 * reading belongs among them.
 *
 * It also sets the width of the bar's ends, and at 600 x 686 the strip's five cells now fill what
 * the ends leave to the pixel. A class wider than `MUST` in the house's face added to that list is
 * therefore a decision about the fifth cell as well as a new entry: a bare `Camaro`, cut to `CAMA`,
 * would cost it.
 */
export const CLASS_AND_PLACE_WIDEST: string = widestInDataFace(CLASS_AND_PLACE_CANDIDATES);

/**
 * Places gained since the start, signed; 0 when the sim does not track it, and 0 where there is no
 * honest count to give, which {@link placesGainedCounted} says.
 *
 * Counted in the field the position beside it is counted in, which is {@link carPosition}'s own
 * question and is why the two read the same setting: a triangle counting the whole race next to a
 * number counting one class is the pair of cells #212 is about, a place and the movement of that
 * place answered from different fields. A car that started eighth overall and third in class and
 * now runs fifth and first drew `P1` with three places gained beside it.
 *
 * It is SimHub's `PositionGain` in both modes, and never `PositionGainClass`, because only the first
 * counts from a place the sim gave. `GameManagerBase` in 9.12.6 keeps a car's overall start while it
 * is 0 or less and replaces it with the car's `Position`, so the overall start is the first place
 * the sim gave the car, and `PositionGain` stays null until then. The class start is kept the same
 * way but taken from `PositionInClass`, which SimHub numbers from 1 for every listed car whether or
 * not the sim has placed it (#1014), so it is set once, on the first frame SimHub sees the car, from
 * the car's order in the driver list within its class. In a practice a GT3 first in that list starts
 * first in class, and once three GT3s have set times and it sets one fourth in class it reads three
 * places lost that it never lost, for the rest of the session (#1022). A race whose first frame
 * has nobody placed would carry the same made-up start to the flag.
 *
 * Counting in class, the overall count is the class count when the field is a single class, every
 * car's class place then being its overall place, and that is where it is drawn. In a field of
 * several classes nothing SimHub publishes says where a car first stood in its class, so the cell
 * draws nothing at all rather than a count measured from a place SimHub made up.
 *
 * A car the sim has not placed has moved nowhere, so the count waits for {@link hasPosition}, as
 * the place beside it does (#1014).
 *
 * `PositionGain` is the start less the sim's place (`RacePositionGain = StartPosition - Position` in
 * `GameManagerBase`), so it moves at the line with it. The live gain is the start less the live place,
 * which is SimHub's gain plus the places the live count is ahead of the sim's: the same arithmetic,
 * and nothing where the plugin publishes no live place, the two places then being one. #1063. In a
 * single class the live overall place is the live class place, so the count beside a class place
 * moves with it too.
 */
export const carRankChange = (idx: Expr): Expr =>
  iff(
    and(hasPosition(idx), placesGainedCounted()),
    add(isnull(driver('positiongain', idx), num(0)), sub(isnull(driver('position', idx), num(0)), carOverallPosition(idx))),
    num(0),
  );

/**
 * Whether {@link carRankChange} has a count to give: always counting overall, and counting in class
 * only while every car on the leaderboard is in the player's class, which is when SimHub's
 * `PlayerClassOpponentsCount` is its `OpponentsCount`.
 *
 * Read by the rank column, which draws nothing, not even the dash that says a car has not moved,
 * where this is false.
 */
export const placesGainedCounted = (): Expr => ncalc.or(ncalc.not(classMode()), eq(classOpponentCount(), opponentCount()));

/**
 * Whether a gap between a car and the car it is measured from means anything: only once the sim has
 * placed both, which {@link hasPosition} says of each.
 *
 * SimHub publishes a `GapToLeader` for every car on the leaderboard whether or not the sim has placed
 * it, and before anyone is placed it measures them from whichever car is first in its driver list.
 * In a practice before anyone had set a time, counting overall, the Gap column read `+33.4`, `−20.8`
 * and `+9.2` down a column of `P--`, gaps to a leader that leads nothing and some of them negative,
 * and the Int column the differences of those (#1028). The car is asked because its own figure is
 * the one drawn, and the car it is measured from because a gap to a car with no place is a gap to
 * nothing in particular, whatever the figure says.
 *
 * Every Gap and Int cell of a board asks it, counted overall or in class, so an unplaced car reads
 * `--` beside its `P--` under either setting.
 */
export const gapIsMeasured = (idx: Expr, from: Expr): Expr => and(hasPosition(idx), hasPosition(from));

/**
 * A car's gap to SimHub's leader, on either side of a difference: SimHub's `gaptoleader`, and 0 on
 * the first car of the leaderboard where SimHub leaves it null.
 *
 * Every gap SimHub publishes is measured from that first car, and the first car is given no gap of
 * its own. `GameManagerBase.ComputeOpponentsData` in 9.12.6 calls `UpdateGapToLeader` for every other
 * car, and sets the first car's `GaptoLeaderSimHub` to 0 only when it is the player's. So whenever
 * somebody else led the race, the class Gap and both Int readings had a null on one side of their
 * subtraction. Counting in class, the default, every classmate of the race leader read `--`, which is
 * every car of a single-class race, and P2's Int was empty under either setting (#1041).
 *
 * The first car is the one every gap is measured from, so its gap to itself is 0 exactly, and SimHub
 * reads it the same way where it builds a class gap itself: `GaptoClassLeader` subtracts 0 when the
 * first car of the player's class is the first car overall. A gap SimHub did publish for it is kept.
 * No other car is read as 0. Any other car with no gap is one SimHub has not measured, having no lap
 * distance for it yet, and a difference taken from it would be a gap to nothing.
 *
 * The first car is asked by its index, 1, and not by its place: the `driver` functions index SimHub's
 * `Opponents` from 1, and the first of that list is the car `UpdateGapToLeader` is handed.
 *
 * The columns read both cars of a difference through it, the car measured as well as the car it is
 * measured from. Once they measure from the leader on the road (#1063), SimHub's first car is not
 * always the one at the top: in the lap after a pass for the lead it is second on the road, and its
 * own row is measured. Read raw, its gap is null whenever it is not the player's, and the car just
 * passed read `--` in the Gap column and nothing in the Int column for the rest of that lap, where
 * its gap to itself, 0, less the passing car's gives the half second it is behind.
 */
export const referenceGap = (idx: Expr): Expr => iff(eq(idx, num(1)), isnull(driver('gaptoleader', num(1)), num(0)), driver('gaptoleader', idx));

/**
 * The sessions whose leaderboard is ordered by best lap rather than on the road, in the words
 * iRacing names them: SimHub's `SessionTypeName` passes iRacing's `SessionType` through unchanged.
 * `Qualify` is the word the Porsche's strip already reads as qualifying, and `Open Practice` and
 * `Lone Practice` are here on the word of the fuel margin's tests and of #1029, which treat them as
 * names iRacing writes, rather than of a recording. They are compared in capitals, so a sim that
 * writes `PRACTICE` reads the same; no committed trace carries another sim's spelling.
 *
 * A list of the sessions that are not races, and not the one word `Race`, because the two ways of
 * being wrong are not alike. A race measured by best lap would draw small, believable gaps that are
 * false, where a practice measured by distance draws `+5L` and three-digit negatives that nobody
 * mistakes for a gap. So a session named in any other word, or in none, keeps the distance gap,
 * which is the reading the columns always gave and is right in every race. iRacing's heat events
 * are among those words.
 */
export const BEST_LAP_SESSIONS = [
  'Practice',
  'Open Practice',
  'Lone Practice',
  'Qualify',
  'Open Qualify',
  'Lone Qualify',
  'Offline Testing',
  'Warmup',
] as const;

/**
 * Whether the session's leaderboard is ordered by best lap, which {@link BEST_LAP_SESSIONS} lists,
 * and so whether the Gap and Int columns measure best laps rather than distance (#1040).
 */
export const rankedByBestLap = (): Expr => isIn(ucase(sessionType()), ...BEST_LAP_SESSIONS.map((name) => str(name.toUpperCase())));

/**
 * The gap outside a race: a car's best lap less the best lap of the car it is measured from, signed
 * to one decimal as the race gap is, or `missing` where either car has no best lap yet.
 *
 * SimHub publishes no such gap. Every `GaptoLeader` it computes, in every session, is the first car's
 * `CurrentLapHighPrecision` less the car's, times a reference lap, and `LapsToLeader` the truncated
 * difference of the same two distances (`GameManagerBase.ComputeOpponentsData` and
 * `GameManagerBaseGapHelpers.UpdateGapToLeader` in 9.12.6). In a race that is the gap, the order being
 * the order on the road. In practice and qualifying iRacing orders its results by best lap, so the
 * first car is the fastest rather than the furthest round, and the distance says only how long each
 * car has been out: a car 0.4 s off the best lap that had run five laps fewer read `+5L`, and one
 * 0.6 s off that had run five more read `−432.0`. Its `DeltaToBest` is a best-lap difference, but
 * taken from the fastest car of the whole field whatever the list, and floored at 0, so the class
 * column and the interval could not read it. So the two best laps are read and subtracted here.
 *
 * A best lap is a TimeSpan SimHub leaves at zero for a car with none, which {@link hasTime} tells
 * apart, and the guard stands outside the subtraction for the reason it gives. Each is read through
 * {@link secondsOf}, as every conversion handed to an operator is (#586).
 */
const bestLapGap = (idx: Expr, from: Expr, missing: string): Expr => {
  const here = driver('bestlap', idx);
  const there = driver('bestlap', from);
  return iff(and(hasTime(here), hasTime(there)), signed(sub(secondsOf(here), secondsOf(there)), '0.0'), str(missing));
};

/**
 * The gap to the leader: `Lead` on the leader's own row, `+2.6` on a car on the lead lap, and `+1L`
 * once a car is a lap or more down.
 *
 * Both are spelled here rather than taken from `gaptoleadercombined`, which is SimHub's one string
 * for both cases and whose sign, decimals and words the dash has no say in. It writes the seconds to
 * two decimals and a lapped car as `+1 lap` or `+2 laps`, where the interval beside it is
 * `signed(..., '0.0')` and the class column {@link carClassRaceGap} spells laps `+1L`: two
 * neighbouring readings of one quantity at different precisions is what the sheet is measured
 * against, and `+2 laps` is wider than the cells the column is cut for. `signed` writes the
 * typographic minus, so the value is not formatted again on top of it.
 *
 * Lapped is a question of distance, and SimHub answers it per car as `lapstoleader`:
 * `GameManagerBase` in 9.12.6 sets it to the truncated difference of the leader's
 * `CurrentLapHighPrecision` and the car's, which count the laps begun and the fraction of the lap
 * run, so it reaches 1 when the car is a whole lap behind and not before. The lap counters cannot
 * answer it. `currentlap` is the lap in progress and goes up as a car crosses the line, so between
 * the leader crossing it and a car on the lead lap crossing it too, the car's counter is one below
 * the leader's, and the row read SimHub's two-decimal string for those seconds of every lap, `+42.30`
 * under a `+36.4` on the pit wall (#1023). Where SimHub has no count the row reads seconds, which is
 * the reading that is always true.
 *
 * The leader is the car in first place as it stands on the road, {@link liveRow} of 1, and the gap is
 * measured to it as the difference of the two gaps SimHub measures to its own leader, which is the
 * shape {@link carClassRaceGap} already has. SimHub's leader is the sim's, which iRacing changes only
 * at the line (#1063); where the two are the same car its own gap is zero and nothing moves. The
 * leader's own gap is read through {@link referenceGap}, since SimHub gives its first car none unless
 * it is the player's (#1041), and so is the car's, which is that first car in the lap after it is
 * passed for the lead. `lapstoleader` is SimHub's count and so counts from SimHub's leader, and
 * in the lap after a pass for the lead it can be one short: a car a lap down on the road leader and
 * not on the sim's reads its seconds to the road leader, which are true, and a car two laps down on
 * the one and one on the other reads `+1L`. No driver function counts laps from a car of the dash's
 * choosing, so the count is SimHub's or the lap counters', and the lap counters are the fault above.
 *
 * This is the reading of a list drawn from the whole field. A list drawn from one class measures to
 * the leader of that class instead, which is {@link carClassRaceGap}.
 *
 * A gap is drawn only between two cars the sim has placed, which {@link gapIsMeasured} asks.
 *
 * All of that is a race. Outside one, where {@link rankedByBestLap} holds, the list is ordered by
 * best lap and a distance says nothing about pace, so the row reads its best lap less the first
 * car's through {@link bestLapGap}, and `--` while either has none. No car is lapped in a practice,
 * so `lapstoleader` is not asked there (#1040).
 */
export const carRaceGap = (idx: Expr): Expr => {
  const leader = liveRow(num(1));
  const here = referenceGap(idx);
  const lead = referenceGap(leader);
  const laps = isnull(driver('lapstoleader', idx), num(0));
  return iff(
    eq(carOverallPosition(idx), num(1)),
    str('Lead'),
    iff(
      not(gapIsMeasured(idx, leader)),
      str(NO_VALUE),
      iff(
        rankedByBestLap(),
        bestLapGap(idx, leader, NO_VALUE),
        iff(ncalc.or(ncalc.isNull(here), ncalc.isNull(lead)), str(NO_VALUE), iff(gt(laps, num(0)), lapsDown(laps), signed(sub(here, lead), '0.0'))),
      ),
    ),
  );
};

/**
 * The widest reading the Gap and Int columns draw, which is what their cells declare: a sign and three
 * whole digits of seconds to one decimal, which is a car not yet lapped on any track a sim models, in
 * {@link CHARS.gap}'s cells. `Lead`, `+1L` and `--` are narrower. The columns read the session's name
 * to choose between distance and best laps (#1040), and a text that reads a property whose words are
 * known declares the widest it draws.
 */
export const GAP_WIDEST = `${MINUS}999.9`;

/** `+1L`: the laps a car is down, as both Gap columns spell them. */
const lapsDown = (laps: Expr): Expr => concat(str('+'), fmt(laps, '0'), str('L'));

/**
 * The same gap on a list drawn from the player's own class: `Lead` on the row it counts from, the
 * seconds to that car on a classmate on its lap, and `+1L` on one a lap or more down on it.
 *
 * The reference is the leader of the list rather than the leader of the race, because a column
 * measured to a car that is not on it says nothing a reader can use. Where the player's class runs
 * a lap behind the overall leader, every row of a class board measured the other way reads `+1L`
 * and no row of it reads `Lead`, which is a column carrying no intra-class gap at all.
 *
 * The laps are SimHub's `lapstoclassleader`, the truncated difference of distance that
 * {@link carRaceGap} reads as `lapstoleader`, taken against the first car of the player's class in
 * the sim's order, which is the car `getopponentleaderboardposition_playerclassonly(1)` names. The
 * column measures its seconds from the first car of the class on the road, {@link liveClassRow} of 1
 * (#1063). The two are the same car except in the lap after a pass for the class lead, and there the
 * count can be one short, as {@link carRaceGap} says of its own. Taken from the two cars'
 * `currentlap`, a classmate a few seconds behind the class
 * leader read `+1L` from the moment the leader crossed the line until it crossed the line too, on
 * every lap (#1023). The seconds are the difference of the two gaps to the overall leader, exactly
 * as {@link carInterval} takes one between two rows, which is what SimHub's own `gaptoclassleader`
 * computes for a car of the player's class; `gaptoclassleadercombined` is not read, for the reasons
 * {@link carRaceGap} gives for its overall twin.
 *
 * The class leader's gap is read through {@link referenceGap}, and so is the car's own, because a
 * class leader that leads the race has none from SimHub, nor does it in the lap after it is passed,
 * and the whole column read `--` below `Lead` in every single-class race and in the class leading
 * every other (#1041). There the difference is the car's gap to the race
 * leader, which is its gap to its class leader exactly. Where another class leads, the difference is
 * of two gaps both measured on the race leader's running, so it is the time the race leader took
 * between the two cars rather than the time the class leader took, and a class leader slower than
 * the race leader sees its classmates a little closer than they are. That is the figure SimHub's own
 * `gaptoclassleader` gives too.
 *
 * The word on the row the column counts from is the one thing here that follows the numbering
 * rather than the rows. `Lead` is a claim about a place, and a zone filtered to one class while
 * the rig counts overall draws that class by its overall places: the top row of such a list reads
 * `P2` where the class is a lap down on another, and `Lead` beside it is two cells of one row
 * making claims that contradict each other. {@link carPosition} is the place the row draws, so the
 * word is shown when that place is the first and the cell is left empty otherwise, the row having
 * nothing to measure against itself, which is what {@link carInterval} already draws in the cell
 * beside it. A class leading the race keeps the word under either setting.
 *
 * And the word waits for {@link hasPosition}, as the place beside it does. SimHub numbers every
 * listed car's class place from 1 before the sim has placed anyone, so on a grid counted in class
 * one car of each class has a class place of 1, and its row read `Lead` beside a place of `--`
 * (#1014). Every other row waits for {@link gapIsMeasured}, as the overall column does (#1028).
 *
 * Outside a race the row reads its best lap less the class leader's, as the overall column reads it
 * less the first car's and for the same reason (#1040). The class leader is the class's first car
 * by place, which in practice and qualifying is its fastest.
 */
export const carClassRaceGap = (idx: Expr): Expr => {
  const leader = liveClassRow(num(1));
  const here = referenceGap(idx);
  const lead = referenceGap(leader);
  const laps = isnull(driver('lapstoclassleader', idx), num(0));
  return iff(
    eq(carClassPosition(idx), num(1)),
    iff(and(hasPosition(idx), eq(carPosition(idx), num(1))), str('Lead'), str('')),
    iff(
      not(gapIsMeasured(idx, leader)),
      str(NO_VALUE),
      iff(
        rankedByBestLap(),
        bestLapGap(idx, leader, NO_VALUE),
        iff(ncalc.or(ncalc.isNull(here), ncalc.isNull(lead)), str(NO_VALUE), iff(gt(laps, num(0)), lapsDown(laps), signed(sub(here, lead), '0.0'))),
      ),
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
 * The whole laps a car is behind the car in front of it by distance: the truncated difference of the
 * two cars' `currentlaphighprecision`, which is how SimHub counts `lapstoleader` from its leader, and 0
 * where SimHub has no distance for either car (#1077).
 *
 * No count SimHub publishes answers it. `lapstoleader` is truncated before it reaches the dash, so the
 * difference of two of them is a lap off whenever the leader's lap falls between the two cars: with
 * the leader at 10.9 laps, cars at 9.95 and 9.85 count 0 and 1, and are a tenth of a lap apart.
 */
const lapsBehind = (idx: Expr, ahead: Expr): Expr => {
  const here = driver('currentlaphighprecision', idx);
  const there = driver('currentlaphighprecision', ahead);
  return iff(ncalc.or(ncalc.isNull(here), ncalc.isNull(there)), num(0), truncate(sub(there, here)));
};

/**
 * The race interval between a car and the car in front of it: the difference of the two gaps to the
 * leader, `+1L` where the car is a whole lap or more behind the car in front by distance, and nothing
 * where SimHub has not measured either gap.
 *
 * A gap to the leader carries the whole laps a lapped car is down, so the difference of two gaps on
 * different laps is those laps in seconds: on the pit wall a car five laps down read `+484.6` beside
 * a Gap of `+5L`, which is neither the time to the car in front on the road nor a lap count (#1077).
 * So the laps are asked first, by distance and not by the lap counters for the reason
 * {@link carRaceGap} gives, and spelled as the Gap column spells them. A lapped car on the lap of the
 * car in front still reads the seconds between them, which are true.
 */
const raceInterval = (idx: Expr, above: Expr): Expr => {
  const here = referenceGap(idx);
  const ahead = referenceGap(above);
  const laps = lapsBehind(idx, above);
  return iff(
    and(ncalc.not(ncalc.isNull(ahead)), ncalc.not(ncalc.isNull(here))),
    iff(gt(laps, num(0)), lapsDown(laps), signed(sub(here, ahead), '0.0')),
    str(''),
  );
};

/**
 * The interval to the car in front on the leaderboard: the difference of the two gaps to the
 * leader, or the laps between the two cars where the car is a lap or more behind it by distance,
 * through {@link raceInterval} (#1077). The leader's own row is empty, there being nothing in front
 * of it.
 *
 * The car in front is the one a place ahead as it stands on the road, found through the car's own
 * place and {@link liveRow} rather than the row number, `idx` being a leaderboard row: with no live
 * order the two are the same row above, which is what this drew before #1063. So a list of one class takes
 * {@link carClassInterval} instead. The two are one decision with the gap column beside them: Int
 * is the difference of two neighbouring Gaps, and a board counting the one from the class leader
 * while the other counted between leaderboard neighbours would draw two columns that do not add up.
 *
 * Below the first row it reads `--` until {@link gapIsMeasured} holds for the car and the car in
 * front, as the Gap beside it does (#1028). The second row's car in front is the first car of the
 * leaderboard, which SimHub gives no gap unless it is the player's, so it is read through
 * {@link referenceGap}; P2's cell was empty in every race the player did not lead (#1041). The car's
 * own gap is read the same way, since in the lap after a pass for the lead that first car is the one
 * a place behind the leader on the road.
 *
 * Outside a race it is the car's best lap less the best lap of the car in front, which is the
 * difference of the two best-lap gaps beside it, and empty where either car has no best lap, as it is
 * beside a gap SimHub has not measured (#1040).
 */
export const carInterval = (idx: Expr): Expr => {
  const place = carOverallPosition(idx);
  const above = liveRow(sub(place, num(1)));
  // The car with nothing in front of it: the leader by place once the sim has placed the car, and the
  // first row before then, every other unplaced row reading `--` (#1028).
  const first = iff(hasPosition(idx), eq(place, num(1)), eq(idx, num(1)));
  return iff(
    first,
    str(''),
    iff(gapIsMeasured(idx, above), iff(rankedByBestLap(), bestLapGap(idx, above, ''), raceInterval(idx, above)), str(NO_VALUE)),
  );
};

/**
 * The interval to the car one place ahead in the player's own class, which on a list drawn from
 * that class is the row above.
 *
 * The row above is found through the class place rather than through the row number, `idx` being
 * the only thing a cell is given: one off a car's own `classposition` and back through SimHub's
 * class-only lookup is the car the list draws above it, whatever leaderboard rows fall between the
 * two.
 *
 * Below the class's first row it reads `--` until {@link gapIsMeasured} holds, as
 * {@link carInterval} does (#1028), and the car in front is read through {@link referenceGap}, as
 * it is there: the class leader is the race leader in every single-class race (#1041). So is the
 * car's own, which is that first car in the lap after it is passed for the class lead. A car a lap
 * or more behind the car in front by distance reads the laps, through {@link raceInterval}, as on
 * the overall list (#1077). Outside a race it measures best laps, as {@link carInterval} does (#1040).
 */
export const carClassInterval = (idx: Expr): Expr => {
  const place = carClassPosition(idx);
  const above = liveClassRow(sub(place, num(1)));
  return iff(
    gt(place, num(1)),
    iff(gapIsMeasured(idx, above), iff(rankedByBestLap(), bestLapGap(idx, above, ''), raceInterval(idx, above)), str(NO_VALUE)),
    str(''),
  );
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
 * `P4 LMP2`: the position of the car behind and its class, `P-- LMP2` before the sim has placed
 * it, or the empty string when there is nothing behind. The position honours PositionMode, as
 * every position OpenDash draws does.
 *
 * The place is {@link positionLabelled}'s and not a second formatting of {@link carPosition}. It
 * was one, and it asked nothing first, so on the formation lap and in a practice session before
 * anyone had a time, which is where a blue flag is most often waved for nothing, the band read
 * `BLUE · P0 GT3` while every other position on the face read `P--` (#931).
 *
 * A car with no class name reads its place alone, `P4`, as {@link classAndPlace} does, rather than
 * a place with a space after it that a centred band would set half a space off its middle.
 */
export const carBehindPositionClass = (): Expr => {
  const car = carBehind();
  const cut = chipText(carClass(car));
  return iff(carAvailable(car), concat(positionLabelled(car), iff(eq(cut, str('')), str(''), concat(str(' '), cut))), str(''));
};

/**
 * The widest class cut the blue flag band draws for the car behind: `LAMB`, from Lamborghini.
 *
 * Not the chip's `LMP2`, for the reason {@link CLASS_AND_PLACE_WIDEST} gives: {@link chipText}
 * keeps whatever four letters a class name starts with, and in the band's Barlow Bold fourteen real
 * cuts are wider than LMP2's, `LAMB`, `MCLA`, `ARCA` and `MAZD` the widest of them (#931). It is not
 * `MUST` either, which is the widest in the data face {@link classAndPlace} is set in and not in
 * this one. `alertBand.test.ts` measures every cut of `classNames.ts` against it.
 */
export const WIDEST_BEHIND_CLASS = 'LAMB';

/**
 * The widest `carBehindPositionClass` can draw: a two-digit place and the widest class cut.
 *
 * `44` and not `99`, for the reason {@link CLASS_AND_PLACE_WIDEST} gives: the band is set in a
 * proportional face, whose `4` is the widest digit it has. The placeholder `--` is narrower than
 * either, so the place before a car is placed draws inside the same box. The class is
 * {@link WIDEST_BEHIND_CLASS} and not the chip's `LMP2`, which a Lamborghini or a McLaren behind
 * drew past.
 */
export const WIDEST_BEHIND_POSITION_CLASS = `P44 ${WIDEST_BEHIND_CLASS}`;

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

/**
 * The length a session declares, in seconds, where the sim says: iRacing's `SessionTimeTotal`, which
 * is the session's own clock as set (900 for a fifteen-minute qualifying) and the same week as
 * `SessionTimeRemain` where there is none. Nought from a sim that publishes no such thing.
 */
export const sessionLength = (): Expr => isnull(raw('SessionTimeTotal'), num(0));

/**
 * Whether the session is timed: its clock is running inside {@link UNTIMED_SECONDS}'s window, or it
 * declares a length inside the same window.
 *
 * The length is what makes a session timed, and the clock alone stopped saying so at nought. A
 * timed iRacing session runs a lap or two past the end of its clock, and with only the time left to
 * go by it read as a session with no clock for those laps: `TotalLaps`, which in a timed session is
 * the leader's laps, came back as the session's length (`L9 of 17` in a qualifying, #1017), and the
 * clock beside it read the unset `-:--:--` of a session that has not started. `SessionTimeTotal` does
 * not move while the session runs, so the session stays timed until it ends, and the clock reads
 * `0:00:00` over its last laps.
 *
 * The clock's own test stays beside the length rather than giving way to it, because the length is
 * iRacing's alone: every other sim, and an iRacing session from a tool that sets only the time left,
 * is timed exactly while its clock is running, as it was before. The length is held to the same day
 * as the clock, since iRacing writes the same week in both where a session has no limit.
 */
export const isTimedSession = (): Expr =>
  ncalc.or(and(gt(sessionTimeLeft(), num(0)), le(sessionTimeLeft(), num(UNTIMED_SECONDS))), and(gt(sessionLength(), num(0)), le(sessionLength(), num(UNTIMED_SECONDS))));

/**
 * A session the sim has given, which has no clock: iRacing's week of `SessionTimeLeft` in a
 * lap-limited race. The other half of {@link isTimedSession}'s window, and the state the two used
 * to be folded into. Strictly above the threshold, so a 24-hour race draws its clock; see
 * {@link UNTIMED_SECONDS} for which side of the line each length of race falls on.
 */
export const isUntimedSession = (): Expr => gt(sessionTimeLeft(), num(UNTIMED_SECONDS));

/**
 * Whether `TotalLaps` is the race's length: a session that is not timed, with a lap count.
 *
 * Timed is {@link isTimedSession}'s, which holds until the session ends and not only while its clock
 * is above nought, so the leader's laps do not come back as a length for the laps a timed session
 * runs after its clock (#1017).
 *
 * Every `/ 30` and `of 30` after a lap is drawn while this holds and only then. iRacing's
 * `TotalLaps` in a timed session is not a length at all but the leader's completed laps, so a
 * denominator that asked only whether the total was above nought told a driver twelve laps into a
 * forty-five minute race that it ended at fourteen, and one that asked nothing drew `4 / 0` in an
 * open practice. The session card had the rule right and the bar, the companion header and the pit
 * wall header each wrote their own (#989).
 *
 * Not timed rather than untimed, because a sim that publishes no clock at all for a lap race, a
 * `SessionTimeLeft` of nought rather than iRacing's week, still has its laps counted. What it costs
 * is the iRacing race given both a lap count and a time limit, which is timed and so draws the clock
 * and no total, as the session card has always drawn it.
 */
export const hasLapTotal = (): Expr => and(not(isTimedSession()), gt(totalLaps(), num(0)));

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
 * writing both nests the same comparison twice in a binding SimHub evaluates every frame. And `clock`
 * would draw the unset clock over the laps a timed session runs after its clock has reached nought,
 * where this draws `0:00:00`, and `hms` draws a time left below nought as nought (#1017).
 *
 * The `0:00:00` is for a clock that has run out, not for one SimHub has not given: on a frame it is
 * still building `SessionTimeLeft` is null while iRacing's declared length is already there, and
 * {@link sessionTimeLeft} reads that null as nought, so the clock asks {@link hasSessionTimeLeft}
 * as well and draws the unset clock there (#586).
 */
export const sessionClock = (): Expr => iff(and(isTimedSession(), hasSessionTimeLeft()), hms(sessionTimeLeft()), str(NO_CLOCK));

/**
 * Whether SimHub has a session time left to give at all, run out or not: false only where the
 * TimeSpan is null. The fallback is a number no clock reaches, below nought as well as above it,
 * since a time left past the end of a timed session can read below nought.
 */
export const hasSessionTimeLeft = (): Expr => gt(isnull(timespanToSeconds(game('SessionTimeLeft')), num(NO_SESSION_TIME)), num(NO_SESSION_TIME));

/** The fallback {@link hasSessionTimeLeft} tells a missing time left by. */
const NO_SESSION_TIME = -1_000_000_000;

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
/** Whether the driver has SimHub set to gallons, which is the one switch every fuel figure follows. */
const fuelInGallons = (): Expr => eq(isnull(game('FuelUnit'), str('Liters')), str('Gallons'));
export const fuelUnit = (): Expr => iff(fuelInGallons(), str('gal'), str('L'));
export const fuel = (): Expr => isnull(game('Fuel'), num(0));
/**
 * The tank in litres whatever the driver's unit, for a figure a car fixes in litres, as the Porsche
 * fixes its low-fuel alarm at ten (#1126). Null where no sim is running, so a caller can tell no tank
 * from an empty one.
 *
 * SimHub fills `Fuel` on every sim, from the reader's own litres (`FuelLevel` on iRacing, `Physics.Fuel`
 * on Assetto Corsa and ACC, `mFuel` on Le Mans Ultimate), and converts it to gallons on a gallons
 * profile by {@link LITRES_TO_GALLONS}, so dividing by the same ratio gives the litres back. The
 * unconverted figure, `FuelRaw`, is not published. iRacing's raw `FuelLevel` is the same litres on
 * iRacing alone and null on every other sim, which is why nothing reads it.
 */
export const fuelLitres = (): Expr => iff(fuelInGallons(), div(game('Fuel'), num(LITRES_TO_GALLONS)), game('Fuel'));
/**
 * How full the tank is, 0..100.
 *
 * **Not `GameData.FuelPercent`.** SimHub's game reader sets that one from the fuel the sim reports,
 * which is litres, over `MaxFuel`, which it has already converted to the driver's unit, so on a
 * gallons profile a half tank reads 189 and a bar fed by it stays full until a quarter of the tank is
 * left. The DataCore plugin's `Computed.Fuel_Percent` is `Fuel` over `MaxFuel`, both converted, so it
 * is right in either unit; SimHub's own fuel percent text reads it. #993.
 */
export const fuelPercent = (): Expr => isnull(computed('Fuel_Percent'), num(0));
export const fuelPerLap = (): Expr => isnull(computed('Fuel_LitersPerLap'), num(0));
export const fuelLapsLeft = (): Expr => isnull(computed('Fuel_RemainingLaps'), num(0));
/** The fuel range in seconds, nought when SimHub has none, guarded here for every caller as {@link sessionTimeLeft} is. */
export const fuelTimeLeft = (): Expr => secondsOf(computed('Fuel_RemainingTime'));
export const fuelLastLap = (): Expr => isnull(computed('Fuel_LastLapConsumption'), num(0));
export const fuelThisLap = (): Expr => isnull(computed('Fuel_CurrentLapConsumption'), num(-1));

/**
 * The lap a timed session's laps left are counted in, in seconds: the session's best lap, the last
 * lap where there is no best yet, and nought until a lap has been timed.
 *
 * The best rather than the last, because the count is a fuel figure's first term and the slow laps
 * are the ones a race is not run at. A lap through the pit lane or behind the safety car counted
 * from would make the race shorter than it is and put too little in the tank, where the best lap
 * makes it a lap long at the worst and the tank a lap's fuel heavy. The last is there for a session
 * whose sim has not set a best, and the nought is what {@link lapsLeftIsKnown} reads.
 */
export const timedLapSeconds = (): Expr =>
  iff(hasTime(bestLap()), secondsOf(bestLap()), iff(hasTime(lastLap()), secondsOf(lastLap()), num(0)));

/**
 * The time run in the lap in progress, in seconds, which a timed count adds to the time left.
 *
 * Its fallback is the double `0.0` rather than {@link secondsOf}'s Int32 `0`, because it is the left
 * operand of the `min` in {@link lapsLeft}, and NCalc answers `min` in its left operand's type (#831).
 */
const lapTimeRun = (): Expr => isnull(timespanToSeconds(game('CurrentLapTime')), real(0));

/**
 * Whether there is a count of laps left: always in a session counted in laps, and in a timed one
 * once a lap has been timed to divide the time left by.
 */
export const lapsLeftIsKnown = (): Expr => ncalc.or(not(isTimedSession()), gt(timedLapSeconds(), num(0)));

/** Whether the session is a race, the one session name OpenDash matches with certainty. */
const isRace = (): Expr => eq(ucase(sessionType()), str('RACE'));

/**
 * The overall leader's row on SimHub's leaderboard: the car first on the road, {@link liveRow} of 1.
 *
 * Not row 1, which is the sim's leader. SimHub sorts its leaderboard by the sim's place, and iRacing
 * moves that place only at the line (#1063), so for the rest of the lap after a pass for the lead row
 * 1 is the car that was passed. A car that had just taken the lead was told to wait for it, and read
 * a lap left too many and a Refuel for it on the last lap of a timed race. Where the plugin publishes
 * no live order, the row is 1: on every sim but iRacing, in every session but a race, with Live
 * positions switched off in the panel, before the sim has placed anyone and once the chequered flag
 * is out (the `LIVE_POSITION` note of the contract). The car then waits for the sim's leader, as it
 * did before.
 */
const RACE_LEADER = liveRow(num(1));

/**
 * The overall leader's lap in seconds: its best, and the car's own {@link timedLapSeconds} until
 * the leader has one, which is the reading the count would have made without it.
 *
 * The leader's own lap rather than the car's, because in a race of several classes the two differ
 * by tens of seconds, and when the leader reaches the line turns on the leader's pace. Taken as the
 * car's own instead, in a model of a race whose leader laps a tenth quicker than the car, the count
 * in the last two minutes missed the extra lap on about one reading in twenty and asked for one that
 * is not run on about one in ten; with the leader's own lap it was right on every one.
 */
const leaderLapSeconds = (): Expr =>
  iff(hasTime(driver('bestlap', RACE_LEADER)), secondsOf(driver('bestlap', RACE_LEADER)), timedLapSeconds());

/**
 * How long after the clock reaches nought the overall leader takes the chequered flag, in seconds,
 * or after now once the clock has reached it: the wait {@link lapsLeft} adds to the time left.
 *
 * The leader is the one car whose crossing ends a timed race, and it is on the leaderboard with how
 * far round its lap it is, `drivertrackpositionpercent` of {@link RACE_LEADER}. Run on at its own
 * lap for the time left, that says how far round it will be when the clock reaches nought, the whole
 * laps dropped, and the flag falls when it has run the rest of that lap: the wait is that rest at its
 * lap. A leader exactly at the line as the clock reaches nought is read as having just crossed it,
 * and waited for a whole lap, the side a fuel figure can afford to be wrong on. The position is a
 * distance and the lap a time, so a leader running slower than its best reaches the line a little
 * later than this says; near the flag that moves the answer only for a car within those seconds of
 * it on the road.
 *
 * Nought where nothing is waited for: outside a race, where a practice or a qualifying session ends
 * for each car at its own first crossing after the clock; for the leader itself, whose crossing is
 * the flag; and once the chequered flag is out, `Flag_Checkered`, which SimHub takes from iRacing's
 * session flag and which the SDK raises for the field once the leader has crossed. That has not been
 * seen on iRacing itself. Were the flag raised only at the car's own crossing, the wait would run on
 * to the leader's next crossing and the car's last lap would read two, a lap too many. A whole lap
 * where the leader's position is not published, which is the most the wait can be.
 *
 * Nothing sharper is published. iRacing's `CarIdxEstTime`, the leader's time round its lap, reaches
 * SimHub's raw telemetry as an array by car index, which a binding has no way to index by the
 * leader's; iRacing's `SessionState` is there as well, and says no more than the chequered flag; and
 * `gaptoleader` is SimHub's own estimate, the iRacing reader setting none, which for a lapped car
 * includes its whole laps, timed at a reference lap or at the leader's own as a SimHub setting
 * chooses, and so cannot be taken modulo a lap into the phase this needs.
 */
const leaderWait = (): Expr => {
  const lap = leaderLapSeconds();
  const position = driver('trackpositionpercent', RACE_LEADER);
  const roundAtTheClock = mod(add(div(clockLeft(), lap), position), num(1));
  const awaited = and(isRace(), not(gt(isnull(game('Flag_Checkered'), num(0)), num(0))), not(carIsPlayer(RACE_LEADER)));
  return iff(awaited, mul(lap, iff(isNull(position), num(1), sub(num(1), roundAtTheClock))), num(0));
};

/**
 * The time left, never below nought. The time comes first in the `max`, because NCalc's `max` takes
 * the type of its first argument, and an Int32 nought there would round the time to a whole second.
 */
const clockLeft = (): Expr => max(sessionTimeLeft(), num(0));

/** The time from now until the overall leader takes the flag: {@link clockLeft} and {@link leaderWait}. */
const flagIn = (): Expr => add(clockLeft(), leaderWait());

/**
 * The laps the car still has to run, the one it is on included: `RemainingLaps` in a session counted
 * in laps, and in a timed session the whole laps the time left holds at {@link timedLapSeconds}, and
 * the lap the clock runs out on.
 *
 * Two quantities, because SimHub makes only the first. It sets `RemainingLaps` for every game to
 * `TotalLaps` less `CompletedLaps`, never below nought (GameReaderCommon 9.12.6, `GameManagerBase`),
 * and in a timed iRacing race `TotalLaps` is the leader's completed laps, so `RemainingLaps` there is
 * how many laps the car is behind the leader: nought on the lead lap with half an hour to run, which
 * the fuel page multiplied into nothing to add, and `3` as the laps left for a car three laps down
 * (#1008). Timed is {@link isTimedSession}'s, the test {@link hasLapTotal} draws a race's length by,
 * so a race whose length is not drawn is a race whose laps are predicted.
 *
 * A timed race ends when the leader crosses the line after the clock has reached nought, so the
 * leader runs the lap it is on and every lap it starts before the clock runs out. Counted at the
 * line that is the whole laps of the time left, and one. Mid-lap the time already run in the lap
 * is added to the time left first, since the lap in progress is a whole lap of which only the rest
 * is still to come: 60 s into a 100 s lap with 1050 s to run, the car is at the line with 1010 s
 * left and runs eleven laps more, twelve in all, where the time left alone made it eleven. The time
 * run is taken at most a lap, so a lap through the pit lane counts as one about to end rather than
 * as two. It counts the lap you are on, as `RemainingLaps` does. After the clock it is the lap in
 * progress, which is one and not nought: #1017 keeps such a session timed until it ends.
 *
 * That is the leader's count. Every other car finishes at its first crossing after the leader's,
 * not after the clock, so a car that crosses between the clock reaching nought and the leader taking
 * the flag runs a lap more (#1027). So the count is also taken to the flag, {@link flagIn}: the laps
 * until the car's first crossing at or after it, which is the time to the flag and the time already
 * run in the lap over a lap, rounded up. A crossing level with the leader's is the car's last, the
 * car being just behind it on the road, which is what the rounding up says and what the clock's
 * truncation and one would not. The count is the larger of the two, so the flag never takes a lap
 * away, and adds none to the leader's own count or to any car's once the chequered flag is out. After
 * the clock it is what keeps a car that will reach the line ahead of the leader from being told the
 * lap it is on is its last.
 *
 * Nought in a timed session until a lap has been timed, which no reading draws as a count: the
 * session page and the fuel page draw {@link NO_VALUE} by {@link lapsLeftIsKnown} instead, and the
 * margin forced onto laps has no end to measure against, as in a race with no laps left.
 *
 * What the timed form costs is the iRacing race given both a lap count and a time limit, which
 * {@link isTimedSession} calls timed, and which now counts its laps from the clock rather than from
 * its count: more laps than it has where the count ends it first, which is the side a fuel figure
 * can afford to be wrong on. Nothing OpenDash reads today tells the two apart.
 */
export const lapsLeft = (): Expr => {
  const lap = timedLapSeconds();
  const run = min(lapTimeRun(), lap);
  const toClock = iff(gt(sessionTimeLeft(), num(0)), add(truncate(div(add(sessionTimeLeft(), run), lap)), num(1)), num(1));
  const toFlag = ceiling(div(add(flagIn(), run), lap));
  return iff(isTimedSession(), iff(gt(lap, num(0)), max(toFlag, toClock), num(0)), isnull(game('RemainingLaps'), num(0)));
};

/** {@link lapsLeft} as the session page draws it: the count, or {@link NO_VALUE} until there is one. */
export const lapsLeftText = (): Expr => iff(lapsLeftIsKnown(), fmt(lapsLeft(), '0'), str(NO_VALUE));

/**
 * Whether the session page draws its laps left: while there are laps left, and in a timed session
 * before the first lap has been timed, where the field says it has no count yet rather than going.
 */
export const lapsLeftShown = (): Expr => ncalc.or(gt(lapsLeft(), num(0)), not(lapsLeftIsKnown()));

/**
 * How much of the lap in progress the car has already run, from nought at the line to one at the
 * next crossing, and nought where nothing says.
 *
 * Measured by distance, `TrackPositionPercent`, because a lap through the pit lane or behind the
 * safety car takes longer than the best lap to cover the same ground, and the time run over the
 * best lap would call it further round than it is and take too much off the laps a fuel figure
 * counts. The time run over the lap is a bound on it all the same, the smaller of the two being
 * taken: the lap count and the lap time go back to nought at the line together, and the distance
 * need not arrive there on the same frame, which in Assetto Corsa, whose spline can start a little
 * way off the timing line, can be some way round. Just past the line the distance can still read
 * the end of the lap before, where the time already reads its start; just before it the distance
 * can read the start where the time reads the end, and the smaller of the two, nought there, counts
 * the lap whole, which is the side a fuel figure can afford to be wrong on. A game that publishes no
 * position gives SimHub's `-1`, read as nought for the same reason.
 *
 * The bounds are the double `0.0`, here and in {@link lapsToRun}. NCalc's `max` and `min` answer in
 * the type of their left operand, so with the Int32 `0` there the fraction was rounded to nought
 * below half a lap and to a whole lap above it. On the VM, a car half way round its last lap after
 * the clock with 0.71 laps of fuel read `−0` in red with Refuel 1.0, and a lap-counted margin
 * stepped by a whole lap at half distance.
 */
const lapFractionRun = (): Expr =>
  iff(
    gt(timedLapSeconds(), num(0)),
    max(real(0), min(isnull(game('TrackPositionPercent'), real(0)), div(lapTimeRun(), timedLapSeconds()))),
    num(0),
  );

/**
 * The laps the car still has to run as a fuel figure counts them: {@link lapsLeft} less the part of
 * the lap in progress already run, never below nought.
 *
 * {@link lapsLeft} is a count, and counts the lap the car is on whole, as Laps left draws it. The
 * tank is read as it is now, with that part of the lap already burnt, so a fuel figure that counted
 * the lap whole as well asked for the part already run a second time: at pit entry, the end of a
 * lap, nearly a lap's fuel more than the race needs, and a car with less than a lap to spare at the
 * flag told in red to stop for it. After a timed race's clock, half way round the last lap, the
 * half lap still to run is what the tank has to cover, and not the whole lap (#1024).
 */
const lapsToRun = (): Expr => max(real(0), sub(lapsLeft(), lapFractionRun()));

/**
 * Fuel to add: what the {@link lapsToRun} will burn, less what is in the tank; never negative. An
 * estimate, labelled `To add`, and not the order the pit service is set to, which is
 * {@link pitRefuel}.
 *
 * The floor is the double `0.0`, as it is in the two clamps above: NCalc's `max` answers in its left
 * operand's type, so `max(0, …)` rounds the litres to a whole one and the reading ends in `.0` under a
 * format that draws tenths, and a shortfall under half a litre draws `0.0` beside a margin in red.
 * #831.
 */
export const fuelToAdd = (): Expr => max(real(0), sub(mul(lapsToRun(), fuelPerLap()), fuel()));

/**
 * Whether there is a fuel to add: a lap has said what one costs, which is {@link fuelIsSettled}, and
 * there is a count of laps to multiply it by. Without the second, a timed race whose first lap the
 * sim gave no time for drew `0.0` to add from a count of nought.
 */
export const fuelToAddIsSettled = (): Expr => and(fuelIsSettled(), lapsLeftIsKnown());

/**
 * The fuel to add as the fuel page draws it: to a tenth up to `999.9`, in whole units above that,
 * and {@link NO_VALUE} until {@link fuelToAddIsSettled}.
 *
 * Whole units above a thousand, because the field is cut for {@link CHARS.fuel}, four digit cells
 * and the point, and a timed race's laps left reach that early in a long one: a day at Daytona is
 * 910 laps, and at three litres and a half a lap the first stint was told to add `3085.0`, which WPF
 * drew as `3085.` with the last glyph cut. Nobody fills a tank to a tenth of a figure that size, and
 * `3085` keeps the field inside its cells up to 9999.
 */
export const fuelToAddText = (): Expr =>
  iff(fuelToAddIsSettled(), iff(lt(fuelToAdd(), num(999.95)), fmt(fuelToAdd(), '0.0'), fmt(fuelToAdd(), '0')), str(NO_VALUE));

/**
 * The widest reading {@link fuelToAddText} draws, `999.9`: the four digit cells and the point of
 * {@link CHARS.fuel}, a cell wider than the whole `9999` above it. Declared because the count it
 * multiplies asks whether the session is a race (#1027), and a text that reads the session's name
 * declares what it draws (#1029).
 */
export const FUEL_TO_ADD_WIDEST = '999.9';

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

/**
 * The least quantity band D's fuel page draws whole: from here up a quantity reads `10`, `13`, `30`,
 * and below it `9.9`, `9.5`, one decimal. In the driver's unit, whichever it is, since the figure
 * is read in it: ten litres and ten gallons are both where a tenth stops being worth a cell.
 */
export const BAND_FUEL_WHOLE_FROM = 10;

/**
 * The least value whose one-decimal reading is {@link BAND_FUEL_WHOLE_FROM}: `format` rounds a half
 * away from zero, so 9.95 is drawn `10.0` and 9.94 `9.9`. The test is on this rather than on 10, so
 * that a 9.96 reads `10` and never `10.0`, which would be a third whole digit cell's worth of figure
 * in the decimal form. A double literal, so that nothing compares or formats as an Int32. #899.
 */
const BAND_FUEL_DECIMAL_BELOW = BAND_FUEL_WHOLE_FROM - 0.05;

/**
 * A fuel quantity as band D's fuel page draws it: whole from 10 up, one decimal below, and
 * {@link NO_VALUE} where the sim gives nothing.
 *
 * The author's ruling of 2026-10-09 on #899. The fuel time's third minute digit cost the Porsche's
 * and the AiM's feet between one and three fields at 850 and 800 wide, and the room is won back on
 * the quantities rather than by shedding: a tank of `32.67` reads `33`, which is what a driver
 * reads off it on a straight anyway, and the tenths come back below ten, where they are the
 * difference between one more lap and none. Every litre or gallon reading of the page goes through
 * this one function (the tank, the refuel, the per lap and the last lap) and no other surface does:
 * the fuel module, pit view, the pop-ups and the cards keep their own decimals.
 *
 * `value` is any figure already in the driver's unit. The page's own gates and fallbacks go around
 * or inside it, as `iff(absent, str(NO_VALUE), bandFuelQuantity(pitRefuel()))` would for a pit
 * service order read through a converted helper. {@link bandFuelQuantityDrawn} is the width it
 * draws, for a unit placed after it, and {@link bandFuelQuantityWidest} the longest reading.
 */
export const bandFuelQuantity = (value: Expr): Expr =>
  iff(isNull(value), str(NO_VALUE), iff(lt(value, real(BAND_FUEL_DECIMAL_BELOW)), fmt(value, '0.0'), fmt(value, '0')));

/** How wide {@link bandFuelQuantity} draws `value` whose whole form has at most `digits` digits. */
export const bandFuelQuantityDrawn = (value: Expr, digits: number): DrawnFigure =>
  drawnEither(
    isNull(value),
    drawnText(NO_VALUE),
    drawnEither(lt(value, real(BAND_FUEL_DECIMAL_BELOW)), drawnFigure({ value, digits: 1, decimals: 1 }), drawnFigure({ value, digits })),
  );

/**
 * The longest reading {@link bandFuelQuantity} draws for a quantity of at most `digits` whole
 * digits, and the cells that hold it: `999` for a tank, against the `9.9` of the decimal form, and
 * the decimal form itself where the whole one is two digits, since a point takes a cell of its own.
 */
export function bandFuelQuantityWidest(digits: number): { widest: string; chars: Chars } {
  if (digits < 2) throw new RangeError('a band D fuel quantity is drawn whole from 10, so its whole form has two digits at least');
  return digits > 2 ? { widest: '9'.repeat(digits), chars: { digits, specials: 0 } } : { widest: '9.9', chars: { digits: 2, specials: 1 } };
}

/** So the timed margin below reads as minutes rather than as a division by a bare 60. */
const SECONDS_PER_MINUTE = 60;

/**
 * Whether there is a race to measure the tank against: a length to run, in a session that is a race.
 *
 * Two questions, and the first one alone is not enough. A length, because a session with no end has
 * no flag for the tank to reach and the subtraction would draw the whole of the range as spare, and
 * the length asked for is the one the page is counting down -- the time in a timed session and the
 * laps remaining in a lap-counted one -- since either may be missing while the other is there. A
 * timed session asks for a lap to have been timed as well, by {@link lapsLeftIsKnown}, because its
 * margin is counted in the laps {@link lapsLeft} predicts from that lap, and until there is one the
 * race has a clock but no laps to measure the tank against.
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
 * The fuel page's `To add` is the counter-precedent and stays as it is: it reads the race's
 * remaining laps in practice too, but it is drawn in caution amber as an instruction to the crew
 * rather than as a verdict, so a figure of no use in practice is not a figure that alarms there.
 */
const raceHasAnEnd = (): Expr =>
  and(isRace(), iff(showsTimeLeft(), and(isTimedSession(), lapsLeftIsKnown()), gt(lapsLeft(), num(0))));

/**
 * Whether there is a margin to draw: a lap has said what one costs, and the race has an end.
 *
 * {@link fuelIsSettled} is the gate every other estimate on the fuel page reads, and this is that
 * gate with the session's own added, so the margin appears at the same crossing as the estimate it
 * is derived from rather than a frame before or after it.
 */
export const fuelToEndIsSettled = (): Expr => and(fuelIsSettled(), raceHasAnEnd());

/**
 * The margin in laps: the range in the tank less the laps the race still has to run, which are
 * {@link lapsToRun}, the laps the Refuel counts, and not the whole laps Laps left draws. At the line
 * the two are the same; mid-lap the margin is more than the estimate less the laps left by the part
 * of the lap already run, because that part has already been burnt out of the range.
 */
const fuelToEndLaps = (): Expr => sub(fuelLapsLeft(), lapsToRun());

/**
 * The timed form: the same laps, read in minutes at the lap {@link lapsLeft} counts them at.
 *
 * Not the range's time less the time left, which is what it was. A timed race does not end when its
 * clock does: the leader finishes the lap the clock runs out on, and every other car at its first
 * crossing after the leader's, so the time still to run is the clock and the rest of that lap, and
 * a lap more for a car that reaches the line between the clock and the leader (#1027). A tank that
 * lasted 90 s with 60 s on the clock and a 110 s lap read `+1` in the green and ran dry 20 s from
 * the flag, and once #1017 kept the session timed after the clock, the margin over the last lap was
 * the whole range less nothing, green whatever was in the tank (#1024).
 *
 * The laps are {@link lapsToRun}, the laps {@link lapsLeft} counts less the part of the lap already
 * run, which Refuel multiplies, and the range is `Fuel_RemainingLaps` and not `Fuel_RemainingTime`,
 * which SimHub makes from the same laps of fuel at the average of the last three laps rather than
 * at the lap the count is made at. With the clock at nought and half a lap still to run, the margin
 * is the range less that half lap.
 *
 * The two fields are one subtraction read two ways, and agree about whether the tank makes it
 * wherever SimHub computes the laps of fuel itself, as `Fuel` over `Fuel_LitersPerLap`, which is
 * every game reader that publishes no estimate of its own (`DataCorePlugin` 9.12.6 prefers a
 * reader's estimate where there is one). The Refuel is drawn to a tenth, so a shortfall under
 * 0.05 draws `0.0` beside a margin in the slower colour.
 */
const fuelToEndMinutes = (): Expr => div(mul(fuelToEndLaps(), timedLapSeconds()), num(SECONDS_PER_MINUTE));

/**
 * Fuel to the end of the race, signed: how much more than the rest of the race the tank holds.
 *
 * The one fuel question in a race is whether the tank makes it, and the fuel page had every term of
 * that subtraction on it -- the range, the estimated laps -- while the laps left sat on the session
 * page, so the driver did the arithmetic mid-corner. [ADR 0009](../../../../docs/decisions/0009-does-the-plugin-compute.md)
 * allows a subtraction of two published values in an expression, and this is that subtraction. #387.
 *
 * One subtraction, `Fuel_RemainingLaps` less {@link lapsToRun}, read in one of two units chosen the
 * way the session page chooses which counter to draw: a timed session reads it in minutes, at the
 * lap its laps left are counted at, and a lap-counted one in laps. The switch is
 * {@link showsTimeLeft}, so the margin counts in whatever the face beside it is counting in and the
 * driver is never asked to notice that the unit changed for a reason of its own. The unit is
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
 * digit cells and a special, where the longest timed reading, an empty tank against a full day, is
 * about the clock and up to three laps, the third the one a car behind the leader can run while it
 * waits for the flag (#1027), `−14xx` min at any lap a race is run at and five cells with the sign.
 * Both are inside {@link CHARS.margin}; a `widest` is declared because a monospaced box is cut from
 * its budget and the fit tests measure what the item says it draws, so a field left with `+1.4` on
 * it is a field measured at three cells for a reading that takes six.
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
 * Zero counts as reaching the flag, a tank that holds exactly the rest of the race, and the absence
 * is drawn in the primary text: a green `--` claims the tank makes it before anything knows.
 */
export const fuelToEndColour = (): Expr =>
  iff(
    ncalc.not(fuelToEndIsSettled()),
    str(dsTokens.color.text.primary),
    iff(lt(fuelToEnd(), num(0)), str(dsTokens.purpose.delta.slower), str(dsTokens.purpose.delta.faster)),
  );

/**
 * The widest reading {@link lapOfTotal} is budgeted for, and what its box is measured against: a
 * three-digit lap of a three-digit race. Its sample is the short end, and a field measured by
 * `12 / 43` passes every fit test while `12 / 120` is clipped on the screen.
 */
export const LAP_OF_TOTAL_WIDEST = '999 / 999';

/**
 * The widest lap count {@link CHARS.lap} is budgeted for, and what a lap field declares as its
 * `widest` so the fit tests measure the long end rather than the `12` of its sample. #596.
 */
export const LAP_WIDEST = '999';

/** The widest `/ 30` a lap count is followed by: the race's length, in three digits. #596. */
export const LAP_TOTAL_WIDEST = '/ 999';

/** The widest incident count, `x` and all, that {@link CHARS.incidents} is budgeted for. #596. */
export const INCIDENTS_WIDEST = '999x';

/**
 * `12 / 43`: the lap you are on, out of the race's length, and `12` alone where the race has none.
 *
 * The lap you are *on* and not the lap you have finished, because that is the number a driver says
 * out loud. SimHub's `CurrentLap` is already 1 on the opening lap, so it needs no adjusting.
 *
 * The length is `TotalLaps` where {@link hasLapTotal} says it is one, and there is no other. This
 * used to predict a timed race's length as laps done plus `RemainingLaps`, on the belief that SimHub
 * makes that prediction; it does not. SimHub sets `RemainingLaps` for every game to `TotalLaps` less
 * `CompletedLaps`, never below nought (GameReaderCommon 9.12.6, `GameManagerBase`), so the sum was
 * `TotalLaps` again: the leader's laps in a timed iRacing race, `12 / 14` with half an hour to run, and
 * nought for a car on the lead lap (#989). A timed race and an open session draw the lap alone, as
 * the bar and the headers do.
 */
export const lapOfTotal = (): Expr =>
  iff(hasLapTotal(), concat(fmt(currentLap(), '0'), str(' / '), fmt(totalLaps(), '0')), fmt(currentLap(), '0'));

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

/**
 * What the running sim can report, read from the plugin with iRacing's own answer as each one's fallback.
 * ADR 0021, #1101.
 *
 * A dashboard never names a game. Where a question has a different answer on another sim and no `isnull()`
 * over the properties already read can tell "not reported" from a value, the plugin answers it from its
 * table (`plugin/OpenDash/SimCapabilities.cs`) and publishes the answer; {@link CAR_HAS_PIT_LIMITER} in
 * `contract.ts` lists the four. Each helper here reads one, and falls back on the reading OpenDash made
 * before the plugin answered it, which is iRacing's: so with no plugin, with no game running and on a game
 * the table does not know, a package draws exactly what it drew before.
 *
 * Every answer is a boolean, `true` or `false` as a raw telemetry boolean is, and so is every fallback, so
 * the caller reads one shape whichever answered.
 */
export const sim = {
  /**
   * The car has a pit limiter to engage. Without the plugin, iRacing's test: the car's own limiter control,
   * `dcPitSpeedLimiterToggle`, is there at all. A car with no limiter has no such control, where SimHub's
   * `PitLimiterOn` reads 0 for it exactly as for a limiter switched off. #558.
   */
  carHasPitLimiter: (): Expr => setting.reported(CAR_HAS_PIT_LIMITER, not(isNull(raw('dcPitSpeedLimiterToggle')))),
  /**
   * The limiter is engaged, as the sim reports it. Without the plugin, SimHub's `PitLimiterOn`, which is
   * iRacing's limiter bit and, on Assetto Corsa, the pit lane rather than the limiter. Not yet read by any
   * binding: {@link pitLimiterOn} is, until #558 moves the pit family onto this.
   */
  pitLimiterOn: (): Expr => setting.reported(PIT_LIMITER_ON, pitLimiterOn()),
  /**
   * The sim reports the car's damage. Without the plugin, false: iRacing fills SimHub's `CarDamage1..5` with
   * zeros, which is no report at all. Not yet read: #582 gates the damage module on it.
   */
  damageReported: (): Expr => setting.reported(DAMAGE_REPORTED, 'false'),
  /**
   * The driver is in the car, physics running. Without the plugin, {@link inTheCar}, with the caller's answer
   * for a sim that says nothing. Not yet read: #561 moves `inTheCar`'s callers onto this.
   */
  driverInCar: (ifUnknown = true): Expr => setting.reported(DRIVER_IN_CAR, inTheCar(ifUnknown)),
};

export const throttle = (): Expr => isnull(game('Throttle'), num(0));
export const brake = (): Expr => isnull(game('Brake'), num(0));
export const clutch = (): Expr => isnull(game('Clutch'), num(0));
/**
 * The wheel angle in radians, clockwise as the driver sees it, so that a right turn is positive.
 *
 * iRacing publishes `SteeringWheelAngle` the other way round: positive is a left turn, counter-clockwise,
 * and SimHub's reader passes the raw value through without touching it. The reading is turned round here
 * and only here, so the dial's mark and the pit wall's trace take the same convention from one place.
 * `docs/research/simhub-dash-format.md` records how the sign was established (#1057).
 */
export const steering = (): Expr => sub(num(0), isnull(raw('SteeringWheelAngle'), num(0)));
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
 * The session names `SessionTypeName` is written with, which is what a box that draws it is cut to.
 *
 * SimHub passes iRacing's own words through. Its `IRacingManager.GD_SessionTypeName` (ICarsReader,
 * decompiled) answers the `SessionType` of the entry in `SessionInfo.Sessions` the telemetry's
 * `SessionNum` points at, and falls back to the weekend's `EventType` where there is none, so the
 * list is both: the session types an iRacing weekend is made of, as the pit wall header listed them
 * before this list existed, and the event types a weekend can be. They are written in iRacing's
 * case, which is the case they are drawn in.
 *
 * `Open Practice` and `Lone Practice` are here on the word of the fuel margin's tests and of #1029
 * rather than of a recording, which has only ever shown `Lone Qualify` and `Race`. A name missing
 * from this list is not measured by anything that cuts its box from the list, the AiM's above all,
 * so a name that might be written is listed rather than left out.
 */
export const SESSION_NAMES: readonly string[] = [
  'Offline Testing',
  'Practice',
  'Open Practice',
  'Lone Practice',
  'Open Qualify',
  'Lone Qualify',
  'Qualify',
  'Warmup',
  'Heat',
  'Consolation',
  'Race',
  'Test',
  'Time Trial',
];

/**
 * The widest of {@link SESSION_NAMES}, in every face the name is drawn in and in capitals too, which
 * is what every text bound to {@link sessionType} declares as its `widest`. A session field cut to
 * `Race` drew `Lone Qualify` as `Lone Qua` (#1029).
 */
export const SESSION_NAME_WIDEST = 'Offline Testing';

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
 * One of three values by the driver's temperature unit, read through {@link temperatureUnit} so that
 * a unit SimHub does not say is Celsius. The tyre thresholds on the cards and on the second screens
 * both pick by this; each read `TemperatureUnit` bare before.
 */
export const perTemperatureUnit = (f: Expr, k: Expr, c: Expr): Expr => iff(eq(temperatureUnit(), str('Fahrenheit')), f, iff(eq(temperatureUnit(), str('Kelvin')), k, c));
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

/**
 * SimHub's own ratio from litres to gallons, `GameManagerBase.GetFuelConvertRatio` in 9.12.6. The
 * tank a face draws beside the pit order is converted by it, so the order is converted by the same
 * figure rather than by a more exact one that would disagree with the tank in the last digit.
 */
export const LITRES_TO_GALLONS = 0.264172;

/** True where the sim publishes no pit fuel order, which is every sim but iRacing. */
export const pitRefuelIsAbsent = (): Expr => isNull(raw('PitSvFuel'));

/**
 * The fuel the pit service is set to add, in the unit the driver reads the tank in (#604).
 *
 * iRacing publishes `PitSvFuel` in litres whatever the display unit, as it does `FuelLevel` (the
 * emulator's catalogue carries the var header's `l`), and SimHub passes raw telemetry through
 * unconverted, where it converts `Fuel` to gallons on a gallons profile. Pit view used to draw the
 * litres beside a unit bound to `FuelUnit`, so a 40 litre order read `40.0 gal`, and band D drew the
 * same litres with no unit beside a tank in gallons. Both read this now, and the face says
 * {@link fuelUnit} after it as it does after the tank.
 *
 * This is the pit service order and not {@link fuelToAdd}, the estimate of what the laps left will
 * burn; the fuel page labels that one `To add` and the pit pages label this one `Refuel`, so the two
 * are never drawn under the same word.
 */
export const pitRefuel = (): Expr => {
  const litres = isnull(raw('PitSvFuel'), num(0));
  return iff(fuelInGallons(), mul(litres, num(LITRES_TO_GALLONS)), litres);
};
export const isInPitLane = (): Expr => gt(isnull(game('IsInPitLane'), num(0)), num(0));

// --- The pit lane and the spotter, one body each for the faces, the strips and the box -----------

/** The limiter is engaged. Null-safe, so a sim that publishes nothing draws no limiter anywhere. */
export const pitLimiterOn = (): Expr => eq(isnull(game('PitLimiterOn'), num(0)), num(1));

/** A car alongside on `side`, as SimHub folds iRacing's `CarLeftRight` into two booleans. */
export const spotterCar = (side: 'Left' | 'Right'): Expr => gt(isnull(game(`SpotterCar${side}`), num(0)), num(0));

/**
 * What {@link pitLimit} reads when the sim publishes no limit: a speed nothing reaches in either
 * unit, so the comparison below never fires on it and a readout can tell it from a real limit.
 */
export const NO_PIT_LIMIT = 999;

/**
 * The pit lane limit, in the driver's own unit.
 *
 * `PitLimiterSpeed` is published through `KmhToLocalSpeedUnit` (GameManagerBase.cs:1339), and
 * `SpeedLocal` is `SpeedKmh` or `SpeedMph` by the same setting (GameManagerBase.cs:1492), so the two
 * are always in one unit and are compared as they are. The metres-per-second twin,
 * `PitLimiterSpeedMs`, carries `[DoNotExpose]` and DataCorePlugin skips it, so it is not a property
 * at all: the box compared against it, read the null as "no limit" and never lit its speeding
 * picture, while the strip compared the local pair and did.
 */
export const pitLimit = (): Expr => isnull(game('PitLimiterSpeed'), num(NO_PIT_LIMIT));

/** The limit as the faces write it: the number, or the house's no-data dashes where there is none. */
export const pitLimitText = (): Expr => iff(lt(pitLimit(), num(NO_PIT_LIMIT)), fmt(pitLimit(), '0'), str(NO_VALUE));

/**
 * How far over the limit, in the driver's unit, before it counts. A limiter holds the car a
 * fraction over its figure, and this is what stops a light strobing as it settles. One unit in
 * either: a mile an hour lets the imperial driver by 1.6 km/h where the metric one gets 1, which is
 * the same order as the limiter's own settling and is not worth a second spelling.
 */
export const PIT_SPEEDING_MARGIN = 1;

/**
 * Over the pit lane limit, wherever the car is. A limit of zero or none is never exceeded, so a
 * track whose limit the sim does not say reads as "not speeding" rather than "always speeding".
 */
export const overPitLimit = (): Expr => and(gt(pitLimit(), num(0)), gt(speed(), add(pitLimit(), num(PIT_SPEEDING_MARGIN))));

/**
 * Speeding in the pit lane. SimHub publishes no speeding property of any kind, so it is composed:
 * in the lane and over the limit. The strip's effect and the box's picture both read this one
 * expression, so the two cannot light at different speeds.
 */
export const pitSpeeding = (): Expr => and(isInPitLane(), overPitLimit());

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
 * a quantity from another page. The cap is the double `100.0`, since `min` answers in its left
 * operand's type and an Int32 there would round the gauge to whole percent. #831.
 */
export const pitServiceProgress = (): Expr =>
  iff(and(isInPitLane(), gt(lastPitDuration(), num(0))), min(real(100), mul(num(100), div(inPitSeconds(), lastPitDuration()))), num(0));

// --- Lap history and deltas ---------------------------------------------------------------------

export const PREVIOUS_LAP_SLOTS = 10;
/** `PersistantTrackerPlugin.PreviousLap_NN`, addressed by the row's repeat index. */
export const previousLap = (slotExpr: Expr): Expr => propByName(concat(str('PersistantTrackerPlugin.PreviousLap_'), fmt(slotExpr, '00')));
/**
 * The same lap's delta to your best, in seconds.
 *
 * SimHub names the property `_DeltaToSessionBest`, and it means the player's own best lap of the
 * session: the tracker writes it as the slot's time less `BestLapTime` (`PersistantTrackerPlugin`,
 * 9.12.6), which is the time Lap times draws as `Your best`. The field's best, which Lap times and
 * every other page call the session best, is {@link sessionBestLap} and a different number, so a
 * reading of this is labelled against your best and never against the session best (#1030).
 */
export const previousLapDelta = (slotExpr: Expr): Expr =>
  propByName(concat(str('PersistantTrackerPlugin.PreviousLap_'), fmt(slotExpr, '00'), str('_DeltaToSessionBest')));

/**
 * Whether the lap in a slot was driven in this session, by SimHub's own word for it.
 *
 * The ring is the lap database's and not the session's. `PersistantTrackerPlugin` fills its ten
 * slots from `DBManager.GetLastLaps(track, car, 10)`, which is the last ten laps of this car at this
 * track from any day, and reloads them whenever SimHub or the game starts or a session restarts.
 * So a driver back at the same track in the same car finds yesterday's laps in the ring before the
 * first lap of today is done, and every reading built from the ring would be built from them
 * (#1011). `PreviousLap_NN_IsCurrentSession` is 1 only for a lap found in this session's own lap
 * history, and it is written with the slot's time whenever the ring is filled.
 *
 * An unpublished flag reads as an earlier session's lap, because a lap that cannot be placed in
 * this session is the one thing the readings built on the ring must not count.
 */
export const previousLapIsThisSession = (slotExpr: Expr): Expr =>
  gt(isnull(propByName(concat(str('PersistantTrackerPlugin.PreviousLap_'), fmt(slotExpr, '00'), str('_IsCurrentSession'))), num(0)), num(0));

/**
 * Whether a slot holds a lap of this session: a time, and SimHub's word that it was driven in it.
 *
 * Both, because neither says it alone. A slot the database has no lap for is written `00:00:00`,
 * but its flag is left at whatever the last lap in it was, so the flag of an emptied slot can
 * still read 1; and a slot with a time may hold a lap of another session. Every reading built on
 * the ring asks this rather than `hasTime` of the slot.
 */
export const hasSessionLap = (slotExpr: Expr): Expr => and(previousLapIsThisSession(slotExpr), hasTime(previousLap(slotExpr)));

/**
 * A signed delta to `places` decimals where `chars` holds it, and to fewer where it does not:
 * `+1.03`, `+123.5` and `+1234` in {@link CHARS.delta}'s five cells at two places.
 *
 * A box cannot change its cells at runtime, so a delta whose whole part grows past what its cells
 * were cut for loses its last digit to WPF instead, and `+123.45` in five cells is drawn `+123.4`,
 * a figure the sim never published. The lap that carried a stop, a tow or a repair is a minute or
 * three off your best and off the lap before it, and its sectors with it, so the deltas that
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
 * is shown. And every one of the five is required to be a lap of this session, because the ring
 * starts a session full of the laps the database kept from earlier ones
 * ({@link previousLapIsThisSession}): the average waits for the fifth lap of today rather than
 * reading yesterday's.
 */
export const average5 = (): Expr => {
  // From slot zero, which is the lap just completed. Starting at one averaged laps two to six and
  // left the newest out, so the number moved a lap late; `modules/lapHistory.ts` reads the same
  // slots and says so where it draws row one.
  const slots = Array.from({ length: AVERAGE_LAPS }, (_, i) => num(i));
  const seconds = slots.map((slot) => secondsOf(previousLap(slot)));
  return iff(
    and(...slots.map((slot) => hasSessionLap(slot))),
    lapReading(secondsToTimespan(div(add(...seconds), num(AVERAGE_LAPS)))),
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
 * different laps. An unknown value reads the `session` reference, which is the default.
 *
 * The `session` reference is SimHub's live delta to its session best, which is the player's own best
 * lap of this session and not the field's: SimHub's lap tracker follows the player's laps and nobody
 * else's. So it is drawn as the delta to your best, the lap Lap times draws under that name, and the
 * session best a page draws beside it in purple is the field's (#1030).
 */
export const referenceDelta = (): Expr =>
  iff(setting.deltaReferenceIs('alltime'), allTimeBestDelta(), iff(setting.deltaReferenceIs('lastlap'), lastLapDelta(), sessionBestDelta()));

/** The caption of a delta to the player's own best lap of the session, which is what SimHub's session best is. */
export const YOUR_BEST_CAPTION = 'vs your best';

/**
 * The label that says which reference the delta is against.
 *
 * "vs last lap" rather than "vs previous": the lap review already uses "vs previous" for a finished
 * lap against the one before it, which is a different comparison drawn at a different moment.
 *
 * "vs your best" rather than the canvas's "vs session best", because the reference is the player's
 * own best lap of the session (see {@link referenceDelta}), and "session best" is what Lap times,
 * Sectors and the pit wall call the field's. On a face that drew both, the same two words named two
 * laps most of a second apart (#1030).
 */
export const referenceLabel = (): Expr =>
  iff(setting.deltaReferenceIs('alltime'), str('vs all-time best'), iff(setting.deltaReferenceIs('lastlap'), str('vs last lap'), str(YOUR_BEST_CAPTION)));

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
