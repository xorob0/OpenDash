/**
 * Module 2, Delta: the live delta to the reference lap, as a number and as a bar that grows from
 * the centre, then the same comparison sector by sector. Which reference is a plugin setting, and
 * the caption says which one is in force so the number is never ambiguous.
 *
 * The bar covers two seconds either side. Beyond that the fill simply stays at the end: a driver
 * two seconds off does not need to know whether it is 2.1 or 2.4. It stands ten high whatever the
 * density, because the canvas draws one delta bar rather than a companion one and a zone one.
 *
 * The bar and the scale are declared parts rather than fixtures. A zone narrow enough for one
 * column drops both and keeps the number and its three sectors, which is the catalogue's own
 * answer at `tall narrow`: the reading the page exists for survives, and the picture of it goes.
 */
import { ncalc } from '../generator.ts';
import { ds } from '../tokens.ts';
import { measureText } from '../design/advances.ts';
import { label } from '../elements/label.ts';
import { rule } from '../elements/rule.ts';
import { rect } from '../design/geometry.ts';
import { densityOf } from '../second/density.ts';
import { fieldWidth } from '../second/field.ts';
import { centreZeroGauge } from '../second/gauge.ts';
import { SECTORS, sectorColour } from '../second/sectors.ts';
import { blockRow, defineModule, fieldsRow, fld, pageKeeps, shapeIn } from './module.ts';
import { archetypeOf } from './shedding.ts';
import { stack, type StackRow } from '../second/layout.ts';
import { CHARS, deltaColour, referenceDelta, referenceLabel, sectorDelta } from '../second/values.ts';

const { signed } = ncalc;

/** Seconds either side of zero the bar covers. */
export const DELTA_RANGE = 2;

/**
 * Gap between the delta and the caption that names what it is against. Ten pixels on the canvas
 * where a unit takes six, which is why the follower carries it rather than `UNIT_GAP`.
 */
const CAPTION_GAP = 10;

/** Gap between the rows of this page, which the canvas draws at 14 rather than the density's. */
const ROW_GAP = 14;

/** Gap between the three sector deltas, which the canvas draws tighter than a rank of readings. */
const SECTOR_GAP = 12;

const SECTOR_SAMPLES = ['−0.29', '−0.23', '+0.31'] as const;

/** The separator between the scale and the sector deltas: one pixel of surface.raised. */
const RULE_HEIGHT = 1;

/** The longest caption the reference setting can produce, which is what its box is measured by. */
const CAPTION_WIDEST = 'VS ALL-TIME BEST';

export const delta = defineModule('delta', (ctx) => {
  const d = densityOf(ctx.density);
  const value = referenceDelta();
  const barHeight = 10;
  const scaleHeight = d.labelSm;
  // Two drawings of one thing: the caption on the number's baseline ten pixels after it, which is
  // what every face with room across draws, and the caption under the number at two pixels, which
  // is the nano's. A compact zone always takes the stacked form, and so does any box the pair
  // would not fit side by side, because a caption is not worth pushing the number off the edge.
  const number = { sample: '−0.21', bind: signed(value, '0.00'), chars: CHARS.delta, fs: d.hero, colorBind: deltaColour(value) };
  const beside = fld(ctx, 'delta', '', { ...number, follower: { text: 'VS SESSION BEST', widest: CAPTION_WIDEST, bind: referenceLabel(), gap: CAPTION_GAP, size: d.label } });
  const below = fld(ctx, 'delta', 'VS SESSION BEST', number, { labelBind: referenceLabel(), labelWidest: CAPTION_WIDEST, labelBelow: true });
  const captionBelow = ctx.density === 'compact' || fieldWidth(beside, ctx.density) > ctx.frame.width;
  // 34 px on both of the canvas's ramps, which is the small rank of the companion and the middle
  // one of a zone; a compact zone steps the pair down together.
  const sectorSize = ctx.density === 'companion' ? d.small : d.mid;
  const rebuild = (row: StackRow | undefined): StackRow | undefined => (row === undefined ? undefined : ruled(row));
  /**
   * The rule and the rank under it are one row, not two.
   *
   * A 1 px line with nothing beneath it is a line drawn for its own sake, and that is what a
   * separate row leaves behind: `rowsThatFit` takes rows off the bottom, so a 607 by 158 strip
   * dropped the three sectors and kept the rule that was there to separate them from the scale.
   * Travelling together, the two are shed together and rebuilt together.
   */
  const ruled = (row: StackRow): StackRow => {
    if (row.height <= 0 || !pageKeeps('rule', ctx)) return row;
    const { fill, shed } = row;
    return {
      ...row,
      height: row.height + ROW_GAP + RULE_HEIGHT,
      draw: (bottom) => [rule(`${ctx.prefix}rule`, ctx.frame.left, bottom - row.height - ROW_GAP, ctx.frame.width, RULE_HEIGHT), ...row.draw(bottom)],
      ...(fill ? { fill: { ...fill, at: (factor: number) => rebuild(fill.at(factor)) } } : {}),
      ...(shed ? { shed: { ...shed, without: (ids: readonly string[]) => rebuild(shed.without(ids)) } } : {}),
    };
  };
  return stack(
    ctx.frame,
    [
      fieldsRow([captionBelow ? below : beside], ctx),
      ...(pageKeeps('bar', ctx)
        ? [blockRow(barHeight + 12, (bottom) => centreZeroGauge(`${ctx.prefix}bar`, rect(ctx.frame.left, bottom - barHeight - 6, ctx.frame.width, barHeight), value, { range: DELTA_RANGE }))]
        : []),
      ...(pageKeeps('scale', ctx)
        ? [
            blockRow(scaleHeight, (bottom) => {
              const y = bottom - scaleHeight;
              const marks: { text: string; color?: string }[] = [
                { text: `−${DELTA_RANGE.toFixed(1)}` },
                { text: 'FASTER', color: ds.purpose.delta.faster },
                { text: '0' },
                { text: 'SLOWER', color: ds.purpose.delta.slower },
                { text: `+${DELTA_RANGE.toFixed(1)}` },
              ];
              // Space-between, as the canvas sets the row: the outer two on the ends of the bar
              // they graduate and the rest sharing what is left. Centring each on its own fraction
              // put the first and the last half off the track and then clamped them back.
              const widths = marks.map((mark) => Math.ceil(measureText('BarlowMedium', mark.text, d.labelSm)) + 2);
              const free = ctx.frame.width - widths.reduce((sum, w) => sum + w, 0);
              const step = free / Math.max(1, marks.length - 1);
              let x = ctx.frame.left;
              return marks.map((mark, i) => {
                const item = label(`${ctx.prefix}scale.${i}`, mark.text, Math.round(x), y, widths[i] ?? 0, { size: d.labelSm, color: mark.color as `#${string}` | undefined });
                x += (widths[i] ?? 0) + step;
                return item;
              });
            }),
          ]
        : []),
      ruled(
        fieldsRow(
          SECTORS.map((sector) =>
            fld(ctx, `s${sector}`, `S${sector}`, {
              sample: SECTOR_SAMPLES[sector - 1] ?? '0.00',
              bind: signed(sectorDelta(sector), '0.00'),
              chars: CHARS.delta,
              fs: sectorSize,
              colorBind: sectorColour(sector),
            }),
          ),
          ctx,
          SECTOR_GAP,
        ),
      ),
    ],
    ctx.density,
    // The one page the catalogue centres at three shapes and spreads at the fourth: a number, a
    // bar and a scale read as one object and are set as one, until a tall zone has room to put the
    // sectors on its bottom edge.
    { gap: ROW_GAP, justify: archetypeOf(shapeIn(ctx)) === 'tall' ? 'spaceBetween' : 'centre' },
  );
});
