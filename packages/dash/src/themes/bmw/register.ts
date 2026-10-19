/**
 * The BMW register (#203): the colours the Bosch display adds, and the box every reading on it
 * stands in.
 *
 * The colours are `palette.bmw` in this theme's overlay and are read at draw time rather than when the
 * module loads, because `themes/drawings.ts` imports this file in every process and only the BMW's
 * has those tokens.
 *
 * The car draws two kinds of box, both with a shadow down and to the right: an outlined box, 2 px of
 * grey round a ground a step lighter than the glass, with its label in bold and its value in the ink;
 * and a filled box, the setting tiles and the alarms, a solid colour with its text in white. WPF draws
 * no blur, so the shadow is a solid rectangle of near-black under the box, offset by `SHADOW`; a box
 * is drawn that much smaller than the room it is given, so that its shadow stays inside the room.
 */
import type { Hex, Item, Rect } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { measureText } from '../../design/advances.ts';
import { rect } from '../../design/geometry.ts';
import { boxSlack } from '../../design/metrics.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { resolveToken } from '../../tokens.ts';
import { boxIn, SHADOW } from './geometry.ts';

export type BmwColour =
  | 'face' | 'ground' | 'ink' | 'tileInk' | 'edge' | 'shadow' | 'off' | 'green' | 'yellow' | 'red' | 'magenta'
  | 'tc' | 'abs' | 'map' | 'arb' | 'lit' | 'litEdge' | 'pillDim' | 'pillInk' | 'head' | 'alarm';

/** A colour this theme adds under `palette.bmw`, refused unless it resolves to `#RRGGBB`. */
export function bmwColour(name: BmwColour): Hex {
  const value = resolveToken(`palette.bmw.${name}`);
  if (typeof value !== 'string' || !/^#[0-9A-F]{6}$/.test(value)) throw new Error(`bmw: palette.bmw.${name} is not a #RRGGBB colour (${String(value)})`);
  return value as Hex;
}

/** An outlined box's border. */
export const EDGE = 2;
/** The corner every box rounds, which the canvas leaves square but the display's glass softens. */
export const RADIUS = 3;

/** The width a run of Barlow takes, with the slack WPF needs to draw its last glyph whole. */
export const runWidth = (text: string, size: number, bold = false): number => Math.ceil(measureText(bold ? 'BarlowBold' : 'BarlowMedium', text, size)) + boxSlack(size);

/** A line of `size` centred on the height of `box`. */
export const centredY = (box: Rect, size: number): number => box.top + (box.height - size * 1.2) / 2;

/** One reading a box shows: the design-time sample, its binding and the widest the binding can draw. */
export interface Reading {
  sample: string;
  bind?: Expr;
  widest: string;
}

export interface BoxStyle {
  /** A filled box's colour; an outlined box when absent. */
  fill?: Hex;
  /** The fill bound, for a box that changes colour with a state; the static `fill` is its design-time colour. */
  fillBind?: Expr;
  visible?: Expr;
  /** The text's colour where it is not the one the fill implies, as on an unlit tile. */
  ink?: Hex;
}

/** The box in `room`, its shadow kept inside it: the shadow and the box, and the rectangle the box takes. */
export function box(name: string, room: Rect, style: BoxStyle = {}): { items: Item[]; inner: Rect } {
  const r = boxIn(room);
  const shadow = band(`${name}.shadow`, rect(r.left + SHADOW.x, r.top + SHADOW.y, r.width, r.height), bmwColour('shadow'), { radius: RADIUS, visibleBind: style.visible });
  const face = style.fill
    ? withMoreBindings(band(`${name}.box`, r, style.fill, { radius: RADIUS, visibleBind: style.visible }), { BackgroundColor: style.fillBind })
    : band(`${name}.box`, r, bmwColour('ground'), { border: { color: bmwColour('edge'), width: EDGE }, radius: RADIUS, visibleBind: style.visible });
  const pad = style.fill ? 0 : EDGE;
  return { items: [shadow, face], inner: rect(r.left + pad, r.top + pad, r.width - 2 * pad, r.height - 2 * pad) };
}

/** The ink a box's text takes: white on a filled box, the display's grey on an outlined one. */
export const inkOf = (style: BoxStyle): Hex => style.ink ?? (style.fill ? bmwColour('tileInk') : bmwColour('ink'));

export interface StackedType {
  label: number;
  value: number;
}

/**
 * The car's reading in a box: the label in bold centred along the top and the value under it, at the
 * right as the outlined boxes write it or centred as the tiles do. The value is cut to the box's width;
 * the caller sizes the type so that the widest reading fits.
 */
export function stackedBox(name: string, room: Rect, title: string, reading: Reading, type: StackedType, style: BoxStyle = {}, align: 'right' | 'center' = 'right'): Item {
  const { items, inner } = box(name, room, style);
  const ink = inkOf(style);
  const pad = Math.max(4, Math.round(type.value * 0.2));
  const labelH = Math.ceil(type.label * 1.2);
  const valueH = Math.ceil(type.value * 1.2);
  const free = inner.height - labelH - valueH;
  const top = inner.top + Math.max(0, Math.floor(free / 3));
  const valueTop = Math.min(inner.top + inner.height - valueH, top + labelH + Math.max(0, Math.floor(free / 3)));
  const width = inner.width - 2 * pad;
  const children: Item[] = [
    ...items,
    label(`${name}.name`, title, inner.left + pad, top, width, { size: type.label, weight: 'Bold', color: ink, hAlign: 'center' }),
    label(`${name}.value`, reading.sample, inner.left + pad, valueTop, width, { size: type.value, color: ink, hAlign: align, bind: reading.bind, widest: reading.widest }),
  ];
  return withMoreBindings({ kind: 'layer', name, children }, { Visible: style.visible });
}

/** Whether a stacked box of `room` holds its label and value at `type`. */
export const stackedFits = (room: Rect, title: string, widest: string, type: StackedType, filled = false): boolean => {
  const pad = Math.max(4, Math.round(type.value * 0.2));
  const inner = boxIn(room);
  const edge = filled ? 0 : 2 * EDGE;
  const width = inner.width - edge - 2 * pad;
  const height = inner.height - edge;
  return runWidth(widest, type.value) <= width && runWidth(title, type.label, true) <= width && Math.ceil(type.label * 1.2) + Math.ceil(type.value * 1.2) <= height;
};

/** The largest type, at the ratio of `type`, down to `floor` of it, at which a stacked box of `room` holds every one of `rows`. */
export function stackedTypeFor(room: Rect, rows: readonly { title: string; widest: string }[], type: StackedType, floor = 0.4, filled = false): StackedType | undefined {
  for (let scale = 1; scale >= floor - 1e-9; scale -= 0.02) {
    const t = { label: Math.max(8, Math.floor(type.label * scale)), value: Math.max(8, Math.floor(type.value * scale)) };
    if (rows.every((row) => stackedFits(room, row.title, row.widest, t, filled))) return t;
  }
  return undefined;
}
