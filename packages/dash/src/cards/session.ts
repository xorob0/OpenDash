/**
 * Card 5, Session: time left or lap of total, per OpenDash.SessionProgress. 'time' and 'laps'
 * force a mode; 'auto' shows time when the session is timed and laps otherwise. iRacing reports
 * SessionTimeLeft as 604800 s (a week) when a session has no time limit, so "timed" means
 * 0 < SessionTimeLeft <= 86400 s, a 24-hour race being timed and the boundary being in for that
 * reason (see UNTIMED_SECONDS), or a declared SessionTimeTotal in the same window, which keeps a
 * timed session timed after its clock reaches nought (#1017); iRacing's TotalLaps is the leader's completed laps in timed
 * sessions, which is why the mode never keys off it. The label follows the resolved mode; in
 * time mode an untimed session shows `∞` and a session that has not started shows `-:--:--` in
 * text.dim; the `/ N` denominator is visible only in laps mode in a session that is not timed and
 * declares a lap count (`hasLapTotal`), its Left following the lap count's digits.
 */
import { ncalc } from '../generator.ts';
import { readoutRow } from '../components/readoutRow.ts';
import { markWhen } from '../elements/mark.ts';
import { LAP_TOTAL_WIDEST, NO_CLOCK, hasLapTotal, isTimedSession, isUntimedSession, sessionClock, showsTimeLeft, untimedMark } from '../second/values.ts';
import type { Expr } from '../bind.ts';
import { ds } from '../tokens.ts';
import { defineCard } from './card.ts';
import { SESSION_CHARS, SESSION_LAP_DIGITS } from './chars.ts';

const { game, eq, lt, or, and, not, str, iff, fmt, concat, add, mul, num, digitCount } = ncalc;

/** What the time value shows when time mode is forced and there is no session to count. */
export const TIME_PLACEHOLDER = NO_CLOCK;

// The session's own two predicates are `second/values.ts`'s, re-exported here because this card was
// where they were written and the tests still name them: the module beside it reads the same
// setting, and a card and a module answering it apart is the fault ADR 0014 is about.
export { UNTIMED_SECONDS, sessionTimeLeft as timeLeft, isTimedSession as timedSession, showsTimeLeft as showTime } from '../second/values.ts';

export const session = defineCard('session', (slot, rung, prefix, meta) => {
  const time = showsTimeLeft();
  const timed = isTimedSession();
  const untimed = isUntimedSession();
  // Dim is for the clock nobody is counting, which is the session that has not started. An untimed
  // session draws the mark instead, at full strength, because a lap race having no clock is a
  // reading and not an absence (#439).
  const placeholder = and(time, not(timed), not(untimed));
  const currentLap = game('CurrentLap');
  const totalLaps = game('TotalLaps');
  return readoutRow(
    slot,
    rung,
    prefix,
    { text: meta.label, bind: iff(time, str('Time left'), str('Lap')) },
    {
      sample: '12',
      bind: iff(time, sessionClock(), fmt(currentLap, '0')),
      // The mark belongs to the mode as well as to the state: a laps-mode card draws a lap number,
      // and a week of time left is nothing to it.
      mark: markWhen(untimedMark(), time),
      chars: SESSION_CHARS,
      colorBind: iff(placeholder, str(ds.color.text.dim), str(ds.color.text.primary)),
    },
    {
      kind: 'denominator',
      sample: '/ 30',
      widest: LAP_TOTAL_WIDEST,
      bind: concat(str('/ '), fmt(totalLaps, '0')),
      after: { digits: 2, specials: 0 },
      maxAfter: { digits: SESSION_LAP_DIGITS, specials: 0 },
      // The race's length only where `TotalLaps` is one, which a timed session's is not even with
      // the lap forced on (#989).
      visibleBind: and(not(time), hasLapTotal()),
      leftBind: ({ x, mono, gap }) => add(num(x), mul(digitCount(currentLap, SESSION_LAP_DIGITS), num(mono.charWidth)), num(gap)),
    },
  );
});
