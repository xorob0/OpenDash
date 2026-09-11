/**
 * Module 10, Inputs: throttle, brake and clutch as three traces over the same window, with a bar
 * and a number per pedal beside them.
 *
 * SimHub's clutch is already inverted for us (100 is fully engaged), so all three read the same
 * way: 100 is "pressed".
 */
import { ncalc } from '../generator.ts';
import { rect } from '../design/geometry.ts';
import { densityOf } from '../second/density.ts';
import { barGauge } from '../second/gauge.ts';
import { trace, type Series } from '../second/trace.ts';
import { CHARS, brake, clutch, throttle } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { numeral } from '../elements/numeral.ts';
import { cells, monoWidth } from '../design/metrics.ts';
import { defineModule } from './module.ts';

const { fmt } = ncalc;

/** The three pedals, in the order the design draws them. */
export const PEDALS = [
  { id: 'throttle', name: 'Throttle', color: ds.purpose.delta.faster, value: throttle },
  { id: 'brake', name: 'Brake', color: ds.purpose.delta.slower, value: brake },
  { id: 'clutch', name: 'Clutch', color: ds.color.text.secondary, value: clutch },
] as const;

export const inputs = defineModule('inputs', (ctx) => {
  const d = densityOf(ctx.density);
  const barWidth = 16;
  const barGap = 10;
  const valueFs = d.small;
  const valueHeight = valueFs + d.fieldGap;
  const barsWidth = PEDALS.length * barWidth + (PEDALS.length - 1) * barGap;
  const plotWidth = Math.max(0, ctx.frame.width - barsWidth - d.gapX);
  const series: Series[] = PEDALS.map((pedal) => ({ name: pedal.name, color: pedal.color, bind: pedal.value(), min: 0, max: 100 }));
  const items = trace(`${ctx.prefix}trace`, rect(ctx.frame.left, ctx.frame.top, plotWidth, ctx.frame.height), series, ctx.density, { legend: true });
  const barsTop = ctx.frame.top;
  const barsHeight = Math.max(0, ctx.frame.height - valueHeight);
  PEDALS.forEach((pedal, i) => {
    const x = ctx.frame.left + plotWidth + d.gapX + i * (barWidth + barGap);
    const mono = cells('SemiBold', valueFs);
    const width = monoWidth(mono, CHARS.percent);
    items.push(
      { ...barGauge(`${ctx.prefix}${pedal.id}.bar`, rect(x, barsTop, barWidth, barsHeight), pedal.value(), { fill: pedal.color, max: 100 }) },
      numeral(`${ctx.prefix}${pedal.id}.value`, '76', Math.round(x + (barWidth - width) / 2), barsTop + barsHeight + d.fieldGap, valueFs, CHARS.percent, {
        bind: fmt(pedal.value(), '0'),
        color: pedal.color,
        maxWidth: width + 4,
      }),
    );
  });
  return items;
});
