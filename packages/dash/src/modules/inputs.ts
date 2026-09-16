/**
 * Module 10, Inputs: throttle, brake and clutch as three traces over the same window, with a bar
 * and a number per pedal beside them, and the steering angle after those.
 *
 * SimHub's clutch is already inverted for us (100 is fully engaged), so all three read the same
 * way: 100 is "pressed".
 *
 * No legend under the plot: every line has its own bar and its own number standing beside it in
 * the same colour, which is the labelling a legend would repeat. The trace draws one where it is
 * the whole panel, which is the pit wall's telemetry page.
 */
import { ncalc } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { densityOf } from '../second/density.ts';
import { barGauge } from '../second/gauge.ts';
import { trace, type Series } from '../second/trace.ts';
import { CHARS, STEERING_RANGE, brake, clutch, steering, throttle } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import type { Item } from '../generator.ts';
import { cells, monoWidth } from '../design/metrics.ts';
import { defineModule, drawnAt, pageKeeps } from './module.ts';

const { add, div, fmt, max, min, mul, num } = ncalc;

/**
 * The three pedals, in the order the design draws them, each with the reading the catalogue puts
 * under its bar. Three different samples rather than one repeated: Dash Studio draws the samples,
 * and three identical numerals say nothing about which bar is which.
 */
export const PEDALS = [
  { id: 'throttle', name: 'Throttle', color: ds.purpose.delta.faster, value: throttle, sample: '76' },
  { id: 'brake', name: 'Brake', color: ds.purpose.delta.slower, value: brake, sample: '12' },
  { id: 'clutch', name: 'Clutch', color: ds.color.text.secondary, value: clutch, sample: '0' },
] as const;

/**
 * A pedal bar: the catalogue draws it 20 px wide at its fullest shape and 16 at the other three,
 * always 10 apart, and puts 16 px between the three groups of the page.
 */
const BAR = { wide: 20, narrow: 16, gap: 10 } as const;
/** The gap between the trace, the bars and the steering, which the catalogue draws at 16 everywhere. */
const GROUP_GAP = ds.space[4];

/**
 * The steering indicator: the canvas's 96 px dial, drawn as a track with a marker running along it.
 *
 * SimHub draws no arc, and `Rotation` is a number written at build time rather than one of the
 * binding targets, so a ring with a dot that turns is not expressible. A position is: `Left`
 * binds, so the marker runs from lock to lock along a track of the width the dial had.
 * `docs/second-screens.md` records the dial itself.
 */
const STEER = { width: 96, marker: 4, gap: 6 } as const;

export const inputs = defineModule('inputs', (ctx) => {
  const d = densityOf(ctx.density);
  const barWidth = drawnAt(ctx) === 'wide' ? BAR.wide : BAR.narrow;
  // The catalogue draws the numbers at 24 at three of its four shapes and at 16 at `tall narrow`,
  // which is `d.tiny` and is 14 at the compact ramp. readability-pass.md §8 is the argument for
  // not following it down there: a percentage nobody can read is one the page may as well drop.
  const valueFs = d.small;
  const valueHeight = valueFs + d.fieldGap;
  // A pedal's column is as wide as the wider of its bar and its number, so three numbers side by
  // side never run into each other however large the density makes them.
  const valueWidth = monoWidth(cells('SemiBold', valueFs), CHARS.percent);
  const columnWidth = Math.max(barWidth, valueWidth);
  const barsWidth = PEDALS.length * columnWidth + (PEDALS.length - 1) * BAR.gap;
  const steers = pageKeeps('steer', ctx);
  const steerWidth = steers ? STEER.width + GROUP_GAP : 0;
  const plotWidth = Math.max(0, ctx.frame.width - barsWidth - steerWidth - GROUP_GAP);
  const series: Series[] = PEDALS.map((pedal) => ({ name: pedal.name, color: pedal.color, bind: pedal.value(), min: 0, max: 100 }));
  const items = trace(`${ctx.prefix}trace`, rect(ctx.frame.left, ctx.frame.top, plotWidth, ctx.frame.height), series, ctx.density, { legend: false });
  const barsTop = ctx.frame.top;
  const barsHeight = Math.max(0, ctx.frame.height - valueHeight);
  const barsLeft = ctx.frame.left + plotWidth + GROUP_GAP;
  PEDALS.forEach((pedal, i) => {
    const x = barsLeft + i * (columnWidth + BAR.gap);
    items.push(
      barGauge(`${ctx.prefix}${pedal.id}.bar`, rect(Math.round(x + (columnWidth - barWidth) / 2), barsTop, barWidth, barsHeight), pedal.value(), {
        fill: pedal.color,
        max: 100,
        value: Number(pedal.sample),
      }),
      numeral(`${ctx.prefix}${pedal.id}.value`, pedal.sample, Math.round(x + (columnWidth - valueWidth) / 2), barsTop + barsHeight + d.fieldGap, valueFs, CHARS.percent, {
        bind: fmt(pedal.value(), '0'),
        color: pedal.color,
        maxWidth: valueWidth,
      }),
    );
  });
  if (steers) items.push(...steerColumn(ctx.prefix, rect(barsLeft + barsWidth + GROUP_GAP, barsTop, STEER.width, barsHeight), valueFs, d.labelSm, d.fieldGap));
  return items;
});

/** The track, the marker on it and the word under it, centred in the column the bars leave. */
function steerColumn(prefix: string, frame: { left: number; top: number; width: number; height: number }, valueFs: number, labelFs: number, fieldGap: number): Item[] {
  const trackHeight = Math.max(2, Math.round(valueFs / 6));
  const trackTop = frame.top + Math.round((frame.height - trackHeight) / 2);
  const travel = frame.width - STEER.marker;
  const centre = frame.left + travel / 2;
  // Radians of wheel angle, clamped to the lock the pit wall's own steering trace is drawn at, then
  // mapped onto the half travel either side of centre.
  const clamped = min(max(steering(), num(-STEERING_RANGE)), num(STEERING_RANGE));
  const labelWidth = Math.ceil(measureText('BarlowMedium', 'STEER', labelFs));
  return [
    band(`${prefix}steer.track`, rect(frame.left, trackTop, frame.width, trackHeight), ds.color.text.dim),
    {
      ...band(`${prefix}steer.marker`, rect(Math.round(centre), trackTop - STEER.gap, STEER.marker, trackHeight + 2 * STEER.gap), ds.color.text.primary),
      ...withBindings({ Left: add(num(centre), mul(div(clamped, num(STEERING_RANGE)), num(travel / 2))) }),
    },
    label(`${prefix}steer.label`, 'Steer', frame.left + Math.round((frame.width - labelWidth) / 2), trackTop + trackHeight + STEER.gap + fieldGap, labelWidth, { size: labelFs }),
  ];
}
