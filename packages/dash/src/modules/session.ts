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
import { drawnFigure } from '../second/drawn.ts';
import { stack } from '../second/layout.ts';
import {
  CHARS,
  INCIDENTS_WIDEST,
  LAP_TOTAL_WIDEST,
  LAP_WIDEST,
  carPosition,
  positionDigits,
  classOpponentCount,
  currentLap,
  fieldSize,
  hasLapTotal,
  incidents,
  lapsLeft,
  player,
  playerClass,
  positionDrawn,
  sessionClock,
  sessionType,
  showsTimeLeft,
  totalLaps,
  untimedMark,
} from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineModule, fieldsRow, fld, leadRankSize } from './module.ts';

const { and, fmt, iff, concat, str, gt, num, isnull, not, driver } = ncalc;

export const session = defineModule('session', (ctx) => {
  const d = densityOf(ctx.density);
  const classPosition = isnull(driver('classposition', player()), num(0));
  const taken = isnull(incidents(), num(0));
  // The Session progress setting was read by the legacy card alone, so a zone drew the lap and the
  // time left side by side whatever the driver had chosen. One of the two answers the question and
  // the rank closes over the other, which is what the setting is for.
  const time = showsTimeLeft();
  // The first rank is promoted a size on a tall face, as the catalogue's `tall` drawing promotes it
  // to 76 over the same 34; see `leadRankSize`.
  const lead = leadRankSize(ctx);
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          fld(ctx, 'type', 'Session', { sample: 'Race', bind: sessionType(), chars: CHARS.word, fs: lead }),
          fld(ctx, 'position', 'Position', {
            sample: '4',
            bind: positionDigits(player()),
            chars: CHARS.position,
            fs: lead,
            follower: { kind: 'denominator', text: '/ 24', bind: concat(str('/ '), fmt(fieldSize(), '0')) },
            drawn: positionDrawn(player()),
          }),
          fld(ctx, 'class', 'Class', {
            sample: 'GT3 · P4',
            bind: concat(playerClass(), str(' · P'), fmt(classPosition, '0')),
            chars: CHARS.classPosition,
            fs: lead,
          }, { visibleBind: gt(classOpponentCount(), num(0)) }),
        ],
        ctx,
      ),
      fieldsRow(
        [
          fld(ctx, 'lap', 'Lap', {
            sample: '12',
            widest: LAP_WIDEST,
            bind: fmt(currentLap(), '0'),
            chars: CHARS.lap,
            fs: d.mid,
            follower: {
              kind: 'denominator',
              text: '/ 30',
              widest: LAP_TOTAL_WIDEST,
              bind: concat(str('/ '), fmt(totalLaps(), '0')),
              // Hidden with the lap as well as without a length. iRacing's `TotalLaps` in a timed race
              // is the leader's laps, so a denominator shown for that alone drew `/ 14` with no lap
              // before it, and with the lap and the time left in one place, on top of the time left.
              // The length is `hasLapTotal`'s, so a lap forced on in a timed race is drawn without
              // the leader's laps after it too (#989).
              visibleBind: and(not(time), hasLapTotal()),
            },
            drawn: drawnFigure({ value: currentLap(), digits: CHARS.lap.digits }),
          }, { visibleBind: not(time) }),
          fld(ctx, 'timeLeft', 'Time left', {
            sample: '0:42:15',
            bind: sessionClock(),
            mark: untimedMark(),
            chars: CHARS.clock,
            fs: d.mid,
          }, { visibleBind: time }),
          fld(ctx, 'lapsLeft', 'Laps left', { sample: '18', widest: LAP_WIDEST, bind: fmt(lapsLeft(), '0'), chars: CHARS.lap, fs: d.mid }, { visibleBind: gt(lapsLeft(), num(0)) }),
        ],
        ctx,
        // The lap and the time left are never shown together, so a zone too narrow for the two side
        // by side draws them in one place rather than leaving a line between the class and the time
        // left that is always empty.
        { turns: [['lap', 'timeLeft']] },
      ),
      fieldsRow(
        [
          fld(ctx, 'incidents', 'Incidents', {
            sample: '3x',
            widest: INCIDENTS_WIDEST,
            bind: concat(fmt(taken, '0'), str('x')),
            chars: CHARS.incidents,
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
