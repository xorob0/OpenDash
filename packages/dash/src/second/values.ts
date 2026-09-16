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
import { MINUS, type Chars } from '../design/metrics.ts';
import { flagBox, setting } from '../contract.ts';
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
  /** `-0.21` */
  delta: { digits: 5, specials: 1 } as Chars,
  /** `-5.886` */
  relativeGap: { digits: 6, specials: 1 } as Chars,
  /** `+1L` or `+12.6` */
  gap: { digits: 6, specials: 1 } as Chars,
  /** `28.41` */
  sector: { digits: 5, specials: 1 } as Chars,
  /** `24` */
  position: { digits: 2, specials: 0 } as Chars,
  /** `123`, bare: the hash belongs to the label. */
  carNumber: { digits: 4, specials: 0 } as Chars,
  /** `0:42:15` */
  clock: { digits: 6, specials: 2 } as Chars,
  /** `08:46` */
  minutesClock: { digits: 4, specials: 1 } as Chars,
  /** `299` */
  speed: { digits: 3, specials: 0 } as Chars,
  /** `12,450` */
  rpm: { digits: 6, specials: 1 } as Chars,
  /** `38.4` */
  fuel: { digits: 4, specials: 1 } as Chars,
  /** `2.84` */
  consumption: { digits: 4, specials: 1 } as Chars,
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
};

/** True when a TimeSpan holds a real lap time rather than the unset `00:00:00`. */
export const hasTime = (ts: Expr): Expr => gt(timespanToSeconds(isnull(ts, num(0))), num(0));

/** A lap time as `m:ss.fff`, or the placeholder of the same shape when it was never set. */
export const lapTime = (ts: Expr, decimals = 3): Expr => iff(hasTime(ts), toShortTime(ts, decimals, false, true), str(noTime(decimals)));

/** A sector time as `ss.fff`, or `--` when it was never set. */
export const sectorTime = (ts: Expr, decimals = 3): Expr => iff(hasTime(ts), toShortTime(ts, decimals, false, false), str(NO_VALUE));

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

/** The car ahead on track (-1) or behind (1). */
export const neighbour = (offset: number): Expr => aheadBehind(num(offset));

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
};

/** The leaderboard index of the car holding the session's best lap. */
export const sessionBestRow = (): Expr => bestLapPosition();

/** The session's best lap, which is the best lap of that car. */
export const sessionBestLap = (): Expr => driver('bestlap', sessionBestRow());

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
export const carNumber = (idx: Expr): Expr => isnull(driver('carnumber', idx), str(''));

/**
 * A driver name cut to the three-letter code the narrow drawings show, upper-cased.
 *
 * The cut is made in NCalc rather than by the renderer, the way `chipText` cuts a class name: WPF
 * has no ellipsis to give, it clips, so a name longer than its box has nowhere to go. Both the
 * lists and the opponents page draw this form, the one in a narrow column and the other in the
 * 64 px box the canvas fixes for it whatever the density.
 */
export const driverCode = (idx: Expr): Expr => ucase(left(isnull(carName(idx), str('')), 3));
export const carClass = (idx: Expr): Expr => driver('carclass', idx);

/** The position a table shows, overall or in class per the plugin's PositionMode. */
export const carPosition = (idx: Expr): Expr =>
  iff(eq(setting.positionMode(), str('class')), isnull(driver('classposition', idx), num(0)), isnull(driver('position', idx), num(0)));

/** Places gained since the start, signed; 0 when the sim does not track it. */
export const carRankChange = (idx: Expr): Expr => isnull(driver('positiongain', idx), num(0));

/** The race gap, which SimHub already formats as "+1L" when a car is lapped; "Lead" for the leader. */
export const carRaceGap = (idx: Expr): Expr =>
  iff(eq(isnull(driver('position', idx), num(0)), num(1)), str('Lead'), isnull(driver('gaptoleadercombined', idx), str(NO_VALUE)));

/**
 * The gap to the player on track, signed, three decimals: a car ahead reads `−5.886` and a car
 * behind `+0.722`. The minus is the typographic one, which `signed` substitutes for the hyphen
 * .NET's formatter writes.
 */
export const carRelativeGap = (idx: Expr): Expr =>
  iff(ncalc.isNull(driver('relativegaptoplayer', idx)), str(NO_VALUE), signed(driver('relativegaptoplayer', idx), '0.000'));

/**
 * The interval to the car in front: the difference of the two gaps to the leader. In class mode
 * the row above belongs to the same class only when the classes do not interleave, so the value
 * is the on-leaderboard interval either way; the leader's row is empty.
 */
export const carInterval = (idx: Expr): Expr => {
  const ahead = driver('gaptoleader', sub(idx, num(1)));
  const here = driver('gaptoleader', idx);
  return iff(and(gt(idx, num(1)), ncalc.not(ncalc.isNull(ahead)), ncalc.not(ncalc.isNull(here))), signed(sub(here, ahead), '0.0'), str(''));
};

export const carLastLap = (idx: Expr): Expr => lapTime(driver('lastlap', idx));
export const carBestLap = (idx: Expr): Expr => lapTime(driver('bestlap', idx));
export const carIsSessionBest = (idx: Expr): Expr => and(eq(idx, sessionBestRow()), gt(sessionBestRow(), num(0)));
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

// --- Session, car and environment -----------------------------------------------------------

export const currentLap = (): Expr => isnull(game('CurrentLap'), num(0));
/** Laps completed, which is one behind the lap in progress for as long as a lap is in progress. */
export const completedLaps = (): Expr => isnull(game('CompletedLaps'), num(0));
export const totalLaps = (): Expr => isnull(game('TotalLaps'), num(0));
export const sessionTimeLeft = (): Expr => timespanToSeconds(game('SessionTimeLeft'));
/** iRacing reports a week of time left when a session is not timed. */
export const UNTIMED_SECONDS = 86400;
export const isTimedSession = (): Expr => and(gt(sessionTimeLeft(), num(0)), lt(sessionTimeLeft(), num(UNTIMED_SECONDS)));

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
export const fieldSize = (): Expr => iff(eq(setting.positionMode(), str('class')), classOpponentCount(), opponentCount());

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
export const fuelTimeLeft = (): Expr => timespanToSeconds(computed('Fuel_RemainingTime'));
export const fuelLastLap = (): Expr => isnull(computed('Fuel_LastLapConsumption'), num(0));
export const fuelThisLap = (): Expr => isnull(computed('Fuel_CurrentLapConsumption'), num(-1));
export const lapsLeft = (): Expr => isnull(game('RemainingLaps'), num(0));

/** Fuel to add: what the laps left will burn, less what is in the tank; never negative. */
export const fuelToAdd = (): Expr => max(num(0), sub(mul(lapsLeft(), fuelPerLap()), fuel()));

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
 */
export const tankIsLow = (): Expr => lt(isnull(computed('Fuel_RemainingLaps'), num(999)), flagBox.lowFuelLaps());

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
export const GRIP_WIDEST = 'MODERATE';
/** The track's grip, upper-cased into the label face; `--` where the sim reports none. */
export const trackGrip = (): Expr => ucase(isnull(game('TrackGripStatus'), str(NO_VALUE)));
export const sessionType = (): Expr => isnull(game('SessionTypeName'), str(''));
export const carModel = (): Expr => isnull(game('CarModel'), str(''));
export const playerClass = (): Expr => isnull(game('CarClass'), str(''));

/** Wind speed in km/h, from the raw metres per second iRacing publishes. */
export const windKmh = (): Expr => mul(isnull(raw('WindVel'), num(0)), num(3.6));
/** Wind direction in degrees, from radians. */
export const windDegrees = (): Expr => mul(isnull(raw('WindDir'), num(0)), num(180 / Math.PI));
/** The sim's own clock, as seconds of day. */
export const simTimeOfDay = (): Expr => isnull(raw('SessionTimeOfDay'), num(0));
/** The wall clock, as SimHub's own date property. */
export const localClock = (): Expr => fmt(prop('DataCorePlugin.CurrentDateTime'), 'HH:mm');
/** The sim's clock as `HH:mm`. */
export const simClock = (): Expr =>
  concat(fmt(truncate(div(simTimeOfDay(), num(3600))), '00'), str(':'), fmt(truncate(div(mod(simTimeOfDay(), num(3600)), num(60))), '00'));

/** Incidents taken, which iRacing publishes raw and other sims do not publish at all. */
export const incidents = (): Expr => raw('PlayerCarMyIncidentCount');
/** The incident limit, a string in iRacing's session YAML ("unlimited" or a number). */
export const incidentLimit = (): Expr => prop('DataCorePlugin.GameRawData.SessionData.WeekendInfo.WeekendOptions.IncidentLimit');

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
 * pairs a driver asks for by name and calls the ten that are left `SOME`. Which corners those are
 * is what the four corner toggles drawn beside the summary still say.
 */
export const TYRE_SELECTIONS: readonly string[] = ['NONE', 'ALL', 'FRONTS', 'REARS', 'LEFTS', 'RIGHTS', 'SOME'];

/** Which corners the next stop changes, as the one word the catalogue writes beside `Tyres`. */
export const pitTyreSelection = (): Expr => {
  const fronts = add(pitServiceBit(PIT_SERVICE_BITS.FrontLeft), pitServiceBit(PIT_SERVICE_BITS.FrontRight));
  const rears = add(pitServiceBit(PIT_SERVICE_BITS.RearLeft), pitServiceBit(PIT_SERVICE_BITS.RearRight));
  const lefts = add(pitServiceBit(PIT_SERVICE_BITS.FrontLeft), pitServiceBit(PIT_SERVICE_BITS.RearLeft));
  const rights = add(pitServiceBit(PIT_SERVICE_BITS.FrontRight), pitServiceBit(PIT_SERVICE_BITS.RearRight));
  const both = (pair: Expr, other: Expr): Expr => and(eq(pair, num(2)), eq(other, num(0)));
  return iff(
    eq(add(fronts, rears), num(0)),
    str('NONE'),
    iff(
      eq(add(fronts, rears), num(4)),
      str('ALL'),
      iff(
        both(fronts, rears),
        str('FRONTS'),
        iff(both(rears, fronts), str('REARS'), iff(both(lefts, rights), str('LEFTS'), iff(both(rights, lefts), str('RIGHTS'), str('SOME')))),
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
  const slots = Array.from({ length: AVERAGE_LAPS }, (_, i) => previousLap(num(i + 1)));
  const seconds = slots.map((slot) => timespanToSeconds(slot));
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

/** The delta the screens show, per the plugin's DeltaReference. */
export const referenceDelta = (): Expr => iff(eq(setting.deltaReference(), str('alltime')), allTimeBestDelta(), sessionBestDelta());

/** The label that says which reference the delta is against. */
export const referenceLabel = (): Expr => iff(eq(setting.deltaReference(), str('alltime')), str('VS ALL-TIME BEST'), str('VS SESSION BEST'));

export const sectorLast = (sector: number): Expr => game(`Sector${sector}LastLapTime`);
export const sectorBest = (sector: number): Expr => game(`Sector${sector}BestTime`);

/** True when a sector of the last lap beat your own best of that sector. */
export const sectorImproved = (sector: number): Expr =>
  and(hasTime(sectorLast(sector)), hasTime(sectorBest(sector)), lt(timespanToSeconds(sectorLast(sector)), timespanToSeconds(sectorBest(sector))));

/** The signed difference between a sector of the last lap and your best of that sector. */
export const sectorDelta = (sector: number): Expr =>
  sub(timespanToSeconds(isnull(sectorLast(sector), num(0))), timespanToSeconds(isnull(sectorBest(sector), num(0))));

/** Absolute seconds, for a gain-or-loss bar that only knows how far it is from zero. */
export const magnitude = (expr: Expr): Expr => abs(expr);

/** Deltas this close to zero are drawn as neither faster nor slower, as on the dash's delta card. */
export const DELTA_DEADBAND = 0.005;

/** Green when faster, red when slower, white within the deadband. */
export const deltaColour = (seconds: Expr): Expr =>
  iff(lt(seconds, num(-DELTA_DEADBAND)), str(dsColour.faster), iff(gt(seconds, num(DELTA_DEADBAND)), str(dsColour.slower), str(dsColour.zero)));

/** The delta colours, named so the expression above reads as a sentence. */
const dsColour = { faster: dsTokens.purpose.delta.faster, slower: dsTokens.purpose.delta.slower, zero: dsTokens.purpose.delta.zero };
