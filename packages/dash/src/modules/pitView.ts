/**
 * Module 8, Pit view: what the next stop is set to do. The corner toggles, the fast repair and the
 * tear-off come from iRacing's pit service bit field, read arithmetically because NCalc has no
 * bitwise operators.
 *
 * Pit time counts up while the car is in the lane and shows the last stop's duration otherwise, so
 * the field is useful both during a stop and after it.
 */
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
import { blockRow, defineModule, fieldsRow, fld } from './module.ts';

const { fmt, iff, isNull, str, not } = ncalc;

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
      blockRow(toggleHeight, (bottom) => {
        const top = bottom - toggleHeight;
        const items = [];
        let x = ctx.frame.left;
        for (const toggle of TOGGLES) {
          const on = pitServiceFlag(toggle.bit);
          const textWidth = Math.ceil(measureText('BarlowMedium', toggle.text, d.labelSm)) + 2;
          items.push(
            {
              ...band(`${ctx.prefix}${toggle.id}.on`, rect(x, top + (toggleHeight - marker) / 2, marker, marker), ds.color.text.primary),
              ...withBindings({ Visible: on }),
            },
            {
              ...band(`${ctx.prefix}${toggle.id}.off`, rect(x, top + toggleHeight / 2 - 1, 12, 2), ds.color.text.dim),
              ...withBindings({ Visible: not(on) }),
            },
            label(`${ctx.prefix}${toggle.id}.label`, toggle.text, x + 16, top + (toggleHeight - d.labelSm) / 2, textWidth, { size: d.labelSm }),
          );
          x += 16 + textWidth + d.gapX / 2;
        }
        return items;
      }),
      blockRow(gaugeHeight, (bottom) => [
        levelGauge(`${ctx.prefix}gauge`, rect(ctx.frame.left, bottom - gaugeHeight, ctx.frame.width, gaugeHeight), fuelPercent(), { value: 62 }),
      ]),
    ],
    ctx.density,
  );
});
