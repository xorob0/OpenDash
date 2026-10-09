/** pitLimiter: a neutral block above the gear with a centred "Pit limiter" label, blinking at 2 Hz while the limiter is on. */
import type { Item, Rect } from '../generator.ts';
import { withMoreBindings } from '../bind.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { pitLimiterOn } from '../second/values.ts';
import { ds } from '../tokens.ts';

/** Half period of the blink in ms. */
export const PIT_LIMITER_BLINK_MS = 250;

export function pitLimiter(frame: Rect, prefix = 'pitLimiter'): Item[] {
  return [
    withMoreBindings({
      kind: 'layer',
      name: prefix,
      children: [
        band(`${prefix}.band`, frame, ds.purpose.pitLimiter),
        label(`${prefix}.label`, 'Pit limiter', frame.left, frame.top + (frame.height - ds.size.label) / 2, frame.width, {
          color: ds.color.surface.base,
          hAlign: 'center',
        }),
      ],
      blink: { enabled: true, delayMs: PIT_LIMITER_BLINK_MS },
    }, { Visible: pitLimiterOn() }),
  ];
}
