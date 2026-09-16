/**
 * A scrolling trace. SimHub's ChartItem holds one series, so an overlay of throttle, brake and
 * clutch is three items sharing a rect with transparent backgrounds. There is no time axis: the
 * window is the sample count times the refresh interval, which is why the plot's own width sets
 * the count rather than a caller asking for seconds.
 *
 * The hairlines at the quarters and the baseline are drawn behind the series, and a legend row is
 * added when there is more than one series, because unlabelled colours are a guess.
 */
import type { ChartItem, Hex, Item, Rect } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { withBindings } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect, roundRect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { ds } from '../tokens.ts';
import { densityOf, type Density } from './density.ts';

export interface Series {
  /** Item name suffix and legend text. */
  name: string;
  color: Hex;
  /** The value sampled on every tick. */
  bind: Expr;
  min?: number;
  max?: number;
  /** False autoscales the top, which is how RPM traces a car whose limit is unknown. */
  useMaximum?: boolean;
}

export interface TraceOptions {
  /** Draw the quarter hairlines. */
  grid?: boolean;
  /** Draw a legend under the plot when there is more than one series. */
  legend?: boolean;
  /** Samples kept; `pointsFor` of the plot's width by default. */
  points?: number;
  lineThickness?: number;
}

/** A sample every six pixels: the canvas draws about a hundred points across a 570 px plot. */
const SAMPLE_PITCH = 6;

/**
 * Samples a plot of this width keeps, which is also the window it covers: the count times the
 * refresh interval.
 *
 * Cut from the plot rather than taken from the density, which is rule 18 on the one quantity of a
 * trace that is not a rectangle. A count per density drew 300 samples into the companion's 570 px
 * plot and 600 into a zone's 491 px one, so the smaller box covered twice the time at half the
 * resolution, and both were finer than a 2 px polyline can distinguish. The floor is where a
 * window stops being long enough to show a pedal being released.
 */
export const pointsFor = (width: number): number => Math.max(60, Math.min(300, Math.round(width / SAMPLE_PITCH)));

/** Height a legend row takes, swatch included. */
export const LEGEND_HEIGHT = 14;
/** Width and height of a legend swatch. */
export const SWATCH = { width: 16, height: 2 } as const;
/** Gap between a swatch and its text, and between legend entries. */
const SWATCH_GAP = 6;
const LEGEND_GAP = 20;

const chartOf = (name: string, frame: Rect, series: Series, opts: TraceOptions & { points: number }): ChartItem => ({
  kind: 'chart',
  name,
  rect: roundRect(frame),
  lineColor: series.color,
  lineThickness: opts.lineThickness ?? 2,
  pointsCount: opts.points,
  minimum: series.min ?? 0,
  maximum: series.max ?? 100,
  useMaximum: series.useMaximum ?? true,
  ...withBindings({ CurrentValue: series.bind }),
});

/**
 * The legend for a set of series, drawn on the line box at `y`.
 *
 * `maxWidth` bounds it: a legend is labelled colours and a colour without its label is a guess, so
 * where three will not fit the last one is dropped rather than drawn past the edge. That is rule
 * 17 applied to chrome -- shed, do not shrink -- and it is what a 249 px zone needs, where THROTTLE
 * BRAKE CLUTCH runs six pixels over.
 */
export function legend(name: string, series: readonly Series[], x: number, y: number, density: Density, maxWidth?: number): Item[] {
  const d = densityOf(density);
  const items: Item[] = [];
  let cursor = x;
  for (const s of series) {
    const width = Math.ceil(measureText('BarlowMedium', s.name.toUpperCase(), d.labelSm));
    const takes = SWATCH.width + SWATCH_GAP + width;
    if (maxWidth !== undefined && cursor - x + takes > maxWidth) break;
    items.push(band(`${name}.${s.name}.swatch`, rect(cursor, Math.round(y + d.labelSm / 2), SWATCH.width, SWATCH.height), s.color));
    items.push(label(`${name}.${s.name}.legend`, s.name, cursor + SWATCH.width + SWATCH_GAP, y, width, { size: d.labelSm, color: ds.color.text.secondary }));
    cursor += takes + LEGEND_GAP;
  }
  return items;
}

/** Width a legend takes, so a caller can right-align it in a panel title row. */
export const legendWidth = (series: readonly Series[], density: Density): number => {
  const d = densityOf(density);
  const total = series.reduce((w, s) => w + SWATCH.width + SWATCH_GAP + Math.ceil(measureText('BarlowMedium', s.name.toUpperCase(), d.labelSm)) + LEGEND_GAP, 0);
  return Math.max(0, total - LEGEND_GAP);
};

/**
 * The traces of `series` filling `frame`, with the grid behind them. The legend, when asked for,
 * is drawn under the plot and takes LEGEND_HEIGHT off the bottom.
 */
export function trace(name: string, frame: Rect, series: readonly Series[], density: Density, opts: TraceOptions = {}): Item[] {
  const showLegend = (opts.legend ?? series.length > 1) && series.length > 0;
  const plot = showLegend ? rect(frame.left, frame.top, frame.width, Math.max(0, frame.height - LEGEND_HEIGHT - 4)) : frame;
  const items: Item[] = [];
  if (opts.grid ?? true) {
    for (const [i, fraction] of [0.25, 0.5, 0.75].entries()) {
      items.push(band(`${name}.grid${i}`, rect(plot.left, Math.round(plot.top + fraction * plot.height), plot.width, 1), ds.color.surface.raised));
    }
    items.push(band(`${name}.baseline`, rect(plot.left, plot.top + plot.height - 1, plot.width, 1), ds.color.text.dim));
  }
  const points = opts.points ?? pointsFor(plot.width);
  for (const s of series) items.push(chartOf(`${name}.${s.name}`, plot, s, { ...opts, points }));
  if (showLegend) items.push(...legend(name, series, plot.left, frame.top + frame.height - LEGEND_HEIGHT, density, plot.width));
  return items;
}
