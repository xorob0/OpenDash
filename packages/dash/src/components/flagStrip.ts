/**
 * flagStrip: one Layer per flag, sharing the band anatomy and differing in colour, label and
 * behaviour. One shows at a time in priority order black, chequered, yellow, blue, white, green;
 * nothing is drawn when no flag is out. Black is outlined, since a black band on this face is
 * invisible; chequered is a hard-edged 20 px check pattern with no label; yellow flashes at 2 Hz.
 */
import type { Hex, Item, LayerItem, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { ds, TRANSPARENT } from '../tokens.ts';

const { game, eq, and, num } = ncalc;

/** Border of the black flag's outline. */
export const BLACK_FLAG_BORDER = 3;
/** Half period of the yellow flag's flash in ms (250 at 2 Hz). */
export const FLAG_BLINK_MS = Math.round(1000 / ds.indicator.flagBand.flashHz / 2);

/** SimHub flag properties in priority order. */
export const FLAG_PRIORITY = ['Flag_Black', 'Flag_Checkered', 'Flag_Yellow', 'Flag_Blue', 'Flag_White', 'Flag_Green'] as const;
export type FlagProperty = (typeof FLAG_PRIORITY)[number];

/** `[Flag_X] = 1` and every higher-priority flag `= 0`. */
export function flagVisible(flag: FlagProperty): Expr {
  const index = FLAG_PRIORITY.indexOf(flag);
  const higher = FLAG_PRIORITY.slice(0, index).map((f) => eq(game(f), num(0)));
  return and(...higher, eq(game(flag), num(1)));
}

const labelY = (frame: Rect): number => frame.top + (frame.height - ds.size.label) / 2;

function solidFlag(frame: Rect, prefix: string, id: string, flag: FlagProperty, color: Hex, text: string, blink: boolean): LayerItem {
  return {
    kind: 'layer',
    name: `${prefix}.${id}`,
    children: [
      band(`${prefix}.${id}.band`, frame, color),
      label(`${prefix}.${id}.label`, text, frame.left, labelY(frame), frame.width, { color: ds.purpose.flag.onFlag, hAlign: 'center' }),
    ],
    ...(blink ? { blink: { enabled: true, delayMs: FLAG_BLINK_MS } } : {}),
    ...withBindings({ Visible: flagVisible(flag) }),
  };
}

function blackFlag(frame: Rect, prefix: string): LayerItem {
  return {
    kind: 'layer',
    name: `${prefix}.black`,
    children: [
      band(`${prefix}.black.band`, frame, TRANSPARENT, { border: { color: ds.purpose.flag.black, width: BLACK_FLAG_BORDER } }),
      label(`${prefix}.black.label`, 'BLACK FLAG', frame.left, labelY(frame), frame.width, { color: ds.color.text.primary, hAlign: 'center' }),
    ],
    ...withBindings({ Visible: flagVisible('Flag_Black') }),
  };
}

function chequeredFlag(frame: Rect, prefix: string): LayerItem {
  const check = frame.height / 2;
  const columns = Math.ceil(frame.width / check);
  const children: Item[] = [band(`${prefix}.chequered.band`, frame, ds.color.surface.base)];
  for (let row = 0; row < 2; row++) {
    for (let col = row % 2; col < columns; col += 2) {
      const width = Math.min(check, frame.left + frame.width - (frame.left + col * check));
      children.push(
        band(`${prefix}.chequered.r${row}c${String(col).padStart(2, '0')}`, rect(frame.left + col * check, frame.top + row * check, width, check), ds.purpose.flag.chequer),
      );
    }
  }
  return { kind: 'layer', name: `${prefix}.chequered`, children, ...withBindings({ Visible: flagVisible('Flag_Checkered') }) };
}

export function flagStrip(frame: Rect, prefix = 'flag'): Item[] {
  return [
    blackFlag(frame, prefix),
    chequeredFlag(frame, prefix),
    solidFlag(frame, prefix, 'yellow', 'Flag_Yellow', ds.purpose.flag.yellow, 'YELLOW FLAG', true),
    solidFlag(frame, prefix, 'blue', 'Flag_Blue', ds.purpose.flag.blue, 'BLUE FLAG', false),
    solidFlag(frame, prefix, 'white', 'Flag_White', ds.purpose.flag.white, 'WHITE FLAG', false),
    solidFlag(frame, prefix, 'green', 'Flag_Green', ds.purpose.flag.green, 'GREEN FLAG', false),
  ];
}
