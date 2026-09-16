/**
 * The two zone pages that are not companion modules: the web view and the wide car-telemetry page.
 *
 * The web view is SimHub's browser pointed at whatever address the plugin holds. With no address
 * it draws an outlined box saying where to set one, rather than an empty white rectangle.
 */
import type { Item, WebPageItem } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { secondScreen } from '../contract.ts';
import { rect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { densityOf } from '../second/density.ts';
import { trace, type Series } from '../second/trace.ts';
import { brake, throttle } from '../second/values.ts';
import { ds, TRANSPARENT } from '../tokens.ts';
import { carSettings } from './carSettings.ts';
import type { ModuleContext } from './module.ts';

const { eq, ne, str } = ncalc;

export const WEB_VIEW_MESSAGE = 'Web view · address set in the plugin';

/** The browser page: the box and its "no address" state, then the browser itself. */
export function webView(ctx: ModuleContext): Item[] {
  const d = densityOf(ctx.density);
  const url = secondScreen.webViewUrl();
  const empty = eq(url, str(''));
  const page: WebPageItem = {
    kind: 'webPage',
    name: `${ctx.prefix}web`,
    rect: rect(ctx.frame.left, ctx.frame.top, ctx.frame.width, ctx.frame.height),
    startAddress: '',
    clickThrough: false,
    ...withBindings({ StartAddress: url, Visible: ne(url, str('')) }),
  };
  return [
    {
      ...band(`${ctx.prefix}box`, rect(ctx.frame.left, ctx.frame.top, ctx.frame.width, ctx.frame.height), TRANSPARENT, {
        border: { color: ds.color.text.dim, width: 1 },
      }),
      ...withBindings({ Visible: empty }),
    },
    label(`${ctx.prefix}empty`, WEB_VIEW_MESSAGE, ctx.frame.left, ctx.frame.top + (ctx.frame.height - d.labelSm) / 2, ctx.frame.width, {
      size: d.labelSm,
      hAlign: 'center',
      // Prose rather than a field label: the canvas writes it in sentence case and `label`
      // upper-cases a literal, so the message is bound to keep the case it was written in. Both
      // bindings are handed to the element, since a second `withBindings` spread would replace the
      // first rather than add to it.
      bind: str(WEB_VIEW_MESSAGE),
      visibleBind: empty,
    }),
    page,
  ];
}

/**
 * The wide car-telemetry page: the two pedal traces beside the settings grid, which is what an
 * engineer watches a stint with. The traces are throttle and brake, labelled, because the design
 * sheet drew two unlabelled lines and an unlabelled line is a guess.
 */
export function carTelemetry(ctx: ModuleContext): Item[] {
  const d = densityOf(ctx.density);
  const gridWidth = Math.min(380, Math.round(ctx.frame.width * 0.34));
  const plotWidth = Math.max(0, ctx.frame.width - gridWidth - d.gapX);
  const series: Series[] = [
    { name: 'Throttle', color: ds.purpose.delta.faster, bind: throttle(), min: 0, max: 100 },
    { name: 'Brake', color: ds.purpose.delta.slower, bind: brake(), min: 0, max: 100 },
  ];
  return [
    ...trace(`${ctx.prefix}trace`, rect(ctx.frame.left, ctx.frame.top, plotWidth, ctx.frame.height), series, ctx.density, { legend: true }),
    ...carSettings.build({ ...ctx, frame: rect(ctx.frame.left + plotWidth + d.gapX, ctx.frame.top, gridWidth, ctx.frame.height), prefix: `${ctx.prefix}settings.` }),
  ];
}
