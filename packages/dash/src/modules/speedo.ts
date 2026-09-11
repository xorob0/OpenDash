/**
 * Module 4, Speedo: speed, engine speed and the shift bar. The bar is the same fifteen segments
 * the dash face carries, with the same per-car shift lights, so a glance at the companion means
 * the same thing as a glance at the wheel.
 */
import { ncalc } from '../generator.ts';
import { revBar } from '../components/revBar.ts';
import { densityOf } from '../second/density.ts';
import { stack } from '../second/layout.ts';
import { CHARS, gearRedline, rpm, speed, speedUnit } from '../second/values.ts';
import { blockRow, defineModule, fieldsRow, fld } from './module.ts';

const { fmt, lcase } = ncalc;

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
            follower: { text: 'km/h', bind: lcase(speedUnit()) },
          }),
          fld(ctx, 'rpm', 'RPM', { sample: '7,420', bind: fmt(rpm(), '#,0'), chars: CHARS.rpm, fs: d.big }),
          fld(ctx, 'redline', 'Redline', { sample: '8,100', bind: fmt(gearRedline(), '#,0'), chars: CHARS.rpm, fs: d.mid }),
        ],
        ctx,
      ),
      blockRow(barHeight, (bottom) => revBar({ left: ctx.frame.left, top: bottom - barHeight, width: ctx.frame.width, height: barHeight, gap }, `${ctx.prefix}rev`)),
    ],
    ctx.density,
  );
});
