/**
 * Module 8, Pit view: what the next stop is set to do. The corner toggles, the fast repair and the
 * tear-off come from iRacing's pit service bit field, read arithmetically because NCalc has no
 * bitwise operators.
 *
 * Pit time counts up while the car is in the lane and shows the last stop's duration otherwise, so
 * the field is useful both during a stop and after it.
 */
import type { Item } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { measureText } from '../design/advances.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { densityOf } from '../second/density.ts';
import { levelGauge } from '../second/gauge.ts';
import { stack } from '../second/layout.ts';
import { CHARS, NO_VALUE, PIT_SERVICE_BITS, fuelPercent, fuelUnit, inPitSeconds, isInPitLane, lastPitDuration, pitRefuelLitres, pitServiceFlag } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { blockRow, defineModule, fieldsRow, fld, pageKeeps } from './module.ts';

const { fmt, iff, isNull, str, not } = ncalc;

/** The four the catalogue writes as one summary, `Tyres · RIGHTS`, and drops in a narrow box. */
export const CORNER_TOGGLES: readonly string[] = ['FrontLeft', 'FrontRight', 'RearLeft', 'RearRight'];

/** The six toggles of the pit box, in the order the black box lists them. */
export const TOGGLES: { id: string; text: string; bit: number }[] = [
  { id: 'FrontLeft', text: 'FL', bit: PIT_SERVICE_BITS.FrontLeft },
  { id: 'FrontRight', text: 'FR', bit: PIT_SERVICE_BITS.FrontRight },
  { id: 'RearLeft', text: 'RL', bit: PIT_SERVICE_BITS.RearLeft },
  { id: 'RearRight', text: 'RR', bit: PIT_SERVICE_BITS.RearRight },
  { id: 'fastRepair', text: 'FAST REPAIR', bit: PIT_SERVICE_BITS.fastRepair },
  { id: 'tearOff', text: 'TEAR-OFF', bit: PIT_SERVICE_BITS.tearOff },
];

export const pitView = defineModule('pitView', (ctx) => {
  const d = densityOf(ctx.density);
  const marker = 8;
  const toggleHeight = Math.max(marker, d.labelSm);
  const gaugeHeight = d.bar;
  const refuel = pitRefuelLitres();

  // Which corners are being changed is the page's own summary, and it is what a box one column
  // wide drops: the fast repair and the tear-off stay at every shape, the corners do not.
  const toggles = pageKeeps('tyres', ctx) ? TOGGLES : TOGGLES.filter((t) => !CORNER_TOGGLES.includes(t.id));
  /** Each toggle with the width its label really takes, so the wrap below can be measured. */
  const measured = toggles.map((t) => ({ ...t, textWidth: Math.ceil(measureText('BarlowMedium', t.text, d.labelSm)) + 2 }));
  /** The toggles wrapped to the frame, in the order the black box lists them. */
  const toggleRows: (typeof measured)[] = [];
  {
    let row: typeof measured = [];
    let used = 0;
    for (const toggle of measured) {
      const w = 16 + toggle.textWidth;
      const needed = row.length === 0 ? w : used + d.gapX / 2 + w;
      if (row.length > 0 && needed > ctx.frame.width) {
        toggleRows.push(row);
        row = [toggle];
        used = w;
      } else {
        row.push(toggle);
        used = needed;
      }
    }
    if (row.length > 0) toggleRows.push(row);
  }
  return stack(
    ctx.frame,
    [
      fieldsRow(
        [
          fld(ctx, 'refuel', 'Refuel', {
            sample: '12.6',
            bind: iff(isNull(refuel), str(NO_VALUE), fmt(refuel, '0.0')),
            chars: CHARS.fuel,
            fs: d.hero,
            color: ds.purpose.fuel.low,
            follower: { text: 'L', bind: fuelUnit() },
          }),
          fld(ctx, 'pitTime', 'Pit time', {
            sample: '24.3',
            bind: fmt(iff(isInPitLane(), inPitSeconds(), lastPitDuration()), '0.0'),
            chars: CHARS.consumption,
            fs: d.mid,
            follower: { text: 's' },
          }),
        ],
        ctx,
      ),
      // The six toggles wrap rather than running off the right edge. They used to be laid out on
      // one line whatever the width, which is fine at 600 and puts TEAR-OFF 8 px past the edge at
      // 360 -- where WPF clips it to "TEAR-OF" and the row reads as a rendering fault. The wrap is
      // in importance order, so a box too short for two lines loses the tear-off before a corner.
      blockRow(toggleRows.length * toggleHeight + Math.max(0, toggleRows.length - 1) * d.fieldGap, (bottom) => {
        const blockHeight = toggleRows.length * toggleHeight + Math.max(0, toggleRows.length - 1) * d.fieldGap;
        const top = bottom - blockHeight;
        const items: Item[] = [];
        toggleRows.forEach((row, rowIndex) => {
          const rowTop = top + rowIndex * (toggleHeight + d.fieldGap);
          let x = ctx.frame.left;
          for (const toggle of row) {
            const on = pitServiceFlag(toggle.bit);
            items.push(
              {
                ...band(`${ctx.prefix}${toggle.id}.on`, rect(x, rowTop + (toggleHeight - marker) / 2, marker, marker), ds.color.text.primary),
                ...withBindings({ Visible: on }),
              },
              {
                ...band(`${ctx.prefix}${toggle.id}.off`, rect(x, rowTop + toggleHeight / 2 - 1, 12, 2), ds.color.text.dim),
                ...withBindings({ Visible: not(on) }),
              },
              label(`${ctx.prefix}${toggle.id}.label`, toggle.text, x + 16, rowTop + (toggleHeight - d.labelSm) / 2, toggle.textWidth, { size: d.labelSm }),
            );
            x += 16 + toggle.textWidth + d.gapX / 2;
          }
        });
        return items;
      }),
      blockRow(gaugeHeight, (bottom) => [
        levelGauge(`${ctx.prefix}gauge`, rect(ctx.frame.left, bottom - gaugeHeight, ctx.frame.width, gaugeHeight), fuelPercent(), { value: 62 }),
      ]),
    ],
    ctx.density,
  );
});
