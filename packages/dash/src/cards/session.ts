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
import { isTimedSession, sessionTimeLeft, showsTimeLeft } from '../second/values.ts';
import type { Expr } from '../bind.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { SESSION_CHARS, SESSION_LAP_DIGITS } from './chars.ts';

const { game, eq, gt, lt, or, and, not, str, iff, fmt, concat, add, mul, num, digitCount, hms, timespanToSeconds } = ncalc;

/** What the time value shows when time mode is forced in an untimed session. */
export const TIME_PLACEHOLDER = '-:--:--';

// The session's own two predicates are `second/values.ts`'s, re-exported here because this card was
// where they were written and the tests still name them: the module beside it reads the same
// setting, and a card and a module answering it apart is the fault ADR 0014 is about.
export { UNTIMED_SECONDS, sessionTimeLeft as timeLeft, isTimedSession as timedSession, showsTimeLeft as showTime } from '../second/values.ts';

export const session = defineCard('session', (slot, rung, prefix, meta) => {
  const time = showsTimeLeft();
  const timed = isTimedSession();
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
      bind: iff(time, iff(timed, hms(sessionTimeLeft()), str(TIME_PLACEHOLDER)), fmt(currentLap, '0')),
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
