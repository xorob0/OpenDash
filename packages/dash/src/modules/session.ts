/**
 * Module 11, Session: what race this is, where you are in it, and how much of it is left.
 *
 * Time left is only shown when the session has one: iRacing reports a week of remaining time for
 * a session that has none, which is why the value is guarded rather than formatted blindly.
 */
import { ncalc } from '../generator.ts';
import { densityOf } from '../second/density.ts';
import { stack } from '../second/layout.ts';
import {
  CHARS,
  carPosition,
  classOpponentCount,
  clock,
  currentLap,
  fieldSize,
  isTimedSession,
  lapsLeft,
  player,
  playerClass,
  sessionTimeLeft,
  sessionType,
  totalLaps,
} from '../second/values.ts';
import { defineModule, fieldsRow, fld } from './module.ts';

const { fmt, iff, concat, str, gt, num, isnull, driver } = ncalc;

export const session = defineModule('session', (ctx) => {
  const d = densityOf(ctx.density);
  const classPosition = isnull(driver('classposition', player()), num(0));
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          fld(ctx, 'type', 'Session', { sample: 'Race', bind: sessionType(), chars: CHARS.word, fs: d.big }),
          fld(ctx, 'position', 'Position', {
            sample: '4',
            bind: fmt(carPosition(player()), '0'),
            chars: CHARS.position,
            fs: d.big,
            follower: { text: '/ 24', bind: concat(str('/ '), fmt(fieldSize(), '0')) },
          }),
          fld(ctx, 'class', 'Class', {
            sample: 'GT3 · P4',
            bind: concat(playerClass(), str(' · P'), fmt(classPosition, '0')),
            chars: CHARS.classPosition,
            fs: d.big,
          }, { visibleBind: gt(classOpponentCount(), num(0)) }),
        ],
        ctx,
      ),
      fieldsRow(
        [
          fld(ctx, 'lap', 'Lap', {
            sample: '12',
            bind: fmt(currentLap(), '0'),
            chars: CHARS.position,
            fs: d.mid,
            follower: { text: '/ 30', bind: concat(str('/ '), fmt(totalLaps(), '0')), visibleBind: gt(totalLaps(), num(0)) },
          }),
          fld(ctx, 'timeLeft', 'Time left', {
            sample: '0:42:15',
            bind: iff(isTimedSession(), clock(sessionTimeLeft()), str('-:--:--')),
            chars: CHARS.clock,
            fs: d.mid,
          }),
          fld(ctx, 'lapsLeft', 'Laps left', { sample: '18', bind: fmt(lapsLeft(), '0'), chars: CHARS.position, fs: d.mid }, { visibleBind: gt(lapsLeft(), num(0)) }),
        ],
        ctx,
      ),
    ],
    ctx.density,
  );
});
