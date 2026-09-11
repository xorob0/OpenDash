/**
 * A scrolling trace. SimHub's ChartItem holds one series, so an overlay of throttle, brake and
 * clutch is three items sharing a rect with transparent backgrounds. There is no time axis: the
 * window is the sample count times the refresh interval, which is why the density sets the count
 * rather than a caller asking for seconds.
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
  /** Samples kept; the density's count by default. */
  points?: number;
  lineThickness?: number;
}

/** Height a legend row takes, swatch included. */
export const LEGEND_HEIGHT = 14;
/** Width and height of a legend swatch. */
export const SWATCH = { width: 16, height: 2 } as const;
/** Gap between a swatch and its text, and between legend entries. */
const SWATCH_GAP = 6;
const LEGEND_GAP = 20;

const chartOf = (name: string, frame: Rect, series: Series, opts: TraceOptions): ChartItem => ({
  kind: 'chart',
  name,
  rect: roundRect(frame),
  lineColor: series.color,
  lineThickness: opts.lineThickness ?? 2,
  pointsCount: opts.points ?? 300,
  minimum: series.min ?? 0,
  maximum: series.max ?? 100,
  useMaximum: series.useMaximum ?? true,
  ...withBindings({ CurrentValue: series.bind }),
});

/** The legend for a set of series, drawn on the line box at `y`. */
export function legend(name: string, series: readonly Series[], x: number, y: number, density: Density): Item[] {
  const d = densityOf(density);
  const items: Item[] = [];
  let cursor = x;
  for (const s of series) {
    const width = Math.ceil(measureText('BarlowMedium', s.name.toUpperCase(), d.labelSm));
    items.push(band(`${name}.${s.name}.swatch`, rect(cursor, Math.round(y + d.labelSm / 2), SWATCH.width, SWATCH.height), s.color));
    items.push(label(`${name}.${s.name}.legend`, s.name, cursor + SWATCH.width + SWATCH_GAP, y, width, { size: d.labelSm, color: ds.color.text.secondary }));
    cursor += SWATCH.width + SWATCH_GAP + width + LEGEND_GAP;
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
  const d = densityOf(density);
  const showLegend = (opts.legend ?? series.length > 1) && series.length > 0;
  const plot = showLegend ? rect(frame.left, frame.top, frame.width, Math.max(0, frame.height - LEGEND_HEIGHT - 4)) : frame;
  const items: Item[] = [];
  if (opts.grid ?? true) {
    for (const [i, fraction] of [0.25, 0.5, 0.75].entries()) {
      items.push(band(`${name}.grid${i}`, rect(plot.left, Math.round(plot.top + fraction * plot.height), plot.width, 1), ds.color.surface.raised));
    }
    items.push(band(`${name}.baseline`, rect(plot.left, plot.top + plot.height - 1, plot.width, 1), ds.color.text.dim));
  }
  const points = opts.points ?? d.tracePoints;
  for (const s of series) items.push(chartOf(`${name}.${s.name}`, plot, s, { ...opts, points }));
  if (showLegend) items.push(...legend(name, series, plot.left, frame.top + frame.height - LEGEND_HEIGHT, density));
  return items;
}
