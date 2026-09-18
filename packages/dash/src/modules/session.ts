/**
 * Module 11, Session: what race this is, where you are in it, and how much of it is left.
 *
 * Time left is only shown when the session has one: iRacing reports a week of remaining time for
 * a session that has none, which is why the value is guarded rather than formatted blindly.
 *
 * The third rank the catalogue draws is Strength, Incidents and Cars, and it is built here as two:
 * strength of field is published by SimHub in no form at all, which is the one field
 * [ADR 0009](../../../../docs/decisions/0009-does-the-plugin-compute.md) leaves out of the bar's
 * catalogue for the same reason. A field that can never have a value is worse than no field.
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
  incidents,
  isTimedSession,
  lapsLeft,
  player,
  playerClass,
  sessionTimeLeft,
  sessionType,
  showsTimeLeft,
  totalLaps,
} from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineModule, fieldsRow, fld } from './module.ts';

const { fmt, iff, concat, str, gt, num, isnull, not, driver } = ncalc;

export const session = defineModule('session', (ctx) => {
  const d = densityOf(ctx.density);
  const classPosition = isnull(driver('classposition', player()), num(0));
  const taken = isnull(incidents(), num(0));
  // The Session progress setting was read by the legacy card alone, so a zone drew the lap and the
  // time left side by side whatever the driver had chosen. One of the two answers the question and
  // the rank closes over the other, which is what the setting is for.
  const time = showsTimeLeft();
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
            follower: { kind: 'denominator', text: '/ 24', bind: concat(str('/ '), fmt(fieldSize(), '0')) },
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
            follower: { kind: 'denominator', text: '/ 30', bind: concat(str('/ '), fmt(totalLaps(), '0')), visibleBind: gt(totalLaps(), num(0)) },
          }, { visibleBind: not(time) }),
          fld(ctx, 'timeLeft', 'Time left', {
            sample: '0:42:15',
            bind: iff(isTimedSession(), clock(sessionTimeLeft()), str('-:--:--')),
            chars: CHARS.clock,
            fs: d.mid,
          }, { visibleBind: time }),
          fld(ctx, 'lapsLeft', 'Laps left', { sample: '18', bind: fmt(lapsLeft(), '0'), chars: CHARS.position, fs: d.mid }, { visibleBind: gt(lapsLeft(), num(0)) }),
        ],
        ctx,
      ),
      fieldsRow(
        [
          // Four cells rather than the count's three: the x the canvas draws after the number takes
          // one, which is how `zones/bar.ts` budgets the same value.
          fld(ctx, 'incidents', 'Incidents', {
            sample: '3x',
            bind: concat(fmt(taken, '0'), str('x')),
            chars: { digits: 4, specials: 0 },
            fs: d.small,
            colorBind: iff(gt(taken, num(0)), str(ds.color.caution.primary), str(ds.color.text.primary)),
          }),
          fld(ctx, 'cars', 'Cars', { sample: '24', bind: fmt(fieldSize(), '0'), chars: CHARS.position, fs: d.small }),
        ],
        ctx,
      ),
    ],
    ctx.density,
  );
});
