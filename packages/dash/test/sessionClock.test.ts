/**
 * The session clock: what every surface draws while a session is timed, where it has no clock, and
 * where there is no session at all.
 *
 * Read by evaluating the NCalc the packages emit, as `session.test.ts` reads the card's three modes,
 * so what is asserted is what a driver sees rather than what a call site was written with. #439.
 */
import { describe, expect, test } from 'bun:test';
import { measureText } from '../src/design/advances.ts';
import { NO_CLOCK, UNTIMED_MARK, UNTIMED_SECONDS, isUntimedSession, sessionClock } from '../src/second/values.ts';
import { ds } from '../src/tokens.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const A_WEEK = 604800;

/** What the sim publishes, in the three states a session clock has. */
const game = (timeLeft: number): Props => ({ 'DataCorePlugin.GameData.SessionTimeLeft': timeLeft });

describe('the mark an untimed session draws is measured before it is drawn', () => {
  test('it is U+221E, and no cell a value is laid in can hold it', () => {
    // Rule 19, and the reason the mark is a second item rather than a string this clock can be bound
    // to: the digit cell is cut for the widest ink a digit draws and this glyph advances a third
    // further in both faces a value is set in. A monospaced item bound to it would be clipped by WPF
    // exactly as the week of time left it replaces was.
    expect(UNTIMED_MARK).toBe('∞');
    const faces = [
      ['BarlowCondensedSemiBold', ds.font.cell.semiBold.digit],
      ['BarlowCondensedBold', ds.font.cell.bold.digit],
    ] as const;
    for (const [face, cell] of faces) {
      const em = measureText(face, UNTIMED_MARK, 1000) / 1000;
      expect({ face, em, cell, overruns: em > cell }).toMatchObject({ overruns: true });
      // And by a third, not by a rounding: a cell would have to be cut 39% and 34% wider for it,
      // which is the whole clock beside it moving.
      expect({ face, ratio: Math.round((em / cell) * 100) / 100 }).toMatchObject({ ratio: face === 'BarlowCondensedSemiBold' ? 1.39 : 1.34 });
    }
  });

  test('the cells were not widened for it either, which is what the ratio above would cost', () => {
    // The alternative the measurement rules out. The mark is one glyph and the clock beside it is
    // eight cells, so holding the mark in a cell widens `12:34:56` by a third wherever it is drawn.
    expect(ds.font.cell.semiBold.digit).toBe(0.47);
    expect(ds.font.cell.bold.digit).toBe(0.49);
  });
});

describe('the clock the surfaces bind', () => {
  test('it counts down while the session is timed', () => {
    for (const [secs, reading] of [[1800, '0:30:00'], [5025, '1:23:45'], [86399, '23:59:59'], [1, '0:00:01']] as const) {
      expect({ secs, reading: evalNcalc(sessionClock(), game(secs)) }).toEqual({ secs, reading });
    }
  });

  test('it is the unset clock where there is no session, and the mark takes the untimed one', () => {
    for (const secs of [0, -1]) {
      expect({ secs, reading: evalNcalc(sessionClock(), game(secs)), marked: evalNcalc(isUntimedSession(), game(secs)) }).toEqual({
        secs,
        reading: NO_CLOCK,
        marked: false,
      });
    }
    // At and above the sentinel the clock is hidden and the mark is shown, so what this expression
    // reads there is never drawn -- but it is the placeholder rather than a week either way.
    for (const secs of [UNTIMED_SECONDS, A_WEEK]) {
      expect({ secs, reading: evalNcalc(sessionClock(), game(secs)), marked: evalNcalc(isUntimedSession(), game(secs)) }).toEqual({
        secs,
        reading: NO_CLOCK,
        marked: true,
      });
    }
  });
});
