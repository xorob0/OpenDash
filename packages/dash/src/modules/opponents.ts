/**
 * Module 16, Opponents: the one car ahead and the one car behind, with the gap large enough to
 * read at a glance and enough about them to know who they are.
 *
 * The gain-and-loss bar the design sheet draws is left out: it needs a history of the gap, which
 * neither SimHub nor a generated dashboard keeps. The gap itself, refreshed every frame, tells the
 * same story to anyone watching it for a second.
 */
import { ncalc } from '../generator.ts';
import { rect } from '../design/geometry.ts';
import { label } from '../elements/label.ts';
import { rule } from '../elements/rule.ts';
import { densityOf } from '../second/density.ts';
import { chip, chipText, chipWidth } from '../second/chip.ts';
import { fieldRowFitted } from '../second/field.ts';
import { stack } from '../second/layout.ts';
import { CHARS, carClass, carLastLap, carName, carNumber, carPosition, carRating, carRelativeGap, neighbour } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { blockRow, defineModule, fld } from './module.ts';
import type { Item } from '../generator.ts';
import type { ModuleContext } from './module.ts';

const { concat, str, fmt } = ncalc;

/** One neighbour: a heading, the gap and who it is, then a line of detail. */
function block(ctx: ModuleContext, id: string, offset: number, heading: string, colour: string): (bottom: number) => Item[] {
  const d = densityOf(ctx.density);
  const idx = neighbour(offset);
  const detailHeight = d.labelSm;
  return (bottom: number) => {
    const items: Item[] = [];
    const gapField = fld(ctx, `${id}.gap`, `${heading} · P4`, { sample: offset < 0 ? '-1.342' : '+0.722', bind: carRelativeGap(idx), chars: CHARS.relativeGap, fs: d.big, color: colour as `#${string}` }, {
      labelBind: concat(str(`${heading.toUpperCase()} · P`), fmt(carPosition(idx), '0')),
      labelWidest: `${heading.toUpperCase()} · P99`,
    });
    const nameField = fld(ctx, `${id}.name`, '', { sample: 'TSA', bind: carName(idx), chars: { digits: 10, specials: 0 }, fs: d.small });
    const numberField = fld(ctx, `${id}.num`, '', { sample: '#41', bind: carNumber(idx), chars: CHARS.carNumber, fs: d.small, color: ds.color.text.label });
    const valueBottom = bottom - detailHeight - d.fieldGap * 2;
    // The chip sits after the row, but never past the module's right edge: on a narrow page the
    // row already fills the box and the chip tucks against the edge instead of leaving it.
    const chipW = chipWidth(ctx.density);
    const row = fieldRowFitted([gapField, nameField, numberField], ctx.frame.left, valueBottom, ctx.frame.width - chipW - d.gapX / 2, ctx.density);
    const chipX = Math.min(ctx.frame.left + row.width + d.gapX / 2, ctx.frame.left + ctx.frame.width - chipW);
    items.push(...row.items);
    items.push(...chip(`${ctx.prefix}${id}.class`, 'GT3', chipX, valueBottom - d.chipHeight, ctx.density, { bind: chipText(carClass(idx)), width: chipW }));
    items.push(
      label(`${ctx.prefix}${id}.detail`, 'LAST 1:43.234 · RATING 3.1K', ctx.frame.left, bottom - detailHeight, ctx.frame.width, {
        size: d.labelSm,
        bind: concat(str('LAST '), carLastLap(idx), str(' · RATING '), carRating(idx)),
      }),
    );
    return items;
  };
}

export const opponents = defineModule('opponents', (ctx) => {
  const d = densityOf(ctx.density);
  const blockHeight = d.label + d.fieldGap + d.big + d.fieldGap * 2 + d.labelSm;
  return stack(
    ctx.frame,
    [
      blockRow(blockHeight, block(ctx, 'ahead', -1, 'Ahead', ds.purpose.delta.faster)),
      blockRow(1, (bottom) => [rule(`${ctx.prefix}rule`, ctx.frame.left, bottom - 1, ctx.frame.width, 1)]),
      blockRow(blockHeight, block(ctx, 'behind', 1, 'Behind', ds.purpose.delta.slower)),
    ],
    ctx.density,
    d.gapY,
  );
});
