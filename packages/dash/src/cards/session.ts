/**
 * Card 5, Session: time left or lap of total, per OpenDash.SessionProgress. 'time' and 'laps'
 * force a mode; 'auto' shows time when the session is timed and laps otherwise. iRacing reports
 * SessionTimeLeft as 604800 s (a week) when a session has no time limit, so "timed" means
 * 0 < SessionTimeLeft < 86400 s; iRacing's TotalLaps is the leader's completed laps in timed
 * sessions, which is why the mode never keys off it. The label follows the resolved mode; in
 * time mode an untimed session shows `-:--:--` in text.dim; the `/ N` denominator is visible
 * only in laps mode with a declared lap count, its Left following the lap count's digits.
 */
import { ncalc } from '../generator.ts';
import { readoutRow } from '../components/readoutRow.ts';
import { setting } from '../contract.ts';
import type { Expr } from '../bind.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { SESSION_CHARS, SESSION_LAP_DIGITS } from './chars.ts';

const { game, eq, gt, lt, or, and, not, str, iff, fmt, concat, add, mul, num, digitCount, hms, timespanToSeconds } = ncalc;

/** SessionTimeLeft at or above this (a day, in seconds) means the session has no time limit. */
export const UNTIMED_SECONDS = 86400;
/** What the time value shows when time mode is forced in an untimed session. */
export const TIME_PLACEHOLDER = '-:--:--';

/** `timespantoseconds([SessionTimeLeft])` */
export const timeLeft = (): Expr => timespanToSeconds(game('SessionTimeLeft'));

/** True when the session has a time limit: `0 < timeLeft < 86400`. */
export const timedSession = (): Expr => and(gt(timeLeft(), num(0)), lt(timeLeft(), num(UNTIMED_SECONDS)));

/** True when the card shows time left: mode 'time', or 'auto' in a timed session. Anything else shows laps. */
export const showTime = (): Expr => {
  const mode = setting.sessionProgress();
  return or(eq(mode, str('time')), and(eq(mode, str('auto')), timedSession()));
};

export const session = defineCard('session', (slot, rung, prefix, meta) => {
  const time = showTime();
  const timed = timedSession();
  const placeholder = and(time, not(timed));
  const currentLap = game('CurrentLap');
  const totalLaps = game('TotalLaps');
  return readoutRow(
    slot,
    rung,
    prefix,
    { text: meta.label, bind: iff(time, str('TIME LEFT'), str('LAP')) },
    {
      sample: '12',
      bind: iff(time, iff(timed, hms(timeLeft()), str(TIME_PLACEHOLDER)), fmt(currentLap, '0')),
      chars: SESSION_CHARS,
      colorBind: iff(placeholder, str(ds.color.text.dim), str(ds.color.text.primary)),
    },
    {
      kind: 'denominator',
      sample: '/ 30',
      bind: concat(str('/ '), fmt(totalLaps, '0')),
      after: { digits: 2, specials: 0 },
      maxAfter: { digits: SESSION_LAP_DIGITS, specials: 0 },
      visibleBind: and(not(time), gt(totalLaps, num(0))),
      leftBind: ({ x, mono, gap }) => add(num(x), mul(digitCount(currentLap, SESSION_LAP_DIGITS), num(mono.charWidth)), num(gap)),
    },
  );
});
