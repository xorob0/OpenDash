/**
 * The two zone pages that are not companion modules: the web view and the wide car-telemetry page.
 *
 * The web view is SimHub's browser pointed at whatever address the plugin holds. With no address
 * it draws an outlined box saying where to set one, rather than an empty white rectangle.
 */
import type { Item, WebPageItem } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings } from '../bind.ts';
import { secondScreen } from '../contract.ts';
import { rect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { densityOf } from '../second/density.ts';
import { drawFieldBlock, planLines } from '../second/field.ts';
import { legend, trace, LEGEND_HEIGHT, type Series } from '../second/trace.ts';
import { brake, throttle } from '../second/values.ts';
import { ds, TRANSPARENT } from '../tokens.ts';
import { settingCells } from './carSettings.ts';
import type { ModuleContext } from './module.ts';

const { eq, ne, str } = ncalc;

export const WEB_VIEW_MESSAGE = 'Web view · address set in the plugin';

/** Width of the car-telemetry page's settings grid, which the canvas draws at a fixed 380 px. */
const SETTINGS_WIDTH = 380;

/** The browser page: the box and its "no address" state, then the browser itself. */
export function webView(ctx: ModuleContext): Item[] {
  const d = densityOf(ctx.density);
  const url = secondScreen.webViewUrl();
  const empty = eq(url, str(''));
  const page: WebPageItem = withMoreBindings({
    kind: 'webPage',
    name: `${ctx.prefix}web`,
    rect: rect(ctx.frame.left, ctx.frame.top, ctx.frame.width, ctx.frame.height),
    startAddress: '',
    clickThrough: false,
  }, { StartAddress: url, Visible: ne(url, str('')) });
  return [
    withMoreBindings(
      band(`${ctx.prefix}box`, rect(ctx.frame.left, ctx.frame.top, ctx.frame.width, ctx.frame.height), TRANSPARENT, {
        border: { color: ds.color.text.dim, width: 1 },
      }),
      { Visible: empty },
    ),
    label(`${ctx.prefix}empty`, WEB_VIEW_MESSAGE, ctx.frame.left, ctx.frame.top + (ctx.frame.height - d.labelSm) / 2, ctx.frame.width, {
      size: d.labelSm,
      hAlign: 'center',
      // Prose rather than a field label: the canvas writes it in sentence case, and `label`
      // upper-cases a literal unless the caller declines it.
      case: 'asIs',
      visibleBind: empty,
    }),
    page,
  ];
}

/** Column and row gaps of the settings grid, which the canvas draws as `gap: 10px 18px`. */
const CELL_GAP = 18;
const CELL_LINE_GAP = 10;

/** Cells to a line of the grid, which the canvas fills three across at both of its wide zones. */
const CELL_COLUMNS = 3;

/**
 * The wide car-telemetry page: the two pedal traces beside the settings grid, which is what an
 * engineer watches a stint with. The traces are throttle and brake, labelled, because the design
 * sheet drew two unlabelled lines and an unlabelled line is a guess.
 *
 * The row is the canvas's, measured on both of its wide zones -- the 1279 x 240 reference frame
 * and the 1039 x 255 the tower page carries. Three things come out of the two of them rather than
 * out of one, which is why they are written as they are below: the settings column is 380 px at
 * both, the row keeps a page padding of slack at its right edge at both, and the plot is 28 px
 * shorter than the content box at both, sitting half that band below the top edge. The band under
 * it is what the legend the canvas has not got is drawn in, so the plot keeps its full height and
 * the grid beside it sits on the plot's own bottom edge, as the sheet's `align-items: flex-end`
 * puts it.
 */
export function carTelemetry(ctx: ModuleContext): Item[] {
  const d = densityOf(ctx.density);
  // The canvas's settings column, or half of what is left where the body is too narrow for 380 to
  // leave the traces the larger half. A share of the width instead capped it at 342 on the 1039 px
  // zone the tower page carries, which is a column the canvas never asked for.
  const gridWidth = Math.min(SETTINGS_WIDTH, Math.round((ctx.frame.width - d.gapX) / 2));
  const plotWidth = Math.max(0, ctx.frame.width - gridWidth - d.gapX - d.padX);
  const plot = rect(ctx.frame.left, ctx.frame.top + LEGEND_HEIGHT, plotWidth, Math.max(0, ctx.frame.height - 2 * LEGEND_HEIGHT));
  const series: Series[] = [
    { name: 'Throttle', color: ds.purpose.delta.faster, bind: throttle(), min: 0, max: 100 },
    { name: 'Brake', color: ds.purpose.delta.slower, bind: brake(), min: 0, max: 100 },
  ];
  // The cells at the size the canvas sets them rather than at the size rule 20 would grow them to:
  // they are a reference an engineer checks, beside a trace he watches, and the trace is the
  // reading. `docs/design/readability-pass.md` §7 carries the exception.
  const cells = settingCells({ ...ctx, prefix: `${ctx.prefix}settings.` }, d.tiny);
  const lines = planLines(cells, gridWidth, ctx.density, { plan: 'grid', columns: CELL_COLUMNS, gap: CELL_GAP });
  return [
    ...trace(`${ctx.prefix}trace`, plot, series, ctx.density, { grid: 'mid', baseline: false, legend: false }),
    ...legend(`${ctx.prefix}trace`, series, plot.left, ctx.frame.top + ctx.frame.height - LEGEND_HEIGHT, ctx.density, plot.width),
    ...drawFieldBlock(lines, plot.left + plotWidth + d.gapX, plot.top + plot.height, gridWidth, ctx.density, {
      gap: CELL_GAP,
      lineGap: CELL_LINE_GAP,
      columns: CELL_COLUMNS,
    }),
  ];
}
