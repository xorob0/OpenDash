/**
 * Module 4, Speedo: speed, engine speed and the shift bar. The bar is the same fifteen segments
 * the dash face carries, lit by the same shift model, so a glance at the companion means
 * the same thing as a glance at the wheel. The Redline field is the same model again, printed: it
 * is the RPM that bar's top band lights at, so the number and the colour beside it agree.
 */
import { ncalc } from '../generator.ts';
import { REV_WELL_PAD_X, REV_WELL_PAD_Y, revBar } from '../components/revBar.ts';
import { densityOf } from '../second/density.ts';
import { stack } from '../second/layout.ts';
import { CHARS, rpm, speed, speedUnit } from '../second/values.ts';
import { redlineRpm } from '../shift.ts';
import { band } from '../elements/band.ts';
import { rect } from '../design/geometry.ts';
import { ds } from '../tokens.ts';
import { blockRow, defineModule, fieldsRow, fld } from './module.ts';

const { fmt } = ncalc;

export const speedo = defineModule('speedo', (ctx) => {
  const d = densityOf(ctx.density);
  const barHeight = ctx.density === 'companion' ? 24 : 14;
  const gap = ctx.density === 'companion' ? 4 : 3;
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          fld(ctx, 'speed', 'Speed', {
            sample: '187',
            bind: fmt(speed(), '0'),
            chars: CHARS.speed,
            fs: d.hero,
            follower: { text: 'km/h', bind: speedUnit(), widest: 'km/h' },
          }),
          fld(ctx, 'rpm', 'RPM', { sample: '7,420', bind: fmt(rpm(), '#,0'), chars: CHARS.rpm, fs: d.big }),
          // The number the bar above it turns red at, from the same model rather than SimHub's own
          // redline: printing two answers to one question is the thing ADR 0014 exists to prevent.
          fld(ctx, 'redline', 'Redline', { sample: '8,100', bind: fmt(redlineRpm(), '#,0'), chars: CHARS.rpm, fs: d.mid }),
        ],
        ctx,
      ),
      // The segments in their well, as the face draws them. The row is the segments plus the well's
      // margin rather than the segments alone, so that the bar keeps the height the density asked
      // for and the eight pixels come out of what the fields above may grow into.
      blockRow(barHeight + 2 * REV_WELL_PAD_Y, (bottom) => {
        const well = rect(ctx.frame.left, bottom - barHeight - 2 * REV_WELL_PAD_Y, ctx.frame.width, barHeight + 2 * REV_WELL_PAD_Y);
        return [
          band(`${ctx.prefix}rev.well`, well, ds.purpose.block.well),
          ...revBar(
            { left: well.left + REV_WELL_PAD_X, top: well.top + REV_WELL_PAD_Y, width: well.width - 2 * REV_WELL_PAD_X, height: barHeight, gap },
            `${ctx.prefix}rev`,
          ),
        ];
      }, true),
    ],
    ctx.density,
  );
});
