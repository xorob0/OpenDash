/**
 * Module 19, Lap history: your last laps with the delta to the session best.
 *
 * SimHub keeps ten previous laps as numbered properties, so the rows are a repeated layer that
 * builds its property name from its own repeat index. A row whose lap has no time is hidden, which
 * is what a driver on lap two should see: one row, not six empty ones.
 */
import type { Item, LayerItem } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { cells, monoWidth } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { densityOf } from '../second/density.ts';
import { rowCapacity } from '../second/table.ts';
import { CHARS, PREVIOUS_LAP_SLOTS, currentLap, hasTime, lapTime, previousLap, previousLapDelta } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineModule } from './module.ts';

const { concat, str, fmt, iff, gt, lt, abs, num, sub, repeatIndex, isnull } = ncalc;

/** A delta this far behind the session best is drawn in caution, and twice that in danger. */
export const DELTA_THRESHOLDS = { caution: 0.5, danger: 1 } as const;

export const lapHistory = defineModule('lapHistory', (ctx) => {
  const d = densityOf(ctx.density);
  const rowHeight = d.rowHeight;
  const rows = Math.max(1, Math.min(PREVIOUS_LAP_SLOTS, rowCapacity(ctx.frame, ctx.density, true, rowHeight)));
  // Row one is the most recent lap, which SimHub numbers 00, so the slot is the repeat index less one.
  const slot = sub(repeatIndex(), num(1));
  const time = previousLap(slot);
  const delta = isnull(previousLapDelta(slot), num(0));
  const lapNumber = sub(currentLap(), repeatIndex());
  const fs = d.small;
  const mono = cells('SemiBold', fs);
  const lapWidth = monoWidth(mono, { digits: 4, specials: 0 });
  const timeWidth = monoWidth(mono, CHARS.lapTime);
  const deltaWidth = monoWidth(mono, CHARS.delta);
  const top = ctx.frame.top + d.headerHeight;
  const timeX = ctx.frame.left + lapWidth + d.cellGap;
  const deltaX = ctx.frame.left + ctx.frame.width - deltaWidth;
  const deltaColour = iff(
    lt(abs(delta), num(0.0005)),
    str(ds.purpose.lap.sessionBest),
    iff(gt(delta, num(DELTA_THRESHOLDS.danger)), str(ds.purpose.delta.slower), iff(gt(delta, num(DELTA_THRESHOLDS.caution)), str(ds.purpose.fuel.low), str(ds.color.text.secondary))),
  );
  const children: Item[] = [
    band(`${ctx.prefix}row.rule`, rect(ctx.frame.left, top + rowHeight - 1, ctx.frame.width, 1), ds.color.surface.raised),
    numeral(`${ctx.prefix}row.lap`, 'L12', ctx.frame.left, top + (rowHeight - fs) / 2, fs, { digits: 4, specials: 0 }, {
      bind: concat(str('L'), fmt(lapNumber, '0')),
      color: ds.color.text.label,
      maxWidth: lapWidth + 4,
    }),
    numeral(`${ctx.prefix}row.time`, '1:42.905', timeX, top + (rowHeight - fs) / 2, fs, CHARS.lapTime, {
      bind: lapTime(time),
      colorBind: iff(lt(abs(delta), num(0.0005)), str(ds.purpose.lap.sessionBest), str(ds.color.text.primary)),
      maxWidth: timeWidth + 4,
    }),
    numeral(`${ctx.prefix}row.delta`, '+0.594', deltaX, top + (rowHeight - fs) / 2, fs, CHARS.delta, {
      bind: fmt(delta, '0.000', true),
      colorBind: deltaColour,
      maxWidth: deltaWidth,
    }),
  ];
  const row: LayerItem = { kind: 'layer', name: `${ctx.prefix}row`, children, ...withBindings({ Visible: hasTime(time) }) };
  return [
    label(`${ctx.prefix}head.lap`, 'LAP', ctx.frame.left, ctx.frame.top + (d.headerHeight - d.labelSm) / 2, lapWidth, { size: d.labelSm }),
    label(`${ctx.prefix}head.time`, 'TIME', timeX, ctx.frame.top + (d.headerHeight - d.labelSm) / 2, timeWidth, { size: d.labelSm }),
    label(`${ctx.prefix}head.delta`, 'Δ BEST', deltaX, ctx.frame.top + (d.headerHeight - d.labelSm) / 2, deltaWidth, { size: d.labelSm, hAlign: 'right' }),
    { kind: 'layer', name: `${ctx.prefix}rows`, children: [row], repetitions: rows - 1, repeatTopOffset: rowHeight, repeatLeftOffset: 0 },
  ];
});
