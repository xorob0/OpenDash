/**
 * A scrolling trace. SimHub's ChartItem holds one series, so an overlay of throttle, brake and
 * clutch is three items sharing a rect with transparent backgrounds. There is no time axis: the
 * window is the sample count times the refresh interval, which is why the plot's own width sets
 * the count rather than a caller asking for seconds.
 *
 * The hairlines are drawn behind the series, with a rule closing the plot for the panels the canvas
 * draws one under, and a legend row is added when there is more than one series, because unlabelled
 * colours are a guess.
 *
 * What a ChartItem will not do is end a line: it carries a colour, a thickness and a sample count
 * and nothing about joins or caps, so the round ones the canvas draws are square here and a
 * polyline turning at a sample is mitred. That is the format and not a setting left unset.
 */
import type { ChartItem, Hex, Item, Rect } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { withBindings } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { inset as insetRect, rect, roundRect } from '../design/geometry.ts';
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

/**
 * Which hairlines a plot carries behind its series.
 *
 * `quarters` is the telemetry panel's grid and the default, because four plots read against each
 * other need the same ruling. `mid` is the one line the wide car-telemetry zone draws, where a
 * pedal is read as full, half or nothing and three lines are three more than that needs. `none` is
 * a plot with a scale of its own beside it.
 */
export type TraceGrid = 'none' | 'mid' | 'quarters';

export interface TraceOptions {
  /** Which hairlines are drawn behind the series. Quarters by default. */
  grid?: TraceGrid;
  /**
   * Close the plot with a rule along its bottom edge. The pit wall's telemetry panels are drawn
   * with one and the inputs page is not: a panel is a plot with a title over it and needs a floor,
   * where the inputs page has three bars standing on the same line beside it.
   */
  baseline?: boolean;
  /** Draw a legend when there is more than one series. */
  legend?: boolean;
  /**
   * Where the legend goes. Under the plot by default, which costs it a row; `title` returns the
   * entries laid from the plot's left edge on the title's own line, for a caller that has a title
   * row to put them in and would rather spend none of the plot on chrome.
   *
   * The caller places them, because only it knows where its title row is: `legendWidth` measures
   * the set so it can be right-aligned there. What this option changes here is that the plot keeps
   * the whole frame.
   */
  legendAt?: 'below' | 'title';
  /**
   * Room left between the plot's edges and the polylines drawn in it, the grid keeping the whole
   * plot. A line of thickness t at either end of its range is drawn half outside the rect and
   * clipped, so a signal that reaches its maximum reads thinner there than it does anywhere else;
   * the inputs page is where that shows, three pedals living at 0 and 100.
   */
  inset?: number;
  /** Samples kept; `pointsFor` of the plot's width by default. */
  points?: number;
  lineThickness?: number;
}

/** A sample every six pixels: the canvas draws about a hundred points across a 570 px plot. */
const SAMPLE_PITCH = 6;

/** Where each mode's hairlines sit, as fractions of the plot's height. */
const GRID_LINES: Record<TraceGrid, readonly number[]> = { none: [], mid: [0.5], quarters: [0.25, 0.5, 0.75] };

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
const SWATCH_GAP = ds.space[2];
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
 * The traces of `series` filling `frame`, with the grid behind them.
 *
 * A legend asked for is drawn under the plot and takes LEGEND_HEIGHT off the bottom, unless the
 * caller says `legendAt: 'title'`, in which case the plot keeps the whole frame and the caller
 * draws the entries itself through `legend` and `legendWidth`.
 */
export function trace(name: string, frame: Rect, series: readonly Series[], density: Density, opts: TraceOptions = {}): Item[] {
  const showLegend = (opts.legend ?? series.length > 1) && series.length > 0;
  const below = showLegend && (opts.legendAt ?? 'below') === 'below';
  const plot = below ? rect(frame.left, frame.top, frame.width, Math.max(0, frame.height - LEGEND_HEIGHT - 4)) : frame;
  const items: Item[] = [];
  const lines = GRID_LINES[opts.grid ?? 'quarters'];
  if (lines.length > 0) {
    for (const [i, fraction] of lines.entries()) {
      items.push(band(`${name}.grid${i}`, rect(plot.left, Math.round(plot.top + fraction * plot.height), plot.width, 1), ds.color.surface.raised));
    }
    if (opts.baseline ?? true) items.push(band(`${name}.baseline`, rect(plot.left, plot.top + plot.height - 1, plot.width, 1), ds.color.text.dim));
  }
  const points = opts.points ?? pointsFor(plot.width);
  // Never more than a quarter of the plot, so that a box too small for the margin loses the margin
  // rather than the trace it was meant to protect.
  const by = Math.max(0, Math.min(opts.inset ?? 0, Math.floor(Math.min(plot.width, plot.height) / 4)));
  const field = by === 0 ? plot : insetRect(plot, by);
  for (const s of series) items.push(chartOf(`${name}.${s.name}`, field, s, { ...opts, points }));
  if (below) items.push(...legend(name, series, plot.left, frame.top + frame.height - LEGEND_HEIGHT, density, plot.width));
  return items;
}
