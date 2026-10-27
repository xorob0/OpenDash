/**
 * A change notification on the BMW (#203), the canvas's `change` state: the time difference and the
 * lap time give way to one tile in the TC colour, lit from above, with the setting's short name in
 * bold over its new value at the display's largest size. It takes the same room on every page of
 * zone C, the lower 172 of the race page, so that the driver's eye finds it where it always is.
 */
import type { Item } from '../../generator.ts';
import { withMoreBindings } from '../../bind.ts';
import { changeNotificationVisible } from '../../components/changeNotification.ts';
import { TRACKED_VALUES, trackedText } from '../../second/tracked.ts';
import type { FaceContext } from '../drawing.ts';
import { bmwFace, raceRows } from './geometry.ts';
import { bmwColour, box, fitSize, stackedRuns } from './register.ts';


export function bmwChangeNotifications(ctx: FaceContext): Item[] {
  const face = bmwFace(ctx.layout);
  const room = raceRows(face.zoneC, face).lower;
  return TRACKED_VALUES.map((value) => {
    const name = `notice.${value.id}`;
    const { items, inner } = box(name, room, { fill: bmwColour('tc') });
    const title = value.strip.toUpperCase();
    const titleSize = Math.min(fitSize(Math.round(30 * face.s), inner.width - 8, [title], true), Math.floor(inner.height / 4));
    const valueSize = Math.min(fitSize(Math.round(104 * face.s), inner.width - 8, [value.widest]), Math.floor((inner.height - titleSize * 1.1) / 1.05));
    const children: Item[] = [
      ...items,
      ...stackedRuns(name, inner, [
        { id: 'label', text: title, size: titleSize, bold: true, lh: 1.1 },
        { id: 'value', text: value.sample, size: valueSize, lh: 1, bind: trackedText(value), widest: value.widest },
      ], bmwColour('tileInk')),
    ];
    return withMoreBindings({ kind: 'layer', name, children }, { Visible: changeNotificationVisible(value.id) });
  });
}
