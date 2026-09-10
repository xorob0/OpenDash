/**
 * flagRing: the flag indicator of a round face. One Layer per flag with the same priority chain
 * as the strip (black, chequered, yellow, blue, white, green), each holding a 12 px ring on the
 * outer edge of the face in the flag colour. Black is a 3 px ring in text.primary; chequered is
 * 24 small white checks rotated around the rim at 15 degree steps, since SimHub has no arcs;
 * yellow flashes at 2 Hz. No labels: the ring is the whole message.
 */
import type { Hex, Item, LayerItem } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { onCircle, type Circle, type Size } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { ring } from '../elements/ring.ts';
import { ds } from '../tokens.ts';
import { BLACK_FLAG_BORDER, FLAG_BLINK_MS, flagVisible, type FlagProperty } from './flagStrip.ts';

/** Checks of the chequered ring: how many, how far apart, and their size (along the rim, across it). */
export const CHEQUER_COUNT = 24;
export const CHEQUER_STEP = 360 / CHEQUER_COUNT;
export const CHEQUER_SIZE: Size = { width: 20, height: 12 };

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
  const rim: Circle = { ...face, r: face.r - CHEQUER_SIZE.height / 2 };
  const children: Item[] = [];
  for (let k = 0; k < CHEQUER_COUNT; k++) {
    const angle = k * CHEQUER_STEP;
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
