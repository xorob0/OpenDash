/**
 * Module 2, Delta: the live delta to the reference lap, as a number and as a bar that grows from
 * the centre. Which reference is a plugin setting, and the label says which one is in force so
 * the number is never ambiguous.
 *
 * The bar covers two seconds either side. Beyond that the fill simply stays at the end: a driver
 * two seconds off does not need to know whether it is 2.1 or 2.4. It stands ten high whatever the
 * density, because the canvas draws one delta bar rather than a companion one and a zone one.
 */
import { ncalc } from '../generator.ts';
import { ds } from '../tokens.ts';
import { measureText } from '../design/advances.ts';
import { label } from '../elements/label.ts';
import { rect } from '../design/geometry.ts';
import { densityOf } from '../second/density.ts';
import { centreZeroGauge } from '../second/gauge.ts';
import { blockRow, defineModule, fieldsRow, fld } from './module.ts';
import { stack } from '../second/layout.ts';
import { CHARS, deltaColour, referenceDelta, referenceLabel } from '../second/values.ts';

const { signed } = ncalc;

/** Seconds either side of zero the bar covers. */
export const DELTA_RANGE = 2;

export const delta = defineModule('delta', (ctx) => {
  const d = densityOf(ctx.density);
  const value = referenceDelta();
  const barHeight = 10;
  const scaleHeight = d.labelSm;
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          fld(ctx, 'delta', 'VS SESSION BEST', { sample: '\u22120.21', bind: signed(value, '0.00'), chars: CHARS.delta, fs: d.hero, colorBind: deltaColour(value) }, {
            labelBind: referenceLabel(),
            labelWidest: 'VS ALL-TIME BEST',
          }),
        ],
        ctx,
      ),
      blockRow(barHeight + 12, (bottom) => centreZeroGauge(`${ctx.prefix}bar`, rect(ctx.frame.left, bottom - barHeight - 6, ctx.frame.width, barHeight), value, { range: DELTA_RANGE })),
      blockRow(scaleHeight, (bottom) => {
        const y = bottom - scaleHeight;
        const marks: { text: string; at: number; color?: string }[] = [
          { text: `\u2212${DELTA_RANGE.toFixed(1)}`, at: 0 },
          { text: 'FASTER', at: 0.25, color: ds.purpose.delta.faster },
          { text: '0', at: 0.5 },
          { text: 'SLOWER', at: 0.75, color: ds.purpose.delta.slower },
          { text: `+${DELTA_RANGE.toFixed(1)}`, at: 1 },
        ];
        return marks.map((mark, i) => {
          const width = Math.ceil(measureText('BarlowMedium', mark.text, d.labelSm)) + 2;
          const centre = ctx.frame.left + mark.at * ctx.frame.width;
          const x = Math.round(Math.min(Math.max(centre - width / 2, ctx.frame.left), ctx.frame.left + ctx.frame.width - width));
          return label(`${ctx.prefix}scale${i}`, mark.text, x, y, width, { size: d.labelSm, color: mark.color as `#${string}` | undefined });
        });
      }),
    ],
    ctx.density,
  );
});
