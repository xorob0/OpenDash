/**
 * flagRing: the flag indicator of a round face. One Layer per flag with the same priority chain
 * as the strip (black, chequered, yellow, blue, white, green), each holding a 12 px ring on the
 * outer edge of the face in the flag colour. Black is a 3 px ring in text.primary; chequered is
 * as many small white checks as the rim takes at one square of the artboards' board each, rotated
 * around it, since SimHub has no arcs; yellow flashes at 2 Hz. No labels: the ring is the whole
 * message.
 */
import type { Hex, Item, LayerItem } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { onCircle, type Circle, type Size } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { ring } from '../elements/ring.ts';
import { ds } from '../tokens.ts';
import { BLACK_FLAG_BORDER, FLAG_BLINK_MS, flagVisible, type FlagProperty } from './flagStrip.ts';

/**
 * One check, along the rim and across it. Both round artboards tile the ring with the same
 * `0 0 / 32px 32px` conic board, whose square is half the tile, so a check is 16 px of rim however
 * large the face; across the rim it is the ring's own width.
 */
export const CHEQUER_SIZE: Size = { width: 16, height: 12 };

/** Whole-pixel rounding moves a rect half a pixel on each axis, so 1 / sqrt(2) in any direction. */
const RIM_SLACK = Math.SQRT1_2;

/**
 * The circle the checks ride. A check is a chord of the ring rather than an arc of it, so its
 * corners reach further out than its outer edge does: seating that edge on the face's rim leaves
 * the corners outside it, and outside the canvas wherever a check falls near twelve, three, six or
 * nine o'clock. The corners sit on the rim instead, which costs the ring a fraction of a pixel of
 * its width and keeps every check on the face.
 */
export const chequerRim = (face: Circle): Circle => ({
  ...face,
  r: Math.sqrt((face.r - RIM_SLACK) ** 2 - (CHEQUER_SIZE.width / 2) ** 2) - CHEQUER_SIZE.height / 2,
});

/**
 * As many checks as the rim takes with a dark square of the same width after each, which is what
 * makes the ring read as a chequer rather than as spaced dots. Rounded to an even number so that
 * six o'clock falls on the ground as twelve o'clock does.
 */
export const chequerCount = (face: Circle): number => 2 * Math.round((Math.PI * chequerRim(face).r) / (2 * CHEQUER_SIZE.width));

/** Degrees from one check to the next: its own square and the dark one after it. */
export const chequerStep = (face: Circle): number => 360 / chequerCount(face);

function solidRing(face: Circle, prefix: string, id: string, flag: FlagProperty, color: Hex, blink: boolean): LayerItem {
  return {
    kind: 'layer',
    name: `${prefix}.${id}`,
    children: [ring(`${prefix}.${id}.ring`, face, ds.indicator.flagRing.width, color)],
    ...(blink ? { blink: { enabled: true, delayMs: FLAG_BLINK_MS } } : {}),
    ...withBindings({ Visible: flagVisible(flag) }),
  };
}

function blackRing(face: Circle, prefix: string): LayerItem {
  return {
    kind: 'layer',
    name: `${prefix}.black`,
    children: [ring(`${prefix}.black.ring`, face, BLACK_FLAG_BORDER, ds.purpose.flag.black)],
    ...withBindings({ Visible: flagVisible('Flag_Black') }),
  };
}

function chequeredRing(face: Circle, prefix: string): LayerItem {
  const rim = chequerRim(face);
  const count = chequerCount(face);
  const step = chequerStep(face);
  const children: Item[] = [];
  for (let k = 0; k < count; k++) {
    // Half a step in, so that twelve o'clock falls on the ground as it does at the band's left
    // edge: the round face and the strip are then the same board rather than each other's inverse.
    const angle = (k + 0.5) * step;
    children.push(band(`${prefix}.chequered.c${String(k).padStart(2, '0')}`, onCircle(rim, angle, CHEQUER_SIZE), ds.purpose.flag.chequer, { rotation: angle }));
  }
  return { kind: 'layer', name: `${prefix}.chequered`, children, ...withBindings({ Visible: flagVisible('Flag_Checkered') }) };
}

export function flagRing(face: Circle, prefix = 'flag'): Item[] {
  return [
    blackRing(face, prefix),
    chequeredRing(face, prefix),
    solidRing(face, prefix, 'yellow', 'Flag_Yellow', ds.purpose.flag.yellow, true),
    solidRing(face, prefix, 'blue', 'Flag_Blue', ds.purpose.flag.blue, false),
    solidRing(face, prefix, 'white', 'Flag_White', ds.purpose.flag.white, false),
    solidRing(face, prefix, 'green', 'Flag_Green', ds.purpose.flag.green, false),
  ];
}
